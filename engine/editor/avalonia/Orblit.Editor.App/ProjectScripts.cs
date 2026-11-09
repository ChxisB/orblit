namespace Orblit.Editor.App;

/// <summary>Finds the C# script files of a project folder.</summary>
internal static class ProjectScripts
{
    const string Prefix = "res://";
    const string Pattern = "*.cs";

    // A folder that starts with a dot holds the engine's cache. bin and obj hold the build's output, which is not a script
    // the project wrote.
    static readonly string[] s_buildFolders = ["bin", "obj"];

    /// <summary>The res:// path of every C# file under the folder, in order.</summary>
    public static IReadOnlyList<string> Find(string project)
    {
        var found = new List<string>();
        Collect(project, Prefix, found);
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>
    /// The file on disk for a res:// path. Throws <see cref="ArgumentException"/> for a path that is no C# file or that
    /// leads out of the project folder.
    /// </summary>
    public static string FileOf(string project, string resourcePath)
    {
        if (!resourcePath.StartsWith(Prefix, StringComparison.Ordinal) || !resourcePath.EndsWith(".cs", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{resourcePath} is no C# file under {Prefix}.", nameof(resourcePath));
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project));
        var file = Path.GetFullPath(Path.Combine(root, resourcePath[Prefix.Length..]));
        // The separator keeps a sibling folder that starts with the project's name out.
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"{resourcePath} leads out of the project.", nameof(resourcePath));
        }

        return file;
    }

    static void Collect(string folder, string prefix, List<string> found)
    {
        found.AddRange(Directory.EnumerateFiles(folder, Pattern).Select(file => prefix + Path.GetFileName(file)));
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if (!name.StartsWith('.') && !s_buildFolders.Contains(name))
            {
                Collect(child, $"{prefix}{name}/", found);
            }
        }
    }
}
