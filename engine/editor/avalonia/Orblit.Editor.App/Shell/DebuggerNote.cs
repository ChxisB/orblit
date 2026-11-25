using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Debugger panel's note. The graph debugger lives in the Graph workspace, so this says whether a game is
/// playing for it to join, and offers to play one. A game that runs under netcoredbg shows its C# session here
/// instead. Without netcoredbg, the note says where to get it, and shows the process id for an outside IDE.
/// </summary>
internal sealed class DebuggerNote : Decorator
{
    readonly Button _run = new()
    {
        Classes = { EditorTheme.TextButton, EditorTheme.Small },
        HorizontalAlignment = HorizontalAlignment.Center,
        IsEnabled = false,
    };

    readonly StackPanel _note;
    readonly Button _copy = new() { Content = "Copy Process ID", HorizontalAlignment = HorizontalAlignment.Center, Classes = { EditorTheme.TextButton, EditorTheme.Small } };
    int? _game;

    public DebuggerNote(KeyModifiers command)
    {
        var card = new HoverCard(Play, "Starts from the main scene.", Parts.KeyText(new KeyGesture(Key.B, command)));
        var content = Parts.Keyed(card.Title, card.Key!);
        content.Children.Insert(0, new EditorIcon("MainPlay") { Width = 13, Height = 13, VerticalAlignment = VerticalAlignment.Center });
        _run.Content = content;
        card.SaysWhyOff(card.On(_run), "Open a scene first.");
        _note = Parts.EmptyState("Debug", "", "");
        Title = (TextBlock)_note.Children[1];
        Line = (TextBlock)_note.Children[2];
        _note.Children.Add(_run);
        _note.Children.Add(_copy);
        _run.Click += (_, _) => RunRequested?.Invoke();
        _copy.Click += (_, _) => CopyRequested?.Invoke($"{_game}");
        Child = _note;
        ShowGame(null, false);
    }

    /// <summary>Raised when Play the game is pressed. The note has not changed, the window shows the new state.</summary>
    public event Action? RunRequested;

    /// <summary>Raised with the running game's process id when Copy Process ID is pressed.</summary>
    public event Action<string>? CopyRequested;

    internal const string Play = "Play the game";

    internal TextBlock Title { get; }

    internal TextBlock Line { get; }

    internal Button RunButton => _run;

    internal Button CopyButton => _copy;

    /// <summary>Shows the running game's process id, or that none runs, and whether a scene is open to run.</summary>
    public void ShowGame(int? game, bool canRun)
    {
        _game = game;
        Title.Text = game is null ? "No game is playing" : "The game is running";
        Line.Text = game is null ? "Press Play and the debugger joins in. It shows errors, pauses and the value of everything while the game runs."
            : "Graph scripts stop at their breakpoints in the Graph workspace. C# stops at its breakpoints when netcoredbg is on "
                + $"PATH or {Netcoredbg.Setting} names it. Get it from {Netcoredbg.Releases}. Or attach VS Code or Rider to process {game}.";
        _run.IsVisible = game is null;
        _run.IsEnabled = canRun;
        _copy.IsVisible = game is not null;
    }

    /// <summary>The scroller a debug session sits in, so locals below the panel's height can be reached.</summary>
    internal ScrollViewer Session { get; } = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

    /// <summary>Shows a debug session in place of the note, or the note again for null.</summary>
    public void ShowSession(Control? session)
    {
        Session.Content = session;
        Child = session is null ? _note : Session;
    }
}
