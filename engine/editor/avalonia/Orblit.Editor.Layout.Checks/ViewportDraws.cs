using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Orblit.Editor.App;

namespace Orblit.Editor.Layout.Checks;

/// <summary>
/// The engine's device leaves the fourth byte of an opaque frame at zero, so the view must show the
/// frame's color and not read that byte as alpha.
/// </summary>
internal static class ViewportDraws
{
    // Red and blue match, so the answer is the same whichever of the two comes first in the picture.
    static readonly byte[] s_magenta = [255, 0, 255, 255];

    public static (string Name, IReadOnlyList<string> Faults) Check()
    {
        var viewport = new ViewportControl(() => false, SpareByteZero);
        var window = new Window { Width = 64, Height = 64, Content = viewport };
        window.Show();
        viewport.Tick();
        Dispatcher.UIThread.RunJobs();
        using var picture = window.CaptureRenderedFrame()!;
        var middle = new byte[4];
        using (var pixels = picture.Lock())
        {
            System.Runtime.InteropServices.Marshal.Copy(pixels.Address + 32 * pixels.RowBytes + 32 * 4, middle, 0, 4);
        }
        window.Close();
        return ("the viewport shows a frame whose spare byte is zero", middle.SequenceEqual(s_magenta) ? [] : [$"the middle pixel is {string.Join(",", middle)}"]);
    }

    static bool SpareByteZero(out int width, out int height, ref byte[] rgba)
    {
        width = height = 64;
        rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = rgba[i + 2] = 255;
        }
        return true;
    }
}
