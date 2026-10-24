namespace Orblit.Filament.Tests;

public sealed class HeadlessDrawTests
{
    private const uint Width = 64;
    private const uint Height = 48;

    // The headless host in native/headless draws four frames before it asks for a capture.
    private const int WarmUpFrames = 4;

    [RendererFact]
    public void AHeadlessRendererDrawsAnOpaqueFrame()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.SetCamera([0f, 1f, 5f], [0f, 0f, 0f], 60f, orthographic: false, viewHeight: 0f, at: 0.0);
        for (var frame = 0; frame < WarmUpFrames; frame++)
        {
            renderer.Draw(frame / 60.0);
        }

        renderer.RequestCapture();
        var capture = Captures.Wait(renderer, WarmUpFrames);

        Assert.Equal((Width, Height), (capture.Width, capture.Height));
        Assert.Equal((int)(Width * Height * 4), capture.Rgba.Length);
        Assert.True(renderer.RenderedFrames >= WarmUpFrames);
        // An undrawn capture is zero in every channel, so a drawn sky shows in the alpha.
        var rgba = capture.Rgba.Span;
        for (var pixel = 0; pixel < rgba.Length / 4; pixel++)
        {
            Assert.Equal(255, rgba[(pixel * 4) + 3]);
        }
    }
}
