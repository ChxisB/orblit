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

    /// <summary>How near an object can be and still be drawn, in metres. Null is 0.1.</summary>
    public double? Near { get; init; }

    /// <summary>
    /// How far away an object can be and still be drawn, in metres. Null is 1000, or 4000 for a flat
    /// view. It must be past <see cref="Near"/>.
    /// </summary>
    public double? Far { get; init; }

    public double Aperture { get; init; } = 16;

    public double ShutterSpeed { get; init; } = 1.0 / 125;

    public double Sensitivity { get; init; } = 100;
}
