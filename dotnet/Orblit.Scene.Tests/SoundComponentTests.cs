using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SoundComponentTests
{
    [Fact]
    public void ReadsAsALoopingSoundOnTheMasterBusWhenTheFileIsBare()
    {
        var source = SoundSourceComponent.FromJson(Map());

        Assert.Null(source.Clip);
        Assert.Equal("master", source.Bus);
        Assert.True(source.Loop);
        Assert.Equal(1, source.Volume);
        Assert.Equal([1.0, 50.0], [source.Near, source.Far]);
        Assert.Equal(SoundCurve.Inverse, source.Curve);
    }

    [Fact]
    public void IsTheSourceItWasWrittenAs()
    {
        var source = new SoundSourceComponent(
            clip: "sound/fire.ogg",
            bus: "sfx",
            loop: false,
            volume: 0.5,
            near: 2,
            far: 30,
            curve: SoundCurve.Linear);

        var read = SoundSourceComponent.FromJson(source.ToJson());

        Same(source.ToJson(), read.ToJson());
        Assert.Equal("sound/fire.ogg", read.Clip);
        Assert.Equal(SoundCurve.Linear, read.Curve);
        Assert.False(read.Loop);
    }

    [Fact]
    public void LeavesTheClipOutOfTheFileWhenItHasNone() =>
        Assert.False(new SoundSourceComponent().ToJson().ContainsKey("clip"));

    [Fact]
    public void ReadsAnEmptyClipAsNoneAndAnEmptyBusAsTheMaster()
    {
        var source = SoundSourceComponent.FromJson(Map(("clip", ""), ("bus", "")));

        Assert.Null(source.Clip);
        Assert.Equal("master", source.Bus);
    }

    [Fact]
    public void ReadsARangeWithoutAnEndAndABadVolumeAsTheDefault()
    {
        var source = SoundSourceComponent.FromJson(Map(("far", double.PositiveInfinity), ("volume", -1L)));

        Assert.Equal([1.0, 50.0], [source.Near, source.Far]);
        Assert.Equal(1, source.Volume);
        Assert.Equal(0, SoundSourceComponent.FromJson(Map(("volume", 0L))).Volume);
    }

    [Fact]
    public void ReadsARangeThatIsNotARangeAsTheDefaultOne()
    {
        var source = SoundSourceComponent.FromJson(Map(("near", 20L), ("far", 5L)));

        Assert.Equal([1.0, 50.0], [source.Near, source.Far]);
    }

    [Fact]
    public void IsTheSameSourceWithOneFieldChangedByCopyWith()
    {
        var louder = new SoundSourceComponent(clip: "a.ogg", far: 9).CopyWith(volume: 2);

        Assert.Equal(2, louder.Volume);
        Assert.Equal("a.ogg", louder.Clip);
        Assert.Equal(9, louder.Far);
    }

    [Fact]
    public void AListenerAndASurfaceSurviveATripThroughADocument()
    {
        var document = new SceneDocument("Scene", entities: [new SceneEntity(
            "ear",
            "Ear",
            components: new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Transform] = new TransformComponent(),
                [SceneComponents.SoundListener] = new SoundListenerComponent(),
                [SceneComponents.SoundSurface] = new SoundSurfaceComponent(name: "wood"),
            })]);

        var ear = SceneDocument.Decode(document.Encode()).Document["ear"]!;

        Assert.IsType<SoundListenerComponent>(ear[SceneComponents.SoundListener]);
        Assert.Equal("wood", Assert.IsType<SoundSurfaceComponent>(ear[SceneComponents.SoundSurface]).Name);
    }
}
