using static Orblit.Remote.Tests.Rpc;

namespace Orblit.Remote.Tests;

public sealed class WaitTests
{
    private readonly FakeWorld _world = new();
    private readonly RemoteVerbs _verbs;

    public WaitTests() => _verbs = new RemoteVerbs(new RemoteHost { World = _world });

    private static string Request(string @params) =>
        $$"""{"jsonrpc":"2.0","id":1,"method":"world.wait","params":{{@params}}}""";

    private static IReadOnlyDictionary<string, object?> ResultOf(IReadOnlyDictionary<string, object?> reply) =>
        (IReadOnlyDictionary<string, object?>)reply["result"]!;

    private static string MessageOf(IReadOnlyDictionary<string, object?> reply) =>
        (string)((IReadOnlyDictionary<string, object?>)reply["error"]!)["message"]!;

    [Fact]
    public async Task AFieldThatAlreadyHoldsAnswersAtOnceWithTheValueItRead()
    {
        var reply = await Call(_verbs, "world.wait", """{"id":1,"component":"Position","field":"x","op":"==","value":1}""");

        var result = ResultOf(reply);
        Assert.Equal(true, result["met"]);
        Assert.Equal(1L, result["value"]);
        Assert.True((long)result["waited_ms"]! >= 0);
    }

    [Theory]
    [InlineData("==", 1, true)]
    [InlineData("==", 2, false)]
    [InlineData("!=", 2, true)]
    [InlineData("!=", 1, false)]
    [InlineData("<", 2, true)]
    [InlineData("<", 1, false)]
    [InlineData("<=", 1, true)]
    [InlineData("<=", 0, false)]
    [InlineData(">", 0, true)]
    [InlineData(">", 1, false)]
    [InlineData(">=", 1, true)]
    [InlineData(">=", 2, false)]
    public async Task EachTestComparesTheFieldWithTheValue(string op, int value, bool holds)
    {
        var reply = await Call(
            _verbs,
            "world.wait",
            $$"""{"id":1,"component":"Position","field":"x","op":"{{op}}","value":{{value}},"timeout_ms":20}""");

        Assert.Equal(holds, reply.ContainsKey("result"));
        if (!holds)
        {
            Assert.Equal(RemoteException.TimedOut, CodeOf(reply));
        }
    }

    [Fact]
    public async Task AFieldThatComesToHoldAnswersWhenItDoes()
    {
        var pending = _verbs.HandleAsync(Request(
            """{"id":1,"component":"Position","field":"x","op":">=","value":10,"timeout_ms":5000}"""));
        Assert.False(pending.IsCompleted);

        _world.Held[1]["Position"]["x"] = 12.5;

        var result = ResultOf(Decode((await pending)!));
        Assert.Equal(12.5, result["value"]);
    }

    [Fact]
    public async Task AFieldThatNeverHoldsTimesOutAndNamesTheLastValue()
    {
        var reply = await Call(_verbs, "world.wait", """{"id":1,"component":"Position","field":"x","op":">","value":100,"timeout_ms":30}""");

        Assert.Equal(RemoteException.TimedOut, CodeOf(reply));
        Assert.Contains("Position.x of entity 1 was 1", MessageOf(reply));
    }

    [Fact]
    public async Task AnEntityThatIsNotAliveSatisfiesGone()
    {
        var reply = await Call(_verbs, "world.wait", """{"id":7,"gone":true}""");

        var result = ResultOf(reply);
        Assert.Equal(true, result["met"]);
        Assert.Null(result["value"]);
    }

    [Fact]
    public async Task AnEntityThatIsGoneBeforeTheTimeIsUpEndsTheWait()
    {
        var pending = _verbs.HandleAsync(Request("""{"id":1,"gone":true,"timeout_ms":5000}"""));
        Assert.False(pending.IsCompleted);

        _world.Held.Remove(1);

        Assert.Equal(true, ResultOf(Decode((await pending)!))["met"]);
    }

    [Fact]
    public async Task AnEntityThatStaysAliveTimesOutOnGone()
    {
        var reply = await Call(_verbs, "world.wait", """{"id":1,"gone":true,"timeout_ms":30}""");

        Assert.Equal(RemoteException.TimedOut, CodeOf(reply));
        Assert.Contains("entity 1 is still alive", MessageOf(reply));
    }

    [Theory]
    [InlineData("""{"id":1,"component":"Position","field":"x","op":"=","value":1}""")]
    [InlineData("""{"id":1,"component":"Position","field":"x","op":"==","value":"high"}""")]
    [InlineData("""{"id":1,"component":"Position","field":"x","op":"==","value":1,"timeout_ms":0}""")]
    [InlineData("""{"id":1,"component":"Position","field":"x","op":"==","value":1,"timeout_ms":20001}""")]
    [InlineData("""{"id":1,"component":"Position","field":"x","op":"==","value":1,"timeout_ms":"soon"}""")]
    [InlineData("""{"id":1,"field":"x","op":"==","value":1}""")]
    [InlineData("""{"id":1,"component":"Position","op":"==","value":1}""")]
    [InlineData("""{"component":"Position","field":"x","op":"==","value":1}""")]
    [InlineData("""{"id":1,"component":"Tag","field":"n","op":"==","value":9,"gone":"yes"}""")]
    [InlineData("""{"id":1,"component":"Position","gone":true}""")]
    [InlineData("""{"id":1,"component":"Tag","field":"x","op":"==","value":1}""")]
    [InlineData("""{"id":1,"component":"Position","field":"z","op":"==","value":1}""")]
    public async Task AWaitThatIsNotWellFormedIsRefused(string @params) =>
        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(_verbs, "world.wait", @params)));

    [Fact]
    public async Task AnEntityThatIsNotAliveIsNotFoundUnlessItIsWhatWeWaitFor()
    {
        var reply = await Call(_verbs, "world.wait", """{"id":7,"component":"Position","field":"x","op":"==","value":1}""");

        Assert.Equal(RemoteException.NotFound, CodeOf(reply));
    }

    [Fact]
    public async Task AGameWithNoWorldCannotBeWaitedOn()
    {
        var bare = new RemoteVerbs(new RemoteHost());

        var reply = await Call(bare, "world.wait", """{"id":1,"gone":true}""");

        Assert.Equal(RemoteException.Unavailable, CodeOf(reply));
    }

    [Fact]
    public async Task AReadOnlySessionMayWait()
    {
        var locked = new RemoteVerbs(new RemoteHost { World = _world, ReadOnly = true });

        var reply = await Call(locked, "world.wait", """{"id":1,"component":"Position","field":"x","op":"==","value":1}""");

        Assert.True(reply.ContainsKey("result"));
    }
}
