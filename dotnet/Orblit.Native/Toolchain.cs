using System.ComponentModel;
using System.Diagnostics;

namespace Orblit.Native;

/// <summary>
/// A C++ compiler, and what to do with it.
/// Found rather than configured. Somebody who has written a <c>.cpp</c> has a
/// compiler, and asking them to say where it is before their script will run
/// is asking a question the machine can answer.
/// </summary>
/// <param name="Compiler">The compiler executable.</param>
/// <param name="ExtraFlags">Flags this particular compiler needs beyond the common ones.</param>
public sealed record Toolchain(string Compiler, IReadOnlyList<string> ExtraFlags)
{
    /// <summary>What a loadable library is called here.</summary>
    public static string LibrarySuffix =>
        OperatingSystem.IsMacOS() ? ".dylib" : OperatingSystem.IsWindows() ? ".dll" : ".so";

    /// <summary>clang-cl and cl both take MSVC's command line, not clang's own.</summary>
    private bool IsMsvc => Compiler is "clang-cl" or "cl";

    /// <summary>
    /// The first compiler on the PATH, or null.
    /// clang first because it is what macOS ships and what the engine itself is
    /// built with, so a script and the core disagree about the C++ runtime as
    /// rarely as possible. On Windows that means <c>clang-cl</c> before plain
    /// <c>clang++</c>: <c>clang-cl</c> finds an installed Visual Studio's
    /// headers and libraries the way <c>cl.exe</c> would, where <c>clang++</c>
    /// on Windows only links cleanly against a MinGW-style environment. <c>cl</c>
    /// is the last resort, for a machine with Visual Studio and no LLVM.
    /// </summary>
    public static Toolchain? Find(IEnumerable<string>? candidates = null)
    {
        candidates ??= OperatingSystem.IsWindows() ? ["clang-cl", "clang++", "cl"] : ["clang++", "c++", "g++"];
        foreach (var candidate in candidates)
        {
            if (OnPath(candidate))
            {
                return new Toolchain(candidate, ExtraFlagsFor(candidate));
            }
        }

        return null;
    }

    /// <summary>
    /// Compiles one source file into a loadable library.
    /// <paramref name="into"/> is a directory, and the name is chosen here
    /// rather than by the caller. Every build goes to a file of its own: the
    /// system loader hands back the image it already has when the same path is
    /// opened twice, so a rebuilt script would go on running the old code. A new
    /// path is the only reliable way to load new code, and it costs a file.
    /// </summary>
    public BuildResult Compile(
        FileInfo source,
        DirectoryInfo into,
        IReadOnlyList<string> includes,
        int revision = 0,
        IReadOnlyList<string>? flags = null)
    {
        var target = Path.Combine(into.FullName, $"{ScriptNames.Of(source.Name)}.{revision}{LibrarySuffix}");
        var arguments = IsMsvc
            ? MsvcArguments(source, target, includes, flags ?? [])
            : ClangArguments(source, target, includes, flags ?? []);
        var command = $"{Compiler} {string.Join(' ', arguments)}";

        into.Create();

        var start = new ProcessStartInfo(Compiler) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        string output;
        int exitCode;
        try
        {
            using var run = Process.Start(start)!;
            var stdout = run.StandardOutput.ReadToEndAsync();
            var stderr = run.StandardError.ReadToEndAsync();
            run.WaitForExit();
            output = string.Join('\n', new[] { stdout.Result.Trim(), stderr.Result.Trim() }.Where(s => s.Length > 0));
            exitCode = run.ExitCode;
        }
        catch (Win32Exception error)
        {
            return new BuildResult(null, $"Could not run {Compiler}: {error.Message}", command);
        }

        // The exit code decides, not whether the file exists: a compiler that
        // failed part way can leave a truncated object behind, and loading one
        // of those is a crash rather than an error message.
        return new BuildResult(exitCode == 0 && File.Exists(target) ? new FileInfo(target) : null, output, command);
    }

    private static List<string> ExtraFlagsFor(string compiler)
    {
        if (compiler is "clang-cl" or "cl")
        {
            // /LD builds a DLL. The MSVC linker already refuses an unresolved
            // symbol, so there is no stance to take like macOS's -undefined.
            return ["/LD"];
        }

        // A dylib whose install name is its own path, so the loader does not go
        // looking for it beside the executable.
        return OperatingSystem.IsMacOS() ? ["-dynamiclib", "-undefined", "error"] : ["-shared", "-fPIC"];
    }

    private static bool OnPath(string command)
    {
        var file = OperatingSystem.IsWindows() ? command + ".exe" : command;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, file)));
    }

    private List<string> MsvcArguments(FileInfo source, string target, IReadOnlyList<string> includes, IReadOnlyList<string> flags) =>
    [
        // The banner both compilers print unasked, which would otherwise be the
        // first line of "what the compiler said" whether it had anything to say.
        "/nologo",
        .. ExtraFlags,
        "/std:c++17",
        // On by default: a script is somebody's gameplay loop, and the point of
        // writing it in C++ was that it is fast.
        "/O2",
        // The standard exception model. Without it a throw in a script is
        // undefined behaviour rather than a stack unwind.
        "/EHsc",
        .. includes.Select(include => $"/I{include}"),
        .. flags,
        source.FullName,
        $"/Fe:{target}",
        // Otherwise the object file lands beside the source, which may be a
        // project's asset folder and not somewhere this owns.
        $"/Fo:{target}.obj",
    ];

    private List<string> ClangArguments(FileInfo source, string target, IReadOnlyList<string> includes, IReadOnlyList<string> flags) =>
    [
        .. ExtraFlags,
        "-std=c++17",
        "-O2",
        // A loadable library must be position independent everywhere.
        "-fPIC",
        .. includes.SelectMany(include => new[] { "-I", include }),
        .. flags,
        source.FullName,
        "-o",
        target,
    ];
}
