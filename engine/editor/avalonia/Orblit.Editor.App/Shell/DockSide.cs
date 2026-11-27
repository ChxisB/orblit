using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One side of the window's body: a column with a place over and a place under. An empty place gives its room to the
/// other, and a side with neither in use takes no width.
/// </summary>
internal sealed class DockSide : Grid
{
    const double LeastRow = 120;

    readonly Control[] _places;
    readonly GridSplitter _between = new() { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Rows };
    // The height of each row and the width of the column while they are hidden.
    readonly GridLength[] _heights = new GridLength[2];
    GridLength _width;
    double? _least;

    public DockSide(Control upper, Control lower)
    {
        _places = [upper, lower];
        RowDefinitions = [new(GridLength.Star) { MinHeight = LeastRow }, new(Gaps.Item, GridUnitType.Pixel), new(GridLength.Star) { MinHeight = LeastRow }];
        SetRow(_between, 1);
        Splitter.DragCompleted += (_, _) => Resized?.Invoke();
        _between.DragCompleted += (_, _) => Resized?.Invoke();
        SetRow(lower, 2);
        Children.AddRange([upper, _between, lower]);
    }

    /// <summary>Raised when a drag of either splitter ends.</summary>
    public event Action? Resized;

    /// <summary>The upper place's share of the height the two places split between them.</summary>
    internal double Split
    {
        get => RowHeight(0).Value / (RowHeight(0).Value + RowHeight(1).Value);
        set
        {
            SetShare(0, value);
            SetShare(1, 1 - value);
        }
    }

    /// <summary>The splitter between this side and the middle. It hides while the side shows nothing.</summary>
    internal GridSplitter Splitter { get; } = new() { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns };

    /// <summary>While true, neither splitter moves.</summary>
    internal bool Locked
    {
        set => Splitter.IsEnabled = _between.IsEnabled = !value;
    }

    /// <summary>Lays out the side for the groups it shows now.</summary>
    /// <param name="upper">The group shown over, or none.</param>
    /// <param name="lower">The group shown under, or none.</param>
    internal void Show(DockTabs? upper, DockTabs? lower)
    {
        ShowRow(0, upper is not null);
        ShowRow(1, lower is not null);
        _between.IsVisible = upper is not null && lower is not null;
        RowDefinitions[1].Height = new GridLength(_between.IsVisible ? Gaps.Item : 0);
        ShowColumn(upper is not null || lower is not null);
    }

    void ShowRow(int at, bool shown)
    {
        if (_places[at].IsVisible == shown)
        {
            return;
        }

        var row = RowDefinitions[at * 2];
        if (!shown)
        {
            _heights[at] = row.Height;
        }

        row.MinHeight = shown ? LeastRow : 0;
        row.Height = shown ? _heights[at] : new GridLength(0);
        _places[at].IsVisible = shown;
    }

    GridLength RowHeight(int at) => _places[at].IsVisible ? RowDefinitions[at * 2].Height : _heights[at];

    void SetShare(int at, double share)
    {
        var height = new GridLength(share, GridUnitType.Star);
        if (_places[at].IsVisible)
        {
            RowDefinitions[at * 2].Height = height;
        }
        else
        {
            _heights[at] = height;
        }
    }

    // The window puts the side and its splitter in columns of the body grid.
    void ShowColumn(bool shown)
    {
        var body = (Grid)Parent!;
        var column = body.ColumnDefinitions[GetColumn(this)];
        _least ??= column.MinWidth;
        if (IsVisible && !shown)
        {
            _width = column.Width;
            column.Width = new GridLength(0);
        }
        else if (!IsVisible && shown)
        {
            column.Width = _width;
        }

        column.MinWidth = shown ? _least.Value : 0;
        body.ColumnDefinitions[GetColumn(Splitter)].Width = new GridLength(shown ? Gaps.Item : 0);
        IsVisible = shown;
        Splitter.IsVisible = shown;
    }
}
