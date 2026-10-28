using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A courtyard in the sun and a hall off it, and the look changing as the
/// camera walks through the door.
/// </summary>
/// <remarks>
/// <para>
/// A scene has one fog, one sky and one exposure, which is right for as long as
/// the camera stays in one kind of place. The moment it goes indoors every one
/// of them is wrong. The flat sky ambient comes from every direction and nothing
/// blocks it, so a hall with a roof on it is lit as brightly as the courtyard
/// outside, and the air in it is the same clear air.
/// </para>
/// <para>
/// A volume is a region that says what is different about it. This one is the
/// hall's own box: darker, warmer, dustier and a little graded. A second,
/// smaller one sits at the far end. It is a cold, damp corner at a higher
/// priority, so where the two overlap its blue fog wins.
/// </para>
/// <para>
/// Nothing here is a new kind of rendering. The volumes are resolved against
/// the camera before the scene is sent, and the renderer is told a fog, a sky
/// and an exposure as it always was. That is why the change is continuous. It
/// is the same settings, moving.
/// </para>
/// </remarks>
public sealed class VolumesExample : Example
{
    // The hall is twenty metres deep behind a doorway at z = -4.
    private const double Front = -4;
    private const double Back = -24;
    private const double HalfWidth = 4;
    private const double Height = 5;
    private const int Stone = 0xB8AC98;
    private const int Pillar = 0xCFC3AE;

    /// <summary>Where the walk starts and ends. Straight down the middle of the doorway, at eye height.</summary>
    private static readonly Vector3 From = new(0, 1.7, 14);

    private static readonly Vector3 To = new(0, 1.7, -16);

    /// <summary>
    /// The two long walls, the back and the roof that makes it a hall. Then the
    /// front, with a doorway three metres wide and three and a half tall cut out
    /// of it. Then pillars down the hall, so there is depth in it to fog. Last,
    /// something red and something blue out in the sun, and a crate inside.
    /// </summary>
    private static readonly IReadOnlyList<(long Key, Vector3 At, Vector3 Size, int Rgb, double Turn)> Boxes =
    [
        (20, new(-HalfWidth - 0.25, Height / 2, (Front + Back) / 2), new(0.5, Height, Front - Back), Stone, 0),
        (21, new(HalfWidth + 0.25, Height / 2, (Front + Back) / 2), new(0.5, Height, Front - Back), Stone, 0),
        (22, new(0, Height / 2, Back - 0.25), new((HalfWidth * 2) + 1, Height, 0.5), Stone, 0),
        (23, new(0, Height + 0.25, ((Front + Back) / 2) - 0.25), new((HalfWidth * 2) + 1, 0.5, Front - Back + 0.5), Stone, 0),
        (24, new(-2.75, Height / 2, Front + 0.25), new(2.5, Height, 0.5), Stone, 0),
        (25, new(2.75, Height / 2, Front + 0.25), new(2.5, Height, 0.5), Stone, 0),
        (26, new(0, 4.25, Front + 0.25), new(3, 1.5, 0.5), Stone, 0),
        .. Enumerable.Range(0, 4).SelectMany(i => new (long, Vector3, Vector3, int, double)[]
        {
            (40 + (i * 2), new(-2.2, Height / 2, -8 - (i * 4.0)), new(0.6, Height, 0.6), Pillar, 0),
            (41 + (i * 2), new(2.2, Height / 2, -8 - (i * 4.0)), new(0.6, Height, 0.6), Pillar, 0),
        }),
        (60, new(-4.5, 0.6, 6), new(1.2, 1.2, 1.2), 0xC8533F, 0.4),
        (61, new(4.2, 1.0, 3), new(0.8, 2.0, 0.8), 0x4F7FA8, 0),
        (62, new(1.4, 0.4, -14), new(1.4, 0.8, 0.9), 0x9C7A55, -0.3),
    ];

    /// <summary>Whether the camera walks in and out on its own.</summary>
    private bool _walking = true;

    /// <summary>Where along the walk the camera is when it is not walking, from the courtyard at nought to the back of the hall at one.</summary>
    private double _along = 0.3;

    /// <summary>Whether the volumes are applied at all, so the difference they make can be seen.</summary>
    private bool _volumes = true;

    /// <summary>How far outside the hall its look reaches, in metres.</summary>
    private double _blend = 4;

    public override string Name => "Environment volumes";

    public override string Blurb =>
        "A sunny courtyard and a dim, dusty hall, and the look blending between them as the camera walks " +
        "through the door.";

    public override ExampleSection Section => ExampleSection.Atmosphere;

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var eye = From + ((To - From) * ProgressAt(seconds));
        return new RenderScene
        {
            Objects = [Box((10, new(0, -0.1, -5), new(60, 0.2, 60), 0xD8D0C0, 0)) with { CastShadows = false }, .. Boxes.Select(Box)],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    // Low enough to throw the front wall's shadow into the courtyard,
                    // and from behind the camera's shoulder so the courtyard is lit.
                    Direction = new Vector3(0.35, -0.75, -0.55).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                    Intensity = 100000,
                    CastShadows = true,
                },
                // Two warm lamps in the hall. The sun does not get in, and without
                // these the hall is lit by nothing but the ambient the volume turns
                // down.
                .. Enumerable.Range(0, 2).Select(Lamp),
            ],
            Sky = new Sky
            {
                Colour = Tint.Hex(0xA8C4E8).Linear,
                Zenith = new Vector3(0.18, 0.36, 0.72),
                Horizon = new Vector3(0.66, 0.78, 0.92),
                Ambient = 12000,
            },
            // A little haze outdoors, so the courtyard has air in it too and the
            // hall's fog is a change of air and not air appearing from nothing.
            Fog = new Fog { Density = 0.004, Colour = Tint.Hex(0xBCCCE0).Linear, HeightFalloff = 0.2 },
            // The example drives the camera itself. The point is the walk through
            // the door, and an orbit round the origin would never go through it.
            // It looks where it is going, a touch down, so the floor of the hall
            // and the doorway both stay in frame.
            Camera = new RenderCamera { Position = eye, Target = eye + new Vector3(0, -0.12, -1), FieldOfView = 60 },
            Volumes = _volumes ? HallVolumes() : [],
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Volumes", _volumes, value => _volumes = value),
        new Toggle("Walk in and out", _walking, value => _walking = value),
        new Setting("Where", _along, 0, 1, value =>
        {
            _along = value;
            _walking = false;
        }),
        new Setting("Blend distance", _blend, 0, 10, value => _blend = value) { Unit = " m" },
    ];

    public override string Code => """
        // The hall's own box, and what is different about being in it. Anything
        // left out is left as the scene has it: here, the environment, the
        // contrast and the fog's height.
        var hall = EnvironmentVolume.Box(
            key: 1,
            centre: new Vector3(0, 2.5, -14),
            halfExtents: new Vector3(4, 2.5, 10),
            overrides: new EnvironmentOverrides
            {
                FogDensity = 0.09,
                FogColour = Tint.Hex(0x8A6A4C).Linear,
                Ambient = 2000,                 // lux, blended in log space
                SkyColour = Tint.Hex(0xC89A6A).Linear,
                ExposureCompensation = -0.5,    // stops
                BloomStrength = 0.25,
                Saturation = 0.85,
                Temperature = 0.15,
            }) with
        {
            // Full strength inside, fading to nothing four metres out of the door.
            BlendDistance = 4,
        };

        // A damp corner at the back, which wins where the two overlap.
        var corner = EnvironmentVolume.Sphere(
            key: 2,
            centre: new Vector3(0, 1.5, -20),
            radius: 3,
            overrides: new EnvironmentOverrides
            {
                FogDensity = 0.14,
                FogColour = Tint.Hex(0x4A5C70).Linear,
            }) with { BlendDistance = 3, Priority = 1 };

        // On the scene. They are resolved against the camera when it is sent, so
        // nothing else has to know they are there.
        new RenderScene { Volumes = [hall, corner], /* ... */ }
        """;

    private static RenderObject Box((long Key, Vector3 At, Vector3 Size, int Rgb, double Turn) box)
    {
        // The built-in cube runs from minus one to one, so a box this size across
        // is the cube scaled by half of it. Getting this wrong closes the doorway
        // to half a metre.
        var transform = Matrix4.Identity();
        transform.SetTranslation(box.At);
        transform.RotateY(box.Turn);
        transform.ScaleByDouble(box.Size.X / 2, box.Size.Y / 2, box.Size.Z / 2, 1);
        return new RenderObject { Key = box.Key, Transform = transform, Colour = Tint.Hex(box.Rgb).Linear, CastShadows = true };
    }

    private static RenderLight Lamp(int i) => new()
    {
        Key = 2 + i,
        Kind = RenderLightKind.Point,
        Position = new Vector3(0, 3.8, -10 - (i * 8.0)),
        Colour = Tint.Hex(0xFFC88A).Linear,
        // A few thousand lux on the floor beneath, which is about what the
        // volume leaves the ambient at. So the lamps read as pools and do not
        // disappear into the wash.
        Intensity = 600000,
        FalloffRadius = 16,
        CastShadows = false,
    };

    /// <summary>
    /// Where the camera is, nought to one along the walk. There and back over
    /// twenty-four seconds, eased at each end so the camera lingers in both
    /// places and does not bounce off them.
    /// </summary>
    private double ProgressAt(double seconds)
    {
        if (!_walking)
        {
            return _along;
        }
        var phase = (seconds / 24) % 1;
        var triangle = phase < 0.5 ? phase * 2 : 2 - (phase * 2);
        return 0.5 - (0.5 * Math.Cos(triangle * Math.PI));
    }

    /// <summary>The hall's volume, and the damp corner at the back of it.</summary>
    private IReadOnlyList<EnvironmentVolume> HallVolumes() =>
    [
        EnvironmentVolume.Box(
            1,
            new Vector3(0, Height / 2, (Front + Back) / 2),
            new Vector3(HalfWidth, Height / 2, (Front - Back) / 2),
            new EnvironmentOverrides
            {
                // Dust in the air, lit warm by the lamps.
                FogDensity = 0.09,
                FogColour = Tint.Hex(0x8A6A4C).Linear,
                FogHeightFalloff = 0,
                // A fraction of the sky's light gets in, and it is the colour of
                // the stone it has bounced off.
                Ambient = 2000,
                SkyColour = Tint.Hex(0xC89A6A).Linear,
                // Half a stop down. Indoors should read as indoors, not as the
                // courtyard with the lights off and the exposure chasing it.
                ExposureCompensation = -0.5,
                BloomStrength = 0.25,
                Saturation = 0.85,
                Temperature = 0.15,
            }) with { BlendDistance = _blend },
        EnvironmentVolume.Sphere(
            2,
            new Vector3(0, 1.5, -20),
            3,
            new EnvironmentOverrides
            {
                FogDensity = 0.14,
                FogColour = Tint.Hex(0x4A5C70).Linear,
                SkyColour = Tint.Hex(0x7890B0).Linear,
                Temperature = -0.1,
            }) with { BlendDistance = 3, Priority = 1 },
    ];
}
