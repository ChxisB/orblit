namespace Orblit.Editor.App;

/// <summary>Reads and writes the breakpoints of the graph scripts of a project, in the editor data folder of the project.</summary>
internal static class BreakpointFile
{
    // The engine keeps the editor data of a project here, apart from its scripts and out of version control.
    const string Folder = ".orblit/editor";
    const string Name = "graph_breakpoints.json";

    /// <summary>The text of the file, or null when the project has none.</summary>
    public static string? Read(string project)
    {
        var file = Path.Combine(project, Folder, Name);
        return File.Exists(file) ? File.ReadAllText(file) : null;
    }

    /// <summary>Replaces the file. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when it cannot be written.</summary>
    public static void Write(string project, string text)
    {
        var folder = Path.Combine(project, Folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Name), text);
    }
}
