using Avalonia.Controls;

namespace Orblit.Filament.Avalonia.Tests;

internal static class TestWindow
{
    /// <summary>
    /// Puts <paramref name="viewport"/> in a window of the given size and waits until it has chosen
    /// how to show frames. The compositor here has no GPU interop, so a viewport takes the readback
    /// path, which is the path Windows and Linux take too.
    /// </summary>
    internal static async Task<Window> ShowAsync(RendererViewport viewport, int width, int height)
    {
        viewport.Width = width;
        viewport.Height = height;
        var window = new Window { Width = width, Height = height, Content = viewport };
        window.Show();
        window.UpdateLayout();
        await viewport.Choosing;
        return window;
    }
}
