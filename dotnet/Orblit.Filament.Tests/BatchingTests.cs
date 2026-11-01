namespace Orblit.Filament.Tests;

public sealed class BatchingTests
{
    private static RenderScene Row(bool? batching = null) =>
        batching is { } set ? SceneBuilders.Row() with { Batching = set } : SceneBuilders.Row();

    [Fact]
    public void IsOnUnlessTurnedOff()
    {
        Assert.True(Row().Batching);
        Assert.Equal(true, Row().ToMessage()["batching"]);
    }

    [Fact]
    public void IsTurnedOffBySayingSoNotBySayingNothing()
    {
        Assert.False(Row(batching: false).Batching);
        Assert.Equal(false, Row(batching: false).ToMessage()["batching"]);
    }

    [Fact]
    public void ReachesTheMessageWhenAskedFor()
    {
        Assert.Equal(true, Row(batching: true).ToMessage()["batching"]);
    }

    [Fact]
    public void ChangesNothingElseAboutTheMessage()
    {
        SceneBuilders.AssertSameBut(Row(batching: false).ToMessage(), Row(batching: true).ToMessage(), "batching");
    }

    [Fact]
    public void SurvivesWithAndCanBeChangedByIt()
    {
        var on = Row(batching: true);
        Assert.True((on with { Sky = new Sky() }).Batching);
        Assert.False((on with { Batching = false }).Batching);
        Assert.True((Row(batching: false) with { Batching = true }).Batching);

        var off = Row(batching: false);
        Assert.False((off with { Sky = new Sky() }).Batching);
        Assert.Equal(false, (off with { Sky = new Sky() }).ToMessage()["batching"]);
    }
}
