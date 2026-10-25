using Orblit.Json;

namespace Orblit.Remote.Tests;

/// <summary>
/// The Dart and C# control surfaces speak to each other. A client makes the same calls against a
/// server of its own language and against the other one, and the answers must match to the byte.
/// </summary>
// Two `dart run` calls in one package at once race in the native asset build hooks.
[Collection(DartPeer.Collection)]
public sealed class CrossTests
{
    // The Dart copy is _script in packages/orblit_remote/tool/cross_peer.dart.
    private static readonly (string Method, IReadOnlyDictionary<string, object?> Params)[] Script =
    [
        ("world.entities", Wire.Map()),
        ("world.entities", Wire.Map(("filter", new List<object?> { "Tag" }))),
        ("entity.get", Wire.Map(("id", 1L))),
        ("entity.get", Wire.Map(("id", 99L))),
        ("world.snapshot", Wire.Map()),
        ("entity.set", Wire.Map(("id", 1L), ("component", "Position"), ("field", "x"), ("value", 7L))),
        ("entity.set", Wire.Map(("id", 1L), ("component", "Position"), ("field", "x"))),
        ("entity.set", Wire.Map(("id", 5L), ("component", "Position"), ("field", "x"), ("value", 7L))),
        ("world.snapshot", Wire.Map()),
        ("world.diff", Wire.Map(("a", "s1"), ("b", "s2"))),
        ("world.diff", Wire.Map(("a", 1L), ("b", 2L))),
        ("world.diff", Wire.Map(("a", "s1"), ("b", "s9"))),
        ("time.pause", Wire.Map()),
        ("time.step", Wire.Map(("n", 3L))),
        ("time.resume", Wire.Map()),
        ("entity.get", Wire.Map(("id", "one"))),
        ("log.tail", Wire.Map()),
        ("no.such.verb", Wire.Map()),
    ];

    [Fact]
    public async Task TheDartClientGetsTheAnswersTheCSharpClientGets()
    {
        Assert.SkipWhen(DartPeer.Dart is null, DartPeer.Missing);
        var own = await Baseline();
        await using var game = await Game.Start(host => host with { World = new FakeWorld() });

        var dart = await DartPeer.Drive(game.Server.Uri);

        Assert.Equal(own, Compare(Assert.IsType<List<object?>>(JsonText.Decode(dart))));
    }

    [Fact]
    public async Task TheCSharpClientGetsTheSameAnswersFromTheDartServer()
    {
        Assert.SkipWhen(DartPeer.Dart is null, DartPeer.Missing);
        var own = await Baseline();
        await using var peer = await DartPeer.Serve();

        await using var client = await RemoteClient.Connect(peer.Address);

        Assert.Equal(own, Compare(await Transcript(client)));
    }

    // The wording of an InvalidParams message is each language's own. Its code is the contract.
    private static string Compare(List<object?> transcript)
    {
        foreach (var entry in transcript.OfType<OrderedDictionary<string, object?>>())
        {
            if (entry.GetValueOrDefault("error") is OrderedDictionary<string, object?> error
                && error["code"] is long code
                && code == RemoteException.InvalidParams)
            {
                error["message"] = "";
            }
        }

        return JsonText.Encode(transcript);
    }

    // Against the C# server, so the two tests above differ in one peer only.
    private static async Task<string> Baseline()
    {
        await using var game = await Game.Start(host => host with { World = new FakeWorld() });
        await using var client = await RemoteClient.Connect(game.Server.Uri);
        var own = Compare(await Transcript(client));

        // A transcript of errors would match another one, so it must show the calls worked.
        Assert.Contains("\"stepped\":3", own);
        Assert.Contains("\"path\":", own);
        Assert.Contains("-32001", own);
        return own;
    }

    private static async Task<List<object?>> Transcript(RemoteClient client)
    {
        var seen = new List<object?>();
        foreach (var (method, args) in Script)
        {
            var entry = Wire.Map(("method", method), ("params", args));
            try
            {
                entry["result"] = await client.Call(method, args);
            }
            catch (RemoteException error)
            {
                entry["error"] = Wire.Map(("code", (long)error.Code), ("message", error.Message));
            }

            seen.Add(entry);
        }

        return seen;
    }
}
