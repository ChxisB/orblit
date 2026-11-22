using System.IO.Compression;

namespace Orblit.Editor.App;

/// <summary>
/// Unpacks a kit into a project. A kit is a zip of project files. When every entry sits in one folder, that
/// folder is taken off, so a kit zipped from its own folder lands at the project's root.
/// </summary>
internal static class KitArchive
{
    /// <summary>
    /// Writes every file of the zip under the project folder. Every entry is checked before any file is written:
    /// one that would land outside the project throws <see cref="InvalidDataException"/>, and one the project has
    /// already throws <see cref="IOException"/>. A file that is not a zip throws <see cref="InvalidDataException"/>.
    /// </summary>
    public static void Install(string zipFile, string project)
    {
        using var zip = ZipFile.OpenRead(zipFile);
        var files = zip.Entries.Where(entry => !entry.FullName.EndsWith('/')).ToList();
        if (files.Count == 0)
        {
            throw new InvalidDataException("The kit holds no files.");
        }

        var top = SharedFolder(files);
        var root = Path.GetFullPath(project) + Path.DirectorySeparatorChar;
        var targets = files.Select(entry => (Entry: entry, File: TargetOf(entry.FullName[top.Length..], root))).ToList();
        // A name the zip holds twice would also fail halfway, so it counts as taken.
        var seen = new HashSet<string>();
        var taken = targets.FirstOrDefault(target => !seen.Add(target.File) || Path.Exists(target.File));
        if (taken.Entry is not null)
        {
            throw new IOException($"{taken.Entry.FullName[top.Length..]} is in the project already.");
        }

        foreach (var (entry, file) in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            entry.ExtractToFile(file);
        }
    }

    // The folder every file sits in, with its slash, or an empty string when they do not share one.
    static string SharedFolder(List<ZipArchiveEntry> files)
    {
        var first = files[0].FullName;
        var slash = first.IndexOf('/');
        var top = slash > 0 ? first[..(slash + 1)] : "";
        return top.Length > 0 && files.All(entry => entry.FullName.StartsWith(top, StringComparison.Ordinal)) ? top : "";
    }

    // A kit's names are untrusted, so a name that resolves outside the project is refused rather than written.
    static string TargetOf(string name, string root)
    {
        var file = Path.GetFullPath(Path.Combine(root, name));
        if (Path.IsPathRooted(name) || !file.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} is outside the project.");
        }

        return file;
    }
}
