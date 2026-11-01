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

    // a zone in a scene

    [Fact]
    public void IsReadBackAsAZoneNotAnUnknownComponent()
    {
        var document = DocumentOf(new ZoneComponent(gravity: new Vector3(0, 9, 0)));
        var read = SceneDocument.Decode(document.Encode());

        Assert.Empty(read.Problems);
        Assert.IsType<ZoneComponent>(read.Document["pool"]![SceneComponents.Zone]);
        Assert.Equal(document.Encode(), read.Document.Encode());
    }

    [Fact]
    public void AddingOneIsAChangeThatGoesBack()
    {
        var a = DocumentOf(null);
        var b = DocumentOf(new ZoneComponent(linearDamping: 4));
        var diff = SceneDiff.Between(a, b);

        Assert.True(diff.IsNotEmpty);
        DiffAssert.RoundTrips(diff, a, b);
    }

    [Fact]
    public void AChangedFieldIsOneOperationAndItGoesBack()
    {
        var a = DocumentOf(new ZoneComponent(priority: 1));
        var b = DocumentOf(new ZoneComponent(priority: 2));
        var diff = SceneDiff.Between(a, b);

        Assert.Equal("priority", Assert.IsType<SetField>(Assert.Single(diff.Operations)).Field);
        DiffAssert.RoundTrips(diff, a, b);
    }

    private static SceneDocument DocumentOf(ZoneComponent? zone)
    {
        var components = new Dictionary<string, SceneComponent>
        {
            [SceneComponents.Transform] = new TransformComponent(),
            [SceneComponents.Body] = new BodyComponent(motion: BodyMotion.Fixed),
        };
        if (zone is not null)
        {
            components[SceneComponents.Zone] = zone;
        }

        return new SceneDocument("Scene", entities: [new SceneEntity("pool", "Pool", components: components)]);
    }
}
