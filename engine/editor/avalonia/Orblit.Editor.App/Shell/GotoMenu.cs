using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Go to menu of the Graph workspace. It finds a step and walks the bookmarks of the graph on show, and pauses
/// the game on the selected step. Each command is one the find bar already has, so the menu adds no logic of its own.
/// </summary>
internal sealed class GotoMenu : MenuFlyout
{
    const string NoGraph = "Open a graph first.";

    readonly Func<GraphCanvas?> _canvas;
    readonly List<(MenuItem Item, string? Note)> _entries = [];

    public GotoMenu(Func<GraphCanvas?> canvas, KeyModifiers command)
    {
        _canvas = canvas;
        Add("Find a step…", "By name, like print, or by number", new KeyGesture(Key.F, command), bar => bar.Open());
        Add("Go to step number…", "Every step shows its number", new KeyGesture(Key.L, command), bar => bar.OpenGoto());
        Items.Add(new Separator());
        Add("Bookmark this step", "Marks a step to come back to", new KeyGesture(Key.F2, command), bar => bar.Run(search => search.ToggleBookmark()));
        Add("Next bookmark", null, new KeyGesture(Key.F2), bar => bar.Run(search => search.StepBookmark(true)));
        Add("Previous bookmark", null, new KeyGesture(Key.F2, KeyModifiers.Shift), bar => bar.Run(search => search.StepBookmark(false)));
        Items.Add(new Separator());
        Add("Pause here", "The game stops at this step so you can look around", new KeyGesture(Key.F9), bar => bar.Run(search => search.ToggleBreakpoint()));
    }

    /// <summary>Turns the commands on while a graph is open, since each one acts on its canvas. Off, each says so.</summary>
    public void Show(bool scriptOpen)
    {
        foreach (var (item, note) in _entries)
        {
            item.IsEnabled = scriptOpen;
            item.Header = Parts.Noted(AutomationProperties.GetName(item) ?? "", scriptOpen ? note : NoGraph);
        }
    }

    void Add(string header, string? note, KeyGesture key, Action<FindBar> command)
    {
        var item = Parts.ActionItem(header, () =>
        {
            if (_canvas() is { } canvas)
            {
                command(canvas.FindBar);
            }
        }, key, note);
        _entries.Add((item, note));
        Items.Add(item);
    }
}
