using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

public class FollowerTests
{
    private readonly Spline _line = Spline.Line([new Vector3(0, 0, 0), new Vector3(10, 0, 0)]);

    private readonly PatrolStop[] _stops =
    [
        new(new Vector3(0, 0, 0)),
        new(new Vector3(10, 0, 0), wait: 2),
        new(new Vector3(10, 0, 10)),
    ];

    // PathFollower

    [Fact]
    public void RidesAtItsSpeed()
    {
        var rider = new PathFollower(_line, speed: 2);
        rider.Advance(1.5);
        Assert.Equal(3, rider.Distance, 1e-9);
        Assert.Equal(3, rider.Frame.Position.X, 1e-9);
    }

    [Fact]
    public void StopsAtTheEndAndSaysSo()
    {
        var rider = new PathFollower(_line, speed: 4);
        rider.Advance(10);
        Assert.Equal(10, rider.Distance);
        Assert.True(rider.IsFinished);
    }

    [Fact]
    public void LoopsBackToTheStart()
    {
        var rider = new PathFollower(_line, speed: 4, end: PathEnd.Loop);
        rider.Advance(3);
        Assert.Equal(2, rider.Distance, 1e-9);
        Assert.False(rider.IsFinished);
    }

    [Fact]
    public void BouncesOffTheEnds()
    {
        var rider = new PathFollower(_line, speed: 4, end: PathEnd.PingPong);
        rider.Advance(3);
        Assert.Equal(8, rider.Distance, 1e-9);
        rider.Advance(1);
        Assert.Equal(4, rider.Distance, 1e-9);
        rider.Advance(2);
        Assert.True(Math.Abs(rider.Distance - 4) < 1e-9, "4 down then 4 up");
    }

    [Fact]
    public void ANegativeSpeedRunsItBackwards()
    {
        var rider = new PathFollower(_line, speed: -2, distance: 6);
        rider.Advance(1);
        Assert.Equal(4, rider.Distance, 1e-9);
        rider.Advance(10);
        Assert.Equal(0, rider.Distance);
        Assert.True(rider.IsFinished);
    }

    [Fact]
    public void IgnoresNoTimeAndNegativeTime()
    {
        var rider = new PathFollower(_line, speed: 4);
        rider.Advance(0);
        rider.Advance(-1);
        Assert.Equal(0, rider.Distance);
    }

    // Rail

    [Fact]
    public void SlidesToTheNearestPointOfTheLine()
    {
        var rail = new Rail(_line, speed: 100);
        rail.Track(new Vector3(7, 5, 2), 1);
        Assert.Equal(7, rail.Distance, 1e-6);
        Assert.Equal(7, rail.Position.X, 1e-6);
    }

    [Fact]
    public void TakesItsSpeedToGetThere()
    {
        var rail = new Rail(_line, speed: 2);
        rail.Track(new Vector3(9, 0, 0), 1);
        Assert.Equal(2, rail.Distance, 1e-9);
        rail.Track(new Vector3(9, 0, 0), 1);
        Assert.Equal(4, rail.Distance, 1e-9);
    }

    [Fact]
    public void GoesTheShortWayRoundAClosedLoop()
    {
        var ring = Spline.Line(
            [new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10), new Vector3(0, 0, 10)],
            closed: true);
        var rail = new Rail(ring, speed: 3, distance: 1);
        rail.Track(new Vector3(0, 0, 8), 1);
        // From 1 the target is at 32, which is 9 the other way round.
        Assert.Equal(0, rail.Position.X, 1e-6);
        Assert.Equal(2, rail.Position.Z, 1e-6);
    }

    // Patrol

    [Fact]
    public void HeadsForTheFirstStop()
    {
        var patrol = new Patrol(_stops);
        Assert.Equal(0, patrol.Target!.X);
    }

    [Fact]
    public void WaitsAtAStopThenMovesOn()
    {
        var patrol = new Patrol(_stops, end: PathEnd.Stop);
        patrol.Update(new Vector3(0, 0, 0), 0.1);
        Assert.Equal(10, patrol.Target!.X);
        patrol.Update(new Vector3(10, 0, 0), 1);
        Assert.True(patrol.IsWaiting);
        Assert.Equal(0, patrol.Target!.Z);
        patrol.Update(new Vector3(10, 0, 0), 1.5);
        Assert.Equal(10, patrol.Target!.Z);
    }

    [Fact]
    public void DoesNotCountTheWaitWhileAway()
    {
        var patrol = new Patrol(_stops, end: PathEnd.Stop);
        patrol.Update(new Vector3(0, 0, 0), 0);
        patrol.Update(new Vector3(10, 0, 0), 1.5);
        patrol.Update(new Vector3(5, 0, 0), 1);
        patrol.Update(new Vector3(10, 0, 0), 1.5);
        Assert.True(patrol.Target!.Z == 0, "the wait started over");
    }

    [Fact]
    public void EndsLoopsOrBouncesAfterTheLastStop()
    {
        var ends = new Patrol(_stops, end: PathEnd.Stop);
        var loops = new Patrol(_stops);
        var bounces = new Patrol(_stops, end: PathEnd.PingPong);
        foreach (var patrol in new[] { ends, loops, bounces })
        {
            foreach (var stop in _stops.Concat(_stops.Reverse().Skip(1)))
            {
                patrol.Update(stop.At, stop.Wait + 0.1);
            }
        }

        Assert.True(ends.IsFinished);
        Assert.Null(ends.Target);
        Assert.False(loops.IsFinished);
        Assert.False(bounces.IsFinished);
    }

    [Fact]
    public void ALoopGoesBackToTheFirst()
    {
        var patrol = new Patrol(_stops);
        foreach (var stop in _stops)
        {
            patrol.Update(stop.At, stop.Wait + 0.1);
        }

        Assert.Equal(0, patrol.Target!.X);
    }

    [Fact]
    public void ABounceComesBackThroughTheStops()
    {
        var patrol = new Patrol(_stops, end: PathEnd.PingPong);
        foreach (var stop in _stops)
        {
            patrol.Update(stop.At, stop.Wait + 0.1);
        }

        Assert.Equal(0, patrol.Target!.Z);
        Assert.Equal(10, patrol.Target!.X);
    }

    [Fact]
    public void NeedsAStop()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Patrol([]));
    }
}
