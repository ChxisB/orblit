using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One side of the window's body: a column with a place over and a place under, and a rail at the window's edge with a
/// button for each collapsed group. An empty or collapsed place gives its room to the other, and a side with neither
/// open takes no width.
/// </summary>
internal sealed class DockSide : Grid
{
    internal const double RailWidth = 44;

    const double LeastRow = 120;

    readonly Control[] _places;
    readonly GridSplitter _between = new() { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Rows };
    readonly StackPanel _buttons = new() { Spacing = Gaps.Tight, HorizontalAlignment = HorizontalAlignment.Center };
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
        Rail = new Border { Classes = { EditorTheme.Island }, Width = RailWidth, Padding = new Thickness(0, Gaps.Item), IsVisible = false, Child = _buttons };
    }

    /// <summary>Raised with the group whose rail button was pressed.</summary>
    public event Action<DockTabs>? RailPressed;

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

    /// <summary>The splitter between this side and the middle. It hides while the side has nothing open.</summary>
    internal GridSplitter Splitter { get; } = new() { Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns };

    /// <summary>While true, neither splitter moves.</summary>
    internal bool Locked
    {
        set => Splitter.IsEnabled = _between.IsEnabled = !value;
    }

    /// <summary>The window puts the rail outside the columns, at its edge.</summary>
    internal Border Rail { get; }

    /// <summary>True while every group on the side is collapsed.</summary>
    internal bool IsFolded { get; private set; }

    /// <summary>The rail's buttons, one for each collapsed group on the side, in order. Each one's tag is its group.</summary>
    internal IEnumerable<Button> RailButtons => _buttons.Children.OfType<Button>();

    /// <summary>Lays out the side for the groups it has now.</summary>
    /// <param name="upper">The group docked over, or none.</param>
    /// <param name="lower">The group docked under, or none.</param>
    /// <param name="collapsed">Whether a group is collapsed to the rail.</param>
    internal void Show(DockTabs? upper, DockTabs? lower, Func<DockTabs, bool> collapsed)
    {
        DockTabs?[] groups = [upper, lower];
        var open = groups.Select(group => group is not null && !collapsed(group)).ToArray();
        ShowRow(0, open[0]);
        ShowRow(1, open[1]);
        _between.IsVisible = open[0] && open[1];
        RowDefinitions[1].Height = new GridLength(_between.IsVisible ? Gaps.Item : 0);

        _buttons.Children.Clear();
        _buttons.Children.AddRange(groups.OfType<DockTabs>().Where(collapsed).Select(RailButton));
        Rail.IsVisible = _buttons.Children.Count > 0;
        IsFolded = Rail.IsVisible && !open.Contains(true);
        ShowColumn(open.Contains(true));
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

        // The rail takes some of the side's least width, so the rail and the column together never grow past it.
        column.MinWidth = shown ? _least.Value - (Rail.IsVisible ? RailWidth + Gaps.Item : 0) : 0;
        body.ColumnDefinitions[GetColumn(Splitter)].Width = new GridLength(shown ? Gaps.Item : 0);
        IsVisible = shown;
        Splitter.IsVisible = shown;
    }

    Button RailButton(DockTabs group)
    {
        var button = Parts.Dress(new Button(), group.Chosen.Icon, new HoverCard(group.Chosen.Title, "Click to bring this panel back."));
        button.Tag = group;
        button.Click += (_, _) => RailPressed?.Invoke(group);
        return button;
    }
}
