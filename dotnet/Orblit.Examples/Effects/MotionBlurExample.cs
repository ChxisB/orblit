using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// What the shutter saw while it was open.
/// </summary>
/// <remarks>
/// A renderer draws instants, and an instant of a spinning fan is a set of
/// perfectly sharp blades. That is the most reliable sign there is that a
/// moving picture was computed rather than photographed. A camera's shutter is
/// open for a length of time, and whatever moves while it is open is recorded
/// all along its path.
///
/// Three things move here in three different ways. The fan turns in place, so
/// its tips smear and its hub barely does. The block slides, so it smears along
/// its path by the same amount everywhere. And the camera can pan, which smears
/// everything, the still wall included. That case needs no object to have
/// moved at all, and is rebuilt from depth alone. The blue box never moves, and
/// stays sharp under a still camera whatever the shutter.
///
/// The shutter is the camera's own, with the sensitivity moved against it so
/// the picture stays equally bright. Slowing the shutter lets in more light,
/// and the blur is what comes with it.
/// </remarks>
public sealed class MotionBlurExample : Example
{
    private static readonly IReadOnlyList<(string Label, double Seconds)> Shutters =
    [
        ("1/1000 s", 1 / 1000.0),
        ("1/250 s", 1 / 250.0),
        ("1/60 s", 1 / 60.0),
        ("1/30 s", 1 / 30.0),
    ];

    // How fast things go. Each is steady between turnarounds, so a picture
    // taken at any moment has a streak whose length can be worked out by hand:
    // speed across the picture times the time the shutter is open.
    private const double TurnsPerSecond = 1;
    private const double SlideSpeed = 4; // metres a second
    private const double PanSpeed = 0.25; // radians a second

    private const long FloorKey = 1;
    private const long WallKey = 2;
    private const long StripesKey = 3; // and the eleven after it
    private const long StillKey = 20;
    private const long HubKey = 30;
    private const long BladesKey = 31; // and the three after it
    private const long BlockKey = 40;

    private bool _on = true;
    private bool _objects = true;
    private bool _panning;

    /// <summary>The camera's shutter, in seconds.</summary>
    private double _shutter = 1 / 30.0;

    public override string Name => "Motion blur";

    public override string Blurb =>
        "A spinning fan and a sliding block, blurred by their own motion while the wall behind them stays sharp. You pick the shutter.";

    public override ExampleSection Section => ExampleSection.Effects;

    public override ViewPoint ViewPoint => new() { Yaw = 0, Pitch = 0, Distance = 10, Height = 1.5 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var spin = seconds * TurnsPerSecond * 2 * Math.PI;
        // Ten metres there and back, a turnaround every two and a half seconds.
        var slid = -5 + (10 * Triangle(seconds * SlideSpeed / 20));
        var hub = new Vector3(2.8, 2.8, 0);
        return new RenderScene
        {
            Camera = Eye(camera, seconds),
            Objects =
            [
                Box(FloorKey, At(0, -1.75, -1, new Vector3(9, 0.1, 5)), 0x8F8A80),
                Box(WallKey, At(0, 1.5, -4, new Vector3(9, 4, 0.1)), 0xE8E4DC),
                // Dark bars along the top of the wall. A pan has nothing to show
                // on a plain wall, and these are what it streaks.
                .. Enumerable.Range(0, 12).Select(i =>
                    Box(StripesKey + i, At(-7.15 + (i * 1.3), 3.9, -3.85, new Vector3(0.18, 1.2, 0.05)), 0x2B2F36)),
                // The witness. It never moves, so under a still camera it is the
                // same picture with the blur on and off.
                Box(StillKey, At(-3.5, 1.2, 0, new Vector3(0.6, 0.6, 0.6)), 0x2F6DBF),
                Box(HubKey, Hub(hub, spin), 0x3A3F48),
                .. Enumerable.Range(0, 4).Select(blade =>
                    Box(BladesKey + blade, Blade(hub, spin + (blade * Math.PI / 2)), 0xC7332B)),
                // A plate rather than a cube, sliding in front of the plain wall:
                // two clean edges against one flat colour, so its streak can be
                // measured off the picture rather than guessed at.
                Box(BlockKey, At(slid, 0, 0, new Vector3(0.45, 0.45, 0.05)), 0xE0A526),
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    Direction = new Vector3(-0.3, -1, -0.5).Normalized(),
                    Colour = new Vector3(1, 0.97, 0.92),
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
            Graph = _on ? new MotionBlur { Objects = _objects }.Graph() : RenderGraph.Standard(),
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Motion blur", _on, value => _on = value),
        new Toggle("Objects", _objects, value => _objects = value)
        {
            Enabled = _on,
            Note = _objects ? "Each object blurs by its own motion" : "Only the camera's motion, rebuilt from depth",
        },
        new Toggle("Pan", _panning, value => _panning = value),
        new Choice(
            "Shutter",
            [.. Shutters.Select(shutter => shutter.Label)],
            Shutters.FirstOrDefault(shutter => Math.Abs(shutter.Seconds - _shutter) < 1e-9, Shutters[^1]).Label,
            label => _shutter = Shutters.First(shutter => shutter.Label == label).Seconds),
    ];

    public override string Code => """
        // The world into a target that keeps its depth, and the blur from there onto
        // the screen. The shutter follows the camera's own unless one is given.
        new RenderScene
        {
            Camera = camera with { ShutterSpeed = 1 / 30.0 },
            Graph = new MotionBlur().Graph(),
            // ...
        }

        // Or as one pass in a graph of your own, reading a target with depth.
        new MotionBlur { MaxPixels = 32, Objects = false }.Pass(reads: "frame", into: "blurred")
        """;

    /// <summary>Nought to one and back at a steady rate, turning round every half unit.</summary>
    private static double Triangle(double u)
    {
        var f = u - Math.Floor(u);
        return 1 - Math.Abs((2 * f) - 1);
    }

    private static RenderObject Box(long key, Matrix4 transform, int rgb) =>
        new() { Key = key, Transform = transform, Colour = Tint.Hex(rgb).Linear, CastShadows = true };

    /// <summary>
    /// The built-in cube spans minus one to one, so a box is placed by its
    /// centre and its half-extents.
    /// </summary>
    private static Matrix4 At(double x, double y, double z, Vector3 half)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(x, y, z));
        transform.ScaleByDouble(half.X, half.Y, half.Z, 1);
        return transform;
    }

    private static Matrix4 Hub(Vector3 hub, double angle)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(hub);
        transform.RotateZ(angle);
        transform.ScaleByDouble(0.22, 0.22, 0.12, 1);
        return transform;
    }

    /// <summary>A blade turned about the hub, with its inner end at the hub.</summary>
    private static Matrix4 Blade(Vector3 hub, double angle)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(hub);
        transform.RotateZ(angle);
        transform.TranslateByDouble(0.9, 0, 0, 1);
        transform.ScaleByDouble(0.9, 0.1, 0.04, 1);
        return transform;
    }

    private RenderCamera Eye(RenderCamera camera, double seconds)
    {
        var eye = camera;
        if (_panning)
        {
            // A pan turns the camera where it stands rather than walking it
            // round the subject, so everything in the picture moves at the same
            // rate whatever its distance: a quarter radian either way, turning
            // round every two seconds.
            var angle = PanSpeed * ((2 * Triangle(seconds / 4)) - 1);
            var look = Matrix3.RotationY(angle).Transformed(camera.Target - camera.Position);
            eye = camera with { Target = camera.Position + look };
        }
        // The sensitivity moved against the shutter, so a slower shutter lets
        // in the same light and brings only its blur.
        return eye with { ShutterSpeed = _shutter, Sensitivity = 100 * (1 / 125.0) / _shutter };
    }
}
