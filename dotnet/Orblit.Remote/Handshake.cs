using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Orblit.Remote;

/// <summary>
/// What the server reads of a WebSocket upgrade request: the request line and the headers.
/// </summary>
internal sealed class Handshake
{
    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly string _method;
    private readonly string _target;
    private readonly Dictionary<string, string> _headers;

    private Handshake(string method, string target, Dictionary<string, string> headers)
    {
        _method = method;
        _target = target;
        _headers = headers;
    }

    /// <summary>The <c>Sec-WebSocket-Accept</c> value that answers this request's key.</summary>
    [SuppressMessage("Security", "CA5350:Do not use weak cryptographic algorithms", Justification = "RFC 6455 fixes SHA-1 for the accept key. It guards nothing, the token does.")]
    public string AcceptKey =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(_headers["Sec-WebSocket-Key"] + AcceptGuid)));

    /// <summary>
    /// The request line and headers in <paramref name="head"/>, or null when the first line is
    /// not a method, a target and a version.
    /// </summary>
    public static Handshake? Parse(string head)
    {
        var lines = head.Split("\r\n");
        var start = lines[0].Split(' ');
        if (start.Length != 3)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }

        return new Handshake(start[0], start[1], headers);
    }

    /// <summary>
    /// True for a plain upgrade from a local program that knows <paramref name="token"/>. A
    /// browser page always sends an <c>Origin</c> and a rebinding attack names another host, so
    /// either one is refused even with the right token.
    /// </summary>
    public bool Allows(string token) => IsUpgrade && !_headers.ContainsKey("Origin") && OnLoopback && Holds(token);

    private bool IsUpgrade =>
        _method == "GET"
        && Lists("Upgrade", "websocket")
        && Lists("Connection", "upgrade")
        && _headers.ContainsKey("Sec-WebSocket-Key")
        && _headers.GetValueOrDefault("Sec-WebSocket-Version") == "13";

    private bool OnLoopback
    {
        get
        {
            if (!_headers.TryGetValue("Host", out var host))
            {
                return false;
            }

            var colon = host.LastIndexOf(':');
            var name = colon < 0 ? host : host[..colon];
            return name == "127.0.0.1" || name.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool Lists(string header, string item) =>
        _headers.TryGetValue(header, out var value)
        && value.Split(',').Any(part => part.Trim().Equals(item, StringComparison.OrdinalIgnoreCase));

    // A wrong guess takes as long to refuse as a near miss, so the token cannot be found one
    // character at a time.
    private bool Holds(string token)
    {
        var query = _target.IndexOf('?', StringComparison.Ordinal);
        var given = HttpUtility.ParseQueryString(query < 0 ? "" : _target[(query + 1)..])["token"];
        return given is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(token));
    }
}
