namespace Orblit.Remote.Tests;

public sealed class HandshakeTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    private static string Request(string method = "GET", string? host = "127.0.0.1:1", string connection = "Upgrade", string version = "13", string token = Token) =>
        $"{method} /?token={token} HTTP/1.1\r\n"
        + (host is null ? "" : $"Host: {host}\r\n")
        + $"Connection: {connection}\r\nUpgrade: websocket\r\nSec-WebSocket-Version: {version}\r\n"
        + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n";

    private static bool Allows(string request) => Handshake.Parse(request)!.Allows(Token);

    [Fact]
    public void AcceptKeyMatchesTheExampleInTheProtocolSpecification()
    {
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", Handshake.Parse(Request())!.AcceptKey);
    }

    [Fact]
    public void AnUpgradeWithTheTokenAndALoopbackHostIsAllowed()
    {
        Assert.True(Allows(Request()));
        Assert.True(Allows(Request(host: "LocalHost:80")));
    }

    [Fact]
    public void ConnectionMayListSeveralValues()
    {
        Assert.True(Allows(Request(connection: "keep-alive, Upgrade")));
    }

    [Fact]
    public void ARequestThatIsNotAVersion13UpgradeIsRefused()
    {
        Assert.False(Allows(Request(version: "8")));
        Assert.False(Allows(Request(method: "POST")));
        Assert.False(Allows(Request(connection: "keep-alive")));
    }

    [Fact]
    public void AMissingOrWrongTokenIsRefused()
    {
        Assert.False(Allows(Request(token: "")));
        Assert.False(Allows(Request(token: "0123456789abcdef0123456789abcdee")));
        Assert.False(Handshake.Parse(Request().Replace("/?token=" + Token, "/", StringComparison.Ordinal))!.Allows(Token));
    }

    [Fact]
    public void AnotherHostOrAMissingHostIsRefused()
    {
        Assert.False(Allows(Request(host: "evil.example")));
        Assert.False(Allows(Request(host: "127.0.0.1.evil.example:80")));
        Assert.False(Allows(Request(host: null)));
    }

    [Fact]
    public void AnOriginHeaderIsRefusedEvenWithTheToken()
    {
        Assert.False(Allows(Request().Replace("\r\n\r\n", "\r\nOrigin: http://127.0.0.1\r\n\r\n", StringComparison.Ordinal)));
    }

    [Fact]
    public void AFirstLineWithoutThreePartsIsNotARequest()
    {
        Assert.Null(Handshake.Parse("GET /\r\n\r\n"));
        Assert.Null(Handshake.Parse("\r\n\r\n"));
    }
}
