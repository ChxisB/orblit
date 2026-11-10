namespace Orblit.Editor.Bridge;

/// <summary>Finds the engine's shared library, which a build with library_type=shared_library writes to bin/.</summary>
public static class EngineLibrary
{
    /// <summary>ORBLIT_ENGINE_LIBRARY wins. Otherwise the nearest bin/ above the running program that holds an editor dev library.</summary>
    public static string Find()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ORBLIT_ENGINE_LIBRARY");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return fromEnvironment;
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

    static bool IsLibrary(string path) => Path.GetExtension(path) is ".dylib" or ".so" or ".dll";
}
