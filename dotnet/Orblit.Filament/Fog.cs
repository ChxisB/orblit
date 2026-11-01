using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// Air with something in it. Distance and height are one setting because they are one effect: fog
/// thick enough to see across a valley is fog that pools in the valley, and the first without the
/// second reads as a filter over the lens.
/// </summary>
public sealed record Fog
{
    /// <summary>Floats the fog occupies in the packed array.</summary>
    public const int Stride = 16;

    /// <summary>The same as no fog, spelled out.</summary>
    public static readonly Fog None = new() { Density = 0 };

    /// <summary>Linear RGB.</summary>
    public Vector3 Colour { get; init; } = new(0.5, 0.55, 0.6);

    public double Density { get; init; } = 0.05;

    /// <summary>Metres in front of the camera where fog begins.</summary>
    public double Distance { get; init; }

    /// <summary>Metres past which fog stops thickening, which keeps a sky visible through heavy weather.</summary>
    public double CutOffDistance { get; init; } = double.PositiveInfinity;

    public double MaximumOpacity { get; init; } = 1;

    public double Height { get; init; }

    public double HeightFalloff { get; init; } = 1;

    public double Structure { get; init; }

    /// <summary>Which way the air moves across the ground, in metres a second.</summary>
    public Vector2 Wind { get; init; } = new(0.4, 0.15);

    public double FeatureSize { get; init; } = 0.02;

    public double Thickness { get; init; } = 6;

    public bool IsVisible => Density > 0 && MaximumOpacity > 0;

    /// <summary>The fog as the renderer reads it. An infinite cut off is sent as a distance no scene reaches.</summary>
    internal float[] Packed => Packing.Floats(
        Colour.X, Colour.Y, Colour.Z,
        Density,
        Distance,
        double.IsFinite(CutOffDistance) ? CutOffDistance : 1e9,
        MaximumOpacity,
        Height,
        HeightFalloff,
        Structure,
        Wind.X, Wind.Y,
        FeatureSize,
        Thickness,
        0, 0);
}
