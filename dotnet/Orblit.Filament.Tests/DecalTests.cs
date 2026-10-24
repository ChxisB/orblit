using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Ported from decal_test.dart.</summary>
public sealed class DecalTests
{
    private static RenderScene SceneOf(params Decal[] decals) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5)) with { Decals = decals };

    private static Decal Unit(long key, Texture? texture = null) =>
        new() { Key = key, Position = Vector3.Zero(), Size = Vector3.All(1), Texture = texture };

    [Fact]
    public void ADecalPacksEveryFieldWhereTheRendererReadsIt()
    {
        var decal = new Decal
        {
            Key = 7,
            Position = new Vector3(1, 2, 3),
            Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 2),
            Size = new Vector3(2, 0.5, 4),
            Colour = new Vector3(0.1, 0.2, 0.3),
            Opacity = 0.75,
            FadeStartAngle = 0.5,
            FadeEndAngle = 1.2,
            Roughness = 0.05,
            Layers = new HashSet<int> { 0, 2 },
            SortOrder = 3,
        };
        var packed = new float[Decal.Stride];
        decal.Pack(packed, 0);

        Assert.Equal([1f, 2f, 3f], packed[0..3]);
        var half = Math.Sqrt(0.5);
        SceneBuilders.Near(half, packed[3]);
        SceneBuilders.Near(half, packed[6]);
        Assert.Equal([2f, 0.5f, 4f], packed[7..10]);
        SceneBuilders.Near(0.1, packed[10]);
        Assert.Equal(0.75f, packed[13]);
        Assert.Equal(0.5f, packed[14]);
        SceneBuilders.Near(1.2, packed[15]);
        // A roughness asked for is painted in full. A metalness not asked for leaves the
        // surface's own alone.
        SceneBuilders.Near(0.05, packed[16]);
        Assert.Equal(1f, packed[17]);
        Assert.Equal(0f, packed[19]);
        Assert.Equal(5f, packed[20]);
        Assert.Equal(3f, packed[21]);
    }

    [Fact]
    public void ADecalReachesEveryLayerUnlessToldOtherwise()
    {
        Assert.Equal(0x7F, Unit(1).LayerMask);

        // Layers past six do not exist.
        Assert.Equal(0, (Unit(1) with { Layers = new HashSet<int> { 9, -1 } }).LayerMask);
    }

    [Fact]
    public void DecalsSendEachPictureOnceAndPointAtIt()
    {
        var poster = new Texture("/tmp/poster.png");
        var message = SceneOf(Unit(1, poster), Unit(2), Unit(3, poster)).ToMessage();

        Assert.Equal(["/tmp/poster.png"], SceneBuilders.Read<string[]>(message, "decalPaths"));
        Assert.Equal([0, -1, 0], SceneBuilders.Read<int[]>(message, "decalImages"));
        Assert.Equal(3 * Decal.Stride, SceneBuilders.Read<float[]>(message, "decalParams").Length);
    }

    [Fact]
    public void ASceneWithoutDecalsSendsEmptyArraysNotNothing()
    {
        var message = SceneOf().ToMessage();

        Assert.Empty(SceneBuilders.Read<float[]>(message, "decalParams"));
        Assert.Empty(SceneBuilders.Read<int[]>(message, "decalImages"));
        Assert.Empty(SceneBuilders.Read<string[]>(message, "decalPaths"));
    }

    [Fact]
    public void WithKeepsTheDecalsAndCanReplaceThem()
    {
        var scene = SceneOf(Unit(1));

        Assert.Single((scene with { Sky = new Sky() }).Decals);
        Assert.Empty((scene with { Decals = [] }).Decals);
    }
}
