using System.Diagnostics;

namespace Orblit.Remote.Tests;

/// <summary>
/// The built <c>orblit-remote</c> tool, run as a process so stdin and stdout are real pipes.
/// </summary>
internal sealed class ToolProcess : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly Process _process;
    private readonly Task<string> _error;

    private ToolProcess(Process process)
    {
        _process = process;
        _error = process.StandardError.ReadToEndAsync();
    }

    /// <summary>Starts the tool with the address in its environment, where no process list shows it.</summary>
    internal static ToolProcess Start(Uri address)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(ToolPath());
        start.Environment["ORBLIT_REMOTE_ADDRESS"] = address.ToString();
        return new ToolProcess(Process.Start(start) ?? throw new InvalidOperationException("The tool did not start"));
    }

    /// <summary>Sends one message and answers the line the tool writes back.</summary>
    internal async Task<string> Ask(string message)
    {
        await _process.StandardInput.WriteLineAsync(message.AsMemory(), TestContext.Current.CancellationToken);
        await _process.StandardInput.FlushAsync(TestContext.Current.CancellationToken);
        return await _process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(Patience)
            ?? throw new InvalidOperationException($"The tool closed its output.\n{await _error}");
    }

    /// <summary>Ends stdin, as a client that is done does, and answers the exit code.</summary>
    internal async Task<int> Finish()
    {
        _process.StandardInput.Close();
        await _process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(Patience);
        return _process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        await _process.WaitForExitAsync();
        _process.Dispose();
    }

    // The project reference builds the tool beside the tests, in the same configuration.
    private static string ToolPath()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var root = output;
        while (!File.Exists(Path.Combine(root.FullName, "Orblit.Remote.Tool", "Orblit.Remote.Tool.csproj")))
        {
            root = root.Parent ?? throw new InvalidOperationException("The dotnet folder was not found");
        }

        return Path.Combine(
            root.FullName,
            "Orblit.Remote.Tool",
            "bin",
            output.Parent!.Name,
            output.Name,
            "Orblit.Remote.Tool.dll");
    }
}
