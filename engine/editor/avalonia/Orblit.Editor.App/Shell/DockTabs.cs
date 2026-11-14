using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A dock: an island with a strip of tabs over a body that shows the chosen tab's content. The menu at the end of the
/// strip offers every slot but the one the dock sits in. The window moves the dock, so the menu only asks.
/// </summary>
internal sealed class DockTabs : Border
{
    /// <summary>A 26 pixel row of tabs, 6 pixels of padding around it, and the line under it.</summary>
    internal const double StripHeight = 39;

    const double TabHeight = 26;

    const double TabPadding = 10;

    readonly Border[] _tabs;
    readonly Control[] _contents;
    readonly Border _body = new();
    readonly MenuItem[] _moves;
    DockSlot _slot;

    public DockTabs(params (string Title, Control Content)[] tabs)
    {
        Title = tabs[0].Title;
        Classes.Add(EditorTheme.Island);
        _contents = tabs.Select(tab => tab.Content).ToArray();
        _tabs = tabs.Select((tab, index) => MakeTab(tab.Title, index)).ToArray();
        var headers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        headers.Children.AddRange(_tabs);
        var strip = new DockPanel();
        _moves = [.. Enum.GetValues<DockSlot>().Select(slot => MoveItem(slot))];
        var menu = Parts.IconButton("GuiTabMenuHl", "Dock options");
        // Under the button and right aligned, so the menu never covers the tabs.
        menu.Flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight, Items = { _moves[0], _moves[1], _moves[2] } };
        DockPanel.SetDock(menu, Dock.Right);
        strip.Children.Add(menu);
        strip.Children.Add(headers);
        var bar = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6), Child = strip };
        Grid.SetRow(_body, 1);
        Child = new Grid { RowDefinitions = new RowDefinitions($"{StripHeight},*"), Children = { bar, _body } };
        Select(0);
        Slot = DockSlot.UpperLeft;
    }

    /// <summary>Raised with the slot the user chose in the dock menu. The dock has not moved.</summary>
    public event Action<DockSlot>? MoveRequested;

    /// <summary>The title of the first tab, which names the dock.</summary>
    internal string Title { get; }

    /// <summary>The slot the dock sits in, which the menu leaves out as a choice.</summary>
    internal DockSlot Slot
    {
        get => _slot;
        set
        {
            _slot = value;
            for (var i = 0; i < _moves.Length; i++)
            {
                _moves[i].IsEnabled = (DockSlot)i != value;
            }
        }
    }

    internal IReadOnlyList<MenuItem> MoveItems => _moves;

    internal int SelectedIndex { get; private set; }

    internal IReadOnlyList<Border> Tabs => _tabs;

    internal Control Content => _contents[SelectedIndex];

    internal void Select(int index)
    {
        SelectedIndex = index;
        _body.Child = _contents[index];
        for (var i = 0; i < _tabs.Length; i++)
        {
            var active = i == index;
            _tabs[i].Background = active ? EditorBrushes.Line : Brushes.Transparent;
            // MakeTab always gives a tab its title block.
            ((TextBlock)_tabs[i].Child!).Foreground = active ? EditorBrushes.Text : EditorBrushes.TextDim;
        }
    }

    MenuItem MoveItem(DockSlot slot)
    {
        var item = new MenuItem { Header = $"Move to {slot.Label()}" };
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
