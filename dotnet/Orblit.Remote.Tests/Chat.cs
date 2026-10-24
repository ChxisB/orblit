using System.Net.WebSockets;
using System.Text;
using Orblit.Json;

namespace Orblit.Remote.Tests;

/// <summary>
/// A plain WebSocket that speaks JSON-RPC one line at a time, so a server test does not lean on
/// <see cref="RemoteClient"/>.
/// </summary>
internal sealed class Chat : IAsyncDisposable
{
    private int _next;

    private Chat()
    {
    }

    internal ClientWebSocket Socket { get; } = new();

    internal static async Task<Chat> Open(Uri address)
    {
        var chat = new Chat();
        await chat.Socket.ConnectAsync(address, TestContext.Current.CancellationToken);
        return chat;
    }

    internal Task Send(string text) =>
        Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);

    internal Task SendBinary(byte[] bytes) =>
        Socket.SendAsync(bytes, WebSocketMessageType.Binary, true, TestContext.Current.CancellationToken);

    /// <summary>The next text message, or null once the server closes the connection.</summary>
    internal async Task<string?> Read()
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        ValueWebSocketReceiveResult part;
        do
        {
            part = await Socket.ReceiveAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);
            if (part.MessageType == WebSocketMessageType.Close)
            {
                await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, TestContext.Current.CancellationToken);
                return null;
            }

            message.Write(buffer, 0, part.Count);
        }
        while (!part.EndOfMessage);

        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    /// <summary>The result of <paramref name="method"/>, decoded. An error reply fails the test.</summary>
    internal async Task<object?> Call(string method, string? @params = null)
    {
        var tail = @params is null ? "" : $",\"params\":{@params}";
        await Send($$"""{"jsonrpc":"2.0","id":{{++_next}},"method":"{{method}}"{{tail}}}""");
        var reply = Rpc.Decode((await Read())!);
        Assert.False(reply.ContainsKey("error"), JsonText.Encode(reply));
        return reply["result"];
    }

    public async ValueTask DisposeAsync()
    {
        if (Socket.State == WebSocketState.Open)
        {
            await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        Socket.Dispose();
    }
}
