namespace Orblit.Motion;

/// <summary>A clip made for another skeleton, and what of it could not be carried.</summary>
public sealed class ClipRetargeted(ClipDocument clip, IReadOnlyList<string>? problems = null)
{
    public ClipDocument Clip { get; } = clip;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>A clip made for the bones of one skeleton, made to move the bones of another.</summary>
public static class ClipRetarget
{
    /// <summary>
    /// <paramref name="clip"/>, made for the bones of <paramref name="from"/>, made to move the bones of
    /// <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each bone of <paramref name="to"/> is turned so it stands in the world the way the bone that drives it
    /// stood in the clip, measured from where each rests. A shoulder that tips forward in the source tips
    /// forward in the target, whatever way the two skeletons happen to point their bones. Bones are driven by
    /// <see cref="BoneNames.Match"/>, and <paramref name="bones"/> adds to that or overrides it, naming a bone
    /// of <paramref name="to"/> and the bone of <paramref name="from"/> that moves it. One bone can drive
    /// several. A bone of <paramref name="to"/> in <paramref name="unpaired"/> is left out of the automatic
    /// pairing, so nothing moves it. A bone cannot be both named in <paramref name="bones"/> and unpaired.
    /// </para>
    /// <para>
    /// With <paramref name="humanoid"/> the bones are also paired by the part of a person each is, from how
    /// they hang and where they stand, which is what carries a clip between two rigs that share no names. What
    /// could not be paired is in <see cref="ClipRetargeted.Problems"/>.
    /// </para>
    /// <para>
    /// With <paramref name="fixRest"/> each bone is first turned to point the way its driver does at rest, so
    /// a target stood in an A-pose follows a source stood in a T-pose by where its limbs point and not by how
    /// far they move. A bone whose rest this changes gets a key even when the source never moves it.
    /// </para>
    /// <para>
    /// Rotations are worked out exactly at every key when the bone and its parent line up with the bones
    /// driving them, and easing, holds and cubic slopes come across as they are. Where they do not, because the
    /// target has fewer spine bones than the source, or a parent nothing drives, the turn is worked out at each
    /// moment any of the bones involved has a key, and joined with straight lines. Positions are the change
    /// from rest, in the world, made as much larger as <paramref name="to"/> is than <paramref name="from"/>:
    /// the ratio of their <see cref="RestSkeleton.Reach"/>. A tall character's hips travel further over a
    /// stride than a short one's. A position that never leaves rest is left out. Scales are the change from
    /// rest, and an axis is assumed to be the same axis in both.
    /// </para>
    /// <para>
    /// Root motion follows the bone that carried it. Only bone channels of <paramref name="target"/> are
    /// retargeted; every other channel is kept as it was. Marks, length and rate are kept.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// When <paramref name="bones"/> names a bone one skeleton does not have, or <paramref name="unpaired"/>
    /// names one <paramref name="to"/> does not have or one <paramref name="bones"/> pairs.
    /// </exception>
    // F2: eight parameters, all but the first three optional and called by name.
    public static ClipRetargeted Of(
        ClipDocument clip,
        RestSkeleton from,
        RestSkeleton to,
        IReadOnlyDictionary<string, string>? bones = null,
        IReadOnlySet<string>? unpaired = null,
        string target = "",
        bool humanoid = false,
        bool fixRest = false) =>
        new RetargetRun(clip, RetargetPlan.Of(from, to, bones, unpaired, humanoid, fixRest), target).Run();
}
