using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Reaches a limb of two bones for a point. This is the solver <see cref="IkChain"/> runs, as a step in
/// the stack. It reaches for a point in the model's space and not a bone, and the stack's influence
/// does the blending that <see cref="IkChain.Influence"/> does inside the pose.
/// Without a pole the joint bends the way the clip already bent it, which is the right default after
/// playback: the knee that was forward stays forward.
/// </summary>
/// <param name="root">The upper bone, a thigh or an upper arm.</param>
/// <param name="mid">The lower bone, a child of the root. The chain's end is its tail.</param>
/// <param name="target">Where the end of the chain reaches. Null leaves the limb as it is.</param>
/// <param name="pole">Where the joint points. Null keeps the bend the animation had.</param>
/// <param name="stretch">How far the limb may stretch past full extension, from zero to one.</param>
public sealed class TwoBoneIk(string root, string mid, ModelPoint target, ModelPoint? pole = null, double stretch = 0) : Modifier
{
    public string Root { get; } = root;

    public string Mid { get; } = mid;

    public ModelPoint Target { get; } = target;

    public ModelPoint? Pole { get; } = pole;

    public double Stretch { get; } = stretch;

    public override void Check(Armature armature)
    {
        BoneEdit.RequireBone(armature, Root, "so there is no limb to reach with");
        BoneEdit.RequireBone(armature, Mid, "so there is no limb to reach with");

        // RequireBone has just shown the bone is there.
        if (armature[Mid]!.Parent != Root)
        {
            throw new ArmatureException($"Bone \"{Mid}\" is not a child of \"{Root}\", so the two are not one limb.");
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var point = Target(pose, frame);
        if (point is null)
        {
            return;
        }

        Reach(pose, Root, Mid, point, Pole?.Invoke(pose, frame), Stretch);
    }

    /// <summary>
    /// Aims <paramref name="root"/> and <paramref name="mid"/> so the tail of <paramref name="mid"/>
    /// lands on <paramref name="target"/>, or as near as the limb reaches.
    /// </summary>
    // F2: six parameters, all named at every call, as a Dart record of named arguments is.
    public static void Reach(Pose pose, string root, string mid, Vector3 target, Vector3? pole = null, double stretch = 0)
    {
        var rootBone = pose.Armature[root] ?? throw new ArmatureException($"No bone called \"{root}\" in this armature.");
        var midBone = pose.Armature[mid] ?? throw new ArmatureException($"No bone called \"{mid}\" in this armature.");
        var rootHead = pose.HeadOf(root);

        var solution = Ik.SolveTwoBoneIk(
            rootHead,
            pole ?? pose.HeadOf(mid),
            target,
            rootBone.Length,
            midBone.Length,
            Doubles.Clamp(stretch, 0.0, 1.0));

        // Only the root is scaled. Scale is inherited, so stretching the upper bone carries the lower
        // one out to the right place and lengthens it by the same factor. It is written every solve,
        // back to one included, so a stretch from a frame that reached further does not outlive the
        // reach.
        pose[root].Scale.SetFrom(Ik.StretchScale(solution.Stretch));
        BoneEdit.AimBone(pose, root, solution.Joint - rootHead);

        pose[mid].Scale.SetFrom(Ik.StretchScale(1));
        BoneEdit.AimBone(pose, mid, solution.End - pose.HeadOf(mid));
    }
}
