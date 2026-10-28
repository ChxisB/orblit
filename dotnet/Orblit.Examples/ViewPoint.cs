using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Where the gallery's camera looks from: an orbit around a point above the
/// origin. The simplest camera that lets somebody look at what an example shows.
/// </summary>
public sealed record ViewPoint
{
    public double Yaw { get; init; } = 0.7;

    public double Pitch { get; init; } = 0.32;

    public double Distance { get; init; } = 11;

    public double Height { get; init; } = 0.5;

    public double FieldOfView { get; init; } = 50;

    /// <summary>The camera the renderer is given for this view.</summary>
    public RenderCamera ToRenderCamera()
    {
        var flat = Distance * Math.Cos(Pitch);
        return new RenderCamera
        {
            Position = new Vector3(
                flat * Math.Sin(Yaw),
                Height + (Distance * Math.Sin(Pitch)),
                flat * Math.Cos(Yaw)),
            Target = new Vector3(0, Height, 0),
            FieldOfView = FieldOfView,
        };
    }
}
