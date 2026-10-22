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

    [Fact]
    public void SurvivesTheRoundTripThroughASceneFile()
    {
        var document = new SceneDocument("Scene", entities: [new SceneEntity(
            "fox",
            "Fox",
            components: new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Transform] = new TransformComponent(),
                [SceneComponents.Motion] = new MotionComponent(
                    clips: ["clips/walk.oclip", "clips/run.oclip"],
                    autoplay: "clips/walk.oclip"),
            })]);

        var back = SceneDocument.Decode(document.Encode()).Document;
        var motion = Assert.IsType<MotionComponent>(back["fox"]![SceneComponents.Motion]);
        Assert.Equal(["clips/walk.oclip", "clips/run.oclip"], motion.Clips);
        Assert.Equal("clips/walk.oclip", motion.Autoplay);
    }
}
