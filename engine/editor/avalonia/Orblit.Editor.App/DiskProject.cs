namespace Orblit.Editor.App;

/// <summary>What a project's folder says about it, which needs no engine.</summary>
internal sealed class DiskProject(EditorOptions options) : IProjectFiles
{
    readonly string _project = options.Project;

    public string SceneFile { get; } = options.HasScene ? options.Scene : "";

    // Read once, because the title asks for it on every edit and the name does not change while the editor runs.
    public string ProjectName { get; } = ProjectSettings.NameOf(options.Project);

    public IReadOnlyList<string> Scenes() => ProjectScenes.Find(_project);

    public IReadOnlyList<string> Scripts() => ProjectScripts.Find(_project);

    public IReadOnlyList<string> GraphScripts() => ProjectScripts.FindGraphs(_project);

    public IReadOnlyList<string> ImportedFiles() => ProjectScripts.FindImported(_project);

    public DateTime ModifiedOf(string resourcePath) => File.GetLastWriteTimeUtc(PathOf(resourcePath));

    // shortcut: reads the head of each scene file at each Search. Keep the kinds by write time if a project has thousands of scenes.
    public string SceneKindOf(string resourcePath)
    {
        try
        {
            return ProjectScenes.KindOf(File.ReadLines(PathOf(resourcePath)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    string PathOf(string resourcePath) => Path.Combine(_project, resourcePath[FileTree.Root.Length..]);
}
