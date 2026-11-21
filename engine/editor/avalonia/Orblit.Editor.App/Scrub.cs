using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;

namespace Orblit.Editor.App;

/// <summary>
/// A drag across a handle, such as a number's label or an axis tag, that steps the number in its box. The box shows
/// each step, and the value is entered once, when the drag ends, so the drag is one step to undo.
/// </summary>
// shortcut: the scene changes when the drag ends, not while it runs. Enter each step live when edits can merge into one undo step.
internal static class Scrub
{
    /// <summary>Pixels of drag for one step.</summary>
    internal const double PixelsPerStep = 2;

    const double RealStep = 0.1;

    public static void Attach(Control handle, TextBox box, Action commit, bool whole)
    {
        handle.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        double? from = null;
        var start = 0.0;
        var moved = false;
        handle.PointerPressed += (_, e) =>
        {
            if (!box.IsEnabled || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed
                || !double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out start))
            {
                return;
            }

            from = e.GetPosition(handle).X;
            moved = false;
            e.Pointer.Capture(handle);
            e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (from is not { } x)
            {
                return;
            }

            var steps = Math.Round((e.GetPosition(handle).X - x) / PixelsPerStep);
            moved |= steps != 0;
            box.Text = whole ? ((long)start + (long)steps).ToString(CultureInfo.InvariantCulture) : VectorRow.AtRestText(start + (steps * RealStep));
        };
        handle.PointerReleased += (_, e) =>
        {
            // A click with no drag enters nothing, because the box may show a rounded value.
            if (from is not null && moved)
            {
                commit();
            }

            from = null;
            e.Pointer.Capture(null);
        };
        // The window lost the pointer mid drag, so the drag ends where it was.
        handle.PointerCaptureLost += (_, _) =>
        {
            if (from is not null && moved)
            {
                commit();
            }

            from = null;
        };
    }
}
