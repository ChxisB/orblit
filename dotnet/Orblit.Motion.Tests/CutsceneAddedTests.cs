namespace Orblit.Motion.Tests;

public class CutsceneAddedTests
{
    [Fact]
    public void ACopyKeepsWhatItIsNotGiven()
    {
        var shot = new CutsceneShot("front", 1, 2);
        var moved = shot.CopyWith(start: 4);
        Assert.Equal(("front", 4.0, 2.0), (moved.Camera, moved.Start, moved.Duration));
        var looked = shot.CopyWith(camera: "side", duration: 3);
        Assert.Equal(("side", 1.0, 3.0), (looked.Camera, looked.Start, looked.Duration));
        Assert.Equal(("front", 1.0, 2.0), (shot.Camera, shot.Start, shot.Duration));
    }

    [Fact]
    public void NamesTheArgumentThatIsWrong()
    {
        Assert.Equal("camera", Assert.Throws<ArgumentException>(() => new CutsceneShot(string.Empty, 0, 1)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneShot("a", -1, 1)).ParamName);
        Assert.Equal("duration", Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneShot("a", 0, 0)).ParamName);
        Assert.Equal("sound", Assert.Throws<ArgumentException>(() => new CutsceneSound(string.Empty, 0, 1)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneSound("a", -1, 1)).ParamName);
        Assert.Equal("duration", Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneSound("a", 0, 0)).ParamName);
    }

    [Fact]
    public void RefusesRootMotionOnItsOwn()
    {
        var walk = new ClipDocument("Walk", 1, rootMotion: new RootMotion(bone: "hips"));
        Assert.Equal("motion", Assert.Throws<ArgumentException>(() => new CutsceneDocument(walk)).ParamName);
    }

    [Fact]
    public void KeepsShotsThatStartTogetherInTheOrderTheyWereGiven()
    {
        var cutscene = new CutsceneDocument(
            new ClipDocument("Together", 3),
            [new CutsceneShot("x", 0, 2), new CutsceneShot("y", 0, 3), new CutsceneShot("z", 0, 1)],
            [new CutsceneSound("a.ogg", 1, 1), new CutsceneSound("b.ogg", 1, 2)]);
        Assert.Equal(["x", "y", "z"], cutscene.Shots.Select(static shot => shot.Camera));
        Assert.Equal(["a.ogg", "b.ogg"], cutscene.Sounds.Select(static sound => sound.Sound));
    }
}
