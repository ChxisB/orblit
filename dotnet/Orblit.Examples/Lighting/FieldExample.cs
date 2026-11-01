using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Light kept in the world instead of on the screen. A bounce taken from the
/// screen only knows what is in frame, so turning away from a red wall takes
/// its bounce with it. A field keeps the answer where the camera cannot move
/// it: probes standing in the room, each holding the light that reaches it
/// from every direction, built up over many frames and read by every surface
/// near it. The room is the bounced light example's on purpose. Here the walls
/// go on tinting the boxes, and the tint is right for where a surface is and
/// not for what happens to be in shot.
/// </summary>
public sealed class FieldExample : Example
{
    private static readonly IReadOnlyList<(long Key, Vector3 At, Vector3 Size, int Rgb)> Slabs =
    [
        (10, new(0, -3, 0), new(4, 0.15, 4), 0xEDEAE3),
        (11, new(-4, 0, 0), new(0.15, 3, 4), 0xC7332B),
        (12, new(4, 0, 0), new(0.15, 3, 4), 0x2F6DBF),
        (13, new(0, 0, -4), new(4, 3, 0.15), 0xEDEAE3),
        (14, new(-1.5, -1.85, 0.6), new(1, 1, 1), 0xF4F2EE),
        (15, new(1.6, -1.35, -1.4), new(0.9, 1.5, 0.9), 0xF4F2EE),
    ];

    /// <summary>
    /// The probes read the picture the scene drew, so the scene is drawn into a
    /// target and then put on the screen. That is the whole reason a copy
    /// effect exists.
    /// </summary>
    private static readonly RenderGraph Present = new()
    {
        Targets = [new RenderTarget { Name = "frame" }],
        Passes =
        [
            new RenderPass { Name = "world", Into = "frame" },
            new RenderPass { Name = "present", Kind = PassKind.Effect, Effect = PassEffect.Copy, Reads = ["frame"] },
        ],
    };

    private bool _on = true;
    private double _intensity = 1.0;
    private double _retention = 0.94;

    public override string Name => "Irradiance field";

    public override string Blurb =>
        "Probes standing in the room hold the light that reaches them, so indirect light stays put when the camera looks away.";

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
                Direction = new Vector3(-0.32, -1, -0.16).Normalized(),
                SunAngularRadius = 1.5,
                CastShadows = true,
                Colour = Tint.Hex(0xFFF6E8).Linear,
            },
        ],

        // Dim on purpose. Ambient fills shadows evenly and for free, and a room
        // where it does that leaves a field nothing to add.
        Sky = new Sky { Colour = Tint.Hex(0x181D24).Linear, Ambient = 400 },
        Field = new IrradianceField
        {
            Enabled = _on,

            // One probe every two metres, from the floor to a little above the
            // walls. Light cannot vary faster than the probes are spaced, which
            // is why a field is coarse and the direct light does the detail.
            Origin = new Vector3(-4, -2.6, -4),
            Spacing = new Vector3(2, 2, 2),
            Counts = new Vector3(5, 4, 5),
            From = "frame",
            Intensity = _intensity,
            Retention = _retention,
        },
        Graph = Present,
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Field", _on, value => _on = value),

        // Three, because past about four a field starts feeding itself. It reads
        // the picture it brightened. The renderer holds it there and says so,
        // and this stops before it has to.
        new Setting("Strength", _intensity, 0, 3, value => _intensity = value),
        new Setting("Retention", _retention, 0.5, 0.99, value => _retention = value),
    ];

    public override string Code => """
        new RenderScene
        {
            Field = new IrradianceField
            {
                Enabled = true,
                Origin = new Vector3(-4, -2.6, -4),   // where the corner probe stands
                Spacing = new Vector3(2, 2, 2),       // metres between probes
                Counts = new Vector3(5, 4, 5),        // how many along each axis
                From = "frame",                       // the target the probes read
                Intensity = 1.6,
                Retention = 0.94,                     // how much survives each frame
            },
            // The probes read the picture the scene drew, so it goes into a
            // target first and is then put on the screen.
            Graph = new RenderGraph
            {
                Targets = [new RenderTarget { Name = "frame" }],
                Passes =
                [
                    new RenderPass { Name = "world", Into = "frame" },
                    new RenderPass
                    {
                        Name = "present",
                        Kind = PassKind.Effect,
                        Effect = PassEffect.Copy,
                        Reads = ["frame"],
                    },
                ],
            },
            // ...
        }
        """;

    private static RenderObject Slab((long Key, Vector3 At, Vector3 Size, int Rgb) slab)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(slab.At);
        transform.ScaleByDouble(slab.Size.X, slab.Size.Y, slab.Size.Z, 1);
        return new RenderObject
        {
            Key = slab.Key,
            Transform = transform,
            Colour = Tint.Hex(slab.Rgb).Linear,
            CastShadows = true,
        };
    }
}
