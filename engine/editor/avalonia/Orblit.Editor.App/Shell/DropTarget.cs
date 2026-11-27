using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// Where a dragged tab would go if dropped now, with the part of a control to mark and the words to mark it with.
/// </summary>
internal abstract record DropTarget(Control On, Rect Mark, string Words)
{
    /// <summary>How near an edge of the window docks on a side that shows no panel.</summary>
    const double EdgeReach = 56;

    internal sealed record Join(DockTabs Group, Control On, Rect Mark) : DropTarget(On, Mark, $"Add to {Group.Title}");

    internal sealed record Place(DockSlot Slot, Control On, Rect Mark, string Words) : DropTarget(On, Mark, Words);

    internal sealed record Float(Control On, Rect Mark) : DropTarget(On, Mark, "Release to float in a window");

    /// <summary>The target under a point of the screen, or none where a drop would change nothing.</summary>
    internal static DropTarget? At(DockArea area, DockTab tab, PixelPoint screen)
    {
        if (area.FloatingAt(screen) is { } floating)
        {
            return floating == tab.Owner ? null : Whole(floating, floating);
        }

        var at = area.PointToClient(screen);
        if (area.ShownGroupAt(at) is { } group)
        {
            return OnGroup(area, tab, group, at);
        }

        return (DropTarget?)AtEdge(area, tab, at) ?? InMiddle(area, tab, at);
    }

    static Join Whole(DockTabs group, Control on) => new(group, on, new Rect(on.Bounds.Size));

    // The top and bottom thirds split a side that has room, and the rest of the group, strip first, takes the tab in.
    static DropTarget? OnGroup(DockArea area, DockTab tab, DockTabs group, Point at)
    {
        var own = group == tab.Owner;
        if (own && group.Tabs.Count == 1)
        {
            return null;
        }

        var box = area.Box(group);
        var y = at.Y - box.Top;
        var upper = group.Slot.IsLeft() ? DockSlot.UpperLeft : DockSlot.UpperRight;
        var half = box.Height / 2;
        if (y > DockTabs.StripHeight && !area.IsTaken(group.Slot.Sibling(), tab))
        {
            if (y < box.Height / 3)
            {
                return new Place(upper, group, new Rect(0, 0, box.Width, half), $"Dock above {group.Title}");
            }

            if (y > box.Height * 2 / 3)
            {
                return new Place(upper.Sibling(), group, new Rect(0, half, box.Width, half), $"Dock below {group.Title}");
            }
        }

        return own ? null : Whole(group, group);
    }

    // A side that shows no panel takes one at the window's edge, in its upper place when that is free.
    static Place? AtEdge(DockArea area, DockTab tab, Point at)
    {
        var size = area.Bounds.Size;
        var left = at.X < EdgeReach;
        var atEdge = new Rect(size).Contains(at) && (left || at.X > size.Width - EdgeReach);
        var upper = left ? DockSlot.UpperLeft : DockSlot.UpperRight;
        var free = area.IsTaken(upper, tab) ? upper.Sibling() : upper;
        if (!atEdge || (left ? area.Left : area.Right).IsVisible || area.IsTaken(free, tab))
        {
            return null;
        }

        var mark = new Rect(left ? 0 : size.Width - EdgeReach, 0, EdgeReach, size.Height);
        return new Place(free, area, mark, left ? "Dock on the left" : "Dock on the right");
    }

    // The middle, or anywhere off the area, floats the tab. A tab alone in a window is floating already.
    static Float? InMiddle(DockArea area, DockTab tab, Point at)
    {
        var floatsAlready = area.IsFloating(tab.Owner) && tab.Owner.Tabs.Count == 1;
        var middle = area.Box(area.Middle).Contains(at) || !new Rect(area.Bounds.Size).Contains(at);
        return middle && !floatsAlready ? new Float(area.Middle, new Rect(area.Middle.Bounds.Size)) : null;
    }
}
