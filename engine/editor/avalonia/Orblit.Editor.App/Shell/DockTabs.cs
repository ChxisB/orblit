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
/// A dock: an island with a strip of tabs over a body that shows the chosen tab's content. The menu at the end of the
/// strip maps where the dock sits, shows and hides its tabs, and names every slot. The window moves the dock, so the
/// menu only asks.
/// </summary>
internal sealed class DockTabs : Border
{
    /// <summary>A 26 pixel row of tabs, 6 pixels of padding around it, and the line under it.</summary>
    internal const double StripHeight = 39;

    const double TabHeight = 26;

    const double TabPadding = 10;

    readonly string[] _titles;
    readonly Border[] _tabs;
    readonly Control[] _contents;
    readonly Border _body = new();
    readonly Control?[] _tools;
    readonly Border _tool = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly MenuItem[] _moves;
    readonly MenuItem _float = new();
    readonly Border[] _map;
    DockSlot _slot;

    public DockTabs(params (string Title, Control Content)[] tabs)
    {
        Title = tabs[0].Title;
        Classes.Add(EditorTheme.Island);
        _titles = tabs.Select(tab => tab.Title).ToArray();
        _contents = tabs.Select(tab => tab.Content).ToArray();
        _tools = new Control?[tabs.Length];
        _tabs = tabs.Select((tab, index) => MakeTab(tab.Title, index)).ToArray();
        var headers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        headers.Children.AddRange(_tabs);
        var strip = new DockPanel();
        _moves = [.. Enum.GetValues<DockSlot>().Select(slot => MoveItem(slot))];
        _map = [.. Enum.GetValues<DockSlot>().Select(_ => new Border { CornerRadius = new CornerRadius(3) })];
        _float.Click += (_, _) => FloatRequested?.Invoke();
        Floats = false;
        MenuButton.Flyout = Options();
        DockPanel.SetDock(MenuButton, Dock.Right);
        DockPanel.SetDock(_tool, Dock.Right);
        strip.Children.Add(MenuButton);
        strip.Children.Add(_tool);
        strip.Children.Add(headers);
        var bar = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6), Child = strip };
        Grid.SetRow(_body, 1);
        Child = new Grid { RowDefinitions = new RowDefinitions($"{StripHeight},*"), Children = { bar, _body } };
        Select(0);
        Slot = DockSlot.UpperLeft;
    }

    /// <summary>Raised with the slot the user chose in the dock menu. The dock has not moved.</summary>
    public event Action<DockSlot>? MoveRequested;

    /// <summary>Raised when the user chose to pop the dock out, or to put a floating one back. The dock has not moved.</summary>
    public event Action? FloatRequested;

    /// <summary>Raised when the user asks for every dock back in its first slot.</summary>
    public event Action? ResetRequested;

    internal Button MenuButton { get; } = Parts.IconButton("GuiTabMenuHl", "Dock options");

    /// <summary>The title of the first tab, which names the dock.</summary>
    internal string Title { get; }

    /// <summary>The mark of the dock's button on a folded side's rail.</summary>
    internal required string Icon { get; init; }

    /// <summary>The slot the dock sits in, which the map lights and the menu shows as off.</summary>
    internal DockSlot Slot
    {
        get => _slot;
        set
        {
            _slot = value;
            for (var i = 0; i < _moves.Length; i++)
            {
                var here = (DockSlot)i == value;
                _moves[i].IsEnabled = !here;
                _moves[i].Header = Parts.Noted(((DockSlot)i).Label(), here ? "It is here now" : "Trades places with the panel there");
                // The design draws the free slots in the tone of a lit guide.
                _map[i].Background = here ? EditorBrushes.Accent : EditorBrushes.GuideLit;
            }
        }
    }

    /// <summary>True while the dock sits in a window of its own, so the menu offers to put it back.</summary>
    internal bool Floats
    {
        get => AutomationProperties.GetName(_float) == PutBack;
        set
        {
            var name = value ? PutBack : PopOut;
            AutomationProperties.SetName(_float, name);
            _float.Header = Parts.Noted(name, value ? "Docks it where it sat" : "Float it over the editor");
        }
    }

    internal const string PopOut = "Pop out into a window";

    internal const string PutBack = "Put it back";

    internal MenuItem FloatItem => _float;

    /// <summary>The move item of each slot, by slot.</summary>
    internal IReadOnlyList<MenuItem> MoveItems => _moves;

    /// <summary>The cell of each slot on the map in the menu, by slot.</summary>
    internal IReadOnlyList<Border> MapCells => _map;

    internal int SelectedIndex { get; private set; }

    internal IReadOnlyList<Border> Tabs => _tabs;

    internal Control Content => _contents[SelectedIndex];

    internal void Select(int index)
    {
        SelectedIndex = index;
        _body.Child = _contents[index];
        _tool.Child = _tools[index];
        for (var i = 0; i < _tabs.Length; i++)
        {
            var active = i == index;
            _tabs[i].Background = active ? EditorBrushes.Line : Brushes.Transparent;
            // MakeTab always gives a tab its title block.
            ((TextBlock)_tabs[i].Child!).Foreground = active ? EditorBrushes.Text : EditorBrushes.TextDim;
        }
    }

    /// <summary>
    /// Shows the tab with this title if it is hidden, and hides it if it shows. A dock with no tab left hides and
    /// keeps its slot. A hidden tab hands the body to the first tab that shows.
    /// </summary>
    internal void ToggleTab(string title)
    {
        var tab = _tabs[Array.IndexOf(_titles, title)];
        tab.IsVisible = !tab.IsVisible;
        IsVisible = _tabs.Any(each => each.IsVisible);
        if (IsVisible && !_tabs[SelectedIndex].IsVisible)
        {
            Select(Array.FindIndex(_tabs, each => each.IsVisible));
        }
    }

    /// <summary>Shows every tab, and the dock with them.</summary>
    internal void ShowAllTabs()
    {
        foreach (var tab in _tabs)
        {
            tab.IsVisible = true;
        }

        IsVisible = true;
    }

    /// <summary>Whether the tab with this title shows, now and after each change.</summary>
    internal IObservable<bool> Shown(string title) => _tabs[Array.IndexOf(_titles, title)].GetObservable(IsVisibleProperty);

    /// <summary>Puts a control in the strip, left of the menu, that shows while the tab with this title is chosen.</summary>
    internal void AddTool(string title, Control tool)
    {
        _tools[Array.IndexOf(_titles, title)] = tool;
        Select(SelectedIndex);
    }

    /// <summary>The titles of the tabs, in order.</summary>
    internal IReadOnlyList<string> Titles => _titles;

    // Under the button and right aligned, so the menu never covers the tabs.
    MenuFlyout Options()
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Items.Add(Parts.MenuHeading("Where this panel sits"));
        flyout.Items.Add(new MenuItem { IsEnabled = false, MinHeight = 0, Header = Map() });
        flyout.Items.Add(Parts.MenuHeading("Tabs in this panel"));
        foreach (var title in _titles)
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

    // The window as three slots round the viewport: the left column over and under, the viewport, and the right.
    Grid Map()
    {
        var viewport = new Border { CornerRadius = new CornerRadius(3), Background = EditorBrushes.Lifted, Margin = new Thickness(3, 0) };
        Grid.SetColumn(viewport, 1);
        Grid.SetRowSpan(viewport, 2);
        _map[(int)DockSlot.UpperLeft].Margin = new Thickness(0, 0, 0, 1.5);
        _map[(int)DockSlot.LowerLeft].Margin = new Thickness(0, 1.5, 0, 0);
        Grid.SetRow(_map[(int)DockSlot.LowerLeft], 1);
        Grid.SetColumn(_map[(int)DockSlot.Right], 2);
        Grid.SetRowSpan(_map[(int)DockSlot.Right], 2);
        return new Grid
        {
            Width = 118,
            Height = 62,
            Margin = new Thickness(0, 2, 0, 6),
            ColumnDefinitions = new ColumnDefinitions("34,*,40"),
            RowDefinitions = new RowDefinitions("*,*"),
            Children = { _map[0], _map[1], viewport, _map[2] },
        };
    }

    MenuItem MoveItem(DockSlot slot)
    {
        var item = new MenuItem();
        AutomationProperties.SetName(item, slot.Label());
        item.Click += (_, _) => MoveRequested?.Invoke(slot);
        return item;
    }

    Border MakeTab(string title, int index)
    {
        var tab = new Border
        {
            Height = TabHeight,
            CornerRadius = EditorTheme.Corners,
            Padding = new Thickness(TabPadding, 0),
            Child = new TextBlock { Text = title, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center },
        };
        tab.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed)
            {
                Select(index);
            }
        };
        return tab;
    }
}
