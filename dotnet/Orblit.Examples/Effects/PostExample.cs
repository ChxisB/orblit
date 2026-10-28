using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// What happens to the image after the scene is drawn.
/// </summary>
/// <remarks>
/// A renderer works in light. Its values go far above one, because the sun is
/// thousands of times brighter than a lamp and both are in the same frame. A
/// screen takes numbers between nought and one. Everything here is about that
/// journey: what glows, what is in focus, what colour the whole thing is, and
/// how the jagged edges are dealt with.
///
/// It is the difference between a scene that looks like data and one that
/// looks like it was photographed.
/// </remarks>
public sealed class PostExample : Example
{
    private static readonly IReadOnlyList<(string Label, ToneMapping ToneMapping)> ToneMaps =
    [
        ("Filmic", ToneMapping.Filmic),
        ("ACES", ToneMapping.Aces),
        ("ACES (legacy)", ToneMapping.AcesLegacy),
        ("Linear", ToneMapping.Linear),
        ("None", ToneMapping.None),
    ];

    private static readonly int[] Swatches = [0xD9634F, 0x7FB069, 0x5B8DD9];

    private PostProcess _post = new()
    {
        Bloom = new Bloom { Enabled = true, Strength = 0.25 },
        Occlusion = new Occlusion { Enabled = true },
        Grading = new Grading { Enabled = true, Contrast = 1.05 },
    };

    public override string Name => "Post-processing";

    public override string Blurb =>
        "Bloom, depth of field, occlusion, grading and anti-aliasing, over a scene bright enough to show each of them.";

    public override ExampleSection Section => ExampleSection.Effects;

    public override ViewPoint ViewPoint => new() { Distance = 16, Pitch = 0.22 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects =
        [
            // A floor to catch the contact shadows, which is what occlusion is for.
            Box(1, new Vector3(0, -1.05, 0), new Vector3(14, 0.05, 14), Tint.Hex(0x3B424C).Linear),
            // A row of boxes going away from the camera, so depth of field has
            // something at several distances to be sharp and blurred against.
            .. Enumerable.Range(0, 7).Select(i => Box(
                10 + i,
                new Vector3(Math.Sin(i * 1.1) * 2.4, 0, (i - 3) * 3.0),
                new Vector3(0.9, 1.4, 0.9),
                Tint.Hex(Swatches[i % 3]).Linear)),
            // And something far brighter than white, which is the only thing
            // that makes bloom mean anything. A lamp glows because there is
            // nowhere brighter for it to go.
            Box(100, new Vector3(3.2, 1.6, -2), new Vector3(0.5, 0.5, 0.5), new Vector3(24, 18, 9)),
        ],
        Camera = camera,
        Post = _post,
        Lights =
        [
            new RenderLight
            {
                Key = 900,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.4, -1, -0.35),
                Intensity = 68000,
            },
            new RenderLight
            {
                Key = 901,
                Kind = RenderLightKind.Point,
                Position = new Vector3(3.2, 1.6, -2),
                Intensity = 9000,
                Colour = new Vector3(1, 0.78, 0.4),
                CastShadows = false,
            },
        ],
        Sky = new Sky { Colour = new Vector3(0.10, 0.13, 0.19), Ambient = 12000 },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Post-processing", _post.Enabled, value => _post = _post with { Enabled = value }),
        new Toggle("Bloom", _post.Bloom.Enabled, value => _post = _post with { Bloom = _post.Bloom with { Enabled = value } }),
        .. _post.Bloom.Enabled ? BloomControls() : [],
        new Toggle("Depth of field", _post.DepthOfField.Enabled, value =>
            _post = _post with { DepthOfField = _post.DepthOfField with { Enabled = value } }),
        .. _post.DepthOfField.Enabled
            ? [new Setting("Focus", _post.DepthOfField.FocusDistance, 1, 40, value =>
                _post = _post with { DepthOfField = _post.DepthOfField with { FocusDistance = value } })
                { Decimals = 1, Unit = "m" }]
            : Array.Empty<ExampleSetting>(),
        new Toggle("Occlusion", _post.Occlusion.Enabled, value =>
            _post = _post with { Occlusion = _post.Occlusion with { Enabled = value } }),
        new Toggle("Vignette", _post.Vignette.Enabled, value =>
            _post = _post with { Vignette = _post.Vignette with { Enabled = value } }),
        new Choice(
            "Tone map",
            [.. ToneMaps.Select(one => one.Label)],
            ToneMaps.First(one => one.ToneMapping == _post.Grading.ToneMapping).Label,
            label => _post = _post with
            {
                Grading = _post.Grading with { ToneMapping = ToneMaps.First(one => one.Label == label).ToneMapping },
            }),
        new Setting("Exposure", _post.Grading.Exposure, -3, 3, value =>
            _post = _post with { Grading = _post.Grading with { Enabled = true, Exposure = value } })
            { Decimals = 1 },
        new Setting("Saturation", _post.Grading.Saturation, 0, 2, value =>
            _post = _post with { Grading = _post.Grading with { Enabled = true, Saturation = value } }),
    ];

    public override string Code => """
        // Everything after the scene is drawn, on the scene rather than the camera.
        // A look belongs to the place, not to where somebody is standing in it.
        new RenderScene
        {
            Objects = objects,
            Camera = camera,
            Post = new PostProcess
            {
                Bloom = new Bloom { Enabled = true, Strength = 0.25, LensFlare = true },
                Occlusion = new Occlusion { Enabled = true },
                DepthOfField = new DepthOfField { Enabled = true, FocusDistance = 12 },
                Grading = new Grading
                {
                    Enabled = true,
                    ToneMapping = ToneMapping.Aces,
                    Exposure = 0.4,
                    Saturation = 1.1,
                },
            },
        }
        """;

    private static RenderObject Box(long key, Vector3 at, Vector3 size, Vector3 colour)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(at);
        transform.ScaleByDouble(size.X, size.Y, size.Z, 1);
        return new RenderObject { Key = key, Transform = transform, Colour = colour };
    }

    private IReadOnlyList<ExampleSetting> BloomControls() =>
    [
        new Setting("Strength", _post.Bloom.Strength, 0, 1, value =>
            _post = _post with { Bloom = _post.Bloom with { Strength = value } }),
        new Toggle("Lens flare", _post.Bloom.LensFlare, value =>
            _post = _post with { Bloom = _post.Bloom with { LensFlare = value } }),
    ];
}
