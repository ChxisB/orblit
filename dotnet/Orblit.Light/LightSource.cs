using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Light;

/// <summary>What kind of source a light is.</summary>
public enum LightType
{
    /// <summary>Radiates in every direction from a point. A bulb.</summary>
    Point,

    /// <summary>Parallel rays from infinitely far away. The only light whose strength does not fall off with distance.</summary>
    Sun,

    /// <summary>A cone. A torch, a stage lamp, a car headlight.</summary>
    Spot,

    /// <summary>A shape that emits from its whole surface, which is what gives a gradual shadow edge.</summary>
    Area,
}

/// <summary>The shape an area light emits from.</summary>
public enum AreaShape
{
    Square,
    Rectangle,
    Disk,
    Ellipse,
}

/// <summary>The kinds of light a renderer implements.</summary>
public enum RendererLightKind
{
    Directional,
    Point,
    Spot,
    Area,
}

/// <summary>
/// A light, described the way an artist states one. Power in watts, sizes in
/// metres, angles in degrees. <see cref="ToRenderer"/> converts in one place,
/// so a change of renderer does not mean re-authoring every light.
/// </summary>
public sealed class LightSource(
    LightType type = LightType.Point,
    Vector3? color = null,
    double? power = null,
    double radius = 0.1,
    double sunAngle = 0.526,
    double spotSize = 45,
    double spotBlend = 0.15,
    AreaShape shape = AreaShape.Square,
    double sizeX = 1.0,
    double sizeY = 1.0,
    double spread = 180,
    double diffuse = 1,
    double specular = 1,
    double volume = 1,
    bool castShadows = true,
    double? customDistance = null)
{
    // sqrt(pi)/2: a diameter times this is the side of a square of the same area as the circle.
    private static readonly double RoundToSquare = Math.Sqrt(Math.PI) / 2;

    public LightType Type { get; set; } = type;

    /// <summary>Linear RGB, not gamma. A colour picked in sRGB has to be converted first.</summary>
    public Vector3 Color { get; set; } = color ?? new Vector3(1, 1, 1);

    /// <summary>Watts for a point, spot or area light. Watts per square metre for a sun.</summary>
    public double Power { get; set; } = power ?? DefaultPower(type);

    /// <summary>
    /// How large the source is, in metres. Not a brightness control: it changes
    /// the shadow, from a knife edge to a penumbra that widens with distance.
    /// </summary>
    public double Radius { get; set; } = radius;

    /// <summary>The sun's angular diameter in degrees. The default is the real one.</summary>
    public double SunAngle { get; set; } = sunAngle;

    /// <summary>The full cone angle of a spot, in degrees.</summary>
    public double SpotSize { get; set; } = spotSize;

    /// <summary>How much of the cone is falloff, from zero for a hard edge to one for all gradient.</summary>
    public double SpotBlend { get; set; } = spotBlend;

    public AreaShape Shape { get; set; } = shape;

    /// <summary>Metres. <see cref="SizeY"/> is ignored for a square or a disk.</summary>
    public double SizeX { get; set; } = sizeX;

    public double SizeY { get; set; } = sizeY;

    /// <summary>How wide an area light throws, in degrees. A hundred and eighty is the whole hemisphere.</summary>
    public double Spread { get; set; } = spread;

    /// <summary>
    /// Multipliers on the light's contribution to each response. Not physical,
    /// and useful for that: no real fixture will kill its own specular.
    /// </summary>
    public double Diffuse { get; set; } = diffuse;

    public double Specular { get; set; } = specular;

    public double Volume { get; set; } = volume;

    public bool CastShadows { get; set; } = castShadows;

    /// <summary>
    /// A distance past which the light is ignored, in metres. Null derives it
    /// from the power, which is usually better than a cutoff chosen by hand.
    /// </summary>
    public double? CustomDistance { get; set; } = customDistance;

    /// <summary>The area of the emitting surface in square metres.</summary>
    public double EmittingArea => Shape switch
    {
        AreaShape.Square => SizeX * SizeX,
        AreaShape.Rectangle => SizeX * SizeY,
        AreaShape.Disk => Math.PI * (SizeX / 2) * (SizeX / 2),
        AreaShape.Ellipse => Math.PI * (SizeX / 2) * (SizeY / 2),
        _ => throw new UnreachableException(),
    };

    /// <summary>Converts to the units and angles a renderer works in.</summary>
    public RendererLight ToRenderer()
    {
        var isSun = Type == LightType.Sun;
        var isArea = Type == LightType.Area;
        var intensity = isSun ? Photometry.IrradianceToLux(Power) : Photometry.WattsToLumens(Power);
        var outer = Doubles.Clamp(SpotSize, 0.0, 180.0) / 2 * Math.PI / 180;

        return new RendererLight
        {
            Kind = KindOf(Type),
            Color = Color.Clone(),
            Intensity = intensity,

            // A sun does not fall off, so its influence has no radius.
            FalloffRadius = isSun ? double.PositiveInfinity : CustomDistance ?? Photometry.InfluenceRadius(intensity),
            InnerConeAngle = outer * (1 - Doubles.Clamp(SpotBlend, 0.0, 1.0)),
            OuterConeAngle = outer,
            SunAngularRadius = SunAngle / 2,
            SourceRadius = isArea ? AreaEquivalentRadius() : Radius,
            CastShadows = CastShadows,
            Width = isArea ? RectangleWidth() : 0,
            Height = isArea ? RectangleHeight() : 0,

            // Only the round shapes lose anything: the shading integrates a
            // square and a rectangle exactly.
            Approximated = isArea && Shape is AreaShape.Disk or AreaShape.Ellipse,
        };
    }

    public LightSource Copy() => new(
        Type,
        Color.Clone(),
        Power,
        Radius,
        SunAngle,
        SpotSize,
        SpotBlend,
        Shape,
        SizeX,
        SizeY,
        Spread,
        Diffuse,
        Specular,
        Volume,
        CastShadows,
        CustomDistance);

    // What a light of this type starts at, matching what an artist saw when they added one.
    private static double DefaultPower(LightType type) => type switch
    {
        LightType.Sun => 1.0,
        LightType.Area => 100,
        _ => 1000,
    };

    private static RendererLightKind KindOf(LightType type) => type switch
    {
        LightType.Sun => RendererLightKind.Directional,
        LightType.Point => RendererLightKind.Point,
        LightType.Spot => RendererLightKind.Spot,
        LightType.Area => RendererLightKind.Area,
        _ => throw new UnreachableException(),
    };

    // A disk and an ellipse are squared off to the same area, not the same
    // bounding box: area sets the brightness, and the box around a disk is over a quarter too big.
    private double RectangleWidth() => Shape switch
    {
        AreaShape.Square or AreaShape.Rectangle => SizeX,
        AreaShape.Disk or AreaShape.Ellipse => SizeX * RoundToSquare,
        _ => throw new UnreachableException(),
    };

    private double RectangleHeight() => Shape switch
    {
        AreaShape.Square => SizeX,
        AreaShape.Rectangle => SizeY,
        AreaShape.Disk => SizeX * RoundToSquare,
        AreaShape.Ellipse => SizeY * RoundToSquare,
        _ => throw new UnreachableException(),
    };

    // The radius of the sphere with the same area, so a light still casts a penumbra of a believable width.
    private double AreaEquivalentRadius() => Math.Sqrt(EmittingArea / Math.PI);
}

/// <summary>A light in the units a renderer takes.</summary>
public sealed class RendererLight
{
    public required RendererLightKind Kind { get; init; }

    public required Vector3 Color { get; init; }

    /// <summary>Lumens for a point or spot, lux for a directional light.</summary>
    public required double Intensity { get; init; }

    /// <summary>Metres, or infinite for a directional light.</summary>
    public required double FalloffRadius { get; init; }

    /// <summary>Radians. Full brightness within the inner angle, falling to nothing at the outer one.</summary>
    public required double InnerConeAngle { get; init; }

    public required double OuterConeAngle { get; init; }

    /// <summary>Half the sun's angular diameter, in degrees.</summary>
    public required double SunAngularRadius { get; init; }

    /// <summary>Metres, for soft shadows.</summary>
    public required double SourceRadius { get; init; }

    public required bool CastShadows { get; init; }

    /// <summary>The emitting rectangle's edges in metres, for <see cref="RendererLightKind.Area"/>.</summary>
    public double Width { get; init; }

    public double Height { get; init; }

    /// <summary>
    /// True for a round emitter, which arrives as a rectangle of the same area.
    /// The total light and the softness are right. The outline reflected in
    /// something mirror-smooth is not.
    /// </summary>
    public bool Approximated { get; init; }
}
