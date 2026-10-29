using System.Globalization;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A place made of soft blobs rather than of surfaces.
/// </summary>
/// <remarks>
/// A Gaussian splat capture is millions of small coloured ellipsoids, fitted
/// to photographs until, seen from where the photographs were taken, they
/// add up to the place. Each is drawn as an ellipse the size its ellipsoid
/// projects to, fading out as a Gaussian, blended over whatever is behind it.
///
/// Nothing needs downloading here: the cloud is generated, either way round.
/// The garden is a tree in a clearing, built the way a capture is put
/// together rather than the way a modeller would build a tree, which is what
/// a million splats are actually for. The ring is a torus of flat,
/// half-transparent discs, striped so that its near and far sides are
/// different colours: it is no place at all, and it is the one that teaches.
/// Where the two sides overlap on screen, the order they are blended in
/// decides which colour wins, so turning the sort off on the ring shows
/// exactly what a splat renderer that does not sort gets wrong. On the
/// garden the same fault is only a haze.
///
/// The pillar is solid geometry standing in the ring, to show the other rule:
/// splats test against the depth of the solid scene and never write their
/// own, so a wall hides a cloud and a cloud never hides a wall. It stands in
/// the ring and nowhere else. In the garden it would only be a monolith in a
/// meadow, and the ground the garden already stands on makes the same point
/// more quietly.
///
/// And the cloud is held to what the device can carry: no more splats than
/// its profile budgets, harmonics no higher than its degree, and a coarse
/// sort where it is low tier. That is how one scene is written once for a
/// desktop, a phone and a browser.
/// </remarks>
public sealed class SplatsExample : Example
{
    private static readonly IReadOnlyList<string> Shapes = ["Garden", "Ring"];

    private static readonly IReadOnlyList<(string Label, int Count)> Counts =
    [
        ("100k", 100000),
        ("300k", 300000),
        ("1M", 1000000),
    ];

    // A real capture to show instead, .ply or .splat.
    private readonly string? _path;

    // A limit of the example's own, which wins over the device's.
    private readonly int? _limit;

    private int _count = 300000;

    // Off is wrong on purpose, for comparison.
    private bool _sorted = true;

    // Only ever drawn in the ring. The garden has no use for it.
    private bool _pillar = true;

    private double _opacity = 1;
    private double _brightness = 1;
    private bool _ring;

    // Only reaches a .ply from the path. The generated clouds are packed into
    // the compact 32-byte records, which have no room for any, so they are the
    // same colour from every side however this is set.
    private int _harmonics = 2;

    private bool _deviceLimits = true;
    private bool _coarseOrder;

    private byte[]? _data;
    private int _builtFor = -1;
    private bool? _ringFor;
    private int? _limitFor;
    private int _revision;

    /// <summary>
    /// Reads its settings from <paramref name="environment"/>, which the
    /// gallery passes as the process environment and a test passes as a table.
    /// </summary>
    public SplatsExample(Func<string, string?> environment)
    {
        var capture = environment("ORBLIT_SPLAT");
        _path = string.IsNullOrEmpty(capture) ? null : capture;
        if (int.TryParse(environment("ORBLIT_SPLAT_COUNT"), out var count) && count > 0)
        {
            _count = count;
        }

        if (int.TryParse(environment("ORBLIT_SPLAT_HARMONICS"), out var harmonics))
        {
            _harmonics = Math.Clamp(harmonics, 0, 3);
        }

        if (int.TryParse(environment("ORBLIT_SPLAT_LIMIT"), out var limit) && limit > 0)
        {
            _limit = Math.Min(limit, SplatCloud.MaxLimit);
        }

        _sorted = environment("ORBLIT_SPLAT_SORT") != "0";
        _pillar = environment("ORBLIT_SPLAT_PILLAR") != "0";
        _coarseOrder = environment("ORBLIT_SPLAT_COARSE") == "1";
        _deviceLimits = environment("ORBLIT_SPLAT_DEVICE") != "0";
    }

    public override string Name => "Gaussian splats";

    public override string Blurb =>
        "A cloud of 3D Gaussians, drawn as ellipses, sorted back to front off the render thread, and kept " +
        "within what the device can carry.";

    public override ExampleSection Section => ExampleSection.Content;

    // Far enough back and high enough to hold the whole tree, which is the
    // taller of the two clouds by a good margin, and no further: the tree is
    // the picture and the lawn around it is not.
    public override ViewPoint ViewPoint => new() { Yaw = 0.6, Pitch = 0.13, Distance = 8.0, Height = 0.7 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var wanted = Limit;
        if (_path is null && (_builtFor != _count || _ringFor != _ring || _limitFor != wanted))
        {
            Rebuild(wanted);
        }

        return new RenderScene
        {
            Objects = _pillar && _ring ? [Floor(), Pillar()] : [Floor()],
            Splats =
            [
                new SplatCloud(1, _path, _path is null ? _data : null)
                {
                    // A capture comes out of structure from motion with y
                    // pointing down, as the first photograph's camera had it,
                    // so one read from a file is turned the right way up.
                    Transform = _path is null ? Matrix4.Identity() : Matrix4.RotationX(Math.PI),
                    Opacity = _opacity,
                    Brightness = _brightness,
                    Sorted = _sorted,
                    Harmonics = Harmonics,
                    Limit = wanted,
                    CoarseOrder = Coarse,
                    Revision = _revision,
                },
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    Direction = new Vector3(-0.4, -0.8, -0.45).Normalized(),
                    Intensity = 60000,
                    Colour = new Vector3(1, 0.97, 0.92),
                },
            ],
            Sky = new Sky { Colour = Tint.Hex(0x12151B).Linear, Ambient = 9000 },
            // Straight through rather than filmic. A capture's colours are the
            // photographs' own, already graded by whatever camera took them,
            // and a second tone curve over them only greys them out.
            Post = new PostProcess { Grading = new Grading { ToneMapping = ToneMapping.Linear } },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Sort back to front", _sorted, value => _sorted = value)
        {
            Note = _sorted
                ? "Re-sorted off the render thread whenever the view moves, leaving out what the camera cannot see."
                : "Drawn in the order generated: the far side paints over the near one wherever they overlap.",
        },
        new Toggle("Coarse sort", _coarseOrder, value => _coarseOrder = value)
        {
            Note = "Sixteen bits of depth rather than thirty-two: half the passes, for splats nearly as far away " +
                "as one another in either order.",
        },
        new Toggle("Device limits", _deviceLimits, value => _deviceLimits = value) { Note = DeviceNote },
        // It is the ring's pillar. Nothing to turn on or off anywhere else.
        new Toggle("Solid pillar", _pillar && _ring, value => _pillar = value) { Enabled = _ring },
        // Nothing to choose between with a capture loaded: the capture is the
        // cloud.
        new Choice("Cloud", Shapes, Shapes[_ring ? 1 : 0], _path is null ? option => _ring = option == Shapes[1] : null),
        new Choice(
            "Splats",
            [.. Counts.Select(count => count.Label)],
            Counts.FirstOrDefault(count => count.Count == _count, Counts[1]).Label,
            _path is null ? option => _count = Counts.First(count => count.Label == option).Count : null),
        // Greyed out with no capture loaded, because there is nothing for it to
        // act on: the generated clouds are packed into the compact records,
        // which carry a splat's colour and no bands at all.
        new Choice(
            "Harmonics",
            ["0", "1", "2", "3"],
            _harmonics.ToString(CultureInfo.InvariantCulture),
            _path is null ? null : option => _harmonics = int.Parse(option, CultureInfo.InvariantCulture)),
        new Setting("Opacity", _opacity, 0, 1, value => _opacity = value),
        new Setting("Brightness", _brightness, 0, 2, value => _brightness = value),
    ];

    public override string Code => """
        // What this device can carry, asked of the renderer once it is up.
        var device = renderer.GetProfile();

        // A capture from a file: the reference trainer's .ply, or a compact .splat.
        new SplatCloud(1, path: "garden.ply")
        {
            // Structure from motion puts y down. Turned the right way up here.
            Transform = Matrix4.RotationX(Math.PI),
            // How much of the capture's view-dependent colour to read: 16 bytes a
            // splat for each degree, so a small device reads less of it.
            Harmonics = device.HarmonicDegree,
            // Keeps the most opaque and largest splats, and never sends the rest to
            // the GPU at all.
            Limit = device.SplatBudget,
            // Sixteen bits of depth where a full sort would lag a turning camera.
            CoarseOrder = device.CoarseSplatOrder,
        }

        // Or a cloud made in C#, packed into the same 32-byte layout.
        var data = SplatCloud.Pack(
            positions,   // three floats a splat, metres
            scales,      // three standard deviations a splat, metres
            colours,     // RGBA, nought to one. Alpha is peak opacity
            rotations);  // quaternions, (w, x, y, z)
        new SplatCloud(1, data: data) { Revision = revision }

        // Sorted back to front whenever the camera moves, on a thread of the
        // renderer's own or a Web Worker in a browser, leaving out what the camera
        // cannot see. Drawn after the solid scene, tested against its depth, never
        // writing any.
        """;

    private int? Limit => _limit ?? (_deviceLimits ? Device?.SplatBudget : null);

    private int Harmonics => _deviceLimits && Device is { } profile ? Math.Min(_harmonics, profile.HarmonicDegree) : _harmonics;

    private bool Coarse => _coarseOrder || (_deviceLimits && (Device?.CoarseSplatOrder ?? false));

    /// <summary>What the device limits come to, in a sentence.</summary>
    private string DeviceNote
    {
        get
        {
            if (Device is not { } profile)
            {
                return "Waiting for the renderer to say what this device can do.";
            }

            var tier = profile.Tier.ToString();
            if (!_deviceLimits)
            {
                return $"{tier}-tier device, ignored: every splat, at the degree chosen below.";
            }

            var budget = profile.SplatBudget.ToString("N0", CultureInfo.InvariantCulture);
            var sort = profile.CoarseSplatOrder ? "a coarse" : "a full";
            return $"{tier}-tier device: at most {budget} splats, harmonics to degree {profile.HarmonicDegree}, {sort} sort.";
        }
    }

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -0.5, 0));
        // Wide enough that its far edge is past the horizon: the garden's own
        // ground thins out into this rather than stopping on a slab with a rim,
        // and a cloud lit for daylight wants ground under it rather than a
        // hole. Lit, it comes out the colour the far grass does. That is the
        // point of the number, and why it looks nothing like grass written down.
        transform.ScaleByDouble(160, 0.1, 160, 1);
        return new RenderObject { Key = 1, Transform = transform, Colour = Tint.Hex(0xABC960).Linear };
    }

    private static RenderObject Pillar()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0.9, 0.7, 1.2));
        transform.ScaleByDouble(0.3, 2.4, 0.3, 1);
        return new RenderObject { Key = 2, Transform = transform, Colour = Tint.Hex(0xCFC7B6).Linear };
    }

    /// <summary>Generates the cloud again if its count or shape moved, and sends it again either way.</summary>
    private void Rebuild(int? wanted)
    {
        if (_builtFor != _count || _ringFor != _ring)
        {
            _data = _ring ? SplatClouds.Ring(_count) : SplatClouds.Garden(_count);
            _builtFor = _count;
            _ringFor = _ring;
        }

        // A limit is applied as a cloud is read, so a cloud held in memory has
        // to be sent again for a new one to reach it.
        _limitFor = wanted;
        _revision++;
    }
}
