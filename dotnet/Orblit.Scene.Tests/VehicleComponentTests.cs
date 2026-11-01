using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class VehicleComponentTests
{
    // a vehicle component

    [Fact]
    public void IsASmallCarWhenTheFileSaysNothing()
    {
        var car = VehicleComponent.FromJson(Map());

        Assert.Equal(30, car.MaxSteer);
        Assert.Equal(VehicleComponent.DefaultTorque, car.Torque);
        Assert.Equal(VehicleComponent.DefaultRatios, car.Ratios);
        Assert.Equal(0xFFFFFFFF, car.Ground);
    }

    [Fact]
    public void KeepsACurveTheFileEmptiesRatherThanInventingOne() =>
        Assert.Empty(VehicleComponent.FromJson(Map(("torque", List()))).Torque);

    [Fact]
    public void IsWrittenAfterTheZoneAndBeforeTheLight()
    {
        var order = SceneComponents.Order.ToList();

        Assert.Equal(order.IndexOf("zone") + 1, order.IndexOf("vehicle"));
        Assert.Equal(order.IndexOf("vehicle") + 1, order.IndexOf("wheel"));
        Assert.Equal(order.IndexOf("wheel") + 1, order.IndexOf("light"));
    }

    // a wheel component

    [Fact]
    public void HasTheNumbersOfACarWheelWhenTheFileSaysNothing()
    {
        var wheel = WheelComponent.FromJson(Map());

        Assert.Equal(0.35, wheel.Radius);
        Assert.Equal(3000, wheel.Brake);
        Assert.False(wheel.Steers);
        Assert.Equal(0.3, wheel.Travel);
    }

    [Fact]
    public void ChangesOneFieldWithCopyWith()
    {
        var wheel = new WheelComponent().CopyWith(drives: true, grip: 0.8);

        Assert.True(wheel.Drives);
        Assert.Equal(0.8, wheel.Grip);
        Assert.Equal(0.35, wheel.Radius);
    }

    [Fact]
    public void SurvivesTheRoundTripThroughASceneFile()
    {
        var car = new VehicleComponent(
            maxSteer: 40,
            antiRoll: 8000,
            torque: [1000, 100, 6000, 180],
            ratios: [3, 1.5],
            @lock: 200,
            ground: 3);
        var wheel = new WheelComponent(radius: 0.4, steers: true, drives: true, handbrake: true, grip: 1.2);
        var document = new SceneDocument("Scene", entities:
        [
            new SceneEntity(
                "car",
                "Car",
                components: new Dictionary<string, SceneComponent> { [SceneComponents.Vehicle] = car }),
            new SceneEntity(
                "wheel",
                "Wheel",
                parent: "car",
                components: new Dictionary<string, SceneComponent> { [SceneComponents.Wheel] = wheel }),
        ]);

        var back = SceneDocument.Decode(document.Encode()).Document;
        Same(car.ToJson(), Assert.IsType<VehicleComponent>(back["car"]![SceneComponents.Vehicle]).ToJson());
        Same(wheel.ToJson(), Assert.IsType<WheelComponent>(back["wheel"]![SceneComponents.Wheel]).ToJson());
    }
}
