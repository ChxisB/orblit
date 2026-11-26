using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The columns under the title bar: a side, a splitter, the middle, a splitter and a side. The middle keeps its least
/// width, so a side made wide on a large window gives room back when the window shrinks, the wider side first. It
/// takes its width again when the window has room.
/// </summary>
internal sealed class SideColumns : Grid
{
    const int Left = 0;
    const int Right = 4;

    // The width each side asked for, and the width it got. A width that is neither came from a splitter.
    readonly Dictionary<int, double> _asked = [];
    readonly Dictionary<int, double> _got = [];

    // The least widths of the three columns fit the smallest window together.
    public SideColumns()
    {
        ColumnDefinitions =
        [
            new(280, GridUnitType.Pixel) { MinWidth = 220 },
            new(Gaps.Item, GridUnitType.Pixel),
            new(GridLength.Star),
            new(Gaps.Item, GridUnitType.Pixel),
            new(316, GridUnitType.Pixel) { MinWidth = 260 },
        ];
        foreach (var side in new[] { Left, Right })
        {
            _asked[side] = _got[side] = ColumnDefinitions[side].Width.Value;
        }
    }

    /// <summary>The narrowest the middle may get, as what it shows needs.</summary>
    public double LeastMiddle
    {
        set => ColumnDefinitions[2].MinWidth = value;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        // A folded side is as narrow as its rail, and keeps what it asked for until it opens.
        var open = new[] { Left, Right }.Where(side => ColumnDefinitions[side].MinWidth > DockSide.RailWidth).ToArray();
        foreach (var side in open.Where(side => ColumnDefinitions[side].Width.Value != _got[side]))
        {
            _asked[side] = ColumnDefinitions[side].Width.Value;
        }

        double Wanted(ColumnDefinition column, int at) =>
            open.Contains(at) ? _asked[at] : column.Width.IsAbsolute ? column.Width.Value : column.MinWidth;

        var over = ColumnDefinitions.Select(Wanted).Sum() - constraint.Width;
        foreach (var side in open.OrderByDescending(side => _asked[side]))
        {
            var column = ColumnDefinitions[side];
            var give = Math.Max(0, Math.Min(over, _asked[side] - column.MinWidth));
            column.Width = new GridLength(_got[side] = _asked[side] - give);
            over -= give;
        }

        return base.MeasureOverride(constraint);
    }
}
