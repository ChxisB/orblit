using Xunit.Sdk;

namespace Orblit.Filament.Tests;

/// <summary>
/// Reads colours out of a frame the renderer drew. Frames settle first and the read comes last,
/// for two reasons that both look like a renderer drawing nothing. A texture is uploaded through
/// the driver and does not arrive in the frame that asked for it. And a read hands back the last
/// frame that arrived and goes on handing it back, so a loop that stops at the first answer
/// measures the capture before.
/// </summary>
internal static class Pictures
{
    private const int SettlingFrames = 4;
    private const int FollowingFrames = 6;
    private const double FramesPerSecond = 60.0;

    /// <summary>The finished frame, drawn from <paramref name="seconds"/> on.</summary>
    internal static Capture Frame(Renderer renderer, double seconds)
    {
        for (var frame = 0; frame < SettlingFrames; frame++)
        {
            renderer.Draw(seconds + (frame / FramesPerSecond));
        }

        renderer.RequestCapture();
        for (var frame = SettlingFrames; frame < SettlingFrames + FollowingFrames; frame++)
        {
            renderer.Draw(seconds + (frame / FramesPerSecond));
        }

        return renderer.ReadCapture() ?? throw new XunitException("No frame came back after the capture was asked for.");
    }

    /// <summary>
    /// The mean red, green and blue of the square of side <c>2 * reach + 1</c> centred on a point
    /// given as a fraction of the width and of the height.
    /// </summary>
    internal static double[] ColourAt(Renderer renderer, double seconds, double across, double down, int reach)
    {
        var capture = Frame(renderer, seconds);
        var centreX = (int)(across * capture.Width);
        var centreY = (int)(down * capture.Height);
        var sum = new double[3];
        var taken = 0;
        for (var y = centreY - reach; y <= centreY + reach; y++)
        {
            for (var x = centreX - reach; x <= centreX + reach; x++)
            {
                var pixel = PixelAt(capture, x, y);
                for (var channel = 0; channel < 3; channel++)
                {
                    sum[channel] += pixel[channel];
                }

                taken++;
            }
        }

        return [.. sum.Select(channel => channel / taken)];
    }

    /// <summary>The middle of the frame, over a few pixels so one stray pixel cannot decide the answer.</summary>
    internal static double[] Middle(Renderer renderer, double seconds) => ColourAt(renderer, seconds, 0.5, 0.5, 2);

    /// <summary>One pixel in the middle column, a quarter of the way down or three quarters.</summary>
    internal static double[] RowColour(Renderer renderer, double seconds, bool lower)
    {
        var capture = Frame(renderer, seconds);
        var y = lower ? (int)(capture.Height * 3 / 4) : (int)(capture.Height / 4);
        return [.. PixelAt(capture, (int)(capture.Width / 2), y).Select(channel => (double)channel)];
    }

    /// <summary>
    /// Whether a colour is mostly one channel. The tone mapper lets a little of a pure colour into
    /// the other two, so mostly is the strongest channel over 120 with the others under three
    /// quarters of it.
    /// </summary>
    internal static bool Mostly(double[] rgb, int channel) =>
        rgb[channel] > 120 && Enumerable.Range(0, 3).All(other => other == channel || rgb[other] <= rgb[channel] * 0.75);

    /// <summary>
    /// Whether the renderer's notes say anything about a name containing <paramref name="name"/>.
    /// A description of a model that loaded is not a problem with it, so those are passed over.
    /// </summary>
    internal static bool Noted(Renderer renderer, string name) =>
        renderer.GetNotes().Any(note => !note.About.StartsWith("orblit.model:", StringComparison.Ordinal) && note.About.Contains(name, StringComparison.Ordinal));

    private static byte[] PixelAt(Capture capture, int x, int y)
    {
        var offset = ((y * (int)capture.Width) + x) * 4;
        return [.. capture.Rgba.Span.Slice(offset, 3)];
    }
}
