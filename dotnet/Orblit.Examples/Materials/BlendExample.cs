using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Two surfaces on one mesh, and three ways of deciding between them. The
/// same cobbles, grass and mask sit on all three panels, so the only thing
/// that differs is the mode.
/// </summary>
public sealed class BlendExample : Example
{
    /// <summary>Stones to a side of the cobbles picture.</summary>
    private const int Stones = 6;

    private static readonly IReadOnlyList<(MaterialBlendMode Mode, string Name)> Panels =
    [
        (MaterialBlendMode.Linear, "Linear"),
        (MaterialBlendMode.Masked, "Masked"),
        (MaterialBlendMode.MaskedDepth, "Masked depth"),
    ];

    private readonly string? _cobbles;
    private readonly string? _grass;
    private readonly string? _height;

    private double _amount = 0.5;
    private double _sharpness = 14;
    private bool _ownScale = true;

    /// <summary>
    /// Draws the three surfaces and keeps them in <paramref name="pictures"/>.
    /// The noise is seeded, so the cobbles are the same stones every run.
    /// </summary>
    public BlendExample(IPictureFolder pictures)
    {
        var noise = new Random(7);
        var perStone = Enumerable.Range(0, Stones * Stones).Select(_ => noise.NextDouble()).ToArray();
        var perPixel = Enumerable.Range(0, 64 * 64).Select(_ => noise.NextDouble()).ToArray();
        _cobbles = pictures.Keep("blend", "cobbles.png", Picture.Size, Picture.Drawn((x, y) => Cobble(x, y, perStone)));
        _grass = pictures.Keep("blend", "grass.png", Picture.Size, Picture.Drawn((x, y) => Grass(x, y, perPixel)));
        _height = pictures.Keep("blend", "height.png", Picture.Size, Picture.Drawn((x, y) => Height(x, y, perStone)));
    }

    public override string Name => "Blending";

    public override string Blurb => "Two surfaces on one mesh: linear, masked, and masked by height.";

    public override ExampleSection Section => ExampleSection.Materials;

    public override ViewPoint ViewPoint => new() { Distance = 12.5, Pitch = 0.62, Height = 0, Yaw = 0.06 };

    private bool Ready => _cobbles is not null && _grass is not null && _height is not null;

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Camera = camera,
        Materials = [.. Panels.Select((panel, i) => PanelMaterial(10 + i, panel.Mode))],
        Objects = [.. Panels.Select((_, i) => Panel(i))],
        Lights =
        [
            new RenderLight
            {
                Key = 900,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.4, -0.9, -0.35),
                Colour = Tint.Hex(0xFFF3E0).Linear,
                Intensity = 95000,
            },
        ],
        Sky = new Sky { Zenith = Tint.Hex(0x4E7FB4).Linear, Horizon = Tint.Hex(0xBACEE0).Linear, Ambient = 24000 },
        Pipeline = new Pipeline { Shadows = new ShadowSettings { Kind = ShadowKind.Soft, Distance = 40 } },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Caption(
            $"Left to right: {string.Join(", ", Panels.Select(panel => panel.Name))}. Same two surfaces, same mask, " +
            "three readings of it."),
        new Setting("Grass", _amount, 0, 1, value => _amount = value),
        new Setting("Sharpness", _sharpness, 1, 40, value => _sharpness = value) { Decimals = 0 },
        new Toggle("Own scale", _ownScale, value => _ownScale = value),
        new Caption(
            "At half grass the first two panels are half grass everywhere. One is flat and one follows the mask. " +
            "The third has grass in the mortar and stone on the stones. Sharpness is how abruptly it hands over. " +
            "Turn it down and it becomes the second panel."),
    ];

    public override string Code => """
        // One material, two surfaces. The mask decides between them, and what
        // the mask is taken to mean is the mode.
        new RenderMaterial
        {
            Key = 1,
            BaseColourMap = new Texture(cobbles),
            BlendBaseColourMap = new Texture(grass),
            BlendMaskMap = new Texture(height, Srgb: false),
            BlendMode = MaterialBlendMode.MaskedDepth,
            BlendAmount = 0.5,                  // how much grass there is
            BlendSharpness = 14,                // how hard the handover is
            BlendTiling = new Vector2(5, 5),    // grass at its own scale
        }
        // Linear:      the amount, everywhere, ignoring the mask.
        // Masked:      the mask scaled by the amount, a proportional fade.
        // MaskedDepth: the mask read as a height, so the low ground fills first.
        """;

    private RenderMaterial PanelMaterial(long key, MaterialBlendMode mode)
    {
        var tiling = new Vector2(2, 2);
        return new RenderMaterial
        {
            Key = key,
            BaseColour = new Vector4(1, 1, 1, 1),
            Roughness = 0.78,
            BaseColourMap = _cobbles is null ? null : new Texture(_cobbles),
            BlendMode = Ready ? mode : MaterialBlendMode.None,
            BlendAmount = _amount,
            BlendSharpness = _sharpness,
            BlendBaseColourMap = _grass is null ? null : new Texture(_grass),
            BlendMaskMap = _height is null ? null : new Texture(_height, Srgb: false),
            Tiling = tiling,

            // Grass at a different scale from the stones it grows between is
            // most of what stops two tiling textures reading as one pattern.
            BlendTiling = _ownScale ? new Vector2(5, 5) : tiling,
        };
    }

    /// <summary>The cube spans minus one to one, so a scale of two is four across, and 4.4 apart leaves a gap and not a seam.</summary>
    private static RenderObject Panel(int i)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3((i - 1) * 4.4, 0, 0));
        transform.Multiply(Matrix4.Diagonal3Values(2.0, 0.16, 2.0));
        return new RenderObject { Key = 10 + i, Material = 10 + i, Transform = transform, Colour = new Vector3(1, 1, 1) };
    }

    /// <summary>
    /// Stones laid in courses, every other course shifted half a width. That
    /// is how paving is laid, and most of why it reads as paving and not as
    /// spots. Darker into the joint, so the relief is in the colour too.
    /// </summary>
    private static (double, double, double, double) Cobble(int x, int y, double[] perStone)
    {
        var (sits, toEdge, cell) = Stone(x, y, perStone);
        var stone = Math.Clamp((sits - toEdge) / 0.26, 0.0, 1.0);
        var shade = 0.82 + perStone[cell] * 0.26;
        var lit = toEdge > sits ? 0.46 : 0.72 + stone * 0.28;
        return (168 * shade * lit, 162 * shade * lit, 152 * shade * lit, 255);
    }

    /// <summary>A green that varies per pixel, so at five times the scale it reads as blades and not as paint.</summary>
    private static (double, double, double, double) Grass(int x, int y, double[] perPixel)
    {
        var blade = perPixel[(x % 64) * 64 + (y % 64)];
        return (58 + blade * 44, 100 + blade * 60, 42 + blade * 28, 255);
    }

    /// <summary>
    /// The height the blend reads. High in the mortar and low on the crown of
    /// each stone, because grass grows in the gaps. It is the stone field
    /// inverted, and that decides which surface fills in first.
    /// </summary>
    private static (double, double, double, double) Height(int x, int y, double[] perStone)
    {
        var (sits, toEdge, _) = Stone(x, y, perStone);
        var level = (1 - Math.Clamp((sits - toEdge) / 0.26, 0.0, 1.0)) * 255;
        return (level, level, level, 255);
    }

    /// <summary>
    /// Where a pixel sits on its stone. A rounded square and not a circle,
    /// because circles on a grid leave more gap at their corners than any
    /// joint has. Each stone sits a little higher or lower than the next, so
    /// the grass line does not run dead straight along a joint.
    /// </summary>
    private static (double Sits, double ToEdge, int Cell) Stone(int x, int y, double[] perStone)
    {
        var (u, v) = (x / (double)Picture.Size, y / (double)Picture.Size);
        var row = (int)Math.Floor(v * Stones);
        var shifted = u * Stones + (row % 2 == 1 ? 0.5 : 0.0);
        var cell = (row * Stones + (int)Math.Floor(shifted)) % perStone.Length;
        var (withinX, withinY) = (shifted % 1 - 0.5, v * Stones % 1 - 0.5);
        var toEdge = Math.Pow(Math.Pow(Math.Abs(withinX * 2), 5) + Math.Pow(Math.Abs(withinY * 2), 5), 0.2);
        return (0.86 + perStone[cell] * 0.12, toEdge, cell);
    }
}
