namespace Orblit.Editor.App;

internal enum FileKind { Favorites, Folder, Scene }

/// <summary>One row of the file tree. A scene row's path is the res:// path to open.</summary>
internal sealed record FileNode(string Name, string Path, FileKind Kind, IReadOnlyList<FileNode> Children);

/// <summary>Builds the rows of the file tree from the res:// paths of the scenes. A path that ends in a slash is a folder.</summary>
internal static class FileTree
{
    internal const string Root = "res://";

    static readonly IComparer<string> s_byName = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// The favorites row and the project folder holding the scenes in their folders. Files come in the order of
    /// their res:// paths under the comparer, by name when there is none.
    /// </summary>
    public static IReadOnlyList<FileNode> Build(IReadOnlyList<string> scenes, IComparer<string>? order = null) =>
        Roots(Under(Root, Relative(scenes), order ?? s_byName));

    /// <summary>The same two rows with the scenes whose path holds the text, side by side without their folders.</summary>
    public static IReadOnlyList<FileNode> Matches(IReadOnlyList<string> scenes, string text, IComparer<string>? order = null) =>
        Roots(Relative(scenes).Where(path => !path.EndsWith('/') && path.Contains(text, StringComparison.OrdinalIgnoreCase)).OrderBy(path => Root + path, order ?? s_byName)
            .Select(path => new FileNode(path, Root + path, FileKind.Scene, [])).ToArray());

    /// <summary>Newest file first, and by name among files of one time. A path with no time counts as the oldest.</summary>
    public static IComparer<string> Newest(IReadOnlyDictionary<string, DateTime> modified) => Comparer<string>.Create((a, b) =>
    {
        var newer = modified.GetValueOrDefault(b).CompareTo(modified.GetValueOrDefault(a));
        return newer != 0 ? newer : s_byName.Compare(a, b);
    });

    static FileNode[] Roots(FileNode[] project) =>
    [
        new FileNode("Favorites", "", FileKind.Favorites, []),
        new FileNode(Root, Root, FileKind.Folder, project),
    ];

    // A path that is not under res:// is not a project file.
    static string[] Relative(IReadOnlyList<string> scenes) =>
        [.. scenes.Where(path => path.StartsWith(Root, StringComparison.Ordinal)).Select(path => path[Root.Length..])];

    // Folders come before files, each in name order, as in the old editor.
    static FileNode[] Under(string folder, string[] relative, IComparer<string> order)
    {
        var folders = relative.Where(path => path.Contains('/')).GroupBy(path => path[..path.IndexOf('/')])
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FileNode(group.Key, $"{folder}{group.Key}/", FileKind.Folder,
                Under($"{folder}{group.Key}/", [.. group.Select(path => path[(group.Key.Length + 1)..]).Where(rest => rest.Length > 0)], order)));
        var files = relative.Where(path => !path.Contains('/')).OrderBy(path => folder + path, order)
            .Select(path => new FileNode(path, folder + path, FileKind.Scene, []));
        return [.. folders, .. files];
    }
}
