using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.Layout.Checks;

/// <summary>
/// Finds the parts of a window that are cut, spill out of their box, sit on each other or touch. A part inside one
/// that is already reported is not reported again, because its fault is most often the same one.
/// </summary>
internal static class LayoutAudit
{
    // Layout rounds each part to whole pixels, so a part may end one pixel past its box. A gap is held to half.
    const double Rounding = 1;
    const double Slack = 0.5;

    public static List<string> Faults(Visual window)
    {
        var faults = new List<string>();
        var reported = new HashSet<Visual>();
        foreach (var control in window.GetVisualDescendants().OfType<Control>().Where(control => IsShown(control) && !IsExempt(control)))
        {
            if (control.GetVisualAncestors().Any(reported.Contains))
            {
                continue;
            }

            var fault = Overflow(control, window) ?? Clipped(control, window) ?? Squeezed(control) ?? TextCut(control) ?? Overlap(control, window) ?? Tight(control, window);
            if (fault is not null)
            {
                reported.Add(control);
                faults.Add($"{fault}: {Describe(control)}");
            }
        }

        return faults;
    }

    static bool IsShown(Control control) => control.IsEffectivelyVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0;

    // A scroll bar lays its parts over the content on purpose. A popup draws its child in a layer of its own. A
    // turned or scaled child keeps its own box, so its box says nothing about where it draws. A tool in a More menu is
    // placed out of view on purpose.
    static bool IsExempt(Control control) =>
        control is Popup || control.GetSelfAndVisualAncestors().Any(visual =>
            visual is ScrollBar ||
            (visual is Control tool && tool.Classes.Contains(OverflowBar.Moved)) ||
            (visual is Viewbox or LayoutTransformControl { LayoutTransform: not null } && visual != control));

    static string? Overflow(Control control, Visual window)
    {
        // Content that scrolls or is placed by hand may run past its parent's box.
        if (control.GetVisualParent() is not Control parent || parent is ScrollContentPresenter or Canvas)
        {
            return null;
        }

        var (box, room, margin) = (BoxOf(control, window), BoxOf(parent, window), control.Margin);
        var wide = Past(room.X - box.X, margin.Left) + Past(box.Right - room.Right, margin.Right);
        var tall = Past(room.Y - box.Y, margin.Top) + Past(box.Bottom - room.Bottom, margin.Bottom);
        return wide > Rounding || tall > Rounding ? $"runs {wide:0}x{tall:0} past {Describe(parent)}" : null;
    }

    // A part partly outside a box that clips is cut, unless that box scrolls it that way. A part that scrolls is
    // inside every box further out. A Canvas places its parts by hand, so what it cuts is only out of view.
    static string? Clipped(Control control, Visual window)
    {
        var box = BoxOf(control, window);
        var (across, down) = (false, false);
        foreach (var holder in control.GetVisualAncestors().OfType<Control>().TakeWhile(holder => holder is not Canvas))
        {
            var scroll = holder as ScrollContentPresenter;
            (across, down) = (across || scroll?.CanHorizontallyScroll == true, down || scroll?.CanVerticallyScroll == true);
            if (!holder.ClipToBounds)
            {
                continue;
            }

            var room = BoxOf(holder, window);
            var wide = across ? 0 : Math.Max(0, Math.Max(room.X - box.X, box.Right - room.Right));
            var tall = down ? 0 : Math.Max(0, Math.Max(room.Y - box.Y, box.Bottom - room.Bottom));
            if (wide > Rounding || tall > Rounding)
            {
                return $"cut {wide:0}x{tall:0} by {Describe(holder)}";
            }
        }

        return null;
    }

    // A negative margin places a part past its parent on purpose, by that much and no more.
    static double Past(double over, double margin) => Math.Max(0, over - Math.Max(0, -margin));

    // A box that got less room than it asked for. A Viewbox scales its child to the room it gets, so it never is.
    static string? Squeezed(Control control)
    {
        if (control is Viewbox)
        {
            return null;
        }

        var wide = control.DesiredSize.Width - control.Margin.Left - control.Margin.Right - control.Bounds.Width;
        var tall = control.DesiredSize.Height - control.Margin.Top - control.Margin.Bottom - control.Bounds.Height;
        return wide > Rounding || tall > Rounding ? $"squeezed {Math.Max(0, wide):0}x{Math.Max(0, tall):0}" : null;
    }

    // Words cut short with "…" pass only when a hover card on the words or a box around them gives them in full.
    static string? TextCut(Control control)
    {
        if (control is not TextBlock text || string.IsNullOrEmpty(text.Text))
        {
            return null;
        }

        var layout = text.TextLayout;
        if (layout.TextLines.Any(line => line.HasCollapsed))
        {
            return text.GetSelfAndVisualAncestors().Any(visual => visual is Control { } box && ToolTip.GetTip(box) is not null) ? null : "cut short with no hover card";
        }

        var wide = layout.Width - (text.Bounds.Width - text.Padding.Left - text.Padding.Right);
        var tall = layout.Height - (text.Bounds.Height - text.Padding.Top - text.Padding.Bottom);
        return wide > Rounding ? $"words {wide:0} wider than their box" : tall > Rounding ? $"words {tall:0} taller than their box" : null;
    }

    static string? Overlap(Control control, Visual window)
    {
        // A bare Panel and a Canvas stack their children on purpose, and grid children in one cell share it.
        if (control.GetVisualParent() is not Panel panel || panel is Canvas || panel.GetType() == typeof(Panel))
        {
            return null;
        }

        var box = BoxOf(control, window);
        var other = panel.Children.FirstOrDefault(other => other != control && other is not Popup && IsShown(other) && !IsExempt(other) && !(panel is Grid && SharesCell(control, other)) && Covers(box.Intersect(BoxOf(other, window))));
        return other is null ? null : $"sits on {Describe(other)}";
    }

    static bool Covers(Rect cut) => cut.Width > 1 && cut.Height > 1;

    // Only things a person reads or presses are held to the spacing scale. A box keeps its own padding, so two boxes
    // may touch. Two lines of words keep the gap their fonts give them.
    static string? Tight(Control control, Visual window)
    {
        // A control's own template and a list of items lay their parts edge to edge, with the room inside each part.
        if (control.GetVisualParent() is not StackPanel panel || panel.TemplatedParent is not null || panel.IsItemsHost)
        {
            return null;
        }

        var shown = panel.Children.Where(IsShown).ToList();
        var at = shown.IndexOf(control);
        if (at <= 0 || !IsSpaced(shown[at - 1], control, panel))
        {
            return null;
        }

        var before = shown[at - 1];
        var (a, b) = (BoxOf(before, window), BoxOf(control, window));
        var gap = panel.Orientation == Orientation.Horizontal ? b.X - a.Right : b.Y - a.Bottom;
        var least = panel.Classes.Contains(EditorTheme.Joined) ? Gaps.Joined : Gaps.Tight;
        return gap < least - Slack ? $"{gap:0} from {Describe(before)}, needs {least}" : null;
    }

    static bool IsSpaced(Control before, Control control, StackPanel panel) =>
        IsRead(before) && IsRead(control) &&
        !(panel.Orientation == Orientation.Vertical && before is TextBlock && control is TextBlock);

    static bool IsRead(Control control) =>
        control is TextBlock or Button or TextBox or ComboBox or ToggleSwitch or EditorIcon or Image;

    static bool SharesCell(Control a, Control b) =>
        Spans(Grid.GetColumn(a), Grid.GetColumnSpan(a), Grid.GetColumn(b), Grid.GetColumnSpan(b)) &&
        Spans(Grid.GetRow(a), Grid.GetRowSpan(a), Grid.GetRow(b), Grid.GetRowSpan(b));

    static bool Spans(int a, int aSpan, int b, int bSpan) => a < b + bSpan && b < a + aSpan;

    // The type, name, words and size of a part, and the editor class that holds it, so a fault can be found.
    static string Describe(Control control)
    {
        var owner = control.GetSelfAndLogicalAncestors().OfType<Control>().FirstOrDefault(c => c.GetType().Namespace?.StartsWith("Orblit", StringComparison.Ordinal) == true);
        var words = control is TextBlock text ? text.Text
            : AutomationProperties.GetName(control) is { Length: > 0 } name ? name
            : control.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text).FirstOrDefault(text => !string.IsNullOrEmpty(text));
        var quoted = words is null ? "" : $" '{(words.Length > 40 ? words[..40] + "…" : words)}'";
        var named = string.IsNullOrEmpty(control.Name) ? "" : "#" + control.Name;
        return $"{control.GetType().Name}{named}{quoted} {control.Bounds.Width:0}x{control.Bounds.Height:0} in {owner?.GetType().Name ?? "?"}";
    }
}
