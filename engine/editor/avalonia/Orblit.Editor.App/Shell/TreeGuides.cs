using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The thin lines that join a tree row to its parent: a vertical down the arrow of the parent and a stub across
/// to the child. Only two sets are drawn. The path from the root to the selected row is lit, and the children of
/// the selected row get faint lines. It sits over the tree and takes no pointer input.
/// </summary>
internal sealed class TreeGuides : Control
{
    const double StubLength = 18;

    // A parent and the boxes of its rows, in the coordinates of this control. Path is the child on the way to
    // the selected row, or -1. Faint is set when the parent is the selected row.
    sealed record Family(double X, IReadOnlyList<Rect> Children, int Path, bool Faint);

    readonly TreeView _tree;

    // The tree has to be laid out with its margin by now, so the lines are cut off where the tree is.
    public TreeGuides(TreeView tree)
    {
        _tree = tree;
        Margin = tree.Margin;
        ClipToBounds = true;
        IsHitTestVisible = false;
        tree.LayoutUpdated += (_, _) => InvalidateVisual();
        tree.SelectionChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The lines in drawing order. A later one is drawn over an earlier one.</summary>
    internal IReadOnlyList<(Rect Area, IBrush Fill)> Lines()
    {
        var families = Families();
        var faint = families.Where(family => family.Faint).ToArray();
        var lit = families.Where(family => family.Path >= 0).ToArray();
        var lines = new List<(Rect, IBrush)>();
        lines.AddRange(faint.Select(family => (Vertical(family, family.Children.Count - 1, 1), EditorBrushes.Guide)));
        lines.AddRange(lit.Select(family => (Vertical(family, family.Path, 0), EditorBrushes.GuideLit)));
        lines.AddRange(faint.SelectMany(family => family.Children.Select(row => (new Rect(family.X, Middle(row), StubLength, 1), EditorBrushes.GuideStub))));
        foreach (var family in lit)
        {
            // A plain line over and under the lit stub. The one over it stops at the lit vertical.
            var y = Middle(family.Children[family.Path]);
            lines.Add((new Rect(family.X + 1, y - 1, StubLength - 1, 1), EditorBrushes.GuideStub));
            lines.Add((new Rect(family.X - 1, y, StubLength + 1, 1), EditorBrushes.GuideLit));
            lines.Add((new Rect(family.X - 1, y + 1, StubLength + 1, 1), EditorBrushes.GuideStub));
        }

        return lines;
    }

    public override void Render(DrawingContext context)
    {
        foreach (var (area, fill) in Lines())
        {
            context.DrawRectangle(fill, null, area);
        }
    }

    // The vertical runs from the first row, a little below its top for a faint one, down to the middle of the
    // row at this index.
    static Rect Vertical(Family family, int last, double drop)
    {
        var top = family.Children[0].Top + drop;
        return new Rect(family.X - 1, top, 2, Middle(family.Children[last]) + 1 - top);
    }

    // The row that a stub is drawn on.
    static double Middle(Rect row) => row.Top + Math.Floor(row.Height / 2);

    // shortcut: this walks every row on each paint, which is fine for editor sized trees. Cache it when the outline is virtualized.
    List<Family> Families()
    {
        var rows = _tree.GetVisualDescendants().OfType<TreeViewItem>().Where(item => item.IsEffectivelyVisible).ToList();
        var lit = rows.Where(item => item.IsSelected).SelectMany(item => item.GetVisualAncestors().OfType<TreeViewItem>().Prepend(item)).ToHashSet();
        var families = new List<Family>();
        var withParent = rows.Select(item => (Item: item, Parent: ParentOf(item))).Where(row => row.Parent is not null);
        foreach (var group in withParent.GroupBy(row => row.Parent, row => row.Item))
        {
            var boxes = group.Select(LineBox).OfType<Rect>().ToList();
            if (boxes.Count == group.Count() && ArrowCentre(group.Key!) is { } x)
            {
                families.Add(new Family(x, boxes, group.ToList().FindIndex(lit.Contains), group.Key!.IsSelected));
            }
        }

        return families;
    }

    static TreeViewItem? ParentOf(TreeViewItem item) => item.FindAncestorOfType<TreeViewItem>();

    static Border? LineOf(TreeViewItem item) =>
        item.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PART_LayoutRoot");

    Rect? LineBox(TreeViewItem item) => LineOf(item) is { } line ? BoxOf(line) : null;

    // The arrow of a parent is the first toggle in its own line.
    double? ArrowCentre(TreeViewItem parent) =>
        LineOf(parent)?.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault() is { } arrow && BoxOf(arrow) is { } box ? box.Center.X : null;

    Rect? BoxOf(Visual visual) => visual.TranslatePoint(default, this) is { } origin ? new Rect(origin, visual.Bounds.Size) : null;
}
