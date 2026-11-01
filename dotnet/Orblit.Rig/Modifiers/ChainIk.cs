using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Reaches a chain of any length for a point, from <see cref="Root"/> down to <see cref="Tip"/>. A
/// spine, a tail or a tentacle has more bones than a limb has, and a closed form for two does not
/// stretch to them. This is FABRIK: it keeps the length of every bone and the place of the root, and
/// bends the chain until its tip lands on the target or runs out of reach.
/// With no pole there is nothing to say which way a joint folds, so the chain keeps the bend the
/// animation gave it as far as the reaching allows.
/// </summary>
/// <param name="root">The first bone of the chain. Its head does not move.</param>
/// <param name="tip">The last bone. Its tail is what reaches.</param>
/// <param name="target">Where the tip reaches, in the model's space. Null leaves the chain as it is.</param>
/// <param name="iterations">
/// How many passes the solver may make. More passes close more of the gap on a long chain and cost a
/// pass each.
/// </param>
public sealed class ChainIk(string root, string tip, ModelPoint target, int iterations = 12) : Modifier
{
    public string Root { get; } = root;

    public string Tip { get; } = tip;

    public ModelPoint Target { get; } = target;

    public int Iterations { get; } = iterations;

    public override void Check(Armature armature)
    {
        BoneEdit.RequireBone(armature, Root, "so the chain has no start");
        BoneEdit.RequireBone(armature, Tip, "so the chain has no end");
        if (BoneEdit.BonePath(armature, Root, Tip) is null)
        {
            throw new ArmatureException($"Bone \"{Tip}\" is not below \"{Root}\", so they are not one chain.");
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var goal = Target(pose, frame);
        if (goal is null)
        {
            return;
        }

        // Check has already shown there is a path.
        var path = BoneEdit.BonePath(pose.Armature, Root, Tip)!;
        List<Vector3> points = [.. path.Select(pose.HeadOf), pose.TailOf(Tip)];
        var wanted = Ik.SolveFabrik(points, goal, Iterations);
        PointAlong(pose, path, wanted);
    }

    /// <summary>
    /// Turns each bone of <paramref name="path"/> so the line from its head to the next bone's head, or
    /// to the tip's tail, runs along <paramref name="points"/>. Parents go first. Turning a bone carries
    /// the bones below it with it, so by the time a bone is reached its head is already where the one
    /// above put it.
    /// </summary>
    public static void PointAlong(Pose pose, IReadOnlyList<string> path, IReadOnlyList<Vector3> points)
    {
        for (var i = 0; i < path.Count; i++)
        {
            var bone = path[i];
            var here = pose.HeadOf(bone);
            var next = i + 1 < path.Count ? pose.HeadOf(path[i + 1]) : pose.TailOf(bone);
            var now = next - here;
            var wanted = points[i + 1] - points[i];
            if (now.Length2 < 1e-12 || wanted.Length2 < 1e-12)
            {
                continue;
            }

            BoneEdit.TurnBone(pose, bone, Ik.RotationBetween(now, wanted));
        }
    }
}
