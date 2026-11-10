using Avalonia;
using Orblit.Editor.App;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

/// <summary>Reads a gizmo hit as the old tests asked for it, by the axis of one kind of part.</summary>
internal static class GizmoTestExtensions
{
    public static GizmoAxis? HandleAt(this ViewportControl viewport, Point at) =>
        viewport.HitAt(at) is { Part: GizmoPart.Move } hit ? hit.Axis : null;

    public static GizmoAxis? RingAt(this ViewportControl viewport, Point at) =>
        viewport.HitAt(at) is { Part: GizmoPart.Turn } hit ? hit.Axis : null;
}
