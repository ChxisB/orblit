using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The window drawn small in a panel's menu: a box for each place round the view and one for a window of its own. A
/// click on a box asks for the panel to go there. The lit box is where it is, and the words under the map say what the
/// box under the pointer or the keys does.
/// </summary>
internal sealed class PanelMap : StackPanel
{
    internal const string Hint = "Click a box to move the panel there";

    internal const string PopOut = "Pop out into a window";

    /// <summary>What a screen reader calls the menu item that holds the map.</summary>
    internal const string ItemName = "Move this panel. Left and right pick a place.";

    /// <summary>The class of the box the panel is in.</summary>
    internal const string Here = "here";

    const string LockedNote = "The layout is locked";

    const double BoxWidth = 48;
    const double BoxHeight = 32;
    const double ViewWidth = 56;

    // The four places by slot, then the window.
    readonly Button[] _boxes;
    readonly ContentControl _words = new() { MinHeight = 34 };
    string _idle = Hint;

    public PanelMap()
    {
        var places = Enum.GetValues<DockSlot>();
        _boxes = [.. places.Select(slot => Box(slot.Label(), () => Picked?.Invoke(slot))), Box(PopOut, () => WindowPicked?.Invoke())];
        Spacing = Gaps.Item;
        Margin = new Thickness(0, Gaps.Tight);
        var map = Drawing();
        _words.Width = map.Width;
        Children.AddRange([map, _words]);
        // The pointer leaving a box leaves its words, until it leaves the map.
        PointerExited += (_, _) => Say(null);
        Say(null);
    }

    /// <summary>Raised with the place of the box pressed. The panel has not moved.</summary>
    public event Action<DockSlot>? Picked;

    /// <summary>Raised when the window's box is pressed. The panel has not moved.</summary>
    public event Action? WindowPicked;

    /// <summary>The box of each place, by place, then the window's.</summary>
    internal IReadOnlyList<Button> Boxes => _boxes;

    /// <summary>The words under the map: the hint, or a box's name and what a click on it does.</summary>
    internal IReadOnlyList<string> Words => _words.Content is StackPanel noted
        ? [((TextBlock)((Panel)noted.Children[0]).Children[0]).Text!, ((TextBlock)noted.Children[1]).Text!]
        : [((TextBlock)_words.Content!).Text!];

    /// <summary>Lights where the panel is, and turns off what a locked layout does not allow.</summary>
    /// <param name="home">The place the panel sits in, or comes back to from a window.</param>
    /// <param name="floating">Whether it is in a window of its own.</param>
    /// <param name="locked">Whether the layout is locked.</param>
    /// <param name="taken">The places that have a panel.</param>
    /// <param name="joins">Whether a move to a taken place makes the two panels one, instead of trading them.</param>
    internal void Show(DockSlot home, bool floating, bool locked, IReadOnlySet<DockSlot> taken, bool joins)
    {
        var there = joins ? "Joins the panel there" : "Trades places with the panel there";
        foreach (var slot in Enum.GetValues<DockSlot>())
        {
            var here = !floating && slot == home;
            // Going back does what closing the window does, so a lock leaves it on.
            var back = floating && slot == home;
            var note = here ? "It is here now" : back ? "Puts it back where it sat" : locked ? LockedNote : taken.Contains(slot) ? there : "Nothing is there yet";
            Mark(_boxes[(int)slot], here, !here && (back || !locked), note);
        }

        Mark(_boxes[^1], floating, !floating && !locked, floating ? "It is here now" : locked ? LockedNote : "Floats over the editor");
        _idle = !locked ? Hint : floating ? "Locked. It can only go back." : LockedNote;
        Say(null);
    }

    /// <summary>
    /// The menu item that holds the map. A press between the boxes leaves the menu open. The menu's keys skip the
    /// boxes, so left and right walk them here.
    /// </summary>
    internal MenuItem AsItem()
    {
        var item = new MenuItem { Header = this, StaysOpenOnClick = true, Classes = { EditorTheme.MapItem } };
        AutomationProperties.SetName(item, ItemName);
        item.KeyDown += (_, e) => e.Handled = Walk(e.Key);
        return item;
    }

    bool Walk(Key key)
    {
        // A focused box takes Enter itself, so here Enter comes from the item and goes into the map.
        var step = key switch { Key.Right or Key.Enter or Key.Space => 1, Key.Left => -1, _ => 0 };
        var on = _boxes.Where(box => box.IsEnabled).ToArray();
        if (step == 0 || on.Length == 0)
        {
            return false;
        }

        var at = Array.FindIndex(on, box => box.IsFocused);
        var next = at < 0 ? (step > 0 ? 0 : on.Length - 1) : (at + step + on.Length) % on.Length;
        on[next].Focus(NavigationMethod.Directional);
        return true;
    }

    static void Mark(Button box, bool here, bool on, string note)
    {
        box.IsEnabled = on;
        box.Classes.Set(Here, here);
        AutomationProperties.SetHelpText(box, note);
    }

    void Say(Button? box) => _words.Content = box is null
        ? new TextBlock { Text = _idle, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap }
        : Parts.Noted(AutomationProperties.GetName(box)!, AutomationProperties.GetHelpText(box));

    Button Box(string name, Action press)
    {
        var box = new Button { Classes = { EditorTheme.MapBox }, Width = BoxWidth, Height = BoxHeight };
        AutomationProperties.SetName(box, name);
        box.Click += (_, _) => press();
        box.GotFocus += (_, _) => Say(box);
        box.LostFocus += (_, _) => Say(null);
        return box;
    }

    // The view in the middle with the two places of each column beside it, and the window apart from them.
    Grid Drawing()
    {
        var map = new Grid
        {
            Width = (3 * BoxWidth) + ViewWidth + (2 * Gaps.Tight) + Gaps.Group,
            ColumnDefinitions = new ColumnDefinitions($"{BoxWidth},{Gaps.Tight},{ViewWidth},{Gaps.Tight},{BoxWidth},{Gaps.Group},{BoxWidth}"),
            RowDefinitions = new RowDefinitions($"{BoxHeight},{Gaps.Tight},{BoxHeight}"),
        };
        var view = new Border { CornerRadius = new CornerRadius(4), Background = EditorBrushes.Lifted };
        Grid.SetColumn(view, 2);
        Grid.SetRowSpan(view, 3);
        map.Children.Add(view);
        foreach (var slot in Enum.GetValues<DockSlot>())
        {
            var hoverable = Hoverable(_boxes[(int)slot]);
            Grid.SetColumn(hoverable, slot.IsLeft() ? 0 : 4);
            Grid.SetRow(hoverable, slot is DockSlot.LowerLeft or DockSlot.LowerRight ? 2 : 0);
            map.Children.Add(hoverable);
        }

        var window = _boxes[^1];
        var dashes = new Rectangle { Stroke = EditorBrushes.TextDim, StrokeThickness = 1, StrokeDashArray = [3, 2], RadiusX = 4, RadiusY = 4 };
        window.Content = new Panel { Children = { dashes, new EditorIcon("Window") { Width = 14, Height = 14 } } };
        var apart = new StackPanel
        {
            Spacing = Gaps.Tight,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Hoverable(window), new TextBlock { Text = "Window", FontSize = 11, Foreground = EditorBrushes.TextDim, HorizontalAlignment = HorizontalAlignment.Center } },
        };
        Grid.SetColumn(apart, 6);
        Grid.SetRowSpan(apart, 3);
        map.Children.Add(apart);
        return map;
    }

    // A box that is off takes no pointer, so the border round it does, and the words can say why.
    Border Hoverable(Button box)
    {
        var around = new Border { Background = Brushes.Transparent, Child = box };
        around.PointerEntered += (_, _) => Say(box);
        return around;
    }
}
