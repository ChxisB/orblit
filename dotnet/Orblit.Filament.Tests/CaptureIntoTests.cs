namespace Orblit.Filament.Tests;

public sealed class CaptureIntoTests
{
    private const uint Width = 64;
    private const uint Height = 48;
    private const int WarmUpFrames = 4;

    private static Renderer Drawn()
    {
        var renderer = new Renderer(Width, Height);
        renderer.SetCamera([0f, 1f, 5f], [0f, 0f, 0f], 60f, orthographic: false, viewHeight: 0f, at: 0.0);
        for (var frame = 0; frame < WarmUpFrames; frame++)
        {
            renderer.Draw(frame / 60.0);
        }

        return renderer;
    }

    [RendererFact]
    public void NothingIsCopiedBeforeAFrameHasBeenAskedFor()
    {
        using var renderer = Drawn();
        var into = new byte[Width * Height * 4];

        Assert.False(renderer.TryReadCapture(into, out _, out _));
        Assert.All(into, value => Assert.Equal(0, value));
    }

    [RendererFact]
    public void AFrameIsCopiedIntoTheBufferTheHostOwns()
    {
        using var renderer = Drawn();
        renderer.RequestCapture();
        var capture = Captures.Wait(renderer, WarmUpFrames);
        var into = new byte[Width * Height * 4];

        Assert.True(renderer.TryReadCapture(into, out var width, out var height));

        Assert.Equal((Width, Height), (width, height));
        Assert.Equal(capture.Rgba.ToArray(), into);
    }

    [RendererFact]
    public void ABufferOneByteShortIsLeftAlone()
    {
        using var renderer = Drawn();
        renderer.RequestCapture();
        Captures.Wait(renderer, WarmUpFrames);
        var into = new byte[(Width * Height * 4) - 1];

        Assert.False(renderer.TryReadCapture(into, out var width, out var height));

        Assert.Equal((Width, Height), (width, height));
        Assert.All(into, value => Assert.Equal(0, value));
    }
}
