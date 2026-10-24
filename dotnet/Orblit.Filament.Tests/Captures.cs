namespace Orblit.Filament.Tests;

internal static class Captures
{
    private const int WaitFrames = 8;

    /// <summary>
    /// Draws on from <paramref name="firstFrame"/> until a requested capture
    /// arrives, which is a frame or two after it was asked for.
    /// </summary>
    internal static Capture Wait(Renderer renderer, int firstFrame)
    {
        for (var frame = 0; frame < WaitFrames; frame++)
        {
            renderer.Draw((firstFrame + frame) / 60.0);
            if (renderer.ReadCapture() is { } capture)
            {
                return capture;
            }
        }

        throw new Xunit.Sdk.XunitException($"No frame came back after {WaitFrames} more draws.");
    }
}
