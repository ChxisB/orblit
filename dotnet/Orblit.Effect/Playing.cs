namespace Orblit.Effect;

/// <summary>
/// Somebody's effects, and the clock they are on. A thin thing on purpose: it
/// holds a time and a list, and everything interesting is in the effects. What
/// it adds is the two questions a host actually asks: what does everything
/// come to right now, and what has finished and can be dropped.
/// </summary>
public sealed class Playing
{
    private readonly List<TimedEffect> _running = [];
    private readonly List<double> _startedAt = [];

    public double Now { get; private set; }

    public int Length => _running.Count;

    /// <summary>
    /// What the effects that have finished came to. Kept rather than dropped,
    /// and this is the part that is easy to get wrong: a change is a
    /// <i>difference</i> from where something was, so an effect removed the
    /// moment it finishes takes its offset with it and the thing snaps back to
    /// where it started. It stays here until a host folds it into the
    /// transform with <see cref="Bake"/>.
    /// </summary>
    public Change Settled { get; private set; } = Change.None;

    /// <summary>Everything running and everything finished, added together.</summary>
    public Change Change
    {
        get
        {
            var total = Settled;
            for (var i = 0; i < _running.Count; i++)
            {
                total = total.And(_running[i].At(Now - _startedAt[i]));
            }

            return total;
        }
    }

    /// <summary>Starts <paramref name="effect"/> at the current moment.</summary>
    public void Start(TimedEffect effect)
    {
        _running.Add(effect);
        _startedAt.Add(Now);
    }

    /// <summary>
    /// Moves the clock on and drops anything that has finished. Returns what
    /// has just finished, because "it has stopped" is the moment a host wants
    /// to do the next thing and polling for it is worse.
    /// </summary>
    public IReadOnlyList<TimedEffect> Advance(double seconds)
    {
        Now += seconds;
        var done = new List<TimedEffect>();
        for (var i = _running.Count - 1; i >= 0; i--)
        {
            if (_running[i].DoneBy(Now - _startedAt[i]))
            {
                var finished = _running[i];
                _running.RemoveAt(i);
                _startedAt.RemoveAt(i);

                // Its end is kept, not dropped. See Settled.
                Settled = Settled.And(finished.At(finished.Duration));
                done.Add(finished);
            }
        }

        done.Reverse();
        return done;
    }

    /// <summary>
    /// Takes what has finished, so a host can fold it into the transform.
    /// After this, <see cref="Change"/> is only what is still running, which is
    /// what a host wants once it has moved the thing itself. Without it the
    /// settled part grows for as long as the object lives.
    /// </summary>
    public Change Bake()
    {
        var taken = Settled;
        Settled = Change.None;
        return taken;
    }

    public void Clear()
    {
        _running.Clear();
        _startedAt.Clear();
        Settled = Change.None;
    }
}
