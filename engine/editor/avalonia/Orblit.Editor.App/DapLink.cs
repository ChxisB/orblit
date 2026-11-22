using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orblit.Editor.App;

/// <summary>
/// The editor's end of a Debug Adapter Protocol link, such as to netcoredbg. Each message is JSON after a
/// Content-Length header. A reader thread queues what arrives, and the UI thread takes it on its poll, so the
/// session that reads it needs no locks.
/// </summary>
internal sealed class DapLink : IDapLink
{
    // A header line past this is not DAP, so the link stops reading rather than buffer it.
    const int MaxHeader = 1024;
    const string Length = "Content-Length:";

    readonly Stream _to;
    readonly ConcurrentQueue<JsonElement> _arrived = new();
    int _seq;
    volatile bool _ended;

    /// <param name="to">Where requests go, such as the adapter's standard input.</param>
    /// <param name="from">Where messages come from, such as the adapter's standard output.</param>
    public DapLink(Stream to, Stream from)
    {
        _to = to;
        new Thread(() => Read(new BufferedStream(from))) { IsBackground = true, Name = "DAP reader" }.Start();
    }

    public bool Closed => _ended && _arrived.IsEmpty;

    public void Request(string command, JsonObject? arguments = null)
    {
        var message = new JsonObject { ["seq"] = ++_seq, ["type"] = "request", ["command"] = command };
        if (arguments is not null)
        {
            message["arguments"] = arguments;
        }

        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        try
        {
            _to.Write(Encoding.ASCII.GetBytes($"{Length} {body.Length}\r\n\r\n"));
            _to.Write(body);
            _to.Flush();
        }
        catch (IOException)
        {
            // The adapter has gone. Closed tells the session once it has taken what came before.
            _ended = true;
        }
    }

    public IReadOnlyList<JsonElement> Take()
    {
        List<JsonElement> taken = [];
        while (_arrived.TryDequeue(out var message))
        {
            taken.Add(message);
        }

        return taken;
    }

    void Read(Stream from)
    {
        try
        {
            while (NextLength(from) is { } length)
            {
                var body = new byte[length];
                from.ReadExactly(body);
                using var json = JsonDocument.Parse(body);
                _arrived.Enqueue(json.RootElement.Clone());
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or ObjectDisposedException)
        {
            // A broken message or a closed pipe ends the link the same way the adapter exiting does.
        }
        finally
        {
            _ended = true;
        }
    }

    // The length the next message's headers give, or null at the end of the stream.
    static int? NextLength(Stream from)
    {
        int? length = null;
        while (Line(from) is { } line)
        {
            if (line.Length == 0)
            {
                return length ?? throw new InvalidDataException("A DAP message came with no Content-Length.");
            }

            if (line.StartsWith(Length, StringComparison.OrdinalIgnoreCase) && int.TryParse(line[Length.Length..], out var given) && given >= 0)
            {
                length = given;
            }
        }

        return null;
    }

    static string? Line(Stream from)
    {
        var line = new StringBuilder();
        int next;
        while ((next = from.ReadByte()) != '\n')
        {
            if (next < 0)
            {
                return null;
            }

            if (line.Length == MaxHeader)
            {
                throw new InvalidDataException("A DAP header line was too long.");
            }

            line.Append((char)next);
        }

        return line.ToString().TrimEnd('\r');
    }
}
