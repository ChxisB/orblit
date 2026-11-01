using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A line round what is selected, following its shape.
/// </summary>
/// <remarks>
/// What an editor shows its selection with, and the thing a box drawn over the
/// picture cannot do. A box does not know what is in front of what. Here the
/// active object, the one an inspector would be showing, stands half behind a
/// wall. Its hidden half is still outlined, fainter and dashed, so it can be
/// found without being mistaken for something in front.
///
/// Worth trying: turn the outline off and nothing about the frame changes,
/// because the renderer skips every pass of it. Widen it and the cost barely
/// moves, because the edge is found a row and then a column at a time rather
/// than by searching a disc round every pixel.
/// </remarks>
public sealed class OutlineExample : Example
{
    private static readonly IReadOnlyList<(string Label, AntiAliasing AntiAliasing)> Smoothings =
    [
        ("Off", AntiAliasing.Off),
        ("FXAA", AntiAliasing.Fxaa),
        ("Temporal", AntiAliasing.Temporal),
    ];

    private const long FloorKey = 1;
    private const long WallKey = 2;
    private const long BehindKey = 3;
    private const long BesideKey = 4;
    private const long SphereKey = 5;

    /// <summary>Whether anything is outlined at all.</summary>
    private bool _on = true;

    /// <summary>Whether the box beside the wall is selected too, as well as the one behind it.</summary>
    private bool _others = true;

    /// <summary>In pixels.</summary>
    private double _width = 3;

    /// <summary>What becomes of the part the wall hides.</summary>
    private OutlineOccluded _occluded = OutlineOccluded.Dashed;

    /// <summary>
    /// How the frame underneath is anti-aliased. The outline is drawn after all
    /// of them, so switching this changes the objects' edges and leaves the
    /// outline exactly where it was. That is the point of drawing it last.
    /// Under temporal anti-aliasing an outline fed into the history would
    /// shimmer every time the camera moved.
    /// </summary>
    private AntiAliasing _antiAliasing = AntiAliasing.Temporal;

    public override string Name => "Outline";

    public override string Blurb =>
        "A selection outline that follows the shape, and still finds an object hidden behind a wall.";

    public override ExampleSection Section => ExampleSection.Effects;

    public override ViewPoint ViewPoint => new() { Yaw = 0.35, Pitch = 0.32, Distance = 11, Height = 0.4 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects =
        [
            Box(FloorKey, new Vector3(0, -1.1, 0), new Vector3(7, 0.1, 7), 0x8A8F96),
            // The wall: in front of the active object, hiding its left half.
            Box(WallKey, new Vector3(-0.9, 0.2, 1.2), new Vector3(1.4, 1.2, 0.12), 0xB9B2A6),
            // The active object, behind the wall.
            Box(BehindKey, new Vector3(0, 0, -0.6), new Vector3(0.9, 0.9, 0.9), 0x4F7FB8),
            // The rest of the selection, in plain view.
            Box(BesideKey, new Vector3(2.4, -0.4, 0.6), new Vector3(0.5, 0.6, 0.5), 0xB85F4F),
            // Not selected: something for the eye to compare against.
            Box(SphereKey, new Vector3(-2.6, -0.5, -0.8), new Vector3(0.5, 0.5, 0.5), 0x6FA06A),
        ],
        Lights =
        [
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.4, -0.8, -0.45).Normalized(),
                Colour = new Vector3(1, 0.96, 0.9),
                Intensity = 90000,
                CastShadows = true,
            },
        ],
        Sky = new Sky
        {
            Zenith = new Vector3(0.30, 0.50, 0.78),
            Horizon = new Vector3(0.72, 0.84, 0.94),
            Ambient = 24000,
        },
        Camera = camera,
        Post = new PostProcess { AntiAliasing = _antiAliasing },
        Outline = _on
            ? new Outline
            {
                Primary = BehindKey,
                Keys = _others ? [BesideKey] : [],
                Width = _width,
                Occluded = _occluded,
            }
            : Outline.None,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Outline", _on, value => _on = value),
        new Toggle("Select the second box too", _others, value => _others = value)
        {
            Note = "Drawn in the deeper orange: selected, but not the active one.",
        },
        new Setting("Width", _width, 1, Outline.MaxWidth, value =>
            _width = Math.Round(value, MidpointRounding.AwayFromZero))
            { Unit = " px", Decimals = 0 },
        new Choice(
            "Behind the wall",
            Enum.GetNames<OutlineOccluded>(),
            _occluded.ToString(),
            value => _occluded = Enum.Parse<OutlineOccluded>(value)),
        new Choice(
            "Anti-aliasing underneath",
            [.. Smoothings.Select(one => one.Label)],
            Smoothings.First(one => one.AntiAliasing == _antiAliasing).Label,
            value => _antiAliasing = Smoothings.First(one => one.Label == value).AntiAliasing),
    ];

    public override string Code => """
        // The outline is on the scene, not on the objects. It is about how the
        // world is being looked at, and an editor changes it on every click
        // without touching a single object.
        new RenderScene
        {
            Objects = objects,
            Camera = camera,
            Outline = new Outline
            {
                // The active object, in the lighter orange.
                Primary = behindTheWall.Key,
                // Everything else selected, in the deeper one.
                Keys = [besideIt.Key],
                Width = 3,
                // What the wall hides is still outlined, but fainter and dashed,
                // so it reads as behind rather than in front.
                Occluded = OutlineOccluded.Dashed,
            },
        }

        // The colours are display colours. The outline is drawn after tone
        // mapping and lands on screen as exactly what was asked for.
        // Outline.None, the default, draws nothing and costs nothing.
        """;

    private static RenderObject Box(long key, Vector3 at, Vector3 size, int rgb)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(at);
        transform.ScaleByDouble(size.X, size.Y, size.Z, 1);
        return new RenderObject { Key = key, Transform = transform, Colour = Tint.Hex(rgb).Linear };
    }
}
