using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.Layout.Checks;

/// <summary>One broken layout for each rule of the audit, so a rule that stops finding its fault fails the run.</summary>
internal static class SelfCheck
{
    // The rule, the words its fault holds, and a layout that has the fault.
    static readonly (string Rule, string Mark, Func<Control> Broken)[] s_cases =
    [
        ("past its box", "runs ", () => new Border { Width = 50, Height = 20, Child = Box(80) }),
        ("cut by a clip", " by ", () => new ScrollViewer { Width = 50, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = Box(80) }),
        ("cut words", "cut short", () => new TextBlock { Width = 40, Text = "More words than fit", TextTrimming = TextTrimming.CharacterEllipsis }),
        ("overlap", "sits on", () => new StackPanel { Children = { Box(20), Box(20, new Thickness(0, -10, 0, 0)) } }),
        ("tight", "needs 4", () => new StackPanel { Children = { new Button { Content = "One" }, new Button { Content = "Two" } } }),
        ("small icon", "icon drawn small", () => new Border { Width = 10, Child = new EditorIcon("Add") }),
    ];

    public static IEnumerable<string> Missed() => s_cases.Where(one => !Finds(one.Broken(), one.Mark)).Select(one => one.Rule);

    static Border Box(double width, Thickness margin = default) =>
        new() { Width = width, Height = 20, Margin = margin, HorizontalAlignment = HorizontalAlignment.Left };

    static bool Finds(Control broken, string mark)
    {
        var window = new Window { Width = 200, Height = 200, Content = broken };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var found = LayoutAudit.Faults(window).Any(fault => fault.Contains(mark, StringComparison.Ordinal));
        window.Close();
        return found;
    }
}
