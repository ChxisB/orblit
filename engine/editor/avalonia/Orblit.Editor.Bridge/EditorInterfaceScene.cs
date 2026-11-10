namespace Orblit.Editor.Bridge;

/// <summary>The scene of an engine started with --editor, reached through EditorInterface.</summary>
public sealed class EditorInterfaceScene : ISceneSource
{
    const int ScanFrameLimit = 3000;
    const int OpenFrames = 30;
    const int SaveFrames = 10;
    const int OpenSceneLimit = 64;

    readonly EngineHost _host;
    readonly nint _editor;

    public EditorInterfaceScene(EngineHost host)
    {
        _host = host;
        _editor = host.Singleton("EditorInterface");
        if (_editor == 0)
        {
            throw new InvalidOperationException("The engine has no EditorInterface. Start it with --editor and call Start first.");
        }
    }

    /// <summary>Runs frames until the project's file scan ends. Throws when it never does.</summary>
    public void PumpUntilScanned()
    {
        var filesystem = _host.EditorInterfaceGetResourceFilesystem(_editor);
        for (var frame = 0; frame < ScanFrameLimit; frame++)
        {
            _host.Iteration();
            if (!_host.EditorFileSystemIsScanning(filesystem))
            {
                return;
            }
        }
        throw new TimeoutException($"The project scan did not finish in {ScanFrameLimit} frames.");
    }

    public nint Root() => _host.EditorInterfaceGetEditedSceneRoot(_editor);

    public void Open(string resourcePath)
    {
        _host.EditorInterfaceOpenSceneFromPath(_editor, resourcePath, false);
        _host.Pump(OpenFrames);
    }

    // The editor keeps every scene it opened as a tab, so closing one shows the next. Root is zero only when all are closed.
    public void Close()
    {
        for (var closed = 0; Root() != 0; closed++)
        {
            if (closed == OpenSceneLimit)
            {
                throw new InvalidOperationException($"{OpenSceneLimit} scenes were closed and one is still open.");
            }
            var error = _host.EditorInterfaceCloseScene(_editor);
            _host.Pump(OpenFrames);
            if (error != 0)
            {
                throw new IOException($"The engine could not close the scene. Error code {error}.");
            }
        }
    }

    public void Save()
    {
        var error = _host.EditorInterfaceSaveScene(_editor);
        _host.Pump(SaveFrames);
        if (error != 0)
        {
            throw new IOException($"The engine could not save the scene. Error code {error}.");
        }
    }
}
