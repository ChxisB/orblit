using Orblit.Rig;

namespace Orblit.Motion;

/// <summary>
/// One skeleton moved by the pose of another, a frame at a time.
/// </summary>
/// <remarks>
/// The same turn as <see cref="ClipRetarget"/>, worked out per frame instead of baked: whatever poses the
/// source, a clip, a blend, a ragdoll or a capture, the target follows it with nothing to bake and nothing to
/// reload when the source changes. A source frame is a map of bone name to <see cref="BoneLocal"/>, as
/// <see cref="ClipFrame.Bones"/> holds for a target. A bone it leaves out is at rest.
/// </remarks>
public sealed class LiveRetarget
{
    private readonly RetargetPlan _plan;
    private readonly Lazy<Matrix4> _intoTarget;

    /// <summary>Pairs the bones of <paramref name="from"/> and <paramref name="to"/> the way <see cref="ClipRetarget"/> does.</summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="bones"/> names a bone one skeleton lacks, or <paramref name="unpaired"/> names one
    /// <paramref name="to"/> lacks or one <paramref name="bones"/> pairs.
    /// </exception>
    // F2: six parameters, called by name.
    public LiveRetarget(
        RestSkeleton from,
        RestSkeleton to,
        IReadOnlyDictionary<string, string>? bones = null,
        IReadOnlySet<string>? unpaired = null,
        bool humanoid = false,
        bool fixRest = false)
    {
        From = from;
        To = to;
        _plan = RetargetPlan.Of(from, to, bones, unpaired, humanoid, fixRest);
        _intoTarget = new Lazy<Matrix4>(() => InverseOf(to.ParentWorldOf(0)));
    }

    public RestSkeleton From { get; }

    public RestSkeleton To { get; }

    /// <summary>What could not be paired or fixed, in words for a person.</summary>
    public IReadOnlyList<string> Problems => _plan.Problems;

    /// <summary>
    /// Where <paramref name="source"/> puts each bone of the target that something drives.
    /// </summary>
    /// <remarks>
    /// A rotation for every driven bone. A position or a scale only where the source frame has one, since a bone
    /// that is not moved keeps its rest.
    /// </remarks>
    public OrderedDictionary<string, BoneLocal> Follow(IReadOnlyDictionary<string, BoneLocal> source)
    {
        Quaternion Turn(int bone) => source.GetValueOrDefault(From.Names[bone])?.Rotation ?? _plan.RestTurnOf(bone);

        var frame = new OrderedDictionary<string, BoneLocal>();
        foreach (var (bone, driver) in _plan.Drivers)
        {
            var local = source.GetValueOrDefault(From.Names[driver]);
            frame[To.Names[bone]] = new BoneLocal(
                rotation: _plan.RotationOf(bone, Turn),
                position: local?.Position is { } position ? _plan.PositionOf(bone, position) : null,
                scale: local?.Scale is { } scale ? _plan.ScaleOf(bone, scale) : null);
        }

        return frame;
    }

    /// <summary>
    /// Where <paramref name="bone"/> of the source stands, as a point in the target's own space, and made as
    /// much larger as the target is.
    /// </summary>
    /// <remarks>
    /// The two models are taken to stand with their origin at the floor, so that scaling a foot's place scales
    /// its height off the floor too.
    /// </remarks>
    /// <exception cref="ArgumentException">When the source has no such bone.</exception>
    public Vector3 PointOf(string bone, IReadOnlyDictionary<string, BoneLocal> source)
    {
        var start = From.IndexOf(bone) ?? throw NoBone(bone);
        var chain = new List<int>();
        for (var up = start; up >= 0; up = From.Parents[up])
        {
            chain.Add(up);
        }

        var world = From.ParentWorldOf(chain[^1]);
        for (var at = chain.Count - 1; at >= 0; at--)
        {
            var index = chain[at];
            var local = source.GetValueOrDefault(From.Names[index]);
            world.Multiply(Matrix4.Compose(
                local?.Position ?? From.PositionOf(index),
                local?.Rotation ?? From.RotationOf(index),
                local?.Scale ?? From.ScaleOf(index)));
        }

        var away = world.GetTranslation();
        away.Scale(_plan.Size);
        return _intoTarget.Value.Transformed3(away);
    }

    /// <summary>
    /// A point for a <see cref="TwoBoneIk"/> target: where <paramref name="bone"/> of the source is, read from
    /// whatever <paramref name="source"/> answers each time it is solved, in the target's own space. Null while
    /// <paramref name="source"/> answers null, which leaves the limb as it is.
    /// </summary>
    /// <remarks>
    /// With <paramref name="bone"/> a source foot and the limb the target's leg, the foot of the target stands
    /// where the source's does, so it slides no further than the source's slid.
    /// </remarks>
    /// <exception cref="ArgumentException">When the source has no such bone.</exception>
    public ModelPoint Pinned(string bone, Func<IReadOnlyDictionary<string, BoneLocal>?> source)
    {
        if (From.IndexOf(bone) is null)
        {
            throw NoBone(bone);
        }

        return (_, _) => source() is { } now ? PointOf(bone, now) : null;
    }

    // A matrix with no inverse is left as it was, so a degenerate skeleton still answers.
    private static Matrix4 InverseOf(Matrix4 world)
    {
        var inverse = Matrix4.Zero();
        inverse.CopyInverse(world);
        return inverse;
    }

    private static ArgumentException NoBone(string bone) => new("The source has no such bone.", nameof(bone));
}
