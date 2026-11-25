using System.Text;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The editor's own settings in editor_settings.cfg. The file has a section heading and key=value lines, as
/// projects.cfg has, with each text value quoted. A missing file or key gives the default.
/// </summary>
internal sealed class EditorSettings(string file)
{
    public const string FileName = "editor_settings.cfg";
    public const string DefaultProjectsFolder = "~/Games";
    const string ProjectsKey = "projects_folder=";

    /// <summary>The folder New project puts each new project in, as people type it, with "~" for home.</summary>
    public string ProjectsFolder()
    {
        try
        {
            var line = File.Exists(file) ? File.ReadLines(file).FirstOrDefault(line => line.StartsWith(ProjectsKey, StringComparison.Ordinal)) : null;
            var folder = line is null ? "" : Unquote(line[ProjectsKey.Length..].Trim());
            return folder.Length > 0 ? folder : DefaultProjectsFolder;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An unreadable file only moves where New project starts, so the default stands in.
            return DefaultProjectsFolder;
        }
    }

    /// <summary>
    /// Saves the folder. It need not exist yet, since New project makes it with the project.
    /// shortcut: the file is written whole with its one key. Keep the other lines when a second setting comes.
    /// </summary>
    public void SetProjectsFolder(string folder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); // a full path always has a parent folder
        var quoted = folder.Replace("\\", "\\\\").Replace("\"", "\\\"");
        File.WriteAllText(file, $"[editor]\n\n{ProjectsKey}\"{quoted}\"\n");
    }

    // A backslash in a quoted value keeps the character after it, as the engine's own files write it.
    static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var text = new StringBuilder();
        for (var at = 1; at < value.Length - 1; at++)
        {
            if (value[at] == '\\' && at + 1 < value.Length - 1)
            {
                at++;
            }

            text.Append(value[at]);
        }

        return text.ToString();
    }
}
