using System.Net;
using System.Net.Sockets;
using Orblit.Json;

namespace Orblit.Remote.Tests;

public sealed class BridgeTests
{
    private static OrderedDictionary<string, object?> Write(Game game, int value) => Wire.Map(
        ("id", game.A),
        ("component", "orblit.LocalTransform"),
        ("field", "x"),
        ("value", value));

    [Fact]
    public async Task ASessionReadsTheCountStepsSetsAValueAndShowsTheDiff()
    {
        await using var game = await Game.Start();
        await using var bridge = Bridge.Start(["--allow-write", Bridge.Address(game.Server)]);

        var hello = await bridge.Ask("initialize", Wire.Map(("protocolVersion", "2025-06-18")));
        Assert.NotNull(FakeGame.Result(hello)["protocolVersion"]);
        bridge.Notify("notifications/initialized");
        Assert.Contains("set", await bridge.ToolNames());

        Assert.Equal(2L, (await bridge.Text("observe"))["count"]);
        Assert.Equal(3L, (await bridge.Text("step", Wire.Map(("n", 3))))["stepped"]);

        var before = await bridge.Text("snapshot");
        await bridge.Text("set", Write(game, 9));
        var after = await bridge.Text("snapshot");
        var diff = await bridge.Text("diff", Wire.Map(("a", before["id"]), ("b", after["id"])));

        Assert.True((long)diff["count"]! > 0);
        Assert.Contains("x", JsonText.Encode(diff["changes"]));
        Assert.Equal(0, await bridge.Finish());
    }

    [Fact]
    public async Task WithoutAllowWriteThereIsNoSetToolAndACallToItIsRefused()
    {
        await using var game = await Game.Start();
        await using var bridge = Bridge.Start([Bridge.Address(game.Server)]);

        Assert.DoesNotContain("set", await bridge.ToolNames());
        var reply = await bridge.Tool("set", Write(game, 9));

        Assert.Equal(RemoteException.InvalidParams, Rpc.CodeOf(reply));
        Assert.Equal(0, await bridge.Finish());
    }

    [Theory]
    [InlineData("ws://example.com:80/?token=x")]
    [InlineData("ws://127.0.0.1@evil.example:80/?token=x")]
    [InlineData("ws://127.0.0.1:80@evil.example:80/?token=x")]
    [InlineData("ws://[::1]:80/?token=x")]
    [InlineData("ws://127.0.0.1/?token=x")]
    public async Task AHostThatIsNotThisMachineIsRefusedWith64(string address)
    {
        var (exit, output, _) = await Bridge.Run([address]);

        Assert.Equal(64, exit);
        Assert.Empty(output);
    }

    [Fact]
    public async Task AnUnknownFlagIsRefusedRatherThanIgnored()
    {
        await using var game = await Game.Start();

        var (exit, _, _) = await Bridge.Run(["--allow-writes", Bridge.Address(game.Server)]);

        Assert.Equal(64, exit);
    }

    [Fact]
    public async Task AWrongTokenExitsWith69AndPrintsNoToken()
    {
        await using var game = await Game.Start();
        var wrong = $"ws://127.0.0.1:{game.Server.Uri.Port}/?token=tok3nXYZ";

        var (exit, output, notes) = await Bridge.Run([wrong]);

        Assert.Equal(69, exit);
        Assert.DoesNotContain("tok3nXYZ", notes);
        Assert.Contains("refused", notes);
        Assert.Empty(output);
    }

    [Fact]
    public async Task TheAddressCanComeFromTheEnvironment()
    {
        await using var game = await Game.Start();
        await using var bridge = Bridge.Start(
            [],
            name => name == "ORBLIT_REMOTE_ADDRESS" ? $"orblit-remote {game.Server.Uri}" : null);

        Assert.NotEmpty(await bridge.ToolNames());
        Assert.Equal(0, await bridge.Finish());
    }

    [Fact]
    public async Task TheProgramExitsWith69WhenTheGameGoesAway()
    {
        await using var game = await Game.Start();
        await using var bridge = Bridge.Start([Bridge.Address(game.Server)]);
        Assert.NotEmpty(await bridge.ToolNames());

        await game.Server.DisposeAsync();

        Assert.Equal(69, await bridge.Exit.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.Contains("closed the connection", bridge.Notes);
    }

    [Fact]
    public async Task AGameThatIsReadOnlyStillRefusesSetWithAllowWrite()
    {
        await using var game = await Game.Start(host => host with { ReadOnly = true }, Game.Quiet(6));
        await using var bridge = Bridge.Start(["--allow-write", Bridge.Address(game.Server)]);

        var reply = await bridge.Tool("set", Write(game, 9));

        Assert.True(FakeGame.IsError(reply));
        Assert.Contains("-32002", JsonText.Encode(FakeGame.Result(reply)["content"]));
        Assert.Equal(0, await bridge.Finish());
    }

    [Fact]
    public async Task AStrayByteOnStdinIsAParseErrorAndTheSessionGoesOn()
    {
        await using var game = await Game.Start();
        await using var bridge = Bridge.Start([Bridge.Address(game.Server)]);

        bridge.SendBytes(0xff, 0xfe, 0x0a);
        var bad = await bridge.Next();

        Assert.Equal(RemoteException.Parse, Rpc.CodeOf(bad));
        Assert.NotEmpty(await bridge.ToolNames());
        Assert.Equal(0, await bridge.Finish());
    }

    [Fact]
    public async Task ABadAddressExitsWithAUsageNoteAndNoOutput()
    {
        var (exit, output, notes) = await Bridge.Run(["http://nowhere"]);

        Assert.Equal(64, exit);
        Assert.Empty(output);
        Assert.Contains("usage", notes);
    }

    [Fact]
    public async Task AGameThatIsNotRunningExitsWith69()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var (exit, output, _) = await Bridge.Run([$"ws://127.0.0.1:{port}/?token=x"]);

        Assert.Equal(69, exit);
        Assert.Empty(output);
    }
}
