using System.Text;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The list of project folders in projects.cfg. It is the file the engine's own project manager keeps, in the
/// same folder and format, so a project made or opened in either one shows in both.
/// </summary>
internal sealed class ProjectList(string file)
{
    public static ProjectList ForUser() => new(Path.Combine(DataFolder(), "projects.cfg"));

    /// <summary>The editor's own settings, in the folder that holds this list.</summary>
    public EditorSettings Settings() => new(Path.Combine(Path.GetDirectoryName(file)!, EditorSettings.FileName)); // a full path always has a parent folder

    // The engine's data folder on each system. .NET maps LocalApplicationData to Application Support on macOS
    // and to the XDG data folder on Linux, where the engine names its folder in lower case.
    public static string DataFolder() => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Orblit")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OperatingSystem.IsMacOS() ? "Orblit" : "orblit");

    /// <summary>The folders in the order the file lists them. Each is a section name, with "]" escaped.</summary>
    public IReadOnlyList<string> Folders() => File.Exists(file)
        ? [.. File.ReadLines(file).Where(line => line.StartsWith('[') && line.EndsWith(']')).Select(line => line[1..^1].Replace("\\]", "]"))]
        : [];

    /// <summary>Adds the folder as the engine writes it, with favorite off. A folder already listed is left as it is.</summary>
    public void Add(string folder)
    {
        if (Folders().Contains(Listed(folder)))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(file)!); // a full path always has a parent folder
        var text = File.Exists(file) ? File.ReadAllText(file) : "";
        var gap = text.Length == 0 ? "" : text.EndsWith('\n') ? "\n" : "\n\n";
        File.AppendAllText(file, $"{gap}{Section(folder)}\n\nfavorite=false\n");
    }

    /// <summary>Drops the folder's section and its keys from the list. The folder and its files stay as they are.</summary>
    public void Remove(string folder)
    {
        if (!File.Exists(file))
        {
            return;
        }

        var dropping = false;
        var kept = new List<string>();
        foreach (var line in File.ReadLines(file))
        {
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                dropping = line == Section(folder);
            }

            if (!dropping)
            {
                kept.Add(line);
            }
        }

        var text = string.Join('\n', kept).TrimEnd('\n');
        File.WriteAllText(file, text.Length == 0 ? "" : text + "\n");
    }

    // The engine keeps forward slashes on Windows too.
    static string Listed(string folder) => OperatingSystem.IsWindows() ? folder.Replace('\\', '/') : folder;

    static string Section(string folder) => $"[{Listed(folder).Replace("]", "\\]")}]";
}

/// <summary>What a launcher card shows of a project folder.</summary>
internal sealed record ProjectEntry(string Folder, string Name, IReadOnlyList<string> Tags, DateTime Edited)
{
    public const string SettingsFile = "project.orblit";

    /// <summary>The folder's name stands in for a project with no name. Edited is when its settings file last changed.</summary>
    public static ProjectEntry Read(string folder)
    {
        var name = ProjectSettings.NameOf(folder);
        return new(
            folder,
            name.Length > 0 ? name : Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)),
            ProjectSettings.TagsOf(folder),
            File.GetLastWriteTimeUtc(Path.Combine(folder, SettingsFile)));
    }
}

/// <summary>Folder text as people type it, with "~" for the home folder.</summary>
internal static class HomeFolder
{
    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Shorten writes the platform separator after the tilde, and people type a slash on every platform.
    public static string Expand(string text) =>
        text == "~" ? Home
        : text.Length > 1 && text[0] == '~' && (text[1] == '/' || text[1] == Path.DirectorySeparatorChar) ? Path.Combine(Home, text[2..])
        : text;

    public static string Shorten(string folder) =>
        folder.StartsWith(Home + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "~" + folder[Home.Length..] : folder;

    /// <summary>The name in lower case with each run of other characters as one dash, or my-game when nothing is left.</summary>
    public static string Slug(string name)
    {
        var slug = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var text = slug.ToString().TrimEnd('-');
        return text.Length > 0 ? text : "my-game";
    }
}
