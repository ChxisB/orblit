using System.Collections.ObjectModel;
using System.Numerics;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

/// <summary>A scene held in memory, so the window runs with no engine.</summary>
internal sealed class FakeScene : IEditorScene
{
    public static readonly OutlineNode Lamp = new(1, "Lamp", "Entity3D", []);
    public static readonly OutlineNode Sound = new(2, "Sound", "Audio", []);
    public static readonly OutlineNode Field = new(3, "Field", "Entity3D", [Lamp, Sound]);

    readonly Dictionary<nint, Vector3> _positions = new() { [1] = new Vector3(1, 2, 3), [3] = Vector3.Zero };
    readonly Dictionary<nint, Vector3> _rotations = new() { [1] = new Vector3(10, 20, 30) };
    readonly Dictionary<nint, Vector3> _scales = new() { [1] = new Vector3(4, 5, 6) };

    readonly ObservableCollection<string> _output = [];
    OutlineNode _root = Field;
    nint _nextHandle = 10;
    bool _isOpen = true;

    public FakeScene()
    {
        Output = new ReadOnlyObservableCollection<string>(_output);
    }

    public int Saves { get; private set; }

    public bool SaveFails { get; init; }

    public bool OpenFails { get; init; }

    public bool StartsEmpty
    {
        init => _isOpen = !value;
    }

    public List<string> Opened { get; } = [];

    public string SceneFile { get; set; } = "";

    public string ProjectName { get; init; } = "";

    public OutlineNode? Outline() => _isOpen ? _root : null;

    /// <summary>The project's scene files and folders. A created scene joins them.</summary>
    public List<string> ScenePaths { get; } = ["res://field.oscn", "res://levels/second.oscn"];

    /// <summary>How many times the window read the project's scene list.</summary>
    public int ScenesAsked { get; private set; }

    /// <summary>When each scene file was last written. A file with no entry has the default time.</summary>
    public Dictionary<string, DateTime> Times { get; } = [];

    public IReadOnlyList<string> Scenes()
    {
        ScenesAsked++;
        return [.. ScenePaths];
    }

    public DateTime ModifiedOf(string resourcePath) => Times.GetValueOrDefault(resourcePath);

    public bool CreateFails { get; init; }

    /// <summary>Every scene the window made, in order, with its root class.</summary>
    public List<(string Path, string ClassName)> Created { get; } = [];

    public void Create(string resourcePath, string className)
    {
        if (ScenePaths.Contains(resourcePath))
        {
            throw new InvalidOperationException($"{resourcePath} already exists.");
        }
        if (CreateFails)
        {
            throw new IOException($"The engine could not save {resourcePath}. Error code 7.");
        }
        Created.Add((resourcePath, className));
        ScenePaths.Add(resourcePath);
        Open(resourcePath);
    }

    public int Closed { get; private set; }

    public void Close()
    {
        Closed++;
        _isOpen = false;
        SceneFile = "";
    }

    public bool InstanceFails { get; init; }

    /// <summary>Every scene the window instanced, in order, with the entity it went under.</summary>
    public List<(nint Parent, string Path)> Instanced { get; } = [];

    public nint Instance(nint parent, string resourcePath)
    {
        if (InstanceFails)
        {
            throw new IOException($"{resourcePath} is not a scene.");
        }
        Instanced.Add((parent, resourcePath));
        return AddChild(parent, "Entity3D", Path.GetFileNameWithoutExtension(resourcePath));
    }

    /// <summary>The metadata of each entity, in the order it was set.</summary>
    public Dictionary<nint, List<MetaEntry>> Metadata { get; } = [];

    public bool MetadataFails { get; init; }

    public IReadOnlyList<MetaEntry> MetadataOf(nint entity) => Metadata.TryGetValue(entity, out var list) ? [.. list] : [];

    public void SetMetadata(nint entity, string name, object value)
    {
        if (MetadataFails)
        {
            throw new ArgumentException($"{name} is not a metadata name.");
        }
        var list = Metadata.TryGetValue(entity, out var found) ? found : Metadata[entity] = [];
        var at = list.FindIndex(entry => entry.Name == name);
        if (at >= 0)
        {
            list[at] = new MetaEntry(name, value);
        }
        else
        {
            list.Add(new MetaEntry(name, value));
        }
    }

    public void RemoveMetadata(nint entity, string name) => Metadata.GetValueOrDefault(entity)?.RemoveAll(entry => entry.Name == name);

    /// <summary>The project's C# files.</summary>
    public List<string> ScriptPaths { get; } = ["res://spin.cs", "res://enemies/slime.cs"];

    /// <summary>The script each entity holds.</summary>
    public Dictionary<nint, string> Scripted { get; } = [];

    public bool AttachFails { get; init; }

    public string? ScriptOf(nint entity) => Scripted.GetValueOrDefault(entity);

    public IReadOnlyList<string> Scripts() => [.. ScriptPaths];

    /// <summary>The graph script each path opens.</summary>
    public Dictionary<string, FakeGraphSource> Graphs { get; } = [];

    public IReadOnlyList<string> GraphScripts() => [.. Graphs.Keys];

    public IGraphFile OpenGraph(string resourcePath) =>
        Graphs.TryGetValue(resourcePath, out var graph) ? graph : throw new IOException($"No {resourcePath}.");

    public void CreateGraph(string resourcePath, string baseType) => Graphs[resourcePath] = FakeGraphSource.Sample();

    /// <summary>The text of each script file.</summary>
    public Dictionary<string, string> ScriptText { get; } = [];

    public List<(string Path, string Text)> ScriptsWritten { get; } = [];

    public bool ScriptWriteFails { get; set; }

    /// <summary>The text of the breakpoint file, or null when the project has none.</summary>
    public string? BreakpointText { get; set; }

    public string? ReadBreakpoints() => BreakpointText;

    public void WriteBreakpoints(string text) => BreakpointText = text;

    public string ReadScript(string resourcePath) =>
        ScriptText.TryGetValue(resourcePath, out var text) ? text : throw new IOException($"No {resourcePath}.");

    public void WriteScript(string resourcePath, string text)
    {
        if (ScriptWriteFails)
        {
            throw new IOException("The disk is full.");
        }

        ScriptsWritten.Add((resourcePath, text));
        ScriptText[resourcePath] = text;
    }

    public void AttachScript(nint entity, string resourcePath)
    {
        if (AttachFails)
        {
            throw new IOException($"The engine could not load {resourcePath}.");
        }
        Scripted[entity] = resourcePath;
    }

    public void ClearScript(nint entity) => Scripted.Remove(entity);

    public ReadOnlyObservableCollection<string> Output { get; }

    /// <summary>Lines the engine wrote that a poll has not read yet.</summary>
    public Queue<string> Unread { get; } = new();

    public int Polls { get; private set; }

    public int OutputClears { get; private set; }

    public int Plays { get; private set; }

    public int Stops { get; private set; }

    /// <summary>True from a Play until a Stop. A test sets it false for a game that ended on its own.</summary>
    public bool IsPlaying { get; set; }

    /// <summary>The breakpoints the last Play was given.</summary>
    public List<(string Source, int Step)> PlayedWith { get; } = [];

    /// <summary>A failure the next Play throws, such as a debug port the engine could not open.</summary>
    public IOException? PlayError { get; set; }

    /// <summary>What the running game said. A test sets <see cref="FakeDebugChannel.Next"/>.</summary>
    public FakeDebugChannel Channel { get; } = new();

    public IDebugChannel Debug => Channel;

    public void Play(IEnumerable<(string Source, int Step)> breakpoints)
    {
        if (PlayError is not null)
        {
            throw PlayError;
        }

        Plays++;
        PlayedWith.Clear();
        PlayedWith.AddRange(breakpoints);
        IsPlaying = true;
    }

    public void Stop()
    {
        Stops++;
        IsPlaying = false;
    }

    public void PollOutput()
    {
        Polls++;
        while (Unread.TryDequeue(out var line))
        {
            _output.Add(line);
        }
    }

    public void ClearOutput()
    {
        OutputClears++;
        _output.Clear();
    }

    public void Open(string resourcePath)
    {
        if (OpenFails)
        {
            throw new IOException($"The engine could not open {resourcePath}. Error code 7.");
        }
        Opened.Add(resourcePath);
        SceneFile = resourcePath;
        _isOpen = true;
        _root = new OutlineNode(_nextHandle++, Path.GetFileNameWithoutExtension(resourcePath), "Entity3D", []);
    }

    public bool IsEntity3D(nint entity) => _positions.ContainsKey(entity);

    public Vector3 PositionOf(nint entity) => _positions[entity];

    public void MoveTo(nint entity, Vector3 position) => _positions[entity] = position;

    /// <summary>The entity a click picks. Zero is a miss.</summary>
    public nint PickResult { get; set; }

    /// <summary>The frame point of the last pick, or null before any.</summary>
    public Vector2? PickedAt { get; private set; }

    public nint PickAt(Vector2 framePoint)
    {
        PickedAt = framePoint;
        return PickResult;
    }

    /// <summary>Each time the window turned the 2D view on (true) or off (false), in order.</summary>
    public List<bool> Planar { get; } = [];

    public void ShowPlanar(bool on) => Planar.Add(on);

    /// <summary>The entity a click in the 2D view picks. Zero is a miss.</summary>
    public nint PlanarPickResult { get; set; }

    /// <summary>The frame point of the last 2D pick, or null before any.</summary>
    public Vector2? PlanarPickedAt { get; private set; }

    public nint PickPlanarAt(Vector2 framePoint)
    {
        PlanarPickedAt = framePoint;
        return PlanarPickResult;
    }

    /// <summary>Where the pivot of each Entity2D lands in the frame. An entity with no entry is no Entity2D.</summary>
    public Dictionary<nint, Vector2> Pivots { get; } = [];

    /// <summary>The entities the 2D view asked a pivot for, in order.</summary>
    public List<nint> PivotsAsked { get; } = [];

    public Vector2? PlanarPivotOf(nint entity)
    {
        PivotsAsked.Add(entity);
        return Pivots.TryGetValue(entity, out var pivot) ? pivot : null;
    }

    /// <summary>Every 2D move the window made, in order.</summary>
    public List<(nint Entity, Vector2 Pixels)> PlanarMoved { get; } = [];

    public void MovePlanar(nint entity, FrameMove move)
    {
        PlanarMoved.Add((entity, move.Delta));
        Pivots[entity] += move.Delta;
    }

    /// <summary>The handles the viewport asks for. Null is a pivot behind the camera.</summary>
    public GizmoHandles? Handles { get; set; }

    /// <summary>The entities the gizmo asked handles for, in order.</summary>
    public List<nint> HandlesAsked { get; } = [];

    /// <summary>The spaces the gizmo asked handles in, in order.</summary>
    public List<GizmoSpace> HandleSpaces { get; } = [];

    public GizmoHandles? HandlesOf(nint entity, GizmoSpace space)
    {
        HandlesAsked.Add(entity);
        HandleSpaces.Add(space);
        return Handles;
    }

    /// <summary>Every drag the gizmo made, in order.</summary>
    public List<(nint Entity, GizmoDirection Direction, Vector2 Pixels)> Dragged { get; } = [];

    // One pixel along the drag's x is one unit along the axis. The fake has no rotation, so a local axis is the global one.
    public void Drag(nint entity, GizmoDirection direction, FrameMove move)
    {
        Dragged.Add((entity, direction, move.Delta));
        SnapsSeen.Add(Snap);
        _positions[entity] += GizmoMath.Axis(direction.Axis) * move.Delta.X;
    }

    /// <summary>Every scale drag the gizmo made, in order.</summary>
    public List<(nint Entity, GizmoAxis Axis, Vector2 Pixels)> ScaleDragged { get; } = [];

    // One pixel along the drag's x is one hundredth more of the axis' scale. The fake has no camera to say which way a handle points.
    public void ScaleDrag(nint entity, GizmoAxis axis, FrameMove move)
    {
        ScaleDragged.Add((entity, axis, move.Delta));
        SnapsSeen.Add(Snap);
        _scales[entity] = ScaleOf(entity) * (Vector3.One + move.Delta.X * 0.01f * GizmoMath.Axis(axis));
    }

    /// <summary>Every uniform scale drag the gizmo made, in order.</summary>
    public List<(nint Entity, Vector2 Pixels)> ScaleAllDragged { get; } = [];

    // One pixel along the drag's x is one hundredth more of all three scale values.
    public void ScaleAllDrag(nint entity, FrameMove move)
    {
        ScaleAllDragged.Add((entity, move.Delta));
        SnapsSeen.Add(Snap);
        _scales[entity] = ScaleOf(entity) * (1f + move.Delta.X * 0.01f);
    }

    /// <summary>Every spin drag the gizmo made, in order.</summary>
    public List<(nint Entity, Vector2 Pixels)> SpinDragged { get; } = [];

    // One pixel along the drag's x is one degree about the y axis, so the inspector has a value to read.
    public void SpinDrag(nint entity, FrameMove move)
    {
        SpinDragged.Add((entity, move.Delta));
        SnapsSeen.Add(Snap);
        RotateTo(entity, RotationOf(entity) + Vector3.UnitY * move.Delta.X);
    }

    /// <summary>Every trackball drag the gizmo made, in order.</summary>
    public List<(nint Entity, Vector2 Pixels)> TrackballDragged { get; } = [];

    public void TrackballDrag(nint entity, FrameMove move)
    {
        TrackballDragged.Add((entity, move.Delta));
        SnapsSeen.Add(Snap);
        RotateTo(entity, RotationOf(entity) + Vector3.UnitX * move.Delta.Y);
    }

    public GizmoSnap? Snap { get; set; }

    /// <summary>The snap each drag saw when it ran, in order, whatever its kind.</summary>
    public List<GizmoSnap?> SnapsSeen { get; } = [];

    /// <summary>How many times a drag ended.</summary>
    public int DragsEnded { get; private set; }

    public void EndDrag() => DragsEnded++;

    public bool KeepChildren { get; set; }

    public void SetLocked(nint entity, bool locked) => SetFlag(entity, OutlineFlags.Locked, locked);

    public void SetGrouped(nint entity, bool grouped) => SetFlag(entity, OutlineFlags.Grouped, grouped);

    public void SetHidden(nint entity, bool hidden) => SetFlag(entity, OutlineFlags.Hidden, hidden);

    void SetFlag(nint entity, OutlineFlags flag, bool on) =>
        _root = Rebuild(_root, entity, node => node with { Flags = on ? node.Flags | flag : node.Flags & ~flag });

    /// <summary>The rings the viewport asks for. Null is a pivot behind the camera.</summary>
    public GizmoRings? Rings { get; set; }

    /// <summary>The entities the gizmo asked rings for, in order.</summary>
    public List<nint> RingsAsked { get; } = [];

    /// <summary>The spaces the gizmo asked rings in, in order.</summary>
    public List<GizmoSpace> RingSpaces { get; } = [];

    public GizmoRings? RingsOf(nint entity, GizmoSpace space)
    {
        RingsAsked.Add(entity);
        RingSpaces.Add(space);
        return Rings;
    }

    /// <summary>Every rotate drag the gizmo made, in order.</summary>
    public List<(nint Entity, GizmoDirection Direction, float Radians)> RotateDragged { get; } = [];

    // The turn follows the fake's rings like the flat ring drag did, and adds to the Euler angle of its axis.
    // The fake has no basis to turn, so only the global axes are exact.
    public void RotateDrag(nint entity, GizmoDirection direction, FrameMove move)
    {
        var radians = Rings is { } rings ? GizmoMath.RingAngle(rings.Of(direction.Axis), move.To, move.Delta) : 0f;
        RotateDragged.Add((entity, direction, radians));
        SnapsSeen.Add(Snap);
        RotateTo(entity, RotationOf(entity) + GizmoMath.Axis(direction.Axis) * radians * (180f / MathF.PI));
    }

    /// <summary>The entities the viewport was pointed at, in order.</summary>
    public List<nint> Framed { get; } = [];

    public void FrameOn(nint entity) => Framed.Add(entity);

    /// <summary>What the selection box was told to surround, in order. Zero is none.</summary>
    public List<nint> Highlighted { get; } = [];

    public void Highlight(nint entity) => Highlighted.Add(entity);

    public Vector3 RotationOf(nint entity) => _rotations.GetValueOrDefault(entity);

    // Like the engine, the scene answers with angles within a half turn.
    public void RotateTo(nint entity, Vector3 degrees) => _rotations[entity] = new Vector3(Wrap(degrees.X), Wrap(degrees.Y), Wrap(degrees.Z));

    public Vector3 ScaleOf(nint entity) => _scales.GetValueOrDefault(entity, Vector3.One);

    public void ScaleTo(nint entity, Vector3 scale) => _scales[entity] = scale;

    static float Wrap(float degrees) => degrees - 360 * MathF.Round(degrees / 360);

    public nint AddChild(nint parent, string className, string name)
    {
        RequireName(name);
        var added = new OutlineNode(_nextHandle++, name, className, []);
        _root = Rebuild(_root, parent, node => node with { Children = [.. node.Children, added] });
        return added.Handle;
    }

    /// <summary>The collision shapes the window asked for, in order, with whether each was convex.</summary>
    public List<(nint Entity, bool Convex)> Collisions { get; } = [];

    public void AddCollision(nint entity, bool convex)
    {
        if (_root.Find(entity)!.ClassName != "MeshInstance3D")
        {
            throw new InvalidOperationException("Not a mesh instance.");
        }
        Collisions.Add((entity, convex));
    }

    /// <summary>The projections the window asked for, in order. True is orthogonal.</summary>
    public List<bool> Projections { get; } = [];

    public void SetOrthogonal(bool orthogonal) => Projections.Add(orthogonal);

    public nint Duplicate(nint entity)
    {
        RequireNotRoot(entity, "duplicated");
        var original = _root.Find(entity)!;
        var copy = Clone(original, original.Name + "2");
        _root = Rebuild(_root, ParentOf(_root, entity)!.Handle, node => node with { Children = InsertAfter(node.Children, entity, copy) });
        return copy.Handle;
    }

    public void MoveWithinParent(nint entity, int places)
    {
        RequireNotRoot(entity, "moved");
        var parent = ParentOf(_root, entity)!;
        var at = parent.Children.ToList().FindIndex(child => child.Handle == entity);
        var to = Math.Clamp(at + places, 0, parent.Children.Count - 1);
        var made = parent.Children.ToList();
        var moved = made[at];
        made.RemoveAt(at);
        made.Insert(to, moved);
        _root = Rebuild(_root, parent.Handle, node => node with { Children = made });
    }

    public void Reparent(nint entity, nint newParent)
    {
        RequireNotRoot(entity, "moved");
        var moved = _root.Find(entity)!;
        if (moved.Find(newParent) is not null)
        {
            throw new InvalidOperationException("An entity cannot move under itself or its own children.");
        }
        var target = _root.Find(newParent)!;
        if (target.Children.Any(child => child.Name == moved.Name))
        {
            throw new InvalidOperationException($"{target.Name} already has a child named {moved.Name}.");
        }
        var without = Without(_root, entity);
        _root = Rebuild(without, newParent, node => node with { Children = [.. node.Children, moved] });
    }

    public void Rename(nint entity, string name)
    {
        RequireName(name);
        _root = Rebuild(_root, entity, node => node with { Name = name.Trim() });
    }

    public void Remove(nint entity)
    {
        RequireNotRoot(entity, "removed");
        _root = Without(_root, entity);
    }

    public void Save()
    {
        if (SaveFails)
        {
            throw new IOException("The engine could not save the scene. Error code 7.");
        }
        Saves++;
    }

    // Like the engine, a copy keeps the transform of the original and every entity under it.
    OutlineNode Clone(OutlineNode node, string name)
    {
        var handle = _nextHandle++;
        CopyValue(_positions, node.Handle, handle);
        CopyValue(_rotations, node.Handle, handle);
        CopyValue(_scales, node.Handle, handle);
        return node with { Handle = handle, Name = name, Children = [.. node.Children.Select(child => Clone(child, child.Name))] };
    }

    static void CopyValue(Dictionary<nint, Vector3> values, nint from, nint to)
    {
        if (values.TryGetValue(from, out var value))
        {
            values[to] = value;
        }
    }

    static List<OutlineNode> InsertAfter(IReadOnlyList<OutlineNode> children, nint after, OutlineNode added)
    {
        var made = children.ToList();
        made.Insert(made.FindIndex(child => child.Handle == after) + 1, added);
        return made;
    }

    static OutlineNode? ParentOf(OutlineNode node, nint handle) =>
        node.Children.Any(child => child.Handle == handle)
            ? node
            : node.Children.Select(child => ParentOf(child, handle)).FirstOrDefault(found => found is not null);

    void RequireNotRoot(nint entity, string verb)
    {
        if (entity == _root.Handle)
        {
            throw new InvalidOperationException($"The scene root cannot be {verb}.");
        }
    }

    static void RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An entity needs a name.");
        }
    }

    static OutlineNode Rebuild(OutlineNode node, nint handle, Func<OutlineNode, OutlineNode> change) =>
        node.Handle == handle ? change(node) : node with { Children = [.. node.Children.Select(child => Rebuild(child, handle, change))] };

    static OutlineNode Without(OutlineNode node, nint handle) =>
        node with { Children = [.. node.Children.Where(child => child.Handle != handle).Select(child => Without(child, handle))] };
}
