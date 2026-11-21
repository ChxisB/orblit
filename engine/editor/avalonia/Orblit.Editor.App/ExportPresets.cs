using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Orblit.Editor.App;

/// <summary>A way to export the project, as a section of export_presets.cfg names it. The path is a full path, or empty.</summary>
internal sealed record ExportPreset(string Name, string Platform, string Path);

/// <summary>Reads the project's export presets from export_presets.cfg and adds one for the computer the editor runs on.</summary>
internal static partial class ExportPresets
{
    const string FileName = "export_presets.cfg";

    /// <summary>The engine's name for this computer's platform, the end of its program's name and the build it exports.</summary>
    public static (string Platform, string Extension, string Architecture) ThisComputer { get; } =
        OperatingSystem.IsMacOS() ? ("macOS", ".app", "universal")
        : OperatingSystem.IsWindows() ? ("Windows Desktop", ".exe", "x86_64")
        : ("Linux", ".x86_64", "x86_64");

    /// <summary>
    /// The presets in the order the file numbers them, or none when the file is missing.
    /// shortcut: a value with an escaped quote keeps its backslash, as in <see cref="ProjectSettings"/>.
    /// </summary>
    public static IReadOnlyList<ExportPreset> Read(string project)
    {
        var file = System.IO.Path.Combine(project, FileName);
        if (!File.Exists(file))
        {
            return [];
        }

        List<Dictionary<string, string>> sections = [];
        Dictionary<string, string>? keys = null;
        foreach (var line in File.ReadLines(file))
        {
            var equals = line.IndexOf('=');
            if (line.StartsWith('['))
            {
                // A preset's options sit in a section of their own, which this skips.
                keys = PresetHeading().IsMatch(line) ? [] : null;
                if (keys is not null)
                {
                    sections.Add(keys);
                }
            }
            else if (keys is not null && equals > 0)
            {
                keys[line[..equals]] = line[(equals + 1)..].Trim('"');
            }
        }

        return [.. sections.Select(keys => new ExportPreset(keys.GetValueOrDefault("name", ""), keys.GetValueOrDefault("platform", ""), FullPath(keys.GetValueOrDefault("export_path", ""), project)))];
    }

    // The engine reads a relative export path from the project's folder.
    static string FullPath(string path, string project) => path.Length == 0 ? "" : System.IO.Path.GetFullPath(path, project);

    /// <summary>Adds a preset for this computer, named after its platform, that exports to the project's build folder.</summary>
    public static void AddForThisComputer(string project) => Add(project, ThisComputer);

    /// <summary>
    /// Adds a preset for a platform. A Mac build also needs a bundle id, and textures imported for its GPUs,
    /// which the project's settings turn on.
    /// </summary>
    internal static void Add(string project, (string Platform, string Extension, string Architecture) host)
    {
        var name = ProjectSettings.NameOf(project);
        var game = string.Concat(name.Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_')).Trim();
        game = game.Length > 0 ? game : "Game";
        var index = Read(project).Count;
        var text = new StringBuilder(index == 0 ? "" : "\n");
        text.Append(CultureInfo.InvariantCulture, $"[preset.{index}]\n\nname=\"{host.Platform}\"\nplatform=\"{host.Platform}\"\nrunnable=true\n");
        text.Append(CultureInfo.InvariantCulture, $"export_filter=\"all_resources\"\ninclude_filter=\"\"\nexclude_filter=\"\"\nexport_path=\"build/{game}{host.Extension}\"\n\n");
        // The engine logs an error for an options section with no keys, so every preset names its build.
        text.Append(CultureInfo.InvariantCulture, $"[preset.{index}.options]\n\nbinary_format/architecture=\"{host.Architecture}\"\n");
        if (host.Platform == "macOS")
        {
            var word = string.Concat(game.Where(char.IsAsciiLetterOrDigit)).ToLowerInvariant();
            text.Append(CultureInfo.InvariantCulture, $"application/bundle_identifier=\"com.example.{(word.Length > 0 && char.IsAsciiLetter(word[0]) ? word : "game" + word)}\"\n");
            ProjectSettings.SetValue(project, "rendering", "textures/vram_compression/import_etc2_astc=true");
        }

        File.AppendAllText(System.IO.Path.Combine(project, FileName), text.ToString());
    }

    [GeneratedRegex(@"^\[preset\.\d+\]$")]
    private static partial Regex PresetHeading();
}
