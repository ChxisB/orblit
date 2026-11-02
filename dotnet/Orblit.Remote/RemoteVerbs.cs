using System.Diagnostics;
using System.Globalization;
using Orblit.Json;
using Args = System.Collections.Generic.IReadOnlyDictionary<string, object?>;
using Rows = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyDictionary<string, object?>>;

namespace Orblit.Remote;

/// <summary>
/// The verbs of the control surface, as one JSON-RPC request in and one answer out. It knows
/// nothing of sockets, so a test can call it directly.
/// </summary>
/// <remarks>
/// A verb reads and writes the world on the thread it resumes on. A server that takes requests
/// on other threads runs <see cref="HandleAsync"/> on the game loop's synchronization context.
/// </remarks>
public sealed class RemoteVerbs
{
    private const int DefaultDrawLimit = 100;
    private const int DefaultTailLines = 50;
    private const int PollMs = 10;

    private readonly RemoteHost _host;
    private readonly SnapshotStore _snapshots = new();
    private readonly Dictionary<string, (bool Write, Func<Args, Task<object?>> Run)> _table;

    public RemoteVerbs(RemoteHost host)
    {
        _host = host;
        _table = new()
        {
            ["world.entities"] = (false, Sync(Entities)),
            ["entity.get"] = (false, Sync(Get)),
            ["entity.set"] = (true, Sync(Set)),
            ["time.pause"] = (false, Sync(Pause)),
            ["time.resume"] = (false, Sync(Resume)),
            ["time.step"] = (false, Step),
            ["world.wait"] = (false, Wait),
            ["world.snapshot"] = (false, Snapshot),
            ["world.diff"] = (false, Sync(Diff)),
            ["input.send"] = (true, Send),
            ["view.screenshot"] = (false, Screenshot),
            ["view.passes"] = (false, Passes),
            ["view.draws"] = (false, Draws),
            ["view.draw"] = (false, Draw),
            ["view.material"] = (false, Material),
            ["log.tail"] = (false, Sync(Tail)),
            ["perf.summary"] = (false, Perf),
        };
    }

    /// <summary>
    /// The answer to one request, as the JSON text to send back. Null for a notification, a
    /// request with no id, which is never answered.
    /// </summary>
    // shortcut: one request at a time, not a batch. Add arrays when a client needs several
    // answers in one round trip.
    public async Task<string?> HandleAsync(string message)
    {
        Args request;
        try
        {
            request = Parse(message);
        }
        catch (RemoteException error)
        {
            return Failure(null, error);
        }

        var id = request.GetValueOrDefault("id");
        try
        {
            var result = await Call(request);
            return id is null ? null : JsonText.Encode(Wire.Map(("jsonrpc", "2.0"), ("id", id), ("result", result)));
        }
        catch (RemoteException error)
        {
            return id is null ? null : Failure(id, error);
        }
#pragma warning disable CA1031 // A host callback can throw anything, and the debugger must outlive it.
        catch (Exception error)
#pragma warning restore CA1031
        {
            return id is null ? null : Failure(id, new RemoteException(RemoteException.Internal, error.Message));
        }
    }

    private static Args Parse(string message)
    {
        object? request;
        try
        {
            request = JsonText.Decode(message);
        }
        catch (FormatException)
        {
            throw new RemoteException(RemoteException.Parse, "The request is not JSON");
        }

        return request is Args map && map.GetValueOrDefault("id") is null or long or string
            ? map
            : throw new RemoteException(
                RemoteException.InvalidRequest,
                "A request is one object with a method and an int or text id");
    }

    private async Task<object?> Call(Args request)
    {
        var method = request.GetValueOrDefault("method");
        if (method is not string name || !_table.TryGetValue(name, out var verb))
        {
            throw new RemoteException(
                RemoteException.NoMethod,
                $"No method named {(method is string text ? text : JsonText.Encode(method))}");
        }

        if (verb.Write && _host.ReadOnly)
        {
            throw new RemoteException(RemoteException.ReadOnly, "This session is read only");
        }

        if ((request.GetValueOrDefault("params") ?? Wire.Map()) is not Args args)
        {
            throw new RemoteException(RemoteException.InvalidParams, "Params are an object of names, not a list");
        }

        try
        {
            return await verb.Run(args);
        }
        catch (ArgumentException error)
        {
            throw new RemoteException(RemoteException.InvalidParams, error.Message);
        }
        catch (InvalidOperationException error)
        {
            throw new RemoteException(RemoteException.NotFound, error.Message);
        }
    }

    private static string Failure(object? id, RemoteException error) => JsonText.Encode(Wire.Map(
        ("jsonrpc", "2.0"),
        ("id", id),
        ("error", Wire.Map(("code", error.Code), ("message", error.Message)))));

    private static Func<Args, Task<object?>> Sync(Func<Args, object?> run) => args => Task.FromResult(run(args));

    private static T Need<T>(T? part, string name)
        where T : class =>
        part ?? throw new RemoteException(RemoteException.Unavailable, $"This game did not give the surface {name}");

    private object? Entities(Args args)
    {
        var world = Need(_host.World, "a world");
        return world.Entities(Wire.Names(args.GetValueOrDefault("filter")))
            .Select(row => Wire.Map(("id", row.Id), ("components", row.Components)))
            .ToList();
    }

    private object? Get(Args args)
    {
        var id = Wire.Whole(args, "id");
        var components = Need(_host.World, "a world").Read(id)
            ?? throw new RemoteException(RemoteException.NotFound, $"Entity {id} is not alive");
        return Wire.Map(("id", id), ("components", Wire.Components(components)));
    }

    private object? Set(Args args)
    {
        Need(_host.World, "a world").Write(
            Wire.Whole(args, "id"),
            Wire.Text(args, "component"),
            Wire.Text(args, "field"),
            Wire.Number(args, "value"));
        return null;
    }

    private object? Pause(Args args)
    {
        Need(_host.Gate, "a time gate").Pause();
        return Wire.Map(("paused", true));
    }

    private object? Resume(Args args)
    {
        Need(_host.Gate, "a time gate").Resume();
        return Wire.Map(("paused", false));
    }

    // shortcut: two clients stepping at once share one count, so `stepped` can be high for one
    // of them. Give each step its own count when that matters.
    private async Task<object?> Step(Args args)
    {
        var gate = Need(_host.Gate, "a time gate");
        var frames = Wire.Count(args, "n");
        var before = gate.Passed;
        var done = gate.Step(frames);
        await WaitForFrames(gate, done);
        return Wire.Map(("paused", true), ("stepped", Math.Min(frames, gate.Passed - before)));
    }

    // The deadline restarts whenever a frame passes, so a long step on a slow game finishes and a
    // loop that never calls the gate is caught.
    private async Task WaitForFrames(TimeGate gate, Task done)
    {
        var left = gate.Pending;
        while (!await Finishes(done))
        {
            if (gate.Pending >= left)
            {
                gate.Pause();
                throw new RemoteException(
                    RemoteException.TimedOut,
                    $"The game passed no frame in {Milliseconds} ms. Its loop must call world.Tick(gate.Advance(delta)).");
            }

            left = gate.Pending;
        }
    }

    private async Task<bool> Finishes(Task done)
    {
        try
        {
            await done.WaitAsync(_host.Timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // F10: the deadline is real time, because a paused game has no clock of its own to wait by.
    private async Task<object?> Wait(Args args)
    {
        var world = Need(_host.World, "a world");
        var condition = WaitCondition.From(args);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            var (met, value, seen) = condition.Look(world);
            var waited = Stopwatch.GetElapsedTime(started);
            if (met)
            {
                return Wire.Map(
                    ("met", true),
                    ("value", Wire.Plain(value)),
                    ("waited_ms", (long)waited.TotalMilliseconds));
            }

            if (waited.TotalMilliseconds >= condition.TimeoutMs)
            {
                throw new RemoteException(RemoteException.TimedOut, $"Not met in {condition.TimeoutMs} ms: {seen}");
            }

            await Task.Delay(PollMs);
        }
    }

    // F5 is broken on purpose: a snapshot is kept so that a later diff can name it, and the
    // client needs its id back with the content.
    private async Task<object?> Snapshot(Args args)
    {
        var label = args.GetValueOrDefault("label") is null ? null : Wire.Text(args, "label");
        var world = Need(_host.World, "a world");
        var entities = new OrderedDictionary<string, object?>();
        foreach (var row in world.Entities([]))
        {
            if (world.Read(row.Id) is { } components)
            {
                entities[$"{row.Id}"] = Wire.Components(components);
            }
        }

        var extras = new OrderedDictionary<string, object?>();
        foreach (var (name, extra) in _host.Extras)
        {
            extras[name] = await FromHost(extra);
        }

        // Plain copies the snapshot, and fails here, before anything is kept.
        var snapshot = (OrderedDictionary<string, object?>)Wire.Plain(Wire.Map(("entities", entities), ("extras", extras)))!;
        _snapshots.Add(snapshot, label);
        var kept = Wire.Map(("id", _snapshots.LastId));
        foreach (var (key, value) in snapshot)
        {
            kept[key] = value;
        }

        return kept;
    }

    private object? Diff(Args args) => JsonDiff.Find(Kept(args, "a"), Kept(args, "b"))
        .Select(change => Wire.Map(("path", change.Path), ("from", change.From), ("to", change.To)))
        .ToList();

    private Args Kept(Args args, string name)
    {
        var id = Wire.Text(args, name);
        return _snapshots.Find(id) ?? throw new RemoteException(RemoteException.NotFound, $"No snapshot named {id}");
    }

    private async Task<object?> Send(Args args)
    {
        if (args.GetValueOrDefault("event") is not Args sent)
        {
            throw new ArgumentException("event must be an object");
        }

        var sink = Need(_host.Input, "an input sink");
        await FromHost(async () =>
        {
            await sink(sent);
            return true;
        });
        return null;
    }

    private async Task<object?> Screenshot(Args args) =>
        Wire.Map(("png", Convert.ToBase64String(await FromHost(Need(_host.Screenshot, "a screenshot source")))));

    private async Task<object?> Passes(Args args) =>
        Wire.Plain(await FromHost(Need(_host.Passes, "a pass list")));

    private async Task<object?> Draws(Args args)
    {
        var limit = args.GetValueOrDefault("limit") is null ? DefaultDrawLimit : Wire.Count(args, "limit");
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        var rows = await FromHost(Need(_host.Draws, "a draw list"));
        return Wire.Plain(Wire.Map(
            ("count", rows.Count),
            ("draws", rows.Take(limit).ToList()),
            ("truncated", rows.Count > limit)));
    }

    private async Task<object?> Draw(Args args)
    {
        var id = Wire.Text(args, "id");
        Rows rows = await FromHost(Need(_host.Draws, "a draw list"));
        var row = rows.FirstOrDefault(row => Equals(row.GetValueOrDefault("id"), id))
            ?? throw new RemoteException(RemoteException.NotFound, $"No draw {id}");
        return Wire.Plain(row);
    }

    private async Task<object?> Material(Args args)
    {
        var key = Wire.Whole(args, "key");
        var read = Need(_host.Material, "a material list");
        var row = await FromHost(() => read(key))
            ?? throw new RemoteException(RemoteException.NotFound, $"No material with key {key}");
        return Wire.Plain(row);
    }

    private object? Tail(Args args) => Need(_host.Log, "a log")
        .Tail(args.GetValueOrDefault("n") is null ? DefaultTailLines : Wire.Count(args, "n"));

    private async Task<object?> Perf(Args args) => await FromHost(Need(_host.Perf, "a perf summary"));

    private string Milliseconds => ((long)_host.Timeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    private Task<T> FromHost<T>(Func<T> call) => FromHost(() => Task.FromResult(call()));

    // The callback can hang, and its own bug must not read as a bad request, so an argument or
    // state error from it is internal.
    private async Task<T> FromHost<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().WaitAsync(_host.Timeout);
        }
        catch (TimeoutException)
        {
            throw new RemoteException(RemoteException.TimedOut, $"The game did not answer in {Milliseconds} ms");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new RemoteException(RemoteException.Internal, error.Message);
        }
    }
}
