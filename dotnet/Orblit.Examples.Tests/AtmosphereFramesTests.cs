namespace Orblit.Examples.Tests;

/// <summary>
/// Frames of the atmosphere examples that the fixture never reaches, pinned
/// by digests from a one-off run of the previous examples. The fixture covers
/// five seconds and moves each control at one. So a sunset, the walk back out
/// of the hall and a lightning strike would otherwise go unchecked.
/// </summary>
public sealed class AtmosphereFramesTests
{
    /// <summary>Before dawn, the first light, an hour after it and just before dusk.</summary>
    [Theory]
    [InlineData(5.6, "cbbe8d45282b7081")]
    [InlineData(6.3, "a5c458fdeda4b2b3")]
    [InlineData(7.0, "431cfda811712b98")]
    [InlineData(17.8, "a4fc7f291c3e3ee5")]
    public void TheSkyAtDawnAndDuskMatchesTheFixture(double hour, string digest)
    {
        var example = new DayAndNightExample();
        example.Control<Toggle>("Running").Change(false);
        example.Control<Setting>("Hour").Change(hour);

        Assert.Equal(digest, SceneDigest.Of(SceneDigest.Message(example, 0)));
    }

    [Fact]
    public void AStoppedClockHasNoSpeed()
    {
        var example = new DayAndNightExample();
        example.Control<Toggle>("Running").Change(false);

        Assert.DoesNotContain(example.Settings(), control => control.Label == "Speed");
    }

    [Fact]
    public void TheWalkBackOutMatchesTheFixture() =>
        Assert.Equal("9feeb2a55993499a", SceneDigest.Of(SceneDigest.Message(new VolumesExample(), 18)));

    /// <summary>The back of the hall, where the damp corner overlaps the hall and wins.</summary>
    [Fact]
    public void TheDampCornerMatchesTheFixture()
    {
        var example = new VolumesExample();
        example.Control<Setting>("Where").Change(0.95);

        Assert.Equal("5cf1a564df87f115", SceneDigest.Of(SceneDigest.Message(example, 0)));
    }

    /// <summary>A storm at a second its lightning is striking.</summary>
    [Fact]
    public void AStrikeMatchesTheFixture()
    {
        var example = new WeatherExample();
        // The condition list always picks.
        example.Control<Choice>("Condition").Select!("Storm");

        Assert.Equal("149c09b1fe29f613", SceneDigest.Of(SceneDigest.Message(example, 2.4)));
    }
}
