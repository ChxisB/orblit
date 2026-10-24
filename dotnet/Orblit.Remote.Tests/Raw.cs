using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Orblit.Remote.Tests;

/// <summary>
/// A hand written upgrade request, so a header or a query that <see cref="System.Net.WebSockets.ClientWebSocket"/>
/// would not send can be sent.
/// </summary>
internal static class Raw
{
    internal static string Upgrade(string target, string host, params string[] extra) =>
        $"GET {target} HTTP/1.1\r\nHost: {host}\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n"
        + "Sec-WebSocket-Version: 13\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        + string.Concat(extra.Select(header => header + "\r\n"))
        + "\r\n";

    /// <summary>The status code in the first line of the answer to <paramref name="request"/>.</summary>
    internal static async Task<int> Status(Uri address, string request)
    {
        var cancel = TestContext.Current.CancellationToken;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, address.Port, cancel);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancel);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var line = await reader.ReadLineAsync(cancel);
        return int.Parse(line!.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
    }
}
