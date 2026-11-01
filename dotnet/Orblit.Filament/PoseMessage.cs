namespace Orblit.Filament;

internal static class PoseMessage
{
    /// <summary>
    /// What clips, variants and hand-set joints do to the objects that have them, or null when no
    /// object has any. Each posed object takes a row of <see cref="ModelAnimation.IntStride"/> whole
    /// numbers and <see cref="ModelAnimation.Stride"/> floats, and its joints follow end to end.
    /// </summary>
    internal static OrderedDictionary<string, object>? Pack(IReadOnlyList<RenderObject> objects)
    {
        var posed = objects.Where(IsPosed).ToList();
        if (posed.Count == 0)
        {
            return null;
        }

        var count = posed.Count;
        var keys = new long[count];
        var ints = new int[count * ModelAnimation.IntStride];
        var floats = new float[count * ModelAnimation.Stride];
        var jointCounts = new int[count];
        var joints = new List<int>();
        var transforms = new List<float>();

        for (var i = 0; i < count; i++)
        {
            keys[i] = posed[i].Key;
            WriteRow(posed[i], ints.AsSpan(i * ModelAnimation.IntStride, ModelAnimation.IntStride), floats.AsSpan(i * ModelAnimation.Stride, ModelAnimation.Stride));
            jointCounts[i] = posed[i].Joints?.Count ?? 0;
            AddJoints(posed[i].Joints, joints, transforms);
        }

        return new OrderedDictionary<string, object>
        {
            ["poseKeys"] = keys,
            ["poseInts"] = ints,
            ["poseFloats"] = floats,
            ["poseJointCounts"] = jointCounts,
            ["poseJoints"] = joints.ToArray(),
            ["poseJointTransforms"] = transforms.ToArray(),
        };
    }

    private static bool IsPosed(RenderObject one) =>
        one.Animation is not null || one.Variant is not null || one.Joints is { Count: > 0 };

    private static void WriteRow(RenderObject one, Span<int> ints, Span<float> floats)
    {
        var animation = one.Animation ?? ModelAnimation.Rest;
        var from = animation.From ?? ModelAnimation.Rest;

        ints[0] = animation.Clip;
        ints[1] = from.Clip;
        ints[2] = (animation.Loop ? ModelAnimation.LoopsFlag : 0) | (from.Loop ? ModelAnimation.FromLoopsFlag : 0);
        ints[3] = one.Variant ?? -1;

        floats[0] = (float)animation.Seconds;
        floats[1] = (float)animation.Speed;
        floats[2] = (float)from.Seconds;
        floats[3] = (float)from.Speed;
        floats[4] = animation.From is null ? 1 : (float)animation.Fade;
    }

    private static void AddJoints(IReadOnlyList<JointPose>? set, List<int> joints, List<float> transforms)
    {
        foreach (var joint in set ?? [])
        {
            joints.Add(joint.Skin);
            joints.Add(joint.Joint);
            for (var k = 0; k < 16; k++)
            {
                transforms.Add((float)joint.Transform[k]);
            }
        }
    }
}
