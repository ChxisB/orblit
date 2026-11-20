using System.Text.RegularExpressions;

namespace Orblit.Editor.App;

/// <summary>Reads what the editor shows from the settings file of a project folder.</summary>
internal static partial class ProjectSettings
{
    const string FileName = "project.orblit";
    const string NameKey = "config/name=";
    const string TagsKey = "config/tags=";
    const string MainSceneKey = "run/main_scene=";

    /// <summary>
    /// The project's name, or an empty string when the file or the key is missing.
    /// shortcut: a name with an escaped quote keeps its backslash. Parse the escapes when a project needs one.
    /// </summary>
    public static string NameOf(string project) => ValueOf(project, NameKey)?.Trim('"') ?? "";

    /// <summary>The quoted values of config/tags, which the file writes as a PackedStringArray, or none.</summary>
    public static IReadOnlyList<string> TagsOf(string project) =>
        ValueOf(project, TagsKey) is { } value ? [.. Quoted().Matches(value).Select(match => match.Groups[1].Value)] : [];

    /// <summary>The scene the project runs first, or main.oscn when the file or the key is missing.</summary>
    public static string MainSceneOf(string project) => ValueOf(project, MainSceneKey)?.Trim('"') ?? "res://main.oscn";

    static string? ValueOf(string project, string key)
    {
        var path = Path.Combine(project, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var line = File.ReadLines(path).FirstOrDefault(line => line.StartsWith(key, StringComparison.Ordinal));
        return line?[key.Length..].Trim();
    }

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex Quoted();
}
