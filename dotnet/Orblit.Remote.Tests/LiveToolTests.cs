namespace Orblit.Remote.Tests;

/// <summary>
/// The built tool, run as its own process against a real server, with real pipes for stdin and
/// stdout.
/// </summary>
[Collection(DartPeer.Collection)]
public sealed class LiveToolTests
{
    private const string Hello =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\"}}";

    private const string Observe =
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"observe\",\"arguments\":{}}}";

    [Fact]
    public async Task TheToolServesAGameOverRealPipesAndEndsWhenStdinDoes()
    {
        await using var game = await Game.Start(host => host with { World = new FakeWorld() });

        var (hello, observed, exit) = await Session(game.Server.Uri);

        Assert.Contains("\"serverInfo\"", hello);
        Assert.Contains("\"isError\":false", observed);
        Assert.Contains("Position", observed);
        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task TheToolSaysTheSameOfTheDartServerAsOfTheCSharpOne()
    {
        Assert.SkipWhen(DartPeer.Dart is null, DartPeer.Missing);
        await using var game = await Game.Start(host => host with { World = new FakeWorld() });
        var own = await Session(game.Server.Uri);
        await using var peer = await DartPeer.Serve();

        var theirs = await Session(peer.Address);

        Assert.Equal(own, theirs);
    }

    private static async Task<(string Hello, string Observed, int Exit)> Session(Uri address)
    {
        await using var tool = ToolProcess.Start(address);
        var hello = await tool.Ask(Hello);
        var observed = await tool.Ask(Observe);
        return (hello, observed, await tool.Finish());
    }
}
