using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>What a toast reports. Each kind has its own mark and colour.</summary>
internal enum ToastKind
{
    Done,
    Failed,
}

/// <summary>The one thing a toast offers to do, such as Try again.</summary>
internal sealed record ToastAction(string Label, Action Run);

/// <summary>A raised card that says what just happened: a mark, a title, an optional line on why, an action and Dismiss.</summary>
internal sealed class Toast : Border
{
    readonly TextBlock _title;

    public Toast(ToastKind kind, string title, string? line, ToastAction? action)
    {
        var (mark, tint) = kind switch
        {
            ToastKind.Done => ("Tick", EditorBrushes.Done),
            ToastKind.Failed => ("FaultCircle", EditorBrushes.Fault),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Icon = new EditorIcon(mark) { Margin = new Thickness(0, 1, 10, 0), VerticalAlignment = VerticalAlignment.Top };
        TextElement.SetForeground(Icon, tint);
        _title = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        Line = new TextBlock { Text = line, FontSize = 12, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0), IsVisible = line is not null };
        // The design draws this cross at 13 in the usual 26 pixel button.
        DismissButton = Parts.IconButton("Close", "Dismiss");
        DismissButton.Content = new EditorIcon("Close") { Width = 13, Height = 13 };
        DismissButton.Padding = new Thickness(6.5);
        DismissButton.Margin = new Thickness(10, 0, 0, 0);
        DismissButton.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(DismissButton, Dock.Right);
        var row = new DockPanel { Children = { Icon, DismissButton } };
        if (action is not null)
        {
            ActionButton = new Button { Content = action.Label, Classes = { EditorTheme.TextButton, EditorTheme.Small }, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(ActionButton, Dock.Right);
            row.Children.Add(ActionButton);
        }

        // Last, so the words fill what the mark and the buttons leave and wrap in it.
        row.Children.Add(new StackPanel { Children = { _title, Line } });
        Padding = new Thickness(12, 11);
        CornerRadius = EditorTheme.IslandCorners;
        Background = EditorBrushes.Raised;
        BorderBrush = EditorBrushes.LineStrong;
        BorderThickness = new Thickness(1);
        BoxShadow = BoxShadows.Parse("0 12 32 0 #73000000");
        Child = row;
    }

    internal string Title => _title.Text ?? "";

    internal TextBlock Line { get; }

    internal EditorIcon Icon { get; }

    internal Button DismissButton { get; }

    internal Button? ActionButton { get; }
}

/// <summary>
/// The toasts at the bottom right of the window, newest at the bottom. A toast goes after four seconds unless it
/// holds an action, which waits for the person. The stack has no fill, so a click beside a card reaches the panel
/// under it.
/// </summary>
internal sealed class ToastStack : StackPanel
{
    internal static readonly TimeSpan Shown = TimeSpan.FromSeconds(4);

    readonly TimeProvider _time;
    readonly Dictionary<Toast, ITimer> _timers = [];

    public ToastStack(TimeProvider time)
    {
        _time = time;
        Width = 380;
        Spacing = 10;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
    }

    internal IEnumerable<Toast> Cards => Children.OfType<Toast>();

    /// <summary>Shows a toast at the bottom. One with the same title goes first, so a repeat moves down and starts again.</summary>
    public void Show(ToastKind kind, string title, string? line = null, ToastAction? action = null)
    {
        if (Cards.FirstOrDefault(card => card.Title == title) is { } repeat)
        {
            Remove(repeat);
        }

        var card = new Toast(kind, title, line, action);
        card.DismissButton.Click += (_, _) => Remove(card);
        if (action is not null)
        {
            card.ActionButton!.Click += (_, _) =>
            {
                Remove(card);
                action.Run();
            };
        }
        else
        {
            // The timer fires on a pool thread, so the removal goes back to the UI thread.
            _timers[card] = _time.CreateTimer(_ => Dispatcher.UIThread.Post(() => Remove(card)), null, Shown, Timeout.InfiniteTimeSpan);
        }

        Children.Add(card);
    }

    /// <summary>Takes every toast down and drops its timer, so nothing fires after the window goes.</summary>
    public void Close()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }

        _timers.Clear();
        Children.Clear();
    }

    void Remove(Toast card)
    {
        Children.Remove(card);
        if (_timers.Remove(card, out var timer))
        {
            timer.Dispose();
        }
    }
}
