using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Ported from morph_test.dart.</summary>
public sealed class MorphTests
{
    private static RenderObject One(long key, double[]? shapes = null) =>
        new()
        {
            Key = key,
            Transform = Matrix4.Identity(),
            Colour = new Vector3(1, 1, 1),
            Mesh = $"/models/{key}.glb",
            MorphWeights = shapes,
        };

    private static OrderedDictionary<string, object> Sent(params RenderObject[] objects) =>
        SceneBuilders.Of(objects).ToMessage();

    [Fact]
    public void AnObjectWithNoShapesTakesNoRoom()
    {
        var message = Sent(One(1), One(2));

        Assert.Equal([0, 0], SceneBuilders.Read<int[]>(message, "objectMorphCounts"));
    }

    [Fact]
    public void CountsSayWhereEachObjectsShapesAreAndTheyPackInOrder()
    {
        // The far side walks the weights alongside the objects, so the counts and the buffer must agree exactly.
        var message = Sent(One(1, [0.25]), One(2), One(3, [0.5, 0.75, 1.0]));

        Assert.Equal([1, 0, 3], SceneBuilders.Read<int[]>(message, "objectMorphCounts"));
        Assert.Equal([0.25f, 0.5f, 0.75f, 1.0f], SceneBuilders.Read<float[]>(message, "objectMorphWeights"));
    }

    [Fact]
    public void TheCountsAddUpToTheWeightsWhateverTheMix()
    {
        var message = Sent(One(1, [0.1, 0.2]), One(2), One(3, [0.3]), One(4, []));

        Assert.Equal(
            SceneBuilders.Read<float[]>(message, "objectMorphWeights").Length,
            SceneBuilders.Read<int[]>(message, "objectMorphCounts").Sum());
    }

    [Fact]
    public void ASceneWithNoShapesAnywhereStillSendsABuffer()
    {
        // The far side takes a pointer, and an empty array has none to give.
        Assert.NotEmpty(SceneBuilders.Read<float[]>(Sent(One(1)), "objectMorphWeights"));
    }
}
