using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>What something does on reaching the end of a path.</summary>
public enum PathEnd
{
    /// <summary>Stays there.</summary>
    Stop,

    /// <summary>Goes back to the start and runs it again.</summary>
    Loop,

    /// <summary>Turns round and runs it the other way, and again.</summary>
    PingPong,
}

/// <summary>Something riding a <see cref="Spline"/> at a speed, a distance at a time.</summary>
public sealed class PathFollower
{
    private double _heading = 1;

    public PathFollower(Spline spline, double speed = 4, PathEnd end = PathEnd.Stop, double distance = 0)
    {
        Spline = spline;
        Speed = speed;
        End = end;
        Distance = Doubles.Clamp(distance, 0.0, spline.Length);
    }

    public Spline Spline { get; }

    /// <summary>Metres a second. Negative runs the path backwards.</summary>
    public double Speed { get; set; }

    public PathEnd End { get; }

    /// <summary>How far along the spline it is, in metres.</summary>
    public double Distance { get; private set; }

    /// <summary>Whether a follower that stops has reached the end it is heading for.</summary>
    public bool IsFinished
    {
        get
        {
            if (End != PathEnd.Stop)
            {
                return false;
            }

            var goal = Speed * _heading >= 0 ? Spline.Length : 0.0;
            return Math.Abs(Distance - goal) < 1e-9;
        }
    }

    public PathFrame Frame => Spline.FrameAt(Distance);

    /// <summary>Rides on for <paramref name="seconds"/>.</summary>
    public void Advance(double seconds)
    {
        var length = Spline.Length;
        if (length <= 0 || seconds <= 0)
        {
            return;
        }

        var next = Distance + (Speed * _heading * seconds);
        Distance = End switch
        {
            PathEnd.Stop => Doubles.Clamp(next, 0.0, length),
            PathEnd.Loop => Wrapped(next, length),
            PathEnd.PingPong => Bounced(next, length),
            _ => throw new InvalidOperationException($"Not a path end: {End}"),
        };
    }

    private static double Wrapped(double value, double length)
    {
        var wrapped = Doubles.Mod(value, length);
        return wrapped < 0 ? wrapped + length : wrapped;
    }

    /// <summary>Folds <paramref name="value"/> back off each end, flipping the heading for every fold.</summary>
    private double Bounced(double value, double length)
    {
        var at = value;
        while (at < 0 || at > length)
        {
            _heading = -_heading;
            at = at < 0 ? -at : (2 * length) - at;
        }

        return at;
    }
}

/// <summary>
/// A slider on a <see cref="Spline"/> that follows a target by sliding to the nearest
/// point of the curve: a camera on a dolly track, a cart on a line.
/// </summary>
public sealed class Rail
{
    public Rail(Spline spline, double speed = 6, double distance = 0)
    {
        Spline = spline;
        Speed = speed;
        Distance = Doubles.Clamp(distance, 0.0, spline.Length);
    }

    public Spline Spline { get; }

    /// <summary>How fast it can slide, in metres a second.</summary>
    public double Speed { get; set; }

    public double Distance { get; private set; }

    public Vector3 Position => Spline.PositionAt(Distance);

    /// <summary>
    /// Slides towards the point of the rail nearest <paramref name="target"/>, for <paramref name="seconds"/>.
    /// A closed rail goes whichever way round is shorter.
    /// </summary>
    public void Track(Vector3 target, double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        var goal = Spline.Closest(target).Distance;
        var gap = goal - Distance;
        var length = Spline.Length;
        var round = Spline.Closed && length > 0;
        if (round)
        {
            gap = Doubles.Mod(gap + (length / 2), length) - (length / 2);
        }

        var most = Speed * seconds;
        var move = Doubles.Clamp(gap, -most, most);
        Distance = round ? Distance + move : Math.Max(0.0, Math.Min(length, Distance + move));
    }
}
