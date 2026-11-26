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

/// <summary>A next step a toast offers, such as Try again, with the key that also runs it when there is one.</summary>
internal sealed record ToastAction(string Label, Action Run, string? Key = null);

/// <summary>A raised card that says what just happened: a mark, a title, an optional line on why, the next steps and Dismiss.</summary>
internal sealed class Toast : Border
{
    readonly TextBlock _title;

    public Toast(ToastKind kind, string title, string? line, IReadOnlyList<ToastAction> actions)
    {
        var (mark, tint) = kind switch
        {
            ToastKind.Done => ("Tick", EditorBrushes.Done),
            ToastKind.Failed => ("FaultCircle", EditorBrushes.Fault),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        // A pixel down, so the mark shares the middle of the title's line.
        Icon = new EditorIcon(mark) { Margin = new Thickness(0, 1, Gaps.Item, 0), VerticalAlignment = VerticalAlignment.Top };
        TextElement.SetForeground(Icon, tint);
        _title = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        Line = new TextBlock { Text = line, FontSize = 12, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, Gaps.Joined, 0, 0), IsVisible = line is not null };
        DismissButton = Parts.IconButton("Close", "Dismiss", 13);
        DismissButton.Margin = new Thickness(Gaps.Item, 0, 0, 0);
        DismissButton.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(DismissButton, Dock.Right);
        ActionButtons = [.. actions.Select(ActionButton)];
        var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, Margin = new Thickness(0, Gaps.Group, 0, 0), IsVisible = ActionButtons.Count > 0 };
        steps.Children.AddRange(ActionButtons);

        // Last, so the words fill what the mark and Dismiss leave and wrap in it.
        var row = new DockPanel { Children = { Icon, DismissButton, new StackPanel { Children = { _title, Line, steps } } } };
        Padding = new Thickness(Gaps.Group);
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

    /// <summary>The next steps, in the order given, under the line.</summary>
    internal IReadOnlyList<Button> ActionButtons { get; }

    static Button ActionButton(ToastAction action) => new()
    {
        Content = action.Key is null ? action.Label : Parts.Keyed(action.Label, action.Key),
        Classes = { EditorTheme.Small },
    };
}

/// <summary>
/// The toasts at the bottom right of the window, newest at the bottom. A toast goes after four seconds unless it
/// offers a next step, which waits for the person. The stack has no fill, so a click beside a card reaches the panel
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
        Spacing = Gaps.Item;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
    }

    internal IEnumerable<Toast> Cards => Children.OfType<Toast>();

    /// <summary>Shows a toast at the bottom. One with the same title goes first, so a repeat moves down and starts again.</summary>
    public void Show(ToastKind kind, string title, string? line = null, params ToastAction[] actions)
    {
        if (Cards.FirstOrDefault(card => card.Title == title) is { } repeat)
        {
            Remove(repeat);
        }

        var card = new Toast(kind, title, line, actions);
        card.DismissButton.Click += (_, _) => Remove(card);
        foreach (var (button, action) in card.ActionButtons.Zip(actions))
        {
            button.Click += (_, _) =>
            {
                Remove(card);
                action.Run();
            };
        }

        if (actions.Length == 0)
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
