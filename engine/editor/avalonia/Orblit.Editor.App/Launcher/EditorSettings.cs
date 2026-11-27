using System.Text;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// The editor's own settings in editor_settings.cfg. The file has section headings and key=value lines, as
/// projects.cfg has, with each text value quoted. A missing file or key gives the default.
/// </summary>
internal sealed class EditorSettings(string file)
{
    public const string FileName = "editor_settings.cfg";
    public const string DefaultProjectsFolder = "~/Games";
    const string EditorSection = "editor";
    const string ProjectsKey = "projects_folder";

    /// <summary>The folder New project puts each new project in, as people type it, with "~" for home.</summary>
    public string ProjectsFolder() =>
        Section(EditorSection).GetValueOrDefault(ProjectsKey) is { Length: > 0 } folder ? folder : DefaultProjectsFolder;

    /// <summary>Saves the folder. It need not exist yet, since New project makes it with the project.</summary>
    public void SetProjectsFolder(string folder) =>
        Rewrite(EditorSection, line => line.StartsWith($"{ProjectsKey}=", StringComparison.Ordinal), [Line(ProjectsKey, folder)]);

    /// <summary>The keys and values of a section. A section that cannot be read has none.</summary>
    public IReadOnlyDictionary<string, string> Section(string name)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file) : [];
            foreach (var line in lines.SkipWhile(line => line.TrimEnd() != $"[{name}]").Skip(1).TakeWhile(line => !line.StartsWith('[')))
            {
                if (line.IndexOf('=') is > 0 and var at)
                {
                    values[line[..at].Trim()] = Unquote(line[(at + 1)..].Trim());
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An unreadable file only costs what was remembered, so the defaults stand in.
        }

        return values;
    }

    /// <summary>Saves a section whole. The other sections keep their lines.</summary>
    public void SetSection(string name, IEnumerable<KeyValuePair<string, string>> values) =>
        Rewrite(name, line => line.Contains('='), values.Select(each => Line(each.Key, each.Value)));

    // Takes the section's lines that go, and puts the new ones where the first of them was, or after its last line. A
    // file that cannot be read throws before anything is written, and the new file takes the old one's place whole.
    void Rewrite(string section, Predicate<string> goes, IEnumerable<string> added)
    {
        var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : [];
        var heading = lines.FindIndex(line => line.TrimEnd() == $"[{section}]");
        if (heading < 0)
        {
            lines.AddRange(lines.Count > 0 ? ["", $"[{section}]", ""] : [$"[{section}]", ""]);
            heading = lines.Count - 2;
        }

        var next = lines.FindIndex(heading + 1, line => line.StartsWith('['));
        var end = next < 0 ? lines.Count : next;
        var body = lines.GetRange(heading + 1, end - heading - 1);
        var at = body.FindIndex(goes);
        body.RemoveAll(goes);
        // A section with nothing in it starts after the blank line under its heading.
        at = at >= 0 ? at : Math.Max(body.FindLastIndex(line => line.Length > 0) + 1, Math.Min(1, body.Count));
        body.InsertRange(at, added);
        lines.RemoveRange(heading + 1, end - heading - 1);
        lines.InsertRange(heading + 1, body);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!); // a full path always has a parent folder
        // A linked file, as one kept with someone's dotfiles, stays a link. The process id keeps two editors apart.
        var link = new FileInfo(file);
        var target = link.LinkTarget is null ? file : link.ResolveLinkTarget(true)!.FullName;
        var temp = $"{target}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, string.Join('\n', lines) + "\n");
        File.Move(temp, target, true);
    }

    static string Line(string key, string value) => $"{key}=\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

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
