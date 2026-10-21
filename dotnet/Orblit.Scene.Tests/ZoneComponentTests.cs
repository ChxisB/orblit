using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class ZoneComponentTests
{
    // reading a zone

    [Fact]
    public void ChangesNothingWhenTheFileSaysNothing()
    {
        var zone = ZoneComponent.FromJson(Map());

        Assert.Null(zone.Gravity);
        Assert.Null(zone.LinearDamping);
        Assert.Null(zone.AngularDamping);
        Assert.Equal(0, zone.Priority);
    }

    [Fact]
    public void IsTheZoneItWasWrittenAs()
    {
        var zone = new ZoneComponent(new Vector3(0, -1.5, 0), 2, 3, 4);
        var read = ZoneComponent.FromJson(zone.ToJson());

        Same(zone.ToJson(), read.ToJson());
        Assert.Equal(new Vector3(0, -1.5, 0), read.Gravity);
        Assert.Equal(2.0, read.LinearDamping);
        Assert.Equal(3.0, read.AngularDamping);
        Assert.Equal(4, read.Priority);
    }

    [Fact]
    public void LeavesAFieldOutOfTheFileWhenItIsLeftOutOfTheZone() =>
        Same(Map(("linearDamping", 2.0), ("priority", 0L)), new ZoneComponent(linearDamping: 2).ToJson());

    [Fact]
    public void KeepsAGravityOfNoughtWhichHoldsABodyInPlace()
    {
        var read = ZoneComponent.FromJson(new ZoneComponent(Vector3.Zero()).ToJson());

        Assert.Equal(Vector3.Zero(), read.Gravity);
    }

    [Fact]
    public void TakesAGravityThatIsNotAListForNoGravity() =>
        Assert.Null(ZoneComponent.FromJson(Map(("gravity", "down"))).Gravity);

    [Fact]
    public void RoundsAPriorityWrittenAsAFraction() =>
        Assert.Equal(3, ZoneComponent.FromJson(Map(("priority", 2.6))).Priority);

    // changing a zone

    [Fact]
    public void ChangesWhatItWasAskedToAndKeepsTheRest()
    {
        var before = new ZoneComponent(new Vector3(0, 9, 0), priority: 1);
        var after = before.CopyWith(linearDamping: 1, priority: 2);

        Assert.Equal(new Vector3(0, 9, 0), after.Gravity);
        Assert.Equal(1.0, after.LinearDamping);
        Assert.Equal(2, after.Priority);
    }

    [Fact]
    public void DoesNotShareItsGravityWithTheZoneItCameFrom()
    {
        var before = new ZoneComponent(new Vector3(0, 9, 0));
        var after = before.CopyWith();
        after.Gravity!.Y = 0;

        Assert.Equal(new Vector3(0, 9, 0), before.Gravity);
    }

    [Fact]
    public void DoesNotShareTheVectorItWasBuiltFrom()
    {
        var up = new Vector3(0, 9, 0);
        var zone = new ZoneComponent(up);
        up.Y = 0;

        Assert.Equal(new Vector3(0, 9, 0), zone.Gravity);
    }

    // a zone in a scene

    [Fact]
    public void IsWrittenAfterTheMotionAndBeforeTheVehicle()
    {
        var order = SceneComponents.Order.ToList();

        Assert.Equal(order.IndexOf("motion") + 1, order.IndexOf(SceneComponents.Zone));
        Assert.Equal(order.IndexOf(SceneComponents.Zone) + 1, order.IndexOf(SceneComponents.Vehicle));
    }
}
