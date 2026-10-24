using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Ported from population_test.dart.</summary>
public sealed class PopulationTests
{
    private static Population Crowd(int count, int revision = 1, int key = 1) =>
        new(key, new float[count * 16], new float[count * 3], new Bounds(new Vector3(-10, 0, -10), new Vector3(10, 4, 10)))
        {
            Revision = revision,
        };

    private static Population One(Fade fade = Fade.Sink) =>
        new(1, new float[16], new float[3], new Bounds(Vector3.Zero(), Vector3.All(1))) { Range = 50, Fade = fade };

    private static RenderScene SceneOf(params Population[] populations) =>
        SceneBuilders.Of([]) with { Populations = populations };

    private static Held HeldWith(params (int Key, int Revision)[] sent)
    {
        var held = new Held();
        foreach (var (key, revision) in sent)
        {
            held.Populations[key] = revision;
        }

        return held;
    }

    [Fact]
    public void AFreshRendererIsSentEverything()
    {
        var message = SceneOf(Crowd(100)).ToMessage(1);

        Assert.Equal(100 * 16, SceneBuilders.Read<float[]>(message, "populationTransforms").Length);
        Assert.Equal(100 * 3, SceneBuilders.Read<float[]>(message, "populationColours").Length);
        Assert.Equal(1, SceneBuilders.Read<int[]>(message, "populationChanged")[0]);
    }

    [Fact]
    public void ARendererThatAlreadyHasThemIsSentNoneOfIt()
    {
        // The whole performance story in one assertion. A hundred thousand transforms is six
        // megabytes, and a scene that is standing still must not send it sixty times a second to
        // say that nothing moved.
        var message = SceneOf(Crowd(100000, revision: 4)).ToMessage(1, HeldWith((1, 4)));

        Assert.Empty(SceneBuilders.Read<float[]>(message, "populationTransforms"));
        Assert.Empty(SceneBuilders.Read<float[]>(message, "populationColours"));
        Assert.Empty(SceneBuilders.Read<int[]>(message, "populationChanged"));

        // It still hears how many there are and where they are, because that is what tells it
        // whether to rebuild and how to cull.
        Assert.Equal(100000, SceneBuilders.Read<int[]>(message, "populationCounts")[0]);
        Assert.Equal(4, SceneBuilders.Read<int[]>(message, "populationRevisions")[0]);
    }

    [Fact]
    public void OnlyTheOneThatMovedIsSent()
    {
        var message = SceneOf(Crowd(10, revision: 2, key: 1), Crowd(20, revision: 9, key: 2))
            .ToMessage(1, HeldWith((1, 2), (2, 8)));

        Assert.Equal(2, SceneBuilders.Read<int[]>(message, "populationChanged").Single());
        Assert.Equal(20 * 16, SceneBuilders.Read<float[]>(message, "populationTransforms").Length);
    }

    [Fact]
    public void TheChangedBuffersArePackedInTheOrderTheyAreNamed()
    {
        var first = Crowd(2, key: 1);
        var second = Crowd(3, key: 2);
        first.Transforms[0] = 7;
        second.Transforms[0] = 11;

        var message = SceneOf(first, second).ToMessage(1);
        var packed = SceneBuilders.Read<float[]>(message, "populationTransforms");

        Assert.Equal([1, 2], SceneBuilders.Read<int[]>(message, "populationChanged"));
        Assert.Equal(7, packed[0]);

        // Three transforms of sixteen after the first population's two.
        Assert.Equal(11, packed[2 * 16]);
    }

    [Fact]
    public void HowFarAPopulationIsDrawnFromTravelsWithIt()
    {
        var far = new Population(
            1, new float[16], new float[3], new Bounds(new Vector3(-800, 0, -800), new Vector3(800, 6, 800)))
        {
            Range = 350,
        };

        Assert.Equal(350, SceneBuilders.Read<float[]>(SceneOf(far).ToMessage(1), "populationRanges").Single());
    }

    [Fact]
    public void DrawingEverythingIsTheDefaultAndSaysSo()
    {
        // Nought and not infinity, because nought is what "no opinion" reads as in a float array
        // and infinity is what a mistake reads as.
        Assert.Equal(0, Crowd(4).Range);
    }

    [Fact]
    public void ASceneWithNoPopulationsSaysNothingAboutThem()
    {
        Assert.False(SceneOf().ToMessage(1).ContainsKey("populationKeys"));
    }

    [Fact]
    public void ATransformAndAColourAreRequiredOfEveryMember()
    {
        Assert.Throws<ArgumentException>(
            () => new Population(1, new float[32], new float[3], new Bounds(Vector3.Zero(), Vector3.Zero())));
    }

    [Fact]
    public void SinkingIsTheDefaultAndIsBitTwoClear()
    {
        Assert.Equal(0, One().Flags & 4);
    }

    [Fact]
    public void EachModeIsItsOwnValueInBitsTwoAndThree()
    {
        Assert.Equal(0, (One(Fade.Sink).Flags >> 2) & 3);
        Assert.Equal(1, (One(Fade.Shrink).Flags >> 2) & 3);
        Assert.Equal(2, (One(Fade.None).Flags >> 2) & 3);
    }

    [Fact]
    public void EveryModeFitsInTheTwoBitsItIsGiven()
    {
        // The layer starts at bit eight and the shadows are below, so a fourth mode runs into
        // neither, and this is the guard for adding a fifth.
        Assert.True(Enum.GetValues<Fade>().Length <= 4);
    }

    [Fact]
    public void ItDoesNotDisturbTheShadowBitsOrTheLayer()
    {
        // Bit two sits between the shadow flags and the layer, and a flag that collides with
        // either is a population that stops casting shadows when somebody changes how it fades.
        var planted = new Population(1, new float[16], new float[3], new Bounds(Vector3.Zero(), Vector3.All(1)))
        {
            CastShadows = true,
            ReceiveShadows = true,
            Layer = 3,
            Fade = Fade.Shrink,
        };

        Assert.Equal(1, planted.Flags & 1);
        Assert.Equal(2, planted.Flags & 2);
        Assert.Equal(1, (planted.Flags >> 2) & 3);
        Assert.Equal(3, planted.Flags >> 8);
    }
}
