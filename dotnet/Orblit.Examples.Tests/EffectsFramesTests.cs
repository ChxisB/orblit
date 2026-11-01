namespace Orblit.Examples.Tests;

/// <summary>
/// Frames of the effects examples that the fixture never reaches, pinned by
/// digests from a one-off run of the previous examples. The fixture moves each
/// control at one second and only to the far end of its range.
/// </summary>
public sealed class EffectsFramesTests
{
    /// <summary>A pan that has turned back, which it first does after four seconds.</summary>
    [Theory]
    [InlineData(4.6, "d53c2b5e5cde8e08")]
    [InlineData(5.5, "cb97d067f175b94e")]
    public void APanThatHasTurnedBackMatchesTheFixture(double seconds, string digest)
    {
        var example = new MotionBlurExample();
        example.Control<Toggle>("Pan").Change(true);

        Assert.Equal(digest, SceneDigest.Of(SceneDigest.Message(example, seconds)));
    }

    [Fact]
    public void ObjectBlurCannotBeChosenWithTheBlurOff()
    {
        var example = new MotionBlurExample();
        example.Control<Toggle>("Motion blur").Change(false);

        Assert.False(example.Control<Toggle>("Objects").Enabled);
    }

    /// <summary>A negative warp, which pinches rather than bulges.</summary>
    [Fact]
    public void APincushionLensMatchesTheFixture()
    {
        var example = new DistortionExample();
        example.Control<Setting>("Lens warp").Change(-0.2);

        Assert.Equal("6cd7615f095bd582", SceneDigest.Of(SceneDigest.Message(example, 0)));
    }
}
