using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// Takes out the keys of a clip that its straight lines do not need.
/// </summary>
public static class KeyReducer
{
    /// <summary>
    /// <paramref name="clip"/> with the keys that its straight lines do not need taken out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A baked clip, such as a retarget that could not keep the source's keys, has a key at every moment any
    /// bone had one. Most of them sit on the line between their neighbours. A key is dropped when the channel,
    /// joined straight between the keys kept, is within tolerance of it. The first and last key of a channel
    /// stay.
    /// </para>
    /// <para>
    /// <paramref name="angle"/> is the most a rotation may be turned, in radians. <paramref name="distance"/>
    /// is the most a vector or a number may be off, in the channel's own unit, which is metres for a bone's
    /// position. Both default to a twentieth of a degree and half a millimetre.
    /// </para>
    /// <para>
    /// Only channels whose keys are all straight lines are reduced. Eased and curved channels are kept as they
    /// are, because taking a key out of one changes the curve across the keys next to it, not only at the
    /// key. A channel with a cut, two keys at one time, is kept as it is too.
    /// </para>
    /// </remarks>
    public static ClipDocument Reduce(ClipDocument clip, double angle = 5e-4, double distance = 5e-4) =>
        clip.CopyWith(channels: [.. clip.Channels.Select(channel => Thin(channel, angle, distance))]);

    private static ClipChannel Thin(ClipChannel channel, double angle, double distance) => channel switch
    {
        ClipChannel<Quaternion> turns => Thinned(turns, TurnedBetween, angle),
        ClipChannel<Vector3> moves => Thinned(moves, (a, b) => a.DistanceTo(b), distance),
        ClipChannel<double> numbers => Thinned(numbers, (a, b) => Math.Abs(a - b), distance),
        _ => channel,
    };

    private static double TurnedBetween(Quaternion a, Quaternion b)
    {
        var along = Math.Abs((a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W));
        return 2 * Math.Acos(Doubles.Clamp(along, 0.0, 1.0));
    }

    private static bool Straight<T>(IReadOnlyList<Key<T>> keys)
        where T : notnull
    {
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var key = keys[i];
            var plain = key.SlopeIn is null && key.SlopeOut is null;
            if (key.Hold != Hold.Linear || !plain || key.At >= keys[i + 1].At)
            {
                return false;
            }
        }

        return true;
    }

    private static ClipChannel<T> Thinned<T>(ClipChannel<T> channel, Func<T, T, double> apart, double tolerance)
        where T : notnull
    {
        var keys = channel.Keys;
        if (keys.Count < 3 || !Straight(keys))
        {
            return channel;
        }

        var keep = new bool[keys.Count];
        keep[0] = true;
        keep[^1] = true;

        // The key farthest from the line between two kept keys is kept, and the spans either side are looked
        // at again. Nothing farther than the tolerance is left out, and between two kept keys the channel is
        // a straight line, so the keys are where it could be off. The spans wait on a stack rather than in
        // recursion, because a long run of keys can nest as deep as it is long.
        var spans = new Stack<(int From, int To)>();
        spans.Push((0, keys.Count - 1));
        while (spans.Count > 0)
        {
            var (from, to) = spans.Pop();
            var worst = Farthest(channel, from, to, apart, tolerance);
            if (worst < 0)
            {
                continue;
            }

            keep[worst] = true;
            spans.Push((from, worst));
            spans.Push((worst, to));
        }

        return channel.WithKeys([.. keys.Where((_, i) => keep[i])]);
    }

    /// <summary>
    /// The key strictly between <paramref name="from"/> and <paramref name="to"/> that is farthest off the
    /// straight line between them and by more than <paramref name="tolerance"/>, or -1.
    /// </summary>
    // F2: five parameters, all of what one look along a span needs.
    private static int Farthest<T>(ClipChannel<T> channel, int from, int to, Func<T, T, double> apart, double tolerance)
        where T : notnull
    {
        var keys = channel.Keys;
        var worst = -1;
        var most = tolerance;
        var seconds = keys[to].At - keys[from].At;
        for (var at = from + 1; at < to; at++)
        {
            var along = (keys[at].At - keys[from].At) / seconds;
            var line = channel.Kind.Mixer.Lerp(keys[from].Value, keys[to].Value, along);
            var off = apart(line, keys[at].Value);
            if (off > most)
            {
                most = off;
                worst = at;
            }
        }

        return worst;
    }
}
