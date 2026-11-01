using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>Where the eye is and how it is set.</summary>
public sealed record RenderCamera
{
    public required Vector3 Position { get; init; }

    public required Vector3 Target { get; init; }

    public double FieldOfView { get; init; } = 50;

    public bool Orthographic { get; init; }

    public double ViewHeight { get; init; } = 10;

    public double Aperture { get; init; } = 16;

    public double ShutterSpeed { get; init; } = 1.0 / 125;

    public double Sensitivity { get; init; } = 100;
}
