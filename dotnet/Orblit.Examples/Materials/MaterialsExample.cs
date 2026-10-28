using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// What things are made of. The numbers describe the material and not the
/// look. Metalness is what the surface is, roughness is how scattered its
/// reflections are, and a value that is right for copper is right for copper
/// under every light in every scene. That is the reason to spell a surface out
/// this way and not as a colour and a shininess slider.
/// </summary>
/// <remarks>
/// The grid is the proof. Metalness goes across it and roughness goes down,
/// nothing else changes, and every square in it is a real material somebody
/// could name.
/// </remarks>
public sealed class MaterialsExample : Example
{
    private const int Across = 5;
    private const int Down = 4;

    /// <summary>The five squares across the front, and the colour of each.</summary>
    private static readonly IReadOnlyList<(MaterialBlend Mode, int Rgb)> Blends =
    [
        (MaterialBlend.Opaque, 0xB9C2CC),
        (MaterialBlend.Transparent, 0x7FC7E8),
        (MaterialBlend.Fade, 0xD98FA8),
        (MaterialBlend.Masked, 0x9CD97F),
        (MaterialBlend.Add, 0xE8A24F),
    ];

    private readonly string? _albedo;
    private readonly string? _normals;
    private readonly string? _packed;
    private readonly string? _cutout;

    private bool _textured = true;
    private double _tiling = 3;
    private double _reflectance = 0.5;
    private double _normalScale = 1;
    private double _alpha = 0.45;

    /// <summary>
    /// Draws the four maps and keeps them in <paramref name="pictures"/>. A
    /// checker and a bump field say more about what a map does than a
    /// photograph would.
    /// </summary>
    public MaterialsExample(IPictureFolder pictures)
    {
        _albedo = pictures.Keep("materials", "albedo.png", Picture.Size, Picture.Drawn(Albedo));
        _normals = pictures.Keep("materials", "normals.png", Picture.Size, Picture.Drawn(Normal));
        _packed = pictures.Keep("materials", "packed.png", Picture.Size, Picture.Drawn(Packed));
        _cutout = pictures.Keep("materials", "cutout.png", Picture.Size, Picture.Drawn(Cutout));
    }

    public override string Name => "Materials";

    public override string Blurb =>
        "Metalness and roughness across a grid, five ways of blending, and textures written at startup so there " +
        "is nothing to download.";

    public override ExampleSection Section => ExampleSection.Materials;

    public override ViewPoint ViewPoint => new() { Distance = 17, Pitch = 0.30, Yaw = 0.5 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects =
        [
            Scaled(1, new Vector3(0, -1.6, 0), new Vector3(16, 0.05, 16)),
            .. Grid().Select(square => Scaled(square.Key, square.At, new Vector3(0.85, 0.6, 0.85))),
            .. Enumerable.Range(0, Blends.Count).SelectMany(i => (RenderObject[])[Backing(i), Square(i, seconds)]),
        ],
        Materials =
        [
            Floor(),
            .. Grid().Select(square => GridMaterial(square.Key, square.Column, square.Row)),
            .. Blends.Select(BlendMaterial),
        ],
        Camera = camera,
        Lights =
        [
            new RenderLight
            {
                Key = 900,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.45, -1, -0.4),
                Intensity = 82000,
            },
            new RenderLight
            {
                Key = 901,
                Kind = RenderLightKind.Point,
                Position = new Vector3(-5, 3.5, 5),
                Intensity = 14000,
                Colour = new Vector3(0.7, 0.82, 1),
                CastShadows = false,
            },
        ],
        Sky = new Sky { Colour = new Vector3(0.12, 0.15, 0.21), Ambient = 26000 },
        Post = new PostProcess { Bloom = new Bloom { Enabled = true, Strength = 0.18 } },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Textures", _textured, value => _textured = value),
        .. _textured
            ? (ExampleSetting[])
            [
                new Setting("Tiling", _tiling, 1, 12, value => _tiling = value) { Decimals = 1 },
                new Setting("Normal strength", _normalScale, 0, 3, value => _normalScale = value),
            ]
            : [],
        new Setting("Reflectance", _reflectance, 0, 1, value => _reflectance = value),
        new Setting("Alpha", _alpha, 0, 1, value => _alpha = value),
    ];

    public override string Code => """
        // A material describes what a surface is, not what it looks like.
        var brass = new RenderMaterial
        {
            Key = 7,
            BaseColour = new Vector4(0.72, 0.45, 0.20, 1),
            Metallic = 1.0,
            Roughness = 0.25,
        };

        // Maps multiply into the numbers beside them, so a texture and a slider
        // are the same control. The shader applies the tiling, so nothing
        // repeats behind your back.
        var floor = new RenderMaterial
        {
            Key = 8,
            Tiling = new Vector2(3, 3),
            BaseColourMap = new Texture("/path/albedo.png"),
            NormalMap = new Texture("/path/normals.png", Srgb: false),
            MetallicRoughnessMap = new Texture("/path/packed.png", Srgb: false),
        };

        // Blending is the one property that cannot change without recompiling
        // the shader, so it picks which compiled surface the object is drawn with.
        var glass = new RenderMaterial
        {
            Key = 9,
            Blend = MaterialBlend.Transparent,
            BaseColour = new Vector4(0.5, 0.78, 0.9, 0.45),
            Roughness = 0.15,
        };

        // Objects name a material by its key. The renderer keeps one instance
        // behind however many are made of it.
        new RenderScene
        {
            Objects = [new RenderObject { Key = 1, Material = 7, Transform = ..., Colour = ... }],
            Materials = [brass, floor, glass],
            Camera = camera,
        }
        """;

    /// <summary>The grid's squares, a column at a time, with where each sits.</summary>
    private static IEnumerable<(long Key, int Column, int Row, Vector3 At)> Grid() =>
        from column in Enumerable.Range(0, Across)
        from row in Enumerable.Range(0, Down)
        select ((long)(100 + row * Across + column), column, row,
            new Vector3((column - (Across - 1) / 2.0) * 2.2, 1.6 - row * 1.5, -2.5));

    private static RenderObject Scaled(long key, Vector3 at, Vector3 size)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(at);
        transform.Multiply(Matrix4.Diagonal3Values(size.X, size.Y, size.Z));
        return new RenderObject { Key = key, Material = key, Transform = transform, Colour = new Vector3(1, 1, 1) };
    }

    private static Vector3 BlendAt(int i) => new((i - 2) * 2.6, -0.7, 3.6);

    /// <summary>Something behind each square to be seen through, faded into, punched out of or added to.</summary>
    private static RenderObject Backing(int i)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(BlendAt(i) + new Vector3(0, 0, -1.4));
        transform.Multiply(Matrix4.Diagonal3Values(0.5, 0.5, 0.5));
        return new RenderObject { Key = 300 + i, Transform = transform, Colour = Tint.Hex(0x39414B).Linear };
    }

    private static RenderObject Square(int i, double seconds)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(BlendAt(i));
        transform.Multiply(Matrix4.RotationY(seconds * 0.4));
        transform.Multiply(Matrix4.Diagonal3Values(1.0, 1.4, 1.0));
        return new RenderObject { Key = 400 + i, Material = 400 + i, Transform = transform, Colour = new Vector3(1, 1, 1) };
    }

    /// <summary>The floor, and the only thing wearing all three maps at once.</summary>
    private RenderMaterial Floor() => new()
    {
        Key = 1,
        BaseColour = new Vector4(1, 1, 1, 1),
        Roughness = 0.8,
        Metallic = 0.0,
        Reflectance = _reflectance,
        NormalScale = _normalScale,
        Tiling = new Vector2(_tiling, _tiling),
        BaseColourMap = Map(_albedo, srgb: true),
        NormalMap = Map(_normals, srgb: false),
        MetallicRoughnessMap = Map(_packed, srgb: false),
        OcclusionMap = Map(_packed, srgb: false),
    };

    private Texture? Map(string? path, bool srgb) => _textured && path is not null ? new Texture(path, srgb) : null;

    /// <summary>Metalness across, roughness down, and nothing else moving.</summary>
    private RenderMaterial GridMaterial(long key, int column, int row) => new()
    {
        Key = key,
        BaseColour = new Vector4(0.72, 0.45, 0.20, 1),
        Metallic = column / (double)(Across - 1),

        // Never quite nought. A highlight smaller than a pixel flickers, which
        // reads as the renderer being broken and not the surface being smooth.
        Roughness = 0.05 + row / (double)(Down - 1) * 0.85,
        Reflectance = _reflectance,
    };

    private RenderMaterial BlendMaterial((MaterialBlend Mode, int Rgb) blend, int i)
    {
        var linear = Tint.Hex(blend.Rgb).Linear;
        var add = blend.Mode == MaterialBlend.Add;
        var masked = blend.Mode == MaterialBlend.Masked;
        return new RenderMaterial
        {
            Key = 400 + i,
            Blend = blend.Mode,
            BaseColour = new Vector4(linear.X, linear.Y, linear.Z, blend.Mode == MaterialBlend.Opaque || masked ? 1.0 : _alpha),
            Roughness = 0.15,
            Metallic = 0.0,
            Reflectance = _reflectance,

            // Additive surfaces are the usual exception. Writing depth makes them
            // hide each other in whatever order they happened to be drawn.
            DepthWrite = !add,
            EmissiveIntensity = add ? 2.0 : 0.0,
            Emissive = add ? linear : Vector3.Zero(),

            // Masking looks at nothing but alpha, so its square wears a map whose
            // alpha is a pattern. Unlike the two fading modes beside it, this one
            // still writes depth and still sorts, which is why leaves and chain
            // link are done this way and not with transparency.
            MaskThreshold = 0.5,
            Tiling = masked ? new Vector2(2, 2) : new Vector2(1, 1),
            BaseColourMap = masked && _cutout is not null ? new Texture(_cutout) : null,
        };
    }

    /// <summary>A checker with a grout line, so tiling is obvious and so is the filter.</summary>
    private static (double, double, double, double) Albedo(int x, int y)
    {
        var (square, grout) = Tile(x, y);
        return grout ? (46, 42, 40, 255) : square ? (214, 206, 190, 255) : (132, 120, 112, 255);
    }

    /// <summary>
    /// A height field turned into a normal by its slope. Flat is the pale lilac
    /// that packs to nought, which is what a normal map looks like when nothing
    /// is happening.
    /// </summary>
    private static (double, double, double, double) Normal(int x, int y)
    {
        const double Step = 1.0 / Picture.Size;
        var (u, v) = (x / (double)Picture.Size, y / (double)Picture.Size);
        var slopeX = (Height(u + Step, v) - Height(u - Step, v)) / (2 * Step);
        var slopeY = (Height(u, v + Step) - Height(u, v - Step)) / (2 * Step);
        var length = Math.Sqrt(slopeX * slopeX + slopeY * slopeY + 1);
        return (((-slopeX / length) * 0.5 + 0.5) * 255, ((-slopeY / length) * 0.5 + 0.5) * 255, ((1 / length * 0.5) + 0.5) * 255, 255);
    }

    private static double Height(double x, double y) => Math.Sin(x * Math.PI * 4) * Math.Sin(y * Math.PI * 4) * 0.5;

    /// <summary>
    /// The packing every exporter writes: occlusion in red, roughness in green,
    /// metalness in blue. The grout is rougher and never metal. The tiles are
    /// smoother and half of them are metal.
    /// </summary>
    private static (double, double, double, double) Packed(int x, int y)
    {
        var (square, grout) = Tile(x, y);
        return (grout ? 150 : 255, grout ? 255 : square ? 90 : 170, square && !grout ? 255 : 0, 255);
    }

    /// <summary>
    /// Rings of holes in the alpha, which is all masking reads. Solid where the
    /// alpha is high, gone where it is low, and nothing between.
    /// </summary>
    private static (double, double, double, double) Cutout(int x, int y)
    {
        var (u, v) = (x / (double)Picture.Size, y / (double)Picture.Size);
        var ring = Math.Sqrt(Math.Pow((u % 0.25) - 0.125, 2) + Math.Pow((v % 0.25) - 0.125, 2)) / 0.125;
        return (230, 226, 214, Math.Clamp(ring, 0.0, 1.0) * 255);
    }

    private static (bool Square, bool Grout) Tile(int x, int y) =>
        (((x / 32) + (y / 32)) % 2 == 0, (x % 32 < 2) || (y % 32 < 2));
}
