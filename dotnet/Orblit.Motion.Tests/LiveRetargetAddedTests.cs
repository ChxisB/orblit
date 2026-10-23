namespace Orblit.Motion.Tests;

public class LiveRetargetAddedTests
{
    [Fact]
    public void PairsByShapeOnlyWhenAskedTo()
    {
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin);
        var silent = new Dictionary<string, BoneLocal>();
        var byName = new LiveRetarget(from.Skeleton, to.Skeleton).Follow(silent);
        var byShape = new LiveRetarget(from.Skeleton, to.Skeleton, humanoid: true).Follow(silent);
        Assert.True(byName.Count < byShape.Count);
    }
}
