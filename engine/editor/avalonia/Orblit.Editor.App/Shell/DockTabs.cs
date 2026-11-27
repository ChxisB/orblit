using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A group of panels: an island with a strip of tabs over a body that shows the chosen panel. The strip ends with a
/// menu that maps where it sits, and shows and hides its tabs. <see cref="DockArea"/> moves and floats the group, so
/// the menu only asks, and it moves tabs from group to group.
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
    readonly PanelMap _map = new();
    readonly MenuItem _reset;
    readonly MenuItem _lock;
    readonly MenuFlyout _options;
    DockTab? _chosen;

    public DockTabs(params DockPage[] pages)
    {
        Classes.Add(EditorTheme.Island);
        Strip = new OverflowBar { Spacing = Gaps.Joined };
        _reset = Parts.ActionItem("Put all panels back", () => ResetRequested?.Invoke());
        _lock = Parts.ActionItem("Lock layout", () => LockRequested?.Invoke(), note: "Stops panels moving by accident");
        _lock.ToggleType = MenuItemToggleType.CheckBox;
        MenuButton.Flyout = _options = Options();
        // The menu shuts first, as a move takes its button along.
        _map.Picked += slot =>
        {
            _options.Hide();
            MoveRequested?.Invoke(slot);
        };
        _map.WindowPicked += () =>
        {
            _options.Hide();
            FloatRequested?.Invoke();
        };
        DockPanel.SetDock(MenuButton, Dock.Right);
        DockPanel.SetDock(_tool, Dock.Right);
        var strip = new DockPanel { Children = { MenuButton, _tool, Strip } };
        var bar = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(Gaps.Item, StripInset), Child = strip };
        Grid.SetRow(_body, 1);
        Child = new Grid { RowDefinitions = new RowDefinitions($"{StripHeight},*"), Children = { bar, _body } };
        foreach (var page in pages)
        {
            Add(new DockTab(page));
        }
    }

    /// <summary>Raised with the place the user chose on the menu's map. The group has not moved.</summary>
    public event Action<DockSlot>? MoveRequested;

    /// <summary>Raised when the user chose a window of its own for the group. It has not moved.</summary>
    public event Action? FloatRequested;

    /// <summary>Raised when the user asks for every group back in its first place.</summary>
    public event Action? ResetRequested;

    /// <summary>Raised when the user asks to lock the layout, or to unlock it.</summary>
    public event Action? LockRequested;

    /// <summary>Raised when the chosen tab or the tabs that show may have changed.</summary>
    public event Action? TabsChanged;

    internal Button MenuButton { get; } = new HoverCard("Panel options", "Move this panel, float it, pick its tabs or put every panel back.").On(Parts.IconButton("GuiTabMenuHl", ""));

    internal OverflowBar Strip { get; }

    /// <summary>
    /// The title of the first tab that shows, or of the first when none does, which names the group. A group that has
    /// lost its last tab has none.
    /// </summary>
    internal string Title => (_tabs.Find(tab => tab.IsVisible) ?? _tabs.FirstOrDefault())?.Page.Title ?? "";

    /// <summary>The page on show.</summary>
    internal DockPage Chosen => _chosen!.Page;

    /// <summary>The place the group sits in, or comes back to from a window of its own.</summary>
    internal DockSlot Slot { get; set; }

    /// <summary>
    /// Marks the group's place on its map, and turns off what a locked layout does not allow.
    /// </summary>
    /// <param name="taken">The places that have a group.</param>
    /// <param name="locked">Whether the layout is locked.</param>
    /// <param name="joins">Whether a move to a taken place makes the two groups one, instead of trading them.</param>
    internal void ShowPlaces(IReadOnlySet<DockSlot> taken, bool locked, bool joins)
    {
        _map.Show(Slot, Floats, locked, taken, joins);
        _reset.IsEnabled = !locked;
        _reset.Header = Parts.Noted("Put all panels back", locked ? "The layout is locked" : "The layout Orblit started with");
        _lock.IsChecked = locked;
    }

    /// <summary>True while the group sits in a window of its own.</summary>
    internal bool Floats { get; set; }

    internal PanelMap Map => _map;

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

        TabsChanged?.Invoke();
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
        flyout.Items.Add(_map.AsItem());
        flyout.Items.Add(Parts.MenuHeading("Tabs in this panel"));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(_reset);
        flyout.Items.Add(_lock);
        return flyout;
    }
}
