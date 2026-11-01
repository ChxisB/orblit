using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// How much of the frame's work actually happens.
/// </summary>
/// <remarks>
/// One pipeline, not a choice of them. Every frame goes the same way: shadow
/// maps, depth, opaque, sky, see-through, then the image work. What these
/// controls change is how much of each step there is. The four named settings
/// are settings of the same dials, which is the point. Nothing appears or
/// disappears between them, so a scene is authored once and checked once.
///
/// The pillars are here to be shadowed. Cascades and map size are the two
/// settings that show most. The way to see them is to look at the shadow edge
/// near the camera and then at the same edge far away.
/// </remarks>
public sealed class PipelineExample : Example
{
    private Detail _detail = Detail.Medium;
    private Pipeline _pipeline = Pipeline.At(Detail.Medium);

    /// <summary>
    /// Reads <c>ORBLIT_DETAIL</c> from <paramref name="environment"/>, which the
    /// gallery passes as the process environment and a test passes as a table.
    /// </summary>
    public PipelineExample(Func<string, string?> environment)
    {
        // So the four settings can be compared from outside, one frame each,
        // without anybody having to click through them.
        var wanted = environment("ORBLIT_DETAIL");
        foreach (var one in Enum.GetValues<Detail>())
        {
            if (string.Equals(one.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
            {
                Retier(one);
            }
        }
    }

    public override string Name => "Pipeline";

    public override string Blurb =>
        "Shadows, cascades, multisampling and render scale: the four named settings, and every dial behind them.";

    public override ExampleSection Section => ExampleSection.Performance;

    public override ViewPoint ViewPoint => new() { Distance = 22, Pitch = 0.12, Yaw = 0.55, Height = 1.5 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects =
        [
            Box(1, Scaled(new Vector3(0, -1.5, 0), new Vector3(200, 0.05, 200)), 0x57606B, castShadows: false),
            // A colonnade going away from the camera, so one shadow edge is a
            // metre off and the next is fifty. That distance is what cascades
            // are for, and it is the only way to see what they do.
            .. from i in Enumerable.Range(0, 14)
               from side in new[] { -1.0, 1.0 }
               select Box(
                   100 + (i * 2) + (side > 0 ? 1 : 0),
                   Scaled(new Vector3(side * 3.2, 0.6, (i * 4.0) - 6), new Vector3(0.45, 2.1, 0.45)),
                   0xD5CFC4),
            // Something with a fine edge close to the camera, because
            // multisampling shows on a thin diagonal and nowhere else.
            .. Enumerable.Range(0, 5).Select(i => Box(300 + i, Bar(i), 0xE0B252)),
        ],
        Camera = camera,
        Pipeline = _pipeline,
        Lights =
        [
            new RenderLight
            {
                Key = 900,
                Kind = RenderLightKind.Directional,
                // Low and across, so the shadows are long and the far ones are
                // where the resolution actually runs out.
                Direction = new Vector3(-0.55, -0.42, -0.72).Normalized(),
                Intensity = 95000,
                SourceRadius = 0.6,
            },
        ],
        Sky = new Sky { Colour = new Vector3(0.24, 0.33, 0.46), Ambient = 22000 },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("Detail", Enum.GetNames<Detail>(), _detail.ToString(), label => Retier(Enum.Parse<Detail>(label))),
        new Toggle("Shadows", _pipeline.Shadows.Enabled, value => Shadows(shadows => shadows with { Enabled = value })),
        .. _pipeline.Shadows.Enabled ? ShadowControls() : [],
        new Setting("Multisampling", Math.Log(_pipeline.Samples) / Math.Log(2), 0, 3, value =>
            _pipeline = _pipeline with { Samples = (int)Math.Pow(2, Math.Round(value, MidpointRounding.AwayFromZero)) })
            { Decimals = 0 },
        new Setting("Render scale", _pipeline.Resolution.Scale, 0.35, 1, value =>
            _pipeline = _pipeline with { Resolution = _pipeline.Resolution with { Scale = value, Adaptive = false } })
            { Decimals = 2 },
        new Toggle("Let it shrink to keep up", _pipeline.Resolution.Adaptive, value =>
            _pipeline = _pipeline with { Resolution = _pipeline.Resolution with { Adaptive = value } }),
    ];

    public override string Code => """
        // One pipeline. The four named settings are settings of its dials, not
        // different pipelines. Nothing appears or disappears between them.
        var pipeline = Pipeline.At(Detail.High);

        // Or every dial by hand.
        var mine = new Pipeline
        {
            Shadows = new ShadowSettings
            {
                Kind = ShadowKind.Soft,
                MapSize = 2048,
                // The single most effective shadow setting there is. One map over a
                // hundred metres puts a centimetre in each pixel. Four cascades over
                // the same hundred metres puts a millimetre in the first.
                Cascades = 3,
                Distance = 80,
                Contact = true,
            },
            // Runs while the frame is drawn, unlike the anti-aliasing in post, which
            // runs on the finished image. Sharper, and costs bandwidth on everything.
            Samples = 4,
            // A frame that arrives on time slightly soft beats one that arrives late
            // sharp.
            Resolution = new Resolution { Adaptive = true, MinScale = 0.6 },
        };

        new RenderScene { Objects = objects, Camera = camera, Pipeline = pipeline }
        """;

    private static RenderObject Box(long key, Matrix4 transform, int rgb, bool castShadows = true) =>
        new() { Key = key, Transform = transform, Colour = Tint.Hex(rgb).Linear, CastShadows = castShadows };

    private static Matrix4 Scaled(Vector3 at, Vector3 size)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(at);
        transform.Multiply(Matrix4.Diagonal3Values(size.X, size.Y, size.Z));
        return transform;
    }

    private static Matrix4 Bar(int i)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(-1.6 + (i * 0.8), -0.4, 4));
        transform.Multiply(Matrix4.RotationZ(0.3 + (i * 0.12)));
        transform.Multiply(Matrix4.Diagonal3Values(0.06, 1.6, 0.06));
        return transform;
    }

    private void Retier(Detail chosen)
    {
        _detail = chosen;
        _pipeline = Pipeline.At(chosen);
    }

    private void Shadows(Func<ShadowSettings, ShadowSettings> change) =>
        _pipeline = _pipeline with { Shadows = change(_pipeline.Shadows) };

    private IReadOnlyList<ExampleSetting> ShadowControls() =>
    [
        new Choice("Edge", Enum.GetNames<ShadowKind>(), _pipeline.Shadows.Kind.ToString(), label =>
            Shadows(shadows => shadows with { Kind = Enum.Parse<ShadowKind>(label) })),
        new Setting("Map size", Math.Log(_pipeline.Shadows.MapSize / 256.0) / Math.Log(2), 0, 4, value =>
            Shadows(shadows => shadows with
            {
                MapSize = 256 * (int)Math.Pow(2, Math.Round(value, MidpointRounding.AwayFromZero)),
            }))
            { Decimals = 0 },
        new Setting("Cascades", _pipeline.Shadows.Cascades, 1, 4, value =>
            Shadows(shadows => shadows with { Cascades = (int)Math.Round(value, MidpointRounding.AwayFromZero) }))
            { Decimals = 0 },
        new Setting("Distance", _pipeline.Shadows.Distance, 0, 200, value =>
            Shadows(shadows => shadows with { Distance = value }))
            { Decimals = 0, Unit = "m" },
        new Setting("Spread", _pipeline.Shadows.Lambda, 0, 1, value => Shadows(shadows => shadows with { Lambda = value }))
            { Decimals = 2 },
        new Toggle("Contact shadows", _pipeline.Shadows.Contact, value =>
            Shadows(shadows => shadows with { Contact = value })),
        new Toggle("Steady edges", _pipeline.Shadows.Stable, value => Shadows(shadows => shadows with { Stable = value })),
    ];
}
