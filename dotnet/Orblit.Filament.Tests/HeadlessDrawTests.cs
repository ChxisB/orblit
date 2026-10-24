namespace Orblit.Filament.Tests;

public sealed class HeadlessDrawTests
{
    private const uint Width = 64;
    private const uint Height = 48;

    // The headless host in native/headless draws four frames before it asks for a capture.
    private const int WarmUpFrames = 4;
    private const int WaitFrames = 8;

    [Fact(Skip = RendererLibrary.SkipReason, SkipUnless = nameof(RendererLibrary.Present), SkipType = typeof(RendererLibrary))]
    public void AHeadlessRendererDrawsAnOpaqueFrame()
    {
        var renderer = NativeMethods.Create(NativeMethods.Backend.Default, 0, Width, Height);
        Assert.NotEqual((nint)0, renderer);
        try
        {
            Assert.Equal(0, NativeMethods.SetCamera(renderer, [0f, 1f, 5f], [0f, 0f, 0f], 60f, 0, 0f, 0.0));
            for (var frame = 0; frame < WarmUpFrames; frame++)
            {
                Assert.Equal(0, NativeMethods.Draw(renderer, frame / 60.0));
            }

            Assert.Equal(0, NativeMethods.RequestCapture(renderer));
            var rgba = WaitForCapture(renderer);

            Assert.Equal((int)(Width * Height * 4), rgba.Length);
            Assert.True(NativeMethods.RenderedFrames(renderer) >= WarmUpFrames);
            // An undrawn capture is zero in every channel, so a drawn sky shows in the alpha.
            Assert.All(Enumerable.Range(0, rgba.Length / 4), pixel => Assert.Equal(255, rgba[(pixel * 4) + 3]));
        }
        finally
        {
            NativeMethods.Destroy(renderer);
        }
    }

    private static byte[] WaitForCapture(nint renderer)
    {
        for (var frame = 0; frame < WaitFrames; frame++)
        {
            Assert.Equal(0, NativeMethods.Draw(renderer, (WarmUpFrames + frame) / 60.0));
            var size = NativeMethods.ReadCapture(renderer, default, 0, out var width, out var height);
            if (size == 0)
            {
                continue;
            }

            Assert.Equal((Width, Height), (width, height));
            var rgba = new byte[size];
            Assert.Equal(size, NativeMethods.ReadCapture(renderer, rgba, size, out _, out _));
            return rgba;
        }

        throw new Xunit.Sdk.XunitException($"No frame came back after {WaitFrames} more draws.");
    }
}
