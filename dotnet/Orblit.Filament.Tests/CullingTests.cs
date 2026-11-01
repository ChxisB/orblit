using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class CullingTests
{
    private static Bounds CubeAt(double x, double y, double z, double half = 0.5) =>
        new(new Vector3(x - half, y - half, z - half), new Vector3(x + half, y + half, z + half));

    /// <summary>A line of boxes, two metres apart.</summary>
    private static Volume[] Line(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new Volume(i, CubeAt(i * 2.0, 0, 0)))];

    /// <summary>Six planes of an axis-aligned box, normals pointing inwards.</summary>
    private static Vector4[] BoxFrustum(double half) =>
    [
        new(1, 0, 0, half),
        new(-1, 0, 0, half),
        new(0, 1, 0, half),
        new(0, -1, 0, half),
        new(0, 0, 1, half),
        new(0, 0, -1, half),
    ];

    [Fact]
    public void ARayDownAnAxisEntersAndLeavesWhereItShould()
    {
        var hit = CubeAt(0, 0, 0).Hit(new Vector3(-10, 0, 0), new Vector3(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Equal(9.5, hit.Value.Near, 1e-9);
        Assert.Equal(10.5, hit.Value.Far, 1e-9);
    }

    [Fact]
    public void ARayParallelToAFaceEitherMissesOrNeverLeaves()
    {
        var box = CubeAt(0, 0, 0);

        // Along x at y = 0: inside that pair of faces the whole way.
        Assert.NotNull(box.Hit(new Vector3(-10, 0, 0), new Vector3(1, 0, 0)));
        // Along x at y = 5: outside them the whole way, and no divide by zero.
        Assert.Null(box.Hit(new Vector3(-10, 5, 0), new Vector3(1, 0, 0)));
    }

    [Fact]
    public void ARayPointingAwayFromTheBoxStillReportsWhereItWouldGo()
    {
        var hit = CubeAt(0, 0, 0).Hit(new Vector3(10, 0, 0), new Vector3(1, 0, 0));

        Assert.Null(hit);
    }

    [Fact]
    public void AreaIsWhatTheTreeIsBuiltToMinimise()
    {
        Assert.Equal(6, CubeAt(0, 0, 0, 0.5).Area, 1e-9);
        Assert.Equal(2 * (2 + 6 + 3), new Bounds(Vector3.Zero(), new Vector3(1, 2, 3)).Area, 1e-9);
    }

    [Fact]
    public void AnEmptySceneIsAnEmptyTreeNotABrokenOne()
    {
        var tree = Bvh.Of([]);

        Assert.True(tree.IsEmpty);
        Assert.Null(tree.Bounds);
        Assert.Empty(tree.Visible([]));
        Assert.Null(tree.First(Vector3.Zero(), new Vector3(1, 0, 0)));
    }

    [Fact]
    public void ItHoldsEverythingItWasGiven()
    {
        var tree = Bvh.Of(Line(100));

        Assert.Equal(100, tree.Length);
        Assert.Equal(-0.5, tree.Bounds!.Minimum.X, 1e-9);
        Assert.Equal(198.5, tree.Bounds.Maximum.X, 1e-9);
    }

    [Fact]
    public void ItIsDeepEnoughToBeATreeAndShallowEnoughToBeWorthOne()
    {
        // A hundred things in leaves of four is about five levels if it splits evenly.
        // Anything near a hundred would mean it degenerated into a list.
        var tree = Bvh.Of(Line(100));

        Assert.InRange(tree.Depth, 4, 11);
    }

    [Fact]
    public void ABoxQueryFindsExactlyWhatOverlapsIt()
    {
        var found = Bvh.Of(Line(100)).Inside(new Bounds(new Vector3(-1, -1, -1), new Vector3(5, 1, 1)));
        found.Sort();

        // Boxes centred at 0, 2 and 4, each half a metre across.
        Assert.Equal([0L, 1L, 2L], found);
    }

    [Fact]
    public void ARayFindsWhatItPassesThroughNearestFirst()
    {
        var hits = Bvh.Of(Line(20)).Along(new Vector3(-10, 0, 0), new Vector3(1, 0, 0));

        Assert.Equal(20, hits.Count);
        Assert.Equal(0, hits[0].Key);
        Assert.Equal(19, hits[^1].Key);
        Assert.Equal(9.5, hits[0].Distance, 1e-9);
    }

    [Fact]
    public void ARayThatMissesEverythingFindsNothing()
    {
        Assert.Null(Bvh.Of(Line(20)).First(new Vector3(-10, 50, 0), new Vector3(1, 0, 0)));
    }

    [Fact]
    public void ARayCanBeCutShort()
    {
        // Ten metres from x = -10 reaches x = 0, which is the first box only.
        var hits = Bvh.Of(Line(20)).Along(new Vector3(-10, 0, 0), new Vector3(1, 0, 0), within: 10);

        Assert.Equal([0L], hits.Select(hit => hit.Key));
    }

    [Fact]
    public void TheAnswerIsTheSameAsAskingEverythingOneAtATime()
    {
        // The whole claim of a tree: a faster way to the same answer, checked against the slow way.
        var random = new Random(7);
        var volumes = Enumerable.Range(0, 400)
            .Select(i => new Volume(
                i,
                CubeAt(
                    (random.NextDouble() * 200) - 100,
                    (random.NextDouble() * 200) - 100,
                    (random.NextDouble() * 200) - 100,
                    (random.NextDouble() * 3) + 0.5)))
            .ToArray();
        var query = new Bounds(new Vector3(-20, -20, -20), new Vector3(20, 20, 20));

        var byTree = Bvh.Of(volumes).Inside(query);
        byTree.Sort();
        var byHand = volumes.Where(volume => volume.Bounds.Overlaps(query)).Select(volume => volume.Key).Order().ToList();

        Assert.Equal(byHand, byTree);
        Assert.NotEmpty(byHand);
    }

    [Fact]
    public void ARayAgreesWithAskingEverythingOneAtATime()
    {
        var random = new Random(11);
        var volumes = Enumerable.Range(0, 300)
            .Select(i => new Volume(
                i,
                CubeAt(
                    (random.NextDouble() * 100) - 50,
                    (random.NextDouble() * 100) - 50,
                    (random.NextDouble() * 100) - 50,
                    2)))
            .ToArray();
        var from = new Vector3(-200, 3, 7);
        var along = new Vector3(1, 0, 0);

        var byTree = Bvh.Of(volumes).Along(from, along).Select(hit => hit.Key).ToHashSet();
        var byHand = volumes.Where(volume => volume.Bounds.Hit(from, along.Normalized()) is not null).Select(volume => volume.Key).ToHashSet();

        Assert.Equal(byHand, byTree);
    }

    [Fact]
    public void WhatIsInsideIsKeptAndWhatIsOutsideIsNot()
    {
        var tree = Bvh.Of([new Volume(1, CubeAt(0, 0, 0)), new Volume(2, CubeAt(5, 0, 0)), new Volume(3, CubeAt(100, 0, 0))]);

        var seen = tree.Visible(BoxFrustum(10));
        seen.Sort();

        Assert.Equal([1L, 2L], seen);
    }

    [Fact]
    public void ABoxStraddlingAPlaneIsKept()
    {
        // Culling has to be conservative: dropping something half in shot is a visible fault,
        // and keeping something just out of it costs a draw.
        var tree = Bvh.Of([new Volume(1, CubeAt(10, 0, 0, 2))]);

        Assert.Equal([1L], tree.Visible(BoxFrustum(10)));
    }

    [Fact]
    public void BoxesFollowWhatMovedWithoutTheTreeBeingRebuilt()
    {
        var tree = Bvh.Of([new Volume(1, CubeAt(0, 0, 0)), new Volume(2, CubeAt(10, 0, 0))]);
        var nodesBefore = tree.NodeCount;

        tree.Refit(key => key == 2 ? CubeAt(50, 0, 0) : null);

        Assert.Equal(nodesBefore, tree.NodeCount);
        Assert.Equal(50.5, tree.Bounds!.Maximum.X, 1e-9);
        Assert.Equal([2L], tree.Inside(new Bounds(new Vector3(49, -1, -1), new Vector3(51, 1, 1))));
        // The one that did not move is where it was.
        Assert.Equal([1L], tree.Inside(new Bounds(new Vector3(-1, -1, -1), new Vector3(1, 1, 1))));
    }
}
