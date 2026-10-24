using System.Net;
using System.Net.Sockets;
using Orblit.Json;

namespace Orblit.Remote.Tests;

public sealed class ClientTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly IReadOnlyDictionary<string, object?> None = new Dictionary<string, object?>();

    private static Dictionary<string, object?> Args(string key, object? value) =>
        new Dictionary<string, object?> { [key] = value };

    private static Task<Game> Hosted(TaskCompletionSource<byte[]> hung) =>
        Game.Start(
            host => host with
            {
                World = null,
                Perf = () => new Dictionary<string, object?> { ["frame"] = 1L },
                Screenshot = () => hung.Task,
                Timeout = TimeSpan.FromMilliseconds(300),
            },
            Game.Quiet(3));

    private static string Text(object? value) => JsonText.Encode(value);

    [Fact]
    public async Task ACallReturnsTheResult()
    {
        await using var game = await Hosted(new());
        await using var client = await RemoteClient.Connect(game.Server.Uri);

        Assert.Equal("""{"frame":1}""", Text(await client.Call("perf.summary", None)));
    }

    [Fact]
    public async Task CallsMadeTogetherAreEachPairedWithTheirOwnAnswer()
    {
        await using var game = await Hosted(new());
        await using var client = await RemoteClient.Connect(game.Server.Uri);

        var step = client.Call("time.step", Args("n", 2L));
        var perf = client.Call("perf.summary", None);

        Assert.Equal("""{"paused":true,"stepped":2}""", Text(await step));
        Assert.Equal("""{"frame":1}""", Text(await perf));
    }

    [Fact]
    public async Task AnErrorReplyBecomesAnExceptionWithTheGamesCode()
    {
        await using var game = await Hosted(new());
        await using var client = await RemoteClient.Connect(game.Server.Uri);

        var refused = await Assert.ThrowsAsync<RemoteException>(() => client.Call("entity.get", Args("id", 1L)));

        Assert.Equal(RemoteException.Unavailable, refused.Code);
    }

    [Fact]
    public async Task ACallWaitingWhenTheServerClosesFailsAsDoesTheNextOne()
    {
        var hung = new TaskCompletionSource<byte[]>();
        await using var game = await Hosted(hung);
        await using var client = await RemoteClient.Connect(game.Server.Uri);
        var waiting = client.Call("view.screenshot", None);
        await Task.Delay(50, Cancel);

        await game.Server.DisposeAsync();

        Assert.Equal(RemoteException.Unavailable, (await Assert.ThrowsAsync<RemoteException>(() => waiting)).Code);
        Assert.Equal(RemoteException.Unavailable, (await Assert.ThrowsAsync<RemoteException>(() => client.Call("perf.summary", None))).Code);
    }

    [Fact]
    public async Task ACallPastItsDeadlineTimesOut()
    {
        await using var game = await Hosted(new());
        await using var client = await RemoteClient.Connect(game.Server.Uri, deadline: TimeSpan.FromMilliseconds(100));

        var late = await Assert.ThrowsAsync<RemoteException>(() => client.Call("view.screenshot", None));

        Assert.Equal(RemoteException.TimedOut, late.Code);
        Assert.Contains("view.screenshot", late.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedTokenSaysSoAndDoesNotRepeatTheToken()
    {
        await using var game = await Hosted(new());
        var wrong = new Uri($"ws://127.0.0.1:{game.Server.Uri.Port}/?token=deadbeefdeadbeefdeadbeefdeadbeef");

        var refused = await Assert.ThrowsAsync<RemoteException>(() => RemoteClient.Connect(wrong));

        Assert.Contains("refused", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("deadbeef", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASpanAtZeroIsRefusedBeforeAnyConnection()
    {
        var address = new Uri("ws://127.0.0.1:1/?token=x");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RemoteClient.Connect(address, deadline: TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RemoteClient.Connect(address, connectTimeout: TimeSpan.Zero));
    }

    [Fact]
    public async Task APortThatNeverAnswersTimesOutTheConnect()
    {
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        var held = new List<TcpClient>();
        var accepting = Task.Run(
            async () =>
            {
                while (true)
                {
                    held.Add(await silent.AcceptTcpClientAsync(Cancel));
                }
            },
            Cancel);
        var address = new Uri($"ws://127.0.0.1:{((IPEndPoint)silent.LocalEndpoint).Port}/?token=x");

        var late = await Assert.ThrowsAsync<RemoteException>(
            () => RemoteClient.Connect(address, connectTimeout: TimeSpan.FromMilliseconds(200)));

        Assert.Contains("did not answer", late.Message, StringComparison.Ordinal);
        silent.Stop();
        await Assert.ThrowsAnyAsync<Exception>(() => accepting);
        held.ForEach(client => client.Dispose());
    }

    [Fact]
    public async Task ClosedCompletesWhenTheServerCloses()
    {
        await using var game = await Hosted(new());
        await using var client = await RemoteClient.Connect(game.Server.Uri);

        await game.Server.DisposeAsync();

        await client.Closed.WaitAsync(TimeSpan.FromSeconds(5), Cancel);
    }

    [Fact]
    public async Task AParamJsonCannotCarryThrowsAndTheClientStaysUsable()
    {
        await using var game = await Hosted(new());
        await using var client = await RemoteClient.Connect(game.Server.Uri);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Call("entity.get", Args("x", double.NaN)));

        Assert.Equal("""{"frame":1}""", Text(await client.Call("perf.summary", None)));
    }

    [Fact]
    public async Task APortNobodyListensOnIsUnavailable()
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        var gone = await Assert.ThrowsAsync<RemoteException>(() => RemoteClient.Connect(new Uri($"ws://127.0.0.1:{port}/?token=x")));

        Assert.Equal(RemoteException.Unavailable, gone.Code);
        Assert.Contains("Nothing is listening", gone.Message, StringComparison.Ordinal);
    }
}
