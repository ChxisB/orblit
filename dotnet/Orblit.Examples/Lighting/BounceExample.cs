using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Light that has hit something before it arrives. Direct lighting stops at the
/// first surface it meets. Put a white box between a red wall and a blue one
/// and a renderer without a second bounce draws it white on both sides, while
/// the room would paint one side pink and the other blue. That difference is
/// most of what makes a lit interior read as a room and not as objects on a
/// stage. Two walls facing across a pale floor show it most plainly, which is
/// why every renderer since Cornell in 1984 has been photographed in one.
/// </summary>
public sealed class BounceExample : Example
{
    /// <summary>
    /// Walls as flattened cubes, because a box is the only shape this needs and
    /// a thin one is a wall. The floor is pale and matt, so what lands on it is
    /// what is seen on it. The two boxes at the end are the witnesses. They are
    /// white on every face, so any colour on them arrived from somewhere else.
    /// </summary>
    private static readonly IReadOnlyList<(long Key, Vector3 At, Vector3 Size, int Rgb, double Turn)> Slabs =
    [
        (10, new(0, -3, 0), new(4, 0.15, 4), 0xEDEAE3, 0),
        (11, new(-4, 0, 0), new(0.15, 3, 4), 0xC7332B, 0),
        (12, new(4, 0, 0), new(0.15, 3, 4), 0x2F6DBF, 0),
        (13, new(0, 0, -4), new(4, 3, 0.15), 0xEDEAE3, 0),
        (14, new(-1.5, -1.85, 0.6), new(1, 1, 1), 0xF4F2EE, 0),
        (15, new(1.6, -1.35, -1.4), new(0.9, 1.5, 0.9), 0xF4F2EE, 0.4),
    ];

    private double _strength = 6;
    private double _reach = 3;
    private bool _on = true;

    public override string Name => "Bounced light";

    public override string Blurb =>
        "One bounce of light, taken from the picture already drawn. Coloured walls tint a white box between them.";

    public override ExampleSection Section => ExampleSection.Lighting;

    public override ViewPoint ViewPoint => new() { Yaw = 0, Pitch = 0.16, Distance = 13, Height = -0.6 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [.. Slabs.Select(Slab)],
        Lights =
        [
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Intensity = 70000,

                // Steep, and leaning just enough to put real light on both
                // walls. A bounce can only carry light that arrived, so a wall
                // in its own shadow has nothing to give.
                Direction = new Vector3(-0.32, -1, -0.16).Normalized(),
                SunAngularRadius = 1.5,
                CastShadows = true,
                Colour = Tint.Hex(0xFFF6E8).Linear,
            },
        ],

        // Dim on purpose. Ambient sky light fills shadows evenly and for free,
        // and a room where it does that leaves a second bounce nothing to add.
        // Turned down, the shadowed side of a box is lit by the wall beside it.
        Sky = new Sky { Colour = Tint.Hex(0x181D24).Linear, Ambient = 400 },
        Graph = _on ? Bounce() : RenderGraph.Standard(),
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Bounced light", _on, value => _on = value),
        new Setting("Strength", _strength, 0, 10, value => _strength = value),
        new Setting("Reach", _reach, 0.2, 8, value => _reach = value) { Unit = " m" },
    ];

    public override string Code => """
        // One effect pass over the finished picture. It reads the colour and the
        // depth of the same target, so the graph names that target once.
        new RenderGraph
        {
            Targets = [new RenderTarget { Name = "frame" }],
            Passes =
            [
                new RenderPass { Name = "world", Into = "frame" },
                new RenderPass
                {
                    Name = "bounce",
                    Kind = PassKind.Effect,
                    Effect = PassEffect.Bounce,
                    Reads = ["frame"],
                    // How far it looks, how much comes back, how solid the depth
                    // buffer's surfaces are, and how many directions each pixel
                    // fans along. Nought means the renderer's own default.
                    Plane = [3.0, 4.0, 0, 0],
                },
            ],
        }
        """;

    private static RenderObject Slab((long Key, Vector3 At, Vector3 Size, int Rgb, double Turn) slab)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(slab.At);
        transform.RotateY(slab.Turn);
        transform.ScaleByDouble(slab.Size.X, slab.Size.Y, slab.Size.Z, 1);
        return new RenderObject
        {
            Key = slab.Key,
            Transform = transform,
            Colour = Tint.Hex(slab.Rgb).Linear,
            CastShadows = true,
        };
    }

    private RenderGraph Bounce() => new()
    {
        Targets = [new RenderTarget { Name = "frame" }],
        Passes =
        [
            new RenderPass { Name = "world", Into = "frame" },
            new RenderPass
            {
                Name = "bounce",
                Kind = PassKind.Effect,
                Effect = PassEffect.Bounce,

                // The picture and its depth both come from this one target.
                Reads = ["frame"],
                Plane = [_reach, _strength, 0, 0],
            },
        ],
    };
}
