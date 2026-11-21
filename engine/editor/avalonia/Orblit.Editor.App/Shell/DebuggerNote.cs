using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Debugger panel's note. The graph debugger lives in the Graph workspace, so this says whether a game is
/// running for it to join, and offers to run one. C# runs in .NET, so a running game's process id is shown for an
/// outside IDE to attach to.
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

    readonly Button _copy = new() { Content = "Copy Process ID", HorizontalAlignment = HorizontalAlignment.Center, Classes = { EditorTheme.TextButton, EditorTheme.Small } };
    int? _game;

    public DebuggerNote()
    {
        var note = Parts.EmptyState("Debug", "", "");
        Title = (TextBlock)note.Children[1];
        Line = (TextBlock)note.Children[2];
        note.Children.Add(_run);
        note.Children.Add(_copy);
        _run.Click += (_, _) => RunRequested?.Invoke();
        _copy.Click += (_, _) => CopyRequested?.Invoke($"{_game}");
        Child = note;
        ShowGame(null, false);
    }

    /// <summary>Raised when Run the project is pressed. The note has not changed, the window shows the new state.</summary>
    public event Action? RunRequested;

    /// <summary>Raised with the running game's process id when Copy Process ID is pressed.</summary>
    public event Action<string>? CopyRequested;

    internal TextBlock Title { get; }

    internal TextBlock Line { get; }

    internal Button RunButton => _run;

    internal Button CopyButton => _copy;

    /// <summary>Shows the running game's process id, or that none runs, and whether a scene is open to run.</summary>
    public void ShowGame(int? game, bool canRun)
    {
        _game = game;
        Title.Text = game is null ? "No game is connected" : "The game is running";
        Line.Text = game is null ? "Run the project and the debugger joins it."
            : $"Graph scripts stop at their breakpoints in the Graph workspace. To debug C#, attach VS Code or Rider to process {game}.";
        _run.IsVisible = game is null;
        _run.IsEnabled = canRun;
        _copy.IsVisible = game is not null;
    }
}
