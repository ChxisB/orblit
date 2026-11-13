using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The engine for one project, and the game process that Play starts. <see cref="Start"/> runs a headless
/// editor engine, which draws nothing. <see cref="StartViewport"/> runs the project as a game with a
/// window, so there are frames to show, and edits the scene while it runs.
/// </summary>
internal sealed class EditorEngine : IEditorScene, IDisposable
{
    // The engine window's first size. The control then asks for its own.
    const string ViewportResolution = "960x540";

    readonly EngineHost _host;
    readonly EditorSession _session;
    readonly ViewportParts? _viewport;
    readonly string _project;
    readonly EngineDebugChannel _debug;
    Process? _game;

    EditorEngine(EngineHost host, EditorSession session, ViewportParts? viewport, EditorOptions options)
    {
        _host = host;
        _debug = new EngineDebugChannel(host);
        _session = session;
        _viewport = viewport;
        _project = options.Project;
        ProjectName = ProjectSettings.NameOf(options.Project);
        SceneFile = options.HasScene ? options.Scene : "";
    }

    /// <summary>Follows the log files of this engine and of every game Play starts.</summary>
    internal required OutputLog Log { get; init; }

    // What a windowed engine adds: the frame reader and the camera that moves the view.
    sealed record ViewportParts(ViewportReadback Readback, EditorCamera Camera, SelectionBox Box, PlanarCamera Planar);

    public static EditorEngine Start(EditorOptions options)
    {
        var log = new OutputLog();
        EngineHost? host = null;
        try
        {
            host = EngineHost.Create(EngineLibrary.Find(), ["--headless", "--editor", "--path", options.Project, "--log-file", log.NewFile()]);
            host.Start();
            var scene = new EditorInterfaceScene(host);
            scene.PumpUntilScanned();
            var session = new EditorSession(host, scene);
            OpenFirstScene(session, options);
            return new EditorEngine(host, session, null, options) { Log = log };
        }
        catch
        {
            host?.Dispose();
            log.Dispose();
            throw;
        }
    }

    public static EditorEngine StartViewport(EditorOptions options)
    {
        var log = new OutputLog();
        EngineHost? host = null;
        ViewportReadback? readback = null;
        try
        {
            host = EngineHost.Create(EngineLibrary.Find(), ["--path", options.Project, "--resolution", ViewportResolution, "--log-file", log.NewFile()]);
            host.Start();
            var session = new EditorSession(host, new RunningScene(host));
            OpenFirstScene(session, options);
            readback = new ViewportReadback(host);
            EditorWorld.Build(host);
            var camera = new EditorCamera(host);
            return new EditorEngine(host, session, new ViewportParts(readback, camera, new SelectionBox(host), new PlanarCamera(host)), options) { Log = log };
        }
        catch
        {
            readback?.Dispose();
            host?.Dispose();
            log.Dispose();
            throw;
        }
    }

    /// <summary>A control that shows this engine's frames and moves its camera, or null for a headless engine.</summary>
    public ViewportControl? CreateViewport()
    {
        if (_viewport is null)
        {
            return null;
        }

        var parts = _viewport;
        var control = new ViewportControl(_host.Iteration, parts.Readback.TryRead);
        control.Orbited += parts.Camera.Orbit;
        control.PlanarPanned += parts.Planar.Pan;
        control.Zoomed += steps =>
        {
            if (parts.Planar.IsOn)
            {
                parts.Planar.Zoom(steps);
            }
            else
            {
                parts.Camera.Zoom(steps);
            }
        };
        control.SizeWanted += _viewport.Readback.Resize;
        return control;
    }

    public OutlineNode? Outline() => _session.Outline();

    public bool IsEntity3D(nint entity) => _session.IsEntity3D(entity);

    public Vector3 PositionOf(nint entity) => _session.PositionOf(entity);

    public void MoveTo(nint entity, Vector3 position) => _session.MoveTo(entity, position);

    public nint PickAt(Vector2 framePoint) => _viewport is null ? 0 : _session.Pick(_viewport.Camera, framePoint);

    public void ShowPlanar(bool on) => _viewport?.Planar.SetOn(on);

    public nint PickPlanarAt(Vector2 framePoint) => _viewport is null ? 0 : _session.PickPlanar(_viewport.Planar, framePoint);

    public Vector2? PlanarPivotOf(nint entity) =>
        _viewport is null || !_session.IsEntity2D(entity) ? null : _viewport.Planar.PivotOf(entity);

    public void MovePlanar(nint entity, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.MovePlanar(_viewport.Planar, entity, move);
        }
    }

    public GizmoHandles? HandlesOf(nint entity, GizmoSpace space) => _viewport is null ? null : _session.HandlesOf(_viewport.Camera, entity, space);

    public void Drag(nint entity, GizmoDirection direction, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.Drag(_viewport.Camera, entity, direction, move);
        }
    }

    public GizmoRings? RingsOf(nint entity, GizmoSpace space) => _viewport is null ? null : _session.RingsOf(_viewport.Camera, entity, space);

    public void RotateDrag(nint entity, GizmoDirection direction, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.RotateDrag(_viewport.Camera, entity, direction, move);
        }
    }

    public void ScaleDrag(nint entity, GizmoAxis axis, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.ScaleDrag(_viewport.Camera, entity, axis, move);
        }
    }

    public void ScaleAllDrag(nint entity, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.ScaleAllDrag(_viewport.Camera, entity, move);
        }
    }

    public void SpinDrag(nint entity, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.SpinDrag(_viewport.Camera, entity, move);
        }
    }

    public void TrackballDrag(nint entity, FrameMove move)
    {
        if (_viewport is not null)
        {
            _session.TrackballDrag(_viewport.Camera, entity, move);
        }
    }

    public GizmoSnap? Snap
    {
        get => _session.Snap;
        set => _session.Snap = value;
    }

    public bool KeepChildren
    {
        get => _session.KeepChildren;
        set => _session.KeepChildren = value;
    }

    public void SetLocked(nint entity, bool locked) => _session.SetLocked(entity, locked);

    public void SetGrouped(nint entity, bool grouped) => _session.SetGrouped(entity, grouped);

    public void SetHidden(nint entity, bool hidden) => _session.SetHidden(entity, hidden);

    public void EndDrag() => _session.EndDrag();

    public void FrameOn(nint entity) => _viewport?.Camera.Frame(entity);

    public void Highlight(nint entity) => _viewport?.Box.Show(entity);

    public Vector3 RotationOf(nint entity) => _session.RotationOf(entity);

    public void RotateTo(nint entity, Vector3 degrees) => _session.RotateTo(entity, degrees);

    public Vector3 ScaleOf(nint entity) => _session.ScaleOf(entity);

    public void ScaleTo(nint entity, Vector3 scale) => _session.ScaleTo(entity, scale);

    public nint AddChild(nint parent, string className, string name) => _session.AddChild(parent, className, name);

    public nint Duplicate(nint entity) => _session.Duplicate(entity);

    public void AddCollision(nint entity, bool convex) => _session.AddCollision(entity, convex);

    public void SetOrthogonal(bool orthogonal) => _viewport?.Camera.SetOrthogonal(orthogonal);

    public void Rename(nint entity, string name) => _session.Rename(entity, name);

    public void MoveWithinParent(nint entity, int places) => _session.MoveWithinParent(entity, places);

    public void Reparent(nint entity, nint newParent) => _session.Reparent(entity, newParent);

    public void Remove(nint entity) => _session.Remove(entity);

    public void Save() => _session.Save();

    public IReadOnlyList<string> Scenes() => ProjectScenes.Find(_project);

    public string SceneFile { get; private set; }

    /// <summary>Read once, because the title asks for it on every edit and the name does not change while the editor runs.</summary>
    public string ProjectName { get; }

    public void Open(string resourcePath)
    {
        _session.OpenScene(resourcePath);
        if (_viewport is not null)
        {
            EditorWorld.PutSkyLast(_host);
        }

        SceneFile = resourcePath;
    }

    public void Create(string resourcePath, string className)
    {
        _session.CreateScene(resourcePath, className, Path.GetFileNameWithoutExtension(resourcePath));
        Open(resourcePath);
    }

    public void Close()
    {
        _session.CloseScene();
        SceneFile = "";
    }

    public nint Instance(nint parent, string resourcePath) => _session.Instance(parent, resourcePath);

    public IReadOnlyList<MetaEntry> MetadataOf(nint entity) => _session.MetadataOf(entity);

    public void SetMetadata(nint entity, string name, object value) => _session.SetMetadata(entity, name, value);

    public void RemoveMetadata(nint entity, string name) => _session.RemoveMetadata(entity, name);

    public string? ScriptOf(nint entity) => _session.ScriptOf(entity);

    public IReadOnlyList<string> Scripts() => ProjectScripts.Find(_project);

    public IReadOnlyList<string> GraphScripts() => ProjectScripts.FindGraphs(_project);

    public IGraphFile OpenGraph(string resourcePath) => new SessionGraphFile(GraphScriptSession.Open(_host, resourcePath));

    public string ReadScript(string resourcePath) => File.ReadAllText(ProjectScripts.FileOf(_project, resourcePath));

    public void WriteScript(string resourcePath, string text) => File.WriteAllText(ProjectScripts.FileOf(_project, resourcePath), text);

    public string? ReadBreakpoints() => BreakpointFile.Read(_project);

    public void WriteBreakpoints(string text) => BreakpointFile.Write(_project, text);

    public void AttachScript(nint entity, string resourcePath) => _session.AttachScript(entity, resourcePath);

    public void ClearScript(nint entity) => _session.ClearScript(entity);

    public ReadOnlyObservableCollection<string> Output => Log.Lines;

    public void PollOutput() => Log.Poll();

    public void ClearOutput() => Log.Clear();

    public DateTime ModifiedOf(string resourcePath) => File.GetLastWriteTimeUtc(Path.Combine(_project, resourcePath[FileTree.Root.Length..]));

    public bool IsPlaying => _game is { HasExited: false };

    public IDebugChannel Debug => _debug;

    /// <summary>Runs the project in its own process, because one process holds one engine.</summary>
    public void Play(IEnumerable<(string Source, int Step)> breakpoints)
    {
        Stop();
        var port = _debug.Listen();
        var player = new ProcessStartInfo(PlayerPath(EngineLibrary.Find()));
        foreach (var argument in GameArguments(_project, Log.NewFile(), port, breakpoints))
        {
            player.ArgumentList.Add(argument);
        }

        _game = Process.Start(player);
    }

    /// <summary>The arguments of a game that links to the editor on this port and stops on these steps, each a source and a step id.</summary>
    internal static IReadOnlyList<string> GameArguments(string project, string logFile, int port, IEnumerable<(string Source, int Step)> breakpoints)
    {
        List<string> arguments = ["--path", project, "--log-file", logFile, "--remote-debug", $"tcp://127.0.0.1:{port}"];
        var list = string.Join(',', breakpoints.Select(point => $"{point.Source}:{point.Step}"));
        if (list.Length > 0)
        {
            arguments.AddRange(["--breakpoints", list]);
        }

        return arguments;
    }

    public void Stop()
    {
        if (_game is null)
        {
            return;
        }

        try
        {
            _game.Kill();
            _game.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // The game ended on its own, and a process that has ended cannot be killed.
        }

        _game.Dispose();
        _game = null;
    }

    // The readback holds a texture the engine owns, so it goes first. A game still running is left to run.
    public void Dispose()
    {
        _game?.Dispose();
        _debug.Dispose();
        _viewport?.Readback.Dispose();
        _host.Dispose();
        Log.Dispose();
    }

    static void OpenFirstScene(EditorSession session, EditorOptions options)
    {
        if (options.HasScene)
        {
            session.OpenScene(options.Scene);
        }
    }

    // shortcut: macOS and Linux file names only. The library liborblit.<tags>.dylib sits beside the
    // program orblit.<tags>. Add the Windows names when the editor runs there.
    static string PlayerPath(string library)
    {
        var name = Path.GetFileNameWithoutExtension(library);
        return Path.Combine(Path.GetDirectoryName(library)!, name["lib".Length..]);
    }
}
