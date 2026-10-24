using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class BoundsAddedTests
{
    private static Bounds CubeAt(double x, double y, double z, double half = 0.5) =>
        new(new Vector3(x - half, y - half, z - half), new Vector3(x + half, y + half, z + half));

    [Fact]
    public void NoBoxesAreNoBoundsRatherThanAnEmptyBox()
    {
        Assert.Null(Bounds.Around([]));
    }

    [Fact]
    public void BoxesThatOnlyTouchOverlapAndAFaceHoldItsPoints()
    {
        var left = CubeAt(0, 0, 0);
        var right = CubeAt(1, 0, 0);

        Assert.True(left.Overlaps(right));
        Assert.True(right.Overlaps(left));
        Assert.True(left.Contains(new Vector3(0.5, 0, 0)));
        Assert.False(left.Contains(new Vector3(0.5001, 0, 0)));
        Assert.False(left.Overlaps(CubeAt(1.001, 0, 0)));
    }

    [Fact]
    public void AUnionIsTheSmallestBoxHoldingBoth()
    {
        var both = CubeAt(0, 0, 0).Union(CubeAt(4, -2, 1));

        Assert.Equal(new Vector3(-0.5, -2.5, -0.5), both.Minimum);
        Assert.Equal(new Vector3(4.5, 0.5, 1.5), both.Maximum);
        Assert.Equal(new Vector3(-0.5, -2.5, -0.5), Bounds.Around([CubeAt(0, 0, 0), CubeAt(4, -2, 1)])!.Minimum);
    }

    [Fact]
    public void AFrustumThatSeesNothingCullsEverything()
    {
        var tree = Bvh.Of([.. Enumerable.Range(0, 20).Select(i => new Volume(i, CubeAt(i * 2.0, 0, 0)))]);

        // Everything in front of x = 100, and nothing is that far.
        var visible = tree.Visible(
        [
            new Vector4(1, 0, 0, -100),
            new Vector4(0, 1, 0, 100),
            new Vector4(0, -1, 0, 100),
            new Vector4(0, 0, 1, 100),
            new Vector4(0, 0, -1, 100),
            new Vector4(-1, 0, 0, 1000),
        ]);

        Assert.Empty(visible);
    }

    [Fact]
    public void ManyBoxesInOnePlaceStillFormAFiniteTreeAndAreAllFound()
    {
        var tree = Bvh.Of([.. Enumerable.Range(0, 200).Select(i => new Volume(i, CubeAt(0, 0, 0)))]);

        Assert.Equal(200, tree.Length);
        Assert.Equal(200, tree.Inside(CubeAt(0, 0, 0)).Count);
        Assert.InRange(tree.Depth, 1, 200);
    }

    [Fact]
    public void TheFirstHitIsTheNearestOfSeveral()
    {
        var tree = Bvh.Of(
        [
            new Volume(30, CubeAt(30, 0, 0)),
            new Volume(10, CubeAt(10, 0, 0)),
            new Volume(20, CubeAt(20, 0, 0)),
        ]);

        var first = tree.First(Vector3.Zero(), new Vector3(1, 0, 0));

        Assert.NotNull(first);
        Assert.Equal(10, first.Value.Key);
        Assert.Equal(9.5, first.Value.Distance, 1e-9);
        Assert.Null(tree.First(Vector3.Zero(), new Vector3(-1, 0, 0)));
    }
}
