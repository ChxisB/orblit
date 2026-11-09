using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A dock: a strip of tabs over a body that shows the chosen tab's content. The menu at the end of the strip offers
/// every slot but the one the dock sits in. The window moves the dock, so the menu only asks.
/// </summary>
internal sealed class DockTabs : Grid
{
    internal const double StripHeight = 33;

    const double TabPadding = 15;

    readonly Border[] _tabs;
    readonly Control[] _contents;
    readonly Border _body = new() { Background = EditorBrushes.Panel };
    readonly MenuItem[] _moves;
    DockSlot _slot;

    public DockTabs(params (string Title, Control Content)[] tabs)
    {
        Title = tabs[0].Title;
        RowDefinitions = new RowDefinitions($"{StripHeight},*");
        _contents = tabs.Select(tab => tab.Content).ToArray();
        _tabs = tabs.Select((tab, index) => MakeTab(tab.Title, index)).ToArray();
        var headers = new StackPanel { Orientation = Orientation.Horizontal };
        headers.Children.AddRange(_tabs);
        var strip = new DockPanel { Background = EditorBrushes.Window };
        _moves = [.. Enum.GetValues<DockSlot>().Select(slot => MoveItem(slot))];
        var menu = Parts.IconButton("GuiTabMenuHl", "Dock options");
        menu.Flyout = new MenuFlyout { Items = { _moves[0], _moves[1], _moves[2] } };
        DockPanel.SetDock(menu, Dock.Right);
        strip.Children.Add(menu);
        strip.Children.Add(headers);
        Children.Add(strip);
        SetRow(_body, 1);
        Children.Add(_body);
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
            _tabs[i].Background = active ? EditorBrushes.Panel : Brushes.Transparent;
            ((TextBlock)_tabs[i].Child!).Foreground = active ? EditorBrushes.Text : EditorBrushes.TabInactive;
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
            CornerRadius = new CornerRadius(4, 4, 0, 0),
            Padding = new Thickness(TabPadding, 0),
            Child = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center },
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
