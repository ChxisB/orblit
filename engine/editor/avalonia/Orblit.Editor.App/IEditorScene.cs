using System.Collections.ObjectModel;
using System.Numerics;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>What the editor window needs from the open scene. The window owns this interface.</summary>
internal interface IEditorScene : IScriptFiles
{
    OutlineNode? Outline();

    bool IsEntity3D(nint entity);

    Vector3 PositionOf(nint entity);

    void MoveTo(nint entity, Vector3 position);

    /// <summary>The Entity3D whose pivot lands nearest a point in the viewport's frame, or 0 for a miss.</summary>
    nint PickAt(Vector2 framePoint);

    /// <summary>Turns the 2D camera and its flat backdrop on, which hides the 3D world, or off. It does nothing when there is no viewport.</summary>
    void ShowPlanar(bool on);

    /// <summary>The Entity2D whose pivot lands nearest a point in the viewport's frame, or 0 for a miss. Nothing is picked when there is no viewport.</summary>
    nint PickPlanarAt(Vector2 framePoint);

    /// <summary>Where the pivot of an Entity2D lands in the viewport's frame. Null when there is no viewport or the entity is no Entity2D.</summary>
    Vector2? PlanarPivotOf(nint entity);

    /// <summary>Moves an Entity2D by a pointer move in the viewport's frame, so its pivot ends under the pointer. It does nothing when there is no viewport.</summary>
    void MovePlanar(nint entity, FrameMove move);

    /// <summary>Points the viewport's camera at the entity. It does nothing when there is no viewport.</summary>
    void FrameOn(nint entity);

    /// <summary>Draws the selection box around the entity. Zero draws none. It does nothing when there is no viewport.</summary>
    void Highlight(nint entity);

    /// <summary>The move gizmo's handles in the viewport's frame. Null when there is no viewport or the pivot is behind the camera.</summary>
    GizmoHandles? HandlesOf(nint entity, GizmoSpace space);

    /// <summary>Moves the entity along an axis by a pointer move in the viewport's frame. It does nothing when there is no viewport.</summary>
    void Drag(nint entity, GizmoDirection direction, FrameMove move);

    /// <summary>The rotate gizmo's rings in the viewport's frame. Null when there is no viewport or the pivot is behind the camera.</summary>
    GizmoRings? RingsOf(nint entity, GizmoSpace space);

    /// <summary>Turns the entity about an axis through its pivot by a pointer move in the viewport's frame. It does nothing when there is no viewport.</summary>
    void RotateDrag(nint entity, GizmoDirection direction, FrameMove move);

    /// <summary>Stretches the entity along one of its own axes by a pointer move in the viewport's frame. It does nothing when there is no viewport.</summary>
    void ScaleDrag(nint entity, GizmoAxis axis, FrameMove move);

    /// <summary>Stretches all three scale values by a pointer move in the viewport's frame. It does nothing when there is no viewport.</summary>
    void ScaleAllDrag(nint entity, FrameMove move);

    /// <summary>Turns the entity about the direction of the view by a pointer move in the viewport's frame. It does nothing when there is no viewport.</summary>
    void SpinDrag(nint entity, FrameMove move);

    /// <summary>Turns the entity like a ball under the pointer. It does nothing when there is no viewport.</summary>
    void TrackballDrag(nint entity, FrameMove move);

    /// <summary>The steps the drags round to, or null for free drags.</summary>
    GizmoSnap? Snap { get; set; }

    /// <summary>Whether a change to an entity's place leaves its children where they are drawn.</summary>
    bool KeepChildren { get; set; }

    /// <summary>Locks the entity against picking in the viewport, or frees it. A save keeps the lock.</summary>
    void SetLocked(nint entity, bool locked);

    /// <summary>Groups the entity with everything under it, so a pick of a child gives it. A save keeps the group.</summary>
    void SetGrouped(nint entity, bool grouped);

    /// <summary>Hides the entity in the viewport, or shows it. A save keeps it.</summary>
    void SetHidden(nint entity, bool hidden);

    /// <summary>Ends the drag in progress, so the next one counts its snapped steps from zero.</summary>
    void EndDrag();

    Vector3 RotationOf(nint entity);

    void RotateTo(nint entity, Vector3 degrees);

    Vector3 ScaleOf(nint entity);

    void ScaleTo(nint entity, Vector3 scale);

    nint AddChild(nint parent, string className, string name);

    /// <summary>Copies the entity and its children right after it and returns the copy. The scene root cannot be copied.</summary>
    nint Duplicate(nint entity);

    /// <summary>Gives a mesh instance a static body with a trimesh or a convex shape made from its mesh. It throws when the entity has no mesh.</summary>
    void AddCollision(nint entity, bool convex);

    /// <summary>Draws the viewport with no perspective, or with it. It does nothing when there is no viewport.</summary>
    void SetOrthogonal(bool orthogonal);

    /// <summary>
    /// Moves the entity up (negative) or down (positive) among its siblings, stopping at the first and the
    /// last place. The scene root cannot be moved.
    /// </summary>
    void MoveWithinParent(nint entity, int places);

    /// <summary>
    /// Moves the entity and its children to the end of another parent. The scene root, the entity itself, its
    /// own children and a parent with a child of the same name are refused.
    /// </summary>
    void Reparent(nint entity, nint newParent);

    void Rename(nint entity, string name);

    void Remove(nint entity);

    void Save();

    /// <summary>The res:// path of the open scene, or an empty string when it has no file.</summary>
    string SceneFile { get; }

    /// <summary>The project's name, or an empty string when it has none.</summary>
    string ProjectName { get; }

    /// <summary>The res:// paths of the project's scene files and, with a slash on the end, of its folders.</summary>
    IReadOnlyList<string> Scenes();

    /// <summary>Replaces the open scene with this one. Throws <see cref="IOException"/> when it cannot.</summary>
    void Open(string resourcePath);

    /// <summary>
    /// Writes a scene file of one root entity of this class, named for the file, and opens it. Throws
    /// <see cref="InvalidOperationException"/> when the file exists and <see cref="IOException"/> when it cannot save.
    /// </summary>
    void Create(string resourcePath, string className);

    /// <summary>Closes the open scene and drops its edits.</summary>
    void Close();

    /// <summary>Adds the scene file under the entity as a reference and not a copy, and returns the new entity.</summary>
    nint Instance(nint parent, string resourcePath);

    /// <summary>When the scene file was last written, in UTC.</summary>
    DateTime ModifiedOf(string resourcePath);

    /// <summary>The entity's metadata in the order the engine lists it. A name that starts with an underscore is the engine's and is left out.</summary>
    IReadOnlyList<MetaEntry> MetadataOf(nint entity);

    /// <summary>
    /// Sets one metadata value, a string, a bool, a double or a long. Throws <see cref="ArgumentException"/> for a name that
    /// is no identifier or starts with an underscore, and for any other kind of value.
    /// </summary>
    void SetMetadata(nint entity, string name, object value);

    void RemoveMetadata(nint entity, string name);

    /// <summary>The res:// path of the entity's script, or null when it has none.</summary>
    string? ScriptOf(nint entity);

    /// <summary>
    /// Attaches the C# file as the entity's script. Throws <see cref="ArgumentException"/> for a path that is no C# file under
    /// res:// and <see cref="IOException"/> when the engine cannot load it. The entity keeps what it had.
    /// </summary>
    void AttachScript(nint entity, string resourcePath);

    void ClearScript(nint entity);

    /// <summary>The log lines of the engine and of the games Play ran, oldest first. The collection raises a change for each line <see cref="PollOutput"/> adds.</summary>
    ReadOnlyObservableCollection<string> Output { get; }

    /// <summary>Reads the log lines written since the last call into <see cref="Output"/>.</summary>
    void PollOutput();

    /// <summary>Empties <see cref="Output"/>.</summary>
    void ClearOutput();

    /// <summary>Whether the game <see cref="Play"/> started is still running.</summary>
    bool IsPlaying { get; }

    /// <summary>Runs the project in its own process. A game already running is stopped first.</summary>
    void Play();

    /// <summary>Ends the running game. Does nothing when none runs.</summary>
    void Stop();
}
