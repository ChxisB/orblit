namespace Orblit.Editor.App;

internal enum FileKind { Folder, Scene, Script, Graph, Imported }

/// <summary>One row of the file tree. A file row's path is the res:// path to open.</summary>
internal sealed record FileNode(string Name, string Path, FileKind Kind, IReadOnlyList<FileNode> Children);

/// <summary>Builds the rows of the file tree from res:// paths. A path that ends in a slash is a folder.</summary>
internal static class FileTree
{
    internal const string Root = "res://";

    static readonly IComparer<string> s_byName = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// The project folder holding the files in their folders. Files come in the order of their res:// paths under
    /// the comparer, by name when there is none.
    /// </summary>
    public static IReadOnlyList<FileNode> Build(IReadOnlyList<string> files, IComparer<string>? order = null) =>
        [new FileNode(Root, Root, FileKind.Folder, Under(Root, Relative(files), order ?? s_byName))];

    /// <summary>The files whose path holds the text, side by side without their folders. Empty text lists every file.</summary>
    public static IReadOnlyList<FileNode> Matches(IReadOnlyList<string> files, string text, IComparer<string>? order = null) =>
        [.. Relative(files).Where(path => !path.EndsWith('/') && path.Contains(text, StringComparison.OrdinalIgnoreCase)).OrderBy(path => Root + path, order ?? s_byName)
            .Select(path => new FileNode(path, Root + path, KindOf(path), []))];

    /// <summary>Newest file first, and by name among files of one time. A path with no time counts as the oldest.</summary>
    public static IComparer<string> Newest(IReadOnlyDictionary<string, DateTime> modified) => Comparer<string>.Create((a, b) =>
    {
        var newer = modified.GetValueOrDefault(b).CompareTo(modified.GetValueOrDefault(a));
        return newer != 0 ? newer : s_byName.Compare(a, b);
    });

    /// <summary>The kind of a file by its extension. The project lists scenes, C# and graph scripts and the files the engine imports, so any other file is imported.</summary>
    internal static FileKind KindOf(string path) => Path.GetExtension(path) switch
    {
        ".cs" => FileKind.Script,
        ".ograph" => FileKind.Graph,
        ".oscn" => FileKind.Scene,
        _ => FileKind.Imported,
    };

    // A path that is not under res:// is not a project file. A folder the engine and the scripts both name is one row.
    static string[] Relative(IReadOnlyList<string> files) =>
        [.. files.Where(path => path.StartsWith(Root, StringComparison.Ordinal)).Select(path => path[Root.Length..]).Distinct()];

    // Folders come before files, each in name order, as in the old editor.
    static FileNode[] Under(string folder, string[] relative, IComparer<string> order)
    {
        var folders = relative.Where(path => path.Contains('/')).GroupBy(path => path[..path.IndexOf('/')])
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FileNode(group.Key, $"{folder}{group.Key}/", FileKind.Folder,
                Under($"{folder}{group.Key}/", [.. group.Select(path => path[(group.Key.Length + 1)..]).Where(rest => rest.Length > 0)], order)));
        var files = relative.Where(path => !path.Contains('/')).OrderBy(path => folder + path, order)
            .Select(path => new FileNode(path, folder + path, KindOf(path), []));
        return [.. folders, .. files];
    }
}
