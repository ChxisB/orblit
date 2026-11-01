using System.Collections.ObjectModel;

namespace Orblit.Motion;

/// <summary>
/// Which bone of a skeleton fills each <see cref="HumanoidBone"/>, and what could not be worked out.
/// </summary>
public sealed class HumanoidMap
{
    private HumanoidMap(IEnumerable<KeyValuePair<HumanoidBone, string>> bones, IEnumerable<string> problems)
    {
        Bones = new ReadOnlyDictionary<HumanoidBone, string>(new OrderedDictionary<HumanoidBone, string>(bones));
        Problems = problems.ToList().AsReadOnly();
    }

    /// <summary>
    /// The bone filling each slot the skeleton has. A slot it lacks, such as a shoulder or toes, is absent.
    /// </summary>
    public IReadOnlyDictionary<HumanoidBone, string> Bones { get; }

    /// <summary>Why a slot was left empty, in words for a person.</summary>
    public IReadOnlyList<string> Problems { get; }

    public string? this[HumanoidBone slot] => Bones.GetValueOrDefault(slot);

    /// <summary>
    /// Finds the parts of a person in <paramref name="skeleton"/> from how its bones hang and where they stand
    /// at rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Names are looked at only to tell left from right. A rig from any tool has legs that hang from the hips
    /// and reach the floor, a spine that reaches the head, and arms that leave the top of the spine, and that is
    /// what is searched for. So <c>Skeleton_arm_joint_L__4_</c> and <c>LeftArm</c> both land in
    /// <see cref="HumanoidBone.LeftUpperArm"/> when each is the first bone of an arm.
    /// </para>
    /// <para>
    /// <paramref name="up"/> is which way is up in the skeleton's world, and <paramref name="forward"/> which way
    /// a person standing in it faces. The left side is then <c>up × forward</c>, which is +X for a glTF model. A
    /// name that says a side is believed over that.
    /// </para>
    /// <para>
    /// A skeleton that is not a person, or one with more bones in a limb than a slot for each, is mapped as far
    /// as it can be and says what it left out in <see cref="Problems"/>. Bones it did not place are named by
    /// hand when a clip is retargeted.
    /// </para>
    /// </remarks>
    public static HumanoidMap Of(RestSkeleton skeleton, Vector3? up = null, Vector3? forward = null)
    {
        var finder = new HumanoidFinder(
            skeleton,
            (up ?? new Vector3(0, 1, 0)).Normalized(),
            (forward ?? new Vector3(0, 0, 1)).Normalized());
        finder.Run();
        return new HumanoidMap(finder.Found, finder.Problems);
    }

    /// <summary>Which bone of <paramref name="from"/> moves each bone of <paramref name="to"/>, by the slot each fills.</summary>
    /// <remarks>
    /// Only the slots both skeletons have are paired. A target with one spine bone against a source with three
    /// is paired at the spine, and what the other two do reaches the target through the bones below.
    /// </remarks>
    public static OrderedDictionary<string, string> Match(RestSkeleton from, RestSkeleton to)
    {
        var source = Of(from).Bones;
        return new OrderedDictionary<string, string>(
            Of(to).Bones
                .Where(slot => source.ContainsKey(slot.Key))
                .Select(slot => KeyValuePair.Create(slot.Value, source[slot.Key])));
    }
}
