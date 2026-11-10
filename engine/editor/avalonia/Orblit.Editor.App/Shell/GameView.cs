using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Game workspace: whether the game is running, with Run and Stop. The game draws in a window of its own, so
/// the view shows no frames. Its log is in the Output panel. It only shows what it is given and asks the window to
/// run or stop the game.
/// </summary>
internal sealed class GameView : Border
{
    readonly TextBlock _state = new() { Foreground = EditorBrushes.TabInactive };
    readonly Button _run = new() { Content = "Run", Padding = new Thickness(12, 4), MinHeight = 0 };
    readonly Button _stop = new() { Content = "Stop", Padding = new Thickness(12, 4), MinHeight = 0 };

    public GameView()
    {
        Background = EditorBrushes.Panel;
        _run.Click += (_, _) => RunRequested?.Invoke();
        _stop.Click += (_, _) => StopRequested?.Invoke();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_run);
        buttons.Children.Add(_stop);
        var panel = new StackPanel { Margin = new Thickness(12, 8), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Game", FontWeight = FontWeight.Bold });
        panel.Children.Add(buttons);
        panel.Children.Add(_state);
        Child = panel;
        Show(false, false);
    }

    /// <summary>Raised when Run is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? RunRequested;

    /// <summary>Raised when Stop is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? StopRequested;

    internal Button RunButton => _run;

    internal Button StopButton => _stop;

    internal TextBlock State => _state;

    /// <summary>Shows whether a game runs and whether a project is open to run.</summary>
    public void Show(bool playing, bool canRun)
    {
        _run.IsEnabled = canRun;
        _stop.IsEnabled = playing;
        _state.Text = playing ? "The game is running in its own window." : "No game is running. Run opens one in its own window.";
    }
}
