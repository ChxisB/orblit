using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The columns under the title bar: a side, a splitter, the middle, a splitter and a side. The middle keeps its least
/// width, so a side made wide on a large window gives room back when the window shrinks, the wider side first.
/// </summary>
internal sealed class SideColumns : Grid
{
    // The least widths of the three columns fit the smallest window together.
    public SideColumns() =>
        ColumnDefinitions =
        [
            new(280, GridUnitType.Pixel) { MinWidth = 220 },
            new(Gaps.Item, GridUnitType.Pixel),
            new(GridLength.Star),
            new(Gaps.Item, GridUnitType.Pixel),
            new(316, GridUnitType.Pixel) { MinWidth = 260 },
        ];

    /// <summary>The narrowest the middle may get, as what it shows needs.</summary>
    public double LeastMiddle
    {
        set => ColumnDefinitions[2].MinWidth = value;
    }

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
