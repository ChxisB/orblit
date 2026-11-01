using Orblit.VectorMath;

namespace Orblit.Agent.Tests;

public class AvoidTests
{
    private static Steerable Walker(double x, double z, double vx = 0, long priority = 0, double speed = 2) =>
        new(
            position: new Vector3(x, 0, z),
            velocity: new Vector3(vx, 0, 0),
            maxSpeed: speed,
            maxForce: 20,
            radius: 0.4,
            priority: priority);

    /// <summary>The velocity an <see cref="Avoid"/> settles on for <paramref name="self"/> wanting to go <paramref name="wanted"/>.</summary>
    private static Vector3 Chosen(Steerable self, Vector3 wanted, IReadOnlyList<Steerable> others) =>
        new Avoid(wanted, others).Force(self) + self.Velocity;

    // avoidance

    [Fact]
    public void GoesWhereTheRouteSaysWhenNothingIsNear()
    {
        var self = Walker(0, 0, vx: 1);
        var far = Walker(40, 0, vx: -1);

        var force = new Avoid(new Vector3(2, 0, 0), [far]).Force(self);

        Assert.Equal(1, force.X, 1e-9);
        Assert.Equal(0, force.Z, 1e-9);
    }

    [Fact]
    public void CapsWhatTheRouteWantsAtTheTopSpeed()
    {
        var self = Walker(0, 0, speed: 2);

        var force = new Avoid(new Vector3(10, 0, 0), []).Force(self);

        Assert.Equal(2, force.Length, 1e-9);
    }

    [Fact]
    public void IsNotTroubledByItself()
    {
        var self = Walker(0, 0);

        var force = new Avoid(new Vector3(2, 0, 0), [self]).Force(self);

        Assert.Equal(2, force.X, 1e-9);
    }

    [Fact]
    public void IgnoresANeighbourThatIsMovingAway()
    {
        var self = Walker(0, 0, vx: 2);
        var ahead = Walker(1.5, 0, vx: 2);

        var velocity = Chosen(self, new Vector3(2, 0, 0), [ahead]);

        Assert.Equal(2, velocity.X, 1e-9);
        Assert.Equal(0, velocity.Z, 1e-9);
    }

    [Fact]
    public void TurnsOffTheLineToMissSomeoneComingTheOtherWay()
    {
        var self = Walker(0, 0, vx: 2);
        var oncoming = Walker(6, 0, vx: -2);

        var velocity = Chosen(self, new Vector3(2, 0, 0), [oncoming]);

        Assert.True(Math.Abs(velocity.Z) > 0.1);
        Assert.True(velocity.X > 0, "still making headway");
    }

    [Fact]
    public void TwoMeetingHeadOnPassOnOppositeSides()
    {
        var a = Walker(0, 0, vx: 2);
        var b = Walker(6, 0, vx: -2);

        var va = Chosen(a, new Vector3(2, 0, 0), [b]);
        var vb = Chosen(b, new Vector3(-2, 0, 0), [a]);

        Assert.True(va.Z < 0, "positive turns first");
        Assert.True(vb.Z > 0);
    }

    [Fact]
    public void OnlyLooksAsFarAsAnyoneCouldGetWithinTheHorizon()
    {
        var self = Walker(0, 0, vx: 2);

        // Two a second each way for two seconds is 8, and the radii add 0.8.
        var inReach = Walker(8.5, 0);
        inReach.Velocity = new Vector3(-10, 0, 0);
        var beyond = Walker(9, 0);
        beyond.Velocity = new Vector3(-10, 0, 0);

        var near = Chosen(self, new Vector3(2, 0, 0), [inReach]);
        var far = Chosen(self, new Vector3(2, 0, 0), [beyond]);

        Assert.True(Math.Abs(near.Z) > 0.1);
        Assert.Equal(0, far.Z, 1e-9);
    }

    [Fact]
    public void GivesTheSameAnswerEveryTimeItIsAsked()
    {
        var self = Walker(0, 0, vx: 2);
        var others = new[] { Walker(5, 0.1, vx: -2), Walker(4, -1, vx: -1) };

        var first = Chosen(self, new Vector3(2, 0, 0), others);
        var second = Chosen(self, new Vector3(2, 0, 0), others);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DoesNotStepRoundSomeoneOfLowerPriority()
    {
        var self = Walker(0, 0, vx: 2, priority: 1);
        var lesser = Walker(6, 0, vx: -2);

        var velocity = Chosen(self, new Vector3(2, 0, 0), [lesser]);

        Assert.Equal(2, velocity.X, 1e-9);
        Assert.Equal(0, velocity.Z, 1e-9);
    }

    [Fact]
    public void StepsRoundSomeoneOfHigherPriority()
    {
        var self = Walker(0, 0, vx: 2);
        var greater = Walker(6, 0, vx: -2, priority: 1);

        var velocity = Chosen(self, new Vector3(2, 0, 0), [greater]);

        Assert.True(Math.Abs(velocity.Z) > 0.1);
    }

    [Fact]
    public void BacksOffFromSomeoneItAlreadyOverlapsAndIsClosingOn()
    {
        var self = Walker(0, 0, vx: 1);
        var inside = Walker(0.5, 0);

        var velocity = Chosen(self, new Vector3(2, 0, 0), [inside]);

        var closing = (inside.Position - self.Position).Dot(velocity);
        Assert.True(closing <= 0);
    }

    [Fact]
    public void StopsForAWallOfNeighboursItCannotTurnRoundIn()
    {
        var self = Walker(0, 0, vx: 2);
        var wall = new List<Steerable>();
        for (var z = -6.0; z <= 6; z += 0.8)
        {
            wall.Add(Walker(1.2, z, speed: 0));
        }

        var velocity = Chosen(self, new Vector3(2, 0, 0), wall);

        Assert.True(velocity.Length < 1);
    }

    [Fact]
    public void GoesBackWhenNoTurnOfItGetsClear()
    {
        // One turn to try is straight back. Stopping would be run into inside
        // a second, and the oncoming walker is as fast, so going back is the
        // only choice that is not hit.
        var self = Walker(0, 0, vx: 2);
        var oncoming = Walker(2.5, 0, vx: -2);

        var velocity = new Avoid(new Vector3(2, 0, 0), [oncoming], turns: 1).Force(self) + self.Velocity;

        Assert.Equal(-2, velocity.X, 1e-9);
        Assert.Equal(0, velocity.Z, 1e-9);
    }

    [Fact]
    public void RefusesWhatCannotWork()
    {
        Avoid Make(double horizon = 2, long turns = 8, double weight = 2) =>
            new(Vector3.Zero(), [], horizon: horizon, turns: turns, weight: weight);

        Assert.ThrowsAny<ArgumentException>(() => Make(horizon: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(horizon: double.PositiveInfinity));
        Assert.ThrowsAny<ArgumentException>(() => Make(horizon: double.NaN));
        Assert.ThrowsAny<ArgumentException>(() => Make(turns: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(weight: -1));
        Assert.ThrowsAny<ArgumentException>(() => Make(weight: double.NaN));
        Assert.ThrowsAny<ArgumentException>(() => Make(weight: double.PositiveInfinity));
        Assert.IsType<Avoid>(Make(weight: 0));
        Assert.IsType<Avoid>(Make(turns: 1));
    }

    // a crowd

    /// <summary>
    /// Fifty walkers a side crossing a plaza in opposite directions, each
    /// starting in its own slot of a grid so none starts touching. The lanes
    /// of the two sides are two metres apart each and interleaved, so a lane
    /// is a metre wide for a walker 0.8 wide. Packed tighter than that, a
    /// head on crowd jams and overlaps by half a metre or more.
    /// </summary>
    internal static List<(Steerable Agent, Vector3 Goal)> Plaza()
    {
        var crowd = new List<(Steerable Agent, Vector3 Goal)>();
        for (var i = 0; i < 50; i++)
        {
            var row = i % 10;
            var column = i / 10;
            var z = (row * 2.0) - 9;
            var eastbound = new Steerable(
                position: new Vector3(-12 - (column * 1.1), 0, z),
                maxSpeed: 2,
                maxForce: 20,
                radius: 0.4);
            var westbound = new Steerable(
                position: new Vector3(12 + (column * 1.1), 0, z + 1.0),
                maxSpeed: 2,
                maxForce: 20,
                radius: 0.4);
            crowd.Add((eastbound, new Vector3(24, 0, z)));
            crowd.Add((westbound, new Vector3(-24, 0, z + 1.0)));
        }

        return crowd;
    }

    /// <summary>
    /// Walks the crowd <paramref name="seconds"/> in the order it is listed. Returns the longest
    /// time any pair stayed overlapping by more than <paramref name="depth"/> metres.
    /// </summary>
    internal static (double Longest, List<Vector3> End) Cross(
        List<(Steerable Agent, Vector3 Goal)> crowd,
        double seconds = 40,
        double depth = 0.1)
    {
        const double step = 1.0 / 20;
        var agents = crowd.Select(entry => entry.Agent).ToList();
        var together = new Dictionary<int, double>();
        var longest = 0.0;
        for (var t = 0.0; t < seconds; t += step)
        {
            foreach (var entry in crowd)
            {
                var toGoal = entry.Goal - entry.Agent.Position;
                var wanted = toGoal.Length < 0.1
                    ? Vector3.Zero()
                    : toGoal.Normalized() * entry.Agent.MaxSpeed;
                entry.Agent.Integrate(new Avoid(wanted, agents).Force(entry.Agent), step);
            }

            longest = Math.Max(longest, Overlaps(agents, together, step, depth));
        }

        return (longest, agents.Select(a => a.Position.Clone()).ToList());
    }

    [Fact]
    public void CrossesWithoutWalkingThroughItself()
    {
        var crowd = Plaza();

        var result = Cross(crowd);

        Assert.True(result.Longest <= 1);

        // Five to a lane want one point, so the last of them queues behind the
        // first. All that is asked is that each got past the far side's start.
        foreach (var entry in crowd)
        {
            Assert.True(Math.Sign(entry.Goal.X) * entry.Agent.Position.X > 17, "got across the plaza");
        }
    }

    [Fact]
    public void RepeatsExactly()
    {
        var first = Cross(Plaza(), seconds: 12).End;
        var second = Cross(Plaza(), seconds: 12).End;

        Assert.Equal(first, second);
    }

    [Fact]
    public void WalksThroughItselfWhenNobodyAvoids()
    {
        var crowd = Plaza();
        var agents = crowd.Select(entry => entry.Agent).ToList();
        var together = new Dictionary<int, double>();
        var longest = 0.0;
        for (var t = 0.0; t < 40; t += 1.0 / 20)
        {
            foreach (var entry in crowd)
            {
                var toGoal = entry.Goal - entry.Agent.Position;
                var wanted = toGoal.Length < 0.1
                    ? Vector3.Zero()
                    : toGoal.Normalized() * 2;
                entry.Agent.Integrate(wanted - entry.Agent.Velocity, 1.0 / 20);
            }

            longest = Math.Max(longest, Overlaps(agents, together, 1.0 / 20, 0.1));
        }

        Assert.True(longest > 1);
    }

    /// <summary>
    /// Adds <paramref name="step"/> to each pair overlapping by more than <paramref name="depth"/>, clears the rest,
    /// and returns the longest run of any pair.
    /// </summary>
    private static double Overlaps(List<Steerable> agents, Dictionary<int, double> together, double step, double depth)
    {
        var longest = 0.0;
        for (var i = 0; i < agents.Count; i++)
        {
            for (var j = i + 1; j < agents.Count; j++)
            {
                var gap = agents[i].Radius + agents[j].Radius - agents[i].Position.DistanceTo(agents[j].Position);
                var key = (i * agents.Count) + j;
                if (gap > depth)
                {
                    together[key] = together.GetValueOrDefault(key) + step;
                    longest = Math.Max(longest, together[key]);
                }
                else
                {
                    together.Remove(key);
                }
            }
        }

        return longest;
    }
}
