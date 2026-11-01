using Orblit.Json;
using Args = System.Collections.Generic.IReadOnlyDictionary<string, object?>;

namespace Orblit.Remote;

/// <summary>
/// The control surface as named tools for an assistant, over the tool protocol. One message in,
/// one answer out. It knows nothing of stdin or of sockets, so a test can hand it a fake call.
/// </summary>
/// <remarks>
/// The one tool that writes, <c>set</c>, is left out of the list and refused when writes are not
/// allowed.
/// </remarks>
public sealed class ToolServer
{
    private const int DefaultLimit = 20;
    private const int DiffLimit = 200;

    // One call must finish inside the client's deadline of thirty seconds, and a game may run at
    // ten frames a second.
    private const int MaxFrames = 300;

    private static readonly string[] ProtocolVersions = ["2025-06-18", "2025-03-26", "2024-11-05"];

    private readonly Func<string, Args, Task<object?>> _call;
    private readonly Tool[] _visible;

    /// <summary>
    /// <paramref name="call"/> runs one verb of the control surface and answers its result, as
    /// <see cref="RemoteClient.Call"/> does. It throws <see cref="RemoteException"/> when the game
    /// refuses the call or cannot be reached.
    /// </summary>
    public ToolServer(Func<string, Args, Task<object?>> call, bool allowWrite)
    {
        _call = call;

        // The one place the write gate is read. The list and the call both use it, so a tool
        // cannot be listed and refused, or hidden and callable.
        _visible = [.. Tools().Where(tool => allowWrite || !tool.Writes)];
    }

    /// <summary>The version this server reports, which is the version of the package.</summary>
    internal static string Version { get; } = ReadVersion();

    /// <summary>
    /// The answer to one message, as JSON text. Null when nothing is owed: a notification, or a
    /// batch of them. A message with no id is never run, since it could not be answered, and no
    /// notification this server knows needs an action.
    /// </summary>
    // shortcut: one message at a time, so a long step holds up a ping behind it. Add concurrent
    // handling when a client needs it.
    public async Task<string?> HandleAsync(string message)
    {
        object? decoded;
        try
        {
            decoded = JsonText.Decode(message);
        }
        catch (FormatException)
        {
            return Failure(null, RemoteException.Parse, "The message is not JSON");
        }

        if (decoded is not IReadOnlyList<object?> batch)
        {
            return await One(decoded);
        }

        if (batch.Count == 0)
        {
            return Failure(null, RemoteException.InvalidRequest, "A batch holds at least one message");
        }

        var owed = new List<string>();
        foreach (var item in batch)
        {
            if (await One(item) is { } answer)
            {
                owed.Add(answer);
            }
        }

        return owed.Count == 0 ? null : $"[{string.Join(',', owed)}]";
    }

    private static string ReadVersion()
    {
        var version = typeof(ToolServer).Assembly.GetName().Version!;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string Reply(object? id, string key, object? body) =>
        JsonText.Encode(Wire.Map(("jsonrpc", "2.0"), ("id", id), (key, body)));

    private static string Failure(object? id, int code, string message) =>
        Reply(id, "error", Wire.Map(("code", code), ("message", message)));

    private static OrderedDictionary<string, object?> Kind(string type, string? note = null)
    {
        var kind = Wire.Map(("type", type));
        if (note is not null)
        {
            kind["description"] = note;
        }

        return kind;
    }

    private static OrderedDictionary<string, object?> Initialize(object? @params)
    {
        var asked = (@params as Args)?.GetValueOrDefault("protocolVersion");
        return Wire.Map(
            ("protocolVersion", asked is string version && ProtocolVersions.Contains(version) ? version : ProtocolVersions[0]),
            ("capabilities", Wire.Map(("tools", Wire.Map()))),
            ("serverInfo", Wire.Map(("name", "orblit-remote"), ("version", Version))));
    }

    private static List<object?> Text(object? value) =>
        [Wire.Map(("type", "text"), ("text", value as string ?? JsonText.Encode(value)))];

    private static OrderedDictionary<string, object?> Problem(string message) =>
        Wire.Map(("content", Text(message)), ("isError", true));

    private Tool[] Tools() =>
    [
        new()
        {
            Name = "observe",
            Description =
                "Lists entities with their component names, and answers the count and the first "
                + $"`limit` rows (default {DefaultLimit}). With `id`, answers that one entity with "
                + "every field of every component.",
            Properties = Wire.Map(
                ("id", Kind("integer", "One entity to read in full")),
                (
                    "filter",
                    Wire.Map(
                        ("type", "array"),
                        ("items", Kind("string")),
                        ("description", "Keep entities that have all of these components"))),
                ("limit", Kind("integer", "Rows to answer"))),
            Run = Observe,
        },
        new()
        {
            Name = "step",
            Description =
                $"Pauses the game and passes `n` frames (default 1, at most {MaxFrames}), each one "
                + "step delta of the game, a sixtieth of a second unless it chose another. Answers "
                + "the frames passed. The game stays paused until this session ends.",
            Properties = Wire.Map(("n", Wire.Map(("type", "integer"), ("minimum", 1), ("maximum", MaxFrames)))),
            Run = Step,

            // Not gated, but it runs game code and holds time, so it does not claim to change nothing.
            ReadOnlyHint = false,
        },
        new()
        {
            Name = "set",
            Description =
                "Writes to the running game. Sets one field of one component on an entity to a "
                + "number, and answers what it wrote.",
            Properties = Wire.Map(
                ("id", Kind("integer")),
                ("component", Kind("string")),
                ("field", Kind("string")),
                ("value", Kind("number"))),
            Run = Set,
            Required = ["id", "component", "field", "value"],
            Writes = true,
            ReadOnlyHint = false,
        },
        new()
        {
            Name = "snapshot",
            Description =
                "Records every entity and the host extras under a new id, and answers the id with "
                + "counts, not the content. The last eight are kept.",
            Properties = Wire.Map(),
            Run = Snapshot,
        },
        new()
        {
            Name = "diff",
            Description =
                $"Compares two snapshot ids and answers the count and the first {DiffLimit} paths "
                + "that differ. It is only as good as the two snapshots, and misses what neither one held.",
            Properties = Wire.Map(
                ("a", Kind("string", "The earlier snapshot id")),
                ("b", Kind("string", "The later snapshot id"))),
            Run = Diff,
            Required = ["a", "b"],
        },
        new()
        {
            Name = "screenshot",
            Description =
                "The render view as one PNG. It is a single frame, so it says nothing about "
                + "motion, and a paused game shows the same picture each time.",
            Properties = Wire.Map(),
            Run = Screenshot,
        },
        new()
        {
            Name = "perf",
            Description = "The phase times the game reports, as it reports them.",
            Properties = Wire.Map(),
            Run = Perf,
        },
    ];

    private async Task<string?> One(object? request)
    {
        if (request is not Args message)
        {
            return Failure(null, RemoteException.InvalidRequest, "A message is one object with a method");
        }

        var id = message.GetValueOrDefault("id");
        if (id is not (null or long or string))
        {
            return Failure(null, RemoteException.InvalidRequest, "An id is a whole number or text");
        }

        if (message.GetValueOrDefault("method") is not string method)
        {
            return Failure(id, RemoteException.InvalidRequest, "A message has a method");
        }

        if (id is null)
        {
            return null;
        }

        try
        {
            return Reply(id, "result", await Dispatch(method, message.GetValueOrDefault("params")));
        }
        catch (RemoteException error)
        {
            return Failure(id, error.Code, error.Message);
        }
    }

    private Task<object?> Dispatch(string method, object? @params) => method switch
    {
        "initialize" => Task.FromResult<object?>(Initialize(@params)),
        "ping" => Task.FromResult<object?>(Wire.Map()),
        "tools/list" => Task.FromResult<object?>(Wire.Map(("tools", List()))),
        "tools/call" => CallTool(@params),
        _ => throw new RemoteException(RemoteException.NoMethod, $"No method named {method}"),
    };

    private List<object?> List() => [.. _visible.Select(Describe)];

    private static OrderedDictionary<string, object?> Describe(Tool tool) => Wire.Map(
        ("name", tool.Name),
        ("description", tool.Description),
        (
            "inputSchema",
            Wire.Map(
                ("type", "object"),
                ("properties", tool.Properties),
                ("required", tool.Required.Cast<object?>().ToList()))),
        ("annotations", Wire.Map(("readOnlyHint", tool.ReadOnlyHint))));

    private async Task<object?> CallTool(object? @params)
    {
        var call = @params as Args;
        var name = call?.GetValueOrDefault("name");
        var tool = _visible.FirstOrDefault(candidate => candidate.Name == name as string);
        if (tool is null)
        {
            throw new RemoteException(
                RemoteException.InvalidParams,
                $"Unknown tool {(name is string text ? text : JsonText.Encode(name))}");
        }

        if ((call?.GetValueOrDefault("arguments") ?? Wire.Map()) is not Args args)
        {
            throw new RemoteException(RemoteException.InvalidParams, "Arguments are an object of names");
        }

        try
        {
            return Wire.Map(("content", await tool.Run(args)), ("isError", false));
        }
        catch (RemoteException error)
        {
            return Problem($"{error.Code}: {error.Message}");
        }
#pragma warning disable CA1031 // A game that answers in a shape this server does not know is the assistant's to read, so the session goes on.
        catch (Exception error)
#pragma warning restore CA1031
        {
            return Problem($"The game answered something unexpected: {error.Message}");
        }
    }

    // shortcut: the cuts happen here, after the game has answered in full. Give world.entities a
    // limit when a world is big enough for that to matter.
    private async Task<List<object?>> Observe(Args args)
    {
        if (args.GetValueOrDefault("id") is { } id)
        {
            return Text(await _call("entity.get", Wire.Map(("id", id))));
        }

        var filter = args.GetValueOrDefault("filter");
        var rows = (IReadOnlyList<object?>)(await _call("world.entities", filter is null ? Wire.Map() : Wire.Map(("filter", filter))))!;
        if ((args.GetValueOrDefault("limit") ?? (long)DefaultLimit) is not long limit || limit < 1)
        {
            throw new RemoteException(RemoteException.InvalidParams, "limit must be a whole number above 0");
        }

        return Text(Wire.Map(
            ("count", rows.Count),
            ("entities", rows.Take((int)Math.Min(limit, rows.Count)).ToList()),
            ("truncated", rows.Count > limit)));
    }

    private async Task<List<object?>> Step(Args args)
    {
        if ((args.GetValueOrDefault("n") ?? 1L) is not long n || n < 1 || n > MaxFrames)
        {
            throw new RemoteException(
                RemoteException.InvalidParams,
                $"n is a whole number from 1 to {MaxFrames}. Call step again for more.");
        }

        return Text(await _call("time.step", Wire.Map(("n", n))));
    }

    private async Task<List<object?>> Set(Args args)
    {
        var value = args.GetValueOrDefault("value");

        // JSON cannot carry these, so the call would fail while it was written.
        if (value is double real && !double.IsFinite(real))
        {
            throw new RemoteException(RemoteException.InvalidParams, "value must be a finite number");
        }

        var written = Wire.Map(
            ("id", args.GetValueOrDefault("id")),
            ("component", args.GetValueOrDefault("component")),
            ("field", args.GetValueOrDefault("field")),
            ("value", value));
        await _call("entity.set", written);
        return Text(written);
    }

    private async Task<List<object?>> Snapshot(Args args)
    {
        var kept = (Args)(await _call("world.snapshot", Wire.Map()))!;
        return Text(Wire.Map(
            ("id", kept["id"]),
            ("entities", ((Args)kept["entities"]!).Count),
            ("extras", ((Args)kept["extras"]!).Keys.Cast<object?>().ToList())));
    }

    private async Task<List<object?>> Diff(Args args)
    {
        var changes = (IReadOnlyList<object?>)(await _call(
            "world.diff",
            Wire.Map(("a", args.GetValueOrDefault("a")), ("b", args.GetValueOrDefault("b")))))!;
        return Text(Wire.Map(
            ("count", changes.Count),
            ("changes", changes.Take(DiffLimit).ToList()),
            ("truncated", changes.Count > DiffLimit)));
    }

    private async Task<List<object?>> Screenshot(Args args)
    {
        var shot = (Args)(await _call("view.screenshot", Wire.Map()))!;
        return [Wire.Map(("type", "image"), ("data", shot["png"]), ("mimeType", "image/png"))];
    }

    private async Task<List<object?>> Perf(Args args) => Text(await _call("perf.summary", Wire.Map()));

    private sealed class Tool
    {
        public required string Name { get; init; }

        public required string Description { get; init; }

        public required OrderedDictionary<string, object?> Properties { get; init; }

        public required Func<Args, Task<List<object?>>> Run { get; init; }

        public string[] Required { get; init; } = [];

        /// <summary>Gated behind the write flag.</summary>
        public bool Writes { get; init; }

        /// <summary>What a client may read as "this changes nothing".</summary>
        public bool ReadOnlyHint { get; init; } = true;
    }
}
