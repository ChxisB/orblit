namespace Orblit.Sequence.Tests;

/// <summary>
/// The fixture exporter checked these with asserts, which a release build skips. The port always checks.
/// </summary>
public class RefusalTests
{
    [Fact]
    public void AChannelWithNoKeysIsRefused() =>
        Assert.ThrowsAny<ArgumentException>(() => new Channel<double>([], Mixers.OfDouble));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void AClipWithNoLengthIsRefused(double duration) =>
        Assert.ThrowsAny<ArgumentException>(() => new Clip(0, duration));

    [Fact]
    public void AClipAtNoSpeedIsRefused() =>
        Assert.ThrowsAny<ArgumentException>(() => new Clip(0, 1, speed: 0));
}
