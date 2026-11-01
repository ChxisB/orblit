using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// One step of a <see cref="ClipPlayer"/>: where the clip now stands, and what happened getting there.
/// </summary>
public sealed class ClipStep(ClipFrame frame, IReadOnlyList<Mark>? marks = null, RootStep? moved = null)
{
    /// <summary>The clip where the player now is, with its root motion held in place.</summary>
    public ClipFrame Frame { get; } = frame;

    /// <summary>Every mark passed, in the order it was passed.</summary>
    public IReadOnlyList<Mark> Marks { get; } = marks ?? [];

    /// <summary>How far root motion carried the character.</summary>
    public RootStep Moved { get; } = moved ?? new RootStep();
}

/// <summary>
/// A clip being played.
/// </summary>
/// <remarks>
/// <para>
/// Like a sequence's director, it holds only where the playhead is and which way it is going, and asks the
/// clip for everything else, so it can be seeked anywhere at any time. It starts paused, the way a director
/// does.
/// </para>
/// <para>
/// What it adds is the clip's two kinds of consequence. Marks fire on the way forwards, including one at the
/// very start of each pass, so a footstep on the first frame of a walk lands on every lap. Root motion is
/// measured over every stretch actually played, so a loop that wraps in the middle of a step still moves the
/// character by the whole step.
/// </para>
/// </remarks>
public sealed class ClipPlayer(ClipDocument clip, WhenDone? whenDone = null, double speed = 1.0)
{
    // Past this many ends in one step the step is not a frame, it is a stall.
    private const int MaxEnds = 256;

    private double _at;
    private bool _playing;
    private bool _finished;
    private int _direction = 1;

    public ClipDocument Clip { get; } = clip;

    /// <summary>What playing does at the end. The clip's own, unless said otherwise.</summary>
    public WhenDone WhenDone { get; set; } = whenDone ?? clip.WhenDone;

    /// <summary>
    /// How fast clip time runs against whatever is advancing it. Negative plays it backwards, which fires no
    /// marks and walks backwards.
    /// </summary>
    public double Speed { get; set; } = speed;

    /// <summary>Where the playhead is, in seconds.</summary>
    public double At => _at;

    public bool Playing => _playing;

    /// <summary>Whether it ran to an end and stopped there on its own.</summary>
    public bool Finished => _finished;

    /// <summary>
    /// Whether it finished by letting go: nothing it says should be applied any more, and whatever else
    /// drives the model takes it back.
    /// </summary>
    public bool Released => _finished && WhenDone == WhenDone.Release;

    public void Play()
    {
        _playing = true;
        _finished = false;
    }

    public void Pause() => _playing = false;

    /// <summary>Back to the start, and stopped.</summary>
    public void Stop()
    {
        _playing = false;
        _finished = false;
        _direction = 1;
        _at = 0;
    }

    /// <summary>
    /// Moves the playhead without playing anything: no marks and no motion, because a scrub is somebody
    /// looking, not the character moving.
    /// </summary>
    public void Seek(double to)
    {
        _at = Doubles.Clamp(to, 0.0, Clip.Duration);
        _finished = false;
    }

    /// <summary>
    /// Moves on by <paramref name="seconds"/> of wall time and hands back everything that happened on the
    /// way, however big the step: a frame dropped under load must not lose a footstep or shorten a walk.
    /// </summary>
    public ClipStep Advance(double seconds)
    {
        var left = seconds * Math.Abs(Speed);
        if (!_playing || !(left > 0))
        {
            return new ClipStep(Sample());
        }

        var run = new Run(left);
        while (!run.Done)
        {
            Round(run);
        }

        return new ClipStep(Sample(), run.Marks, run.Moved);
    }

    /// <summary>What the clip says right now, root held, without moving.</summary>
    public ClipFrame Sample() => Clip.SampleAt(_at, inPlace: true);

    private void Round(Run run)
    {
        var duration = Clip.Duration;
        var forward = (_direction > 0) == (Speed > 0);
        Walk(run, forward);

        if (!(forward ? _at >= duration : _at <= 0))
        {
            run.Done = true;
            return;
        }

        if (WhenDone is WhenDone.Hold or WhenDone.Release)
        {
            _playing = false;
            _finished = true;
            run.Done = true;
            return;
        }

        // A loop or a bounce turns at the end only with time left to spend, so a step that lands exactly on
        // the last frame shows it.
        if (run.Left <= 0 || duration <= 0)
        {
            run.Done = true;
            return;
        }

        Turn(forward);
        if (++run.Ends >= MaxEnds)
        {
            Skip(run, forward);
        }
    }

    /// <summary>Plays as much of what is left as there is room for before the next end.</summary>
    private void Walk(Run run, bool forward)
    {
        var room = forward ? Clip.Duration - _at : _at;
        var go = Math.Min(run.Left, room);
        if (!(go > 0))
        {
            return;
        }

        var from = _at;
        var to = Landing(from, go, room, forward);
        if (forward)
        {
            // A pass that starts at the very start takes the marks there too: nothing else would ever fire
            // them.
            run.Marks.AddRange(Clip.MarksBetween(from <= 0 ? -1e-9 : from, to));
        }

        run.Moved = run.Moved.Then(Clip.RootStepBetween(from, to));
        _at = to;
        run.Left -= go;
    }

    private double Landing(double from, double go, double room, bool forward)
    {
        if (go == room)
        {
            return forward ? Clip.Duration : 0.0;
        }

        return forward ? from + go : from - go;
    }

    private void Turn(bool forward)
    {
        if (WhenDone == WhenDone.Loop)
        {
            _at = forward ? 0 : Clip.Duration;
        }
        else
        {
            _direction = -_direction;
        }
    }

    /// <summary>
    /// Whole laps are skipped, with where they would have taken the character but without their marks.
    /// </summary>
    private void Skip(Run run, bool forward)
    {
        var duration = Clip.Duration;
        var loops = WhenDone == WhenDone.Loop;
        var lap = loops ? duration : 2 * duration;
        var laps = Doubles.Floor(run.Left / lap);
        run.Left -= laps * lap;
        if (loops && laps > 0)
        {
            var one = forward ? Clip.RootStepBetween(0, duration) : Clip.RootStepBetween(duration, 0);
            run.Moved = run.Moved.Then(RootStep.Repeated(one, laps));
        }

        run.Ends = 0;
    }

    private sealed class Run(double left)
    {
        public double Left = left;

        public int Ends;

        public bool Done;

        public RootStep Moved = new();

        public List<Mark> Marks = [];
    }
}
