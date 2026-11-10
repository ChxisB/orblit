namespace Orblit.Editor.App;

/// <summary>Reads what the editor shows from the settings file of a project folder.</summary>
internal static class ProjectSettings
{
    const string FileName = "project.orblit";
    const string NameKey = "config/name=";

    /// <summary>
    /// The project's name, or an empty string when the file or the key is missing.
    /// shortcut: a name with an escaped quote keeps its backslash. Parse the escapes when a project needs one.
    /// </summary>
    public static string NameOf(string project)
    {
        var path = Path.Combine(project, FileName);
        if (!File.Exists(path))
        {
            return "";
        }

        var line = File.ReadLines(path).FirstOrDefault(line => line.StartsWith(NameKey, StringComparison.Ordinal));
        return line?[NameKey.Length..].Trim().Trim('"') ?? "";
    }
}
