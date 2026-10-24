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
        JointPose[]? joints = null) =>
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
