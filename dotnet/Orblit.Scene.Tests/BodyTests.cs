using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class BodyTests
{
    // reading a body

    [Fact]
    public void SaysWhatItDefaultsToWhenTheFileSaysNothing()
    {
        var body = BodyComponent.FromJson(Map());

        Assert.Equal(BodyShape.Box, body.Shape);
        Assert.Equal(Vector3.All(1), body.Size);
        Assert.Equal(0.5, body.Radius);
        Assert.Equal(2, body.Height);
        Assert.Equal(Vector3.Zero(), body.Centre);
        Assert.Equal(BodyMotion.Free, body.Motion);
        Assert.Equal(1, body.Mass);
        Assert.Equal(0.5, body.Friction);
        Assert.Equal(0, body.Restitution);
        Assert.Equal(0.05, body.LinearDamping);
        Assert.Equal(0.05, body.AngularDamping);
        Assert.Equal(1, body.Layers);
        Assert.Equal(BodyComponent.EveryLayer, body.Cares);
        Assert.False(body.StartsAsleep);
        Assert.False(body.Trigger);
        Assert.False(body.Stay);
        Assert.Equal(Vector3.Zero(), body.Surface);
        Assert.Empty(body.Locks);
        Assert.Equal(1, body.GravityScale);
        Assert.Equal(0, body.MaxSpeed);
        Assert.Equal(0, body.MaxSpin);
        Assert.False(body.Continuous);
        Assert.Equal(Vector3.Zero(), body.CentreOfMass);
        Assert.Equal(Vector3.Zero(), body.Inertia);
    }

    [Fact]
    public void IsTheBodyItWasWrittenAs()
    {
        var body = new BodyComponent(
            shape: BodyShape.Capsule,
            size: new Vector3(1, 2, 3),
            radius: 0.3,
            height: 1.8,
            centre: new Vector3(0, 0.9, 0),
            motion: BodyMotion.Driven,
            mass: 80,
            friction: 0.9,
            restitution: 0.1,
            linearDamping: 0.2,
            angularDamping: 0.4,
            layers: 4,
            cares: 3,
            startsAsleep: true,
            trigger: true,
            stay: true,
            surface: new Vector3(2, 0, -1),
            locks: new HashSet<BodyLock> { BodyLock.MoveZ, BodyLock.TurnX },
            gravityScale: 0.5,
            maxSpeed: 12,
            maxSpin: 3,
            continuous: true,
            centreOfMass: new Vector3(0, -0.2, 0),
            inertia: new Vector3(2, 3, 4));
        var read = BodyComponent.FromJson(body.ToJson());

        Same(body.ToJson(), read.ToJson());
        Assert.Equal(BodyShape.Capsule, read.Shape);
        Assert.Equal(new Vector3(0, 0.9, 0), read.Centre);
        Assert.Equal(BodyMotion.Driven, read.Motion);
        Assert.True(read.StartsAsleep);
        Assert.True(read.Trigger);
        Assert.True(read.Stay);
        Assert.Equal(new Vector3(2, 0, -1), read.Surface);
        Assert.True(read.Locks.SetEquals([BodyLock.MoveZ, BodyLock.TurnX]));
        Assert.Equal(0.5, read.GravityScale);
        Assert.Equal(12, read.MaxSpeed);
        Assert.Equal(3, read.MaxSpin);
        Assert.True(read.Continuous);
        Assert.Equal(new Vector3(0, -0.2, 0), read.CentreOfMass);
        Assert.Equal(new Vector3(2, 3, 4), read.Inertia);
    }

    [Fact]
    public void ACharacterKeepsItsWalkingNumbersThroughAFile()
    {
        var body = new BodyComponent(
            shape: BodyShape.Capsule,
            motion: BodyMotion.Character,
            radius: 0.3,
            height: 1.8,
            stepHeight: 0.45,
            steepest: 0.9,
            skin: 0.02,
            strength: 120,
            crouchHeight: 1.0);
        var read = BodyComponent.FromJson(body.ToJson());

        Assert.Equal(BodyMotion.Character, read.Motion);
        Assert.Equal(0.45, read.StepHeight);
        Assert.Equal(0.9, read.Steepest);
        Assert.Equal(0.02, read.Skin);
        Assert.Equal(120, read.Strength);
        Assert.Equal(1.0, read.CrouchHeight);
        Assert.Equal(0.8, read.CopyWith(crouchHeight: 0.8).CrouchHeight);
        Assert.Equal(0.45, read.CopyWith(crouchHeight: 0.8).StepHeight);
    }

    [Fact]
    public void AFileFromBeforeCharactersReadsAsOneThatDoesNotCrouch()
    {
        var body = BodyComponent.FromJson(Map(("motion", "driven")));

        Assert.Equal(0.3, body.StepHeight);
        Assert.Equal(0.01, body.Skin);
        Assert.Equal(500, body.Strength);
        Assert.Equal(0, body.CrouchHeight);
    }

    [Fact]
    public void AFileFromBeforeTriggersAndBeltsReadsAsAPlainBody()
    {
        var body = BodyComponent.FromJson(Map(("mass", 5L), ("asleep", true)));

        Assert.False(body.Trigger);
        Assert.False(body.Stay);
        Assert.Equal(Vector3.Zero(), body.Surface);
    }

    [Fact]
    public void AFileFromBeforeBodyControlsReadsAsAPlainBody()
    {
        var body = BodyComponent.FromJson(Map(("mass", 5L), ("trigger", true)));

        Assert.Empty(body.Locks);
        Assert.Equal(1, body.GravityScale);
        Assert.Equal(0, body.MaxSpeed);
        Assert.Equal(0, body.MaxSpin);
        Assert.False(body.Continuous);
        Assert.Equal(Vector3.Zero(), body.CentreOfMass);
        Assert.Equal(Vector3.Zero(), body.Inertia);
    }

    [Fact]
    public void LocksAreWrittenInOneOrderAndALockItDoesNotKnowGoes()
    {
        var body = BodyComponent.FromJson(Map(("locks", List("turnZ", "sideways", "moveX", "turnZ", 7L))));

        Assert.True(body.Locks.SetEquals([BodyLock.MoveX, BodyLock.TurnZ]));
        Same(List("moveX", "turnZ"), body.ToJson()["locks"]);
    }

    [Fact]
    public void AShapeOrAMotionItDoesNotKnowFallsBack()
    {
        var body = BodyComponent.FromJson(Map(("shape", "cone"), ("motion", "kinematic")));

        Assert.Equal(BodyShape.Box, body.Shape);
        Assert.Equal(BodyMotion.Free, body.Motion);
    }

    [Fact]
    public void IsTheCylinderAndTheHullItWasWrittenAs()
    {
        var cylinder = BodyComponent.FromJson(Map(("shape", "cylinder"), ("radius", 0.4), ("height", 1.2)));
        var hull = new BodyComponent(shape: BodyShape.Hull, hull: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1]);

        Assert.Equal(BodyShape.Cylinder, cylinder.Shape);
        Assert.Equal(0.4, cylinder.Radius);
        Assert.Equal(1.2, cylinder.Height);
        Assert.Equal(hull.Hull, BodyComponent.FromJson(hull.ToJson()).Hull);
        Assert.Equal(BodyShape.Hull, BodyComponent.FromJson(hull.ToJson()).Shape);
    }

    [Fact]
    public void AFileFromBeforeHullsReadsAsHavingNoCorners() =>
        Assert.Empty(BodyComponent.FromJson(Map(("mass", 5L))).Hull);

    [Fact]
    public void CornersThatAreNotAllNumbersAreNoneNotShifted()
    {
        var body = BodyComponent.FromJson(Map(("hull", List(0L, 0L, "one", 1L, 1L, 1L))));

        Assert.Empty(body.Hull);
        Assert.Empty(BodyComponent.FromJson(Map(("hull", "cube"))).Hull);
    }

    [Fact]
    public void ALayerMaskIsKeptToThirtyTwoBits()
    {
        var body = BodyComponent.FromJson(Map(("layers", -1L), ("cares", 2.0)));

        Assert.Equal(BodyComponent.EveryLayer, body.Layers);
        Assert.Equal(2, body.Cares);
    }

    // changing a body

    [Fact]
    public void ChangesWhatItWasAskedToAndKeepsTheRest()
    {
        var before = new BodyComponent(
            shape: BodyShape.Capsule,
            size: new Vector3(1, 2, 3),
            radius: 0.25,
            mass: 70,
            layers: 4,
            startsAsleep: true);
        var after = before.CopyWith(mass: 80, motion: BodyMotion.Driven);

        Assert.Equal(80, after.Mass);
        Assert.Equal(BodyMotion.Driven, after.Motion);
        Same(With(before.ToJson(), ("mass", 80.0), ("motion", "driven")), after.ToJson());
    }

    [Fact]
    public void KeepsItsCornersThroughAChangeAndChangesThemOnRequest()
    {
        var before = new BodyComponent(shape: BodyShape.Hull, hull: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1]);

        Assert.Equal(before.Hull, before.CopyWith(mass: 3).Hull);
        Assert.Equal([1.0, 2.0, 3.0], before.CopyWith(hull: [1, 2, 3]).Hull);
    }

    [Fact]
    public void HoldsItsCornersWhereNobodyCanChangeThem()
    {
        var corners = new double[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        var body = new BodyComponent(shape: BodyShape.Hull, hull: corners);
        corners[0] = 9;

        Assert.Equal(0, body.Hull[0]);
        Assert.Throws<NotSupportedException>(() => ((ICollection<double>)body.Hull).Add(1));
    }

    [Fact]
    public void DoesNotShareItsVectorsWithTheBodyItCameFrom()
    {
        var before = new BodyComponent(size: new Vector3(1, 2, 3));
        var after = before.CopyWith();
        after.Size.X = 9;
        after.Centre.Y = 9;
        after.Surface.Z = 9;

        Assert.Equal(new Vector3(1, 2, 3), before.Size);
        Assert.Equal(Vector3.Zero(), before.Centre);
        Assert.Equal(Vector3.Zero(), before.Surface);
    }

    [Fact]
    public void TurnsABodyIntoATriggerAndABelt()
    {
        var belt = new BodyComponent(motion: BodyMotion.Fixed)
            .CopyWith(trigger: true, stay: true, surface: new Vector3(0, 0, 3));

        Assert.True(belt.Trigger);
        Assert.True(belt.Stay);
        Assert.Equal(new Vector3(0, 0, 3), belt.Surface);
        Assert.Equal(new Vector3(0, 0, 3), belt.CopyWith(mass: 2).Surface);
    }

    [Fact]
    public void DoesNotShareItsCentreOfMassOrInertiaEither()
    {
        var before = new BodyComponent(centreOfMass: new Vector3(0, -0.2, 0), inertia: new Vector3(2, 3, 4));
        var after = before.CopyWith();
        after.CentreOfMass.Y = 9;
        after.Inertia.X = 9;

        Assert.Equal(new Vector3(0, -0.2, 0), before.CentreOfMass);
        Assert.Equal(new Vector3(2, 3, 4), before.Inertia);
    }

    [Fact]
    public void HoldsItsLocksWhereNobodyCanChangeThem()
    {
        var body = new BodyComponent(locks: new HashSet<BodyLock> { BodyLock.MoveY });

        Assert.Throws<NotSupportedException>(() => ((ICollection<BodyLock>)body.Locks).Add(BodyLock.TurnX));
        Assert.True(body.CopyWith(locks: new HashSet<BodyLock> { BodyLock.TurnZ }).Locks.SetEquals([BodyLock.TurnZ]));
        Assert.True(body.CopyWith(mass: 3).Locks.SetEquals([BodyLock.MoveY]));
    }

    // a body made of something

    [Fact]
    public void TakesTheFrictionAndRestitutionOfThePresetAndNoMore()
    {
        var wood = BodyMaterial.Presets.First(m => m.Name == "Wood");
        var body = new BodyComponent(mass: 9, layers: 4).MadeOf(wood);

        Assert.Equal(wood.Friction, body.Friction);
        Assert.Equal(wood.Restitution, body.Restitution);
        Assert.Equal(9, body.Mass);
        Assert.Equal(4, body.Layers);
    }

    [Fact]
    public void IsFoundAgainByItsNumbers()
    {
        foreach (var preset in BodyMaterial.Presets)
        {
            Assert.Same(preset, BodyMaterial.Of(new BodyComponent().MadeOf(preset)));
        }
    }

    [Fact]
    public void IsNothingInParticularOnceANumberIsItsOwn()
    {
        var rubber = BodyMaterial.Presets[^1];
        var body = new BodyComponent().MadeOf(rubber).CopyWith(friction: 0.96);

        Assert.Null(BodyMaterial.Of(body));
    }

    [Fact]
    public void IsNotMadeOfAnythingJustBecauseItIsNew() => Assert.Null(BodyMaterial.Of(new BodyComponent()));

    [Fact]
    public void HasPresetsThatTellEachOtherApart()
    {
        var numbers = BodyMaterial.Presets.Select(p => (p.Friction, p.Restitution)).ToHashSet();

        Assert.Equal(BodyMaterial.Presets.Count, numbers.Count);
    }

    // a body in a scene

    [Fact]
    public void KeepsTheSizeOfAShapeItIsNotUsing()
    {
        var box = new BodyComponent(size: new Vector3(2, 1, 4));
        var ball = BodyComponent.FromJson(With(box.ToJson(), ("shape", EnumNames.Of(BodyShape.Sphere))));
        var boxAgain = BodyComponent.FromJson(With(ball.ToJson(), ("shape", EnumNames.Of(BodyShape.Box))));

        Assert.Equal(new Vector3(2, 1, 4), boxAgain.Size);
    }

    [Fact]
    public void IsWrittenAfterWhatItDrawsAndBeforeWhatItLights()
    {
        var order = SceneComponents.Order.ToList();

        Assert.True(order.IndexOf(SceneComponents.Mesh) < order.IndexOf(SceneComponents.Body));
        Assert.True(order.IndexOf(SceneComponents.Body) < order.IndexOf(SceneComponents.Light));
    }
}
