using static Orblit.Motion.Tests.Rigs;

namespace Orblit.Motion.Tests;

public class RestSkeletonTests
{
    [Fact]
    public void StandsBonesInTheWorldThroughTheirParents()
    {
        var rig = Target();
        var world = rig.Pose();
        var skeleton = rig.Skeleton;
        for (var i = 0; i < rig.Names.Count; i++)
        {
            var expected = TurnOf(world[i]);
            var actual = skeleton.WorldRotationOf(i).AsRotationMatrix();
            for (var e = 0; e < 9; e++)
            {
                Assert.Equal(expected[e], actual[e], 1e-9);
            }
        }
    }

    [Fact]
    public void KnowsItsOwnBonesByName()
    {
        var skeleton = Source().Skeleton;

        Assert.Equal(6, skeleton.Length);
        Assert.Equal(3, skeleton.IndexOf("head"));
        Assert.Null(skeleton.IndexOf("tail"));
    }

    [Fact]
    public void MeasuresHowBigItIsFromWhereTheRootsHang()
    {
        var small = Source();
        var tall = Target();

        Assert.Equal(small.Reach, small.Skeleton.Reach, 1e-9);
        Assert.Equal(tall.Reach, tall.Skeleton.Reach, 1e-9);
        Assert.True(tall.Skeleton.Reach > small.Skeleton.Reach);
    }

    [Fact]
    public void RefusesAParentThatDoesNotComeFirst() =>
        Assert.Throws<ArgumentException>(
            () => new RestSkeleton(["a", "b"], [1, -1], [Matrix4.Identity(), Matrix4.Identity()]));

    [Fact]
    public void RefusesTwoBonesWithOneName() =>
        Assert.Throws<ArgumentException>(
            () => new RestSkeleton(["a", "a"], [-1, 0], [Matrix4.Identity(), Matrix4.Identity()]));

    [Fact]
    public void RefusesListsThatAreNotTheSameLength() =>
        Assert.Throws<ArgumentException>(
            () => new RestSkeleton(["a", "b"], [-1], [Matrix4.Identity(), Matrix4.Identity()]));
}
