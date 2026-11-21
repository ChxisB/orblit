using System.IO.Compression;

namespace Orblit.Editor.App;

/// <summary>
/// Installs export templates from a .tpz, the zip a release ships them in, into the folder the engine looks in:
/// one folder per version under export_templates in the engine's data folder.
/// </summary>
internal static class ExportTemplates
{
    const string Top = "templates/";

    /// <summary>The engine's data folder for this user, which holds its export_templates folder.</summary>
    // shortcut: an engine in self-contained mode keeps its data beside its program. Read its _sc_ file when the editor ships that mode.
    public static string DataFolder =>
        OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Orblit")
        : OperatingSystem.IsMacOS() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Orblit")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "orblit");

    /// <summary>
    /// Unpacks the templates into a folder named by their templates/version.txt, in place of what that folder held.
    /// Every entry is checked before any file is written, and a file that is not a zip, a missing or odd version,
    /// or an entry that would land outside the folder throws <see cref="InvalidDataException"/>.
    /// </summary>
    public static void Install(string tpz, string dataFolder)
    {
        using var zip = ZipFile.OpenRead(tpz);
        var version = VersionOf(zip);
        // The version names a folder, so it must be one plain name. The engine reads at least three parts.
        var parts = version.Split('.');
        if (parts.Length < 3 || !parts.All(part => part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
        {
            throw new InvalidDataException("The file holds no templates/version.txt with a version such as 1.0.stable.");
        }

        var folder = Path.GetFullPath(Path.Combine(dataFolder, "export_templates", version));
        var targets = zip.Entries
            .Where(file => file.FullName.StartsWith(Top, StringComparison.Ordinal) && !file.FullName.EndsWith('/'))
            .Select(file => (Entry: file, File: Path.GetFullPath(Path.Combine(folder, file.FullName[Top.Length..]))))
            .ToList();
        var outside = targets.FirstOrDefault(target => !target.File.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        if (outside.Entry is not null)
        {
            throw new InvalidDataException($"{outside.Entry.FullName} is outside the templates folder.");
        }

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        foreach (var (file, target) in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            file.ExtractToFile(target);
        }
    }

    static string VersionOf(ZipArchive zip)
    {
        if (zip.GetEntry(Top + "version.txt") is not { } entry)
        {
            return "";
        }

        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd().Trim();
    }
}
