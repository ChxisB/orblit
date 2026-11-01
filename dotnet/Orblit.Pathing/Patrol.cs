using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>A place to go and how long to wait there.</summary>
public sealed class PatrolStop
{
    private readonly Vector3 _at;

    public PatrolStop(Vector3 at, double wait = 0)
    {
        if (wait < 0 || !double.IsFinite(wait))
        {
            throw new ArgumentException("Must be zero or more.", nameof(wait));
        }

        _at = at.Clone();
        Wait = wait;
    }

    public Vector3 At => _at.Clone();

    /// <summary>Seconds to stay once there.</summary>
    public double Wait { get; }
}

/// <summary>
/// Which stop an agent is heading for, and how long it has waited at the
/// last one. The agent does the walking; this says where to.
/// </summary>
public sealed class Patrol
{
    private int _index;
    private int _heading = 1;
    private double _waited;

    public Patrol(IReadOnlyList<PatrolStop> stops, PathEnd end = PathEnd.Loop, double reached = 0.5)
    {
        Stops = stops.ToList().AsReadOnly();
        if (Stops.Count == 0)
        {
            throw new ArgumentException("A patrol needs a stop.", nameof(stops));
        }

        End = end;
        Reached = reached;
    }

    public IReadOnlyList<PatrolStop> Stops { get; }

    /// <summary>
    /// What happens after the last stop: <see cref="PathEnd.Stop"/> ends the patrol, loop
    /// goes back to the first, and pingPong goes back through them.
    /// </summary>
    public PathEnd End { get; }

    /// <summary>How close counts as being there, in metres.</summary>
    public double Reached { get; }

    /// <summary>The stop it is heading for, or null once a patrol that ends is over.</summary>
    public PatrolStop? Stop => IsFinished ? null : Stops[_index];

    public Vector3? Target => Stop?.At;

    /// <summary>Whether it has arrived and is standing out the stop's wait.</summary>
    public bool IsWaiting => _waited > 0;

    public bool IsFinished { get; private set; }

    /// <summary>
    /// Tells the patrol where the agent is, after <paramref name="seconds"/> of walking. At a
    /// stop it counts its wait down, and moves to the next when that is over.
    /// </summary>
    public void Update(Vector3 position, double seconds)
    {
        if (Stop is not { } current)
        {
            return;
        }

        if (position.DistanceTo(current.At) > Reached)
        {
            _waited = 0;
            return;
        }

        _waited += seconds;
        if (_waited >= current.Wait)
        {
            Advance();
        }
    }

    private void Advance()
    {
        _waited = 0;
        var last = Stops.Count - 1;
        var next = _index + _heading;
        if (next >= 0 && next <= last)
        {
            _index = next;
            return;
        }

        switch (End)
        {
            case PathEnd.Stop:
                IsFinished = true;
                break;
            case PathEnd.Loop:
                _index = next < 0 ? last : 0;
                break;
            case PathEnd.PingPong:
                _heading = -_heading;
                _index = Math.Clamp(_index + _heading, 0, last);
                break;
            default:
                throw new InvalidOperationException($"Not a path end: {End}");
        }
    }
}
