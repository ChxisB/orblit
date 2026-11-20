using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The buttons that line up, lay out and tidy the selected steps, in three named rows on a floating pill: align
/// the edges, lay the steps out in a row, a column or even gaps, then straighten, select a flow, expand a call or
/// collapse steps to a function.
/// </summary>
internal sealed class ArrangeBar : Border
{
    const double LabelWidth = 52;

    public ArrangeBar(GraphArranger arranger, Action<IEnumerable<int>> select, GraphEdits edits)
    {
        IsVisible = false;
        Classes.Add(EditorTheme.Island);
        Background = EditorBrushes.Floating;
        BorderBrush = EditorBrushes.LineStrong;
        BoxShadow = BoxShadows.Parse("0 6 20 0 #59000000");
        Padding = new Thickness(8);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(12, 8, 0, 0);
        var rows = new StackPanel { Spacing = 6 };
        foreach (var group in Buttons(arranger, select, edits).GroupBy(button => button.Group))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(new TextBlock { Text = group.Key, Width = LabelWidth, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center });
            foreach (var (_, id, label, tip, run) in group)
            {
                var button = PanelFields.Small($"arrange:{id}", label, run);
                button.Classes.AddRange([EditorTheme.TextButton, EditorTheme.Segment]);
                button.Padding = new Thickness(7, 0);
                ToolTip.SetTip(button, tip);
                row.Children.Add(button);
            }
            rows.Children.Add(row);
        }

        Child = rows;
    }

    static (string Group, string Id, string Label, string Tip, Action Run)[] Buttons(GraphArranger arranger, Action<IEnumerable<int>> select, GraphEdits edits) =>
    [
        ("Align", "align_left", "Left", "Line up the left edges", () => arranger.Align(Edge.Left)),
        ("Align", "align_center", "Center", "Line up the centers across", () => arranger.Align(Edge.Center)),
        ("Align", "align_right", "Right", "Line up the right edges", () => arranger.Align(Edge.Right)),
        ("Align", "align_top", "Top", "Line up the top edges", () => arranger.Align(Edge.Top)),
        ("Align", "align_middle", "Middle", "Line up the centers down", () => arranger.Align(Edge.Middle)),
        ("Align", "align_bottom", "Bottom", "Line up the bottom edges", () => arranger.Align(Edge.Bottom)),
        ("Lay out", "stack_row", "Row", "Put the steps side by side", () => arranger.Stack(Axis.Horizontal)),
        ("Lay out", "stack_column", "Column", "Put the steps one under another", () => arranger.Stack(Axis.Vertical)),
        ("Lay out", "spread_across", "Spread across", "Give the gaps between three or more steps in a row one size", () => arranger.Distribute(Axis.Horizontal)),
        ("Lay out", "spread_down", "Spread down", "Give the gaps between three or more steps in a column one size", () => arranger.Distribute(Axis.Vertical)),
        ("Tidy", "straighten", "Straighten", "Move steps up or down until the wires between them are level", arranger.Straighten),
        ("Tidy", "flow", "Flow", "Select every step joined to the selection by flow wires", () => select(arranger.Flow())),
        ("Tidy", "expand", "Expand", "Open the selected Call step out into the steps of its function", edits.Expand),
        ("Tidy", "collapse", "To function", "Move the selected steps into a new function and call it from here", edits.Collapse),
    ];
}
