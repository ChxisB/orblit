using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// Where the 2D workspace looks: the world point at the middle of the frame and how many pixels one world unit
/// takes. It is a value, so the camera can be rebuilt from it.
/// </summary>
public readonly record struct PlanarView(Vector2 Centre, float Zoom)
{
    public const float MinZoom = 0.05f;
    public const float MaxZoom = 32f;

    // Each wheel step scales the zoom by this much, as the 3D view's steps do.
    const float ZoomStep = 1.1f;

    public static PlanarView Start { get; } = new(Vector2.Zero, 1f);

    /// <summary>The view after the drawing moves by pixels with the pointer.</summary>
    public PlanarView Panned(Vector2 pixels) => this with { Centre = Centre - pixels / Zoom };

    /// <summary>The view after steps wheel steps. A positive count zooms in. The middle stays the same point.</summary>
    public PlanarView Zoomed(float steps) => this with { Zoom = Math.Clamp(Zoom * MathF.Pow(ZoomStep, steps), MinZoom, MaxZoom) };

    /// <summary>Where a world point lands in a frame of the given size, in pixels.</summary>
    public Vector2 Project(Vector2 world, Vector2 size) => (world - Centre) * Zoom + size / 2;
}
