using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class MotionComponentTests
{
    [Fact]
    public void PlaysNothingWhenTheFileSaysNothing()
    {
        var motion = MotionComponent.FromJson(Map());

        Assert.Empty(motion.Clips);
        Assert.Null(motion.Autoplay);
    }

    [Fact]
    public void WritesNoAutoplayWhenNoneIsSet() =>
        Same(Map(("clips", List("a.oclip"))), new MotionComponent(clips: ["a.oclip"]).ToJson());
}
