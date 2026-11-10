using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// An orbiting camera for a running scene. It sits under the stage root, beside the scene and not in it,
/// so the outline never lists it and a save never packs it.
/// </summary>
public sealed class EditorCamera
{
    /// <summary>How near a pivot must land to a click, in frame pixels, for the click to pick it.</summary>
    public const float PickRadius = 24f;

    // The engine's own defaults for a new camera.
    const float FieldOfView = 75f;
    const float Near = 0.05f;
    const float Far = 4000f;

    readonly EngineHost _host;
    readonly nint _camera;
    OrbitView _view = OrbitView.Start;

    public EditorCamera(EngineHost host)
    {
        _host = host;
        var stage = host.EngineGetMainLoop(host.Singleton("Engine"));
        _camera = host.Construct("Camera3D");
        host.EntitySetName(_camera, "EditorCamera");
        host.EntityAddChild(host.StageGetRoot(stage), _camera, false, 0);
        host.Camera3DMakeCurrent(_camera);
        Place();
    }

    public void Orbit(float yaw, float pitch)
    {
        _view = _view.Orbited(yaw, pitch);
        Place();
    }

    public void Zoom(float steps)
    {
        _view = _view.Zoomed(steps);
        Place();
        if (Orthogonal)
        {
            Lens();
        }
    }

    /// <summary>Whether the camera draws with no perspective, so parallel lines stay parallel.</summary>
    public bool Orthogonal { get; private set; }

    public void SetOrthogonal(bool orthogonal)
    {
        Orthogonal = orthogonal;
        Lens();
    }

    /// <summary>Looks at an entity's pivot from the same angles and distance.</summary>
    public void Frame(nint entity)
    {
        _view = _view.Framed(_host.Entity3DGetGlobalPosition(entity));
        Place();
    }

    /// <summary>The direction the camera looks along, as a unit vector.</summary>
    public Vector3 ViewAxis => Vector3.Normalize(_view.Target - _view.Eye);

    /// <summary>Where a world point lands in the frame, in pixels. A point behind the camera lands mirrored, so ask <see cref="IsBehind"/> too.</summary>
    public Vector2 Project(Vector3 world) => _host.Camera3DUnprojectPosition(_camera, world);

    public bool IsBehind(Vector3 world) => _host.Camera3DIsPositionBehind(_camera, world);

    /// <summary>
    /// The entity whose pivot lands nearest the frame point, within <see cref="PickRadius"/> pixels, or 0 for a
    /// miss. The first of two equally near wins. Entities behind the camera are never picked.
    /// </summary>
    public nint Pick(IEnumerable<nint> entities, Vector2 framePoint)
    {
        nint picked = 0;
        var nearest = PickRadius;
        foreach (var entity in entities)
        {
            var world = _host.Entity3DGetGlobalPosition(entity);
            if (IsBehind(world))
            {
                continue;
            }

            var distance = Vector2.Distance(Project(world), framePoint);
            if (distance < nearest)
            {
                nearest = distance;
                picked = entity;
            }
        }
        return picked;
    }

    /// <summary>The move gizmo's handles for an entity placed at <paramref name="place"/>, or null when its pivot is behind the camera.</summary>
    public GizmoHandles? HandlesAt(Transform3D place, GizmoSpace space)
    {
        var world = place.Origin;
        if (IsBehind(world))
        {
            return null;
        }

        var origin = Project(world);
        var length = ProbeLength(world);
        Vector2 Tip(GizmoAxis axis) => GizmoMath.Tip(origin, Project(world + GizmoMath.Axis(new GizmoDirection(axis, space), place) * length));
        return new GizmoHandles(origin, Tip(GizmoAxis.X), Tip(GizmoAxis.Y), Tip(GizmoAxis.Z));
    }

    /// <summary>The ray under a frame pixel, from the same projection <see cref="Project"/> uses.</summary>
    public Ray RayAt(Vector2 framePoint) =>
        new(_host.Camera3DProjectRayOrigin(_camera, framePoint), _host.Camera3DProjectRayNormal(_camera, framePoint));

    /// <summary>The rotate gizmo's rings for an entity placed at <paramref name="place"/>, or null when its pivot is behind the camera.</summary>
    public GizmoRings? RingsAt(Transform3D place, GizmoSpace space)
    {
        var world = place.Origin;
        if (IsBehind(world))
        {
            return null;
        }

        var radius = GizmoMath.RingRadius(Project, world, Axes(place, space), ProbeLength(world));
        Vector2[] Ring(GizmoAxis axis) =>
            [.. GizmoMath.Ring(world, GizmoMath.Axis(new GizmoDirection(axis, space), place), radius).Select(Project)];
        return new GizmoRings(Project(world), Ring(GizmoAxis.X), Ring(GizmoAxis.Y), Ring(GizmoAxis.Z))
        {
            Outer = [.. GizmoMath.Ring(world, ViewAxis, radius * GizmoMath.OuterShare).Select(Project)],
        };
    }

    /// <summary>The world offset a pointer move gives the pivot of an entity placed at <paramref name="place"/>. A pivot behind the camera gives zero.</summary>
    public Vector3 Drag(Transform3D place, GizmoDirection along, FrameMove move)
    {
        if (IsBehind(place.Origin))
        {
            return Vector3.Zero;
        }

        var direction = GizmoMath.Axis(along, place);
        return direction * GizmoMath.Distance(RayAt(move.From), RayAt(move.To), place.Origin, direction);
    }

    /// <summary>What a pointer move multiplies an entity's scale along one of its own axes by. A pivot behind the camera gives 1.</summary>
    public float ScaleFactor(Transform3D place, GizmoAxis axis, FrameMove move)
    {
        if (IsBehind(place.Origin))
        {
            return 1f;
        }

        var direction = GizmoMath.Axis(new GizmoDirection(axis, GizmoSpace.Local), place);
        return GizmoMath.ScaleFactor(RayAt(move.From), RayAt(move.To), place.Origin, direction, ProbeLength(place.Origin));
    }

    /// <summary>What a pointer move multiplies all three scale values of an entity placed at <paramref name="place"/> by. A pivot behind the camera gives 1.</summary>
    public float UniformFactor(Transform3D place, FrameMove move) =>
        IsBehind(place.Origin) ? 1f : GizmoMath.UniformFactor(Project(place.Origin), move);

    /// <summary>The angle in radians a pointer move sweeps round the pivot of an entity placed at <paramref name="place"/>. A pivot behind the camera gives zero.</summary>
    public float Spin(Transform3D place, FrameMove move) =>
        IsBehind(place.Origin) ? 0f : GizmoMath.Spun(Project(place.Origin), move);

    /// <summary>The turn a pointer move gives an entity held like a ball under the pointer.</summary>
    public Quaternion Roll(FrameMove move) => GizmoMath.Rolled(ViewAxis, move);

    /// <summary>
    /// The angle in radians a pointer move turns an entity placed at <paramref name="place"/> about an axis through
    /// its pivot. A ring seen edge on follows the screen tangent. A pivot behind the camera gives zero.
    /// </summary>
    public float Turn(Transform3D place, GizmoDirection about, FrameMove move)
    {
        if (IsBehind(place.Origin))
        {
            return 0f;
        }

        var axis = GizmoMath.Axis(about, place);
        if (GizmoMath.Swept(RayAt(move.From), RayAt(move.To), place.Origin, axis) is { } swept)
        {
            return swept;
        }

        return RingsAt(place, about.Space) is { } rings ? GizmoMath.RingAngle(rings.Of(about.Axis), move.To, move.Delta) : 0f;
    }

    static Vector3[] Axes(Transform3D place, GizmoSpace space) =>
        [.. Enum.GetValues<GizmoAxis>().Select(axis => GizmoMath.Axis(new GizmoDirection(axis, space), place))];

    // A probe that is a share of the camera distance projects to a few pixels at any zoom.
    float ProbeLength(Vector3 world) => Vector3.Distance(_view.Eye, world) * GizmoMath.ProbeShare;

    // The orthogonal view shows as much height at the target as the perspective view, so a switch keeps the scene's size.
    void Lens()
    {
        if (Orthogonal)
        {
            _host.Camera3DSetOrthogonal(_camera, 2f * _view.Distance * MathF.Tan(FieldOfView * MathF.PI / 360f), Near, Far);
            return;
        }
        _host.Camera3DSetPerspective(_camera, FieldOfView, Near, Far);
    }

    void Place() => _host.Entity3DLookAtFromPosition(_camera, _view.Eye, _view.Target, Vector3.UnitY, false);
}
