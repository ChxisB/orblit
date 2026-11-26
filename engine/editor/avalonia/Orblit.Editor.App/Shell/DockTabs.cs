using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A group of panels: an island with a strip of tabs over a body that shows the chosen panel. The strip ends with a
/// button that collapses the group and a menu that maps where it sits, shows and hides its tabs, and names every
/// place. <see cref="DockArea"/> moves, floats and collapses the group, so the buttons only ask.
/// </summary>
internal sealed class DockTabs : Border
{
    /// <summary>A 26 pixel row of tabs, the room around it, and the line under it.</summary>
    internal const double StripHeight = 39;

    const double TabHeight = 26;

    // Centers a tab in the strip, above the line under it.
    internal const double StripInset = (StripHeight - 1 - TabHeight) / 2;

    const double TabPadding = Gaps.Item;

    readonly DockPage[] _pages;
    readonly DockTab[] _tabs;
    readonly Border _body = new();
    readonly Border _tool = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly MenuItem[] _moves;
    readonly MenuItem _float = new();
    readonly Border[] _map;

    public DockTabs(params DockPage[] pages)
    {
        Title = pages[0].Title;
        Classes.Add(EditorTheme.Island);
        _pages = pages;
        _tabs = [.. pages.Select((page, index) => new DockTab(page.Title, () => Select(index)))];
        Strip = new OverflowBar(_tabs) { Spacing = Gaps.Joined };
        _moves = [.. Enum.GetValues<DockSlot>().Select(MoveItem)];
        _map = [.. Enum.GetValues<DockSlot>().Select(_ => new Border { CornerRadius = new CornerRadius(3) })];
        _float.Click += (_, _) => FloatRequested?.Invoke();
        Floats = false;
        MenuButton.Flyout = Options();
        CollapseButton.Click += (_, _) => CollapseRequested?.Invoke();
        DockPanel.SetDock(MenuButton, Dock.Right);
        DockPanel.SetDock(CollapseButton, Dock.Right);
        DockPanel.SetDock(_tool, Dock.Right);
        var strip = new DockPanel { Children = { MenuButton, CollapseButton, _tool, Strip } };
        var bar = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(Gaps.Item, StripInset), Child = strip };
        Grid.SetRow(_body, 1);
        Child = new Grid { RowDefinitions = new RowDefinitions($"{StripHeight},*"), Children = { bar, _body } };
        Select(0);
    }

    /// <summary>Raised with the place the user chose in the menu. The group has not moved.</summary>
    public event Action<DockSlot>? MoveRequested;

    /// <summary>Raised when the user chose to pop the group out, or to put a floating one back. It has not moved.</summary>
    public event Action? FloatRequested;

    /// <summary>Raised when the user asks to collapse the group. It still shows.</summary>
    public event Action? CollapseRequested;

    /// <summary>Raised when the user asks for every group back in its first place.</summary>
    public event Action? ResetRequested;

    internal Button MenuButton { get; } = new HoverCard("Panel options", "Move this panel, float it, pick its tabs or put every panel back.").On(Parts.IconButton("GuiTabMenuHl", ""));

    internal Button CollapseButton { get; } = Parts.Dress(new Button { Margin = new Thickness(0, 0, Gaps.Joined, 0) }, "Back", new HoverCard("Collapse to the side", "Folds this panel to a button at the window's edge."));

    internal OverflowBar Strip { get; }

    /// <summary>The title of the first tab, which names the group.</summary>
    internal string Title { get; }

    /// <summary>The page on show, whose name and mark a collapsed group's rail button takes.</summary>
    internal DockPage Chosen => _pages[SelectedIndex];

    /// <summary>The place the group sits in, or comes back to from a window of its own.</summary>
    internal DockSlot Slot { get; set; }

    /// <summary>Marks the group's place on its map and menu, and points its collapse button at the near edge.</summary>
    /// <param name="taken">The places that have a group.</param>
    internal void ShowPlaces(IReadOnlySet<DockSlot> taken)
    {
        foreach (var slot in Enum.GetValues<DockSlot>())
        {
            var here = slot == Slot;
            var move = _moves[(int)slot];
            move.IsEnabled = !here;
            move.Header = Parts.Noted(slot.Label(), here ? "It is here now" : taken.Contains(slot) ? "Trades places with the panel there" : "Nothing is there yet");
            // The design draws the other places in the tone of a lit guide.
            _map[(int)slot].Background = here ? EditorBrushes.Accent : EditorBrushes.GuideLit;
        }

        CollapseButton.Content = new EditorIcon(Slot.IsLeft() ? "Back" : "Forward");
    }

    /// <summary>True while the group sits in a window of its own, so the menu offers to put it back.</summary>
    internal bool Floats
    {
        get => AutomationProperties.GetName(_float) == PutBack;
        set
        {
            var name = value ? PutBack : PopOut;
            AutomationProperties.SetName(_float, name);
            _float.Header = Parts.Noted(name, value ? "Docks it where it sat" : "Float it over the editor");
            CollapseButton.IsVisible = !value;
        }
    }

    internal const string PopOut = "Pop out into a window";

    internal const string PutBack = "Put it back";

    internal MenuItem FloatItem => _float;

    /// <summary>The move item of each place, by place.</summary>
    internal IReadOnlyList<MenuItem> MoveItems => _moves;

    /// <summary>The cell of each place on the map in the menu, by place.</summary>
    internal IReadOnlyList<Border> MapCells => _map;

    internal int SelectedIndex { get; private set; }

    internal IReadOnlyList<Border> Tabs => _tabs;

    internal Control Content => Chosen.Content;

    internal void Select(int index)
    {
        SelectedIndex = index;
        _body.Child = Chosen.Content;
        _tool.Child = Chosen.Tool;
        Strip.Pinned = _tabs[index];
        for (var i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].Light(i == index);
        }
    }

    /// <summary>
    /// Shows the tab with this title if it is hidden, and hides it if it shows. A group with no tab left hides and
    /// keeps its place. A hidden tab hands the body to the first tab that shows.
    /// </summary>
    internal void ToggleTab(string title)
    {
        var tab = _tabs[IndexOf(title)];
        tab.IsVisible = !tab.IsVisible;
        IsVisible = _tabs.Any(each => each.IsVisible);
        if (IsVisible && !_tabs[SelectedIndex].IsVisible)
        {
            Select(Array.FindIndex(_tabs, each => each.IsVisible));
        }
    }

    /// <summary>Shows every tab, and the group with them.</summary>
    internal void ShowAllTabs()
    {
        foreach (var tab in _tabs)
        {
            tab.IsVisible = true;
        }

        IsVisible = true;
    }

    internal bool IsShown(string title) => _tabs[IndexOf(title)].IsVisible;

    /// <summary>Whether the tab with this title shows, now and after each change.</summary>
    internal IObservable<bool> Shown(string title) => _tabs[IndexOf(title)].GetObservable(IsVisibleProperty);

    /// <summary>The titles of the tabs, in order.</summary>
    internal IReadOnlyList<string> Titles => [.. _pages.Select(page => page.Title)];

    int IndexOf(string title) => Array.FindIndex(_pages, page => page.Title == title);

    // Under the button and right aligned, so the menu never covers the tabs.
    MenuFlyout Options()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Items.Add(Parts.MenuHeading("Where this panel sits"));
        flyout.Items.Add(new MenuItem { IsEnabled = false, MinHeight = 0, Header = Map() });
        flyout.Items.Add(Parts.MenuHeading("Tabs in this panel"));
        foreach (var title in Titles)
        {
            var item = Parts.ActionItem(title, () => ToggleTab(title), note: EditorMenu.PanelNote(title));
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.Bind(MenuItem.IsCheckedProperty, Shown(title));
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new Separator());
        foreach (var move in _moves)
        {
            flyout.Items.Add(move);
        }

        flyout.Items.Add(_float);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(Parts.ActionItem("Put all panels back", () => ResetRequested?.Invoke(), note: "The layout Orblit started with"));
        return flyout;
    }

    // The window as four places round the viewport: each column over and under.
    Grid Map()
    {
        var viewport = new Border { CornerRadius = new CornerRadius(3), Background = EditorBrushes.Lifted, Margin = new Thickness(3, 0) };
        Grid.SetColumn(viewport, 1);
        Grid.SetRowSpan(viewport, 2);
        foreach (var slot in Enum.GetValues<DockSlot>())
        {
            var cell = _map[(int)slot];
            var lower = slot is DockSlot.LowerLeft or DockSlot.LowerRight;
            cell.Margin = lower ? new Thickness(0, 1.5, 0, 0) : new Thickness(0, 0, 0, 1.5);
            Grid.SetRow(cell, lower ? 1 : 0);
            Grid.SetColumn(cell, slot.IsLeft() ? 0 : 2);
        }

        var map = new Grid
        {
            Width = 118,
            Height = 62,
            Margin = new Thickness(0, Gaps.Joined, 0, Gaps.Item),
            ColumnDefinitions = new ColumnDefinitions("34,*,40"),
            RowDefinitions = new RowDefinitions("*,*"),
            Children = { viewport },
        };
        map.Children.AddRange(_map);
        return map;
    }

    MenuItem MoveItem(DockSlot slot)
    {
        var item = new MenuItem();
        AutomationProperties.SetName(item, slot.Label());
        item.Click += (_, _) => MoveRequested?.Invoke(slot);
        return item;
    }

    // A tab. In a group too narrow for it, the strip's More menu holds it, and its menu item chooses it.
    sealed class DockTab : Border, IOverflowTool
    {
        readonly TextBlock _title;
        readonly Button _choose = new();

        public DockTab(string title, Action choose)
        {
            _title = new TextBlock { Text = title, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Height = TabHeight;
            CornerRadius = EditorTheme.Corners;
            Padding = new Thickness(TabPadding, 0);
            Child = _title;
            _choose.Click += (_, _) => choose();
            PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    choose();
                }
            };
        }

        string IOverflowTool.MenuWords => _title.Text!;

        Button IOverflowTool.MenuButton => _choose;

        public void Light(bool chosen)
        {
            Background = chosen ? EditorBrushes.Line : Brushes.Transparent;
            _title.Foreground = chosen ? EditorBrushes.Text : EditorBrushes.TextDim;
        }
    }
}
