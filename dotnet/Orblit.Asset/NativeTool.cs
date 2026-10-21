using System.ComponentModel;
using System.Diagnostics;

namespace Orblit.Asset;

/// <summary>
/// One of the native programs the cook drives. The importers do not encode anything themselves.
/// Texture compression, mesh import and splat quantisation are all C++ that already exists, is
/// already tested, and is already what the by-hand scripts in <c>tool/</c> call. An importer's job is
/// to decide the flags, run the program and file what it wrote, so that the thing a build does and
/// the thing a person does by hand stay the same thing.
/// </summary>
public sealed class NativeTool
{
    private readonly string? _path;

    /// <param name="name">What the program is called, such as <c>orblit_texture_cook</c> or <c>cmgen</c>. Used in errors and to find it.</param>
    /// <param name="path">Where it is, when the caller knows. Nothing is searched for then.</param>
    /// <param name="environmentVariable">
    /// The variable that overrides where it is, following the convention the by-hand scripts set:
    /// <c>ORBLIT_TEXTURE_COOK</c>, <c>ORBLIT_CMGEN</c>.
    /// </param>
    public NativeTool(string name, string? path = null, string? environmentVariable = null)
    {
        Name = name;
        _path = path;
        EnvironmentVariable = environmentVariable;
    }

    /// <summary>
    /// Where the built programs are looked for when nothing says otherwise. Settable because a build
    /// machine puts them somewhere else, and because a test needs to point at a program it wrote
    /// itself. A colon-separated <c>ORBLIT_TOOL_PATH</c> adds to it without any code change.
    /// </summary>
    public static IReadOnlyList<string> SearchPath { get; set; } =
    [
        .. Environment.GetEnvironmentVariable("ORBLIT_TOOL_PATH")?.Split(':') ?? [],
        "packages/orblit_filament/native/headless/build",
        "packages/orblit_filament/native/texture_cook/build",
        "packages/orblit_filament/darwin/third_party/filament-mac/filament/bin",
    ];

    public string Name { get; }

    public string? EnvironmentVariable { get; }

    /// <summary>
    /// The program, or null when it is not where it should be. Resolved every time rather than cached,
    /// because a cook run from an editor can outlive a rebuild of the tools it drives.
    /// </summary>
    public string? Locate()
    {
        if (_path is not null)
        {
            return _path;
        }

        var overridden = EnvironmentVariable is null ? null : Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(overridden))
        {
            return overridden;
        }

        var candidates = SearchPath.Select(directory => NativeWork.Beside(directory, Name));
        return candidates.Where(File.Exists).Select(Path.GetFullPath).FirstOrDefault() ?? OnPath();
    }

    /// <summary>
    /// Runs it, and turns a bad exit into an <see cref="ImportFailureException"/> that says what was run
    /// and what the program printed. A tool's own diagnostics are the useful part of an import failure
    /// ("unsupported PNG bit depth" tells someone what to do, and "exit code 1" does not), so they are
    /// carried through rather than summarised.
    /// </summary>
    public async Task RunAsync(IReadOnlyList<string> arguments, AssetId on)
    {
        var program = Locate() ?? throw new ImportFailureException(
            on,
            $"needs {Name}, which is not built. Look for it in {string.Join(", ", SearchPath)}"
            + (EnvironmentVariable is null ? "" : $", or set {EnvironmentVariable}") + ".");

        var result = await Task.Run(() => Capture(program, arguments)).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var said = string.Join("\n", new[] { DartText.Trim(result.Error), DartText.Trim(result.Output) }.Where(text => text.Length > 0));
            throw new ImportFailureException(
                on,
                $"{Name} exited {result.ExitCode}.\n  {program} {string.Join(' ', arguments)}",
                said.Length == 0 ? null : said);
        }
    }

    /// <summary>Runs <paramref name="program"/> to the end and returns what it wrote.</summary>
    internal static Captured Capture(string program, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        process.Start();
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new Captured(process.ExitCode, output, error.Result);
    }

    // Last: whatever is on PATH, which is how cmgen usually arrives.
    private string? OnPath()
    {
        try
        {
            var found = Capture(OperatingSystem.IsWindows() ? "where" : "which", [Name]);
            var first = found.ExitCode == 0 ? DartText.Trim(found.Output.Split('\n')[0]) : "";
            return first.Length == 0 ? null : first;
        }
        catch (Win32Exception)
        {
            // A machine with no `which` has nothing on its PATH that this can find.
            return null;
        }
    }

    internal readonly record struct Captured(int ExitCode, string Output, string Error);
}
