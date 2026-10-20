namespace Orblit.Rig.Tests;

public class FootPlacementTests
{
    /// <summary>A hip, a bent leg and a foot lying forwards, standing on y = 0.</summary>
    private static Armature Body(double ankle = 0.1) => new(
    [
        new Bone("hips", new Vector3(0, 1, 0), new Vector3(0, 1.2, 0)),
        new Bone("thigh", new Vector3(0, 1, 0), new Vector3(0, 0.55 + ankle - 0.1, 0.1), parent: "hips"),
        new Bone("shin", new Vector3(0, 0.55 + ankle - 0.1, 0.1), new Vector3(0, ankle, 0), parent: "thigh", connected: true),
        new Bone("foot", new Vector3(0, ankle, 0), new Vector3(0, ankle, 0.2), parent: "shin", connected: true),
    ]);

    private static readonly FootLeg Leg = new("thigh", "shin", "foot", ankleHeight: 0.1);

    /// <summary>Ground that is flat at <paramref name="height"/>.</summary>
    private static GroundProbe Flat(double height) => (from, to) =>
    {
        if (from.Y < height || to.Y > height)
        {
            return null;
        }

        return new GroundHit(new Vector3(from.X, height, from.Z), new Vector3(0, 1, 0));
    };

    /// <summary>Ground that is a plane through y = height at the origin, tipped by angle about X so it climbs along +Z.</summary>
    private static GroundProbe Sloped(double height, double angle)
    {
        var normal = new Vector3(0, Math.Cos(angle), -Math.Sin(angle));
        return (from, to) =>
        {
            var y = height + (from.Z * Math.Tan(angle));
            if (from.Y < y || to.Y > y)
            {
                return null;
            }

            return new GroundHit(new Vector3(from.X, y, from.Z), normal);
        };
    }

    private static void Place(Pose pose, FootPlacement modifier, double dt = 0, Matrix4? toWorld = null)
    {
        pose.Evaluate();
        var stack = new ModifierStack();
        stack.Add("feet", modifier);
        stack.Apply(pose, new ModifierFrame(dt: dt, modelToWorld: toWorld));
    }

    private static FootPlacement Snapped(GroundProbe probe, string? pelvis = null, double ease = 0) =>
        new([Leg], probe, pelvis: pelvis, ease: ease);

    // foot placement

    [Fact]
    public void LeavesAFootAloneOnGroundTheAnimationWasMadeFor()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped(Flat(0)));
        Assert.True(pose["thigh"].IsRest);
        Assert.True(pose["shin"].IsRest);
    }

    [Fact]
    public void StandsTheAnkleOnHigherGround()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped(Flat(0.2)));
        Assert.Equal(0.3, pose.TailOf("shin").Y, 1e-3);
    }

    [Fact]
    public void KeepsTheAnkleWhereItWasAcrossTheGround()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped(Flat(0.2)));
        Assert.Equal(0, pose.TailOf("shin").X, 1e-6);
        Assert.Equal(0, pose.TailOf("shin").Z, 1e-3);
    }

    [Fact]
    public void KeepsTheHeightTheAnimationLiftedTheFoot()
    {
        var pose = new Pose(Body(ankle: 0.25));
        Place(pose, Snapped(Flat(0.2)));
        Assert.Equal(0.25 + 0.2, pose.TailOf("shin").Y, 1e-3);
    }

    [Fact]
    public void FollowsTheAnimationWhenTheProbeFindsNothing()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped((_, _) => null));
        Assert.True(pose["thigh"].IsRest);
    }

    [Fact]
    public void LowersThePelvisSoALegCanReachGroundThatFallsAway()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped(Flat(-0.2), pelvis: "hips"));
        Assert.Equal(-0.1, pose.TailOf("shin").Y, 1e-3);
        Assert.Equal(-0.2, pose["hips"].Location.Y, 1e-6);
    }

    [Fact]
    public void DoesNotMoveThePelvisForGroundThatRises()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped(Flat(0.2), pelvis: "hips"));
        Assert.True(pose["hips"].IsRest);
    }

    [Fact]
    public void TiltsAPlantedFootToTheSlope()
    {
        var pose = new Pose(Body());
        const double angle = 0.3;
        Place(pose, Snapped(Sloped(0, angle)));

        var sole = TrackAxis.NegativeZ.Of(pose.WorldOf("foot"));
        var normal = new Vector3(0, Math.Cos(angle), -Math.Sin(angle));
        Assert.True(Math.Acos(Doubles.Clamp(sole.Dot(normal), -1.0, 1.0)) < 1e-3);
    }

    [Fact]
    public void TiltsNoFurtherThanTheLimit()
    {
        var pose = new Pose(Body());
        const double angle = 0.6;
        Place(pose, new FootPlacement(legs: [Leg], probe: Sloped(0, angle), ease: 0, maxTilt: 0.2));

        var sole = TrackAxis.NegativeZ.Of(pose.WorldOf("foot"));
        var level = new Vector3(0, 1, 0);
        Assert.True(Math.Acos(Doubles.Clamp(sole.Dot(level), -1.0, 1.0)) < 0.25);
    }

    [Fact]
    public void LeavesAFootInTheAirLevel()
    {
        var pose = new Pose(Body(ankle: 0.4));
        Place(pose, Snapped(Sloped(0, 0.3)));
        var sole = TrackAxis.NegativeZ.Of(pose.WorldOf("foot"));
        Assert.Equal(1, sole.Dot(new Vector3(0, 1, 0)), 1e-3);
    }

    [Fact]
    public void PlacesAFootOnTheFirstFrameEvenWhileEasing()
    {
        var pose = new Pose(Body());
        Place(pose, Snapped(Flat(0.2), ease: 0.5), dt: 0.01);
        Assert.Equal(0.3, pose.TailOf("shin").Y, 1e-3);
    }

    [Fact]
    public void ClosesPartOfTheGapInTheTimeThatPassed()
    {
        var pose = new Pose(Body());
        var ground = 0.0;
        var stack = new ModifierStack();
        stack.Add("feet", new FootPlacement(legs: [Leg], probe: (from, to) => Flat(ground)(from, to), ease: 0.1));

        void Step(double dt)
        {
            pose.Evaluate();
            stack.Apply(pose, new ModifierFrame(dt: dt));
        }

        Step(1.0 / 60);
        ground = 0.2;
        Step(0.1);
        var expected = 0.1 + (0.2 * (1 - Math.Exp(-1)));
        Assert.Equal(expected, pose.TailOf("shin").Y, 2e-3);
    }

    [Fact]
    public void DoesNotMoveInAFrameOfNoTimeWhileEasing()
    {
        var pose = new Pose(Body());
        var ground = 0.0;
        var stack = new ModifierStack();
        stack.Add("feet", new FootPlacement(legs: [Leg], probe: (from, to) => Flat(ground)(from, to), ease: 0.1));
        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: 1.0 / 60));
        ground = 0.2;
        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: 0));
        Assert.Equal(0.1, pose.TailOf("shin").Y, 1e-6);
    }

    [Fact]
    public void SnapsAgainAfterAReset()
    {
        var pose = new Pose(Body());
        double? ground = 0.2;
        var stack = new ModifierStack();
        stack.Add("feet", new FootPlacement(legs: [Leg], probe: (from, to) => ground is null ? null : Flat(ground.Value)(from, to), ease: 0.1));
        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: 1.0 / 60));
        Assert.Equal(0.3, pose.TailOf("shin").Y, 1e-3);

        // Without the reset the foot would ease back from 0.3 and still be up.
        stack.Reset();
        ground = null;
        pose.Reset();
        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: 1.0 / 60));
        Assert.Equal(0.1, pose.TailOf("shin").Y, 1e-6);
    }

    [Fact]
    public void AsksTheProbeInTheWorldNotInTheModel()
    {
        var pose = new Pose(Body());
        var asked = new List<Vector3>();
        Place(pose, Snapped((from, to) =>
        {
            asked.Add(from);
            asked.Add(to);
            return null;
        }), toWorld: Matrix4.TranslationValues(10, 0, 0));
        Assert.Equal(10, asked[0].X, 1e-9);
        Assert.Equal(0.1 + 0.5, asked[0].Y, 1e-9);
        Assert.Equal(0.1 - 0.5, asked[1].Y, 1e-9);
    }

    [Fact]
    public void ReadsAHitInTheWorldBackIntoTheModel()
    {
        var pose = new Pose(Body());
        Place(
            pose,
            Snapped((from, to) => new GroundHit(new Vector3(from.X, 5.2, from.Z), new Vector3(0, 1, 0))),
            toWorld: Matrix4.TranslationValues(0, 5, 0));
        Assert.Equal(0.3, pose.TailOf("shin").Y, 1e-3);
    }

    [Fact]
    public void RefusesTwoBonesThatAreNotOneLeg()
    {
        var pose = new Pose(Body());
        pose.Evaluate();
        var stack = new ModifierStack();
        stack.Add("feet", new FootPlacement(legs: [new FootLeg("hips", "shin")], probe: Flat(0)));
        Assert.Throws<ArmatureException>(() => stack.Apply(pose));
    }

    [Fact]
    public void RefusesARigWithNoLegs()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FootPlacement(legs: [], probe: Flat(0)));
    }
}
