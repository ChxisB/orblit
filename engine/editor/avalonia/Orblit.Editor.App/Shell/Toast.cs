using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A raised card at the bottom right of the window that says what a click on an unbuilt control did. It goes
/// after four seconds. It takes no clicks, so it never blocks the panel under it.
/// </summary>
internal sealed class Toast : Border
{
    internal static readonly TimeSpan Shown = TimeSpan.FromSeconds(4);

    readonly TimeProvider _time;
    readonly TextBlock _message = new() { FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    ITimer? _timer;
    int _shows;

    public Toast(TimeProvider time)
    {
        _time = time;
        var icon = new EditorIcon("Info") { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
        TextElement.SetForeground(icon, EditorBrushes.Warn);
        Width = 360;
        Padding = new Thickness(12, 11);
        CornerRadius = EditorTheme.IslandCorners;
        Background = EditorBrushes.Raised;
        BorderBrush = EditorBrushes.LineStrong;
        BorderThickness = new Thickness(1);
        BoxShadow = BoxShadows.Parse("0 12 32 0 #73000000");
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        IsHitTestVisible = false;
        IsVisible = false;
        // A dock panel, so the message gets a width to wrap in.
        Child = new DockPanel { Children = { icon, _message } };
    }

    internal string Message => _message.Text ?? "";

    /// <summary>Shows the message and starts the four seconds again.</summary>
    public void Show(string message)
    {
        _message.Text = message;
        IsVisible = true;
        var show = ++_shows;
        _timer?.Dispose();
        // The timer fires on a pool thread, so the hide goes back to the UI thread.
        _timer = _time.CreateTimer(_ => Dispatcher.UIThread.Post(() => Hide(show)), null, Shown, Timeout.InfiniteTimeSpan);
    }

    // A late timer from an earlier message leaves a newer one up.
    void Hide(int show)
    {
        if (show == _shows)
        {
            IsVisible = false;
        }
    }
}
