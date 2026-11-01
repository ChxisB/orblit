using Orblit.VectorMath;

namespace Orblit.Agent.Tests;

public class SteeringTests
{
    private static Steerable AgentAt(double x, Vector3? velocity = null, double speed = 4) =>
        new(position: new Vector3(x, 0, 0), velocity: velocity, maxSpeed: speed);

    /// <summary>
    /// Runs <paramref name="behaviour"/> until <paramref name="until"/> or the clock runs out, and says how long
    /// it took. Fixed steps, so the answer is the same every run.
    /// </summary>
    private static (bool Arrived, double Seconds) Simulate(
        Steerable self,
        Func<Steering> behaviour,
        Func<Steerable, bool> until,
        double step = 1.0 / 60,
        double most = 20)
    {
        var elapsed = 0.0;
        while (elapsed < most)
        {
            if (until(self))
            {
                return (true, elapsed);
            }

            self.Integrate(behaviour().Force(self), step);
            elapsed += step;
        }

        return (until(self), elapsed);
    }

    // an agent

    [Fact]
    public void NeverExceedsWhatItIsCapableOf()
    {
        var self = AgentAt(0, speed: 3);

        // A force far past its limit, applied for a long step.
        self.Integrate(new Vector3(1000, 0, 0), 1);

        Assert.True(self.Speed <= 3 + 1e-9);
    }

    [Fact]
    public void StandingStillIsNotFacingAnywhere()
    {
        // Null rather than a stale direction: keeping the last one makes a
        // mover that stops and starts snap to an angle it had a minute ago.
        Assert.Null(AgentAt(0).Heading);
        Assert.Equal(1, AgentAt(0, velocity: new Vector3(2, 0, 0)).Heading!.X, 1e-9);
    }

    [Fact]
    public void AStepOfNoTimeChangesNothing()
    {
        var self = AgentAt(5, velocity: new Vector3(1, 0, 0));
        self.Integrate(new Vector3(10, 0, 0), 0);
        Assert.Equal(5, self.Position.X);
        Assert.Equal(1, self.Velocity.X);
    }

    // seek and flee

    [Fact]
    public void SeekClosesOnItsTarget()
    {
        var self = AgentAt(0);
        var run = Simulate(
            self,
            () => new Seek(new Vector3(10, 0, 0)),
            a => (a.Position - new Vector3(10, 0, 0)).Length < 0.5);

        Assert.True(run.Arrived);
        Assert.True(run.Seconds < 5);
    }

    [Fact]
    public void FleeOpensTheDistance()
    {
        var self = AgentAt(1);
        var before = self.Position.Length;
        Simulate(self, () => new Flee(Vector3.Zero()), a => a.Position.Length > 8, most: 5);

        Assert.True(self.Position.Length > before);
    }

    [Fact]
    public void FleeIgnoresWhatIsFarEnoughAway()
    {
        var self = AgentAt(0);
        var force = new Flee(new Vector3(100, 0, 0), within: 10).Force(self);
        Assert.Equal(0, force.Length);
    }

    // arrive

    [Fact]
    public void ItStopsOnTheTargetRatherThanOrbitingIt()
    {
        // The whole difference from seek: a seek at full speed overshoots and
        // comes back, over and over.
        var self = AgentAt(0);
        var target = new Vector3(10, 0, 0);
        Simulate(self, () => new Arrive(target), _ => false, most: 12);

        Assert.True((self.Position - target).Length < 0.2);
        Assert.True(self.Speed < 0.5);
    }

    [Fact]
    public void ItComesInSlowerThanASeekDoes()
    {
        // The actual difference between the two, and not the one it is usually
        // described as: both sail past, because a steering force is a velocity
        // difference applied over a frame and therefore lags. What arriving
        // buys is the speed at the door, which is why it settles where a seek
        // circles.
        var target = new Vector3(10, 0, 0);

        double SpeedOnApproach(Func<Steering> behaviour)
        {
            var self = AgentAt(0);
            var elapsed = 0.0;
            while (elapsed < 12)
            {
                if ((self.Position - target).Length < 1)
                {
                    return self.Speed;
                }

                self.Integrate(behaviour().Force(self), 1.0 / 60);
                elapsed += 1.0 / 60;
            }

            return self.Speed;
        }

        Assert.True(SpeedOnApproach(() => new Arrive(target)) < SpeedOnApproach(() => new Seek(target)));
    }

    // pursue and evade

    [Fact]
    public void APursuerAimsAheadOfItsQuarryNotAtIt()
    {
        var quarry = new Steerable(
            position: new Vector3(10, 0, 0),
            velocity: new Vector3(0, 0, 4),
            maxSpeed: 4);
        var hunter = new Steerable(position: Vector3.Zero(), maxSpeed: 5);

        var ahead = new Pursue(quarry).Force(hunter);
        var at = new Seek(quarry.Position).Force(hunter);

        // Aiming ahead leans towards where the quarry is going, along z,
        // where aiming at it does not.
        Assert.True(ahead.Z > 0.5);
        Assert.True(Math.Abs(at.Z) < 1e-9);
    }

    [Fact]
    public void AnEvaderLeansAwayFromWhereTheHunterWillBe()
    {
        var hunter = new Steerable(
            position: new Vector3(2, 0, 0),
            velocity: new Vector3(4, 0, 0),
            maxSpeed: 4);
        var self = new Steerable(position: Vector3.Zero(), maxSpeed: 4);

        Assert.True(new Evade(hunter).Force(self).X < 0);
    }

    // wander

    [Fact]
    public void TheSameAgentWandersTheSameWayTwice()
    {
        // Deterministic in the seed and the clock, which is what makes a
        // wandering crowd reproducible in a replay and in a test.
        var one = new Wander(seed: 7, at: 3.25).Force(AgentAt(0));
        var two = new Wander(seed: 7, at: 3.25).Force(AgentAt(0));
        Assert.Equal(one.X, two.X);
        Assert.Equal(one.Z, two.Z);
    }

    [Fact]
    public void TwoAgentsSideBySideDoNotWanderInStep()
    {
        var one = new Wander(seed: 1, at: 2).Force(AgentAt(0));
        var two = new Wander(seed: 2, at: 2).Force(AgentAt(0));
        Assert.True((one - two).Length > 0.01);
    }

    [Fact]
    public void ItDriftsRatherThanJitters()
    {
        // Independent random numbers average to nothing and make a mover that
        // vibrates on the spot. Consecutive samples have to be close.
        var self = AgentAt(0);
        var worst = 0.0;
        var previous = new Wander(seed: 5, at: 0).Force(self).Normalized();

        for (var i = 1; i < 200; i++)
        {
            var now = new Wander(seed: 5, at: i / 60.0).Force(self).Normalized();
            var change = (now - previous).Length;
            if (change > worst)
            {
                worst = change;
            }

            previous = now;
        }

        // A sixtieth of a second never swings the direction far.
        Assert.True(worst < 0.2);
    }

    [Fact]
    public void OverTimeItGoesSomewhereRatherThanNowhere()
    {
        var self = AgentAt(0);
        var at = 0.0;
        for (var i = 0; i < 600; i++)
        {
            self.Integrate(new Wander(seed: 3, at: at).Force(self), 1.0 / 60);
            at += 1.0 / 60;
        }

        Assert.True(self.Position.Length > 1);
    }

    // a crowd

    [Fact]
    public void SeparationPushesNeighboursApart()
    {
        var a = new Steerable(position: new Vector3(0, 0, 0));
        var b = new Steerable(position: new Vector3(0.5, 0, 0));
        var crowd = new[] { a, b };

        Assert.True(new Separate(crowd).Force(a).X < 0);
        Assert.True(new Separate(crowd).Force(b).X > 0);
    }

    [Fact]
    public void ItShovesHarderTheCloserSomethingIs()
    {
        // A flat force makes a crowd that shuffles as a block.
        var self = new Steerable(position: Vector3.Zero());
        var near = new Separate([self, new Steerable(position: new Vector3(0.2, 0, 0))]);
        var far = new Separate([self, new Steerable(position: new Vector3(1.8, 0, 0))]);

        Assert.True(near.Force(self).Length > far.Force(self).Length);
    }

    [Fact]
    public void AnAgentDoesNotSeparateFromItself()
    {
        var self = new Steerable(position: Vector3.Zero());
        Assert.Equal(0, new Separate([self]).Force(self).Length);
    }

    [Fact]
    public void AlignmentTurnsTowardsHowTheNeighboursAreGoing()
    {
        var self = new Steerable(position: Vector3.Zero(), maxSpeed: 4);
        var crowd = new[]
        {
            self,
            new Steerable(position: new Vector3(1, 0, 0), velocity: new Vector3(0, 0, 4)),
            new Steerable(position: new Vector3(2, 0, 0), velocity: new Vector3(0, 0, 4)),
        };
        Assert.True(new Align(crowd).Force(self).Z > 0);
    }

    [Fact]
    public void CohesionPullsTowardsWhereTheNeighboursAre()
    {
        var self = new Steerable(position: Vector3.Zero(), maxSpeed: 4);
        var crowd = new[]
        {
            self,
            new Steerable(position: new Vector3(3, 0, 0)),
            new Steerable(position: new Vector3(4, 0, 0)),
        };
        Assert.True(new Cohere(crowd).Force(self).X > 0);
    }

    [Fact]
    public void NothingInRangeIsNoForceAtAll()
    {
        var self = new Steerable(position: Vector3.Zero());
        var distant = new[] { self, new Steerable(position: new Vector3(500, 0, 0)) };
        Assert.Equal(0, new Separate(distant).Force(self).Length);
        Assert.Equal(0, new Align(distant).Force(self).Length);
        Assert.Equal(0, new Cohere(distant).Force(self).Length);
    }

    // a blend

    [Fact]
    public void ItNeverAsksForMoreThanTheAgentCanPush()
    {
        var self = new Steerable(position: Vector3.Zero(), maxForce: 6);
        var blended = new Blend(
        [
            (Behaviour: new Seek(new Vector3(100, 0, 0)), Weight: 10),
            (Behaviour: new Seek(new Vector3(0, 0, 100)), Weight: 10),
        ]);
        Assert.True(blended.Force(self).Length <= 6 + 1e-9);
    }

    [Fact]
    public void AWeightOfNothingContributesNothing()
    {
        var self = new Steerable(position: Vector3.Zero());
        var only = new Blend(
        [
            (Behaviour: new Seek(new Vector3(10, 0, 0)), Weight: 1),
            (Behaviour: new Seek(new Vector3(0, 0, 10)), Weight: 0),
        ]);
        Assert.True(Math.Abs(only.Force(self).Z) < 1e-9);
    }

    [Fact]
    public void TwoReasonsToMoveGiveOneDirectionBetweenThem()
    {
        // Blending rather than choosing: an agent that picked one behaviour per
        // tick snaps between them, and the snap is visible however good each is.
        var self = new Steerable(position: Vector3.Zero());
        var both = new Blend(
        [
            (Behaviour: new Seek(new Vector3(10, 0, 0)), Weight: 1),
            (Behaviour: new Seek(new Vector3(0, 0, 10)), Weight: 1),
        ]).Force(self);

        Assert.True(both.X > 0);
        Assert.True(both.Z > 0);
    }

    // following a path

    private static readonly Vector3[] Route = [new(5, 0, 0), new(5, 0, 5), new(0, 0, 5)];

    [Fact]
    public void ItAdvancesOnceAPointIsReached()
    {
        var self = new Steerable(position: new Vector3(5, 0, 0));
        Assert.Equal(1, new FollowPath(Route, index: 0).NextIndex(self));
    }

    [Fact]
    public void AskingForTheForceDoesNotAdvanceAnything()
    {
        // A behaviour that moved an index as a side effect of being asked could
        // not be asked twice, and a test and a debug overlay both do.
        var self = new Steerable(position: new Vector3(5, 0, 0));
        var path = new FollowPath([], index: 0);
        Assert.Equal(0, path.NextIndex(self));

        var walking = new FollowPath(Route, index: 0);
        walking.Force(self);
        walking.Force(self);
        Assert.Equal(0, walking.Index);
    }

    [Fact]
    public void ALoopGoesBackToTheBeginningARouteDoesNot()
    {
        var self = new Steerable(position: new Vector3(0, 0, 5));
        Assert.Equal(0, new FollowPath(Route, index: 2, loop: true).NextIndex(self));
        Assert.Equal(2, new FollowPath(Route, index: 2).NextIndex(self));
    }

    [Fact]
    public void ItWalksAWholeRouteAndStopsOnTheEnd()
    {
        var self = new Steerable(position: Vector3.Zero(), maxSpeed: 4);
        var path = new FollowPath(Route, index: 0);
        var elapsed = 0.0;

        while (elapsed < 20 && !path.IsDone(self))
        {
            self.Integrate(path.Force(self), 1.0 / 60);
            path = new FollowPath(Route, index: path.NextIndex(self));
            elapsed += 1.0 / 60;
        }

        Assert.True(path.IsDone(self));
        Assert.True((self.Position - Route[^1]).Length < 1.1);
    }

    [Fact]
    public void AnEmptyPathAsksForNothing()
    {
        var self = new Steerable(position: Vector3.Zero());
        Assert.Equal(0, new FollowPath([], index: 0).Force(self).Length);
    }
}
