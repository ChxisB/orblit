using Orblit.Json;

namespace Orblit.Remote.Tests;

public sealed class ToolServerTests
{
    private static string Message(string body) => $"{{\"jsonrpc\":\"2.0\",{body}}}";

    private static async Task<IReadOnlyDictionary<string, object?>> Answer(string message) =>
        Rpc.Decode((await new FakeGame().Server.HandleAsync(message))!);

    [Fact]
    public async Task AVersionItKnowsIsAnsweredWithTheSameOne()
    {
        var reply = await new FakeGame().Ask("initialize", Wire.Map(("protocolVersion", "2025-03-26")));
        var result = FakeGame.Result(reply);

        Assert.Equal("2025-03-26", result["protocolVersion"]);
        Assert.Equal("""{"tools":{}}""", JsonText.Encode(result["capabilities"]));
        Assert.Equal("orblit-remote", ((IReadOnlyDictionary<string, object?>)result["serverInfo"]!)["name"]);
    }

    [Fact]
    public async Task AVersionItDoesNotKnowIsAnsweredWithTheNewest()
    {
        var reply = await new FakeGame().Ask("initialize", Wire.Map(("protocolVersion", "1999-01-01")));

        Assert.Equal("2025-06-18", FakeGame.Result(reply)["protocolVersion"]);
    }

    [Fact]
    public void ItReportsTheVersionOfBothProjects()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Orblit.Remote", "Orblit.Remote.csproj")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("The dotnet folder was not found");
        }

        var tag = $"<Version>{ToolServer.Version}</Version>";

        Assert.Contains(tag, File.ReadAllText(Path.Combine(root, "Orblit.Remote", "Orblit.Remote.csproj")));
        Assert.Contains(tag, File.ReadAllText(Path.Combine(root, "Orblit.Remote.Tool", "Orblit.Remote.Tool.csproj")));
    }

    [Fact]
    public async Task ANotificationIsNeverAnswered() =>
        Assert.Null(await new FakeGame().Server.HandleAsync(Message("\"method\":\"notifications/initialized\"")));

    [Fact]
    public async Task PingIsAnsweredWithAnEmptyResult() =>
        Assert.Empty(FakeGame.Result(await new FakeGame().Ask("ping")));

    [Fact]
    public async Task AMessageThatIsNotJsonIsAParseError() =>
        Assert.Equal(RemoteException.Parse, Rpc.CodeOf(await Answer("{nope")));

    [Fact]
    public async Task AnUnknownMethodIsMinus32601() =>
        Assert.Equal(RemoteException.NoMethod, Rpc.CodeOf(await new FakeGame().Ask("resources/list")));

    [Fact]
    public async Task ACallWithNoIdIsNeverRun()
    {
        var game = new FakeGame(new Dictionary<string, object?> { ["perf.summary"] = Wire.Map() });

        var reply = await game.Server.HandleAsync(
            Message("\"method\":\"tools/call\",\"params\":{\"name\":\"perf\"}"));

        Assert.Null(reply);
        Assert.Empty(game.Seen);
    }

    [Fact]
    public async Task AnIdOfNullIsANotification() =>
        Assert.Null(await new FakeGame().Server.HandleAsync(Message("\"id\":null,\"method\":\"ping\"")));

    [Theory]
    [InlineData("1.5")]
    [InlineData("""{"a":1}""")]
    [InlineData("[1]")]
    [InlineData("true")]
    public async Task AnIdThatIsNotANumberOrTextIsRefused(string id)
    {
        var reply = await Answer(Message($"\"id\":{id},\"method\":\"ping\""));

        Assert.Equal(RemoteException.InvalidRequest, Rpc.CodeOf(reply));
        Assert.Null(reply["id"]);
    }

    [Fact]
    public async Task ARequestWithNoMethodIsAnsweredUnderItsOwnId()
    {
        var reply = await Answer(Message("\"id\":5"));

        Assert.Equal(5L, reply["id"]);
        Assert.Equal(RemoteException.InvalidRequest, Rpc.CodeOf(reply));
    }

    [Fact]
    public async Task ABatchIsAnsweredInOrderAndLeavesOutNotifications()
    {
        var reply = await new FakeGame().Server.HandleAsync(
            """
            [{"jsonrpc":"2.0","id":1,"method":"ping"},
             {"jsonrpc":"2.0","method":"notifications/initialized"},
             {"jsonrpc":"2.0","id":"b","method":"ping"}]
            """);

        var answers = ((IReadOnlyList<object?>)JsonText.Decode(reply!)!).Cast<IReadOnlyDictionary<string, object?>>();
        Assert.Equal<object?[]>([1L, "b"], [.. answers.Select(answer => answer["id"])]);
    }

    [Fact]
    public async Task ABatchOfNotificationsIsNotAnswered() =>
        Assert.Null(await new FakeGame().Server.HandleAsync(
            """[{"jsonrpc":"2.0","method":"notifications/initialized"}]"""));

    [Fact]
    public async Task AnEmptyBatchIsAnInvalidRequest() =>
        Assert.Equal(RemoteException.InvalidRequest, Rpc.CodeOf(await Answer("[]")));

    [Fact]
    public async Task TheListHasSevenReadToolsAndNoSet()
    {
        var reply = await new FakeGame().Ask("tools/list");

        Assert.Equal(["observe", "step", "wait", "snapshot", "diff", "screenshot", "perf"], FakeGame.Names(reply));
    }

    [Fact]
    public async Task SetIsAddedWhenWritesAreAllowed()
    {
        var names = FakeGame.Names(await new FakeGame(allowWrite: true).Ask("tools/list"));

        Assert.Equal(8, names.Count);
        Assert.Contains("set", names);
    }

    [Fact]
    public async Task SetIsRefusedWhenWritesAreNotAllowed()
    {
        var game = new FakeGame();

        var reply = await game.Tool(
            "set",
            Wire.Map(("id", 1), ("component", "Position"), ("field", "x"), ("value", 5)));

        Assert.Equal(RemoteException.InvalidParams, Rpc.CodeOf(reply));
        Assert.Empty(game.Seen);
    }

    [Fact]
    public async Task StepAndSetAreSaidToChangeTheGameAndTheRestAreNot()
    {
        var reply = await new FakeGame(allowWrite: true).Ask("tools/list");

        var hints = ((IReadOnlyList<object?>)FakeGame.Result(reply)["tools"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToDictionary(
                tool => (string)tool["name"]!,
                tool => (bool)((IReadOnlyDictionary<string, object?>)tool["annotations"]!)["readOnlyHint"]!);

        Assert.False(hints["step"]);
        Assert.False(hints["set"]);
        Assert.True(hints["wait"]);
        Assert.True(hints["observe"]);
        Assert.True(hints["snapshot"]);
    }

    [Fact]
    public async Task AToolItDoesNotHaveIsRefused() =>
        Assert.Equal(
            RemoteException.InvalidParams,
            Rpc.CodeOf(await new FakeGame().Tool("delete_everything")));
}
