namespace Orblit.Filament.Tests;

/// <summary>Ported from depth_prepass_test.dart.</summary>
public sealed class DepthPrepassTests
{
    private static RenderScene Row(bool? depthPrepass = null) =>
        depthPrepass is { } set ? SceneBuilders.Row() with { DepthPrepass = set } : SceneBuilders.Row();

    [Fact]
    public void IsOffUnlessAskedFor()
    {
        Assert.False(Row().DepthPrepass);
        Assert.Equal(false, Row().ToMessage()["depthPrepass"]);
    }

    [Fact]
    public void ReachesTheMessageWhenAskedFor()
    {
        Assert.Equal(true, Row(depthPrepass: true).ToMessage()["depthPrepass"]);
    }

    [Fact]
    public void ChangesNothingElseAboutTheMessage()
    {
        SceneBuilders.AssertSameBut(
            Row(depthPrepass: false).ToMessage(),
            Row(depthPrepass: true).ToMessage(),
            "depthPrepass");
    }

    [Fact]
    public void SurvivesWithAndCanBeChangedByIt()
    {
        var on = Row(depthPrepass: true);
        Assert.True((on with { Sky = new Sky() }).DepthPrepass);
        Assert.False((on with { DepthPrepass = false }).DepthPrepass);
        Assert.True((Row() with { DepthPrepass = true }).DepthPrepass);
    }

    [Fact]
    public void IsIndependentOfBatching()
    {
        var both = Row() with { Batching = true, DepthPrepass = true };
        Assert.True(both.Batching);
        Assert.True(both.DepthPrepass);
        Assert.True((both with { Batching = false }).DepthPrepass);
        Assert.True((both with { DepthPrepass = false }).Batching);

        var message = both.ToMessage();
        Assert.Equal(true, message["batching"]);
        Assert.Equal(true, message["depthPrepass"]);
    }
}
