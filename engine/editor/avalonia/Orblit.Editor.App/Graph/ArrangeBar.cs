using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The tools for the picked steps, on a floating pill. Line up and Spread open menus. Tidy up holds the buttons that
/// straighten wires, pick joined steps, open up a call and make an action. A tool that needs other steps picked is
/// off, and its card or note says what to pick first.
/// </summary>
internal sealed class ArrangeBar : Border
{
    const string CallKind = "call_function";
    const string PickTwo = "Pick two or more steps first.";
    const string PickThree = "Pick three or more steps first.";
    const string EvenGaps = "The same space between each";

    readonly Button _lineUp;
    readonly Button _spread;
    readonly Button _straighten;
    readonly Button _expand;
    readonly MenuItem[] _gaps;

    public ArrangeBar(GraphArranger arranger, Action<IEnumerable<int>> select, GraphEdits edits)
    {
        IsVisible = false;
        Classes.Add(EditorTheme.Island);
        Background = EditorBrushes.Floating;
        BorderBrush = EditorBrushes.LineStrong;
        BoxShadow = BoxShadows.Parse("0 6 20 0 #59000000");
        Padding = new Thickness(8);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(12, 8, 0, 0);
        _gaps =
        [
            Item("spread_across", "Even gaps across", () => arranger.Distribute(Axis.Horizontal), EvenGaps),
            Item("spread_down", "Even gaps down", () => arranger.Distribute(Axis.Vertical), EvenGaps),
        ];
        _lineUp = Menu("line_up", new HoverCard("Line up", "Lines up the picked steps by their edges or centers."), PickTwo, LineUpItems(arranger));
        _spread = Menu("spread", new HoverCard("Spread", "Puts the picked steps in a row or a column, or evens out the gaps."), PickTwo,
        [
            Item("stack_row", "Side by side", () => arranger.Stack(Axis.Horizontal), "One row, in the order they sit"),
            Item("stack_column", "One under another", () => arranger.Stack(Axis.Vertical), "One column"),
            new Separator(),
            .. _gaps,
        ]);
        _straighten = Tool("straighten", new HoverCard("Straighten wires", "Moves steps up or down until the wires between them are level."), arranger.Straighten, "Pick two or more joined steps first.");
        _expand = Tool("expand", new HoverCard("Open up the call", "Puts the steps of the action it runs in its place."), edits.Expand, "Pick a step that runs an action first.");
        var box = Tool("box", new HoverCard("Put in a box", "Draws a box round the picked steps.", "⌘G"), () => edits.FrameSelection());
        var joined = Tool("flow", new HoverCard("Pick joined steps", "Adds every step joined by a run wire to what is picked."), () => select(arranger.Flow()));
        var action = Tool("collapse", new HoverCard("Make an action", "Moves the picked steps into a new action and runs it from here."), edits.Collapse);
        var tidy = new TextBlock { Text = "Tidy up", FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 4, 0) };
        Child = new StackPanel { Spacing = 6, Children = { Row(_lineUp, _spread, box), Row(tidy, _straighten, joined, _expand, action) } };
    }

    /// <summary>The items of the Line up and Spread menus that run a tool, in menu order.</summary>
    internal IEnumerable<MenuItem> Items =>
        new[] { _lineUp, _spread }.Select(menu => menu.Flyout).OfType<MenuFlyout>().SelectMany(flyout => flyout.Items.OfType<MenuItem>()).Where(item => item.Tag is string);

    /// <summary>Shows the bar while steps are picked, and turns on the tools that fit the kinds of the picked steps.</summary>
    internal void Sync(IReadOnlyList<string> picked)
    {
        IsVisible = picked.Count > 0;
        foreach (var tool in new[] { _lineUp, _spread, _straighten })
        {
            tool.IsEnabled = picked.Count > 1;
        }

        _expand.IsEnabled = picked is [CallKind];
        foreach (var gaps in _gaps)
        {
            gaps.IsEnabled = picked.Count > 2;
            gaps.Header = Parts.Noted(AutomationProperties.GetName(gaps) ?? "", gaps.IsEnabled ? EvenGaps : PickThree);
        }
    }

    static Control[] LineUpItems(GraphArranger arranger) =>
    [
        Parts.MenuHeading("Line up the picked steps"),
        Item("align_left", "Left edges", () => arranger.Align(Edge.Left)),
        Item("align_center", "Centers across", () => arranger.Align(Edge.Center)),
        Item("align_right", "Right edges", () => arranger.Align(Edge.Right)),
        new Separator(),
        Item("align_top", "Top edges", () => arranger.Align(Edge.Top)),
        Item("align_middle", "Centers down", () => arranger.Align(Edge.Middle)),
        Item("align_bottom", "Bottom edges", () => arranger.Align(Edge.Bottom)),
    ];

    static MenuItem Item(string id, string name, Action run, string? note = null)
    {
        var item = Parts.ActionItem(name, run, note: note);
        item.Tag = $"arrange:{id}";
        return item;
    }

    // The tool's word with an arrow that says a menu opens.
    static Button Menu(string id, HoverCard card, string off, Control[] entries)
    {
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        var flyout = new MenuFlyout();
        foreach (var entry in entries)
        {
            flyout.Items.Add(entry);
        }

        var button = new Button
        {
            Tag = $"arrange:{id}",
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = card.Title, VerticalAlignment = VerticalAlignment.Center }, chevron } },
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small },
            Flyout = flyout,
        };
        card.SaysWhyOff(button, off);
        return card.On(button);
    }

    static Button Tool(string id, HoverCard card, Action run, string? off = null)
    {
        var button = new Button
        {
            Tag = $"arrange:{id}",
            Content = card.Key is null ? card.Title : Parts.Keyed(card.Title, card.Key),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small },
        };
        button.Click += (_, _) => run();
        if (off is not null)
        {
            card.SaysWhyOff(button, off);
        }

        return card.On(button);
    }

    static StackPanel Row(params Control[] tools)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.AddRange(tools);
        return row;
    }
}
