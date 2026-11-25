using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The History tab: each step, newest first, then the open scene at the foot. The step the scene is at is lit and
/// the undone steps are faded. A click on a row asks to go to that step. The window does the going, so the scene
/// reloads once.
/// </summary>
internal sealed class HistoryPane : DockPanel
{
    // Every time is five mono figures, so a fixed width keeps the names in one column.
    const double TimeWidth = 40;

    readonly StackPanel _rows = new() { Spacing = 2, Margin = new Thickness(0, 6) };
    readonly StackPanel _empty = Parts.EmptyState("Past", "Nothing to undo yet", "Each change you make shows here. Click one to go back to it.");
    readonly WrapPanel _hint = new() { Margin = new Thickness(12, 4, 12, 12) };

    public HistoryPane(KeyModifiers command)
    {
        _hint.Children.AddRange([
            HintText("Faded steps are undone."),
            Parts.Kbd(Parts.KeyText(new KeyGesture(Key.Z, command | KeyModifiers.Shift))),
            HintText("redoes them."),
        ]);
        _hint.Children[1].Margin = new Thickness(4, 0);
        SetDock(_hint, Dock.Bottom);
        Children.Add(_hint);
        Children.Add(_empty);
        Children.Add(new ScrollViewer { Content = _rows });
        Show(null, "", default);
    }

    /// <summary>Raised with the number of steps that should be done after a click on a row.</summary>
    public event Action<int>? Picked;

    /// <summary>The button the dock strip shows while this tab is chosen.</summary>
    internal Button ClearButton { get; } = Parts.IconButton("Bin", "Clear the history");

    internal StackPanel Rows => _rows;

    /// <summary>Lists the steps of this history, newest first, over a last row for the scene it starts from.</summary>
    public void Show(EditHistory? history, string opened, DateTimeOffset openedAt)
    {
        _rows.Children.Clear();
        var steps = history?.Steps ?? [];
        _empty.IsVisible = steps.Count == 0;
        _hint.IsVisible = steps.Count > 0;
        ClearButton.IsEnabled = steps.Count > 0;
        if (steps.Count == 0)
        {
            return;
        }

        for (var i = steps.Count - 1; i >= 0; i--)
        {
            _rows.Children.Add(Row(i + 1, steps[i].Label, steps[i].At, history!.Done));
        }

        _rows.Children.Add(Row(0, opened, openedAt, history!.Done));
    }

    static TextBlock HintText(string text) => new() { Text = text, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };

    // Row n is the scene with n steps done, so row 0 is the scene as it opened.
    Button Row(int done, string label, DateTimeOffset at, int current)
    {
        var lit = done == current;
        var icon = new EditorIcon(lit ? "StepArrow" : "Past") { Width = 13, Height = 13, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, lit ? EditorBrushes.Accent : EditorBrushes.TreeIcon);
        var time = new TextBlock
        {
            Text = at.ToString("HH:mm", CultureInfo.InvariantCulture),
            FontFamily = EditorTheme.Mono,
            FontSize = 11,
            Foreground = EditorBrushes.TextDim,
            Width = TimeWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(time, Dock.Left);
        var row = new Button
        {
            Height = 26,
            Margin = new Thickness(6, 0),
            Padding = new Thickness(10, 0, 6, 0),
            CornerRadius = new CornerRadius(6),
            Focusable = false,
            Opacity = done > current ? 0.45 : 1,
            Background = lit ? EditorBrushes.AccentSoft : Brushes.Transparent,
            Foreground = lit ? Brushes.White : EditorBrushes.Text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel
            {
                Children =
                {
                    icon,
                    time,
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                },
            },
        };
        ToolTip.SetTip(row, lit ? "The scene is at this step" : $"Go to {label}");
        row.Click += (_, _) => Picked?.Invoke(done);
        return row;
    }
}
