using Orblit.Rig;

namespace Orblit.Motion;

/// <summary>Where a limb of three joints ends up reaching for a point.</summary>
public sealed class LimbReach
{
    private LimbReach(Quaternion root, Quaternion mid, Vector3 end, bool reachable)
    {
        Root = root;
        Mid = mid;
        End = end;
        Reachable = reachable;
    }

    /// <summary>The turn to give the upper bone in its parent.</summary>
    public Quaternion Root { get; }

    /// <summary>The turn to give the lower bone in the upper one.</summary>
    public Quaternion Mid { get; }

    /// <summary>
    /// Where the tip stands, in the model's space. The target when it was reachable, and the nearest the limb
    /// gets when it was not.
    /// </summary>
    public Vector3 End { get; }

    /// <summary>
    /// False when the target was further than the limb is long, or so near the limb would have to fold through
    /// itself.
    /// </summary>
    public bool Reachable { get; }

    /// <summary>
    /// The turns that put the tip of a limb on <paramref name="target"/>, as a pose would have them: for
    /// <paramref name="root"/> and <paramref name="mid"/>, their rotation in their parents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="root"/> is the upper bone, a thigh or an upper arm. <paramref name="mid"/> hangs from it,
    /// and <paramref name="tip"/> hangs from <paramref name="mid"/>, so the three heads are a hip, a knee and an
    /// ankle. <paramref name="pose"/> is where the bones stand now, as a clip frame gives them, and
    /// <paramref name="target"/> is in the model's space, the space <see cref="RestSkeleton.PosedOrigins"/>
    /// answers in.
    /// </para>
    /// <para>
    /// Without a <paramref name="pole"/> the joint bends the way it already does, turning with the limb, so a
    /// knee that was forward stays forward as the foot is lifted. A limb that is straight bends the way the rest
    /// pose bends it. Neither bone is stretched, since the answer is two rotations that a clip can key, and a
    /// rotation cannot lengthen a bone.
    /// </para>
    /// <para>
    /// Worked out from world turns, so it assumes the skeleton is scaled the same in every direction, as a rig
    /// made in centimetres is.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// A name is not a bone, or the three are not a chain from parent to child.
    /// </exception>
    // F2: a limb is a skeleton, a pose, three names, a target and a pole, called by name.
    public static LimbReach Of(
        RestSkeleton skeleton,
        IReadOnlyDictionary<string, BoneLocal> pose,
        string root,
        string mid,
        string tip,
        Vector3 target,
        Vector3? pole = null)
    {
        var rootAt = BoneNamed(skeleton, root);
        var midAt = BoneNamed(skeleton, mid);
        var tipAt = BoneNamed(skeleton, tip);
        if (skeleton.Parents[midAt] != rootAt || skeleton.Parents[tipAt] != midAt)
        {
            throw new ArgumentException(
                $"\"{root}\", \"{mid}\" and \"{tip}\" are not a limb: each has to hang from the one before.");
        }

        var worlds = skeleton.PosedWorlds(pose);
        var rootHead = worlds[rootAt].GetTranslation();
        var midHead = worlds[midAt].GetTranslation();
        var tipHead = worlds[tipAt].GetTranslation();
        var upper = midHead - rootHead;
        var lower = tipHead - midHead;

        var solution = Ik.SolveTwoBoneIk(
            root: rootHead,
            pole: pole ?? BendPole(skeleton, worlds, (rootAt, midAt, tipAt), target) ?? midHead,
            target: target,
            upperLength: upper.Length,
            lowerLength: lower.Length);

        var upperTurn = Ik.RotationBetween(upper, solution.Joint - rootHead);
        var lowerTurn = Ik.RotationBetween(
            // Through the matrix, because rotating a vector by a quaternion directly turns it backwards.
            upperTurn.AsRotationMatrix().Transformed(lower),
            solution.End - solution.Joint);

        var parent = skeleton.Parents[rootAt];
        var aboveRoot = RotationOf(parent < 0 ? skeleton.ParentWorldOf(rootAt) : worlds[parent]);
        var rootWorld = upperTurn * RotationOf(worlds[rootAt]);
        var midWorld = lowerTurn * upperTurn * RotationOf(worlds[midAt]);

        return new LimbReach(
            (aboveRoot.Conjugated() * rootWorld).Normalized(),
            (rootWorld.Conjugated() * midWorld).Normalized(),
            solution.End,
            solution.Reachable);
    }

    /// <summary>
    /// A point on the side of the root to tip line that the joint bends to, turned the way the line turns to
    /// reach <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    /// The joint itself will not do as a pole: it is on one side of the line from the root to where the tip is,
    /// and a target past it puts it on the other side of the line to there, which would swing the knee backwards.
    /// Null when the limb is straight and so is the rest, which names no side.
    /// </remarks>
    private static Vector3? BendPole(RestSkeleton skeleton, IReadOnlyList<Matrix4> worlds, (int, int, int) limb, Vector3 target)
    {
        var (rootAt, _, tipAt) = limb;
        var rootHead = worlds[rootAt].GetTranslation();
        var line = worlds[tipAt].GetTranslation() - rootHead;
        var bend = BendOf(worlds, limb) ?? BendOf(skeleton.PosedWorlds(new Dictionary<string, BoneLocal>()), limb);
        if (bend is null)
        {
            return null;
        }

        var turn = Ik.RotationBetween(line, target - rootHead);
        return rootHead + turn.AsRotationMatrix().Transformed(bend);
    }

    /// <summary>
    /// The part of the joint's offset from the root that is not along the line to the tip, or null when the
    /// limb is straight to within rounding.
    /// </summary>
    private static Vector3? BendOf(IReadOnlyList<Matrix4> worlds, (int, int, int) limb)
    {
        var (rootAt, midAt, tipAt) = limb;
        var rootHead = worlds[rootAt].GetTranslation();
        var line = worlds[tipAt].GetTranslation() - rootHead;
        var joint = worlds[midAt].GetTranslation() - rootHead;
        var length = line.Length;
        if (length < 1e-9)
        {
            return null;
        }

        var along = line * (joint.Dot(line) / (length * length));
        var bend = joint - along;
        return bend.Length < 1e-6 * length ? null : bend;
    }

    private static int BoneNamed(RestSkeleton skeleton, string name) =>
        skeleton.IndexOf(name) ?? throw new ArgumentException("The skeleton has no such bone.", nameof(name));

    private static Quaternion RotationOf(Matrix4 matrix)
    {
        var rotation = Quaternion.Identity();
        matrix.Decompose(Vector3.Zero(), rotation, Vector3.Zero());
        return rotation.Normalized();
    }
}
