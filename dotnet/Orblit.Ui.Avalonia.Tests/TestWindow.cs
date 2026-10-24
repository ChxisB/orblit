using Avalonia.Controls;

namespace Orblit.Ui.Avalonia.Tests;

internal static class TestWindow
{
    /// <summary>Puts <paramref name="content"/> in a window of the given size and lays it out.</summary>
    internal static Window Show(Control content, double width = 400, double height = 300)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
}
