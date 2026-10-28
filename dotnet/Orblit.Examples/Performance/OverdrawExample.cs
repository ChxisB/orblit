using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Surfaces stacked deep in front of each other, to see what overdraw costs.
/// </summary>
/// <remarks>
/// The renderer already sorts opaque objects nearest first. So a pile of
/// separate things standing one behind another is mostly shaded once without
/// any help: the near one fills the depth buffer and the GPU skips the rest.
/// What sorting cannot fix is surfaces that pass through each other. These
/// slabs all cross at the middle, so every one is in front of the others
/// somewhere and behind them somewhere else. No order of objects is the right
/// order for every pixel. That is the case a depth prepass exists for.
///
/// It is also the scene that says a prepass is not worth turning on here. On an
/// Apple GPU, going from one slab to ninety-six of them moves a frame from
/// about 3.4 ms to about 3.9 ms. Every slab wears the dearest surface the
/// engine has, with three specular lobes and seven lights. Forty-eight slabs
/// sometimes measures faster than one. The hardware works out which surface
/// wins a tile before it shades any of it, which is a prepass already. So there
/// is no fragment cost here left for a second pass to save, and a second pass
/// would only add the draws. Where it should pay is a desktop Vulkan or GL
/// backend, which shades every layer it is handed in the order it is handed
/// them.
///
/// <see cref="RenderScene.DepthPrepass"/> builds the second pass, and this
/// scene was measured with it on and off, on an M4 Pro, three runs each way.
/// It took 3.97 ms against 4.10 ms at ninety-six slabs, 3.84 against 3.89 at
/// forty-eight, and 3.47 either way at one. The spread within a single setting
/// is a hundredth of a millisecond, so the loss at ninety-six slabs is real
/// rather than noise. A prepass on this hardware costs about three per cent and
/// returns nothing, which is why the switch ships off.
///
/// The other half of the answer needed a GPU that is not tile-based, which no
/// Apple machine has. This same scene was built into the headless host and run
/// under Mesa's llvmpipe in a Linux container. That is a software rasteriser,
/// so immediate-mode by construction, shading every layer it is handed. There
/// the prepass roughly halves the frame: 40.9 ms against 21.3 ms at ninety-six
/// slabs, 44.0 against 17.3 at forty-eight, medians of three runs. At one slab
/// it is 11.0 against 11.3. That is the control, and the same small loss the
/// Apple GPU shows, because with nothing hidden there is nothing to save.
///
/// The picture is bit-identical with the prepass on and off, on both backends:
/// 0 of 518400 pixels differ. That is worth stating because it was not true of
/// the first attempt. A prepass entity sitting exactly on the surface it stands
/// in for occludes that surface in the structure buffer that contact shadows
/// march along. One per cent of the frame came back visibly darker until the
/// depth-only draw was pushed a hair behind.
/// </remarks>
public sealed class OverdrawExample : Example
{
    private const long MaterialKey = 3;

    /// <summary>How many slabs cross at the middle: how deep the overdraw goes.</summary>
    private double _slabs = 48;

    /// <summary>
    /// Whether the slabs wear the dearest surface there is, so that shading a
    /// pixel twice actually costs something.
    /// </summary>
    private bool _heavy = true;

    public override string Name => "Overdraw";

    public override string Blurb =>
        "Slabs passing through each other, so every pixel is drawn once for each one that covers it.";

    public override ExampleSection Section => ExampleSection.Performance;

    public override ViewPoint ViewPoint => new() { Yaw = 0.3, Pitch = 0.25, Distance = 16, Height = 0 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var count = (int)Math.Round(_slabs, MidpointRounding.AwayFromZero);
        return new RenderScene
        {
            Materials =
            [
                new RenderMaterial
                {
                    Key = MaterialKey,
                    BaseColour = new Vector4(0.62, 0.42, 0.30, 1),
                    Roughness = 0.35,
                    Metallic = _heavy ? 0.4 : 0.0,
                    // A coat over a sheen over a stretched highlight: three more
                    // lobes for every pixel, which is what makes overdraw show
                    // up as time.
                    ClearCoat = _heavy ? 1.0 : 0.0,
                    ClearCoatRoughness = 0.15,
                    Anisotropy = _heavy ? 0.7 : 0.0,
                },
            ],
            Objects = [.. Enumerable.Range(0, count).Select(i => Slab(i, count)), Floor()],
            Lights =
            [
                new RenderLight
                {
                    Key = 2,
                    Kind = RenderLightKind.Directional,
                    Intensity = 70000,
                    Direction = new Vector3(-0.3, -1, -0.5).Normalized(),
                },
                .. Enumerable.Range(0, 6).Select(i => new RenderLight
                {
                    Key = 10 + i,
                    Kind = RenderLightKind.Point,
                    Intensity = 30000,
                    Position = new Vector3(Math.Cos(i * 1.05) * 4, 0.5, Math.Sin(i * 1.05) * 4),
                    Colour = Tint.Lerp(Tint.Hex(0x5FA8D3), Tint.Hex(0xD9634F), i / 5.0).Linear,
                    FalloffRadius = 9,
                    CastShadows = false,
                }),
            ],
            Sky = new Sky { Colour = Tint.Hex(0x1D242E).Linear, Ambient = 9000 },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Setting("Slabs", _slabs, 4, 160, value => _slabs = value) { Decimals = 0 },
        new Toggle("Heavy surface", _heavy, value => _heavy = value),
    ];

    public override string Code => """
        // Slabs all crossing at the middle, so no order of objects is the right
        // order for every pixel and each one is covered many times over.
        new RenderScene
        {
            Materials = [new RenderMaterial { Key = 3, ClearCoat = 1, Anisotropy = 0.7 }],
            Objects =
            [
                .. Enumerable.Range(0, 48).Select(i =>
                {
                    var transform = Matrix4.Identity();
                    transform.RotateY(i * Math.PI / 48);
                    transform.ScaleByDouble(5, 3, 0.05, 1);
                    return new RenderObject { Key = 100 + i, Material = 3, Transform = transform };
                }),
            ],
            Camera = camera,
        }

        // What it is for is measuring. Turn the slabs up and watch the frame's GPU
        // time. On this machine it barely moves, because the hardware already
        // decides which surface wins a tile before shading any of it.
        """;

    /// <summary>
    /// Every slab through the middle at its own angle, so each is nearest
    /// somewhere and furthest somewhere else.
    /// </summary>
    private static RenderObject Slab(int i, int count)
    {
        var transform = Matrix4.Identity();
        transform.RotateY(i * Math.PI / count);
        transform.RotateX(Math.Sin(i * 1.7) * 0.35);
        transform.ScaleByDouble(5.0, 3.0, 0.05, 1);
        return new RenderObject
        {
            Key = 100 + i,
            Material = MaterialKey,
            Transform = transform,
            Colour = new Vector3(1, 1, 1),
            CastShadows = false,
        };
    }

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -3.2, 0));
        transform.ScaleByDouble(14, 0.05, 14, 1);
        return new RenderObject
        {
            Key = 1,
            Transform = transform,
            Colour = Tint.Hex(0x3A4048).Linear,
            CastShadows = false,
        };
    }
}
