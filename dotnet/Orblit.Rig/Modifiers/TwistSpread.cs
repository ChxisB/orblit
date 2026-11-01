using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Shares the roll of one bone out along the twist bones above it. A wrist that rolls a quarter turn
/// pinches the skin of the forearm into a sweet wrapper if one bone takes all of it. A twist bone
/// halfway up the forearm that takes half of the roll, and another that takes a quarter, spread it
/// over the length of the arm.
/// The roll is read from the source's own turn about its length. Each of the bones is given that angle
/// times its share, and its other rotation is replaced. A twist bone is one the clip has no business
/// animating, so nothing is lost.
/// </summary>
public sealed class TwistSpread : Modifier
{
    /// <summary>The twist bones go from the one furthest from the source to the nearest.</summary>
    /// <param name="source">The bone whose roll is spread, a hand or a foot.</param>
    /// <param name="bones">The twist bones.</param>
    /// <param name="shares">
    /// What part of the roll each bone takes: one for the bone that should turn as far as the source
    /// does, and zero for one that stays put. Even steps up to one when left out.
    /// </param>
    public TwistSpread(string source, IReadOnlyList<string> bones, IReadOnlyList<double>? shares = null)
    {
        var used = shares ?? EvenShares(bones.Count);
        if (bones.Count == 0)
        {
            throw new ArgumentException("Give at least one bone.", nameof(bones));
        }

        if (used.Count != bones.Count)
        {
            throw new ArgumentException($"Give one share for each of the {bones.Count} bones.", nameof(shares));
        }

        if (used.Any(share => !double.IsFinite(share)))
        {
            throw new ArgumentException("A share is a number.", nameof(shares));
        }

        Source = source;
        Bones = bones;
        Shares = used;
    }

    public string Source { get; }

    public IReadOnlyList<string> Bones { get; }

    public IReadOnlyList<double> Shares { get; }

    private static List<double> EvenShares(int count) =>
        Enumerable.Range(1, count).Select(i => (double)i / count).ToList();

    public override void Check(Armature armature)
    {
        BoneEdit.RequireBone(armature, Source, "so there is no roll to spread");
        foreach (var bone in Bones)
        {
            BoneEdit.RequireBone(armature, bone, "so there is nowhere to spread it");
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var along = BoneEdit.AlongBone;
        var roll = Ik.TwistAngle(Ik.SwingTwist(pose[Source].Rotation, along).Twist, along);
        for (var i = 0; i < Bones.Count; i++)
        {
            pose[Bones[i]].Rotation = Quaternion.AxisAngle(along, roll * Shares[i]);
            pose.Refresh(from: Bones[i]);
        }
    }
}
