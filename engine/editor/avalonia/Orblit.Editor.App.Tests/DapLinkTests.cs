using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orblit.Editor.App.Tests;

public sealed class DapLinkTests
{
    static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    static string Framed(string json) => $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";

    // Takes what the reader thread queued until it has read to the end.
    static List<JsonElement> ReadAll(DapLink link)
    {
        List<JsonElement> taken = [];
        Assert.True(SpinWait.SpinUntil(() =>
        {
            taken.AddRange(link.Take());
            return link.Closed;
        }, TimeSpan.FromSeconds(10)));
        return taken;
    }

    [Fact]
    public void A_request_is_json_after_its_length_with_a_rising_sequence_number()
    {
        var sent = new MemoryStream();
        var link = new DapLink(sent, Bytes(""));

        link.Request("initialize", new JsonObject { ["adapterID"] = "coreclr" });
        link.Request("next");

        Assert.Equal(
            Framed("""{"seq":1,"type":"request","command":"initialize","arguments":{"adapterID":"coreclr"}}""")
            + Framed("""{"seq":2,"type":"request","command":"next"}"""),
            Encoding.UTF8.GetString(sent.ToArray()));
    }

    [Fact]
    public void Messages_are_taken_in_the_order_they_came_and_then_the_link_is_closed()
    {
        var first = """{"type":"event","event":"initialized","body":{"name":"é"}}""";
        var second = """{"type":"response","command":"launch","success":true}""";
        // A header the link does not know is passed over.
        var link = new DapLink(new MemoryStream(), Bytes($"Content-Type: application/json\r\n{Framed(first)}{Framed(second)}"));

        var taken = ReadAll(link);

        Assert.Equal(["initialized", "launch"], taken.Select(message => message.GetProperty(message.GetProperty("type").GetString() == "event" ? "event" : "command").GetString()));
        Assert.Equal("é", taken[0].GetProperty("body").GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("Content-Length: 5\r\n\r\nnot j")]
    [InlineData("Content-Type: text\r\n\r\n{}")]
    [InlineData("Content-Length: 40\r\n\r\n{}")]
    public void A_broken_message_closes_the_link(string arrived) =>
        Assert.Empty(ReadAll(new DapLink(new MemoryStream(), Bytes(arrived))));

    [Fact]
    public void A_request_to_an_adapter_that_has_gone_closes_the_link()
    {
        // A pipe nobody writes to, so only the failed request can close the link.
        using var silent = new AnonymousPipeServerStream(PipeDirection.Out);
        using var from = new AnonymousPipeClientStream(PipeDirection.In, silent.ClientSafePipeHandle);
        var link = new DapLink(new BlockedWrite(), from);

        link.Request("next");

        Assert.True(link.Closed);
    }

    sealed class BlockedWrite : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("The pipe has been ended.");
    }
}
