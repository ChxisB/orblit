using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>One clip made for the bones of another skeleton, as <paramref name="plan"/> says.</summary>
internal sealed class RetargetRun(ClipDocument clip, RetargetPlan plan, string target)
{
    private const string LostRoot = "The bone that carried root motion moves nothing in the target, so the clip has none.";

    /// <summary>Source bone, then property, then what the clip has there.</summary>
    private readonly Dictionary<int, Dictionary<string, ClipChannel>> _moves = MovesOf(clip, plan.From, target);

    public ClipRetargeted Run()
    {
        var carried = clip.Channels.Where(channel => channel.Bone is null || channel.Target != target);
        var made = plan.Drivers.Keys.Order().SelectMany(ChannelsOf);
        var root = RootMotionOf();
        List<string> problems = [.. plan.Problems, .. LeftOut()];
        if (root is null && clip.RootMotion is not null)
        {
            problems.Add(LostRoot);
        }

        return new ClipRetargeted(clip.CopyWith(channels: [.. carried, .. made], rootMotion: root, clearRootMotion: root is null), problems);
    }

    /// <summary>[keys] with each value and slope carried through <paramref name="value"/> and <paramref name="slope"/>.</summary>
    private static List<Key<T>> MapKeys<T>(IReadOnlyList<Key<T>> keys, Func<T, T> value, Func<T, T> slope)
    {
        Slope<T>? Carry(Slope<T>? one) => one is { } named ? new Slope<T>(slope(named.Value)) : null;
        return [.. keys.Select(key => new Key<T>(key.At, value(key.Value), key.Hold, key.Shape, Carry(key.SlopeIn), Carry(key.SlopeOut)))];
    }

    private static Dictionary<int, Dictionary<string, ClipChannel>> MovesOf(ClipDocument clip, RestSkeleton from, string target)
    {
        var moves = new Dictionary<int, Dictionary<string, ClipChannel>>();
        foreach (var channel in clip.Channels)
        {
            if (channel.Bone is not { } name || channel.Target != target || from.IndexOf(name) is not { } bone)
            {
                continue;
            }

            if (!moves.TryGetValue(bone, out var byProperty))
            {
                moves[bone] = byProperty = [];
            }

            byProperty[channel.Property] = channel;
        }

        return moves;
    }

    /// <summary>Whether <paramref name="channel"/> never leaves <paramref name="rest"/>.</summary>
    private static bool StaysAt(ClipChannel<Vector3> channel, Vector3 rest) => channel.Keys.All(key =>
        (key.Value - rest).Length <= 1e-6 * (1 + rest.Length) && Still(key.SlopeIn) && Still(key.SlopeOut));

    private static bool Still(Slope<Vector3>? slope) => slope is null || slope.Value.Value.Length < 1e-9;

    private static Quaternion Unit(Quaternion turn)
    {
        turn.Normalize();
        return turn;
    }

    /// <summary>The first few of <paramref name="names"/>, and how many more there are.</summary>
    private static string Some(List<string> names)
    {
        var unique = names.Distinct().ToList();
        var shown = string.Join(", ", unique.Take(5).Select(static name => $"\"{name}\""));
        return unique.Count > 5 ? $"{shown} and {unique.Count - 5} more" : shown;
    }

    private IEnumerable<ClipChannel> ChannelsOf(int bone) =>
        new ClipChannel?[] { Rotation(bone), Position(bone), Scale(bone) }.OfType<ClipChannel>();

    private ClipChannel<T>? ChannelOf<T>(int source, string property)
        where T : notnull =>
        _moves.TryGetValue(source, out var byProperty) && byProperty.TryGetValue(property, out var channel) ? channel as ClipChannel<T> : null;

    private ClipChannel<Quaternion>? Turn(int source) => ChannelOf<Quaternion>(source, "rotation");

    private ClipChannel<Quaternion>? Rotation(int bone)
    {
        var link = plan.LinkOf(bone);
        var moving = link.Involved.Where(source => Turn(source) is not null).ToList();
        if (moving.Count == 0 && !plan.RestsElsewhere(bone))
        {
            return null;
        }

        var alone = moving.Count == 1 && moving[0] == link.Source;
        var keys = alone && Turn(link.Source) is { } own ? Carried(bone, link.Source, own) : Baked(bone, moving);
        return new ClipChannel<Quaternion>(target, "rotation", ChannelKind.Rotation, keys, plan.To.Names[bone]);
    }

    /// <summary>
    /// The driver's own keys, turned: the one bone that moves is the last factor of <c>Up</c>, so its key is
    /// multiplied by constants either side.
    /// </summary>
    private List<Key<Quaternion>> Carried(int bone, int source, ClipChannel<Quaternion> own)
    {
        Quaternion Carry(Quaternion turn) => plan.RotationOf(bone, other => other == source ? turn : plan.RestTurnOf(other));
        return MapKeys(own.Keys, turn => Unit(Carry(turn)), Carry);
    }

    /// <summary>A key at each moment a moving bone has one, or a single key at the start when none moves and the rest was fixed.</summary>
    private List<Key<Quaternion>> Baked(int bone, List<int> moving)
    {
        var times = moving.Select(Turn).OfType<ClipChannel<Quaternion>>().SelectMany(static channel => channel.Keys).Select(static key => key.At).ToHashSet();
        if (moving.Count == 0)
        {
            times.Add(0);
        }

        return [.. times.Order().Select(at => new Key<Quaternion>(
            at,
            Unit(plan.RotationOf(bone, source => Turn(source)?.Curve.At(at) ?? plan.RestTurnOf(source))),
            Hold.Linear))];
    }

    private ClipChannel<Vector3>? Position(int bone)
    {
        var source = plan.Drivers[bone];
        if (ChannelOf<Vector3>(source, "position") is not { } channel || StaysAt(channel, plan.From.PositionOf(source)))
        {
            return null;
        }

        return VectorChannel(bone, "position", MapKeys(channel.Keys, vector => plan.PositionOf(bone, vector), slope => plan.ChangeOf(bone, slope)));
    }

    private ClipChannel<Vector3>? Scale(int bone)
    {
        var source = plan.Drivers[bone];
        if (ChannelOf<Vector3>(source, "scale") is not { } channel || StaysAt(channel, plan.From.ScaleOf(source)))
        {
            return null;
        }

        return VectorChannel(bone, "scale", MapKeys(channel.Keys, vector => plan.ScaleOf(bone, vector), vector => plan.ScaleOf(bone, vector)));
    }

    private ClipChannel<Vector3> VectorChannel(int bone, string property, List<Key<Vector3>> keys) =>
        new(target, property, ChannelKind.Vector, keys, plan.To.Names[bone]);

    private RootMotion? RootMotionOf()
    {
        var root = clip.RootMotion;
        if (root?.Bone is not { } name || root.Target != target)
        {
            return root;
        }

        var source = plan.From.IndexOf(name);
        var carriers = plan.Drivers.Where(pair => pair.Value == source).Select(static pair => pair.Key).ToList();
        if (source is not { } driver || carriers.Count == 0)
        {
            return null;
        }

        var bone = carriers.Min();
        var turn = (plan.ParentStanceOf(bone) * plan.To.ParentWorldRotationOf(bone)).Inverted() * plan.From.ParentWorldRotationOf(driver);
        return new RootMotion(root.Target, plan.To.Names[bone], root.Turns, root.Rises, turn.AsRotationMatrix().Transformed(root.Up));
    }

    private List<string> LeftOut()
    {
        var unknown = new List<string>();
        var undriven = new List<string>();
        foreach (var channel in clip.Channels)
        {
            if (channel.Bone is not { } name || channel.Target != target)
            {
                continue;
            }

            var source = plan.From.IndexOf(name);
            if (source is null)
            {
                unknown.Add(name);
            }
            else if (!plan.Drivers.Values.Contains(source.Value))
            {
                undriven.Add(name);
            }
        }

        List<string> notes = [];
        if (unknown.Count > 0)
        {
            notes.Add($"The clip moves {Some(unknown)}, which the source skeleton does not have, so {(unknown.Count == 1 ? "it was" : "they were")} left out.");
        }

        if (undriven.Count > 0)
        {
            notes.Add($"Nothing in the target skeleton is moved by {Some(undriven)}, so {(undriven.Count == 1 ? "its" : "their")} motion was left out.");
        }

        return notes;
    }
}
