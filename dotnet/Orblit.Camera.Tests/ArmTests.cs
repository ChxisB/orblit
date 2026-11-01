using Orblit.VectorMath;

namespace Orblit.Camera.Tests;

public class ArmTests
{
    private static readonly Lens Plain = new();

    private static readonly ArmProbe OpenAir = (_, _, _) => null;

    /// <summary>
    /// A wall standing across the Z axis at <paramref name="z"/>, from <paramref name="minX"/> to
    /// <paramref name="maxX"/> and as tall as anyone needs. A ball going from one side of it
    /// towards the other stops <c>radius</c> short of it.
    /// </summary>
    internal static ArmProbe WallAt(double z, double minX = -2, double maxX = 2) => (from, to, radius) =>
    {
        var stand = z + (radius * (from.Z > z ? 1 : -1));
        var run = to.Z - from.Z;
        if (Math.Abs(from.Z - z) <= radius)
        {
            return 0;
        }

        if (Math.Abs(run) < 1e-9)
        {
            return null;
        }

        var share = (stand - from.Z) / run;
        if (share < 0 || share > 1)
        {
            return null;
        }

        var x = from.X + ((to.X - from.X) * share);
        if (x < minX || x > maxX)
        {
            return null;
        }

        return share * (to - from).Length;
    };

    private static Vector3 Step(ArmBody body, Vector3 current, ICameraTarget? target) =>
        body.Solve(current, target, Quaternion.Identity(), Plain, 16.0 / 9, 1.0 / 60);

    private static FollowBody Behind(double damping = 0) =>
        new(new Vector3(0, 0, 6), FollowBinding.World, Vector3.All(damping));

    private static void Near(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected:R} within {tolerance}, got {actual:R}");

    // a camera arm

    [Fact]
    public void LeavesACameraAloneWhenNothingIsInTheWay()
    {
        var arm = new ArmBody(Behind(), OpenAir, height: 1);
        var target = new FixedTarget(new Vector3(3, 0, 0));
        var place = Step(arm, Vector3.Zero(), target);
        Near(3, place.X, 1e-9);
        Near(6, place.Z, 1e-9);
    }

    [Fact]
    public void StopsACameraShortOfAWallBehindTheTarget()
    {
        var arm = new ArmBody(Behind(), WallAt(3), radius: 0.25, height: 1);
        var place = Step(arm, Vector3.Zero(), new FixedTarget(Vector3.Zero()));
        Near(2.75, place.Z, 1e-9);

        // It stays on the line from the target's head to where it wanted to be.
        Near(1 - (2.75 / 6), place.Y, 1e-9);
    }

    [Fact]
    public void LandsAtTheTargetWhenTheWallIsAgainstTheTarget()
    {
        var arm = new ArmBody(Behind(), WallAt(0.1), height: 1);
        var place = Step(arm, Vector3.Zero(), new FixedTarget(Vector3.Zero()));
        Near(0, place.Z, 1e-9);
        Near(1, place.Y, 1e-9);
    }

    [Fact]
    public void IsNotStoppedByAWallTheArmGoesRound()
    {
        var arm = new ArmBody(Behind(), WallAt(3), height: 1);
        var place = Step(arm, Vector3.Zero(), new FixedTarget(new Vector3(5, 0, 0)));
        Near(6, place.Z, 1e-9);
    }

    [Fact]
    public void IsNotStoppedByAWallBeyondWhereItWantsToBe()
    {
        var arm = new ArmBody(Behind(), WallAt(9), height: 1);
        var place = Step(arm, Vector3.Zero(), new FixedTarget(Vector3.Zero()));
        Near(6, place.Z, 1e-9);
    }

    [Fact]
    public void HandsACameraWithNoTargetToTheBodyInside()
    {
        var arm = new ArmBody(Behind(), WallAt(3));
        var place = Step(arm, new Vector3(1, 2, 3), null);
        Near(1, place.X, 1e-9);
        Near(3, place.Z, 1e-9);
    }

    [Fact]
    public void TakesItsFramingGuidesFromTheBodyInside()
    {
        var inner = new ScreenFollowBody(screenX: 0.3, screenY: 0.6);
        var arm = new ArmBody(inner, OpenAir);
        Assert.NotNull(arm.Guides);
        Assert.Equal(0.3, arm.Guides.ScreenX);
        Assert.Equal(0.6, arm.Guides.ScreenY);
    }

    // a camera arm over a moving target

    [Fact]
    public void NeverEndsUpBeyondAWallWhileTheTargetWalksAtIt()
    {
        var arm = new ArmBody(Behind(0.3), WallAt(4), radius: 0.25, height: 1);
        var target = new FixedTarget(new Vector3(0, 0, -6));
        var place = new Vector3(0, 1, 0);
        var held = 0;
        for (var frame = 0; frame < 600; frame++)
        {
            // The target walks towards the wall and then away from it.
            var back = frame < 300 ? frame : 600 - frame;
            target.Position = new Vector3(0, 0, -6 + (back * 0.03));
            place = Step(arm, place, target);
            if (place.Z < target.Position.Z + 5.99)
            {
                held++;
            }

            if (target.Position.Z < 4 - 0.25)
            {
                Assert.True(place.Z <= (4 - 0.25) + 1e-9, $"{frame}");
            }
        }

        // The wall was in the way for a good part of the walk, or the test
        // would pass by never meeting it.
        Assert.True(held > 100);
    }

    [Fact]
    public void KeepsACameraInTheRoomWhileTheTargetCirclesIt()
    {
        // The camera orbits a target standing in a room whose far wall is at
        // z = -3. Whatever the bearing, the camera is never past that wall.
        var orbit = new OrbitBody(radius: 7, elevation: 10, damping: 0);
        var arm = new ArmBody(orbit, WallAt(-3, minX: -50, maxX: 50), radius: 0.25, height: 1.5);
        var target = new FixedTarget(new Vector3(0, 0, 0));
        var place = Vector3.Zero();
        for (var degrees = 0; degrees < 360; degrees += 5)
        {
            orbit.Azimuth = degrees;
            place = Step(arm, place, target);
            Assert.True(place.Z >= (-3 + 0.25) - 1e-9, $"{degrees}");
        }
    }

    [Fact]
    public void ComesBackOutAtThePaceTheBodyInsideCatchesUp()
    {
        var wallUp = true;
        var arm = new ArmBody(
            Behind(0.5),
            (from, to, radius) => wallUp ? WallAt(3)(from, to, radius) : null,
            height: 1);
        var target = new FixedTarget(Vector3.Zero());
        var place = new Vector3(0, 1, 6);
        for (var frame = 0; frame < 30; frame++)
        {
            place = Step(arm, place, target);
        }

        Near(2.75, place.Z, 1e-9);

        wallUp = false;
        place = Step(arm, place, target);
        Assert.True(place.Z > 2.75);
        Assert.True(place.Z < 6, "it eases out and does not jump");
        for (var frame = 0; frame < 600; frame++)
        {
            place = Step(arm, place, target);
        }

        Near(6, place.Z, 0.01);
    }

    // a camera arm in a shot

    [Fact]
    public void PutsACutToTheShotOnTheNearSideOfTheWall()
    {
        var shot = new VirtualCamera(
            "behind",
            body: new ArmBody(Behind(0.5), WallAt(3), height: 1),
            follow: new FixedTarget(Vector3.Zero()));
        shot.Snap(16.0 / 9);
        Near(2.75, shot.State.Position.Z, 1e-9);
    }
}
