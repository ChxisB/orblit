using System.Text;
using System.Text.RegularExpressions;

namespace Orblit.Remote;

/// <summary>
/// The streams and variables <see cref="RemoteCli"/> works with. A test passes its own, so no
/// process is started.
/// </summary>
/// <param name="Input">One JSON message per line.</param>
/// <param name="Output">Carries JSON messages and nothing else.</param>
/// <param name="Error">Carries notes for the person running the tool.</param>
/// <param name="Variable">Reads an environment variable, null when it is not set.</param>
public sealed record RemoteCliEnvironment(
    TextReader Input,
    TextWriter Output,
    TextWriter Error,
    Func<string, string?> Variable);

/// <summary>
/// Serves a running game's control surface to an assistant over stdin and stdout, as tools. The
/// address is the line the game prints when it starts the server, with or without its
/// <c>orblit-remote</c> prefix. With no address on the command line, <c>ORBLIT_REMOTE_ADDRESS</c>
/// holds it, which keeps the token out of the process list.
/// </summary>
public static partial class RemoteCli
{
    private const int UsageExit = 64;
    private const int UnavailableExit = 69;

    private const string Usage =
        "usage: orblit-remote [--allow-write] ws://127.0.0.1:PORT/?token=TOKEN\n"
        + "The host must be 127.0.0.1 or localhost. Quote the address in a shell.\n"
        + "Without an address, ORBLIT_REMOTE_ADDRESS is read.";

    /// <summary>
    /// A reader that decodes <paramref name="stream"/> as UTF-8 and nothing else. A byte that is
    /// not UTF-8 becomes a replacement character, so it reads as a parse error and does not end
    /// the session. A reader that looks for a byte order mark would take <c>0xff 0xfe</c> for
    /// UTF-16 and read the rest as noise.
    /// </summary>
    public static TextReader OpenInput(Stream stream) =>
        new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);

    /// <summary>
    /// Runs until stdin ends and answers 0. Answers 64 for an address that is missing, unknown or
    /// not this machine, and 69 when the game cannot be reached or closes the connection.
    /// </summary>
    public static async Task<int> RunAsync(string[] arguments, RemoteCliEnvironment environment)
    {
        var allowWrite = arguments.Contains("--allow-write");
        var address = Address([.. arguments.Where(word => word != "--allow-write")], environment.Variable);
        if (address is null)
        {
            await environment.Error.WriteLineAsync(Usage);
            return UsageExit;
        }

        RemoteClient client;
        try
        {
            client = await RemoteClient.Connect(address);
        }
        catch (RemoteException error)
        {
            // The message never holds the token, because the client writes it without the address.
            await environment.Error.WriteLineAsync(error.Message);
            return UnavailableExit;
        }

        await using (client)
        {
            await environment.Error.WriteLineAsync(
                $"orblit-remote: connected to {address.Host}:{address.Port}, "
                + (allowWrite ? "writes allowed" : "read only"));
            return await Serve(new ToolServer(client.Call, allowWrite), client.Closed, environment);
        }
    }

    [GeneratedRegex(@"^ws://[^/?#@]*:\d+(?:[/?#]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitPort();

    // The token is sent to whatever host is named, so the host is checked here.
    private static Uri? Address(string[] words, Func<string, string?> variable)
    {
        if (words.Any(word => word.StartsWith("--", StringComparison.Ordinal)))
        {
            return null;
        }

        var given = string.Join(' ', words).Trim();
        var text = (given.Length == 0 ? variable("ORBLIT_REMOTE_ADDRESS") : given)?.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

        // A default port is dropped from the parsed address, so only the text shows one was given.
        return text is not null
            && ExplicitPort().IsMatch(text)
            && Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme == "ws"
            && uri.Host is "127.0.0.1" or "localhost"
                ? uri
                : null;
    }

    // shortcut: one message at a time, so a long step holds up a ping behind it. Add concurrent
    // handling when a client needs it.
    private static async Task<int> Serve(ToolServer server, Task closed, RemoteCliEnvironment environment)
    {
        while (true)
        {
            // A read can wait forever, so the game closing is raced against it. The read is left
            // pending when the game wins, and the process ends with it.
            var read = environment.Input.ReadLineAsync();
            await Task.WhenAny(read, closed);
            if (closed.IsCompleted)
            {
                await environment.Error.WriteLineAsync("orblit-remote: the game closed the connection");
                return UnavailableExit;
            }

            if (await read is not { } line)
            {
                await environment.Output.FlushAsync();
                return 0;
            }

            if (line.Trim().Length > 0 && await server.HandleAsync(line) is { } reply)
            {
                await environment.Output.WriteLineAsync(reply);
            }
        }
    }
}
