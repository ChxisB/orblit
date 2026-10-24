using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// The arrays a scene packs, byte for byte against an independent run of the original packer.
/// The fixture holds more keys than a scene core sends. Keys the port does not send yet are
/// skipped here and join as their parts land.
/// </summary>
public class SceneFixtureTests
{
    private static readonly Fixture Fixture = Fixture.Load("orblit_filament", "scene");

    private static RenderCamera Camera() => SceneBuilders.CameraAt(0, 2, 8);

    private static RenderObject Object(
        long key,
        string? mesh = null,
        Matrix4? transform = null,
        Vector3? colour = null,
        bool castShadows = true,
        bool receiveShadows = true,
        bool visible = true,
        int layer = 0,
        double[]? morphWeights = null,
        ModelAnimation? animation = null,
        int? variant = null,
        JointPose[]? joints = null,
        long? material = null) =>
        new()
        {
            Key = key,
            Mesh = mesh,
            Transform = transform ?? Matrix4.Identity(),
            Colour = colour ?? new Vector3(1, 1, 1),
            CastShadows = castShadows,
            ReceiveShadows = receiveShadows,
            Visible = visible,
            Layer = layer,
            MorphWeights = morphWeights,
            Animation = animation,
            Variant = variant,
            Joints = joints,
            Material = material,
        };

    private static Matrix4 Moved()
    {
        var matrix = Matrix4.TranslationValues(1, 2, 3);
        matrix.Multiply(Matrix4.RotationY(0.7));
        return matrix;
    }

    private static RenderScene Scene(params RenderObject[] objects) =>
        new() { Camera = Camera(), Objects = objects };

    private static Dictionary<string, (RenderScene Scene, double? At)> Scenes() => new()
    {
        ["objects"] = (
            Scene(
                Object(1, "/m/a.glb"),
                Object(
                    2,
                    "/m/a.glb",
                    Moved(),
                    new Vector3(0.1, 0.25, 0.9),
                    castShadows: false,
                    receiveShadows: false,
                    layer: 3),
                Object(3, colour: new Vector3(2, -1, 1.0 / 3), visible: false, layer: 99, morphWeights: [0.25, 0.5]),
                Object(4, "/m/b.glb", morphWeights: []),
                Object(5, "/m/c.glb", receiveShadows: false, morphWeights: [1])),
            null),
        ["poses"] = (
            Scene(
                Object(1, "/m/1.glb"),
                Object(7, "/m/7.glb", animation: new ModelAnimation { Clip = 2, Seconds = 1.5, Speed = 0.5, Fade = 0.25 }),
                Object(
                    3,
                    "/m/3.glb",
                    animation: new ModelAnimation
                    {
                        Clip = 1,
                        Seconds = 0.25,
                        Speed = 2,
                        Loop = false,
                        From = new ModelAnimation { Clip = 0, Seconds = 3, Speed = 1, Loop = false },
                        Fade = 0.5,
                    }),
                Object(2, "/m/2.glb", variant: 3),
                Object(
                    5,
                    "/m/5.glb",
                    joints:
                    [
                        new JointPose(0, 6, Matrix4.TranslationValues(1, 2, 3)),
                        new JointPose(1, 2, Matrix4.RotationZ(0.3)),
                    ]),
                Object(6, "/m/6.glb", joints: [])),
            12.5),
        ["lights"] = (
            Scene() with
            {
                Lights =
                [
                    new RenderLight
                    {
                        Key = 20,
                        Kind = RenderLightKind.Directional,
                        Intensity = 100000,
                        Colour = new Vector3(1, 0.9, 0.8),
                        Direction = new Vector3(0.2, -1, 0.1),
                        SunAngularRadius = 0.3,
                        HaloSize = 12,
                        HaloFalloff = 70,
                    },
                    new RenderLight
                    {
                        Key = 21,
                        Kind = RenderLightKind.Point,
                        Intensity = 800,
                        Position = new Vector3(1, 2, 3),
                        FalloffRadius = 7,
                        SourceRadius = 0.2,
                        CastShadows = false,
                    },
                    new RenderLight
                    {
                        Key = 22,
                        Kind = RenderLightKind.Spot,
                        Intensity = 1200,
                        Position = new Vector3(0, 3, 0),
                        InnerConeAngle = 0.3,
                        OuterConeAngle = 0.7,
                        FalloffRadius = 12,
                    },
                    new RenderLight
                    {
                        Key = 23,
                        Kind = RenderLightKind.Area,
                        Intensity = 500,
                        Position = new Vector3(0, 1, -2),
                        Direction = new Vector3(0, 0, 1),
                        Width = 2,
                        Height = 0.5,
                        Tangent = new Vector3(0, 0, 1),
                    },
                ],
            },
            null),
        ["air"] = (
            Scene() with
            {
                Sky = new Sky
                {
                    Colour = new Vector3(0.2, 0.3, 0.4),
                    Zenith = new Vector3(0.1, 0.2, 0.5),
                    Horizon = new Vector3(0.7, 0.8, 0.9),
                    Ambient = 15000,
                    Quality = SkyQuality.Fair,
                    BodyDirection = new Vector3(0, 2, 0),
                    BodyColour = new Vector3(1, 0.8, 0.6),
                    BodySize = 0.01,
                    Flash = 0.5,
                    FlashDirection = new Vector3(1, 1, 0),
                    FlashSeed = 7,
                    Clouds = new Clouds
                    {
                        Colour = new Vector3(0.9, 0.9, 0.95),
                        Cover = 0.5,
                        Wind = new Vector2(3, 1),
                        FeatureSize = 1.0 / 500,
                        Altitude = 1200,
                        Thickness = 700,
                        Density = 0.8,
                        Billow = 0.6,
                        Extinction = 0.02,
                    },
                },
                Fog = new Fog
                {
                    Colour = new Vector3(0.4, 0.5, 0.6),
                    Density = 0.1,
                    Distance = 5,
                    CutOffDistance = 500,
                    MaximumOpacity = 0.9,
                    Height = 3,
                    HeightFalloff = 0.5,
                    Structure = 0.4,
                    Wind = new Vector2(0.1, 0.2),
                    FeatureSize = 0.05,
                    Thickness = 8,
                },
                Precipitation = new Precipitation
                {
                    Colour = new Vector3(0.8, 0.8, 0.9),
                    Amount = 0.6,
                    Fall = 12,
                    Wind = new Vector2(1, 0.5),
                    DropsPerMetre = 8,
                    Stretch = 30,
                    Threshold = 0.6,
                },
            },
            null),
        ["air-fallbacks"] = (
            Scene() with
            {
                Sky = new Sky { Drawn = false, ShowBody = false, BodyDirection = Vector3.Zero(), FlashDirection = Vector3.Zero() },
                Fog = new Fog(),
            },
            null),
        ["materials"] = (
            Scene(
                Object(1, material: 20),
                Object(2, material: 21),
                Object(3, material: 999),
                Object(4),
                Object(5, material: 22)) with
            {
                Videos =
                [
                    new Video { Key = 70, Path = "/v/a.mp4" },
                    new Video { Key = 71, Path = "/v/b.mp4", Playing = false, Loop = false },
                ],
                Materials =
                [
                    new RenderMaterial
                    {
                        Key = 20,
                        Shading = MaterialShading.Unlit,
                        Blend = MaterialBlend.Masked,
                        Culling = MaterialCulling.None,
                        DoubleSided = true,
                        BaseColour = new Vector4(0.2, 0.4, 0.6, 0.5),
                        Metallic = 0.9,
                        Roughness = 0.2,
                        Reflectance = 0.7,
                        ClearCoat = 0.6,
                        ClearCoatRoughness = 0.05,
                        Anisotropy = -0.4,
                        SheenColour = new Vector3(0.3, 0.2, 0.1),
                        SheenRoughness = 0.45,
                        Wind = new Wind { Bearing = 90, Speed = 6, Strength = 0.75 },
                        Emissive = new Vector3(0.5, 0.25, 0.1),
                        EmissiveIntensity = 3,
                        AmbientOcclusion = 0.8,
                        NormalScale = 1.5,
                        Tiling = new Vector2(2, 3),
                        Offset = new Vector2(0.25, 0.5),
                        MaskThreshold = 0.55,
                        DepthWrite = false,
                        DepthBias = 0.01,
                        Wrap = MaterialWrap.Mirror,
                        Filter = MaterialFilter.Sharp,
                        BaseColourMap = new Texture("/art/a.png"),
                        NormalMap = new Texture("/art/n.png", Srgb: false),
                        MetallicRoughnessMap = new Texture("/art/n.png", Srgb: false),
                        OcclusionMap = new Texture("/art/a.png", Srgb: false),
                        EmissiveMap = new Texture("/art/e.png"),
                    },
                    new RenderMaterial
                    {
                        Key = 21,
                        Shading = MaterialShading.Video,
                        Video = 71,
                        ScreenMapped = true,
                        LitScreen = true,
                        BlendMode = MaterialBlendMode.Masked,
                        BlendAmount = 0.6,
                        BlendSharpness = 4,
                        BlendTiling = new Vector2(3, 3),
                        BlendOffset = new Vector2(0.1, 0.2),
                        BlendBaseColourMap = new Texture("/art/b.png"),
                        BlendMaskMap = new Texture("/art/m.png", Srgb: false),
                        BaseColourMap = new Texture("/art/a.png"),
                    },
                    new RenderMaterial
                    {
                        Key = 22,
                        Shading = MaterialShading.ShadowCatcher,
                        Video = 999,
                        Wind = new Wind { Bearing = 0, Speed = 0, Strength = 2 },
                    },
                    new RenderMaterial { Key = 20, Roughness = 0.7 },
                ],
            },
            null),
        ["surfaces"] = (
            Scene(Object(1, material: 8), Object(2, material: 9)) with
            {
                Materials =
                [
                    new RenderMaterial { Key = 7 },
                    new RenderMaterial
                    {
                        Key = 8,
                        Surface = new CustomSurface(
                            "/pkg/water.matc",
                            new Dictionary<string, IReadOnlyList<double>>
                            {
                                ["speed"] = new[] { 2.5 },
                                ["tint"] = new[] { 1, 0.5, 0.25, 1 },
                            },
                            new Dictionary<string, Texture> { ["noise"] = new("/art/noise.png") }),
                    },
                    new RenderMaterial
                    {
                        Key = 9,
                        Surface = new CustomSurface(
                            "/pkg/water.matc",
                            new Dictionary<string, IReadOnlyList<double>> { ["speed"] = new[] { 4.0 } },
                            new Dictionary<string, Texture> { ["noise"] = new("/art/noise.png") }),
                    },
                ],
            },
            null),
        ["decals"] = (
            Scene() with
            {
                Decals =
                [
                    new Decal
                    {
                        Key = 1,
                        Position = new Vector3(1, 2, 3),
                        Size = new Vector3(2, 0.5, 4),
                        Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 2),
                        Texture = new Texture("/art/scorch.png"),
                        Colour = new Vector3(0.9, 0.8, 0.7),
                        Opacity = 0.75,
                        FadeStartAngle = 0.5,
                        FadeEndAngle = 1.2,
                        Roughness = 0.3,
                        Layers = new HashSet<int> { 0, 2, 9, -1 },
                        SortOrder = 3,
                    },
                    new Decal { Key = 2, Position = Vector3.Zero(), Size = new Vector3(1, 1, 1), Metallic = 0.8 },
                    new Decal
                    {
                        Key = 3,
                        Position = new Vector3(0, 1, 0),
                        Size = new Vector3(3, 3, 3),
                        Texture = new Texture("/art/scorch.png", Srgb: false),
                        FadeStartAngle = Decal.NoFade,
                        FadeEndAngle = Decal.NoFade,
                    },
                ],
            },
            null),
        ["outline"] = (
            Scene(Object(3), Object(7), Object(9)) with
            {
                Outline = new Outline
                {
                    Keys = [3, 7, 9],
                    Primary = 7,
                    Colour = DisplayColour.FromArgb(0xFF00FF00),
                    PrimaryColour = DisplayColour.FromArgb(0x80FF0000),
                    Width = 400,
                    Occluded = OutlineOccluded.Faint,
                    OccludedOpacity = 0.25,
                    Dash = -5,
                },
            },
            null),
        ["outline-only-primary"] = (
            Scene() with { Outline = new Outline { Primary = 4, Width = -2 } },
            null),
        ["screen"] = (
            Scene() with
            {
                Lights =
                [
                    new RenderLight
                    {
                        Key = 1,
                        Kind = RenderLightKind.Point,
                        Intensity = 100,
                        Position = new Vector3(1, 1, 1),
                    },
                    new RenderLight
                    {
                        Key = 2,
                        Kind = RenderLightKind.Directional,
                        Intensity = 1000,
                        Colour = new Vector3(1, 0.8, 0.6),
                        Direction = new Vector3(0, -0.2, -1).Normalized(),
                    },
                ],
                Sky = new Sky { Clouds = Clouds.Stratus },
                GodRays = new GodRays { Strength = 0.5, Samples = 32 },
                Distortions =
                [
                    Distortion.Shockwave(new Vector3(1, 2, 3), 4, thickness: 0.5, strength: 0.02, chromatic: 0.3),
                    Distortion.Haze(
                        new Vector3(-1, 0, 2),
                        new Vector3(0.5, 1, 0.5),
                        scale: 0.2,
                        speed: 2,
                        seconds: 3),
                    Distortion.Lens(-0.1),
                    Distortion.Expanding(Vector3.Zero(), 1.5, speed: 4, lifetime: 2, strength: 0.04),
                    Distortion.Lens(0),
                ],
            },
            null),
        ["screen-tint"] = (
            Scene() with
            {
                Lights =
                [
                    new RenderLight
                    {
                        Key = 2,
                        Kind = RenderLightKind.Directional,
                        Intensity = 1000,
                        Direction = new Vector3(1, 0, 0),
                    },
                ],
                Sky = new Sky { Drawn = false, Clouds = Clouds.Stratus },
                GodRays = new GodRays { Strength = 1, Tint = new Vector3(0.2, 0.4, 1) },
            },
            null),
        ["camera"] = (
            Scene() with
            {
                Camera = new RenderCamera
                {
                    Position = new Vector3(3, 4, 5),
                    Target = new Vector3(1, 0, -1),
                    FieldOfView = 70,
                    Orthographic = true,
                    ViewHeight = 12,
                    Aperture = 2.8,
                    ShutterSpeed = 1.0 / 60,
                    Sensitivity = 400,
                },
            },
            null),
        ["post"] = (
            Scene() with
            {
                Post = new PostProcess
                {
                    AntiAliasing = AntiAliasing.Temporal,
                    Bloom = new Bloom
                    {
                        Enabled = true,
                        Strength = 0.3,
                        Levels = 5,
                        Threshold = false,
                        LensFlare = true,
                        FlareStrength = 0.07,
                    },
                    DepthOfField = new DepthOfField
                    {
                        Enabled = true,
                        FocusDistance = 4,
                        BlurScale = 2,
                        MaxForeground = 3,
                        MaxBackground = 5,
                    },
                    Vignette = new Vignette
                    {
                        Enabled = true,
                        MidPoint = 0.3,
                        Roundness = 0.8,
                        Feather = 0.2,
                        Colour = new Vector3(0.1, 0.2, 0.3),
                    },
                    Occlusion = new Occlusion
                    {
                        Enabled = true,
                        Radius = 0.6,
                        Strength = 0.8,
                        Bias = 0.002,
                        Quality = 2,
                        BentNormals = true,
                    },
                    Reflections = new Reflections
                    {
                        Enabled = true,
                        Thickness = 0.2,
                        Bias = 0.03,
                        MaxDistance = 7,
                        Stride = 3,
                    },
                    Grading = new Grading
                    {
                        Enabled = true,
                        ToneMapping = ToneMapping.Aces,
                        Exposure = 0.5,
                        Contrast = 1.2,
                        Saturation = 0.9,
                        Vibrance = 1.4,
                        Temperature = -0.2,
                        Tint = 0.1,
                        Shadows = new Vector3(0.9, 1, 1.1),
                        Midtones = new Vector3(1, 1.05, 1),
                        Highlights = new Vector3(1.2, 1, 0.8),
                    },
                    Dithering = false,
                },
            },
            null),
        ["post-off"] = (
            Scene() with { Post = new PostProcess { Enabled = false, AntiAliasing = AntiAliasing.Off } },
            null),
        ["pipeline"] = (
            Scene() with
            {
                Pipeline = new Pipeline
                {
                    Shadows = new ShadowSettings
                    {
                        Kind = ShadowKind.Area,
                        MapSize = 2048,
                        Cascades = 3,
                        Distance = 60,
                        Lambda = 0.7,
                        ConstantBias = 0.002,
                        NormalBias = 0.5,
                        Stable = true,
                        Contact = true,
                        Softness = 1.5,
                        Splits = [0.1, 0.3, 0.6, 0.9],
                        SoftnessFalloff = 2,
                        ContactDistance = 0.5,
                        ContactSteps = 12,
                        Variance = new VarianceShadows
                        {
                            Blur = 2.5,
                            Anisotropy = 4,
                            Samples = 2,
                            LightBleedReduction = 0.3,
                            HighPrecision = true,
                            Mipmapping = true,
                            Exponential = true,
                        },
                    },
                    Resolution = new Resolution { Adaptive = true, MinScale = 0.6, MaxScale = 0.9, Sharpness = 0.4 },
                    Lighting = new Lighting { ClusterNear = 1, ClusterFar = 250 },
                    Samples = 4,
                    Precise = true,
                    Culling = false,
                    Refraction = false,
                    Textures = new TextureLimits { MaxSize = 2048, UploadKilobytes = 512 },
                },
            },
            null),
        ["pipeline-fixed-scale"] = (
            Scene() with
            {
                Pipeline = new Pipeline
                {
                    Shadows = new ShadowSettings { Enabled = false, Splits = [0.5], MapSize = 256 },
                    Resolution = new Resolution { Scale = 0.75 },
                    Textures = new TextureLimits { MaxSize = 1 << 20, UploadKilobytes = -5 },
                },
            },
            null),
        ["pipeline-handheld"] = (
            Scene() with { Pipeline = Pipeline.ForDisplay(new Display(1280, 800, Detail.Ultra)) },
            null),
        ["pipeline-handheld-low"] = (
            Scene() with { Pipeline = Pipeline.ForDisplay(new Display(1280, 800, Detail.Low)) },
            null),
        ["pipeline-large-display"] = (
            Scene() with { Pipeline = Pipeline.ForDisplay(new Display(3840, 2160, Detail.High)) },
            null),
        ["environment"] = (
            Scene() with
            {
                Environment = RenderEnvironment.FromImage("/e/room.hdr") with
                {
                    Intensity = 20000,
                    Rotation = 1.2,
                    ShowSkybox = false,
                    Size = 512,
                },
                Probes =
                [
                    new Probe
                    {
                        Key = 40,
                        Position = new Vector3(1, 2, 3),
                        Radius = 8,
                        Resolution = 128,
                        Version = 2,
                        Layers = 0x0F,
                        Intensity = 0.5,
                    },
                    new Probe { Key = 41, Position = new Vector3(-4, 1.5, 0) },
                ],
                Field = new IrradianceField
                {
                    Enabled = true,
                    Origin = new Vector3(-8, 0, -8),
                    Spacing = new Vector3(1, 1, 1),
                    Counts = new Vector3(4, 2, 4),
                    From = "light",
                    Intensity = 1.5,
                    Retention = 0.9,
                    Bias = 0.2,
                },
            },
            null),
        ["environment-split"] = (
            Scene() with { Environment = new RenderEnvironment { Radiance = "/e/radiance.ktx", Skybox = "/e/sky.ktx" } },
            null),
        ["graph-custom"] = (
            Scene() with
            {
                Graph = new RenderGraph
                {
                    Targets =
                    [
                        new RenderTarget { Name = "frame", Scale = 0.5 },
                        new RenderTarget { Name = "mirror", Width = 640, Height = 360, Depth = false },
                        new RenderTarget { Name = "flat", Colour = false },
                    ],
                    Passes =
                    [
                        new RenderPass
                        {
                            Name = "mirror",
                            Kind = PassKind.Reflection,
                            Into = "mirror",
                            Plane = [0, 1, 0, -0.5],
                            Clear = false,
                            Layers = 0x03,
                        },
                        new RenderPass { Name = "world", Into = "frame", Layers = 0x0F },
                        new RenderPass { Name = "off", Into = "flat", Enabled = false },
                        new RenderPass
                        {
                            Name = "sharpen",
                            Kind = PassKind.Effect,
                            Effect = PassEffect.Sharpen,
                            Reads = ["frame", "mirror"],
                            Plane = [0.6],
                        },
                    ],
                },
            },
            null),
        ["graph-problem"] = (
            Scene() with { Graph = new RenderGraph { Passes = [new RenderPass { Name = "a" }, new RenderPass { Name = "b" }] } },
            null),
        ["graph-motion-blur"] = (
            Scene() with { Graph = new MotionBlur { Shutter = 0.5, Samples = 8 }.Graph() },
            null),
        ["graph-distortion-only"] = (
            Scene() with { Distortions = [Distortion.Lens(strength: -0.1)] },
            null),
        ["graph-god-rays-no-sun"] = (
            Scene() with { GodRays = new GodRays { Strength = 0.5 } },
            null),
    };

    /// <summary>The bytes a packed array holds, as the fixture writes them in base 64.</summary>
    private static string Encoded(object value) => value switch
    {
        float[] f => Convert.ToBase64String(ToBytes(f)),
        int[] i => Convert.ToBase64String(ToBytes(i)),
        long[] l => Convert.ToBase64String(ToBytes(l)),
        _ => throw new InvalidDataException($"unexpected {value.GetType().Name}"),
    };

    private static byte[] ToBytes<T>(T[] values) where T : unmanaged
    {
        var bytes = new byte[values.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>()];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static string? Difference(JsonElement expected, object actual)
    {
        if (expected.TryGetProperty("f32", out var f32))
        {
            return Same(f32.GetString(), Encoded(actual));
        }

        if (expected.TryGetProperty("i32", out var i32))
        {
            return Same(i32.GetString(), Encoded(actual));
        }

        if (expected.TryGetProperty("i64", out var i64))
        {
            return Same(i64.GetString(), Encoded(actual));
        }

        if (expected.TryGetProperty("strings", out var strings))
        {
            return Same(
                string.Join('|', strings.EnumerateArray().Select(s => s.GetString())),
                string.Join('|', (string[])actual));
        }

        if (expected.TryGetProperty("string", out var text))
        {
            return Same(text.GetString(), (string)actual);
        }

        if (expected.TryGetProperty("bool", out var flag))
        {
            return Same(flag.GetBoolean(), (bool)actual);
        }

        if (expected.TryGetProperty("double", out var number))
        {
            return Same(Fixture.Number(number), Convert.ToDouble(actual, System.Globalization.CultureInfo.InvariantCulture));
        }

        throw new InvalidDataException($"unhandled fixture value {expected.GetRawText()}");
    }

    private static string? Same<T>(T expected, T actual) =>
        EqualityComparer<T>.Default.Equals(expected, actual) ? null : $"the fixture {expected}, the port {actual}";

    [Fact]
    public void EveryKeyTheScenePortSendsMatchesTheOriginalPackerByByte()
    {
        var scenes = Scenes();
        var wrong = new List<string>();
        foreach (var c in Fixture.Cases)
        {
            var name = c.GetProperty("name").GetString()!;
            var (scene, at) = scenes[name];
            var message = scene.ToMessage(at);
            foreach (var entry in c.GetProperty("expect").EnumerateObject())
            {
                if (message.TryGetValue(entry.Name, out var actual) && Difference(entry.Value, actual) is { } difference)
                {
                    wrong.Add($"{name}.{entry.Name}: {difference}");
                }
            }
        }

        Assert.True(wrong.Count == 0, $"{string.Join(Environment.NewLine, wrong)}{Environment.NewLine}from fixture commit {Fixture.SourceCommit}");
    }

    [Fact]
    public void EveryScenePacksTheKeysThatTheFixtureCovers()
    {
        var scenes = Scenes();
        foreach (var c in Fixture.Cases)
        {
            var name = c.GetProperty("name").GetString()!;
            var expected = c.GetProperty("expect").EnumerateObject().Select(e => e.Name).ToHashSet();
            var (scene, at) = scenes[name];
            var missing = scene.ToMessage(at).Keys.Where(key => !expected.Contains(key)).ToList();
            Assert.True(missing.Count == 0, $"{name}: the port sends {string.Join(", ", missing)}, which the original does not.");
        }
    }
}
