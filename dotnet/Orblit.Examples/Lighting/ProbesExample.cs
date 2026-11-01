using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A reflection taken from inside the scene, not brought to it. An environment
/// is a photograph of somewhere else, and indoors that is the wrong photograph.
/// A chrome box in a red and blue room reflects the sky, because the sky is the
/// only environment the scene has. A probe is the scene photographing itself
/// from a point inside, so the box reflects the room it is standing in. The
/// comparison is the point, which is why it is a switch and not a setting.
/// </summary>
public sealed class ProbesExample : Example
{
    /// <summary>
    /// A probe captured from inside a mirror photographs the mirror, and the
    /// mirror then reflects a smaller copy of itself. Keeping the shiny things
    /// on their own layer and leaving that layer out of the capture is the
    /// whole of the fix.
    /// </summary>
    private const int ShinyLayer = 1;

    private const int RoomLayer = 0;

    private static readonly IReadOnlyList<(long Key, Vector3 At, Vector3 Size, int Rgb)> Room =
    [
        (10, new(0, -3, 0), new(4, 0.15, 4), 0xEDEAE3),
        (11, new(-4, 0, 0), new(0.15, 3, 4), 0xC7332B),
        (12, new(4, 0, 0), new(0.15, 3, 4), 0x2F6DBF),
        (13, new(0, 0, -4), new(4, 3, 0.15), 0xEDEAE3),
        (14, new(0, 3, 0), new(4, 0.15, 4), 0xEDEAE3),
    ];

    private bool _on = true;
    private double _roughness = 0.08;

    public override string Name => "Reflection probes";

    public override string Blurb =>
        "A chrome box reflecting the room it is in, from a cubemap the scene captured of itself.";

    public override ExampleSection Section => ExampleSection.Lighting;

    public override ViewPoint ViewPoint => new() { Yaw = 0, Pitch = 0.10, Distance = 10.5, Height = -0.9 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [.. Room.Select(Slab), Witness()],
        Materials =
        [
            new RenderMaterial
            {
                Key = 1,
                BaseColour = new Vector4(0.95, 0.95, 0.96, 1),
                Metallic = 1,

                // Never quite nought. A highlight smaller than a pixel flickers,
                // and that reads as the renderer being broken, not as a mirror.
                Roughness = Math.Clamp(_roughness, 0.02, 1.0),
                Reflectance = 0.9,
            },
        ],
        Probes = _on
            ?
            [
                new Probe
                {
                    Key = 100,

                    // Head height in the middle of the room. A probe on the
                    // floor sees a great deal of floor.
                    Position = new Vector3(0, -0.4, 0),
                    Radius = 14,
                    Resolution = 256,

                    // Everything but the shiny things.
                    Layers = 1 << RoomLayer,
                },
            ]
            : [],
        Lights =
        [
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Intensity = 45000,
                Direction = new Vector3(-0.3, -1, -0.2).Normalized(),
                SunAngularRadius = 1.5,
                CastShadows = true,
                Colour = Tint.Hex(0xFFF6E8).Linear,
            },
        ],
        Sky = new Sky { Colour = Tint.Hex(0x6E8CB4).Linear, Ambient = 9000 },
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Probe", _on, value => _on = value),
        new Setting("Roughness", _roughness, 0.02, 0.6, value => _roughness = value),
    ];

    public override string Code => """
        // The scene photographs itself from a point inside, and is lit by that
        // and not by the environment. Captured once and kept. Six renders of
        // the whole scene is not a cost to pay every frame, so raising Version
        // is how a host says the room has changed.
        new RenderScene
        {
            Probes =
            [
                new Probe
                {
                    Key = 100,
                    Position = new Vector3(0, -0.4, 0),   // head height, not the floor
                    Radius = 14,                          // how far its influence reaches
                    Resolution = 256,
                    // Everything but the reflective things. A probe captured from
                    // inside a mirror photographs the mirror, and the mirror then
                    // reflects a smaller copy of itself.
                    Layers = 1 << 0,
                },
            ],
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
            Layer = RoomLayer,
        };
    }

    /// <summary>The chrome box, on its own layer so the capture leaves it out.</summary>
    private static RenderObject Witness()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.6, 0.4));

        // Turned half a right angle, so two faces show at once, one looking at
        // each wall. A box square to the camera shows only the face that
        // reflects what is behind the camera, which says nothing about the room.
        transform.RotateY(0.785);
        transform.ScaleByDouble(1.1, 1.1, 1.1, 1);
        return new RenderObject
        {
            Key = 20,
            Transform = transform,

            // The material's colour is the one seen. Every object still states one.
            Colour = new Vector3(1, 1, 1),
            Material = 1,
            Layer = ShinyLayer,
        };
    }
}
