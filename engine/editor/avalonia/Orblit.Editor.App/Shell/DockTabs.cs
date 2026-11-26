using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A group of panels: an island with a strip of tabs over a body that shows the chosen panel. The strip ends with a
/// button that collapses the group and a menu that maps where it sits, shows and hides its tabs, and names every
/// place. <see cref="DockArea"/> moves, floats and collapses the group, so the buttons only ask, and it moves tabs
/// from group to group.
/// </summary>
internal sealed class DockTabs : Border
{
    /// <summary>A 26 pixel row of tabs, the room around it, and the line under it.</summary>
    internal const double StripHeight = 39;

    // Centers a tab in the strip, above the line under it.
    internal const double StripInset = (StripHeight - 1 - DockTab.TabHeight) / 2;

    // The tabs' items in the menu follow its first heading, its map and its second heading.
    const int FirstTabItem = 3;

    readonly List<DockTab> _tabs = [];
    readonly Border _body = new();
    readonly Border _tool = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly MenuItem[] _moves;
    readonly MenuItem _float = new();
    readonly Border[] _map;
    readonly MenuFlyout _options;
    DockTab? _chosen;

    public DockTabs(params DockPage[] pages)
    {
        Classes.Add(EditorTheme.Island);
        Strip = new OverflowBar { Spacing = Gaps.Joined };
        _moves = [.. Enum.GetValues<DockSlot>().Select(MoveItem)];
        _map = [.. Enum.GetValues<DockSlot>().Select(_ => new Border { CornerRadius = new CornerRadius(3) })];
        _float.Click += (_, _) => FloatRequested?.Invoke();
        Floats = false;
        MenuButton.Flyout = _options = Options();
        CollapseButton.Click += (_, _) => CollapseRequested?.Invoke();
        DockPanel.SetDock(MenuButton, Dock.Right);
        DockPanel.SetDock(CollapseButton, Dock.Right);
        DockPanel.SetDock(_tool, Dock.Right);
        var strip = new DockPanel { Children = { MenuButton, CollapseButton, _tool, Strip } };
        var bar = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(Gaps.Item, StripInset), Child = strip };
        Grid.SetRow(_body, 1);
        Child = new Grid { RowDefinitions = new RowDefinitions($"{StripHeight},*"), Children = { bar, _body } };
        foreach (var page in pages)
        {
            Add(new DockTab(page));
        }
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

    /// <summary>The title of the first tab, which names the group. A group that has lost its last tab has none.</summary>
    internal string Title => _tabs.Count > 0 ? _tabs[0].Page.Title : "";

    /// <summary>The page on show, whose name and mark a collapsed group's rail button takes.</summary>
    internal DockPage Chosen => _chosen!.Page;

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

    internal int SelectedIndex => _tabs.IndexOf(_chosen!);

    internal IReadOnlyList<DockTab> Tabs => _tabs;

    internal Control Content => Chosen.Content;

    /// <summary>Takes a tab at the end of the strip, with its menu item.</summary>
    internal void Add(DockTab tab)
    {
        tab.Owner = this;
        _tabs.Add(tab);
        Strip.Add(tab);
        _options.Items.Insert(FirstTabItem + _tabs.Count - 1, tab.Item);
        _chosen ??= tab;
        ShowTabs();
    }

    /// <summary>Lets a tab go, and its panel with it, for another group to take.</summary>
    internal void Remove(DockTab tab)
    {
        _tabs.Remove(tab);
        Strip.Children.Remove(tab);
        _options.Items.Remove(tab.Item);
        if (tab == _chosen)
        {
            _body.Child = null;
            _tool.Child = null;
            _chosen = _tabs.FirstOrDefault();
        }

        ShowTabs();
    }

    internal void Select(int index) => Select(_tabs[index]);

    internal void Select(DockTab tab)
    {
        _chosen = tab;
        _body.Child = Chosen.Content;
        _tool.Child = Chosen.Tool;
        Strip.Pinned = tab;
        foreach (var each in _tabs)
        {
            each.Light(each == tab);
        }
    }

    /// <summary>Shows the tab if it is hidden, and hides it if it shows.</summary>
    internal void Toggle(DockTab tab)
    {
        tab.IsVisible = !tab.IsVisible;
        ShowTabs();
    }

    /// <summary>Toggles the tab with this title.</summary>
    internal void ToggleTab(string title) => Toggle(Tab(title));

    /// <summary>Shows every tab, and the group with them.</summary>
    internal void ShowAllTabs()
    {
        foreach (var tab in _tabs)
        {
            tab.IsVisible = true;
        }

        ShowTabs();
    }

    internal bool IsShown(string title) => Tab(title).IsVisible;

    /// <summary>Whether the tab with this title shows, now and after each change.</summary>
    internal IObservable<bool> Shown(string title) => Tab(title).GetObservable(IsVisibleProperty);

    /// <summary>The titles of the tabs, in order.</summary>
    internal IReadOnlyList<string> Titles => [.. _tabs.Select(tab => tab.Page.Title)];

    DockTab Tab(string title) => _tabs.Single(tab => tab.Page.Title == title);

    // A group with no tab to show hides and keeps its place. A hidden chosen tab hands the body to the first that shows.
    void ShowTabs()
    {
        IsVisible = _tabs.Exists(tab => tab.IsVisible);
        if (_chosen is not null)
        {
            Select(_chosen.IsVisible ? _chosen : _tabs.Find(tab => tab.IsVisible) ?? _chosen);
        }
    }

    // Under the button and right aligned, so the menu never covers the tabs.
    MenuFlyout Options()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Items.Add(Parts.MenuHeading("Where this panel sits"));
        flyout.Items.Add(new MenuItem { IsEnabled = false, MinHeight = 0, Header = Map() });
        flyout.Items.Add(Parts.MenuHeading("Tabs in this panel"));

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
}
