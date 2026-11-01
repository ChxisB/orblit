using System.Reflection;
using System.Runtime.InteropServices;

namespace Orblit.Interop;

/// <summary>
/// Finds the native libraries that ship in a package's
/// <c>runtimes/{rid}/native</c> folder. It looks there first, then beside the
/// app, and when it finds nothing it says every path it tried.
/// </summary>
public static class NativeLoader
{
    /// <summary>
    /// The runtime id of this machine as the runtimes folder names it, for
    /// example <c>osx-arm64</c>. Only the desktop systems are known.
    /// </summary>
    public static string RuntimeId { get; } = $"{OsName()}-{ArchitectureName()}";

    /// <summary>The file a library called <paramref name="library"/> has on this system.</summary>
    public static string FileName(string library)
    {
        if (OperatingSystem.IsWindows())
        {
            return library + ".dll";
        }

        return OperatingSystem.IsMacOS() ? $"lib{library}.dylib" : $"lib{library}.so";
    }

    /// <summary>The paths <see cref="Load"/> tries, in order.</summary>
    public static IReadOnlyList<string> SearchPaths(string library, string baseDirectory) =>
    [
        Path.Combine(baseDirectory, "runtimes", RuntimeId, "native", FileName(library)),
        Path.Combine(baseDirectory, FileName(library)),
    ];

    /// <summary>Loads the library, or throws <see cref="DllNotFoundException"/> naming each path tried.</summary>
    public static nint Load(string library, string baseDirectory)
    {
        var paths = SearchPaths(library, baseDirectory);
        foreach (var path in paths)
        {
            if (NativeLibrary.TryLoad(path, out var handle))
            {
                return handle;
            }
        }

        var tried = paths.Select(p => $"  {p} ({(File.Exists(p) ? "found, but it would not load" : "not found")})");
        throw new DllNotFoundException(
            $"Could not load the native library '{library}' for {RuntimeId}. Tried:{Environment.NewLine}"
            + string.Join(Environment.NewLine, tried));
    }

    /// <summary>
    /// Makes <c>[LibraryImport("name")]</c> calls in <paramref name="assembly"/> find
    /// <paramref name="library"/> through <see cref="Load"/>. The runtime allows one
    /// resolver per assembly.
    /// </summary>
    public static void Register(Assembly assembly, string library) =>
        NativeLibrary.SetDllImportResolver(
            assembly,
            (name, _, _) => name == library ? Load(library, AppContext.BaseDirectory) : nint.Zero);

    private static string OsName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return OperatingSystem.IsLinux()
            ? "linux"
            : throw new PlatformNotSupportedException("Only Windows, macOS and Linux have native libraries so far.");
    }

    private static string ArchitectureName() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        var other => throw new PlatformNotSupportedException($"The {other} architecture has no native libraries."),
    };
}
