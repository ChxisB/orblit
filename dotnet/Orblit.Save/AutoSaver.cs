namespace Orblit.Save;

/// <summary>
/// Picks which keys to save on each tick. A key comes due once in
/// <see cref="Every"/> ticks plus up to <see cref="Jitter"/>, and a tick claims
/// at most <see cref="PerTick"/> of the keys that are due, the longest overdue
/// first. A key seen for the first time comes due somewhere in its first
/// <see cref="Every"/> ticks, so players who joined together do not save
/// together.
/// </summary>
// shortcut: a tick sorts the due keys, which is fine to a few thousand keys.
// Upgrade to a heap past that.
public sealed class AutoSaver
{
    private readonly Random _random;

    // Insertion order is the order the random numbers are drawn in.
    private readonly OrderedDictionary<string, long> _dueAt = new(StringComparer.Ordinal);

    /// <summary>Refuses a quota or interval below 1, and a negative jitter.</summary>
    public AutoSaver(int perTick, int every, int jitter, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(perTick, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(every, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(jitter);
        PerTick = perTick;
        Every = every;
        Jitter = jitter;
        _random = random;
    }

    /// <summary>The most keys one tick claims.</summary>
    public int PerTick { get; }

    /// <summary>The ticks between one save of a key and its next.</summary>
    public int Every { get; }

    /// <summary>The most ticks added to <see cref="Every"/>, chosen at random.</summary>
    public int Jitter { get; }

    /// <summary>
    /// The keys of <paramref name="live"/> to save on <paramref name="tick"/>. A key that is
    /// no longer in <paramref name="live"/> is forgotten.
    /// </summary>
    // F5: claiming a key is what moves its next turn, as removeFirst does.
    public IReadOnlyList<string> Claim(IEnumerable<string> live, long tick)
    {
        var keys = live.Distinct(StringComparer.Ordinal).ToList();
        var present = keys.ToHashSet(StringComparer.Ordinal);
        foreach (var gone in _dueAt.Keys.Where(key => !present.Contains(key)).ToList())
        {
            _dueAt.Remove(gone);
        }

        // A draw is made only for a new key, so TryAdd cannot be used here.
        foreach (var key in keys.Where(key => !_dueAt.ContainsKey(key)))
        {
            _dueAt.Add(key, tick + 1 + _random.Next(Every));
        }

        // Strings compare by UTF-16 code unit, which is ordinal.
        var picked = _dueAt
            .Where(entry => entry.Value <= tick)
            .OrderBy(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(PerTick)
            .Select(entry => entry.Key)
            .ToList();
        foreach (var key in picked)
        {
            _dueAt[key] = tick + Every + _random.Next(Jitter + 1);
        }

        return picked;
    }
}
