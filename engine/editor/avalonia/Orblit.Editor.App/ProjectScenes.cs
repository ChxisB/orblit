namespace Orblit.Editor.App;

/// <summary>Finds the scene files and the folders of a project folder.</summary>
internal static class ProjectScenes
{
    const string Prefix = "res://";
    const string Pattern = "*.oscn";

    /// <summary>
    /// The res:// path of every text scene under the folder, in order, and of every folder, with a slash on its
    /// end so that an empty folder still has a row. A folder that starts with a dot holds the engine's cache, so
    /// the walk does not enter it.
    /// </summary>
    public static IReadOnlyList<string> Find(string project)
    {
        var found = new List<string>();
        Collect(project, Prefix, found);
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    static void Collect(string folder, string prefix, List<string> found)
    {
        found.AddRange(Directory.EnumerateFiles(folder, Pattern).Select(file => prefix + Path.GetFileName(file)));
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if (!name.StartsWith('.'))
            {
                found.Add($"{prefix}{name}/");
                Collect(child, $"{prefix}{name}/", found);
            }
        }
    }
}
