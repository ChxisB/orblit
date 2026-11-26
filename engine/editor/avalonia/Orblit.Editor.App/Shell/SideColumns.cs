using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The columns under the title bar: a side, a splitter, the middle, a splitter and a side. The middle keeps its least
/// width, so a side made wide on a large window gives room back when the window shrinks, the wider side first.
/// </summary>
internal sealed class SideColumns : Grid
{
    protected override Size MeasureOverride(Size constraint)
    {
        var over = ColumnDefinitions.Sum(column => column.Width.IsAbsolute ? column.Width.Value : column.MinWidth) - constraint.Width;
        foreach (var side in new[] { ColumnDefinitions[0], ColumnDefinitions[4] }.OrderByDescending(column => column.Width.Value))
        {
            var give = Math.Max(0, Math.Min(over, side.Width.Value - side.MinWidth));
            side.Width = new GridLength(side.Width.Value - give);
            over -= give;
        }

        return base.MeasureOverride(constraint);
    }
}
