namespace Orblit.Motion.Tests;

/// <summary>The skeleton, clips and poses the layer tests share.</summary>
internal static class LayerSamples
{
    /// <summary>Hips with a leg and a spine below it, and an arm below the spine, all at rest in place.</summary>
    public static RestSkeleton Skeleton() => new(
        ["hips", "leg", "spine", "arm"],
        [-1, 0, 0, 2],
        [.. Enumerable.Range(0, 4).Select(static _ => Matrix4.Identity())]);

    /// <summary>One second of <paramref name="bone"/> sliding along x from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static ClipChannel<Vector3> MovingBone(string bone, double from, double to) => new(
        string.Empty,
        "position",
        ChannelKind.Vector,
        [new Key<Vector3>(0, new Vector3(from, 0, 0), Hold.Linear), new Key<Vector3>(1, new Vector3(to, 0, 0))],
        bone: bone);

    /// <summary>A frame that says everything about the arm, and nothing else.</summary>
    public static ClipFrame Pose(double position = 0, double rotation = 0, double scale = 1)
    {
        var frame = new ClipFrame(0);
        frame.Bones[string.Empty] = new OrderedDictionary<string, BoneLocal>
        {
            ["arm"] = new BoneLocal(new Vector3(position, 0, 0), Quaternion.AxisAngle(new Vector3(0, 0, 1), rotation), Vector3.All(scale)),
        };
        return frame;
    }

    public static double BoneX(ClipFrame frame, string bone) => frame.BoneOf(string.Empty, bone)!.Position!.X;

    /// <summary>How far round <paramref name="rotation"/> turns, in radians.</summary>
    public static double Radians(Quaternion rotation) => 2 * Math.Acos(rotation.W);
}
