using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Debugger panel's note. The graph debugger lives in the Graph workspace, so this says whether a game is
/// running for it to join, and offers to run one.
/// </summary>
internal sealed class DebuggerNote : Decorator
{
    readonly Button _run = new()
    {
        Classes = { EditorTheme.TextButton, EditorTheme.Small },
        HorizontalAlignment = HorizontalAlignment.Center,
        IsEnabled = false,
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new EditorIcon("MainPlay") { Width = 13, Height = 13, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "Run the project", VerticalAlignment = VerticalAlignment.Center },
            },
        },
    };

    public DebuggerNote()
    {
        var note = Parts.EmptyState("Debug", "", "");
        Title = (TextBlock)note.Children[1];
        Line = (TextBlock)note.Children[2];
        note.Children.Add(_run);
        _run.Click += (_, _) => RunRequested?.Invoke();
        Child = note;
        ShowGame(false, false);
    }

    /// <summary>Raised when Run the project is pressed. The note has not changed, the window shows the new state.</summary>
    public event Action? RunRequested;

    internal TextBlock Title { get; }

    internal TextBlock Line { get; }

    internal Button RunButton => _run;

    /// <summary>Shows whether a game is running and whether a scene is open to run.</summary>
    public void ShowGame(bool playing, bool canRun)
    {
        Title.Text = playing ? "The game is running" : "No game is connected";
        Line.Text = playing ? "Graph scripts stop at their breakpoints in the Graph workspace." : "Run the project and the debugger joins it.";
        _run.IsVisible = !playing;
        _run.IsEnabled = canRun;
    }
}
