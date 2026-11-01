using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Air that bends the light through it.
/// </summary>
/// <remarks>
/// Three kinds at once, on a chequered floor because a grid is what shows a
/// bend. A straight line that stops being straight is the whole of the effect.
/// A shockwave runs out across the floor from one corner, heat rises off a vent
/// in the other, and the lens can warp the lot.
///
/// The pillar in front of the vent is the part worth looking at twice. The haze
/// is behind it, so its edges stay straight while the floor beyond them
/// shimmers. The distortion knows what is in front of it.
/// </remarks>
public sealed class DistortionExample : Example
{
    /// <summary>
    /// Seconds between one wave and the next. Each is gone before the next
    /// begins, so there is always exactly one on the floor.
    /// </summary>
    private const double Every = 2.5;

    private const long TilesKey = 1000;
    private const long VentKey = 10;
    private const long PillarKey = 11;

    private static readonly Vector3 Blast = new(-2.5, -1.5, 0.5);
    private static readonly Vector3 Vent = new(2.4, -1.5, -1.2);

    private bool _shockwave = true;
    private bool _haze = true;
    private double _strength = 0.03;
    private double _lens;
    private double _chromatic = 0.3;

    public override string Name => "Distortion";

    public override string Blurb =>
        "A shockwave across a chequered floor, heat rising off a vent, and a lens warping all of it.";

    public override ExampleSection Section => ExampleSection.Effects;

    public override ViewPoint ViewPoint => new() { Yaw = 0, Pitch = 0.55, Distance = 13, Height = 0 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects =
        [
            // Sixteen by sixteen tiles of eighty centimetres.
            .. Enumerable.Range(0, 256).Select(i => Box(
                TilesKey + i,
                new Vector3(((i % 16) - 7.5) * 0.8, -1.6, ((i / 16) - 7.5) * 0.8),
                new Vector3(0.4, 0.1, 0.4),
                ((i % 16) + (i / 16)) % 2 == 0 ? 0xE8E4DC : 0x26282C)),
            Box(VentKey, Vent + new Vector3(0, 0.15, 0), new Vector3(0.6, 0.15, 0.6), 0x3A2A22),
            // In front of the haze, and not bent by it.
            Box(PillarKey, Vent + new Vector3(-0.2, 1.4, 1.6), new Vector3(0.12, 1.4, 0.12), 0xB8452E),
        ],
        Lights =
        [
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Intensity = 90000,
                Direction = new Vector3(-0.4, -1, -0.3).Normalized(),
                Colour = new Vector3(1, 0.97, 0.92),
                CastShadows = true,
            },
        ],
        Sky = new Sky
        {
            Zenith = new Vector3(0.30, 0.50, 0.78),
            Horizon = new Vector3(0.72, 0.84, 0.94),
            Ambient = 24000,
        },
        // A wave that has run its course and a lens at nought both have a
        // strength of nought, and a scene whose distortions are all at rest
        // draws no extra pass at all.
        Distortions =
        [
            .. _shockwave
                ? [Distortion.Expanding(
                    Blast,
                    seconds % Every,
                    speed: 3.5,
                    lifetime: Every,
                    thickness: 0.8,
                    strength: _strength,
                    chromatic: _chromatic)]
                : Array.Empty<Distortion>(),
            .. _haze
                ? [Distortion.Haze(
                    Vent + new Vector3(0, 1.6, 0),
                    new Vector3(0.9, 1.6, 0.9),
                    strength: 0.008,
                    scale: 0.18,
                    speed: 1.2,
                    seconds: seconds)]
                : Array.Empty<Distortion>(),
            .. _lens != 0 ? [Distortion.Lens(_lens, _chromatic)] : Array.Empty<Distortion>(),
        ],
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Shockwave", _shockwave, value => _shockwave = value),
        new Toggle("Heat haze", _haze, value => _haze = value),
        new Setting("Wave strength", _strength, 0, 0.08, value => _strength = value),
        new Setting("Lens warp", _lens, -0.3, 0.3, value => _lens = value),
        new Setting("Chromatic split", _chromatic, 0, 1, value => _chromatic = value),
    ];

    public override string Code => """
        // Distortions are part of the scene, like its lights. The renderer sums
        // them in one pass over the finished frame, and draws no pass at all
        // when none of them is moving anything.
        new RenderScene
        {
            Distortions =
            [
                Distortion.Expanding(
                    blast,
                    seconds - wentOffAt,  // the host's clock
                    speed: 3.5,
                    chromatic: 0.3),
                Distortion.Haze(
                    vent + new Vector3(0, 1.6, 0),
                    new Vector3(0.9, 1.6, 0.9),
                    seconds: seconds),
                Distortion.Lens(0.1),  // barrel. Negative is pincushion
            ],
            // ...
        }
        """;

    private static RenderObject Box(long key, Vector3 at, Vector3 half, int rgb)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(at);
        transform.ScaleByDouble(half.X, half.Y, half.Z, 1);
        return new RenderObject { Key = key, Transform = transform, Colour = Tint.Hex(rgb).Linear, CastShadows = true };
    }
}
