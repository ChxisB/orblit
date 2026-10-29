using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A moving picture, in the world.
/// </summary>
/// <remarks>
/// The frame never becomes a texture in the ordinary sense. It stays the buffer
/// the decoder wrote and is handed to the GPU where it lies. That is the
/// difference between a screen that costs an upload every frame and one that
/// costs about as much as a flat colour.
///
/// It is also why the same film on four screens is one decoder and not four. A
/// video is listed on the scene, and a material points at it.
/// </remarks>
public sealed class VideoExample : Example
{
    private static readonly IReadOnlyList<string> Jumps = ["0s", "2s", "4s"];

    private readonly Func<string, bool> _exists;

    private string? _path;

    /// <summary>What is in the path field, which is not yet a film until it names a file.</summary>
    private string _typed;

    private bool _playing = true;
    private bool _loop = true;
    private double _rate = 1.0;
    private double _fade = 1.0;
    private long _seekToken;
    private double? _seekTo;

    /// <summary>
    /// Takes a film from <c>ORBLIT_VIDEO</c>, for a screenshot or a test, when
    /// <paramref name="exists"/> finds it. The gallery passes the process
    /// environment and the file system. A test passes tables.
    /// </summary>
    public VideoExample(Func<string, string?> environment, Func<string, bool> exists)
    {
        _exists = exists;
        if (environment("ORBLIT_VIDEO") is { } given && exists(given))
        {
            _path = given;
        }

        _typed = _path ?? "";
    }

    public override string Name => "Video";

    public override ExampleSection Section => ExampleSection.Content;

    public override string Blurb =>
        "A film on four screens from one decoder, tinted, faded and played backwards. Set ORBLIT_VIDEO or paste a path.";

    public override ViewPoint ViewPoint => new() { Distance = 14, Pitch = 0.14, Yaw = 0.0 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [Floor(), .. Enumerable.Range(0, 4).Select(Screen)],
        Materials =
        [
            // The room, so a screen has somewhere to be and something to light.
            new RenderMaterial { Key = 1, BaseColour = new Vector4(0.16, 0.17, 0.20, 1), Roughness = 0.9 },
            .. Enumerable.Range(0, 4).Select(ScreenMaterial),
        ],
        Videos = _path is { } showing
            ?
            [
                new Video
                {
                    Key = 1,
                    Path = showing,
                    Playing = _playing,
                    Loop = _loop,
                    Rate = _rate,
                    Volume = 0,
                    SeekTo = _seekTo,
                    SeekToken = _seekToken,
                },
            ]
            : [],
        Camera = camera,
        Lights =
        [
            new RenderLight
            {
                Key = 900,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.3, -1, -0.5),
                Intensity = 24000,
            },
        ],
        Sky = new Sky { Colour = new Vector3(0.05, 0.06, 0.08), Ambient = 6000 },
        Post = new PostProcess { Bloom = new Bloom { Enabled = true, Strength = 0.14 } },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new TextEntry("Or a path", _typed, Type) { Hint = "/path/to/a.mp4" },
        new Toggle("Playing", _playing, value => _playing = value),
        new Toggle("Loop", _loop, value => _loop = value),
        new Setting("Rate", _rate, -2, 3, value => _rate = value),
        new Setting("Fade", _fade, 0, 1, value => _fade = value),
        new Buttons("", Jumps, Jump),
    ];

    public override string Code => """
        // The film is on the scene, not on the material. One decoder, however many
        // screens are showing it.
        new RenderScene
        {
            Videos = [new Video { Key = 1, Path = "/path/to/a.mp4", Playing = true, Loop = true }],
            Materials =
            [
                // A screen: unlit, because it makes its own light, and the base colour
                // tints the frame rather than replacing it.
                new RenderMaterial
                {
                    Key = 10,
                    Shading = MaterialShading.Video,
                    Video = 1,
                    BaseColour = new Vector4(1, 1, 1, 1),
                },
            ],
            Objects = [new RenderObject { Key = 10, Material = 10, Transform = placement, Colour = white }],
            Camera = camera,
        };

        // Seeking is an event and the scene is a description, so a token reconciles
        // them: the renderer jumps when the token moves, not when the target does.
        new Video { Key = 1, Path = path, SeekTo = 12.0, SeekToken = ++token };
        """;

    /// <summary>
    /// Checked here rather than left to the decoder, which reports nothing back
    /// and simply shows no frames.
    /// </summary>
    private void Type(string value)
    {
        _typed = value;
        var wanted = value.Trim();
        if (wanted.Length > 0 && !_exists(wanted))
        {
            Note = "There is no file at that path.";
            return;
        }

        _path = wanted.Length == 0 ? null : wanted;
        Note = null;
    }

    /// <summary>
    /// A jump is an event and the scene is a description, so the two are
    /// reconciled by a token. The renderer acts when this moves, not when the
    /// target does.
    /// </summary>
    private void Jump(string option)
    {
        _seekTo = double.Parse(option.TrimEnd('s'), System.Globalization.CultureInfo.InvariantCulture);
        _seekToken++;
    }

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -2.6, 0));
        transform.ScaleByDouble(14, 0.05, 10, 1);
        return new RenderObject { Key = 1, Material = 1, Transform = transform, Colour = Tint.Hex(0x2A2E35).Linear };
    }

    /// <summary>
    /// Sixteen by nine, and thin. A screen is a plane with a back to it, which
    /// is why the material is not double-sided and the box has depth instead.
    /// </summary>
    private static RenderObject Screen(int index)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3((index - 1.5) * 4.0, 0, 0));
        transform.ScaleByDouble(1.78, 1.0, 0.06, 1);
        return new RenderObject { Key = 10 + index, Material = 10 + index, Transform = transform, Colour = new Vector3(1, 1, 1) };
    }

    /// <summary>
    /// Four screens, all pointing at the same film. The tints are the point. The
    /// base colour multiplies the frame, so a screen can be dimmed, washed or
    /// faded without the decoder knowing anything about it.
    /// </summary>
    private RenderMaterial ScreenMaterial(int index) => new()
    {
        Key = 10 + index,
        Shading = MaterialShading.Video,
        Video = 1,
        Blend = index == 3 ? MaterialBlend.Fade : MaterialBlend.Opaque,
        BaseColour = index switch
        {
            0 => new Vector4(1, 1, 1, 1),
            1 => new Vector4(1.0, 0.55, 0.35, 1),
            2 => new Vector4(0.45, 0.70, 1.0, 1),
            _ => new Vector4(1, 1, 1, _fade),
        },
    };
}
