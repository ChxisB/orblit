using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>One state of a blend: what plays while the blend is in it.</summary>
public sealed class BlendState
{
    // F2: five parameters, all but the first two optional and called by name.
    public BlendState(
        string name,
        BlendSource plays,
        double speed = 1,
        WhenDone? whenDone = null,
        IReadOnlyList<string>? sync = null)
    {
        List<string> markers = [.. sync ?? []];
        if (name.Length == 0)
        {
            throw new ArgumentException("A state needs a name.", nameof(name));
        }

        if (!double.IsFinite(speed) || speed < 0)
        {
            throw new ArgumentException("A state plays forwards, or not at all.", nameof(speed));
        }

        if (markers.Count > 0 && !AreMarkers(markers))
        {
            throw new ArgumentException("At least two distinct marker names.", nameof(sync));
        }

        Name = name;
        Plays = plays;
        Speed = speed;
        WhenDone = whenDone;
        Sync = markers.AsReadOnly();
    }

    /// <summary>What changes name it by, one to a blend.</summary>
    public string Name { get; }

    public BlendSource Plays { get; }

    /// <summary>
    /// How fast it plays. Forwards only: a walk backwards is a clip of a walk backwards, because a walk
    /// played in reverse puts its weight on the wrong foot.
    /// </summary>
    public double Speed { get; }

    /// <summary>What its clips do at their ends, or null for each clip's own.</summary>
    public WhenDone? WhenDone { get; }

    /// <summary>Cyclic contacts in order. Phase zero is the first named mark.</summary>
    public IReadOnlyList<string> Sync { get; }

    /// <summary>
    /// This state with whatever is given in place of what it has. A <paramref name="clearWhenDone"/> leaves
    /// each clip to its own ending.
    /// </summary>
    // F2: six parameters, all optional and called by name.
    public BlendState CopyWith(
        string? name = null,
        BlendSource? plays = null,
        double? speed = null,
        WhenDone? whenDone = null,
        bool clearWhenDone = false,
        IReadOnlyList<string>? sync = null) => new(
        name ?? Name,
        plays ?? Plays,
        speed ?? Speed,
        clearWhenDone ? null : whenDone ?? WhenDone,
        sync ?? Sync);

    public double LapFor(ClipDocument clip, double lap)
    {
        // The property is a nullable, so the enum is named in full.
        var ending = WhenDone ?? clip.WhenDone;
        return ending == Orblit.Sequence.WhenDone.Loop ? ClipSync.SyncedLap(clip, lap, Sync) : lap;
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var written = new OrderedDictionary<string, object?> { ["name"] = Name };
        foreach (var (key, value) in Plays.ToJson())
        {
            written[key] = value;
        }

        written["speed"] = Speed == 1 ? null : Speed;
        written["whenDone"] = WhenDone is { } ending ? FileNames.Of(ending) : null;
        written["sync"] = Sync.Count == 0 ? null : Sync;
        return Values.Pruned(written);
    }

    private static bool AreMarkers(List<string> names) =>
        names.Count >= 2 && names.Distinct().Count() == names.Count && names.All(static one => one.Length > 0);
}
