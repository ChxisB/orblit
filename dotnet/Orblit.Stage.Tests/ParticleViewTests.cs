using Orblit.Filament;
using Orblit.Particles;
using Orblit.Scene;
using Orblit.VectorMath;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

public class ParticleViewTests
{
    // Ten born a second, each living five and going nowhere, so a count says how long the clock has
    // run and a place says where the entity was.
    private static readonly ParticleEmitterComponent Still = new(
        lifetimeMin: 5,
        lifetimeMax: 5,
        speedMin: 0,
        speedMax: 0);

    private static SceneEntity EmitterAt(string id, ParticleEmitterComponent emitter, double x = 0, bool visible = true) =>
        Thing(
            id,
            visible: visible,
            components: Parts(
                (SceneComponents.Transform, new TransformComponent(position: new Vector3(x, 0, 0))),
                (SceneComponents.Particles, emitter)));

    private static DocumentView ViewOf(params SceneEntity[] entities) => new(Of(entities));

    private static void Change(DocumentView view, SceneEntity entity) =>
        view.Replace(view.Document.WithEntity(entity.Id, entity));

    private static List<double> Xs(Population population) =>
        [.. Enumerable.Range(0, population.Count).Select(i => (double)population.Transforms[(i * 16) + 12])];

    private static Vector3 Column(float[] transforms, int at) =>
        new(transforms[at], transforms[at + 1], transforms[at + 2]);

    [Fact]
    public void AnEmitterIsOnePopulationKeyedFromTheKey()
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", Still)), key: 700);

        particles.Advance(3);

        var drawn = Assert.Single(particles.Populations);
        Assert.Equal(700, drawn.Key);
        Assert.Equal(31, drawn.Count);
        Assert.Null(drawn.Mesh);
    }

    [Fact]
    public void ADefaultComponentIsDrawnAsADefaultEmitter()
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", new ParticleEmitterComponent())), key: 0);
        var wanted = new ParticleBuffer(1000);

        particles.Advance(1.7);
        new Emitter().Sample(1.7, wanted);

        var drawn = Assert.Single(particles.Populations);
        Assert.Equal(wanted.Count, drawn.Count);
        for (var i = 0; i < wanted.Count; i++)
        {
            Assert.Equal(wanted.Positions[i * 3], drawn.Transforms[(i * 16) + 12]);
            Assert.Equal(wanted.Positions[(i * 3) + 1], drawn.Transforms[(i * 16) + 13]);
            Assert.Equal(wanted.Positions[(i * 3) + 2], drawn.Transforms[(i * 16) + 14]);
        }
    }

    [Fact]
    public void AParticleFacesTheCameraAtItsSize()
    {
        var view = ViewOf(EmitterAt("a", Still));
        view.Through = new RenderCamera { Position = new Vector3(0, 0, 10), Target = Vector3.Zero() };
        var particles = new ParticleView(view, key: 0);

        particles.Advance(1);

        var t = Assert.Single(particles.Populations).Transforms;
        Near.Vector(Column(t, 8), new Vector3(0, 0, 0.1), tolerance: 1e-6);
        Assert.Equal(0.1, Column(t, 0).Length, 1e-6);
        Assert.Equal(0.1, Column(t, 4).Length, 1e-6);
        Assert.Equal(0, Column(t, 0).Dot(Column(t, 4)), 1e-6);
    }

    [Fact]
    public void LookingStraightDownStillGivesATurnedSquare()
    {
        var view = ViewOf(EmitterAt("a", Still));
        view.Through = new RenderCamera { Position = new Vector3(0, 10, 0), Target = Vector3.Zero() };
        var particles = new ParticleView(view, key: 0);

        particles.Advance(1);

        var t = Assert.Single(particles.Populations).Transforms;
        Assert.All(t, one => Assert.True(float.IsFinite(one)));
        Assert.Equal(0.1, t[9], 1e-6);
    }

    [Fact]
    public void TheBoxHoldsEveryParticleWithItsSize()
    {
        var emitter = new ParticleEmitterComponent(shape: EmitterShape.Sphere, radius: 3, size: [0.4]);
        var particles = new ParticleView(ViewOf(EmitterAt("a", emitter)), key: 0);

        particles.Advance(2);

        var drawn = Assert.Single(particles.Populations);
        for (var i = 0; i < drawn.Count; i++)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var at = drawn.Transforms[(i * 16) + 12 + axis];
                Assert.True(at - 0.4 >= drawn.Bounds.Minimum[axis] - 1e-6, $"low {axis} of {i}");
                Assert.True(at + 0.4 <= drawn.Bounds.Maximum[axis] + 1e-6, $"high {axis} of {i}");
            }
        }
    }

    [Fact]
    public void ThePopulationHandedOutIsNotChangedByTheNextFrame()
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", Still)), key: 0);
        particles.Advance(1);
        var first = Assert.Single(particles.Populations);
        var before = first.Transforms.ToArray();

        particles.Advance(3);

        Assert.Equal(before, first.Transforms);
    }

    [Fact]
    public void TheClockStartsAgainWhenTheEntityIsHiddenAndShown()
    {
        var view = ViewOf(EmitterAt("a", Still));
        var particles = new ParticleView(view, key: 0);
        particles.Advance(3);
        Assert.Equal(31, Assert.Single(particles.Populations).Count);

        Change(view, EmitterAt("a", Still, visible: false));
        particles.Advance(1);
        Assert.Empty(particles.Populations);

        Change(view, EmitterAt("a", Still));
        particles.Advance(1);
        Assert.Equal(11, Assert.Single(particles.Populations).Count);
    }

    [Fact]
    public void TheClockIsKeptWhenTheComponentIsEdited()
    {
        var view = ViewOf(EmitterAt("a", Still));
        var particles = new ParticleView(view, key: 0);
        particles.Advance(3);

        Change(view, EmitterAt("a", Still.CopyWith(rate: 20)));
        particles.Advance(0);

        Assert.Equal(61, Assert.Single(particles.Populations).Count);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    [InlineData(double.PositiveInfinity)]
    public void TheClockDoesNotMoveForAStepThatIsNotATime(double step)
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", Still)), key: 0);
        particles.Advance(1);

        particles.Advance(step);

        Assert.Equal(11, Assert.Single(particles.Populations).Count);
    }

    [Fact]
    public void TheRevisionMovesOnEachFrame()
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", Still)), key: 0);
        particles.Advance(1);
        var first = Assert.Single(particles.Populations).Revision;

        particles.Advance(0);

        Assert.True(Assert.Single(particles.Populations).Revision > first);
    }

    [Fact]
    public void AWorldParticleStaysWhereItWasBorn()
    {
        var view = ViewOf(EmitterAt("a", Still));
        var particles = new ParticleView(view, key: 0);
        particles.Advance(1);
        Change(view, EmitterAt("a", Still, x: 10));
        particles.Advance(1);
        particles.Advance(1);

        var where = Xs(Assert.Single(particles.Populations));

        Assert.Equal(20, where.Count(x => Math.Abs(x) < 1e-6));
        Assert.Equal(11, where.Count(x => Math.Abs(x - 10) < 1e-6));
    }

    [Fact]
    public void ALocalParticleGoesWithTheEntity()
    {
        var local = Still.CopyWith(space: EmitterSpace.Local);
        var view = ViewOf(EmitterAt("a", local));
        var particles = new ParticleView(view, key: 0);
        particles.Advance(2);
        Change(view, EmitterAt("a", local, x: 10));

        particles.Advance(1);

        Assert.All(Xs(Assert.Single(particles.Populations)), x => Assert.Equal(10, x));
    }

    [Fact]
    public void AnEntityThatNeverMovesKeepsOnePlace()
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", Still, x: 4)), key: 0);
        for (var i = 0; i < 100; i++)
        {
            particles.Advance(0.1);
        }

        Assert.All(Xs(Assert.Single(particles.Populations)), x => Assert.Equal(4, x));
    }

    [Fact]
    public void ACrowdIsCutToTheBudgetTheLaterEmittersFirst()
    {
        var particles = new ParticleView(ViewOf(EmitterAt("a", Still), EmitterAt("b", Still)), key: 0, budget: 40);

        particles.Advance(3);

        Assert.Equal([31, 9], particles.Populations.Select(one => one.Count));
        Assert.Equal(2, particles.Populations.Select(one => one.Key).Distinct().Count());
    }

    [Fact]
    public void AnEmitterWithNothingAliveHasNoPopulation()
    {
        var particles = new ParticleView(
            ViewOf(EmitterAt("a", Still.CopyWith(rate: 0)), EmitterAt("b", Still)),
            key: 0);

        particles.Advance(1);

        Assert.Single(particles.Populations);
    }

    [Fact]
    public void NothingIsDrawnFromAnEntityWithNoEmitter()
    {
        var particles = new ParticleView(ViewOf(Thing("a")), key: 0);

        particles.Advance(1);

        Assert.Empty(particles.Populations);
    }

    [Fact]
    public void AComponentThatMakesNoEmitterIsNamedAndLeftOut()
    {
        var view = ViewOf(EmitterAt("bad", new ParticleEmitterComponent(drag: -1)), EmitterAt("good", Still));
        var particles = new ParticleView(view, key: 0);

        particles.Advance(1);

        Assert.Single(particles.Populations);
        Assert.Equal(["bad"], particles.Rejected.Keys);
        Assert.Contains("drag", particles.Rejected["bad"], StringComparison.OrdinalIgnoreCase);

        Change(view, EmitterAt("bad", Still));
        particles.Advance(0);

        Assert.Empty(particles.Rejected);
        Assert.Equal(2, particles.Populations.Count);
    }

    [Fact]
    public void TheViewsCameraIsTheOneItLooksThrough()
    {
        var view = ViewOf();
        var lens = new RenderCamera { Position = new Vector3(1, 2, 3), Target = Vector3.Zero() };

        view.Through = lens;

        Assert.Same(lens, view.Camera);
    }
}
