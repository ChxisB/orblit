using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>How hard the clouds are marched. Fewer steps cost less and look softer.</summary>
public sealed class SkyQuality
{
    public static readonly SkyQuality Lean = new(8, 2, 2);

    public static readonly SkyQuality Fair = new(12, 3, 0.6);

    public static readonly SkyQuality Full = new(18, 3, 0.35);

    private SkyQuality(int marchSteps, int lightSteps, double erosion)
    {
        MarchSteps = marchSteps;
        LightSteps = lightSteps;
        Erosion = erosion;
    }

    public int MarchSteps { get; }

    public int LightSteps { get; }

    public double Erosion { get; }
}

/// <summary>Clouds in the sky. A value with many fields, so it is changed with <c>with</c>.</summary>
public sealed record Clouds
{
    /// <summary>No cloud: it covers nothing, so the sky skips drawing it.</summary>
    public static readonly Clouds None = new() { Colour = new Vector3(0.17, 0.22, 0.33), Wind = Vector2.Zero() };

    public static readonly Clouds Cumulus = new() { Colour = new Vector3(0.17, 0.22, 0.33), Cover = 0.35, Wind = new Vector2(4, 1.5) };

    public static readonly Clouds Stratocumulus = new()
    {
        Colour = new Vector3(0.15, 0.19, 0.28),
        Cover = 0.6,
        Wind = new Vector2(5, 2),
        FeatureSize = 1 / 950.0,
        Altitude = 700,
        Thickness = 320,
        Density = 0.85,
        Billow = 0.5,
        Extinction = 0.010,
    };

    public static readonly Clouds Stratus = new()
    {
        Colour = new Vector3(0.14, 0.16, 0.21),
        Cover = 0.95,
        Wind = new Vector2(3, 1),
        FeatureSize = 1 / 1700.0,
        Altitude = 480,
        Thickness = 260,
        Density = 0.75,
        Billow = 0.08,
        Extinction = 0.009,
    };

    public static readonly Clouds Cirrus = new()
    {
        Colour = new Vector3(0.26, 0.32, 0.44),
        Cover = 0.4,
        Wind = new Vector2(16, 6),
        FeatureSize = 1 / 2600.0,
        Altitude = 7000,
        Thickness = 900,
        Density = 0.22,
        Billow = 0.3,
        Extinction = 0.004,
    };

    public static readonly Clouds Cumulonimbus = new()
    {
        Colour = new Vector3(0.08, 0.09, 0.12),
        Cover = 0.85,
        Wind = new Vector2(9, 4),
        FeatureSize = 1 / 1200.0,
        Altitude = 600,
        Thickness = 2600,
        Density = 1.25,
        Billow = 0.7,
        Extinction = 0.016,
    };

    /// <summary>Linear RGB: the blue the sky puts back into the side the sun does not reach. A cloud has no colour of its own.</summary>
    public required Vector3 Colour { get; init; }

    /// <summary>What carries the cloud across the sky, in metres a second.</summary>
    public required Vector2 Wind { get; init; }

    /// <summary>How much of the sky is covered, from nothing to everything.</summary>
    public double Cover { get; init; }

    public double FeatureSize { get; init; } = 1 / 700.0;

    public double Altitude { get; init; } = 900;

    public double Thickness { get; init; } = 600;

    public double Density { get; init; } = 1;

    public double Billow { get; init; } = 0.85;

    public double Extinction { get; init; } = 0.012;

    public bool IsVisible => Cover > 0.01 && Density > 0;
}

/// <summary>The sky behind everything, and the light it gives. A value with many fields, so it is changed with <c>with</c>.</summary>
public sealed record Sky
{
    /// <summary>Floats the sky occupies in the packed array.</summary>
    public const int Stride = 34;

    /// <summary>Linear RGB: what the sky is worth as a light source, one colour averaged over the dome.</summary>
    public Vector3 Colour { get; init; } = new(0.10, 0.12, 0.16);

    public Vector3 Zenith { get; init; } = new(0.05, 0.17, 0.48);

    public Vector3 Horizon { get; init; } = new(0.60, 0.74, 0.90);

    /// <summary>How much light the sky casts, in lux.</summary>
    public double Ambient { get; init; } = 28000;

    public bool ShowBody { get; init; } = true;

    /// <summary>Whether there is a sky to draw at all. An interior has none and pays for none.</summary>
    public bool Drawn { get; init; } = true;

    public SkyQuality Quality { get; init; } = SkyQuality.Full;

    public Vector3 BodyDirection { get; init; } = new(0.35, 0.78, 0.52);

    public Vector3 BodyColour { get; init; } = new(1.00, 0.96, 0.90);

    public double BodySize { get; init; } = 0.0047;

    public double Flash { get; init; }

    public Vector3 FlashDirection { get; init; } = new(0, 0.35, 1);

    public double FlashSeed { get; init; }

    public Clouds Clouds { get; init; } = Clouds.None;

    /// <summary>The sky as the renderer reads it, <see cref="Stride"/> floats.</summary>
    public float[] Packed
    {
        get
        {
            var body = BodyDirection.Length2 > 0 ? BodyDirection.Normalized() : new Vector3(0, 1, 0);
            var strike = FlashDirection.Length2 > 0 ? FlashDirection.Normalized() : new Vector3(0, 0, 1);

            return Packing.Floats(
                Zenith.X, Zenith.Y, Zenith.Z,
                Horizon.X, Horizon.Y, Horizon.Z,
                body.X, body.Y, body.Z,
                BodyColour.X, BodyColour.Y, BodyColour.Z,
                BodySize,
                ShowBody ? 1 : 0,
                Clouds.Colour.X, Clouds.Colour.Y, Clouds.Colour.Z,
                Clouds.Cover,
                Clouds.Altitude,
                Clouds.Thickness,
                Clouds.FeatureSize,
                Clouds.Density,
                Clouds.Billow,
                Clouds.Extinction,
                Quality.MarchSteps,
                Quality.LightSteps,
                Quality.Erosion,
                Clouds.Wind.X, Clouds.Wind.Y,
                Flash,
                strike.X, strike.Y, strike.Z,
                FlashSeed);
        }
    }
}
