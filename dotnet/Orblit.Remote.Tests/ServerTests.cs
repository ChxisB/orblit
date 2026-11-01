using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using Orblit.Json;

namespace Orblit.Remote.Tests;

public sealed partial class ServerTests
{
    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex Hex32();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Token(Uri address) => address.Query["?token=".Length..];

    [Fact]
    public async Task TheAnnouncedLineIsTheUriWithTheTokenInIt()
    {
        string? line = null;
        await using var game = await Game.Start(options: new RemoteServerOptions
        {
            Announce = text => line = text,
            Random = new Random(1),
            DebugBuild = true,
        });

        Assert.Equal($"orblit-remote {game.Server.Uri}", line);
        Assert.Equal("127.0.0.1", game.Server.Uri.Host);
        Assert.Matches(Hex32(), Token(game.Server.Uri));
    }

    [Fact]
    public async Task ASeededRandomGivesTheSameTokenEachTime()
    {
        await using var first = await Game.Start(options: Game.Quiet(5));
        await using var second = await Game.Start(options: Game.Quiet(5));

        Assert.Equal(Token(first.Server.Uri), Token(second.Server.Uri));
    }

    [Fact]
    public async Task AScriptOverTheSocketPausesStepsEditsAndDiffs()
    {
        await using var game = await Game.Start();
        await using var chat = await Chat.Open(game.Server.Uri);

        await chat.Call("time.pause");
        var before = game.Passed;
        var stepped = await chat.Call("time.step", """{"n":10}""");
        Assert.Equal("""{"paused":true,"stepped":10}""", JsonText.Encode(stepped));
        Assert.Equal(before + 10, game.Passed);

        var entity = (IReadOnlyDictionary<string, object?>)(await chat.Call("entity.get", $$"""{"id":{{game.A}}}"""))!;
        var transform = (IReadOnlyDictionary<string, object?>)((IReadOnlyDictionary<string, object?>)entity["components"]!)["orblit.LocalTransform"]!;
        Assert.Equal(4L, Convert.ToInt64(transform["x"], System.Globalization.CultureInfo.InvariantCulture));

        var first = ((IReadOnlyDictionary<string, object?>)(await chat.Call("world.snapshot"))!)["id"];
        await chat.Call("entity.set", $$"""{"id":{{game.B}},"component":"orblit.LocalTransform","field":"y","value":9}""");
        var second = ((IReadOnlyDictionary<string, object?>)(await chat.Call("world.snapshot"))!)["id"];
        var diff = await chat.Call("world.diff", JsonText.Encode(Rpc.Row(("a", first), ("b", second))));

        Assert.Equal(
            $$"""[{"path":"entities/{{game.B}}/orblit.LocalTransform/y","from":1.0,"to":9.0}]""",
            JsonText.Encode(diff));
    }

    [Fact]
    public async Task EveryVerbRunsOnThePumpThread()
    {
        var seen = new List<int>();
        await using var game = await Game.Start(host => host with { Perf = () => { seen.Add(Environment.CurrentManagedThreadId); return new Dictionary<string, object?>(); } });
        await using var chat = await Chat.Open(game.Server.Uri);

        await chat.Call("perf.summary");

        Assert.Equal([game.LoopThread], seen);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("")]
    public async Task AWrongOrEmptyTokenIsRefusedWith403(string token)
    {
        await using var game = await Game.Start();

        Assert.Equal(403, await Raw.Status(game.Server.Uri, Raw.Upgrade($"/?token={token}", $"127.0.0.1:{game.Server.Uri.Port}")));
    }

    [Fact]
    public async Task NoTokenAndAPlainGetAreRefusedWith403()
    {
        await using var game = await Game.Start();
        var host = $"127.0.0.1:{game.Server.Uri.Port}";

        Assert.Equal(403, await Raw.Status(game.Server.Uri, Raw.Upgrade("/", host)));
        Assert.Equal(403, await Raw.Status(game.Server.Uri, $"GET /{game.Server.Uri.Query} HTTP/1.1\r\nHost: {host}\r\n\r\n"));
    }

    [Fact]
    public async Task APercentEncodingThatIsNotATokenIsAWrongTokenNotAFault()
    {
        await using var game = await Game.Start();
        var host = $"127.0.0.1:{game.Server.Uri.Port}";

        Assert.Equal(403, await Raw.Status(game.Server.Uri, Raw.Upgrade("/?token=%ff", host)));
        Assert.Equal(101, await Raw.Status(game.Server.Uri, Raw.Upgrade(game.Server.Uri.PathAndQuery, host)));
    }

    [Fact]
    public async Task AnOriginAndAForeignHostAreRefusedButLocalhostIsNot()
    {
        await using var game = await Game.Start();
        var port = game.Server.Uri.Port;
        var target = game.Server.Uri.PathAndQuery;

        Assert.Equal(403, await Raw.Status(game.Server.Uri, Raw.Upgrade(target, $"127.0.0.1:{port}", "Origin: http://example.com")));
        Assert.Equal(403, await Raw.Status(game.Server.Uri, Raw.Upgrade(target, "evil.example")));
        Assert.Equal(101, await Raw.Status(game.Server.Uri, Raw.Upgrade(target, $"localhost:{port}")));
    }

    [Fact]
    public async Task ANinthConnectionIsTurnedAway()
    {
        await using var game = await Game.Start();
        var open = new List<Chat>();
        try
        {
            for (var i = 0; i < 8; i++)
            {
                open.Add(await Chat.Open(game.Server.Uri));
            }

            Assert.Equal(503, await Raw.Status(game.Server.Uri, Raw.Upgrade(game.Server.Uri.PathAndQuery, $"127.0.0.1:{game.Server.Uri.Port}")));
        }
        finally
        {
            foreach (var chat in open)
            {
                await chat.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task AnOversizeMessageEndsTheConnection()
    {
        await using var game = await Game.Start();
        await using var chat = await Chat.Open(game.Server.Uri);

        var ended = false;
        try
        {
            // The server may close while the client is still writing, so the send can fail too.
            await chat.Send(new string('x', 2 << 20));
            ended = await chat.Read() is null;
            Assert.NotEqual(WebSocketState.Open, chat.Socket.State);
        }
        catch (WebSocketException)
        {
            // A send that fails this way leaves the client socket Open.
            ended = true;
        }

        Assert.True(ended);
    }

    [Fact]
    public async Task RequestsOnOneConnectionAreAnsweredInOrder()
    {
        await using var game = await Game.Start();
        await using var chat = await Chat.Open(game.Server.Uri);

        await chat.Send("""{"id":1,"method":"time.step","params":{"n":40}}""");
        await chat.Send("""{"id":2,"method":"world.entities"}""");

        var first = Rpc.Decode((await chat.Read())!);
        var second = Rpc.Decode((await chat.Read())!);
        Assert.Equal([1L, 2L], new[] { first["id"], second["id"] });
    }

    [Fact]
    public async Task ABinaryFrameIsIgnoredAndTheNextTextIsAnswered()
    {
        await using var game = await Game.Start();
        await using var chat = await Chat.Open(game.Server.Uri);

        await chat.SendBinary([1, 2, 3]);

        Assert.NotNull(await chat.Call("world.entities"));
    }

    [Fact]
    public async Task APausedGameRunsAgainWhenItsLastClientLeaves()
    {
        await using var game = await Game.Start();
        await using (var chat = await Chat.Open(game.Server.Uri))
        {
            await chat.Call("time.pause");
            Assert.True(game.Gate.Paused);
        }

        await Poll(() => !game.Gate.Paused);
    }

    [Fact]
    public async Task ClosingTheServerRunsAPausedGameAgain()
    {
        var game = await Game.Start();
        game.Gate.Pause();

        await game.DisposeAsync();

        Assert.False(game.Gate.Paused);
    }

    [Fact]
    public async Task AFailingAnnounceLeavesNoPortListening()
    {
        Uri? seen = null;
        var game = Game.Start(options: new RemoteServerOptions
        {
            Announce = line => { seen = new Uri(line["orblit-remote ".Length..]); throw new InvalidOperationException("no console"); },
            DebugBuild = true,
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => game);
        using var client = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(async () => await client.ConnectAsync(IPAddress.Loopback, seen!.Port, Cancel));
    }

    [Fact]
    public async Task ClosingTheServerEndsAnOpenConnectionAsGoingAway()
    {
        await using var game = await Game.Start();
        await using var chat = await Chat.Open(game.Server.Uri);
        await chat.Call("world.entities");

        await game.Server.DisposeAsync();

        Assert.Null(await chat.Read());
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, chat.Socket.CloseStatus);
    }

    [Fact]
    public async Task ClosingTheServerDoesNotWaitForAClientThatNeverReads()
    {
        await using var game = await Game.Start();
        await using var chat = await Chat.Open(game.Server.Uri);
        await chat.Call("world.entities");
        var timer = Stopwatch.StartNew();

        await game.Server.DisposeAsync();

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(4), $"{timer.Elapsed}");
    }

    [Fact]
    public async Task ARequestHeadThatNeverEndsIsDropped()
    {
        await using var game = await Game.Start(options: new RemoteServerOptions
        {
            Announce = _ => { },
            Random = new Random(2),
            DebugBuild = true,
            HeadTimeout = TimeSpan.FromMilliseconds(100),
        });
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, game.Server.Uri.Port, Cancel);

        var read = await client.GetStream().ReadAsync(new byte[1], Cancel);

        Assert.Equal(0, read);
    }

    [Fact]
    public async Task AServerClosedRightAfterAConnectionStopsCleanly()
    {
        for (var i = 0; i < 50; i++)
        {
            var server = await RemoteServer.Start(new RemoteHost(), new GameLoopContext(), Game.Quiet(i));
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, server.Uri.Port, Cancel);
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task RenderVerbsAnswerOverTheSocket()
    {
        await using var game = await Game.Start(host => host with
        {
            Draws = () => [Rpc.Row(("id", "object:1"), ("x", double.NaN))],
            Material = key => Rpc.Row(("key", key)),
        });
        await using var chat = await Chat.Open(game.Server.Uri);

        Assert.Equal(
            """{"count":1,"draws":[{"id":"object:1","x":"NaN"}],"truncated":false}""",
            JsonText.Encode(await chat.Call("view.draws")));
        Assert.Equal("""{"key":3}""", JsonText.Encode(await chat.Call("view.material", """{"key":3}""")));
    }

    [Fact]
    public async Task AReleaseBuildRefusesToStart()
    {
        var options = new RemoteServerOptions { Announce = _ => { }, DebugBuild = false };

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RemoteServer.Start(new RemoteHost(), new GameLoopContext(), options));

        Assert.Contains("debug build", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAnAssemblyBuiltWithoutOptimisationsIsADebugBuild()
    {
        Assert.False(RemoteServer.IsDebugBuild(null));
        Assert.False(RemoteServer.IsDebugBuild(new System.Diagnostics.DebuggableAttribute(false, false)));
        Assert.True(RemoteServer.IsDebugBuild(new System.Diagnostics.DebuggableAttribute(false, true)));
    }

    private static async Task Poll(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(10, Cancel);
        }

        Assert.True(done());
    }
}
