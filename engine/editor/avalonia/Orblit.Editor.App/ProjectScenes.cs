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

    /// <summary>
    /// "2D" or "3D" for a scene whose root is a 2D or a 3D entity, read from the first entity line of the scene's
    /// text. Empty for any other root, and for a root that is another scene.
    /// </summary>
    // shortcut: reads the end of the root's class name, which every 2D and 3D class of the engine keeps. Ask the class registry if a class breaks that.
    public static string KindOf(IEnumerable<string> lines)
    {
        var root = lines.FirstOrDefault(line => line.StartsWith("[node ", StringComparison.Ordinal)) ?? "";
        var type = root.Split("type=\"") is [_, var rest, ..] ? rest[..Math.Max(rest.IndexOf('"'), 0)] : "";
        return type.EndsWith("2D", StringComparison.Ordinal) ? "2D" : type.EndsWith("3D", StringComparison.Ordinal) ? "3D" : "";
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
