using Orblit.VectorMath;

namespace Orblit.Sequence;

/// <summary>What happens when a sequence reaches its end.</summary>
public enum WhenDone
{
    /// <summary>Stops on the last frame and stays there. What a cutscene wants.</summary>
    Hold,

    /// <summary>Starts again. An idle, an ambience, a looping shot.</summary>
    Loop,

    /// <summary>Runs backwards to the start, then forwards again.</summary>
    Bounce,

    /// <summary>Stops and lets go, so the scene returns to whatever else is driving it.</summary>
    Release,
}

/// <summary>
/// A sequence being played. The only stateful thing in this package, and it
/// holds exactly two numbers: where the playhead is and whether it is moving.
/// </summary>
public sealed class Director(CutSequence sequence, WhenDone whenDone = WhenDone.Hold, double speed = 1.0)
{
    private double _at;
    private bool _playing;
    private int _direction = 1;

    public CutSequence Sequence { get; } = sequence;

    public WhenDone WhenDone { get; set; } = whenDone;

    /// <summary>
    /// How fast sequence time runs against whatever clock is advancing it.
    /// Negative plays it backwards, which fires no marks.
    /// </summary>
    public double Speed { get; set; } = speed;

    /// <summary>Where the playhead is, in seconds.</summary>
    public double At => _at;

    public bool Playing => _playing;

    /// <summary>Whether the playhead has run off the end and stopped.</summary>
    public bool Finished => !_playing && WhenDone != WhenDone.Loop && _at >= Sequence.Duration;

    public void Play() => _playing = true;

    public void Pause() => _playing = false;

    /// <summary>Back to the start, and stopped.</summary>
    public void Stop()
    {
        _playing = false;
        _direction = 1;
        _at = 0;
    }

    /// <summary>
    /// Moves the playhead without playing anything. Marks are not fired: a scrub
    /// is somebody looking, not the scene happening.
    /// </summary>
    public void Seek(double to) => _at = Doubles.Clamp(to, 0.0, Sequence.Duration);

    /// <summary>
    /// Moves the playhead on by <paramref name="seconds"/> of wall time and hands
    /// back everything that happened on the way. The marks are the crossed ones,
    /// in order, however big the step, so a dropped frame does not lose an event.
    /// </summary>
    public Advanced Advance(double seconds)
    {
        if (!_playing || seconds == 0)
        {
            return new Advanced(Sequence.SampleAt(_at), []);
        }

        var was = _at;
        var step = seconds * Speed * _direction;
        var now = was + step;
        var duration = Sequence.Duration;
        var marks = new List<Mark>();

        if (now >= duration && step > 0)
        {
            // Everything up to the end happens before whatever the wrap does, so a
            // mark on the last frame of a loop fires on every pass.
            marks.AddRange(Sequence.MarksBetween(was, duration));
            now = RunPastTheEnd(now, duration, marks);
        }
        else if (now <= 0 && step < 0)
        {
            now = RunPastTheStart(now, duration);
        }
        else if (step > 0)
        {
            marks.AddRange(Sequence.MarksBetween(was, now));
        }

        _at = now;
        return new Advanced(Sequence.SampleAt(_at), marks);
    }

    /// <summary>What the sequence says right now, without moving.</summary>
    public SequenceFrame Sample() => Sequence.SampleAt(_at);

    private double RunPastTheEnd(double now, double duration, List<Mark> marks)
    {
        switch (WhenDone)
        {
            case WhenDone.Hold:
            case WhenDone.Release:
                _playing = false;
                return duration;
            case WhenDone.Loop:
                var over = duration <= 0 ? 0.0 : Doubles.Mod(now - duration, duration);
                marks.AddRange(Sequence.MarksBetween(-1e-9, over));
                return over;
            case WhenDone.Bounce:
                _direction = -1;
                var back = duration - (now - duration);
                return back < 0 ? 0 : back;
            default:
                throw new System.Diagnostics.UnreachableException();
        }
    }

    /// <summary>Running backwards fires nothing on the way, by design.</summary>
    private double RunPastTheStart(double now, double duration)
    {
        switch (WhenDone)
        {
            case WhenDone.Bounce:
                _direction = 1;
                return -now > duration ? duration : -now;
            case WhenDone.Loop:
                return duration;
            case WhenDone.Hold:
            case WhenDone.Release:
                _playing = false;
                return 0;
            default:
                throw new System.Diagnostics.UnreachableException();
        }
    }
}

/// <summary>One step of a director: where the sequence now stands, and what happened getting there.</summary>
public sealed class Advanced(SequenceFrame frame, IReadOnlyList<Mark> marks)
{
    public SequenceFrame Frame { get; } = frame;

    public IReadOnlyList<Mark> Marks { get; } = marks;
}
