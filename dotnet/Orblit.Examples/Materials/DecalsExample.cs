using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Pictures thrown onto whatever is under them. A decal is a box. What it
/// overlaps it paints, before the surface is lit, so a poster takes the
/// wall's shadow and a puddle reflects the sun the dry floor does not.
/// </summary>
public sealed class DecalsExample : Example
{
    /// <summary>A floor, the wall behind it and a crate on layer one for the paint to spare.</summary>
    private static readonly IReadOnlyList<(long Key, Vector3 At, Vector3 Size, int Rgb, int Layer)> Boxes =
    [
        (1, new Vector3(0, -0.05, 0), new Vector3(12, 0.1, 12), 0xB8B2A6, 0),
        (2, new Vector3(0, 2, -3), new Vector3(12, 4, 0.2), 0xD6D0C4, 0),
        (3, new Vector3(2.4, 0.5, 0.4), new Vector3(1, 1, 1), 0x9C7A52, 1),
    ];

    private readonly string? _poster;
    private readonly string? _scorch;
    private readonly string? _puddle;
    private readonly string? _stripes;
    private readonly string? _splash;

    private bool _on = true;
    private bool _angleFade = true;
    private bool _spareTheCrate = true;
    private double _opacity = 1;

    /// <summary>Draws the five pictures and keeps them in <paramref name="pictures"/>.</summary>
    public DecalsExample(IPictureFolder pictures)
    {
        _poster = Keep(pictures, "poster.png", Poster);
        _scorch = Keep(pictures, "scorch.png", Scorch);
        _puddle = Keep(pictures, "puddle.png", Puddle);
        _stripes = Keep(pictures, "stripes.png", Stripes);
        _splash = Keep(pictures, "splash.png", Splash);
    }

    public override string Name => "Decals";

    public override string Blurb =>
        "Posters, scorches, a puddle and road paint projected onto a floor and a wall. They are lit and shadowed " +
        "with the surface under them.";

    public override ExampleSection Section => ExampleSection.Materials;

    public override ViewPoint ViewPoint => new() { Yaw = 0.35, Pitch = 0.55, Distance = 10, Height = 0.8 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [.. Boxes.Select(Box)],
        Decals = _on ? Decals() : [],
        Lights =
        [
            // Low and from the side, so the crate's shadow falls across the
            // splash and the puddle has a sun to reflect.
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Intensity = 90000,
                Direction = new Vector3(-0.55, -0.6, -0.58),
                Colour = Tint.Hex(0xFFF1DD).Linear,
            },
        ],
        Sky = new Sky { Colour = Tint.Hex(0x8FA6C0).Linear, Ambient = 18000 },
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Decals", _on, value => _on = value),
        new Toggle("Angle fade", _angleFade, value => _angleFade = value)
        {
            Note = _angleFade ? "The hazard stripe stays on the floor." : "The stripe runs down the wall it straddles.",
        },
        new Toggle("Spare the crate", _spareTheCrate, value => _spareTheCrate = value)
        {
            Note = _spareTheCrate
                ? "The paint names layer nought; the crate is on layer one."
                : "The paint reaches every layer, the crate included.",
        },
        new Setting("Opacity", _opacity, 0, 1, value => _opacity = value),
    ];

    public override string Code => """
        // A poster. A box whose own y is the way it is thrown. A quarter turn
        // about x throws it along minus z, onto a wall facing the camera. The
        // box's x and z are the picture's width and height, and its y is how
        // deep it reaches.
        new Decal
        {
            Key = 101,
            Position = new Vector3(-1.6, 2.1, -2.9),
            Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 2),
            Size = new Vector3(1.6, 0.4, 2.2),
            Texture = new Texture("/path/poster.png"),
        };

        // A puddle. Mostly a colour and a roughness. Painted before the floor
        // is lit, so it reflects the sun where the dry floor round it does not.
        new Decal
        {
            Key = 103,
            Position = new Vector3(0.6, 0, 2.2),
            Size = new Vector3(2.6, 0.3, 1.5),
            Texture = new Texture("/path/puddle.png"),
            Colour = new Vector3(0.05, 0.06, 0.07),
            Roughness = 0.04,
        };

        // Paint that leaves anything on layer one alone.
        new Decal
        {
            Key = 105,
            Position = new Vector3(2.4, 0.6, 0.4),
            Size = new Vector3(2.6, 1.6, 2.6),
            Texture = new Texture("/path/splash.png"),
            Layers = new HashSet<int> { 0 },
        };

        // Past thirty-two in one scene, the rest are reported and not painted.
        """;

    private static RenderObject Box((long Key, Vector3 At, Vector3 Size, int Rgb, int Layer) box)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(box.At);
        transform.ScaleByDouble(box.Size.X / 2, box.Size.Y / 2, box.Size.Z / 2, 1);
        return new RenderObject
        {
            Key = box.Key,
            Transform = transform,
            Colour = Tint.Hex(box.Rgb).Linear,
            CastShadows = true,
            Layer = box.Layer,
        };
    }

    private IReadOnlyList<Decal> Decals()
    {
        var (start, end) = _angleFade ? (Decal.DefaultFadeStart, Decal.DefaultFadeEnd) : (Decal.NoFade, Decal.NoFade);
        return [.. Thrown().Select(decal => decal with { FadeStartAngle = start, FadeEndAngle = end })];
    }

    private IEnumerable<Decal> Thrown()
    {
        // A quarter turn about x throws a decal along minus z, onto the wall,
        // which faces the camera.
        var ontoWall = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 2);
        yield return new Decal
        {
            Key = 101,
            Position = new Vector3(-1.6, 2.1, -2.9),
            Rotation = ontoWall,
            Size = new Vector3(1.6, 0.4, 2.2),
            Texture = Texture(_poster),
            Opacity = _opacity,
        };

        // Charred wood is rough. A scorch on a polished floor kills its shine
        // as well as darkening it.
        yield return new Decal
        {
            Key = 102,
            Position = new Vector3(-1.3, 0, 1.0),
            Size = new Vector3(2.0, 0.4, 2.0),
            Texture = Texture(_scorch),
            Opacity = _opacity,
            Roughness = 0.95,
        };

        // A puddle is mostly this. Water is nearly a mirror.
        yield return new Decal
        {
            Key = 103,
            Position = new Vector3(0.6, 0, 2.2),
            Size = new Vector3(2.6, 0.3, 1.5),
            Texture = Texture(_puddle),
            Colour = new Vector3(0.05, 0.06, 0.07),
            Opacity = _opacity * 0.85,
            Roughness = 0.04,
        };

        // Straddling the corner. The box reaches down into the floor and up
        // the wall. Thrown downwards it belongs on the floor, and the wall is
        // edge on to it, which is what the angle fade is for. Sorted over any
        // floor paint under it.
        yield return new Decal
        {
            Key = 104,
            Position = new Vector3(1.2, 0.3, -2.5),
            Size = new Vector3(3.2, 1.2, 1.0),
            Texture = Texture(_stripes),
            Opacity = _opacity,
            SortOrder = 1,
        };

        // Across the crate, which is on layer one.
        yield return new Decal
        {
            Key = 105,
            Position = new Vector3(2.4, 0.6, 0.4),
            Size = new Vector3(2.6, 1.6, 2.6),
            Texture = Texture(_splash),
            Opacity = _opacity,
            Layers = _spareTheCrate ? new HashSet<int> { 0 } : null,
        };
    }

    private static Texture? Texture(string? path) => path is null ? null : new Texture(path);

    /// <summary>Keeps a picture drawn at each texel's centre, from nought to one across.</summary>
    private static string? Keep(IPictureFolder pictures, string name, Func<double, double, (double, double, double, double)> at) =>
        pictures.Keep("decals", name, Picture.Size,
            Picture.Drawn((x, y) => at((x + 0.5) / Picture.Size, (y + 0.5) / Picture.Size)));

    /// <summary>A border, a sky, a sun and some hills. Solid everywhere, so it covers the wall inside its box.</summary>
    private static (double, double, double, double) Poster(double u, double v)
    {
        if (u < 0.05 || u > 0.95 || v < 0.04 || v > 0.96)
        {
            return (240, 236, 222, 255);
        }

        if (Math.Sqrt(Math.Pow(u - 0.66, 2) + Math.Pow(v - 0.32, 2)) < 0.12)
        {
            return (250, 196, 60, 255);
        }

        return v > 0.62 + 0.08 * Math.Sin(u * 9)
            ? (46, 120, 88, 255)
            : (70 + Math.Round(v * 90, MidpointRounding.AwayFromZero), 130 + Math.Round(v * 60, MidpointRounding.AwayFromZero), 200, 255);
    }

    /// <summary>Black at the heart, soft and ragged at the edge. Almost all of it is partly transparent.</summary>
    private static (double, double, double, double) Scorch(double u, double v)
    {
        var (dx, dy) = (u - 0.5, v - 0.5);
        var r = Math.Sqrt(dx * dx + dy * dy) * 2;
        var ragged = 0.08 * Math.Sin(Math.Atan2(dy, dx) * 7);
        return (18, 14, 10, (1 - Smooth(0.35 + ragged, 0.95 + ragged, r)) * 235);
    }

    /// <summary>A white mask with a soft rim. The colour and the roughness come from the decal.</summary>
    private static (double, double, double, double) Puddle(double u, double v)
    {
        var (dx, dy) = ((u - 0.5) * 2, (v - 0.5) * 2);
        var r = Math.Sqrt(dx * dx + dy * dy) + 0.06 * Math.Sin(u * 17 + v * 5);
        return (255, 255, 255, (1 - Smooth(0.7, 0.95, r)) * 255);
    }

    /// <summary>Hazard stripes, the kind painted along a loading bay's wall.</summary>
    private static (double, double, double, double) Stripes(double u, double v) =>
        (u * 6 + v * 2) % 1 < 0.5 ? (236, 184, 30, 255) : (24, 22, 20, 255);

    /// <summary>A star of paint, solid inside and gone outside.</summary>
    private static (double, double, double, double) Splash(double u, double v)
    {
        var (dx, dy) = (u - 0.5, v - 0.5);
        var r = Math.Sqrt(dx * dx + dy * dy) * 2;
        var spikes = 0.62 + 0.25 * Math.Cos(Math.Atan2(dy, dx) * 5);
        return (214, 48, 150, (1 - Smooth(spikes - 0.04, spikes + 0.02, r)) * 255);
    }

    private static double Smooth(double from, double to, double x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0.0, 1.0);
        return t * t * (3 - 2 * t);
    }
}
