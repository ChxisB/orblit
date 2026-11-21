using System.Collections.ObjectModel;
using System.Numerics;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The open scene with each edit noted in a history, as how to take it back and make it again. Queries, the view and
/// the files pass straight through. A removed entity, and a new one that was undone, is kept out of the scene rather
/// than freed, so the handle later steps hold stays good. The history frees it when it drops the step.
/// </summary>
internal sealed class UndoableScene(IEditorScene scene, EditHistory history) : IEditorScene
{
    Place? _dragFrom;

    public OutlineNode? Outline() => scene.Outline();

    public bool IsEntity3D(nint entity) => scene.IsEntity3D(entity);

    public Vector3 PositionOf(nint entity) => scene.PositionOf(entity);

    public Vector3 RotationOf(nint entity) => scene.RotationOf(entity);

    public Vector3 ScaleOf(nint entity) => scene.ScaleOf(entity);

    public nint PickAt(Vector2 framePoint) => scene.PickAt(framePoint);

    public void ShowPlanar(bool on) => scene.ShowPlanar(on);

    public nint PickPlanarAt(Vector2 framePoint) => scene.PickPlanarAt(framePoint);

    public Vector2? PlanarPivotOf(nint entity) => scene.PlanarPivotOf(entity);

    // shortcut: a 2D move is not undone, because the 2D view tells no end of a drag. Record it once it does.
    public void MovePlanar(nint entity, FrameMove move) => scene.MovePlanar(entity, move);

    public PlanarView PlanarView { get => scene.PlanarView; set => scene.PlanarView = value; }

    public bool IsCanvasItem(nint entity) => scene.IsCanvasItem(entity);

    public void FrameOn(nint entity) => scene.FrameOn(entity);

    public void Highlight(nint entity) => scene.Highlight(entity);

    public GizmoHandles? HandlesOf(nint entity, GizmoSpace space) => scene.HandlesOf(entity, space);

    public GizmoRings? RingsOf(nint entity, GizmoSpace space) => scene.RingsOf(entity, space);

    public GizmoSnap? Snap { get => scene.Snap; set => scene.Snap = value; }

    public bool KeepChildren { get => scene.KeepChildren; set => scene.KeepChildren = value; }

    public void SetOrthogonal(bool orthogonal) => scene.SetOrthogonal(orthogonal);

    public void Save() => scene.Save();

    public string SceneFile => scene.SceneFile;

    public string ProjectName => scene.ProjectName;

    public IReadOnlyList<string> Scenes() => scene.Scenes();

    public DateTime ModifiedOf(string resourcePath) => scene.ModifiedOf(resourcePath);

    public IReadOnlyList<string> ClassChainOf(nint entity) => scene.ClassChainOf(entity);

    public IReadOnlyList<EntityProperty> PropertiesOf(nint entity) => scene.PropertiesOf(entity);

    // shortcut: making resources unique is not undone, because the shared copies it let go of may be freed. Record it if it is missed.
    public void MakeResourcesUnique(nint entity) => scene.MakeResourcesUnique(entity);

    public IReadOnlyList<EntitySignal> SignalsOf(nint entity) => scene.SignalsOf(entity);

    public IReadOnlyList<MetaEntry> MetadataOf(nint entity) => scene.MetadataOf(entity);

    public string? ScriptOf(nint entity) => scene.ScriptOf(entity);

    public ReadOnlyObservableCollection<string> Output => scene.Output;

    public void PollOutput() => scene.PollOutput();

    public void ClearOutput() => scene.ClearOutput();

    public bool IsPlaying => scene.IsPlaying;

    public void Play(IEnumerable<(string Source, int Step)> breakpoints, IEnumerable<string> flags) => scene.Play(breakpoints, flags);

    public void Stop() => scene.Stop();

    public void Restore(DetachedEntity detached) => scene.Restore(detached);

    public void Destroy(nint entity) => scene.Destroy(entity);

    public IReadOnlyList<string> Scripts() => scene.Scripts();

    public string ReadScript(string resourcePath) => scene.ReadScript(resourcePath);

    public void WriteScript(string resourcePath, string text) => scene.WriteScript(resourcePath, text);

    public IReadOnlyList<string> GraphScripts() => scene.GraphScripts();

    public IReadOnlyList<string> ImportedFiles() => scene.ImportedFiles();

    public string ReadImport(string resourcePath) => scene.ReadImport(resourcePath);

    public void WriteImport(string resourcePath, string text) => scene.WriteImport(resourcePath, text);

    public void Reimport() => scene.Reimport();

    public IReadOnlyList<string> AddableClasses() => scene.AddableClasses();

    public IGraphFile OpenGraph(string resourcePath) => scene.OpenGraph(resourcePath);

    public void CreateGraph(string resourcePath, string baseType) => scene.CreateGraph(resourcePath, baseType);

    public IDebugChannel Debug => scene.Debug;

    public string? ReadBreakpoints() => scene.ReadBreakpoints();

    public void WriteBreakpoints(string text) => scene.WriteBreakpoints(text);

    public void Open(string resourcePath)
    {
        scene.Open(resourcePath);
        history.Clear();
    }

    public void Create(string resourcePath, string className)
    {
        scene.Create(resourcePath, className);
        history.Clear();
    }

    public void Close()
    {
        scene.Close();
        history.Clear();
    }

    public void MoveTo(nint entity, Vector3 position) => Keep(entity, scene.PositionOf, scene.MoveTo, position);

    public void RotateTo(nint entity, Vector3 degrees) => Keep(entity, scene.RotationOf, scene.RotateTo, degrees);

    public void ScaleTo(nint entity, Vector3 scale) => Keep(entity, scene.ScaleOf, scene.ScaleTo, scale);

    public void Rename(nint entity, string name) => Keep(entity, each => NodeOf(each).Name, scene.Rename, name);

    public void SetLocked(nint entity, bool locked) => Keep(entity, each => NodeOf(each).Locked, scene.SetLocked, locked);

    public void SetGrouped(nint entity, bool grouped) => Keep(entity, each => NodeOf(each).Grouped, scene.SetGrouped, grouped);

    public void SetHidden(nint entity, bool hidden) => Keep(entity, each => NodeOf(each).Hidden, scene.SetHidden, hidden);

    // shortcut: a property the bridge reads as nothing, such as a resource, has nothing to go back to, so its undo does nothing. Fine while the inspector sets plain values only.
    public void SetProperty(nint entity, string name, object value) =>
        Keep<object?>(entity, each => scene.PropertiesOf(each).FirstOrDefault(property => property.Name == name)?.Value, (each, set) =>
        {
            if (set is not null)
            {
                scene.SetProperty(each, name, set);
            }
        }, value);

    public void SetMetadata(nint entity, string name, object value) => KeepMetadata(entity, name, value);

    public void RemoveMetadata(nint entity, string name) => KeepMetadata(entity, name, null);

    public void AttachScript(nint entity, string resourcePath) => Keep(entity, scene.ScriptOf, PutScript, resourcePath);

    public void ClearScript(nint entity) => Keep(entity, scene.ScriptOf, PutScript, null);

    public void Connect(nint entity, string signal, nint target, string method)
    {
        scene.Connect(entity, signal, target, method);
        history.Add(new Edit(() => scene.Disconnect(entity, signal, target, method), () => scene.Connect(entity, signal, target, method)));
    }

    public void Disconnect(nint entity, string signal, nint target, string method)
    {
        scene.Disconnect(entity, signal, target, method);
        history.Add(new Edit(() => scene.Connect(entity, signal, target, method), () => scene.Disconnect(entity, signal, target, method)));
    }

    public nint AddChild(nint parent, string className, string name) => Made(scene.AddChild(parent, className, name));

    public nint Duplicate(nint entity) => Made(scene.Duplicate(entity));

    public nint Instance(nint parent, string resourcePath) => Made(scene.Instance(parent, resourcePath));

    public void AddCollision(nint entity, bool convex)
    {
        var before = ChildrenOf(entity);
        scene.AddCollision(entity, convex);
        foreach (var made in ChildrenOf(entity).Except(before))
        {
            Made(made);
        }
    }

    public DetachedEntity Detach(nint entity)
    {
        var kept = scene.Detach(entity);
        history.Add(new Edit(() => scene.Restore(kept), () => scene.Detach(entity), done =>
        {
            if (done)
            {
                scene.Destroy(entity);
            }
        }));
        return kept;
    }

    public void MoveWithinParent(nint entity, int places)
    {
        var from = SpotOf(entity).Index;
        scene.MoveWithinParent(entity, places);
        var moved = SpotOf(entity).Index - from;
        history.Add(new Edit(() => scene.MoveWithinParent(entity, -moved), () => scene.MoveWithinParent(entity, moved)));
    }

    // A move under another parent puts the entity last, so its undo moves it back up to its old place.
    public void Reparent(nint entity, nint newParent, bool keepGlobalTransform)
    {
        var (parent, index) = SpotOf(entity);
        scene.Reparent(entity, newParent, keepGlobalTransform);
        history.Add(new Edit(
            () =>
            {
                scene.Reparent(entity, parent, keepGlobalTransform);
                scene.MoveWithinParent(entity, index - SpotOf(entity).Index);
            },
            () => scene.Reparent(entity, newParent, keepGlobalTransform)));
    }

    public void Drag(nint entity, GizmoDirection direction, FrameMove move) => Dragging(entity, () => scene.Drag(entity, direction, move));

    public void RotateDrag(nint entity, GizmoDirection direction, FrameMove move) => Dragging(entity, () => scene.RotateDrag(entity, direction, move));

    public void ScaleDrag(nint entity, GizmoAxis axis, FrameMove move) => Dragging(entity, () => scene.ScaleDrag(entity, axis, move));

    public void ScaleAllDrag(nint entity, FrameMove move) => Dragging(entity, () => scene.ScaleAllDrag(entity, move));

    public void SpinDrag(nint entity, FrameMove move) => Dragging(entity, () => scene.SpinDrag(entity, move));

    public void TrackballDrag(nint entity, FrameMove move) => Dragging(entity, () => scene.TrackballDrag(entity, move));

    // A drag is many small moves, so its first move notes where the entity was and its end notes one edit.
    public void EndDrag()
    {
        scene.EndDrag();
        if (_dragFrom is not { } from)
        {
            return;
        }
        _dragFrom = null;
        var to = PlaceOf(from.Entity);
        if (to != from)
        {
            history.Add(new Edit(() => Put(from), () => Put(to)));
        }
    }

    void Dragging(nint entity, Action drag)
    {
        _dragFrom ??= PlaceOf(entity);
        drag();
    }

    Place PlaceOf(nint entity) => new(entity, scene.PositionOf(entity), scene.RotationOf(entity), scene.ScaleOf(entity));

    void Put(Place place)
    {
        scene.MoveTo(place.Entity, place.Position);
        scene.RotateTo(place.Entity, place.Rotation);
        scene.ScaleTo(place.Entity, place.Scale);
    }

    // The value is read back after the write, because the engine can change what it was given, such as a name made unique.
    void Keep<T>(nint entity, Func<nint, T> read, Action<nint, T> write, T value)
    {
        var before = read(entity);
        write(entity, value);
        var after = read(entity);
        history.Add(new Edit(() => write(entity, before), () => write(entity, after)));
    }

    void KeepMetadata(nint entity, string name, object? value) => Keep(entity, each => scene.MetadataOf(each).Where(entry => entry.Name == name).Select(entry => (object?)entry.Value).FirstOrDefault(), (each, set) =>
    {
        if (set is null)
        {
            scene.RemoveMetadata(each, name);
        }
        else
        {
            scene.SetMetadata(each, name, set);
        }
    }, value);

    void PutScript(nint entity, string? resourcePath)
    {
        if (resourcePath is null)
        {
            scene.ClearScript(entity);
        }
        else
        {
            scene.AttachScript(entity, resourcePath);
        }
    }

    // Undo takes the new entity out and keeps it, so redo puts back the same handle that later steps hold.
    nint Made(nint made)
    {
        DetachedEntity? kept = null;
        history.Add(new Edit(() => kept = scene.Detach(made), () => scene.Restore(kept!), done =>
        {
            if (!done)
            {
                scene.Destroy(made);
            }
        }));
        return made;
    }

    OutlineNode NodeOf(nint entity) => scene.Outline()!.Find(entity)!;

    List<nint> ChildrenOf(nint entity) => [.. NodeOf(entity).Children.Select(child => child.Handle)];

    (nint Parent, int Index) SpotOf(nint entity)
    {
        var path = scene.Outline()!.PathTo(entity);
        var parent = path[^2];
        return (parent.Handle, parent.Children.ToList().FindIndex(child => child.Handle == entity));
    }

    readonly record struct Place(nint Entity, Vector3 Position, Vector3 Rotation, Vector3 Scale);
}
