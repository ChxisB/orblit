using Orblit.Camera;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Cameras as shots, and the rules they frame by drawn over the frame.
/// </summary>
/// <remarks>
/// There is one real camera. These are descriptions of what it should be
/// doing, and the brain picks between them: cutting to a different angle is
/// raising a number, not moving anything. Nothing outside the camera library
/// touches a transform, which is what stops two systems fighting over it.
/// That is the failure that makes cameras the worst part of most codebases.
/// </remarks>
public sealed class CamerasExample : Example
{
    private static readonly string[] Close = ["Chase", "First person", "Flat"];
    private static readonly string[] Far = ["Watchtower", "Orbit"];

    /// <summary>What everything is looking at. Moved along its own path each frame.</summary>
    private readonly FixedTarget _subject = new(Vector3.Zero());

    private readonly ComposerAim _chaseAim = new(
        screenY: 0.45,
        deadZoneWidth: 0.08,
        deadZoneHeight: 0.10,
        softZoneWidth: 0.35,
        softZoneHeight: 0.30,
        damping: 0.4);

    private readonly CameraBrain _brain = new(blends: new BlendTable(new Blend(BlendStyle.EaseInOut, 0.9)));
    private readonly VirtualCamera[] _cameras;

    private string _live = "Chase";
    private double _blendSeconds = 0.9;
    private BlendStyle _blendStyle = BlendStyle.EaseInOut;
    private bool _showGuides = true;
    private double _lastSeconds;

    public CamerasExample()
    {
        _cameras = [Chase(), Eyes(), Flat(), Watchtower(), Orbit()];
        foreach (var camera in _cameras)
        {
            _brain.Add(camera);
        }

        _brain.Snap();
    }

    public override string Name => "Virtual cameras";

    public override string Blurb =>
        "Third person, first person and flat, blending between them, with the framing drawn on top.";

    public override ExampleSection Section => ExampleSection.Basics;

    public override ViewPoint ViewPoint => new() { Distance = 22, Pitch = 0.4 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var delta = Math.Clamp(seconds - _lastSeconds, 0.0, 0.1);
        _lastSeconds = seconds;

        // The subject moves, and its heading is where it is going. That is what
        // a chase camera binds to.
        var at = PathAt(seconds);
        _subject.Position = at;
        _subject.Rotation = Rotations.LookRotation(PathAt(seconds + 0.12) - at);

        _brain.Blends.DefaultBlend = new Blend(_blendStyle, _blendSeconds);
        foreach (var shot in _cameras)
        {
            shot.Priority = shot.Name == _live ? 20 : 10;
        }

        _brain.Update(delta);

        return new RenderScene
        {
            Objects =
            [
                new RenderObject
                {
                    Key = 700,
                    Transform = Matrix4.Compose(at + new Vector3(0, 0.8, 0), _subject.Rotation, new Vector3(0.5, 0.8, 0.9)),
                    Colour = Tint.Hex(0xD9633B).Linear,
                },
                .. Enumerable.Range(0, 14).Select(Prop),
                Floor(),
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 760,
                    Kind = RenderLightKind.Directional,
                    Intensity = 76000,
                    Direction = new Vector3(-0.4, -0.9, -0.35).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                    SunAngularRadius = 0.6,
                },
            ],
            Sky = Sky,
            // The brain's answer, not the orbit camera the other examples use.
            // That is the whole point: nothing here moves the camera by hand.
            Camera = AsRenderCamera(_brain.State),
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("Live", Close, Close.Contains(_live) ? _live : "", option => _live = option),
        new Choice("", Far, Far.Contains(_live) ? _live : "", option => _live = option),
        new Choice("Blend", ["Cut", "Linear", "Ease"], BlendName(_blendStyle), option => _blendStyle = option switch
        {
            "Cut" => BlendStyle.Cut,
            "Linear" => BlendStyle.Linear,
            _ => BlendStyle.EaseInOut,
        }),
        .. _blendStyle == BlendStyle.Cut
            ? Array.Empty<ExampleSetting>()
            : [new Setting("Blend time", _blendSeconds, 0.1, 3, value => _blendSeconds = value) { Unit = " s" }],
        new Toggle("Show framing", _showGuides, value => _showGuides = value),
        new Setting("Dead zone", _chaseAim.DeadZoneWidth, 0, 0.4, value =>
        {
            _chaseAim.DeadZoneWidth = value;
            _chaseAim.DeadZoneHeight = value * 1.2;
        }),
        new Setting("Soft zone", _chaseAim.SoftZoneWidth, 0.05, 0.5, value =>
        {
            _chaseAim.SoftZoneWidth = value;
            _chaseAim.SoftZoneHeight = value * 0.85;
        }),
        new Setting("Aim damping", _chaseAim.Damping, 0, 2, value => _chaseAim.Damping = value) { Unit = " s" },
    ];

    public override string Code => """
        // One real camera, and as many shots as the scene has situations. Cutting to
        // a different angle is raising a number. Nothing outside the library moves a
        // transform, which is what stops two systems fighting over the camera.
        var subject = new FixedTarget(Vector3.Zero());

        var chase = new VirtualCamera(
            "Chase",
            priority: 20,
            follow: subject,
            lookAt: subject,
            // Behind and above in the subject's own frame, so it stays behind when the
            // subject turns rather than staying north of it. Per axis, because a camera
            // may lag a long way behind and must not float up and down.
            body: new FollowBody(new Vector3(0, 2.4, 7), damping: new Vector3(0.35, 0.18, 0.5)),
            aim: new ComposerAim(
                screenY: 0.45,
                // Inside this, the camera holds still. A camera that corrects for every
                // twitch reads as a nervous operator rather than a steady one.
                deadZoneWidth: 0.08,
                deadZoneHeight: 0.10,
                // Between the two it eases after the subject. Past the soft edge it is
                // dragged, because by then keeping them in frame matters more.
                softZoneWidth: 0.35,
                softZoneHeight: 0.30,
                damping: 0.4));

        var brain = new CameraBrain(blends: new BlendTable(new Blend(BlendStyle.EaseInOut, 0.9)));
        brain.Add(chase);
        brain.Add(watchtower);
        brain.Add(orbit);
        brain.Snap();

        // Every frame: move the world, then ask what the camera should be doing.
        brain.Update(delta);

        new RenderScene
        {
            Camera = new RenderCamera
            {
                Position = brain.State.Position,
                Target = brain.State.Position + brain.State.Forward,
                FieldOfView = brain.State.Lens.FieldOfView,
            },
            // ...
        };

        // And to show the rules rather than guess at them:
        var guides = brain.Live?.Guides;   // dead and soft, in fractions
        """;

    private static Sky Sky => new()
    {
        Colour = Tint.Hex(0x6E8DB4).Linear,
        Zenith = Tint.Hex(0x2F5F97).Linear,
        Horizon = Tint.Hex(0xB9CBDD).Linear,
        Ambient = 24000,
        BodyDirection = new Vector3(0.4, 0.9, 0.35).Normalized(),
        BodyColour = Tint.Hex(0xFFF6E8).Linear,
        BodySize = 0.011,
        Clouds = Clouds.Cumulus with { Cover = 0.28, Wind = new Vector2(4, 1.5) },
    };

    /// <summary>
    /// Where the subject is at this moment, on a figure of eight. A path rather
    /// than a straight line, because a camera that never has to turn round shows
    /// nothing about how it turns round.
    /// </summary>
    private static Vector3 PathAt(double seconds)
    {
        var t = seconds * 0.45;
        return new Vector3(Math.Sin(t) * 9, 0, Math.Sin(t * 2) * 5.5);
    }

    /// <summary>The brain's state, as the renderer takes a camera.</summary>
    private static RenderCamera AsRenderCamera(CameraState state) => new()
    {
        Position = state.Position,
        // A renderer wants somewhere to look and a camera has a rotation. One
        // metre along its own forward is the same thing said the other way.
        Target = state.Position + state.Forward,
        FieldOfView = state.Lens.FieldOfView,
        Orthographic = state.Lens.Orthographic,
        ViewHeight = state.Lens.Height,
    };

    private static string BlendName(BlendStyle style) => style switch
    {
        BlendStyle.Cut => "Cut",
        BlendStyle.Linear => "Linear",
        _ => "Ease",
    };

    /// <summary>Something to move past, so the motion reads as motion.</summary>
    private static RenderObject Prop(int index)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(
            Math.Cos(index * 0.9) * (7 + ((index % 4) * 3.5)),
            0.9 + ((index % 3) * 0.7),
            Math.Sin(index * 1.7) * (7 + ((index % 5) * 2.5))));
        transform.RotateY(index * 0.7);
        transform.ScaleByDouble(0.7, 0.9 + ((index % 3) * 0.7), 0.7, 1);
        return new RenderObject
        {
            Key = 710 + index,
            Transform = transform,
            Colour = Tint.Lerp(Tint.Hex(0x5B6470), Tint.Hex(0x8B94A0), (index % 5) / 4.0).Linear,
        };
    }

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -0.1, 0));
        transform.ScaleByDouble(40.0, 0.06, 40.0, 1);
        return new RenderObject
        {
            Key = 690,
            Transform = transform,
            Colour = Tint.Hex(0x3C424A).Linear,
            CastShadows = false,
        };
    }

    private VirtualCamera Chase() => new(
        "Chase",
        priority: 20,
        follow: _subject,
        lookAt: _subject,
        // Behind and above, in the subject's own frame, so it stays behind when
        // the subject turns rather than staying north of it. Per axis, because
        // the axes want different answers: a camera may lag a long way behind
        // and must not float up and down.
        body: new FollowBody(new Vector3(0, 2.4, 7), damping: new Vector3(0.35, 0.18, 0.5)),
        aim: _chaseAim,
        lens: new Lens(FieldOfView: 55));

    private VirtualCamera Watchtower() => new(
        "Watchtower",
        lookAt: _subject,
        body: new StaticBody(new Vector3(-16, 9, 16)),
        aim: new ComposerAim(
            screenX: 0.35,
            screenY: 0.4,
            deadZoneWidth: 0.05,
            deadZoneHeight: 0.06,
            softZoneWidth: 0.30,
            softZoneHeight: 0.25,
            damping: 0.8),
        lens: new Lens(FieldOfView: 38));

    /// <summary>
    /// First person: on the subject's own head, looking where it looks. Nothing
    /// composes, damps or frames. Any of those would put the view somewhere other
    /// than where the character is looking, which a first-person camera may never do.
    /// </summary>
    private VirtualCamera Eyes() => new(
        "First person",
        follow: _subject,
        lookAt: _subject,
        body: new FollowBody(new Vector3(0, 0.75, -0.2), FollowBinding.TargetRotation, Vector3.Zero()),
        aim: new HeadAim(),
        lens: new Lens(FieldOfView: 70));

    /// <summary>
    /// A game seen flat on, from above. Framed by moving, because turning an
    /// orthographic view does not move anything through the frame.
    /// </summary>
    private VirtualCamera Flat() => new(
        "Flat",
        follow: _subject,
        lookAt: _subject,
        body: new ScreenFollowBody(
            distance: 40,
            screenY: 0.55,
            deadZoneWidth: 0.12,
            deadZoneHeight: 0.16,
            softZoneWidth: 0.38,
            softZoneHeight: 0.35,
            damping: 0.35,
            // A level has edges, and a camera that follows a character over one
            // shows whatever is past it.
            bounds: (new Vector3(-6, 30, -6), new Vector3(6, 50, 22))),
        // Looking straight down, level with the world's own axes.
        aim: new StaticAim(Rotations.LookRotation(new Vector3(0, -1, -0.0001))),
        lens: Lens.Flat(26));

    private VirtualCamera Orbit() => new(
        "Orbit",
        follow: _subject,
        lookAt: _subject,
        body: new OrbitBody(radius: 11, elevation: 26, damping: 0.5),
        aim: new HardLookAt(),
        lens: new Lens(FieldOfView: 48));
}
