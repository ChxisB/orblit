using Avalonia.Controls;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Goto menu of the Graph workspace. It finds a step and walks the bookmarks of the script on show, and sets a
/// breakpoint on the selected step. Each command is one the find bar already has, so the menu adds no logic of its own.
/// </summary>
internal sealed class GotoMenu : MenuFlyout
{
    readonly Func<GraphCanvas?> _canvas;

    public GotoMenu(Func<GraphCanvas?> canvas)
    {
        _canvas = canvas;
        Items.Add(Entry("Find Step...", bar => bar.Open()));
        Items.Add(Entry("Go to Step...", bar => bar.OpenGoto()));
        Items.Add(new Separator());
        Items.Add(Entry("Toggle Bookmark", bar => bar.Run(search => search.ToggleBookmark())));
        Items.Add(Entry("Next Bookmark", bar => bar.Run(search => search.StepBookmark(true))));
        Items.Add(Entry("Previous Bookmark", bar => bar.Run(search => search.StepBookmark(false))));
        Items.Add(new Separator());
        Items.Add(Entry("Toggle Breakpoint", bar => bar.Run(search => search.ToggleBreakpoint())));
    }

    /// <summary>Turns the commands on while a script is open, since each one acts on its canvas.</summary>
    public void Show(bool scriptOpen)
    {
        foreach (var item in Items.OfType<MenuItem>())
        {
            item.IsEnabled = scriptOpen;
        }
    }

    MenuItem Entry(string header, Action<FindBar> command)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            if (_canvas() is { } canvas)
            {
                command(canvas.FindBar);
            }
        };
        return item;
    }
}
