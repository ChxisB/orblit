using System.Text.RegularExpressions;

namespace Orblit.Editor.App;

/// <summary>Reads what the editor shows from the settings file of a project folder.</summary>
internal static partial class ProjectSettings
{
    const string FileName = "project.orblit";
    const string NameKey = "config/name=";
    const string TagsKey = "config/tags=";
    const string MainSceneKey = "run/main_scene=";

    /// <summary>The project's name with its escapes read, or an empty string when the file or the key is missing.</summary>
    public static string NameOf(string project) => ValueOf(project, NameKey) is { } value ? Escaped().Replace(Unquoted(value), "$1") : "";

    /// <summary>Writes the project's name under [application], quoted, with its quotes and backslashes escaped.</summary>
    public static void SetName(string project, string name) =>
        SetValue(project, "application", $"{NameKey}\"{name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");

    /// <summary>The quoted values of config/tags, which the file writes as a PackedStringArray, or none.</summary>
    public static IReadOnlyList<string> TagsOf(string project) =>
        ValueOf(project, TagsKey) is { } value ? [.. Quoted().Matches(value).Select(match => match.Groups[1].Value)] : [];

    /// <summary>The scene the project runs first, or main.oscn when the file or the key is missing.</summary>
    public static string MainSceneOf(string project) => ValueOf(project, MainSceneKey)?.Trim('"') ?? "res://main.oscn";

    /// <summary>
    /// Writes one key=value line into a section of the settings file. A key the file has takes the new value in
    /// its place. A new key goes under the section's heading, and a section the file lacks is added at the end.
    /// </summary>
    public static void SetValue(string project, string section, string line)
    {
        var path = Path.Combine(project, FileName);
        var lines = File.ReadAllLines(path).ToList();
        var key = line[..(line.IndexOf('=') + 1)];
        var at = lines.FindIndex(next => next.StartsWith(key, StringComparison.Ordinal));
        var heading = lines.IndexOf($"[{section}]");
        if (at >= 0)
        {
            lines[at] = line;
        }
        else if (heading >= 0)
        {
            // The engine writes a blank line under each heading.
            lines.Insert(Math.Min(heading + 2, lines.Count), line);
        }
        else
        {
            lines.AddRange(["", $"[{section}]", "", line]);
        }

        File.WriteAllText(path, string.Join('\n', lines) + "\n");
    }

    static string Unquoted(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

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

    // A backslash and the character it escapes.
    [GeneratedRegex(@"\\(.)")]
    private static partial Regex Escaped();
}
