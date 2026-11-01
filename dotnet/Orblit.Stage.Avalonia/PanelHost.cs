using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Stage.Avalonia;

/// <summary>
/// The one hidden window every <see cref="ControlPanel"/> is drawn in.
/// </summary>
/// <remarks>
/// Avalonia gives a control a theme and a way to be pressed only inside a shown top level, and a package
/// cannot make one of its own. So the stage keeps a window nobody sees: no frame, no taskbar entry, fully
/// clear and off the screen. Panels stack in it at its corner, and each is hit tested on its own, so one
/// window serves a wall of them. Make the host after the application has started and keep it as long as
/// the panels it opened. Everything runs on the UI thread.
/// </remarks>
public sealed class PanelHost : IDisposable
{
    // Far enough from every screen that a window left on it by a platform that ignores opacity is
    // never under the pointer.
    private const int Away = -30000;

    private readonly Window _window;
    private readonly Grid _layers = new();

    public PanelHost()
    {
        _window = new Window
        {
            Content = _layers,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(Away, Away),
        };
        _window.Show();
    }

    internal Visual Root => _window;

    /// <summary>
    /// Puts <paramref name="content"/> in the host at <paramref name="size"/> logical pixels. The control is
    /// not resized, so what it lays out against is the size given. <paramref name="scale"/> is how many
    /// picture pixels one logical pixel becomes.
    /// </summary>
    public ControlPanel Open(Control content, Size size, double scale = 1)
    {
        // A bitmap of no pixels throws when the panel is painted, far from the call that made it.
        if (!IsPositive(size.Width) || !IsPositive(size.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A panel needs a width and a height above zero.");
        }

        if (!IsPositive(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "A panel needs a scale above zero.");
        }

        var frame = new Panel
        {
            Width = size.Width,
            Height = size.Height,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { content },
        };
        _layers.Children.Add(frame);
        return new ControlPanel(this, frame, scale);
    }

    internal void Remove(Control frame) => _layers.Children.Remove(frame);

    private static bool IsPositive(double value) => value > 0 && double.IsFinite(value);

    /// <summary>Closes the window. The panels it opened stop answering.</summary>
    public void Dispose() => _window.Close();
}
