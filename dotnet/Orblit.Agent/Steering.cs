using Orblit.VectorMath;

namespace Orblit.Agent;

/// <summary>
/// What a steering behaviour is given to decide with.
///
/// Deliberately not the agent itself. A behaviour that held a reference to
/// whatever is moving would be a behaviour that could only be used by that
/// kind of thing, and half the point of steering is that the same
/// <see cref="Seek"/> moves a bird, a boat and a cursor.
/// </summary>
public class Steerable(
    Vector3? position = null,
    Vector3? velocity = null,
    double maxSpeed = 4,
    double maxForce = 8,
    double radius = 0.5,
    long priority = 0)
{
    public Vector3 Position { get; set; } = position ?? Vector3.Zero();

    public Vector3 Velocity { get; set; } = velocity ?? Vector3.Zero();

    /// <summary>How fast it may end up going, in metres a second.</summary>
    public double MaxSpeed { get; } = maxSpeed;

    /// <summary>
    /// How hard it may be pushed, which is what turns a jerk into a turn.
    ///
    /// The difference between the two is the whole feel of a mover: a high
    /// force against a low speed is something nimble, and a low force against a
    /// high speed is something with mass.
    /// </summary>
    public double MaxForce { get; } = maxForce;

    /// <summary>How wide it is, for the behaviours that care about touching.</summary>
    public double Radius { get; } = radius;

    /// <summary>
    /// Who gives way. <see cref="Avoid"/> steps round a neighbour of the same
    /// priority or higher and ignores a lower one, which is expected to step
    /// round it.
    /// </summary>
    public long Priority { get; } = priority;

    public double Speed => Velocity.Length;

    /// <summary>
    /// Where it is facing, or null when it is not moving.
    ///
    /// Null rather than a stale direction, because "which way is it pointing"
    /// has no answer for something standing still, and the usual fudge, keeping
    /// the last one, makes a mover that stops and starts snap to an angle it
    /// had a minute ago.
    /// </summary>
    public Vector3? Heading
    {
        get
        {
            var length = Velocity.Length;
            return length < 1e-9 ? null : Velocity / length;
        }
    }

    /// <summary>Moves by <paramref name="force"/> over <paramref name="seconds"/>, clamped to what it is capable of.</summary>
    public void Integrate(Vector3 force, double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        var push = SteeringMath.Truncate(force, MaxForce);
        Velocity += push * seconds;
        Velocity = SteeringMath.Truncate(Velocity, MaxSpeed);
        Position += Velocity * seconds;
    }
}

internal static class SteeringMath
{
    /// <summary>A force, capped at a length.</summary>
    internal static Vector3 Truncate(Vector3 value, double most)
    {
        var length = value.Length;
        if (length <= most || length < 1e-12)
        {
            return value;
        }

        return value * (most / length);
    }
}

/// <summary>
/// One reason to move.
///
/// A behaviour answers "which way, and how hard" and nothing else. It does
/// not move anything, it does not decide whether it should be running, and it
/// does not know about the others, which is what lets several of them be
/// added together, and what makes each one testable on its own.
/// </summary>
public abstract class Steering
{
    /// <summary>The force this behaviour wants applied, in world space.</summary>
    public abstract Vector3 Force(Steerable self);
}

/// <summary>Straight at a point, at full speed.</summary>
public class Seek(Vector3 target) : Steering
{
    public Vector3 Target { get; } = target;

    public override Vector3 Force(Steerable self)
    {
        var offset = Target - self.Position;
        if (offset.Length < 1e-9)
        {
            return Vector3.Zero();
        }

        // The force is the difference between the velocity wanted and the one
        // there is, which is what makes a mover turn rather than stop and start
        // again in the new direction.
        return (offset.Normalized() * self.MaxSpeed) - self.Velocity;
    }
}

/// <summary>Straight away from a point.</summary>
public class Flee(Vector3 from, double within = double.PositiveInfinity) : Steering
{
    public Vector3 From { get; } = from;

    /// <summary>
    /// How close it has to be before this cares. Beyond it, nothing.
    ///
    /// Without a limit a thing being fled is a thing fled from for ever,
    /// including from the other side of the map, which reads as a mover that
    /// will not settle.
    /// </summary>
    public double Within { get; } = within;

    public override Vector3 Force(Steerable self)
    {
        var offset = self.Position - From;
        var distance = offset.Length;
        if (distance < 1e-9 || distance > Within)
        {
            return Vector3.Zero();
        }

        return (offset.Normalized() * self.MaxSpeed) - self.Velocity;
    }
}

/// <summary>At a point, slowing to a stop on it.</summary>
public class Arrive(Vector3 target, double slowingRadius = 3) : Steering
{
    public Vector3 Target { get; } = target;

    /// <summary>
    /// How far out it starts slowing down.
    ///
    /// What separates arriving from seeking is the speed on approach, not
    /// whether it overshoots: a steering force is a difference between the
    /// velocity wanted and the one there is, applied over a frame, so it lags
    /// and something coming in fast will still sail past. What this buys is
    /// that it comes in slowly, and therefore sails past by little and settles
    /// instead of circling.
    ///
    /// Wide enough to stop in, then. The braking a ramp asks for is
    /// v²/2d. Four metres a second wanting to stop in three metres asks for
    /// under three metres a second squared, which is well inside a default
    /// agent's means. Halve the radius and it is not.
    /// </summary>
    public double SlowingRadius { get; } = slowingRadius;

    public override Vector3 Force(Steerable self)
    {
        var offset = Target - self.Position;
        var distance = offset.Length;
        if (distance < 1e-9)
        {
            return -self.Velocity;
        }

        var wanted = distance < SlowingRadius && SlowingRadius > 0
            ? self.MaxSpeed * (distance / SlowingRadius)
            : self.MaxSpeed;
        return (offset.Normalized() * wanted) - self.Velocity;
    }
}

/// <summary>
/// At where something will be, rather than where it is.
///
/// Looking ahead by how long it would take to get there, so a chase closes on
/// the target instead of trailing behind it. A pursuer that aims at the
/// present position is always one step late and ends up describing a curve
/// behind its quarry.
/// </summary>
public class Pursue(Steerable quarry, double lookAhead = 1.0) : Steering
{
    public Steerable Quarry { get; } = quarry;

    /// <summary>A ceiling on how far into the future to aim, in seconds.</summary>
    public double LookAhead { get; } = lookAhead;

    public override Vector3 Force(Steerable self)
    {
        var offset = Quarry.Position - self.Position;
        var closing = Math.Max(self.MaxSpeed, 1e-6);
        var ahead = Math.Min(offset.Length / closing, LookAhead);
        return new Seek(Quarry.Position + (Quarry.Velocity * ahead)).Force(self);
    }
}

/// <summary>Away from where something will be.</summary>
public class Evade(Steerable hunter, double lookAhead = 1.0, double within = 12) : Steering
{
    public Steerable Hunter { get; } = hunter;

    public double LookAhead { get; } = lookAhead;

    public double Within { get; } = within;

    public override Vector3 Force(Steerable self)
    {
        var offset = Hunter.Position - self.Position;
        var closing = Math.Max(self.MaxSpeed, 1e-6);
        var ahead = Math.Min(offset.Length / closing, LookAhead);
        return new Flee(Hunter.Position + (Hunter.Velocity * ahead), within: Within).Force(self);
    }
}

/// <summary>
/// Nowhere in particular, but not at random either.
///
/// A force drawn from noise rather than from a fresh random number each tick.
/// That is the whole trick: independent random numbers average to nothing and
/// produce a mover that vibrates on the spot, while a value that walks
/// smoothly produces one that ambles. The walk is deterministic in
/// <see cref="Seed"/> and the time it is given, so the same agent wanders the
/// same way twice, which is what makes a wandering crowd reproducible in a test
/// and in a replay.
/// </summary>
public class Wander(long seed, double at, double strength = 1, double rate = 0.35) : Steering
{
    /// <summary>This agent's own number, so two agents side by side do not wander in step.</summary>
    public long Seed { get; } = seed;

    /// <summary>The clock, in seconds.</summary>
    public double At { get; } = at;

    /// <summary>How hard it pushes, as a fraction of what the agent can manage.</summary>
    public double Strength { get; } = strength;

    /// <summary>How quickly the direction drifts.</summary>
    public double Rate { get; } = rate;

    public override Vector3 Force(Steerable self)
    {
        var t = At * Rate;

        // Three independent walks, one per axis, offset from each other so they
        // are not the same curve three times over.
        var direction = new Vector3(
            Walk(Seed, t),
            Walk(unchecked(Seed + 8191), t) * 0.25,
            Walk(unchecked(Seed + 16381), t));
        if (direction.Length < 1e-9)
        {
            return Vector3.Zero();
        }

        return direction.Normalized() * (self.MaxForce * Strength);
    }

    /// <summary>
    /// A value between -1 and 1 that moves smoothly with <paramref name="t"/>.
    ///
    /// Value noise: the integers either side are hashed to fixed numbers and
    /// what is between them is faded across with a curve whose slope is zero at
    /// both ends. Linear interpolation would leave a kink at every integer, and
    /// a kink in a steering force reads as the agent flinching once a second.
    /// </summary>
    private static double Walk(long seed, double t)
    {
        var floor = Doubles.Floor(t);
        var fraction = t - floor;
        var a = Hash(seed, floor);
        var b = Hash(seed, unchecked(floor + 1));
        var blend = fraction * fraction * (3 - (2 * fraction));
        return a + ((b - a) * blend);
    }

    /// <summary>One integer to one number between -1 and 1, always the same one.</summary>
    private static double Hash(long seed, long at)
    {
        // The multiplies wrap in 64 bits, as Dart's ints do, and the mask keeps the result positive.
        var x = unchecked((at * 374761393) + (seed * 668265263)) & 0x7FFFFFFF;
        x = unchecked((x ^ (x >> 13)) * 1274126177) & 0x7FFFFFFF;
        x ^= x >> 16;
        return ((x % 20001) / 10000.0) - 1.0;
    }
}

/// <summary>
/// Away from whatever is too close.
///
/// The force falls off with distance, so a neighbour just inside the radius
/// nudges and one about to be walked into shoves. A flat force instead makes
/// a crowd that shuffles as a block.
/// </summary>
public class Separate(IReadOnlyList<Steerable> neighbours, double radius = 2) : Steering
{
    public IReadOnlyList<Steerable> Neighbours { get; } = neighbours;

    public double Radius { get; } = radius;

    public override Vector3 Force(Steerable self)
    {
        var away = Vector3.Zero();
        var found = 0;

        foreach (var other in Neighbours)
        {
            if (ReferenceEquals(other, self))
            {
                continue;
            }

            var offset = self.Position - other.Position;
            var distance = offset.Length;
            if (distance < 1e-9 || distance > Radius)
            {
                continue;
            }

            // How far inside the radius it is: nothing at the edge, everything at
            // contact. The magnitude has to survive to the end. Normalising the
            // total away, which is the usual way this is written, throws the
            // falloff out and leaves a crowd that shuffles as a block.
            away += offset.Normalized() * ((Radius - distance) / Radius);
            found++;
        }

        if (found == 0)
        {
            return Vector3.Zero();
        }

        away /= found;

        var urgency = away.Length;
        if (urgency < 1e-9)
        {
            return Vector3.Zero();
        }

        return (away.Normalized() * (self.MaxSpeed * Doubles.Clamp(urgency, 0.0, 1.0))) - self.Velocity;
    }
}

/// <summary>
/// Steps round whatever it would walk into.
///
/// Of the velocities it could take, it picks the one that stays nearest to
/// <see cref="Preferred"/> and hits no neighbour within <see cref="Horizon"/>
/// seconds, or hits the latest one. A neighbour is assumed to keep the velocity
/// it has, so two agents meeting head on both turn the same way about the
/// vertical axis and pass on opposite sides. The candidates and their order are
/// fixed, so the same crowd in the same order gives the same run.
///
/// <see cref="Preferred"/> is the velocity the route wants, such as a
/// <see cref="Seek"/> without the subtraction. It is capped at the agent's top
/// speed. Neighbours of lower <see cref="Steerable.Priority"/> are not avoided.
/// The force is the velocity chosen minus the one it has, as
/// <see cref="Seek"/> gives, so it blends with the others.
///
/// shortcut: every neighbour in the list is looked at, so a crowd of n costs
/// n squared. Fine for a few hundred. Upgrade to a cell index for the
/// neighbours before a crowd is in the thousands.
/// </summary>
public sealed class Avoid : Steering
{
    private static readonly int[] Signs = [1, -1];

    public Avoid(
        Vector3 preferred,
        IReadOnlyList<Steerable> neighbours,
        double horizon = 2,
        long turns = 8,
        double weight = 2)
    {
        if (!(horizon > 0 && double.IsFinite(horizon)))
        {
            throw new ArgumentOutOfRangeException(nameof(horizon), horizon, "Must be above zero.");
        }

        if (turns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(turns), turns, "Must be at least one.");
        }

        if (!(weight >= 0 && double.IsFinite(weight)))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Must be zero or more.");
        }

        Preferred = preferred;
        Neighbours = neighbours;
        Horizon = horizon;
        Turns = turns;
        Weight = weight;
    }

    public Vector3 Preferred { get; }

    public IReadOnlyList<Steerable> Neighbours { get; }

    /// <summary>How far ahead, in seconds, a hit is looked for.</summary>
    public double Horizon { get; }

    /// <summary>How many steps of turn it tries from straight on to straight back, each side.</summary>
    public long Turns { get; }

    /// <summary>
    /// How much a near hit counts against a change of velocity. The cost of a
    /// hit is this over the seconds until it, against the metres a second the
    /// velocity moved from <see cref="Preferred"/>.
    /// </summary>
    public double Weight { get; }

    public override Vector3 Force(Steerable self)
    {
        var wanted = SteeringMath.Truncate(Preferred, self.MaxSpeed);
        var threats = Neighbours.Where(other => Matters(self, other)).ToList();
        if (threats.Count == 0)
        {
            return wanted - self.Velocity;
        }

        var best = wanted;
        var bestCost = double.PositiveInfinity;
        foreach (var candidate in Candidates(wanted))
        {
            var cost = Cost(self, candidate, wanted, threats);
            if (cost < bestCost)
            {
                best = candidate;
                bestCost = cost;
            }
        }

        return best - self.Velocity;
    }

    private bool Matters(Steerable self, Steerable other)
    {
        if (other.Priority < self.Priority)
        {
            return false;
        }

        var reach = ((self.MaxSpeed + other.MaxSpeed) * Horizon) + self.Radius + other.Radius;
        return self.Position.DistanceTo(other.Position) <= reach;
    }

    /// <summary>
    /// The velocity wanted first, then turns of it either side in growing
    /// steps, positive first, each at full speed and at half, then standing
    /// still. Positive turns an agent heading along +x towards -z, which is the
    /// hand every agent favours when two meet head on.
    /// </summary>
    private IEnumerable<Vector3> Candidates(Vector3 wanted)
    {
        yield return wanted;
        for (long step = 1; step <= Turns; step++)
        {
            foreach (var sign in Signs)
            {
                var turned = Turned(wanted, sign * step * Math.PI / Turns);
                yield return turned;
                yield return turned * 0.5;
            }
        }

        yield return wanted * 0.5;
        yield return Vector3.Zero();
    }

    private double Cost(Steerable self, Vector3 candidate, Vector3 wanted, List<Steerable> threats)
    {
        var soonest = double.PositiveInfinity;
        foreach (var other in threats)
        {
            var hit = TimeToHit(self, other, candidate);
            if (hit < soonest)
            {
                soonest = hit;
            }
        }

        var change = candidate.DistanceTo(wanted);
        if (soonest >= Horizon)
        {
            return change;
        }

        // Floored so a hit this instant costs a large number. Divided by zero it
        // would be infinity, and a weight of zero would make it not a number.
        return change + (Weight / Math.Max(soonest, 1e-3));
    }

    /// <summary>
    /// Seconds until <paramref name="self"/>, moving at <paramref name="velocity"/>, touches
    /// <paramref name="other"/>, moving as it is, or infinity if it never does. Zero if they
    /// already touch and are closing.
    /// </summary>
    private static double TimeToHit(Steerable self, Steerable other, Vector3 velocity)
    {
        var offset = other.Position - self.Position;
        var relative = velocity - other.Velocity;
        var reach = self.Radius + other.Radius;
        var closing = offset.Dot(relative);
        var gap = offset.Dot(offset) - (reach * reach);
        if (gap < 0)
        {
            return closing > 0 ? 0 : double.PositiveInfinity;
        }

        var speedSquared = relative.Dot(relative);
        if (closing <= 0 || speedSquared < 1e-12)
        {
            return double.PositiveInfinity;
        }

        var discriminant = (closing * closing) - (speedSquared * gap);
        if (discriminant <= 0)
        {
            return double.PositiveInfinity;
        }

        return (closing - Math.Sqrt(discriminant)) / speedSquared;
    }

    /// <summary><paramref name="v"/> turned by <paramref name="angle"/> about the vertical axis.</summary>
    private static Vector3 Turned(Vector3 v, double angle)
    {
        var c = Math.Cos(angle);
        var s = Math.Sin(angle);
        return new Vector3((v.X * c) + (v.Z * s), v.Y, (-v.X * s) + (v.Z * c));
    }
}

/// <summary>Facing the way the neighbours are facing.</summary>
public class Align(IReadOnlyList<Steerable> neighbours, double radius = 4) : Steering
{
    public IReadOnlyList<Steerable> Neighbours { get; } = neighbours;

    public double Radius { get; } = radius;

    public override Vector3 Force(Steerable self)
    {
        var heading = Vector3.Zero();
        var found = 0;

        foreach (var other in Neighbours)
        {
            if (ReferenceEquals(other, self))
            {
                continue;
            }

            if ((other.Position - self.Position).Length > Radius)
            {
                continue;
            }

            heading += other.Velocity;
            found++;
        }

        if (found == 0 || heading.Length < 1e-9)
        {
            return Vector3.Zero();
        }

        return (heading.Normalized() * self.MaxSpeed) - self.Velocity;
    }
}

/// <summary>Towards where the neighbours are.</summary>
public class Cohere(IReadOnlyList<Steerable> neighbours, double radius = 5) : Steering
{
    public IReadOnlyList<Steerable> Neighbours { get; } = neighbours;

    public double Radius { get; } = radius;

    public override Vector3 Force(Steerable self)
    {
        var centre = Vector3.Zero();
        var found = 0;

        foreach (var other in Neighbours)
        {
            if (ReferenceEquals(other, self))
            {
                continue;
            }

            if ((other.Position - self.Position).Length > Radius)
            {
                continue;
            }

            centre += other.Position;
            found++;
        }

        if (found == 0)
        {
            return Vector3.Zero();
        }

        return new Seek(centre / found).Force(self);
    }
}

/// <summary>
/// Along a line of points, without stopping at each one.
///
/// Aims at the point after the one it is closest to reaching, so a path is
/// followed as a route rather than as a series of destinations. An agent
/// that arrives at every waypoint in turn stops dead at each corner.
/// </summary>
public class FollowPath(IReadOnlyList<Vector3> points, long index, double reached = 1.0, bool loop = false) : Steering
{
    public IReadOnlyList<Vector3> Points { get; } = points;

    /// <summary>Which point it is heading for.</summary>
    public long Index { get; } = index;

    /// <summary>How close counts as having got there.</summary>
    public double Reached { get; } = reached;

    public bool Loop { get; } = loop;

    /// <summary>
    /// The point to head for after this force has been applied.
    ///
    /// Returned rather than written, because a behaviour that advanced an index
    /// as a side effect of being asked for a force could not be asked twice,
    /// and being asked twice is what a test and a debug overlay both do.
    /// </summary>
    public long NextIndex(Steerable self)
    {
        if (Points.Count == 0)
        {
            return Index;
        }

        var at = Math.Clamp(Index, 0L, Points.Count - 1L);
        if ((Points[(int)at] - self.Position).Length > Reached)
        {
            return at;
        }

        if (at + 1 < Points.Count)
        {
            return at + 1;
        }

        return Loop ? 0 : at;
    }

    /// <summary>Whether the path has been walked to its end.</summary>
    public bool IsDone(Steerable self) =>
        !Loop
        && Points.Count != 0
        && Index >= Points.Count - 1
        && (Points[^1] - self.Position).Length <= Reached;

    public override Vector3 Force(Steerable self)
    {
        if (Points.Count == 0)
        {
            return Vector3.Zero();
        }

        var at = Math.Clamp(Index, 0L, Points.Count - 1L);

        // The last point is arrived at rather than seeked, so a route ends with
        // the agent stopped on it instead of circling it.
        var isLast = !Loop && at == Points.Count - 1;
        return isLast
            ? new Arrive(Points[(int)at], slowingRadius: Math.Max(Reached * 2, 1)).Force(self)
            : new Seek(Points[(int)at]).Force(self);
    }
}

/// <summary>
/// Several reasons to move, weighed against each other.
///
/// Added together and truncated rather than picked between. Blending is what
/// makes a crowd read as a crowd: an agent that chose one behaviour per tick
/// snaps between them, and the snap is visible however good each behaviour is.
///
/// The order matters only for what survives the truncation, which is why the
/// urgent ones, separation and avoidance, belong first: everything is summed,
/// but if the total is over what the agent can push, what is left is
/// dominated by whatever contributed most.
/// </summary>
public class Blend(IReadOnlyList<(Steering Behaviour, double Weight)> parts) : Steering
{
    /// <summary>Each behaviour and how much of it, as a fraction.</summary>
    public IReadOnlyList<(Steering Behaviour, double Weight)> Parts { get; } = parts;

    public override Vector3 Force(Steerable self)
    {
        var total = Vector3.Zero();
        foreach (var (behaviour, weight) in Parts)
        {
            if (weight == 0)
            {
                continue;
            }

            total += behaviour.Force(self) * weight;
        }

        return SteeringMath.Truncate(total, self.MaxForce);
    }
}
