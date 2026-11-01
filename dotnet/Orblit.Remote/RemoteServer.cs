using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Orblit.Remote;

/// <summary>
/// The control surface of a running debug game, spoken over a WebSocket on the loopback address.
/// A client must know the token in <see cref="Uri"/>, which is announced once and never logged.
/// </summary>
/// <remarks>
/// Socket reads run on the thread pool. Each request is handed to the game's synchronization
/// context, so the verbs and every <c>await</c> inside them touch the world on the game's thread.
/// A game with no context of its own passes a <see cref="GameLoopContext"/> and pumps it each
/// frame. A request on one connection waits for the one before it, so replies come in order.
/// </remarks>
public sealed class RemoteServer : IAsyncDisposable
{
    private const int MaxSockets = 8;
    private const int MaxPayload = 1 << 20;
    private const int MaxHead = 16 * 1024;
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(1);

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly RemoteVerbs _verbs;
    private readonly SynchronizationContext _loop;
    private readonly TimeGate? _gate;
    private readonly string _token;
    private readonly TimeSpan _headTimeout;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _slots = new(MaxSockets);
    private readonly object _lock = new();
    private readonly List<WebSocket> _sockets = [];
    private readonly HashSet<Task> _serving = [];
    private Task _accepting = Task.CompletedTask;
    private bool _closing;

    private RemoteServer(RemoteHost host, SynchronizationContext loop, RemoteServerOptions options)
    {
        _verbs = new RemoteVerbs(host);
        _loop = loop;
        _gate = host.Gate;
        _headTimeout = options.HeadTimeout;
        _token = NewToken(options.Random);
        _listener.Start();
        Uri = new Uri($"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/?token={_token}");
    }

    /// <summary>The address a client connects to, token included.</summary>
    public Uri Uri { get; }

    /// <summary>
    /// Listens on a free loopback port and announces <see cref="Uri"/>. Throws
    /// <see cref="InvalidOperationException"/> in a build whose entry assembly was compiled with
    /// optimisations, because a shipped game must not carry a way to set its components.
    /// </summary>
    public static async Task<RemoteServer> Start(
        RemoteHost host,
        SynchronizationContext loop,
        RemoteServerOptions? options = null)
    {
        options ??= new RemoteServerOptions();
        var entry = Assembly.GetEntryAssembly()?.GetCustomAttribute<DebuggableAttribute>();
        if (!(options.DebugBuild ?? IsDebugBuild(entry)))
        {
            throw new InvalidOperationException("The control surface runs in a debug build only");
        }

        var announce = options.Announce ?? (line => Console.WriteLine(line));
        var server = new RemoteServer(host, loop, options);
        try
        {
            announce($"orblit-remote {server.Uri}");
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }

        server._accepting = server.AcceptLoop();
        return server;
    }

    internal static bool IsDebugBuild(DebuggableAttribute? compiled) => compiled?.IsJITOptimizerDisabled == true;

    /// <summary>
    /// Ends every open connection with the going-away status, lets time run, and stops listening.
    /// A client that never reads is cut off after a second.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        WebSocket[] open;
        lock (_lock)
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            open = [.. _sockets];
        }

        _gate?.Resume();
        _listener.Stop();
        await Task.WhenAll(open.Select(socket => End(socket, WebSocketCloseStatus.EndpointUnavailable)));
        await Task.WhenAny(Task.WhenAll(Serving()), Task.Delay(CloseWait));
        await _stop.CancelAsync();
        await Task.WhenAll(Serving());
        await _accepting;
        _stop.Dispose();
        _slots.Dispose();
    }

    private static string NewToken(Random? random)
    {
        var bytes = new byte[16];
        if (random is null)
        {
            RandomNumberGenerator.Fill(bytes);
        }
        else
        {
            random.NextBytes(bytes);
        }

        return Convert.ToHexStringLower(bytes);
    }

    private static bool IsConnectionFault(Exception e) =>
        e is IOException or SocketException or WebSocketException or OperationCanceledException or ObjectDisposedException;

    private static async Task End(WebSocket socket, WebSocketCloseStatus status)
    {
        using var limit = new CancellationTokenSource(CloseWait);
        try
        {
            await socket.CloseOutputAsync(status, null, limit.Token);
        }
        catch (Exception e) when (IsConnectionFault(e))
        {
            // The client is gone or stuck, and the socket is dropped either way.
        }
    }

    private static ValueTask Refuse(NetworkStream stream, string status) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));

    private bool Closing
    {
        get
        {
            lock (_lock)
            {
                return _closing;
            }
        }
    }

    private Task[] Serving()
    {
        lock (_lock)
        {
            return [.. _serving];
        }
    }

    private async Task AcceptLoop()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = Track(Serve(client));
            }
        }
        catch (Exception e) when (IsConnectionFault(e) || (e is InvalidOperationException && Closing))
        {
            // The listener was stopped. One stopped before its first accept says so with
            // InvalidOperationException instead.
        }
    }

    private async Task Track(Task work)
    {
        lock (_lock)
        {
            _serving.Add(work);
        }

        try
        {
            await work;
        }
        finally
        {
            lock (_lock)
            {
                _serving.Remove(work);
            }
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                if (await ReadRequest(stream) is { } request)
                {
                    await Handle(stream, request);
                }
            }
            catch (Exception e) when (IsConnectionFault(e))
            {
                // A client that resets or stalls must not take the game down.
            }
        }
    }

    // One byte at a time, so a frame the client sends right behind its request is not read here
    // and lost to the socket.
    private async Task<Handshake?> ReadRequest(NetworkStream stream)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        limit.CancelAfter(_headTimeout);
        var head = new List<byte>();
        var one = new byte[1];
        while (!CollectionsMarshal.AsSpan(head).EndsWith("\r\n\r\n"u8))
        {
            if (head.Count == MaxHead || await stream.ReadAsync(one, limit.Token) == 0)
            {
                return null;
            }

            head.Add(one[0]);
        }

        return Handshake.Parse(Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(head)));
    }

    private async Task Handle(NetworkStream stream, Handshake request)
    {
        using var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });
        if (!request.Allows(_token))
        {
            await Refuse(stream, "403 Forbidden");
            return;
        }

        if (!_slots.Wait(0))
        {
            await Refuse(stream, "503 Service Unavailable");
            return;
        }

        Join(socket);
        try
        {
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                    + $"Sec-WebSocket-Accept: {request.AcceptKey}\r\n\r\n"));
            await Converse(socket);
        }
        finally
        {
            Leave(socket);
        }
    }

    private void Join(WebSocket socket)
    {
        lock (_lock)
        {
            _sockets.Add(socket);
        }
    }

    private void Leave(WebSocket socket)
    {
        lock (_lock)
        {
            _sockets.Remove(socket);
            if (_sockets.Count == 0)
            {
                _gate?.Resume();
            }
        }

        _slots.Release();
    }

    private async Task Converse(WebSocket socket)
    {
        // A connection that joins after DisposeAsync took its list is not closed there.
        if (Closing)
        {
            await End(socket, WebSocketCloseStatus.EndpointUnavailable);
            return;
        }

        var buffer = new byte[64 * 1024];
        while (await ReceiveText(socket, buffer) is { } text)
        {
            var reply = await Run(text);
            if (reply is not null && socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, _stop.Token);
            }
        }
    }

    // A frame that is not text is read and dropped. Null means the connection is over.
    private async Task<string?> ReceiveText(WebSocket socket, byte[] buffer)
    {
        while (true)
        {
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult part;
            do
            {
                part = await socket.ReceiveAsync(buffer.AsMemory(), _stop.Token);
                if (part.MessageType == WebSocketMessageType.Close)
                {
                    await End(socket, WebSocketCloseStatus.NormalClosure);
                    return null;
                }

                if (message.Length + part.Count > MaxPayload)
                {
                    await End(socket, WebSocketCloseStatus.MessageTooBig);
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

    private async Task<string?> Run(string message)
    {
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loop.Post(ignored => { _ = Relay(message, done); }, null);
        return await done.Task.WaitAsync(_stop.Token);
    }

    private async Task Relay(string message, TaskCompletionSource<string?> done) =>
        done.SetResult(await _verbs.HandleAsync(message));
}
