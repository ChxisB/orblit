using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The rounded block behind a selected or hovered tree row, 6 pixels in from each side of the tree at every depth.
/// The theme paints the whole line, so its own row colors are clear and a row lays this under its content instead.
/// </summary>
internal sealed class RowHighlight : Control
{
    const double Inset = 6;

    TreeViewItem? _item;
    Border? _line;

    public RowHighlight()
    {
        IsHitTestVisible = false;
    }

    internal IBrush? Fill => _item is { IsSelected: true } ? EditorBrushes.AccentSoft : _line is { IsPointerOver: true } ? EditorBrushes.Hover : null;

    /// <summary>The block in the coordinates of this control, as tall as the line of the tree item.</summary>
    internal Rect Area()
    {
        var line = _line!.Bounds.Size;
        var origin = _line.TranslatePoint(default, this)!.Value;
        return new Rect(origin.X + Inset, origin.Y, line.Width - (2 * Inset), line.Height);
    }

    public override void Render(DrawingContext context)
    {
        if (Fill is { } fill)
        {
            context.DrawRectangle(fill, null, new RoundedRect(Area(), EditorTheme.Corners.TopLeft));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _item = this.FindAncestorOfType<TreeViewItem>();
        _line = this.GetVisualAncestors().OfType<Border>().FirstOrDefault(border => border.Name == "PART_LayoutRoot");
        if (_item is not null && _line is not null)
        {
            _item.PropertyChanged += Changed;
            _line.PropertyChanged += Changed;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_item is not null && _line is not null)
        {
            _item.PropertyChanged -= Changed;
            _line.PropertyChanged -= Changed;
        }

        _item = null;
        _line = null;
    }

    void Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SelectingItemsControl.IsSelectedProperty || e.Property == IsPointerOverProperty)
        {
            InvalidateVisual();
        }
    }
}
