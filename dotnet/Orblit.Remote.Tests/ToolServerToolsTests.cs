using Orblit.Json;
using Args = System.Collections.Generic.IReadOnlyDictionary<string, object?>;

namespace Orblit.Remote.Tests;

public sealed class ToolServerToolsTests
{
    private static List<object?> Entities(int count, params string[] components) =>
    [
        .. Enumerable.Range(0, count).Select(id => (object?)Wire.Map(
            ("id", (long)id),
            ("components", components.Cast<object?>().ToList()))),
    ];

    private static FakeGame With(string verb, object? answer, bool allowWrite = false) =>
        new(new Dictionary<string, object?> { [verb] = answer }, allowWrite);

    [Fact]
    public async Task ObserveCutsALongListToTheLimit()
    {
        var game = With("world.entities", Entities(30, "Position"));

        var body = (Args)FakeGame.Text(await game.Tool("observe", Wire.Map(("limit", 3))))!;

        Assert.Equal(30L, body["count"]);
        Assert.Equal(3, ((IReadOnlyList<object?>)body["entities"]!).Count);
        Assert.Equal(true, body["truncated"]);
    }

    [Fact]
    public async Task ObserveShowsTwentyRowsWithNoLimit()
    {
        var game = With("world.entities", Entities(30));

        var body = (Args)FakeGame.Text(await game.Tool("observe"))!;

        Assert.Equal(20, ((IReadOnlyList<object?>)body["entities"]!).Count);
    }

    [Fact]
    public async Task ObservePassesAFilterOnToTheGame()
    {
        var game = With("world.entities", new List<object?>());

        await game.Tool("observe", Wire.Map(("filter", new List<object?> { "Tag" })));

        Assert.Equal("""{"filter":["Tag"]}""", JsonText.Encode(game.Seen.Single().Params));
    }

    [Fact]
    public async Task ObserveWithAnIdReadsThatEntityInFull()
    {
        var game = With("entity.get", Wire.Map(("id", 2), ("components", Wire.Map(("Tag", Wire.Map(("n", 9)))))));

        var body = (Args)FakeGame.Text(await game.Tool("observe", Wire.Map(("id", 2))))!;

        Assert.NotNull(body["components"]);
        Assert.Equal("entity.get", game.Seen.Single().Method);
    }

    [Fact]
    public async Task ObserveTellsTheAssistantALimitOfZeroIsWrong()
    {
        var game = With("world.entities", new List<object?>());

        var reply = await game.Tool("observe", Wire.Map(("limit", 0)));

        Assert.True(FakeGame.IsError(reply));
        Assert.Contains("limit", FakeGame.Written(reply));
    }

    [Fact]
    public async Task WaitPassesItsArgumentsOnAndAnswersWhatTheGameSaid()
    {
        var game = With("world.wait", Wire.Map(("met", true), ("value", 5), ("waited_ms", 40)));
        var asked = Wire.Map(
            ("id", 3), ("component", "Position"), ("field", "x"), ("op", ">"), ("value", 4), ("timeout_ms", 900));

        var body = (Args)FakeGame.Text(await game.Tool("wait", asked))!;

        Assert.Equal(true, body["met"]);
        Assert.Equal("world.wait", game.Seen.Single().Method);
        Assert.Equal(JsonText.Encode(asked), JsonText.Encode(game.Seen.Single().Params));
    }

    [Fact]
    public async Task WaitTellsTheAssistantWhyTheGameGaveUp()
    {
        var game = With("world.wait", new RemoteException(RemoteException.TimedOut, "Not met in 900 ms: Position.x of entity 3 was 1"));

        var reply = await game.Tool("wait", Wire.Map(("id", 3), ("gone", true)));

        Assert.True(FakeGame.IsError(reply));
        Assert.Contains("Position.x of entity 3 was 1", FakeGame.Written(reply));
    }

    [Fact]
    public async Task WaitLeavesOutWhatTheAssistantDidNotSay()
    {
        var game = With("world.wait", Wire.Map(("met", true), ("value", null), ("waited_ms", 0)));

        await game.Tool("wait", Wire.Map(("id", 3), ("gone", true), ("surprise", 1)));

        Assert.Equal("""{"id":3,"gone":true}""", JsonText.Encode(game.Seen.Single().Params));
    }

    [Fact]
    public async Task AToolThatTakesNoArgumentsWorksWithoutTheField()
    {
        var game = With("perf.summary", Wire.Map(("frame", 1)));

        var without = await game.Ask("tools/call", Wire.Map(("name", "perf")));
        var nulled = await game.Ask("tools/call", Wire.Map(("name", "perf"), ("arguments", null)));

        Assert.False(FakeGame.IsError(without));
        Assert.False(FakeGame.IsError(nulled));
    }

    [Fact]
    public async Task StepRefusesACountOutsideOneToThreeHundred()
    {
        var game = With("time.step", Wire.Map(("stepped", 1)));

        foreach (var n in new object[] { 0, 301, -1, "x", 2.5 })
        {
            Assert.True(FakeGame.IsError(await game.Tool("step", Wire.Map(("n", n)))), $"{n}");
        }

        Assert.Empty(game.Seen);
        await game.Tool("step", Wire.Map(("n", 300)));
        Assert.Equal("""{"n":300}""", JsonText.Encode(game.Seen.Single().Params));
    }

    [Fact]
    public async Task SetRefusesAValueJsonCannotCarry()
    {
        var game = new FakeGame(allowWrite: true);

        var reply = await game.Server.HandleAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"tools/call","params":
            {"name":"set","arguments":{"id":1,"component":"a","field":"b","value":1e999}}}
            """);

        var result = FakeGame.Result(Rpc.Decode(reply!));
        Assert.Equal(true, result["isError"]);
        Assert.Contains("finite", JsonText.Encode(result["content"]));
        Assert.Empty(game.Seen);
    }

    [Fact]
    public async Task StepPassesOneFrameByDefault()
    {
        var game = With("time.step", Wire.Map(("stepped", 1)));

        await game.Tool("step");

        Assert.Equal("""{"n":1}""", JsonText.Encode(game.Seen.Single().Params));
    }

    [Fact]
    public async Task SetWritesThroughWhenAllowed()
    {
        var game = new FakeGame(allowWrite: true);

        var reply = await game.Tool(
            "set",
            Wire.Map(("id", 1), ("component", "Position"), ("field", "x"), ("value", 5)));

        const string Written = """{"id":1,"component":"Position","field":"x","value":5}""";
        Assert.False(FakeGame.IsError(reply));
        Assert.Equal(Written, FakeGame.Written(reply));
        Assert.Equal("entity.set", game.Seen.Single().Method);
        Assert.Equal(Written, JsonText.Encode(game.Seen.Single().Params));
    }

    [Fact]
    public async Task SnapshotAnswersTheIdAndTheCountsNotTheContent()
    {
        var game = With(
            "world.snapshot",
            Wire.Map(
                ("id", "s1"),
                ("entities", Wire.Map(("1", Wire.Map()), ("2", Wire.Map()))),
                ("extras", Wire.Map(("physics", Wire.Map())))));

        var reply = await game.Tool("snapshot");

        Assert.Equal("""{"id":"s1","entities":2,"extras":["physics"]}""", FakeGame.Written(reply));
    }

    [Fact]
    public async Task DiffCutsALongListOfChanges()
    {
        var game = With(
            "world.diff",
            Enumerable.Range(0, 500).Select(i => (object?)Wire.Map(("path", $"p{i}"), ("from", 0), ("to", 1))).ToList());

        var body = (Args)FakeGame.Text(await game.Tool("diff", Wire.Map(("a", "s1"), ("b", "s2"))))!;

        Assert.Equal(500L, body["count"]);
        Assert.Equal(200, ((IReadOnlyList<object?>)body["changes"]!).Count);
        Assert.Equal(true, body["truncated"]);
    }

    [Fact]
    public async Task ScreenshotAnswersAnImageBlock()
    {
        var game = With("view.screenshot", Wire.Map(("png", "AAAA")));

        var block = FakeGame.Content(await game.Tool("screenshot")).Single();

        Assert.Equal("""{"type":"image","data":"AAAA","mimeType":"image/png"}""", JsonText.Encode(block));
    }

    [Fact]
    public async Task ARefusalFromTheGameIsAnErrorResultNotACrash()
    {
        var game = With("entity.get", new RemoteException(RemoteException.NotFound, "Entity 9 is not alive"));

        var reply = await game.Tool("observe", Wire.Map(("id", 9)));

        Assert.True(FakeGame.IsError(reply));
        Assert.Contains("Entity 9 is not alive", FakeGame.Written(reply));
    }

    [Fact]
    public async Task AnAnswerInAnUnknownShapeIsAnErrorResult()
    {
        var game = With("world.snapshot", "nonsense");

        Assert.True(FakeGame.IsError(await game.Tool("snapshot")));
    }
}
