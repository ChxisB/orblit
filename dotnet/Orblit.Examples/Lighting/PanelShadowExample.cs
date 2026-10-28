using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The shadow a window casts. A point source gives a knife edge, because every
/// surface either sees the point or does not. A panel is not a point. A surface
/// near the edge of its shadow sees part of the rectangle, and how much it sees
/// is what the gradient is made of. Widen the panel and the edge softens while
/// the light on the floor barely changes, because the same flux is spread over
/// more surface. Raise it and the shadow lengthens and softens further,
/// because everything under it is further from what lights it.
/// </summary>
public sealed class PanelShadowExample : Example
{
    /// <summary>
    /// The floor, pale and matt, because what lands on it is what is seen on it.
    /// Then three occluders at three heights, because the softness of an edge
    /// grows with the distance between what casts and what catches. The tall
    /// one's shadow is softer at its far end than at its foot.
    /// </summary>
    private static readonly IReadOnlyList<(long Key, Vector3 At, Vector3 Size, int Rgb, double Turn)> Boxes =
    [
        (10, new(0, -2, 0), new(9, 0.12, 9), 0xE8E4DA, 0),
        (11, new(-2.2, -0.4, -0.6), new(0.35, 1.6, 0.35), 0xD9634F, 0),
        (12, new(1.4, -1.35, 1.2), new(0.9, 0.6, 0.9), 0xE8E4DC, 0.5),
        (13, new(2.0, -1.05, -1.8), new(1.4, 0.9, 0.25), 0x7FA8B8, -0.35),
    ];

    /// <summary>The panel's edges, in metres.</summary>
    private double _panel = 2.0;

    /// <summary>How far above the floor it hangs.</summary>
    private double _height = 5.0;

    /// <summary>Whether it casts at all, so the difference can be seen.</summary>
    private bool _shadows = true;

    public override string Name => "Panel shadows";

    public override string Blurb =>
        "The soft shadow a rectangular light casts, and how its edge changes with the size of the panel.";

    public override ExampleSection Section => ExampleSection.Lighting;

    /// <summary>
    /// Looking down at the floor, where the shadow is. A viewpoint at eye level
    /// shows the lit sides of things and almost none of the floor under them.
    /// </summary>
    public override ViewPoint ViewPoint => new() { Yaw = 0.6, Pitch = 0.85, Distance = 15, Height = 0.5 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [.. Boxes.Select(Box)],
        Lights =
        [
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Area,

                // Lumens, like a bulb. A bigger panel does not brighten the
                // floor. The same flux spreads over more surface, which softens
                // the shadow instead.
                Intensity = 900000,

                // Straight down. A panel emits along its own normal, and this
                // one points at the floor, so everything on it is between them.
                Position = new Vector3(0, _height, 0),
                Direction = new Vector3(0, -1, 0),

                // Which way the width runs. Without it the panel is free to spin
                // in its own plane, and a strip on its side is a different light.
                Tangent = new Vector3(1, 0, 0),
                Width = _panel,
                Height = _panel,
                FalloffRadius = 30,
                CastShadows = _shadows,
                Colour = Tint.Hex(0xFFF4E2).Linear,
            },
        ],

        // Low, so what fills the shadow is its own darkness and not the sky. An
        // ambient bright enough to see by would flatten the gradient.
        Sky = new Sky { Colour = Tint.Hex(0x1A1F27).Linear, Ambient = 900 },
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Cast shadows", _shadows, value => _shadows = value),
        new Setting("Panel size", _panel, 0.15, 6, value => _panel = value) { Unit = " m" },
        new Setting("Height", _height, 2, 12, value => _height = value) { Unit = " m" },
    ];

    public override string Code => """
        // A rectangle of light, hung above the floor and pointed at it.
        //
        // CastShadows is what this example is about. A rectangle is not one of
        // Filament's own lights. It is shaded in the surface material against a
        // fitted table, so its shadow is a depth map of its own, drawn once from
        // where the panel stands and compared against by every surface it reaches.
        new RenderLight
        {
            Key = 1,
            Kind = RenderLightKind.Area,
            // Lumens off a surface and not out of a point, so a wider panel
            // spreads the same light and does not add more of it.
            Intensity = 900000,
            Position = new Vector3(0, 5, 0),
            Direction = new Vector3(0, -1, 0),
            // Which way the width runs. A strip on its side is a different light.
            Tangent = new Vector3(1, 0, 0),
            Width = 2,
            Height = 2,
            FalloffRadius = 30,
            CastShadows = true,
        }

        // One rectangle casts. A scene has one key light and the rest are fill,
        // and a map for every panel would cost a scene render each to shadow
        // lights whose job is to not be noticed. A second one asking is reported
        // and lit without a shadow, not dropped.
        """;

    private static RenderObject Box((long Key, Vector3 At, Vector3 Size, int Rgb, double Turn) box)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(box.At);
        transform.RotateY(box.Turn);
        transform.ScaleByDouble(box.Size.X, box.Size.Y, box.Size.Z, 1);
        return new RenderObject
        {
            Key = box.Key,
            Transform = transform,
            Colour = Tint.Hex(box.Rgb).Linear,
            CastShadows = true,
        };
    }
}
