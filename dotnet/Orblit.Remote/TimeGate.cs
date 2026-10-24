namespace Orblit.Remote;

/// <summary>
/// The clock a game's loop passes its frame delta through, so that a debugger
/// can hold time still and walk it forward a frame at a time.
/// </summary>
/// <remarks>
/// The stage and the world take no clock of their own. The game's ticker calls
/// <c>World.Tick</c>, so pausing means asking the loop to hand over a smaller delta. A game
/// opts in with <c>world.Tick(gate.Advance(delta))</c>. The loop calls <see cref="Advance"/>
/// on one thread and a session calls the rest from a socket's, so every member takes the lock.
/// </remarks>
public sealed class TimeGate
{
    private readonly object _lock = new();
    private bool _paused;
    private int _pending;
    private int _passed;
    private TaskCompletionSource? _stepped;

    /// <summary>
    /// <paramref name="stepDelta"/> is how much time one stepped frame passes, which defaults to
    /// a sixtieth of a second because the solver is tuned for that rate. Throws
    /// <see cref="ArgumentOutOfRangeException"/> unless it is a positive, finite number, because a
    /// stepped frame that passes no time or NaN would look like a frozen game.
    /// </summary>
    public TimeGate(double stepDelta = 1.0 / 60)
    {
        if (!(stepDelta > 0 && double.IsFinite(stepDelta)))
        {
            throw new ArgumentOutOfRangeException(nameof(stepDelta), stepDelta, "The step delta is not above 0.");
        }

        StepDelta = stepDelta;
    }

    public double StepDelta { get; }

    public bool Paused
    {
        get
        {
            lock (_lock)
            {
                return _paused;
            }
        }
    }

    /// <summary>
    /// Stepped frames the loop has passed since the gate was made. A step reads it before and
    /// after to say how many frames it got.
    /// </summary>
    public int Passed
    {
        get
        {
            lock (_lock)
            {
                return _passed;
            }
        }
    }

    /// <summary>Frames a step has asked for that the loop has not passed yet.</summary>
    public int Pending
    {
        get
        {
            lock (_lock)
            {
                return _pending;
            }
        }
    }

    /// <summary>
    /// The delta to simulate for a frame that took <paramref name="real"/> seconds.
    /// </summary>
    // F5: a step is spent by being passed, and answering it without spending it would let the
    // loop run the same frame twice.
    public double Advance(double real)
    {
        lock (_lock)
        {
            if (_pending > 0)
            {
                _pending--;
                _passed++;
                if (_pending == 0)
                {
                    Release();
                }

                return StepDelta;
            }

            return _paused ? 0 : real;
        }
    }

    /// <summary>Holds time still. Ends a step that has frames left.</summary>
    public void Pause()
    {
        lock (_lock)
        {
            _paused = true;
            _pending = 0;
            Release();
        }
    }

    /// <summary>Lets time run. Ends a step that has frames left.</summary>
    public void Resume()
    {
        lock (_lock)
        {
            _paused = false;
            _pending = 0;
            Release();
        }
    }

    /// <summary>
    /// Pauses, then passes <paramref name="frames"/> frames of <see cref="StepDelta"/> each.
    /// Completes when the last of them has been passed, or when a pause or a resume cancels the
    /// rest. Throws <see cref="ArgumentOutOfRangeException"/> for fewer than one frame.
    /// </summary>
    public Task Step(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frames, 1);
        lock (_lock)
        {
            _paused = true;
            _pending += frames;
            _stepped ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _stepped.Task;
        }
    }

    // Called with the lock held.
    private void Release()
    {
        _stepped?.SetResult();
        _stepped = null;
    }
}
