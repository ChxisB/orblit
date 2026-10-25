using System.Diagnostics;

namespace Orblit.Remote.Tests;

/// <summary>
/// The Dart control surface, run as a process from <c>packages/orblit_remote/tool/cross_peer.dart</c>.
/// Either it serves two entities and waits for stdin to end, or it drives a server and prints
/// what came back.
/// </summary>
internal sealed class DartPeer : IAsyncDisposable
{
    /// <summary>The collection every test class that starts a Dart process joins, so they run one at a time.</summary>
    internal const string Collection = "DartPeer";

    internal const string Missing = "The Dart SDK was not found. Set DART or put dart on the PATH.";

    // A cold start compiles the package and runs its build hooks.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(120);

    private readonly Process _process;
    private readonly Task<string> _error;

    private DartPeer(Process process)
    {
        _process = process;
        _error = process.StandardError.ReadToEndAsync();
    }

    internal static string? Dart { get; } = FindDart();

    /// <summary>The address the Dart server announced.</summary>
    internal Uri Address { get; private set; } = null!;

    /// <summary>Starts a Dart server and waits until it has announced its address.</summary>
    internal static async Task<DartPeer> Serve()
    {
        var peer = new DartPeer(Launch("serve"));
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            limit.CancelAfter(Patience);
            while (await peer._process.StandardOutput.ReadLineAsync(limit.Token) is { } line)
            {
                if (line.StartsWith("orblit-remote ws://", StringComparison.Ordinal))
                {
                    peer.Address = new Uri(line["orblit-remote ".Length..]);
                    return peer;
                }
            }

            throw new InvalidOperationException($"The Dart server ended before it announced an address.\n{await peer.Failure()}");
        }
        catch
        {
            await peer.DisposeAsync();
            throw;
        }
    }

    /// <summary>Runs the Dart client against <paramref name="address"/> and answers its one line of JSON.</summary>
    internal static async Task<string> Drive(Uri address)
    {
        await using var peer = new DartPeer(Launch("drive", address.ToString()));
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(Patience);
        var output = await peer._process.StandardOutput.ReadToEndAsync(limit.Token);
        await peer._process.WaitForExitAsync(limit.Token);
        if (peer._process.ExitCode != 0)
        {
            throw new InvalidOperationException($"The Dart client exited with {peer._process.ExitCode}.\n{await peer.Failure()}");
        }

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.StandardInput.Close();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await _process.WaitForExitAsync(limit.Token);
            }
            catch (OperationCanceledException)
            {
                _process.Kill(entireProcessTree: true);
            }
        }

        await _process.WaitForExitAsync();
        _process.Dispose();
    }

    private static Process Launch(params string[] arguments)
    {
        var start = new ProcessStartInfo(Dart ?? throw new InvalidOperationException(Missing))
        {
            WorkingDirectory = Path.Combine(RepositoryRoot(), "packages", "orblit_remote"),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var word in (string[])["run", "--enable-asserts", "tool/cross_peer.dart", .. arguments])
        {
            start.ArgumentList.Add(word);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("The Dart process did not start");
    }

    private static string RepositoryRoot()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "packages", "orblit_remote", "tool", "cross_peer.dart")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("The repository root was not found");
        }

        return root;
    }

    private static string? FindDart()
    {
        var name = OperatingSystem.IsWindows() ? "dart.exe" : "dart";
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "development", "flutter", "bin"));
        return Environment.GetEnvironmentVariable("DART") is { Length: > 0 } set && File.Exists(set)
            ? set
            : folders.Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
    }

    private async Task<string> Failure()
    {
        await _process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return await _error;
    }
}
