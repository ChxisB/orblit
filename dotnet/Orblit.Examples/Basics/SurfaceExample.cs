using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The first thing the engine had to prove: a lit scene that is a control. Not
/// a window on top of the application but a picture inside it, which lays out,
/// clips and scrolls like anything else. Everything in the gallery is drawn
/// into the panel beside these words, which is the point.
/// </summary>
public sealed class SurfaceExample : Example
{
    private static readonly IReadOnlyList<(string Name, int Rgb)> Swatches =
    [
        ("Clay", 0xD9634F),
        ("Amber", 0xE5B84F),
        ("Sky", 0x5FA8D3),
        ("Moss", 0x6FBF73),
        ("Bone", 0xE8E4DC),
    ];

    private double _spin = 0.35;
    private int _swatch;

    public override string Name => "A scene as a control";

    public override string Blurb => "A lit surface drawn inside the window, laid out like any other control.";

    public override ExampleSection Section => ExampleSection.Basics;

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var turn = seconds * _spin;
        var spun = Matrix4.Identity();
        spun.RotateY(turn);
        spun.RotateX(turn * 0.42);

        // A floor, so the light has somewhere to land and the shadow has somewhere to fall.
        var floor = Matrix4.Identity();
        floor.SetTranslation(new Vector3(0, -1.6, 0));
        floor.ScaleByDouble(6, 0.06, 6, 1);

        return new RenderScene
        {
            Objects =
            [
                new RenderObject { Key = 1, Transform = spun, Colour = Tint.Hex(Swatches[_swatch].Rgb).Linear },
                new RenderObject
                {
                    Key = 2,
                    Transform = floor,
                    Colour = Tint.Hex(0x3B424C).Linear,
                    CastShadows = false,
                },
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 10,
                    Kind = RenderLightKind.Directional,
                    Intensity = 82000,
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                    Direction = new Vector3(-0.4, -1, -0.55).Normalized(),
                },
            ],
            Sky = new Sky { Colour = Tint.Hex(0x1B222C).Linear, Ambient = 9000 },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Setting("Spin", _spin, 0, 2, value => _spin = value),
        new Choice(
            "Colour",
            [.. Swatches.Select(swatch => swatch.Name)],
            Swatches[_swatch].Name,
            option => _swatch = Swatches.Select(swatch => swatch.Name).ToList().IndexOf(option)),
    ];

    public override string Code => """
        // A scene is a description, handed over whole every frame.
        viewport.Frame(new RenderScene
        {
            Objects =
            [
                new RenderObject
                {
                    Key = 1,
                    Transform = Matrix4.RotationY(turn),
                    Colour = new Vector3(0.72, 0.13, 0.08),   // linear RGB
                },
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 10,
                    Kind = RenderLightKind.Directional,
                    Intensity = 82000,                         // lux
                    Direction = new Vector3(-0.4, -1, -0.55).Normalized(),
                },
            ],
            Sky = new Sky { Colour = new Vector3(0.01, 0.02, 0.03), Ambient = 9000 },
            Camera = camera,
        }, seconds);

        // The key is what makes saying it again cheap. Same key, same object.
        // The renderer moves what moved instead of building the scene again.
        """;
}
