using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;
using Orblit.Json;
using Args = System.Collections.Generic.IReadOnlyDictionary<string, object?>;

namespace Orblit.Remote.Tests;

/// <summary>
/// <see cref="RemoteCli"/> run in this process and spoken to one JSON line at a time. A line on
/// its output that is not JSON fails the test, so the output is proved to carry messages and
/// nothing else. Input goes through a real pipe and the decoder the tool itself uses.
/// </summary>
internal sealed class Bridge : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly AnonymousPipeServerStream _stdin = new(PipeDirection.Out);
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
    private readonly StringWriter _notes = new();
    private int _next;

    private Bridge(string[] arguments, Func<string, string?> variable)
    {
        var input = RemoteCli.OpenInput(new AnonymousPipeClientStream(PipeDirection.In, _stdin.ClientSafePipeHandle));
        Exit = RemoteCli.RunAsync(arguments, new RemoteCliEnvironment(input, new LineWriter(_lines.Writer), _notes, variable));
    }

    internal Task<int> Exit { get; }

    /// <summary>What the program wrote to its error stream so far.</summary>
    internal string Notes => _notes.ToString();

    internal static Bridge Start(string[] arguments, Func<string, string?>? variable = null) =>
        new(arguments, variable ?? (_ => null));

    internal static string Address(RemoteServer server) => server.Uri.ToString();

    /// <summary>Runs the program on empty input to its end, and gives what it wrote.</summary>
    internal static async Task<(int Exit, string Output, string Notes)> Run(
        string[] arguments,
        Func<string, string?>? variable = null)
    {
        var output = new StringWriter();
        var notes = new StringWriter();
        var exit = await RemoteCli.RunAsync(
            arguments,
            new RemoteCliEnvironment(new StringReader(""), output, notes, variable ?? (_ => null)));
        return (exit, output.ToString(), notes.ToString());
    }

    internal void Notify(string method) =>
        SendBytes(Encoding.UTF8.GetBytes(JsonText.Encode(Wire.Map(("jsonrpc", "2.0"), ("method", method))) + "\n"));

    internal void SendBytes(params byte[] bytes)
    {
        _stdin.Write(bytes);
        _stdin.Flush();
    }

    /// <summary>The next line the program wrote, whatever it answers.</summary>
    internal async Task<Args> Next() =>
        Rpc.Decode(await _lines.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(Patience));

    internal async Task<Args> Ask(string method, Args? @params = null)
    {
        var request = Wire.Map(("jsonrpc", "2.0"), ("id", ++_next), ("method", method), ("params", @params ?? Wire.Map()));
        SendBytes(Encoding.UTF8.GetBytes(JsonText.Encode(request) + "\n"));
        var reply = await Next();
        Assert.Equal((long)_next, reply["id"]);
        return reply;
    }

    internal Task<Args> Tool(string name, Args? arguments = null) =>
        Ask("tools/call", Wire.Map(("name", name), ("arguments", arguments ?? Wire.Map())));

    /// <summary>The decoded text of the one block a tool answered.</summary>
    internal async Task<Args> Text(string name, Args? arguments = null)
    {
        var reply = await Tool(name, arguments);
        Assert.False(FakeGame.IsError(reply), JsonText.Encode(reply["result"]));
        return (Args)FakeGame.Text(reply)!;
    }

    internal async Task<IReadOnlyList<string>> ToolNames() => FakeGame.Names(await Ask("tools/list"));

    /// <summary>Ends stdin and waits for the exit code.</summary>
    internal async Task<int> Finish()
    {
        await _stdin.DisposeAsync();
        return await Exit.WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _stdin.DisposeAsync();
        await Exit.WaitAsync(Patience);
    }

    private sealed class LineWriter(ChannelWriter<string> lines) : TextWriter
    {
        private readonly StringBuilder _line = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value != '\n')
            {
                _line.Append(value);
                return;
            }

            lines.TryWrite(_line.ToString());
            _line.Clear();
        }
    }
}
