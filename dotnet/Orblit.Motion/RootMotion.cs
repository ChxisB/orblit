using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>
/// Which channel moves the character, rather than the model.
/// </summary>
/// <remarks>
/// <para>
/// A walk authored in place looks like walking on a treadmill; one authored moving walks the model off its
/// own origin while the character it belongs to stays behind. Root motion is the second kind played as the
/// first: the named thing is held where it started, and how far it would have gone comes out of the player
/// as a step for whoever moves the character to take.
/// </para>
/// <para>What is taken is chosen the way a character wants it, not all or nothing:</para>
/// <list type="bullet">
/// <item><b>Across the ground</b> always: that is the walk.</item>
/// <item><b>Up and down</b> only when <see cref="Rises"/>. A walk's bob stays in the pose, because a body
/// that bobs a capsule up and down is a body that skips; a climb or a jump takes it, because there the body
/// really goes up.</item>
/// <item><b>Turning</b> only when <see cref="Turns"/>, and then only about <see cref="Up"/>. A turn on the
/// spot hands the character the heading; a lean or a sway stays in the pose.</item>
/// </list>
/// </remarks>
public sealed class RootMotion
{
    // F2: five parameters, all optional and called by name.
    public RootMotion(string target = "", string? bone = null, bool turns = false, bool rises = false, Vector3? up = null)
    {
        Target = target;
        Bone = bone;
        Turns = turns;
        Rises = rises;
        Up = up is null || up.Length2 < 1e-12 ? new Vector3(0, 1, 0) : up.Normalized();
    }

    /// <summary>The entity, as a channel's <see cref="ClipChannel.Target"/> names it.</summary>
    public string Target { get; }

    /// <summary>Its bone, for a skeleton's root, or null for the entity.</summary>
    public string? Bone { get; }

    /// <summary>Whether turning goes to the character.</summary>
    public bool Turns { get; }

    /// <summary>Whether rising and falling goes to the character.</summary>
    public bool Rises { get; }

    /// <summary>
    /// Which way is up, in the space the root's own channels are in.
    /// </summary>
    /// <remarks>
    /// A model's root bone hangs from the model, which is Y-up when the file is glTF. One exported Z-up and
    /// stood upright by a node above the skeleton has its root's channels in the space before that turn, and
    /// says so here.
    /// </remarks>
    public Vector3 Up { get; }

    public static RootMotion? FromJson(object? raw) => raw is not JsonMap map
        ? null
        : new RootMotion(
            Values.Text(map, "target") ?? string.Empty,
            Values.Text(map, "bone"),
            Values.Flag(map, "turns", false),
            Values.Flag(map, "rises", false),
            map.GetValueOrDefault("up") is JsonList up ? Values.Vector(up) : null);

    /// <summary>Whether <paramref name="channel"/> is this root's.</summary>
    public bool Owns(ClipChannel channel) => channel.Target == Target && channel.Bone == Bone;

    public OrderedDictionary<string, object?> ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["target"] = Target,
        ["bone"] = Bone,
        ["turns"] = Turns ? true : null,
        ["rises"] = Rises ? true : null,
        ["up"] = Up.Y == 1 ? null : Values.VectorToJson(Up),
    });
}
