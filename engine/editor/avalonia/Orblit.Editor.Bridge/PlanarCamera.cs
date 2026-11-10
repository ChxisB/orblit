using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// The camera of the 2D workspace: a Camera2D that pans and zooms the scene, and a flat backdrop that hides the
/// 3D world while the camera is on. Both sit under the stage root, beside the scene and not in it, so the
/// outline never lists them and a save never packs them.
/// </summary>
public sealed class PlanarCamera
{
    const long FullRect = 15;

    // The engine draws a layer below zero under the scene's own layer and over the 3D world.
    const long BackdropLayer = -1;
    static readonly Vector4 BackdropColour = new(0.11f, 0.12f, 0.14f, 1);

    readonly EngineHost _host;
    readonly nint _root;
    readonly nint _camera;
    readonly nint _backdrop;
    PlanarView _view = PlanarView.Start;

    public PlanarCamera(EngineHost host)
    {
        _host = host;
        _root = EditorWorld.StageRoot(host);
        // A Camera2D turns itself on when it enters the tree, so it is turned off again before a frame draws.
        _camera = EditorWorld.Add(host, _root, "Camera2D", "EditorPlanarCamera");
        host.Camera2DSetEnabled(_camera, false);
        _backdrop = EditorWorld.Add(host, _root, "CanvasLayer", "EditorPlanarBackdrop");
        host.CanvasLayerSetLayer(_backdrop, BackdropLayer);
        host.CanvasLayerSetVisible(_backdrop, false);
        var fill = EditorWorld.Add(host, _backdrop, "ColorRect", "Fill");
        host.ColorRectSetColor(fill, BackdropColour);
        host.ControlSetAnchorsAndOffsetsPreset(fill, FullRect, 0, 0);
        Place();
    }

    /// <summary>Whether the camera and its backdrop are on.</summary>
    public bool IsOn { get; private set; }

    public void SetOn(bool on)
    {
        IsOn = on;
        // Turning the camera on also makes it the current one, as the engine does when it is inside the stage.
        _host.Camera2DSetEnabled(_camera, on);
        _host.CanvasLayerSetVisible(_backdrop, on);
    }

    /// <summary>Moves what is drawn by a pointer move, in frame pixels.</summary>
    public void Pan(Vector2 pixels)
    {
        _view = _view.Panned(pixels);
        Place();
    }

    public void Zoom(float steps)
    {
        _view = _view.Zoomed(steps);
        Place();
    }

    /// <summary>The size of the frame in pixels.</summary>
    public Vector2 Size
    {
        get
        {
            var size = _host.WindowGetSize(_root);
            return new Vector2(size.X, size.Y);
        }
    }

    /// <summary>Where a world point lands in the frame, in pixels.</summary>
    public Vector2 Project(Vector2 world) => _view.Project(world, Size);

    /// <summary>Where the pivot of an Entity2D lands in the frame, in pixels.</summary>
    public Vector2 PivotOf(nint entity) => Project(_host.Entity2DGetGlobalPosition(entity));

    /// <summary>
    /// The entity whose pivot lands nearest the frame point, within <see cref="EditorCamera.PickRadius"/> pixels,
    /// or 0 for a miss. The first of two equally near wins.
    /// </summary>
    public nint Pick(IEnumerable<nint> entities, Vector2 framePoint)
    {
        nint picked = 0;
        var nearest = EditorCamera.PickRadius;
        foreach (var entity in entities)
        {
            var distance = Vector2.Distance(PivotOf(entity), framePoint);
            if (distance < nearest)
            {
                nearest = distance;
                picked = entity;
            }
        }
        return picked;
    }

    /// <summary>The offset in world units a pointer move gives a pivot, so the pivot ends under the pointer.</summary>
    public Vector2 Offset(FrameMove move) => move.Delta / _view.Zoom;

    void Place()
    {
        _host.Entity2DSetPosition(_camera, _view.Center);
        _host.Camera2DSetZoom(_camera, new Vector2(_view.Zoom, _view.Zoom));
    }
}
