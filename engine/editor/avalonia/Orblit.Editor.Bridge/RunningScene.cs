namespace Orblit.Editor.Bridge;

/// <summary>
/// The scene of an engine started as a game. A game has no EditorInterface, so the scene opens through
/// Stage and saves by packing the root into a Prefab. Only entities the root owns are packed, so an
/// entity added without an owner, such as an editor camera, never reaches the file.
/// </summary>
public sealed class RunningScene(EngineHost host) : ISceneSource
{
    // change_scene_to_file waits for the end of a frame, then builds the new scene on the next one.
    const int ChangeFrames = 3;

    public nint Root() => host.StageGetCurrentScene(Stage());

    public void Open(string resourcePath)
    {
        var error = host.StageChangeSceneToFile(Stage(), resourcePath);
        if (error != 0)
        {
            throw new IOException($"The engine could not open {resourcePath}. Error code {error}.");
        }
        host.Pump(ChangeFrames);
        if (Root() == 0)
        {
            throw new IOException($"{resourcePath} did not open a scene.");
        }
    }

    public void Save()
    {
        var root = Root();
        var path = root == 0 ? "" : host.EntityGetSceneFilePath(root);
        if (path.Length == 0)
        {
            throw new InvalidOperationException("The open scene has no file to save to.");
        }

        PackedFile.Write(host, root, path);
    }

    public void Close()
    {
        host.StageUnloadCurrentScene(Stage());
        host.Pump(ChangeFrames);
    }

    nint Stage() => host.EngineGetMainLoop(host.Singleton("Engine"));
}
