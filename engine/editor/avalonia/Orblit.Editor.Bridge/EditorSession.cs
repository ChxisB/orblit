using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// The editor's scene services over a started engine. An Avalonia editor reads the scene here and changes
/// it. Where the scene lives, and how it opens and saves, is the <see cref="ISceneSource"/>'s business.
/// Scene calls need frames to take effect, so the ones that change the tree pump the engine.
/// </summary>
public sealed class EditorSession
{
    // Entity.DUPLICATE_DEFAULT: signals, groups, scripts and instancing.
    const long DuplicateDefault = 15;

    // Prefab.GEN_EDIT_STATE_INSTANCE: the instance keeps its scene file, so a save writes a reference to it.
    const long InstanceEditState = 1;

    // ResourceLoader.CACHE_MODE_REUSE: a scene that is already loaded is not read again.
    const long CacheReuse = 1;

    // Persistent groups, so a save keeps them.
    const string LockGroup = "_edit_lock_";
    const string GroupGroup = "_edit_group_";

    readonly EngineHost _host;
    readonly ISceneSource _source;

    // A drag adds up its pointer moves in _raw. The entity holds the total to the snap step, and _applied is what
    // it already took, so a snapped drag lands the same for one big move or many small ones.
    float _raw;
    float _applied;

    public EditorSession(EngineHost host, ISceneSource source)
    {
        _host = host;
        _source = source;
    }

    /// <summary>The steps a gizmo drag snaps to, or null for none. A drag in progress takes a change at its next move.</summary>
    public GizmoSnap? Snap { get; set; }

    /// <summary>
    /// Whether a change to an Entity3D leaves its children where they are drawn. Their own places change to
    /// cancel the move, so a child of a moved parent stays put.
    /// </summary>
    public bool KeepChildren { get; set; }

    public void OpenScene(string resourcePath) => _source.Open(resourcePath);

    /// <summary>Closes the open scene and drops its edits.</summary>
    public void CloseScene() => _source.Close();

    /// <summary>
    /// Writes a scene file holding one entity of the named class, without opening it. The path is a res://
    /// scene file. Throws <see cref="InvalidOperationException"/> when the file exists.
    /// </summary>
    public void CreateScene(string resourcePath, string className, string name)
    {
        RequireSceneFile(resourcePath);
        RequireName(name);
        if (_host.ResourceLoaderExists(_host.Singleton("ResourceLoader"), resourcePath, ""))
        {
            throw new InvalidOperationException($"{resourcePath} already exists.");
        }
        var root = _host.Construct(className);
        try
        {
            if (!_host.ObjectIsClass(root, "Entity"))
            {
                throw new ArgumentException($"{className} is not an entity class.", nameof(className));
            }
            _host.EntitySetName(root, name.Trim());
            PackedFile.Write(_host, root, resourcePath);
        }
        finally
        {
            _host.Destroy(root);
        }
    }

    /// <summary>
    /// Puts an instance of a scene file under the parent and returns its handle. The scene root owns the
    /// instance, and the entities under it belong to the instance, so a save writes a reference to the file
    /// and not a copy of its entities. The engine may change the name to keep it unique among its siblings.
    /// </summary>
    public nint Instance(nint parent, string resourcePath)
    {
        RequireSceneFile(resourcePath);
        var prefab = _host.ResourceLoaderLoad(_host.Singleton("ResourceLoader"), resourcePath, "", CacheReuse);
        if (prefab == 0)
        {
            throw new IOException($"The engine could not load {resourcePath}.");
        }
        try
        {
            var made = _host.ObjectIsClass(prefab, "Prefab") ? _host.PrefabInstantiate(prefab, InstanceEditState) : 0;
            if (made == 0)
            {
                throw new IOException($"{resourcePath} is not a scene.");
            }
            _host.EntityAddChild(parent, made, true, 0);
            _host.EntitySetOwner(made, _source.Root());
            return made;
        }
        finally
        {
            _host.Release(prefab);
        }
    }

    /// <summary>
    /// The entity's metadata, in the engine's order. Names that start with an underscore belong to the engine and are
    /// left out, and so are values that are not a string, a bool or a number.
    /// shortcut: a vector or an object value is kept but not listed, add a kind when the editor edits one.
    /// </summary>
    public IReadOnlyList<MetaEntry> MetadataOf(nint entity) => _host.ObjectGetMetaList(entity)
        .Where(name => !name.StartsWith('_'))
        .Select(name => (Name: name, Value: _host.ObjectGetMeta(entity, name, null)))
        .Where(entry => entry.Value is string or bool or double or long)
        .Select(entry => new MetaEntry(entry.Name, entry.Value!))
        .ToList();

    /// <summary>
    /// Sets one metadata value, which is a string, a bool, a double or a long, and keeps its type. The name is
    /// an identifier that does not start with an underscore.
    /// </summary>
    public void SetMetadata(nint entity, string name, object value)
    {
        RequireMetaName(name);
        if (value is not (string or bool or double or long))
        {
            throw new ArgumentException($"A {value.GetType().Name} cannot be metadata.", nameof(value));
        }
        _host.ObjectSetMeta(entity, name, value);
    }

    public void RemoveMetadata(nint entity, string name)
    {
        RequireMetaName(name);
        _host.ObjectRemoveMeta(entity, name);
    }

    /// <summary>The res:// path of the entity's script, an empty string for a script with no file, or null for none.</summary>
    public string? ScriptOf(nint entity) =>
        _host.ObjectGetScript(entity) is nint script ? _host.ResourceGetPath(script) : null;

    /// <summary>
    /// Attaches the script file to the entity in place of any other. Throws <see cref="IOException"/> when the
    /// engine cannot load the file as a script, which is the case for a C# file when the engine has no C# module.
    /// </summary>
    public void AttachScript(nint entity, string resourcePath)
    {
        RequireScriptFile(resourcePath);
        var script = _host.ResourceLoaderLoad(_host.Singleton("ResourceLoader"), resourcePath, "", CacheReuse);
        if (script == 0)
        {
            throw new IOException($"The engine could not load {resourcePath}.");
        }
        try
        {
            _host.ObjectSetScript(entity, script);
        }
        finally
        {
            _host.Release(script);
        }
    }

    public void ClearScript(nint entity) => _host.ObjectSetScript(entity, null);

    /// <summary>Ends a gizmo drag. The next one starts with nothing carried over.</summary>
    public void EndDrag()
    {
        _raw = 0f;
        _applied = 0f;
    }

    /// <summary>The root of the open scene, or null when none is open.</summary>
    public OutlineNode? Outline()
    {
        var root = _source.Root();
        return root == 0 ? null : Describe(root);
    }

    public bool IsEntity3D(nint entity) => _host.ObjectIsClass(entity, "Entity3D");

    public bool IsEntity2D(nint entity) => _host.ObjectIsClass(entity, "Entity2D");

    public Vector3 PositionOf(nint entity) => _host.Entity3DGetPosition(entity);

    public void MoveTo(nint entity, Vector3 position) => Place(entity, () => _host.Entity3DSetPosition(entity, position));

    /// <summary>
    /// The Entity3D of the open scene whose pivot lands nearest a point in the frame, or 0 for a miss. A locked
    /// entity is never picked. An entity under a grouped one gives the topmost grouped entity above it, and 0 when
    /// that one is locked.
    /// </summary>
    public nint Pick(EditorCamera camera, Vector2 framePoint) => Choose(camera.Pick, IsEntity3D, framePoint);

    /// <summary>
    /// The Entity2D of the open scene whose pivot lands nearest a point in the frame, or 0 for a miss. Locks and
    /// groups act as they do for <see cref="Pick"/>.
    /// </summary>
    public nint PickPlanar(PlanarCamera camera, Vector2 framePoint) => Choose(camera.Pick, IsEntity2D, framePoint);

    // The pick, with a locked entity left out of it and a group's topmost entity standing for what is under it.
    nint Choose(Func<IEnumerable<nint>, Vector2, nint> pick, Func<nint, bool> isPlaced, Vector2 framePoint)
    {
        var root = Outline();
        if (root is null)
        {
            return 0;
        }
        var picked = pick(Flatten(root).Where(node => !node.Locked).Select(node => node.Handle).Where(isPlaced), framePoint);
        var group = root.PathTo(picked).FirstOrDefault(node => node.Grouped);
        return group is { Locked: true } ? 0 : group?.Handle ?? picked;
    }

    /// <summary>Locks an entity against picking in the viewport, or frees it. A save keeps the lock.</summary>
    public void SetLocked(nint entity, bool locked)
    {
        RequirePlace(entity, "lock");
        SetMember(entity, LockGroup, locked);
    }

    /// <summary>Groups an entity with everything under it, so a pick of a child gives it. A save keeps the group.</summary>
    public void SetGrouped(nint entity, bool grouped)
    {
        RequirePlace(entity, "group");
        SetMember(entity, GroupGroup, grouped);
    }

    /// <summary>Hides an entity in the viewport, or shows it. A save keeps it. Only a planar or spatial entity can hide.</summary>
    public void SetHidden(nint entity, bool hidden)
    {
        RequirePlace(entity, "hide");
        if (IsEntity3D(entity))
        {
            _host.Entity3DSetVisible(entity, !hidden);
        }
        else
        {
            _host.CanvasItemSetVisible(entity, !hidden);
        }
    }

    /// <summary>The move gizmo's handles for an Entity3D, or null when its pivot is behind the camera.</summary>
    public GizmoHandles? HandlesOf(EditorCamera camera, nint entity, GizmoSpace space) =>
        camera.HandlesAt(_host.Entity3DGetGlobalTransform(entity), space);

    /// <summary>Moves an Entity3D along an axis by a pointer move in the frame. Its rotation and scale stay.</summary>
    public void Drag(EditorCamera camera, nint entity, GizmoDirection direction, FrameMove move)
    {
        var place = _host.Entity3DGetGlobalTransform(entity);
        var axis = GizmoMath.Axis(direction, place);
        var by = Carry(Vector3.Dot(camera.Drag(place, direction, move), axis), Snap?.Move ?? 0f);
        SetGlobal(entity, place with { Origin = place.Origin + axis * by });
    }

    /// <summary>
    /// Moves an Entity2D by a pointer move in the frame, so its pivot ends under the pointer. Its rotation and
    /// scale stay. shortcut: no snap in 2D and no Keep child transforms, add both when the 2D tools grow.
    /// </summary>
    public void MovePlanar(PlanarCamera camera, nint entity, FrameMove move) =>
        _host.Entity2DSetGlobalPosition(entity, _host.Entity2DGetGlobalPosition(entity) + camera.Offset(move));

    /// <summary>The rotate gizmo's rings for an Entity3D, or null when its pivot is behind the camera.</summary>
    public GizmoRings? RingsOf(EditorCamera camera, nint entity, GizmoSpace space) =>
        camera.RingsAt(_host.Entity3DGetGlobalTransform(entity), space);

    /// <summary>Turns an Entity3D about an axis through its pivot by a pointer move in the frame. Its place and scale stay.</summary>
    public void RotateDrag(EditorCamera camera, nint entity, GizmoDirection direction, FrameMove move) =>
        RotateBy(entity, direction, Carry(camera.Turn(_host.Entity3DGetGlobalTransform(entity), direction, move), Snap?.TurnRadians ?? 0f));

    /// <summary>Turns an Entity3D about an axis through its pivot by an angle in radians. Its place and scale stay.</summary>
    public void RotateBy(nint entity, GizmoDirection direction, float radians)
    {
        var place = _host.Entity3DGetGlobalTransform(entity);
        TurnAbout(entity, place, GizmoMath.Axis(direction, place), radians);
    }

    /// <summary>Turns an Entity3D about the direction the camera looks along, by a pointer move round its pivot. Its place and scale stay.</summary>
    public void SpinDrag(EditorCamera camera, nint entity, FrameMove move)
    {
        var place = _host.Entity3DGetGlobalTransform(entity);
        TurnAbout(entity, place, camera.ViewAxis, Carry(camera.Spin(place, move), Snap?.TurnRadians ?? 0f));
    }

    /// <summary>Turns an Entity3D as if it were a ball under the pointer. Snap does not apply, because the axis changes with each move.</summary>
    public void TrackballDrag(EditorCamera camera, nint entity, FrameMove move) =>
        SetGlobal(entity, GizmoMath.Turned(_host.Entity3DGetGlobalTransform(entity), camera.Roll(move)));

    /// <summary>
    /// Stretches an Entity3D along one of its own axes by a pointer move in the frame, and leaves its place and its
    /// other two scale values alone. A pivot behind the camera changes nothing. An axis scaled to nothing grows by a
    /// step, so it can be stretched again.
    /// </summary>
    public void ScaleDrag(EditorCamera camera, nint entity, GizmoAxis axis, FrameMove move) =>
        ScaleTo(entity, GizmoMath.Scaled(ScaleOf(entity), axis, CarryFactor(camera.ScaleFactor(_host.Entity3DGetGlobalTransform(entity), axis, move))));

    /// <summary>Stretches an Entity3D along all three of its own axes by a pointer move in the frame. A pivot behind the camera changes nothing.</summary>
    public void ScaleAllDrag(EditorCamera camera, nint entity, FrameMove move) =>
        ScaleTo(entity, GizmoMath.ScaledAll(ScaleOf(entity), CarryFactor(camera.UniformFactor(_host.Entity3DGetGlobalTransform(entity), move))));

    /// <summary>The rotation in degrees, as Euler angles.</summary>
    public Vector3 RotationOf(nint entity) => _host.Entity3DGetRotationDegrees(entity);

    public void RotateTo(nint entity, Vector3 degrees) => Place(entity, () => _host.Entity3DSetRotationDegrees(entity, degrees));

    public Vector3 ScaleOf(nint entity) => _host.Entity3DGetScale(entity);

    public void ScaleTo(nint entity, Vector3 scale) => Place(entity, () => _host.Entity3DSetScale(entity, scale));

    /// <summary>
    /// Makes an entity of the named class under the parent and returns its handle. The scene root owns it,
    /// so a save keeps it. The engine may change the name to keep it unique among its siblings.
    /// </summary>
    public nint AddChild(nint parent, string className, string name)
    {
        RequireName(name);
        var made = _host.Construct(className);
        if (!_host.ObjectIsClass(made, "Entity"))
        {
            _host.Destroy(made);
            throw new ArgumentException($"{className} is not an entity class.", nameof(className));
        }
        _host.EntitySetName(made, name.Trim());
        _host.EntityAddChild(parent, made, true, 0);
        _host.EntitySetOwner(made, _source.Root());
        return made;
    }

    /// <summary>
    /// Copies an entity and everything under it, and puts the copy right after the original under the same
    /// parent. The engine copies no owner, and a save drops an entity that has none, so every entity of the copy
    /// gets the scene root. The engine gives the copy a name that is unique among its siblings.
    /// </summary>
    public nint Duplicate(nint entity)
    {
        RequireNotRoot(entity, "duplicated");
        var root = _source.Root();
        var parent = _host.EntityGetParent(entity);
        var copy = _host.EntityDuplicate(entity, DuplicateDefault);
        _host.EntityAddChild(parent, copy, true, 0);
        _host.EntityMoveChild(parent, copy, _host.EntityGetIndex(entity, false) + 1);
        foreach (var node in Flatten(Describe(copy)))
        {
            _host.EntitySetOwner(node.Handle, root);
        }
        return copy;
    }

    /// <summary>
    /// Makes a static body with a collision shape from a mesh instance's mesh, as the last child of the instance. The
    /// scene root owns the new entities, so a save keeps them. The engine makes nothing for an instance with no mesh,
    /// and that is an error here.
    /// </summary>
    public void AddCollision(nint entity, bool convex)
    {
        if (!_host.ObjectIsClass(entity, "MeshInstance3D"))
        {
            throw new InvalidOperationException($"{_host.EntityGetName(entity)} is not a mesh instance.");
        }
        var before = (int)_host.EntityGetChildCount(entity, false);
        if (convex)
        {
            _host.MeshInstance3DCreateConvexCollision(entity, true, false);
        }
        else
        {
            _host.MeshInstance3DCreateTrimeshCollision(entity);
        }
        if (_host.EntityGetChildCount(entity, false) == before)
        {
            throw new InvalidOperationException($"{_host.EntityGetName(entity)} has no mesh to make a shape from.");
        }
        var root = _source.Root();
        foreach (var node in Flatten(Describe(_host.EntityGetChild(entity, before, false))))
        {
            _host.EntitySetOwner(node.Handle, root);
        }
    }

    /// <summary>
    /// Moves an entity up (negative) or down (positive) among its siblings. The index stops at the first and
    /// the last place, so a move past either end leaves it there.
    /// </summary>
    public void MoveWithinParent(nint entity, int places)
    {
        RequireNotRoot(entity, "moved");
        var parent = _host.EntityGetParent(entity);
        var last = _host.EntityGetChildCount(parent, false) - 1;
        var to = Math.Clamp(_host.EntityGetIndex(entity, false) + places, 0, last);
        _host.EntityMoveChild(parent, entity, to);
    }

    /// <summary>
    /// Moves an entity and everything under it to the end of another parent, keeping where it is drawn. The
    /// engine keeps the owner, so a save still holds it. A parent that already has a child of that name is
    /// refused, because the engine would give the mover an unreadable name.
    /// </summary>
    public void Reparent(nint entity, nint newParent)
    {
        RequireNotRoot(entity, "moved");
        var moving = Describe(entity);
        if (Flatten(moving).Any(node => node.Handle == newParent))
        {
            throw new InvalidOperationException("An entity cannot move under itself or its own children.");
        }
        if (_host.EntityGetParent(entity) == newParent)
        {
            throw new InvalidOperationException($"{moving.Name} is already under {_host.EntityGetName(newParent)}.");
        }
        if (Describe(newParent).Children.Any(child => child.Name == moving.Name))
        {
            throw new InvalidOperationException($"{_host.EntityGetName(newParent)} already has a child named {moving.Name}.");
        }
        _host.EntityReparent(entity, newParent, true);
    }

    public void Rename(nint entity, string name)
    {
        RequireName(name);
        _host.EntitySetName(entity, name.Trim());
    }

    /// <summary>Takes the entity and everything under it out of the open scene. The scene root stays.</summary>
    public void Remove(nint entity)
    {
        RequireNotRoot(entity, "removed");
        _host.EntityRemoveChild(_host.EntityGetParent(entity), entity);
        _host.EntityQueueDestroy(entity);
        _host.Pump(1);
    }

    /// <summary>Saves the open scene. Throws with the engine's error code when it fails.</summary>
    public void Save() => _source.Save();

    void TurnAbout(nint entity, Transform3D place, Vector3 axis, float radians) =>
        SetGlobal(entity, GizmoMath.Turned(place, Quaternion.CreateFromAxisAngle(axis, radians)));

    void SetGlobal(nint entity, Transform3D place) => Place(entity, () => _host.Entity3DSetGlobalTransform(entity, place));

    // Runs a change to an entity's place. With KeepChildren on, each Entity3D child gets back the place it was
    // drawn at before the change.
    void Place(nint entity, Action change)
    {
        if (!KeepChildren)
        {
            change();
            return;
        }
        var kept = ChildrenOf(entity).Where(IsEntity3D).Select(child => (child, place: _host.Entity3DGetGlobalTransform(child))).ToList();
        change();
        foreach (var (child, place) in kept)
        {
            _host.Entity3DSetGlobalTransform(child, place);
        }
    }

    // The outline reads flags for placed entities only, so a flag on any other would be written and never shown.
    void RequirePlace(nint entity, string action)
    {
        if (!OutlineNode.IsPlaced(_host.ObjectGetClass(entity)))
        {
            throw new InvalidOperationException($"{_host.EntityGetName(entity)} has no place to {action}.");
        }
    }

    void SetMember(nint entity, string group, bool member)
    {
        if (member)
        {
            _host.EntityAddToGroup(entity, group, true);
        }
        else
        {
            _host.EntityRemoveFromGroup(entity, group);
        }
    }

    IEnumerable<nint> ChildrenOf(nint entity) =>
        Enumerable.Range(0, (int)_host.EntityGetChildCount(entity, false)).Select(i => _host.EntityGetChild(entity, i, false));

    // Only a placed entity reads its flags, so the other entities of a scene cost no extra calls.
    OutlineFlags FlagsOf(nint entity, string className)
    {
        if (!OutlineNode.IsPlaced(className))
        {
            return OutlineFlags.None;
        }
        var flags = OutlineFlags.None;
        if (IsHidden(entity))
        {
            flags |= OutlineFlags.Hidden;
        }
        if (_host.EntityIsInGroup(entity, LockGroup))
        {
            flags |= OutlineFlags.Locked;
        }
        if (_host.EntityIsInGroup(entity, GroupGroup))
        {
            flags |= OutlineFlags.Grouped;
        }
        return flags;
    }

    bool IsHidden(nint entity) =>
        IsEntity3D(entity) ? !_host.Entity3DIsVisible(entity)
        : _host.ObjectIsClass(entity, "CanvasItem") && !_host.CanvasItemIsVisible(entity);

    // F5: the add and the read are one step, so a caller cannot take a move twice.
    float Carry(float amount, float step)
    {
        _raw += amount;
        var target = GizmoMath.Stepped(_raw, step);
        var now = target - _applied;
        _applied = target;
        return now;
    }

    // F5, as Carry. A factor carries as its change from 1, and the entity takes the ratio of the held change to
    // the one it already has. A held change of -1 collapses the axis, and the next ratio has nothing to divide by,
    // so it is 1.
    float CarryFactor(float factor)
    {
        _raw = (1f + _raw) * factor - 1f;
        var held = GizmoMath.Stepped(_raw, Snap?.Scale ?? 0f);
        var ratio = 1f + _applied == 0f ? 1f : (1f + held) / (1f + _applied);
        _applied = held;
        return ratio;
    }

    void RequireNotRoot(nint entity, string verb)
    {
        if (entity == _source.Root())
        {
            throw new InvalidOperationException($"The scene root cannot be {verb}.");
        }
    }

    // A scene path is a res:// file of the scene format. The engine would save to any path it can write.
    static void RequireSceneFile(string resourcePath) => ResourcePath.RequireFile(resourcePath, ".oscn", "a scene file");

    // The scene file writes the name as it is. A name with a space or an equals sign would not read back.
    static void RequireMetaName(string name)
    {
        var identifier = name.Length > 0 && !char.IsAsciiDigit(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
        if (!identifier || name[0] == '_')
        {
            throw new ArgumentException($"{name} is not a metadata name. Use letters, digits and underscores, and start with a letter.", nameof(name));
        }
    }

    // A script file is a C# file under res://. The language is decision 184.
    static void RequireScriptFile(string resourcePath) => ResourcePath.RequireFile(resourcePath, ".cs", "a C# file");

    static void RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An entity needs a name.", nameof(name));
        }
    }

    static IEnumerable<OutlineNode> Flatten(OutlineNode node) => node.Children.SelectMany(Flatten).Prepend(node);

    OutlineNode Describe(nint entity)
    {
        var count = _host.EntityGetChildCount(entity, false);
        var children = new List<OutlineNode>((int)count);
        for (var i = 0; i < count; i++)
        {
            children.Add(Describe(_host.EntityGetChild(entity, i, false)));
        }
        var className = _host.ObjectGetClass(entity);
        return new OutlineNode(entity, _host.EntityGetName(entity), className, children, FlagsOf(entity, className));
    }
}
