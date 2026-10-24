using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using Orblit.Json;

namespace Orblit.Remote;

/// <summary>
/// One connection to a running game's control surface. Calls are paired with their answers by
/// id, so several can wait at once.
/// </summary>
public sealed class RemoteClient : IAsyncDisposable
{
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(1);

    private readonly ClientWebSocket _socket;
    private readonly TimeSpan _deadline;
    private readonly object _lock = new();
    private readonly Dictionary<long, TaskCompletionSource<object?>> _waiting = [];
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _reading;
    private long _next;
    private bool _open = true;

    private RemoteClient(ClientWebSocket socket, TimeSpan deadline)
    {
        _socket = socket;
        _deadline = deadline;
        _reading = Listen();
    }

    /// <summary>Completes when the connection ends, whichever side ended it.</summary>
    public Task Closed => _done.Task;

    /// <summary>
    /// Connects to <paramref name="address"/>, the <c>ws://</c> line a game announces, token
    /// included. <paramref name="deadline"/> bounds each call and defaults to 30 seconds. A game
    /// waits up to ten seconds on one callback and restarts that wait for every frame a
    /// <c>time.step</c> passes, so a long step can outlast the default.
    /// <paramref name="connectTimeout"/> bounds the wait for the upgrade and defaults to 10
    /// seconds. Throws <see cref="ArgumentOutOfRangeException"/> for either at zero or less, and
    /// <see cref="RemoteException"/> with <see cref="RemoteException.Unavailable"/> when the game
    /// cannot be reached, does not answer, or refuses the token.
    /// </summary>
    public static async Task<RemoteClient> Connect(
        Uri address,
        TimeSpan? deadline = null,
        TimeSpan? connectTimeout = null)
    {
        var callSpan = Above0(deadline ?? TimeSpan.FromSeconds(30), nameof(deadline));
        var connectSpan = Above0(connectTimeout ?? TimeSpan.FromSeconds(10), nameof(connectTimeout));
        var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        try
        {
            using var limit = new CancellationTokenSource(connectSpan);
            await socket.ConnectAsync(address, limit.Token);
            return new RemoteClient(socket, callSpan);
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException)
        {
            var answered = socket.HttpStatusCode != 0;
            socket.Dispose();
            var where = $"{address.Host}:{address.Port}";

            // The library's text repeats the address, and the address holds the token, so the
            // message is written here and the original exception is not kept.
            throw new RemoteException(
                RemoteException.Unavailable,
                e switch
                {
                    OperationCanceledException => $"{where} did not answer in {Seconds(connectSpan)} s.",
                    _ when answered => "The game refused the connection. Check the address and its token.",
                    _ => $"Nothing is listening at {where}.",
                });
        }
    }

    /// <summary>
    /// The result of <paramref name="method"/>. Throws <see cref="RemoteException"/> with the
    /// game's code when it refuses, with <see cref="RemoteException.TimedOut"/> when no answer
    /// comes within the deadline, and with <see cref="RemoteException.Unavailable"/> once the
    /// connection has closed.
    /// </summary>
    public async Task<object?> Call(string method, IReadOnlyDictionary<string, object?> @params)
    {
        var id = Interlocked.Increment(ref _next);

        // Encoded before the waiter exists, because a value JSON cannot carry throws here and
        // would leave the waiter in the map until close.
        var message = Encoding.UTF8.GetBytes(
            JsonText.Encode(Wire.Map(("jsonrpc", "2.0"), ("id", id), ("method", method), ("params", @params))));
        var answer = Wait(id);
        using var limit = new CancellationTokenSource(_deadline);
        try
        {
            await Send(message, limit.Token);
            return await answer.Task.WaitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            Forget(id);
            throw new RemoteException(RemoteException.TimedOut, $"No answer to {method} in {Seconds(_deadline)} s");
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException)
        {
            Forget(id);
            throw Gone();
        }
    }

    /// <summary>
    /// Closes the connection and waits a second for the game to answer in kind. The game's own
    /// answer is not needed to be done with it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        using var limit = new CancellationTokenSource(CloseWait);
        try
        {
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, limit.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The connection already ended, or the game is not answering. It is dropped below.
        }

        await Task.WhenAny(_reading, Task.Delay(CloseWait));
        _socket.Abort();
        await _reading;
        _socket.Dispose();
        _sending.Dispose();
    }

    private static TimeSpan Above0(TimeSpan span, string name) =>
        span > TimeSpan.Zero ? span : throw new ArgumentOutOfRangeException(name, span, "The span is not above 0.");

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    private static RemoteException Gone() =>
        new(RemoteException.Unavailable, "The game closed the connection");

    private TaskCompletionSource<object?> Wait(long id)
    {
        var waiter = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (!_open)
            {
                throw Gone();
            }

            _waiting.Add(id, waiter);
        }

        return waiter;
    }

    private void Forget(long id)
    {
        lock (_lock)
        {
            _waiting.Remove(id);
        }
    }

    private async Task Send(byte[] message, CancellationToken cancel)
    {
        await _sending.WaitAsync(cancel);
        try
        {
            await _socket.SendAsync(message, WebSocketMessageType.Text, true, cancel);
        }
        finally
        {
            _sending.Release();
        }
    }

    private async Task Listen()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (await ReceiveText(buffer) is { } text)
            {
                Answer(text);
            }
        }
        catch (Exception e) when (e is WebSocketException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            // A reset ends the connection the same way a close frame does.
        }

        Finish();
    }

    // A frame that is not text is read and dropped. Null means the connection is over.
    private async Task<string?> ReceiveText(byte[] buffer)
    {
        while (true)
        {
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult part;
            do
            {
                part = await _socket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);
                if (part.MessageType == WebSocketMessageType.Close)
                {
                    await Reciprocate();
                    return null;
                }

                message.Write(buffer, 0, part.Count);
            }
            while (!part.EndOfMessage);

            if (part.MessageType == WebSocketMessageType.Text)
            {
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }
        }
    }

    private async Task Reciprocate()
    {
        using var limit = new CancellationTokenSource(CloseWait);
        try
        {
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, limit.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The game has gone, so there is nobody left to answer.
        }
    }

    // A reply that is not JSON, not an object, or has an id nobody waits for is dropped, as a
    // late answer to a call that already gave up is.
    private void Answer(string text)
    {
        object? reply;
        try
        {
            reply = JsonText.Decode(text);
        }
        catch (FormatException)
        {
            return;
        }

        if (reply is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("id") is not long id)
        {
            return;
        }

        TaskCompletionSource<object?>? waiter;
        lock (_lock)
        {
            if (!_waiting.Remove(id, out waiter))
            {
                return;
            }
        }

        Complete(waiter, map);
    }

    private static void Complete(TaskCompletionSource<object?> waiter, IReadOnlyDictionary<string, object?> map)
    {
        if (map.GetValueOrDefault("error") is not IReadOnlyDictionary<string, object?> error)
        {
            waiter.SetResult(map.GetValueOrDefault("result"));
            return;
        }

        var code = error.GetValueOrDefault("code") is long whole and >= int.MinValue and <= int.MaxValue
            ? (int)whole
            : RemoteException.Internal;
        waiter.SetException(new RemoteException(code, $"{error.GetValueOrDefault("message")}"));
    }

    private void Finish()
    {
        TaskCompletionSource<object?>[] left;
        lock (_lock)
        {
            _open = false;
            left = [.. _waiting.Values];
            _waiting.Clear();
        }

        _done.SetResult();
        foreach (var waiter in left)
        {
            waiter.SetException(Gone());
        }
    }
}
