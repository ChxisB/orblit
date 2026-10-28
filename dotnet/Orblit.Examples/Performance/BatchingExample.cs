using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Many copies of one thing, drawn as few things.
/// </summary>
/// <remarks>
/// Every crate here is an ordinary <see cref="RenderObject"/> with its own key,
/// and nothing about how the scene is written changes when batching is switched
/// on. What changes is on the far side. Crates that are the same mesh, made of
/// the same stuff, with the same shadow settings, are made to share one
/// material instance. The renderer merges their draws into instanced ones that
/// each carry every copy's own transform.
///
/// Three things are worth trying. Flip Batching, which is on by default here
/// and everywhere, and the picture barely moves: 2.27% of pixels, by 2.6 parts
/// in 255 on average, all of it along the edges of the shadows one crate in
/// five casts. That is the whole of what merging costs, measured rather than
/// assumed. It comes from a merged group being culled and shadow-fitted by one
/// box for up to sixty-four members rather than one box each. Turn the shadows
/// off and the whole of it goes: two pixels left in the frame, each by one
/// level. This example is the worst case on purpose. Three thousand crates lie
/// across a wide grid, so a chunk of sixty-four spans a lot of ground. Scenes
/// with a dozen or a couple of hundred objects near each other move by single
/// pixels.
///
/// Change the palette from one colour to every crate different, and the
/// batching has nothing left to do. On the default surface a crate's colour is
/// its material, so the switch stops changing anything at all. And turn Moving
/// on: one crate in the middle keeps turning, and only that one crate's
/// transform is written each frame, batched or not.
/// </remarks>
public sealed class BatchingExample : Example
{
    private const long MaterialKey = 7;

    private static readonly int[] Swatches = [0xC8874E, 0x8E6B45, 0x5FA8D3, 0xD9634F, 0x7FA86A, 0xE8D9B0];

    /// <summary>Whether identical crates are merged.</summary>
    private bool _batching = true;

    /// <summary>How many crates there are.</summary>
    private double _count = 3000;

    /// <summary>How many different colours they come in: one, a handful, or one each.</summary>
    private string _palette = "One";

    /// <summary>
    /// Whether they are made of a shared material rather than the default
    /// surface tinted by their colour.
    /// </summary>
    private bool _material;

    /// <summary>
    /// Whether the crate in the middle turns, to show a single member of a
    /// batch being moved on its own.
    /// </summary>
    private bool _moving = true;

    public override string Name => "Batching";

    public override string Blurb =>
        "Thousands of identical crates, merged into instanced draws without the scene asking for it.";

    public override ExampleSection Section => ExampleSection.Performance;

    public override ViewPoint ViewPoint => new() { Yaw = 0.5, Pitch = 0.55, Distance = 46, Height = 0 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var total = (int)Math.Round(_count, MidpointRounding.AwayFromZero);
        var side = (int)Math.Ceiling(Math.Sqrt(total));
        const double spacing = 1.1;
        var half = (side - 1) * spacing / 2;
        var first = Tint.Hex(Swatches[0]).Linear;
        return new RenderScene
        {
            Batching = _batching,
            Materials = _material
                ? [new RenderMaterial { Key = MaterialKey, BaseColour = new Vector4(first.X, first.Y, first.Z, 1), Roughness = 0.7 }]
                : [],
            Objects =
            [
                .. Enumerable.Range(0, total).Select(i =>
                {
                    var transform = Matrix4.Identity();
                    transform.SetTranslation(new Vector3(((i % side) * spacing) - half, -2.2, ((i / side) * spacing) - half));
                    // The one in the middle turns. Batched or not, its transform
                    // is the only one written each frame. The rest are compared
                    // and left alone.
                    transform.RotateY(_moving && i == total / 2 ? seconds : (i % 4) * 0.2);
                    transform.ScaleByDouble(0.4, 0.4, 0.4, 1);
                    return new RenderObject
                    {
                        Key = 1000 + i,
                        Material = _material ? MaterialKey : null,
                        Transform = transform,
                        Colour = ColourOf(i, total),
                        // One in five casts, so the shadow pass has its own
                        // batches to merge rather than every crate or none.
                        CastShadows = i % 5 == 0,
                    };
                }),
                Floor(half),
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 2,
                    Kind = RenderLightKind.Directional,
                    Intensity = 78000,
                    Direction = new Vector3(-0.4, -1, -0.3).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                },
            ],
            Sky = new Sky { Colour = Tint.Hex(0x1D242E).Linear, Ambient = 12000 },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Batching", _batching, value => _batching = value),
        new Setting("Crates", _count, 100, 6000, value => _count = value) { Decimals = 0 },
        new Choice("Colours", ["One", "Six", "Every one"], _palette, value => _palette = value),
        new Toggle("Shared material", _material, value => _material = value),
        new Toggle("Moving", _moving, value => _moving = value),
    ];

    public override string Code => """
        // Nothing about the objects changes. Each crate is still its own object
        // with its own key. The scene just says that batching is allowed.
        new RenderScene
        {
            Batching = true,
            Objects =
            [
                .. Enumerable.Range(0, 3000).Select(i => new RenderObject
                {
                    Key = 1000 + i,
                    Transform = PlacementOf(i),
                    Colour = crateColour,       // the same for all, or they cannot share
                    CastShadows = i % 5 == 0,   // casters batch with casters
                }),
            ],
            Camera = camera,
        }

        // On the other side, objects with the same mesh, material and flags are
        // counted as they arrive, and on the default surface they need the same
        // colour too. Groups of four or more share one material instance, and the
        // renderer merges their draws into instanced ones. A transform written to
        // one crate moves that one only.
        """;

    private static RenderObject Floor(double half)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -2.7, 0));
        transform.ScaleByDouble(half + 3, 0.06, half + 3, 1);
        return new RenderObject
        {
            Key = 1,
            Transform = transform,
            Colour = Tint.Hex(0x3A4048).Linear,
            CastShadows = false,
        };
    }

    private Vector3 ColourOf(int index, int total) => _palette switch
    {
        "One" => Tint.Hex(Swatches[0]).Linear,
        "Six" => Tint.Hex(Swatches[index % Swatches.Length]).Linear,
        // Every one different, which is the case batching cannot help. A lerp
        // across the whole set gives each crate a colour of its own.
        _ => Tint.Lerp(Tint.Hex(Swatches[2]), Tint.Hex(Swatches[3]), index / (double)Math.Max(total - 1, 1)).Linear,
    };
}
