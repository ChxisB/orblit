using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App;

/// <summary>
/// The History tab: the open scene, then each step since, oldest first. The step the scene is at is lit and the
/// undone steps are grayed. A click on a row asks to go to that step. The window does the going, so the scene
/// reloads once.
/// </summary>
internal sealed class HistoryPane : DockPanel
{
    readonly StackPanel _rows = new() { Spacing = 2, Margin = new Thickness(0, 6) };
    readonly StackPanel _empty = Parts.EmptyState("Past", "Nothing to undo yet", "Each edit to the scene is listed here as a step.");
    readonly TextBlock _hint = new()
    {
        Text = "Click a step to go back to it. Grayed steps are undone.",
        FontSize = 11,
        Foreground = EditorBrushes.TextDim,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(12, 4, 12, 12),
    };

    public HistoryPane()
    {
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

    /// <summary>Lists the steps of this history under a first row for the scene it starts from.</summary>
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

        _rows.Children.Add(Row(0, opened, openedAt, history!.Done));
        for (var i = 0; i < steps.Count; i++)
        {
            _rows.Children.Add(Row(i + 1, steps[i].Label, steps[i].At, history.Done));
        }
    }

    // Row n is the scene with n steps done, so the first row is the scene as it opened.
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
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(time, Dock.Right);
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
