using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>Rain or snow in front of the scene.</summary>
public sealed record Precipitation
{
    /// <summary>Floats the precipitation occupies in the packed array.</summary>
    public const int Stride = 12;

    public static readonly Precipitation None = new();

    /// <summary>Linear RGB.</summary>
    public Vector3 Colour { get; init; } = new(0.72, 0.78, 0.86);

    /// <summary>How much of it there is, from nothing to a downpour.</summary>
    public double Amount { get; init; }

    /// <summary>Metres a second, downwards. Rain falls at about nine, snow at under one.</summary>
    public double Fall { get; init; } = 9;

    /// <summary>What carries it sideways, in metres a second.</summary>
    public Vector2 Wind { get; init; } = Vector2.Zero();

    public double DropsPerMetre { get; init; } = 6;

    public double Stretch { get; init; } = 26;

    public double Threshold { get; init; } = 0.72;

    public bool IsVisible => Amount > 0;

    internal float[] Packed => Packing.Floats(
        Colour.X, Colour.Y, Colour.Z,
        Amount,
        Fall,
        Wind.X, Wind.Y,
        DropsPerMetre,
        Stretch,
        Threshold,
        0, 0);
}
