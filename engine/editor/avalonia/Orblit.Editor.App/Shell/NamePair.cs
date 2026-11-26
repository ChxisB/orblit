using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// Two names with a mark between them. When both do not fit, each gets half the room, and one that needs less than
/// half leaves the rest to the other.
/// </summary>
internal sealed class NamePair : Panel
{
    readonly Control _first;
    readonly Control _mark;
    readonly Control _second;

    public NamePair(Control first, Control mark, Control second)
    {
        (_first, _mark, _second) = (first, mark, second);
        Children.AddRange([first, mark, second]);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        var room = Math.Max(0, availableSize.Width - _mark.DesiredSize.Width);
        var share = Math.Max(room / 2, room - _second.DesiredSize.Width);
        _first.Measure(new Size(Math.Min(_first.DesiredSize.Width, share), availableSize.Height));
        _second.Measure(new Size(Math.Max(0, room - _first.DesiredSize.Width), availableSize.Height));
        return new Size(Children.Sum(child => child.DesiredSize.Width), Children.Max(child => child.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }
}
