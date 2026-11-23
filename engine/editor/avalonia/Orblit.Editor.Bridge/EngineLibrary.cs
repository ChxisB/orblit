namespace Orblit.Editor.Bridge;

/// <summary>Finds the engine's shared library, which a build with library_type=shared_library writes to bin/.</summary>
public static class EngineLibrary
{
    /// <summary>
    /// ORBLIT_ENGINE_LIBRARY wins. Then the library beside the running program, where a release puts it. Otherwise the
    /// nearest bin/ above the running program that holds an editor dev library.
    /// </summary>
    public static string Find()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ORBLIT_ENGINE_LIBRARY");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return fromEnvironment;
        }
        if (InFolder(AppContext.BaseDirectory) is { } beside)
        {
            return beside;
        }
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var bin = Path.Combine(folder.FullName, "bin");
            if (Directory.Exists(bin) && Directory.GetFiles(bin, "liborblit.*.editor.dev.*").FirstOrDefault(IsLibrary) is { } found)
            {
                return found;
            }
        }
        throw new FileNotFoundException(
            "No engine library found. Build one with: scons platform=macos target=editor dev_build=yes library_type=shared_library, or set ORBLIT_ENGINE_LIBRARY.");
    }

    /// <summary>The editor library in the folder: liborblit.&lt;tags&gt; on macOS and Linux, orblit.&lt;tags&gt;.dll on Windows.</summary>
    // The names are matched with case, because the app's own Orblit.Editor.*.dll files share the folder.
    internal static string? InFolder(string folder) => Directory.GetFiles(folder).FirstOrDefault(path =>
        IsLibrary(path)
        && Path.GetFileName(path) is var name
        && (name.StartsWith("liborblit.", StringComparison.Ordinal) || name.StartsWith("orblit.", StringComparison.Ordinal))
        && name.Contains(".editor.", StringComparison.Ordinal));

    static bool IsLibrary(string path) => Path.GetExtension(path) is ".dylib" or ".so" or ".dll";
}
