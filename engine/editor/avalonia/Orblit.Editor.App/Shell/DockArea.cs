using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body under the title bar: the middle, with groups of panels in four places round it and a rail at each edge.
/// Each place has the handle that collapses its group. A group moves, floats and collapses here, and a panel moves from
/// group to group. The groups it is made with, and their tabs and places, are the layout Reset gives back.
/// </summary>
internal sealed class DockArea : Grid
{
    public static readonly StyledProperty<bool> LockedProperty = AvaloniaProperty.Register<DockArea, bool>(nameof(Locked));

    readonly List<DockTabs> _groups;
    readonly (DockTabs Group, DockTab[] Tabs, DockSlot Slot)[] _homes;
    readonly Dictionary<DockTabs, Window> _floats = [];
    readonly Dictionary<DockSlot, Border> _places = Enum.GetValues<DockSlot>().ToDictionary(slot => slot, _ => new Border());
    // One for each place, over the group there. A floating group is in no place, so it has none.
    readonly Dictionary<DockSlot, Button> _handles = Enum.GetValues<DockSlot>().ToDictionary(slot => slot, slot => Parts.CollapseHandle(slot.IsLeft() ? Dock.Left : Dock.Right, "Folds this panel to a button at the window's edge."));
    readonly SideColumns _columns = new();
    // What the user collapsed, and what is collapsed now. A workspace that folds the sides has its own for as long as
    // it shows.
    readonly CollapsedSlots _users = new();
    CollapsedSlots _collapsed;

    public DockArea(Control center, params DockTabs[] groups)
    {
        _groups = [.. groups];
        _homes = [.. groups.Select(group => (group, group.Tabs.ToArray(), group.Slot))];
        _collapsed = _users;
        Middle = center;
        Margin = new Thickness(Gaps.Item, 0, Gaps.Item, Gaps.Item);
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        // A bare panel holds the group and its handle, so a handle can reach past the group's edge.
        var places = _places.ToDictionary(each => each.Key, each => new Panel { Children = { each.Value, _handles[each.Key] } });
        // The sides draw over what is beside them, where the handles reach.
        Left = new DockSide(places[DockSlot.UpperLeft], places[DockSlot.LowerLeft]) { ZIndex = 1 };
        Right = new DockSide(places[DockSlot.UpperRight], places[DockSlot.LowerRight]) { ZIndex = 1 };
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
        foreach (var tab in groups.SelectMany(group => group.Tabs))
        {
            TabDrag.Follow(tab, this);
        }

        Wire();
        Refresh();
    }

    /// <summary>Raised with what just happened, for the status line.</summary>
    public event Action<string>? Said;

    /// <summary>
    /// Raised after anything the layout keeps may have changed: a group moves, floats, collapses or locks, a tab is
    /// picked or hidden, or a splitter is let go. A move can raise it before it is whole.
    /// </summary>
    public event Action? Changed;

    /// <summary>Raised with a key pressed in a floating panel's window.</summary>
    public event Action<Window, KeyEventArgs>? FloatKeyDown;

    /// <summary>Every panel, with the place it starts in.</summary>
    internal IReadOnlyList<(string Title, DockSlot Home)> Panels =>
        [.. _homes.SelectMany(home => home.Tabs.Select(tab => (tab.Page.Title, home.Slot)))];

    internal DockSide Left { get; }

    internal DockSide Right { get; }

    internal Control Middle { get; }

    /// <summary>While true, no panel drags, moves or resizes. Collapsing, closing a window and hiding a tab still work.</summary>
    internal bool Locked
    {
        get => GetValue(LockedProperty);
        set => SetValue(LockedProperty, value);
    }

    /// <summary>Says why a drag does nothing while the layout is locked.</summary>
    internal void Refuse() => Said?.Invoke("The layout is locked. Unlock it in View or a panel's menu.");

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

    /// <summary>The group that holds the panel with this title now.</summary>
    internal DockTabs GroupOf(string title) => TabOf(title).Owner;

    /// <summary>Brings the panel with this title into view.</summary>
    internal void Show(string title)
    {
        var tab = TabOf(title);
        if (!tab.IsVisible)
        {
            tab.Owner.Toggle(tab);
        }

        tab.Owner.Select(tab);
        Uncover(tab.Owner);
    }

    /// <summary>Shows or hides a panel's tab. A tab turned on comes into view.</summary>
    internal void ToggleTab(string title)
    {
        var group = GroupOf(title);
        group.ToggleTab(title);
        if (group.IsShown(title))
        {
            Uncover(group);
        }
    }

    /// <summary>Whether the panel's tab shows, now and after each change.</summary>
    internal IObservable<bool> Shown(string title) => GroupOf(title).Shown(title);

    /// <summary>Carries out the drop of a dragged tab.</summary>
    /// <param name="tab">The tab dropped.</param>
    /// <param name="target">Where it goes.</param>
    /// <param name="at">The pointer on the screen, where a new window starts.</param>
    internal void Drop(DockTab tab, DropTarget target, PixelPoint at)
    {
        switch (target)
        {
            case DropTarget.Join join:
                Join(tab, join.Group);
                break;
            case DropTarget.Place place:
                Place(tab, place.Slot);
                break;
            default:
                FloatOut(tab, at);
                break;
        }
    }

    /// <summary>The layout as it is now, to save. A workspace that folds the sides is not part of it.</summary>
    internal SavedLayout Capture() => new()
    {
        Groups = [.. _groups.Select(group => new SavedGroup(group.Slot, group.Titles, SpotOf(group)))],
        Chosen = [.. _groups.Select(group => group.Chosen.Title)],
        Hidden = [.. _groups.SelectMany(group => group.Titles.Where(title => !group.IsShown(title)))],
        Collapsed = [.. Enum.GetValues<DockSlot>().Where(_users.Has)],
        Locked = Locked,
        LeftWidth = Math.Round(_columns.Asked(Left)),
        RightWidth = Math.Round(_columns.Asked(Right)),
        LeftSplit = Math.Round(Left.Split, 3),
        RightSplit = Math.Round(Right.Split, 3),
    };

    /// <summary>
    /// Lays out a saved layout over the one the area started with. Its windows wait for <see cref="ShowFloats"/>.
    /// </summary>
    internal void Apply(SavedLayout layout)
    {
        // The sizes go first, as a side that empties on the way keeps the size it had.
        Resize(Left, layout.LeftWidth, layout.LeftSplit);
        Resize(Right, layout.RightWidth, layout.RightSplit);
        var taken = new HashSet<DockTabs>();
        foreach (var saved in layout.Groups)
        {
            Regroup(saved, taken);
        }

        _groups.RemoveAll(group => group.Tabs.Count == 0);
        foreach (var tab in layout.Hidden.Select(TabOf))
        {
            tab.Owner.Toggle(tab);
        }

        foreach (var tab in layout.Chosen.Select(TabOf).Where(tab => tab.IsVisible))
        {
            tab.Owner.Select(tab);
        }

        foreach (var slot in layout.Collapsed)
        {
            _users.Collapse(slot);
        }

        Locked = layout.Locked;
        Refresh();
        foreach (var (group, window) in _floats)
        {
            window.Content = group;
        }
    }

    /// <summary>Shows the windows a saved layout made, once the editor's own window is up, each on a screen.</summary>
    internal void ShowFloats()
    {
        var editor = (Window)TopLevel.GetTopLevel(this)!;
        var screens = editor.Screens.All.Select(screen => screen.WorkingArea).ToList();
        foreach (var window in _floats.Values.Where(window => !window.IsVisible))
        {
            window.Position = SavedLayout.OnScreen(window.Position, screens, editor.Position);
            window.Show(editor);
        }
    }

    internal bool IsFloating(DockTabs group) => _floats.ContainsKey(group);

    /// <summary>The handle at the place the group sits in.</summary>
    internal Button CollapseButtonOf(DockTabs group) => _handles[group.Slot];

    /// <summary>The group whose window is under this point of the screen, if any.</summary>
    internal DockTabs? FloatingAt(PixelPoint screen) =>
        _floats.FirstOrDefault(each => new Rect(each.Value.ClientSize).Contains(each.Value.PointToClient(screen))).Key;

    /// <summary>The rail button under this point of the area, if any.</summary>
    internal Button? RailButtonAt(Point at) =>
        Left.RailButtons.Concat(Right.RailButtons).FirstOrDefault(button => button.IsEffectivelyVisible && Box(button).Contains(at));

    /// <summary>The docked group shown under this point of the area, if any.</summary>
    internal DockTabs? ShownGroupAt(Point at) =>
        _groups.FirstOrDefault(group => !IsFloating(group) && group.IsEffectivelyVisible && Box(group).Contains(at));

    /// <summary>Whether a group would still sit in a place once this tab has left where it is.</summary>
    internal bool IsTaken(DockSlot slot, DockTab tab) =>
        _groups.Any(group => group.Slot == slot && !IsFloating(group) && !(group == tab.Owner && group.Tabs.Count == 1));

    /// <summary>Where a control of the area sits in it.</summary>
    internal Rect Box(Control control) => new(control.TranslatePoint(default, this)!.Value, control.Bounds.Size);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LockedProperty)
        {
            ApplyLock();
        }
    }

    // shortcut: the system still moves a floating window by its title bar while locked. Take the title bar's drag away
    // if that matters.
    void ApplyLock()
    {
        Left.Locked = Right.Locked = Locked;
        foreach (var window in _floats.Values)
        {
            window.CanResize = !Locked;
        }

        Refresh();
        Said?.Invoke(Locked ? "Locked the layout. Panels stay where they are." : "Unlocked the layout. Panels can move again.");
    }

    DockTab TabOf(string title) => _homes.SelectMany(home => home.Tabs).Single(tab => tab.Page.Title == title);

    void Wire()
    {
        foreach (var side in new[] { Left, Right })
        {
            side.RailPressed += group => Open(group.Slot);
            side.Resized += () => Changed?.Invoke();
        }

        foreach (var group in _groups)
        {
            Wire(group);
        }

        foreach (var (slot, handle) in _handles)
        {
            handle.Click += (_, _) => Collapse(slot);
        }
    }

    void Wire(DockTabs group)
    {
        group.MoveRequested += to => Move(group, to);
        group.FloatRequested += () => Float(group);
        group.ResetRequested += Reset;
        group.LockRequested += () => Locked = !Locked;
        group.TabsChanged += () =>
        {
            Changed?.Invoke();
            // Settled once the move or menu that changed the tabs has ended.
            Dispatcher.UIThread.Post(() => Settle(group));
        };
        group.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                Refresh();
            }
        };
    }

    // A rail button and a window take their names from the group's tabs, and a window with none to show closes.
    void Settle(DockTabs group)
    {
        if (!group.IsVisible && _floats.TryGetValue(group, out var window))
        {
            window.Close();
        }
        else if (_collapsed.Has(group.Slot) || IsFloating(group))
        {
            Refresh();
        }
    }

    // Puts each group in its place, then lays out the sides.
    void Refresh()
    {
        // Every place lets go before any takes a group, as a control has one parent.
        foreach (var (slot, place) in _places)
        {
            if (place.Child != Docked(slot))
            {
                place.Child = null;
            }
        }

        foreach (var (slot, place) in _places)
        {
            place.Child ??= Docked(slot);
        }

        var taken = _groups.Select(group => group.Slot).ToHashSet();
        foreach (var group in _groups)
        {
            // A floating group whose home is taken has no place to trade, so a move makes the two one.
            group.ShowPlaces(taken, Locked, IsFloating(group) && Docked(group.Slot) is not null);
        }

        foreach (var (group, window) in _floats)
        {
            window.Title = group.Title;
        }

        Left.Show(ShownAt(DockSlot.UpperLeft), ShownAt(DockSlot.LowerLeft), group => _collapsed.Has(group.Slot));
        Right.Show(ShownAt(DockSlot.UpperRight), ShownAt(DockSlot.LowerRight), group => _collapsed.Has(group.Slot));
        Changed?.Invoke();
    }

    DockTabs? Docked(DockSlot slot, DockTabs? except = null) =>
        _groups.FirstOrDefault(group => group.Slot == slot && group != except && !IsFloating(group));

    // A group with every tab hidden takes no room.
    DockTabs? ShownAt(DockSlot slot) => Docked(slot) is { IsVisible: true } group ? group : null;

    IEnumerable<DockSlot> Taken() => Enum.GetValues<DockSlot>().Where(slot => ShownAt(slot) is not null);

    void Collapse(DockSlot slot)
    {
        _collapsed.Collapse(slot);
        Refresh();
    }

    void Open(DockSlot slot)
    {
        _collapsed.Open(slot);
        Refresh();
    }

    void Uncover(DockTabs group)
    {
        if (_floats.TryGetValue(group, out var window))
        {
            window.Activate();
        }
        else
        {
            Open(group.Slot);
        }
    }

    void Reset()
    {
        foreach (var floating in _floats.Keys.ToList())
        {
            Detach(floating);
        }

        foreach (var (group, tabs, slot) in _homes)
        {
            foreach (var tab in tabs)
            {
                tab.Owner.Remove(tab);
                group.Add(tab);
            }

            group.Slot = slot;
            group.ShowAllTabs();
            group.Select(tabs[0]);
        }

        _groups.Clear();
        _groups.AddRange(_homes.Select(home => home.Group));
        _users.OpenAll();
        _collapsed.OpenAll();
        Refresh();
        // Both sides are open now, so each can take its first width.
        Resize(Left, SideColumns.LeftStart, 0.5);
        Resize(Right, SideColumns.RightStart, 0.5);
        Said?.Invoke("Reset the layout. Every dock is back in its place with all its tabs.");
    }

    // A floating group docks before it moves. A group sent to a taken place trades with the group there, and a floating
    // group that held the place comes back to the one traded for it. With nothing to trade, the two become one.
    void Move(DockTabs group, DockSlot to)
    {
        var from = group.Slot;
        Detach(group);
        if (Docked(to, group) is { } there)
        {
            if (Docked(from, group) is not null)
            {
                var moved = group.Chosen.Title;
                Merge(group, there);
                Open(to);
                Said?.Invoke($"Moved {moved} in with {there.Title}.");
                return;
            }

            there.Slot = from;
        }

        foreach (var floating in _floats.Keys.Where(each => each.Slot == to))
        {
            floating.Slot = from;
        }

        group.Slot = to;
        _collapsed.Open(from);
        Open(to);
        Said?.Invoke($"Moved {group.Title} to the {to.Label().ToLowerInvariant()}.");
    }

    void Join(DockTab tab, DockTabs into)
    {
        var from = tab.Owner;
        from.Remove(tab);
        into.Add(tab);
        into.Select(tab);
        if (from.Tabs.Count == 0)
        {
            Detach(from);
            _groups.Remove(from);
        }

        Uncover(into);
        Refresh();
        Said?.Invoke($"Moved {tab.Page.Title} in with {into.Title}.");
    }

    // The group in the place moves to the other place of the side, and the tab takes the one it asked for.
    void Place(DockTab tab, DockSlot slot)
    {
        var group = Alone(tab);
        Detach(group);
        if (Docked(slot, group) is { } there)
        {
            there.Slot = slot.Sibling();
            _collapsed.Open(there.Slot);
        }

        group.Slot = slot;
        group.Select(tab);
        Open(slot);
        Said?.Invoke($"Moved {tab.Page.Title} to the {slot.Label().ToLowerInvariant()}.");
    }

    void FloatOut(DockTab tab, PixelPoint at)
    {
        var size = tab.Owner.Bounds.Size;
        OpenWindow(Alone(tab), size, at);
    }

    // The tab's group when it is the only one there, or a new group for it.
    DockTabs Alone(DockTab tab)
    {
        var from = tab.Owner;
        if (from.Tabs.Count == 1)
        {
            return from;
        }

        from.Remove(tab);
        var group = NewGroup();
        group.Slot = from.Slot;
        group.Add(tab);
        return group;
    }

    DockTabs NewGroup()
    {
        var group = new DockTabs();
        Wire(group);
        _groups.Add(group);
        return group;
    }

    void Resize(DockSide side, double? width, double? split)
    {
        if (width is { } across)
        {
            _columns.Ask(side, across);
        }

        if (split is { } share)
        {
            side.Split = share;
        }
    }

    // A saved group takes the group its first panel is in, unless another saved group has taken that one.
    void Regroup(SavedGroup saved, HashSet<DockTabs> taken)
    {
        var tabs = saved.Titles.Select(TabOf).ToList();
        var group = taken.Add(tabs[0].Owner) ? tabs[0].Owner : NewGroup();
        taken.Add(group);
        foreach (var tab in tabs)
        {
            tab.Owner.Remove(tab);
            group.Add(tab);
        }

        group.Slot = saved.Slot;
        if (saved.Window is { } spot)
        {
            MakeWindow(group, spot);
        }
    }

    FloatSpot? SpotOf(DockTabs group) =>
        _floats.TryGetValue(group, out var window) ? new FloatSpot(window.Position, new Size(window.Width, window.Height)) : null;

    // Every tab of one group moves into another, with the chosen one still chosen. The empty group goes.
    void Merge(DockTabs from, DockTabs into)
    {
        var chosen = from.Tabs[from.SelectedIndex];
        foreach (var tab in from.Tabs.ToList())
        {
            from.Remove(tab);
            into.Add(tab);
        }

        if (chosen.IsVisible)
        {
            into.Select(chosen);
        }

        _groups.Remove(from);
    }

    // Takes a group out of its window without putting it back, for a caller that places it.
    void Detach(DockTabs group)
    {
        if (_floats.Remove(group, out var window))
        {
            window.Content = null;
            group.Floats = false;
            window.Close();
        }
    }

    // A window that closes puts its group back, in with the group that has taken its place meanwhile.
    void PutBack(DockTabs group)
    {
        if (Docked(group.Slot, group) is { } there)
        {
            Merge(group, there);
        }

        Open(group.Slot);
    }

    void Float(DockTabs group)
    {
        if (_floats.TryGetValue(group, out var floating))
        {
            floating.Close();
            return;
        }

        // The window is larger than the group by its edge, and starts that far up and left, so the group stays put.
        OpenWindow(group, group.Bounds.Size, group.PointToScreen(new Point(-Gaps.Item, -Gaps.Item)));
    }

    void OpenWindow(DockTabs group, Size size, PixelPoint at)
    {
        var edges = new Size(2 * Gaps.Item, 2 * Gaps.Item);
        var window = MakeWindow(group, new FloatSpot(at, new Size(size.Width + edges.Width, size.Height + edges.Height)));
        Refresh();
        window.Content = group;
        // The area is in the editor's window while its groups can be asked to float.
        window.Show((Window)TopLevel.GetTopLevel(this)!);
        Said?.Invoke($"{group.Title} is in a window of its own. Close it, or choose {DockTabs.PutBack} in its menu.");
    }

    // A window for the group, not yet shown and without the group in it, as the group may still sit in a place.
    Window MakeWindow(DockTabs group, FloatSpot spot)
    {
        var window = new Window
        {
            Title = group.Title,
            Width = spot.Size.Width,
            Height = spot.Size.Height,
            Padding = new Thickness(Gaps.Item),
            ShowInTaskbar = false,
            CanResize = !Locked,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = spot.At,
        };
        // shortcut: moving or resizing the window is saved at the next change or on close. Raise Changed on both if
        // losing it to a crash matters.
        window.KeyDown += (_, e) => FloatKeyDown?.Invoke(window, e);
        window.Closed += (_, _) =>
        {
            // A window that Detach closed holds nothing any more.
            if (_floats.GetValueOrDefault(group) == window)
            {
                _floats.Remove(group);
                window.Content = null;
                group.Floats = false;
                PutBack(group);
            }
        };
        _floats[group] = window;
        group.Floats = true;
        return window;
    }
}
