using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>The kinds of light the renderer implements. The order is the number it is sent as.</summary>
public enum RenderLightKind
{
    Directional,
    Point,
    Spot,
    Area,
}

/// <summary>One light. A value with many fields, so it is changed with <c>with</c>.</summary>
public sealed record RenderLight
{
    /// <summary>Floats a light occupies in the packed array.</summary>
    public const int Stride = 22;

    public required long Key { get; init; }

    public required RenderLightKind Kind { get; init; }

    /// <summary>Lux for a directional light, lumens for a point or a spot.</summary>
    public required double Intensity { get; init; }

    /// <summary>Linear RGB.</summary>
    public Vector3 Colour { get; init; } = new(1, 1, 1);

    public Vector3 Position { get; init; } = Vector3.Zero();

    public Vector3 Direction { get; init; } = new(0, -1, 0);

    public double FalloffRadius { get; init; } = 10;

    public double InnerConeAngle { get; init; } = 0.5;

    public double OuterConeAngle { get; init; } = 0.6;

    public double SunAngularRadius { get; init; } = 0.263;

    public double SourceRadius { get; init; } = 0.1;

    public double HaloSize { get; init; } = 10;

    public double HaloFalloff { get; init; } = 80;

    public bool CastShadows { get; init; } = true;

    public double Width { get; init; } = 1;

    public double Height { get; init; } = 1;

    public Vector3 Tangent { get; init; } = new(1, 0, 0);

    internal void Pack(float[] into, int at) => Packing.Floats(
        Colour.X, Colour.Y, Colour.Z,
        Intensity,
        Position.X, Position.Y, Position.Z,
        Direction.X, Direction.Y, Direction.Z,
        FalloffRadius,
        InnerConeAngle,
        OuterConeAngle,
        SunAngularRadius,
        SourceRadius,
        HaloSize,
        HaloFalloff,
        Width,
        Height,
        Tangent.X, Tangent.Y, Tangent.Z).CopyTo(into, at);
}
