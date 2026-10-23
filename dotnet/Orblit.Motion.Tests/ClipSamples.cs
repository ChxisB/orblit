using Orblit.Json;

namespace Orblit.Motion.Tests;

/// <summary>Clips the tests read and write, and the helpers that make a file around a body.</summary>
internal static class ClipSamples
{
    /// <summary>A clip with one of most things in it, to send round the trip.</summary>
    public static ClipDocument Populated() => new(
        "Lamp flicker",
        2,
        WhenDone.Loop,
        24,
        rootMotion: new RootMotion(bone: "hips", turns: true),
        channels:
        [
            new ClipChannel<double>(
                "bulb",
                "light.power",
                ChannelKind.Number,
                [
                    new Key<double>(0, 60, Hold.Linear),
                    new Key<double>(0.5, 20, Hold.Linear),
                    new Key<double>(1, 60, Hold.Shaped, Easing.Bounce),
                    new Key<double>(2, 60, Hold.Linear),
                ]),
            new ClipChannel<Vector3>(
                string.Empty,
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, new Vector3(0, 0, 0)), new Key<Vector3>(2, new Vector3(1, 2, 3))]),
            new ClipChannel<bool>(
                "bulb/glow",
                "mesh.visible",
                ChannelKind.Flag,
                [new Key<bool>(0, true, Hold.Step), new Key<bool>(1.5, false)]),
            new ClipChannel<Quaternion>(
                string.Empty,
                "rotation",
                ChannelKind.Rotation,
                [
                    new Key<Quaternion>(
                        0,
                        Quaternion.Identity(),
                        Hold.Curve,
                        slopeOut: new Slope<Quaternion>(new Quaternion(0, 0.5, 0, 0))),
                    new Key<Quaternion>(
                        1,
                        Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.5),
                        Hold.Curve,
                        slopeIn: new Slope<Quaternion>(new Quaternion(0, 0.25, 0, -0.1))),
                ],
                "hips"),
        ],
        marks:
        [
            new Mark(1, "buzz", Tree.Map(("volume", 0.5))),
            new Mark(0.25, "spark"),
        ]);

    public static OrderedDictionary<string, object?> File(params (string Key, object? Value)[] body) =>
        Tree.With(
            Tree.Map(("kind", ClipDocument.Marker), ("formatVersion", ClipDocument.FormatVersion), ("name", "Test")),
            body);

    public static ClipLoad Load(params (string Key, object? Value)[] body) =>
        ClipDocument.Decode(JsonText.Encode(File(body)));

    public static double[] Numbers(Quaternion rotation) => [rotation.X, rotation.Y, rotation.Z, rotation.W];

    /// <summary>A rotation channel on <paramref name="bone"/> with straight lines between the keys.</summary>
    public static ClipChannel<Quaternion> Turns(string bone, params (double At, Quaternion Turn)[] keys) =>
        new(
            string.Empty,
            "rotation",
            ChannelKind.Rotation,
            [.. keys.Select(key => new Key<Quaternion>(key.At, key.Turn, Hold.Linear))],
            bone);

    /// <summary>A position channel on <paramref name="bone"/> with straight lines between the keys.</summary>
    public static ClipChannel<Vector3> Moves(string bone, params (double At, Vector3 Place)[] keys) =>
        new(
            string.Empty,
            "position",
            ChannelKind.Vector,
            [.. keys.Select(key => new Key<Vector3>(key.At, key.Place, Hold.Linear))],
            bone);

    public static Quaternion About(double x, double y, double z, double degrees) =>
        Quaternion.AxisAngle(new Vector3(x, y, z).Normalized(), Doubles.Radians(degrees));

    /// <summary>The angle between two rotations, written out so a check never goes through the code under test.</summary>
    public static double TurnBetween(Quaternion a, Quaternion b)
    {
        var along = Math.Abs((a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W));
        return 2 * Math.Acos(Math.Clamp(along, 0.0, 1.0));
    }

    /// <summary>The channel that is the only one in <paramref name="clip"/>, as the kind it carries.</summary>
    public static ClipChannel<T> Only<T>(ClipDocument clip)
        where T : notnull => Assert.IsType<ClipChannel<T>>(Assert.Single(clip.Channels));
}
