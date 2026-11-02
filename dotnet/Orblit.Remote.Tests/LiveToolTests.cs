namespace Orblit.Remote.Tests;

/// <summary>
/// The built tool, run as its own process against a real server, with real pipes for stdin and
/// stdout.
/// </summary>
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
    public async Task AWaitAndALabelledSnapshotGoThroughTheToolAndTheServer()
    {
        await using var game = await Game.Start(host => host with { World = new FakeWorld() });
        await using var tool = ToolProcess.Start(game.Server.Uri);
        await tool.Ask(Hello);

        var held = await tool.Ask(Call(3, "wait", """{"id":1,"component":"Position","field":"x","op":"==","value":1}"""));
        var never = await tool.Ask(Call(4, "wait", """{"id":1,"component":"Position","field":"x","op":">","value":5,"timeout_ms":50}"""));
        var named = await tool.Ask(Call(5, "snapshot", """{"label":"before"}"""));

        Assert.Contains("\"isError\":false", held);
        Assert.Contains("waited_ms", held);
        Assert.Contains("\"isError\":true", never);
        Assert.Contains("Not met in 50 ms", never);
        Assert.Contains("\"isError\":false", named);
        Assert.Contains("before", named);
        Assert.Equal(0, await tool.Finish());
    }

    private static string Call(int id, string name, string arguments) =>
        $$$"""{"jsonrpc":"2.0","id":{{{id}}},"method":"tools/call","params":{"name":"{{{name}}}","arguments":{{{arguments}}}}}""";

    private static async Task<(string Hello, string Observed, int Exit)> Session(Uri address)
    {
        await using var tool = ToolProcess.Start(address);
        var hello = await tool.Ask(Hello);
        var observed = await tool.Ask(Observe);
        return (hello, observed, await tool.Finish());
    }
}
