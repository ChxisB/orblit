using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>The buttons that line up, spread, stack and straighten the selected steps, select a flow, and collapse steps to a function or expand a call.</summary>
internal sealed class ArrangeBar : Border
{
    public ArrangeBar(GraphArranger arranger, Action<IEnumerable<int>> select, GraphEdits edits)
    {
        IsVisible = false;
        Background = EditorBrushes.Panel;
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(6, 3);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        Margin = new Thickness(12, 8, 0, 0);
        var row = new WrapPanel { MaxWidth = 380 };
        foreach (var (id, label, tip, run) in Buttons(arranger, select, edits))
        {
            var button = PanelFields.Small($"arrange:{id}", label, run);
            ToolTip.SetTip(button, tip);
            row.Children.Add(button);
        }

        Child = row;
    }

    static (string Id, string Label, string Tip, Action Run)[] Buttons(GraphArranger arranger, Action<IEnumerable<int>> select, GraphEdits edits) =>
    [
        ("align_left", "Left", "Line up the left edges", () => arranger.Align(Edge.Left)),
        ("align_center", "Center", "Line up the centers across", () => arranger.Align(Edge.Center)),
        ("align_right", "Right", "Line up the right edges", () => arranger.Align(Edge.Right)),
        ("align_top", "Top", "Line up the top edges", () => arranger.Align(Edge.Top)),
        ("align_middle", "Middle", "Line up the centers down", () => arranger.Align(Edge.Middle)),
        ("align_bottom", "Bottom", "Line up the bottom edges", () => arranger.Align(Edge.Bottom)),
        ("spread_across", "Spread across", "Give the gaps between three or more steps in a row one size", () => arranger.Distribute(Axis.Horizontal)),
        ("spread_down", "Spread down", "Give the gaps between three or more steps in a column one size", () => arranger.Distribute(Axis.Vertical)),
        ("stack_row", "Row", "Put the steps side by side", () => arranger.Stack(Axis.Horizontal)),
        ("stack_column", "Column", "Put the steps one under another", () => arranger.Stack(Axis.Vertical)),
        ("straighten", "Straighten", "Move steps up or down until the wires between them are level", arranger.Straighten),
        ("flow", "Flow", "Select every step joined to the selection by flow wires", () => select(arranger.Flow())),
        ("collapse", "To function", "Move the selected steps into a new function and call it from here", edits.Collapse),
        ("expand", "Expand", "Open the selected Call step out into the steps of its function", edits.Expand),
    ];
}
