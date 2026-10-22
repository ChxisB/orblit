using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class ParticleComponentTests
{
    private static ParticleEmitterComponent Read(JsonMap json) => ParticleEmitterComponent.FromJson(json);

    // reading an emitter

    [Fact]
    public void TakesTheDefaultsWhenTheFileSaysNothing()
    {
        var emitter = Read(Map());

        Assert.Equal(EmitterShape.Point, emitter.Shape);
        Assert.Equal(10, emitter.Rate);
        Assert.Equal(1, emitter.LifetimeMin);
        Assert.Equal(2, emitter.LifetimeMax);
        Assert.Equal([0.1], emitter.Size);
        Assert.Equal([1.0, 1.0, 1.0], emitter.Colour);
        Assert.Null(emitter.Duration);
        Assert.Equal(EmitterSpace.World, emitter.Space);
        Assert.Equal(1000, emitter.MaxParticles);
        Assert.Empty(emitter.Bursts);
    }

    [Fact]
    public void IsTheEmitterItWasWrittenAs()
    {
        var emitter = new ParticleEmitterComponent(
            seed: 7,
            shape: EmitterShape.Cone,
            radius: 0.2,
            angle: 0.6,
            extents: new Vector3(1, 2, 3),
            rate: 0,
            bursts: [new EmitterBurst(0.5, 12), new EmitterBurst(2, 3)],
            duration: 4,
            lifetimeMin: 0.4,
            lifetimeMax: 0.9,
            speedMin: 3,
            speedMax: 6,
            spinMin: -1,
            spinMax: 1,
            size: [0.05, 0.02, 0],
            colour: [1, 0.9, 0.4, 1, 0.3, 0.1],
            gravity: new Vector3(0, -9.8, 0),
            drag: 0.3,
            noiseStrength: 0.1,
            noiseScale: 2,
            space: EmitterSpace.Local,
            maxParticles: 200);

        var again = Read(emitter.ToJson());

        Same(emitter.ToJson(), again.ToJson());
        Assert.Equal(3, again.Bursts[^1].Count);
        Assert.Equal(new Vector3(1, 2, 3), again.Extents);
        Assert.Equal(new Vector3(0, -9.8, 0), again.Gravity);
        Assert.Equal(EmitterSpace.Local, again.Space);
    }

    [Fact]
    public void LeavesOutADurationThatIsNotSet()
    {
        Assert.False(new ParticleEmitterComponent().ToJson().ContainsKey("duration"));
        Assert.Equal(3, Read(Map(("duration", 3L))).Duration);
    }

    [Fact]
    public void KeepsADurationOfNoughtWhichIsNoBirths() =>
        Assert.Equal(0, Read(Map(("duration", 0L))).Duration);

    [Fact]
    public void ReadsABadNumberAsTheDefaultForThatNumber()
    {
        var emitter = Read(Map(
            ("rate", -1L),
            ("radius", double.NaN),
            ("angle", 4L),
            ("drag", -2L),
            ("noiseStrength", -1L),
            ("noiseScale", 0L),
            ("maxParticles", 0L),
            ("duration", -5L),
            ("shape", "blob"),
            ("space", "moon")));

        Assert.Equal(10, emitter.Rate);
        Assert.Equal(0.5, emitter.Radius);
        Assert.Equal(0.4, emitter.Angle);
        Assert.Equal(0, emitter.Drag);
        Assert.Equal(0, emitter.NoiseStrength);
        Assert.Equal(1, emitter.NoiseScale);
        Assert.Equal(1000, emitter.MaxParticles);
        Assert.Null(emitter.Duration);
        Assert.Equal(EmitterShape.Point, emitter.Shape);
        Assert.Equal(EmitterSpace.World, emitter.Space);
    }

    [Fact]
    public void ReadsASeedOrGravityThatIsNotFiniteAsTheDefault()
    {
        var emitter = Read(Map(("seed", double.PositiveInfinity), ("gravity", List(0L, double.NaN, 0L))));

        Assert.Equal(0, emitter.Seed);
        Assert.Equal(Vector3.Zero(), emitter.Gravity);
    }

    [Fact]
    public void ReadsARangeThatIsNotARangeAsTheDefaultRange()
    {
        foreach (var life in new[]
        {
            List(2L, 1L),
            List(0L, 1L),
            List(1L),
            List("a", "b"),
            List(1L, double.NaN),
        })
        {
            var emitter = Read(Map(("lifetime", life)));
            Assert.Equal([1.0, 2.0], [emitter.LifetimeMin, emitter.LifetimeMax]);
        }

        Assert.Equal(1, Read(Map(("speed", List(3L, 1L)))).SpeedMin);
        Assert.Equal(-2, Read(Map(("spin", List(-2L, 2L)))).SpinMin);
    }

    [Fact]
    public void ReadsARampThatIsNotWholeAsTheDefaultRamp()
    {
        Assert.Equal([0.1], Read(Map(("size", List()))).Size);
        Assert.Equal([0.1], Read(Map(("size", List(0.1, -1L)))).Size);
        Assert.Equal([1.0, 1.0, 1.0], Read(Map(("colour", List(1L, 1L)))).Colour);
        Assert.Equal(6, Read(Map(("colour", List(1L, 0L, 0L, 0L, 0L, 1L)))).Colour.Count);
    }

    [Fact]
    public void DropsABurstThatIsNotOneAndKeepsTheRest()
    {
        var emitter = Read(Map(("bursts", List(
            Map(("time", 1L), ("count", 5L)),
            Map(("time", -1L), ("count", 5L)),
            Map(("time", 1L), ("count", 0L)),
            Map(("time", 1L), ("count", 70000L)),
            "nonsense",
            Map(("time", 2L), ("count", 7L))))));

        Assert.Equal([5L, 7L], emitter.Bursts.Select(b => b.Count));
    }

    [Fact]
    public void ReadsExtentsWithANegativeSideAsTheDefaultBox() =>
        Assert.Equal(Vector3.All(0.5), Read(Map(("extents", List(1L, -1L, 1L)))).Extents);

    // changing an emitter

    [Fact]
    public void ChangesWhatItWasAskedToAndKeepsTheRest()
    {
        var before = new ParticleEmitterComponent(rate: 5, drag: 0.5);

        var after = before.CopyWith(rate: 20, shape: EmitterShape.Sphere);

        Assert.Equal(20, after.Rate);
        Assert.Equal(EmitterShape.Sphere, after.Shape);
        Assert.Equal(0.5, after.Drag);
    }

    [Fact]
    public void DoesNotShareItsVectorsWithWhatItWasBuiltFrom()
    {
        var g = new Vector3(0, -9.8, 0);
        var emitter = new ParticleEmitterComponent(gravity: g);
        g.Y = 0;
        emitter.CopyWith().Gravity.Y = 5;

        Assert.Equal(new Vector3(0, -9.8, 0), emitter.Gravity);
    }

    // an emitter in a scene

    [Fact]
    public void IsReadBackAsAnEmitterNotAnUnknownComponent()
    {
        var document = DocumentOf(new ParticleEmitterComponent(bursts: [new EmitterBurst(1, 4)]));
        var back = SceneDocument.Decode(document.Encode());

        Assert.Empty(back.Problems);
        Assert.IsType<ParticleEmitterComponent>(back.Document["fire"]![SceneComponents.Particles]);
        Assert.Equal(document.Encode(), back.Document.Encode());
    }

    [Fact]
    public void IsWrittenAfterTheCanvasAndBeforeTheData()
    {
        var order = SceneComponents.Order.ToList();

        Assert.True(order.IndexOf(SceneComponents.Particles) > order.IndexOf(SceneComponents.Canvas));
        Assert.True(order.IndexOf(SceneComponents.Data) > order.IndexOf(SceneComponents.Particles));
    }

    [Fact]
    public void AddingOneIsAChangeThatGoesBack()
    {
        var a = DocumentOf(null);
        var b = DocumentOf(new ParticleEmitterComponent(rate: 40));

        DiffAssert.RoundTrips(SceneDiff.Between(a, b), a, b);
    }

    [Fact]
    public void AChangedFieldIsOneOperationAndItGoesBack()
    {
        var a = DocumentOf(new ParticleEmitterComponent(rate: 1));
        var b = DocumentOf(new ParticleEmitterComponent(rate: 2));
        var diff = SceneDiff.Between(a, b);

        Assert.Equal("rate", Assert.IsType<SetField>(Assert.Single(diff.Operations)).Field);
        DiffAssert.RoundTrips(diff, a, b);
    }

    private static SceneDocument DocumentOf(ParticleEmitterComponent? emitter)
    {
        var components = new Dictionary<string, SceneComponent> { [SceneComponents.Transform] = new TransformComponent() };
        if (emitter is not null)
        {
            components[SceneComponents.Particles] = emitter;
        }

        return new SceneDocument("Scene", entities: [new SceneEntity("fire", "Fire", components: components)]);
    }
}
