using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body under the title bar: the middle, with groups of panels in four places round it and a rail at each edge.
/// A group moves, floats and collapses here. The place each group is made with is its home.
/// </summary>
internal sealed class DockArea : Grid
{
    readonly DockTabs[] _groups;
    readonly Dictionary<DockTabs, DockSlot> _homes;
    readonly Dictionary<DockTabs, Window> _floats = [];
    readonly Dictionary<DockSlot, Border> _places = Enum.GetValues<DockSlot>().ToDictionary(slot => slot, _ => new Border());
    readonly SideColumns _columns = new();
    // What the user collapsed, and what is collapsed now. A workspace that folds the sides has its own for as long as
    // it shows.
    readonly CollapsedSlots _users = new();
    CollapsedSlots _collapsed;

    public DockArea(Control center, params DockTabs[] groups)
    {
        _groups = groups;
        _homes = groups.ToDictionary(group => group, group => group.Slot);
        _collapsed = _users;
        Margin = new Thickness(Gaps.Item, 0, Gaps.Item, Gaps.Item);
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        Left = new DockSide(_places[DockSlot.UpperLeft], _places[DockSlot.LowerLeft]);
        Right = new DockSide(_places[DockSlot.UpperRight], _places[DockSlot.LowerRight]);
        Control[] columns = [Left, Left.Splitter, center, Right.Splitter, Right];
        for (var i = 0; i < columns.Length; i++)
        {
            SetColumn(columns[i], i);
            _columns.Children.Add(columns[i]);
        }

        Left.Rail.Margin = new Thickness(0, 0, Gaps.Item, 0);
        Right.Rail.Margin = new Thickness(Gaps.Item, 0, 0, 0);
        SetColumn(_columns, 1);
        SetColumn(Right.Rail, 2);
        Children.AddRange([Left.Rail, _columns, Right.Rail]);
        Wire();
        Refresh();
    }

    /// <summary>Raised with what just happened, for the status line.</summary>
    public event Action<string>? Said;

    /// <summary>Raised after the groups are laid out again.</summary>
    public event Action? Changed;

    internal DockSide Left { get; }

    internal DockSide Right { get; }

    /// <summary>The narrowest the middle may get, as what it shows needs.</summary>
    internal double LeastMiddle
    {
        set => _columns.LeastMiddle = value;
    }

    /// <summary>True while no group shows in either side, so the next thing to do is to show them.</summary>
    internal bool AllCollapsed
    {
        get
        {
            var shown = Taken().ToArray();
            return (shown.Length > 0 ? shown : Enum.GetValues<DockSlot>()).All(_collapsed.Has);
        }
    }

    internal void HidePanels()
    {
        _collapsed.CollapseAll();
        Refresh();
    }

    internal void ShowPanels()
    {
        _collapsed.BringBack(Taken());
        Refresh();
    }

    /// <summary>Starts a workspace with every group collapsed, or gives back what the user had.</summary>
    internal void UseWorkspaceFolds(bool folds)
    {
        _collapsed = folds ? new CollapsedSlots(Enum.GetValues<DockSlot>()) : _users;
        Refresh();
    }

    /// <summary>Brings a group into view on one of its tabs.</summary>
    internal void Show(DockTabs group, int tab)
    {
        group.Select(tab);
        _floats.GetValueOrDefault(group)?.Activate();
        Open(group.Slot);
    }

    /// <summary>Shows or hides a tab. A tab turned on comes into view.</summary>
    internal void ToggleTab(DockTabs group, string title)
    {
        group.ToggleTab(title);
        if (group.IsShown(title))
        {
            Open(group.Slot);
        }
    }

    void Wire()
    {
        foreach (var side in new[] { Left, Right })
        {
            side.RailPressed += group => Open(group.Slot);
        }

        foreach (var group in _groups)
        {
            group.MoveRequested += to => Move(group, to);
            group.FloatRequested += () => Float(group);
            group.ResetRequested += Reset;
            group.CollapseRequested += () =>
            {
                _collapsed.Collapse(group.Slot);
                Refresh();
            };
            group.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsVisibleProperty)
                {
                    Refresh();
                }
            };
        }
    }

    // Puts each group in its place, then lays out the sides.
    void Refresh()
    {
        // Every place lets go before any takes a group, as a control has one parent.
        foreach (var (slot, place) in _places)
        {
            if (place.Child != DockedAt(slot))
            {
                place.Child = null;
            }
        }

        foreach (var (slot, place) in _places)
        {
            place.Child ??= DockedAt(slot);
        }

        var taken = _groups.Select(group => group.Slot).ToHashSet();
        foreach (var group in _groups)
        {
            group.ShowPlaces(taken);
        }

        Left.Show(ShownAt(DockSlot.UpperLeft), ShownAt(DockSlot.LowerLeft), group => _collapsed.Has(group.Slot));
        Right.Show(ShownAt(DockSlot.UpperRight), ShownAt(DockSlot.LowerRight), group => _collapsed.Has(group.Slot));
        Changed?.Invoke();
    }

    DockTabs? DockedAt(DockSlot slot) => _groups.FirstOrDefault(group => group.Slot == slot && !_floats.ContainsKey(group));

    // A group with every tab hidden takes no room.
    DockTabs? ShownAt(DockSlot slot) => DockedAt(slot) is { IsVisible: true } group ? group : null;

    IEnumerable<DockSlot> Taken() => Enum.GetValues<DockSlot>().Where(slot => ShownAt(slot) is not null);

    void Open(DockSlot slot)
    {
        _collapsed.Open(slot);
        Refresh();
    }

    void Reset()
    {
        foreach (var floating in _floats.Values.ToList())
        {
            floating.Close();
        }

        foreach (var (group, home) in _homes)
        {
            group.Slot = home;
            group.ShowAllTabs();
        }

        _users.OpenAll();
        _collapsed.OpenAll();
        Refresh();
        Said?.Invoke("Reset the layout. Every dock is back in its place with all its tabs.");
    }

    // A floating group docks before it moves. A group sent to a taken place trades with the group there, which may be
    // floating, and that one comes back to the place it traded for.
    void Move(DockTabs group, DockSlot to)
    {
        _floats.GetValueOrDefault(group)?.Close();
        var from = group.Slot;
        if (_groups.FirstOrDefault(each => each.Slot == to) is { } other)
        {
            other.Slot = from;
        }

        group.Slot = to;
        _collapsed.Open(from);
        Open(to);
        Said?.Invoke($"Moved {group.Title} to the {to.Label().ToLowerInvariant()}.");
    }

    // shortcut: the editor's keys do not reach the float window. Route them when floating groups are saved in the
    // layout.
    void Float(DockTabs group)
    {
        if (_floats.TryGetValue(group, out var floating))
        {
            floating.Close();
            return;
        }

        // The window is larger than the group by its edge, and starts that far up and left, so the group stays put.
        const double Edge = Gaps.Item;
        var size = group.Bounds.Size;
        var window = new Window
        {
            Title = group.Title,
            Width = size.Width + 2 * Edge,
            Height = size.Height + 2 * Edge,
            Padding = new Thickness(Edge),
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = group.PointToScreen(new Point(-Edge, -Edge)),
        };
        window.Closed += (_, _) =>
        {
            window.Content = null;
            _floats.Remove(group);
            group.Floats = false;
            Open(group.Slot);
        };
        _floats[group] = window;
        group.Floats = true;
        Refresh();
        window.Content = group;
        // The area is in the editor's window while its groups can be asked to float.
        window.Show((Window)TopLevel.GetTopLevel(this)!);
        Said?.Invoke($"{group.Title} is in a window of its own. Close it, or choose {DockTabs.PutBack} in its menu.");
    }
}
