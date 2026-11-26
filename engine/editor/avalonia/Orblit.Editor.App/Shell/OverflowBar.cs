using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Orblit.Editor.App.Shell;

/// <summary>A tool of an <see cref="OverflowBar"/> that is not a button itself: its menu item's words and the button the item presses.</summary>
internal interface IOverflowTool
{
    string MenuWords { get; }

    Button MenuButton { get; }
}

/// <summary>
/// A row of tools that moves the ones that do not fit into a More menu at its end, the last first. The
/// <see cref="Pinned"/> tool never moves. A first tool with a least width shrinks to it before any tool moves. A
/// moved tool stays in the row, out of view and out of the Tab order, so it keeps its state, and its menu item
/// presses it.
/// </summary>
internal sealed class OverflowBar : Panel
{
    /// <summary>The class of a tool the menu holds.</summary>
    internal const string Moved = "moved";

    readonly Button _more = Parts.Dress(new Button(), "GuiTabMenuHl", new HoverCard("More", "The tools that do not fit here."));
    IReadOnlyList<Control> _shown = [];
    Control? _pinned;

    public OverflowBar(params Control[] tools)
    {
        ClipToBounds = true;
        Children.AddRange(tools);
        Children.Add(_more);
        var menu = new MenuFlyout();
        menu.Opening += (_, _) => Fill(menu);
        _more.Flyout = menu;
    }

    public double Spacing { get; init; } = 4;

    /// <summary>The tool that stays in view, in place of the last one that fits when it would have moved.</summary>
    public Control? Pinned
    {
        get => _pinned;
        set
        {
            _pinned = value;
            InvalidateMeasure();
        }
    }

    /// <summary>The tools in their order, without the More button.</summary>
    public IEnumerable<Control> Tools => Children.Take(Children.Count - 1);

    internal Button MoreButton => _more;

    private bool Hides => _shown.Count < Children.Count - 1;

    public void Add(Control tool) => Children.Insert(Children.Count - 1, tool);

    public void RemoveAt(int at) => Children.RemoveAt(at);

    public void Clear() => Children.RemoveRange(0, Children.Count - 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        var endless = new Size(double.PositiveInfinity, availableSize.Height);
        foreach (var child in Children)
        {
            child.Measure(endless);
        }

        var count = Children.Count - 1;
        _shown = Pick(count);
        while (_shown.Count > 1 && Wanted(Least(_shown[0])) > availableSize.Width)
        {
            _shown = Pick(--count);
        }

        if (_shown.Count == 0)
        {
            return default;
        }

        var width = FirstWidth(availableSize.Width);
        _shown[0].Measure(new Size(width, availableSize.Height));
        return new Size(Math.Min(Wanted(width), availableSize.Width), Children.Max(child => child.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children)
        {
            var shown = child == _more ? Hides : _shown.Contains(child);
            var width = shown && child == _shown.FirstOrDefault() ? FirstWidth(finalSize.Width) : child.DesiredSize.Width;
            child.Arrange(new Rect(shown ? x : finalSize.Width + Spacing, 0, width, finalSize.Height));
            Show(child, shown);
            x += shown ? width + Spacing : 0;
        }

        return finalSize;
    }

    IReadOnlyList<Control> Pick(int count)
    {
        var picked = Tools.Take(count).ToList();
        if (_pinned is { } pinned && picked.Count > 0 && !picked.Contains(pinned))
        {
            picked[^1] = pinned;
        }

        return picked;
    }

    // The width of the tools in view when the first is this wide, with the More button when a tool has moved.
    double Wanted(double first) =>
        first + _shown.Skip(1).Sum(tool => tool.DesiredSize.Width + Spacing) + (Hides ? Spacing + _more.DesiredSize.Width : 0);

    // A first tool with a least width stretches and shrinks between that and its widest. Any other keeps its own
    // width, and gives way only when it is the one tool left and still does not fit.
    double FirstWidth(double available)
    {
        var first = _shown[0];
        var room = Math.Max(0, available - Wanted(0));
        if (first.MinWidth <= 0)
        {
            return Math.Min(first.DesiredSize.Width, room);
        }

        var margins = first.Margin.Left + first.Margin.Right;
        var most = double.IsFinite(first.MaxWidth) ? first.MaxWidth + margins : first.DesiredSize.Width;
        return Math.Clamp(room, first.MinWidth + margins, most);
    }

    static double Least(Control tool) => tool.MinWidth > 0 ? tool.MinWidth + tool.Margin.Left + tool.Margin.Right : tool.DesiredSize.Width;

    static void Show(Control tool, bool shown)
    {
        tool.Classes.Set(Moved, !shown);
        foreach (var button in tool.GetVisualDescendants().Prepend(tool).OfType<Button>())
        {
            button.Focusable = shown;
        }
    }

    void Fill(MenuFlyout menu)
    {
        menu.Items.Clear();
        foreach (var tool in Tools.Except(_shown))
        {
            if (tool is IOverflowTool named)
            {
                menu.Items.Add(Item(named.MenuWords, named.MenuButton));
            }
            else if (tool is Button button)
            {
                menu.Items.Add(Item(ToolTip.GetTip(button) is HoverCard card ? card.Title : AutomationProperties.GetName(button), button));
            }
        }
    }

    static MenuItem Item(string? words, Button button)
    {
        var toggle = button as ToggleButton;
        var item = new MenuItem
        {
            Header = words,
            IsEnabled = button.IsEnabled,
            ToggleType = toggle is null ? MenuItemToggleType.None : MenuItemToggleType.CheckBox,
            IsChecked = toggle?.IsChecked == true,
        };
        item.Click += (_, _) => Press(button);
        return item;
    }

    // The automation peer presses the button as a click does.
    static void Press(Button button)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        if (peer is IToggleProvider toggle)
        {
            toggle.Toggle();
            return;
        }

        ((IInvokeProvider)peer).Invoke();
    }
}
