using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// What the keys are for.
/// </summary>
/// <remarks>
/// Every frame here is a complete description of a thousand objects, sent
/// again from scratch. Nothing on the wire says what changed, and nothing has
/// to, because each object carries a number that is its own. The renderer
/// keeps what it built between frames, writes the transforms that actually
/// moved, and leaves the rest alone.
///
/// Turn Settling on and most of them stop. What that costs the renderer drops
/// with them, which is the whole point of the arrangement.
/// </remarks>
public sealed class ManyExample : Example
{
    private double _count = 600;
    private bool _settling;
    private double _speed = 1;

    public override string Name => "A thousand objects";

    public override string Blurb => "A whole scene sent every frame. You only pay for what moved.";

    public override ExampleSection Section => ExampleSection.Performance;

    public override ViewPoint ViewPoint => new() { Distance = 34, Pitch = 0.45, Height = 1 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var total = (int)Math.Round(_count, MidpointRounding.AwayFromZero);
        return new RenderScene
        {
            Objects =
            [
                .. Enumerable.Range(0, total).Select(i => Crate(i, total, seconds)),
                Floor(),
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 510,
                    Kind = RenderLightKind.Directional,
                    Intensity = 78000,
                    Direction = new Vector3(-0.4, -1, -0.4).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                    SunAngularRadius = 1.2,
                },
            ],
            Sky = new Sky { Colour = Tint.Hex(0x1D242E).Linear, Ambient = 12000 },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Setting("Objects", _count, 10, 2000, value => _count = value) { Decimals = 0 },
        new Setting("Speed", _speed, 0, 4, value => _speed = value),
        new Toggle("Settling", _settling, value => _settling = value),
    ];

    public override string Code => """
        // The whole scene, every frame. There is no add, move or remove call. A
        // description that says everything cannot go stale, and a key nobody sends
        // this time is an object that has left.
        new RenderScene
        {
            Objects =
            [
                .. Enumerable.Range(0, total).Select(i => new RenderObject
                {
                    Key = 1000 + i,                   // its own, for as long as it exists
                    Transform = PlacementOf(i, seconds),
                    Colour = ColourOf(i),
                    CastShadows = i % 7 == 0,
                }),
            ],
            Lights = [sun],
            Camera = camera,
        }

        // On the other side, a transform is compared before it is written, because
        // writing one dirties the node and everything under it. A mesh instance whose
        // object is gone goes back to a pool rather than being destroyed. Only a
        // change of mesh rebuilds anything.
        """;

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -2.6, 0));
        transform.ScaleByDouble(30.0, 0.06, 30.0, 1);
        return new RenderObject
        {
            Key = 500,
            Transform = transform,
            Colour = Tint.Hex(0x3A4048).Linear,
            CastShadows = false,
        };
    }

    private RenderObject Crate(int index, int total, double seconds)
    {
        // Placed on a spiral, so the count can change without everything jumping.
        var angle = index * 2.399;
        var radius = Math.Sqrt(index + 1) * 0.62;

        // Most of them stop when settling is on. The ones that keep going are
        // the only ones the renderer writes a transform for.
        var moving = !_settling || index % 11 == 0;
        var wobble = moving ? Math.Sin((seconds * _speed) + (index * 0.7)) : 0.0;

        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(
            Math.Cos(angle) * radius,
            -2.2 + (wobble * 0.6) + ((index % 5) * 0.12),
            Math.Sin(angle) * radius));
        transform.RotateY(angle + (wobble * 0.3));
        transform.ScaleByDouble(0.34, 0.34 + ((index % 4) * 0.1), 0.34, 1);
        return new RenderObject
        {
            // Its own number, kept for as long as it exists. Two objects sharing
            // one would be one of them quietly taking the other's place.
            Key = 1000 + index,
            Transform = transform,
            Colour = Tint.Lerp(Tint.Hex(0xD9634F), Tint.Hex(0x5FA8D3), index / (double)Math.Max(total - 1, 1)).Linear,
            // Shadows from a thousand things are a thousand things in the shadow
            // map. Worth having off by default at this count.
            CastShadows = index % 7 == 0,
        };
    }
}
