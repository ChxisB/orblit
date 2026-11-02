using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Stage;

namespace Orblit.Game.Avalonia;

/// <summary>
/// The window a game plays in: the scene drawn through a screen, the keys and the pointer going to the
/// game, and the game's own control over the view when it has one.
/// </summary>
/// <remarks>
/// A throw from the game ends the game and puts the throw on the window in place of the view. That keeps
/// a fault on screen where the person who wrote the game is looking, and it stops the loop asking for
/// frames the game can no longer use.
/// </remarks>
internal sealed class GameWindow : Window
{
    /// <summary>The longest a frame is allowed to count. A stall of seconds is one slow step and not a jump.</summary>
    private const double LongestStep = 0.1;

    private readonly GameHost _host;
    private readonly DocumentView _view;
    private readonly IGameScreen _screen;
    private readonly Func<Control>? _interfaceOf;
    private readonly GameInputSource _input;
    private readonly Panel _stage;
    private Control? _interface;
    private TimeSpan? _last;
    private double _seconds;
    private int _loop;
    private bool _requested;

    public GameWindow(GameHost host, DocumentView view, IGameScreen screen, Func<Control>? interfaceOf = null)
    {
        _host = host;
        _view = view;
        _screen = screen;
        _interfaceOf = interfaceOf;
        _input = new GameInputSource(screen.View);
        _stage = new Panel { Children = { screen.View } };
        Content = _stage;
        host.SceneChanged += view.Apply;
    }

    /// <summary>Seconds since <paramref name="last"/>, none on the first frame, and never more than a tenth.</summary>
    internal static double StepSince(TimeSpan? last, TimeSpan now) =>
        last is { } before ? Math.Clamp((now - before).TotalSeconds, 0, LongestStep) : 0;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _screen.View.Focus();
        _host.Start();
        if (_host.Running && _interfaceOf is not null)
        {
            _host.Guard("CreateInterface", () => _interface = _interfaceOf());
        }

        if (!_host.Running)
        {
            ShowFault();
            return;
        }

        if (_interface is not null)
        {
            _stage.Children.Add(_interface);
        }

        RequestFrame();
    }

    protected override void OnClosed(EventArgs e)
    {
        // A frame already asked for still comes after the window closes. The new loop leaves it unanswered.
        _loop++;
        _requested = false;
        _host.Stop();
        _host.SceneChanged -= _view.Apply;
        _input.Detach();
        _stage.Children.Clear();
        base.OnClosed(e);
    }

    private void RequestFrame()
    {
        if (_requested)
        {
            return;
        }

        _requested = true;
        var loop = _loop;
        RequestAnimationFrame(now =>
        {
            if (loop == _loop)
            {
                OnFrame(now);
            }
        });
    }

    private void OnFrame(TimeSpan now)
    {
        _requested = false;
        var step = StepSince(_last, now);
        _last = now;
        _seconds += step;

        _host.Update(_input.Read(_view.Camera), step);
        _input.EndFrame();
        if (!_host.Running)
        {
            ShowFault();
            return;
        }

        _screen.Draw(_view.Scene, _seconds);
        RequestFrame();
    }

    private void ShowFault()
    {
        if (_host.Fault is not { } fault)
        {
            return;
        }

        _stage.Children.Clear();
        Content = new TextBlock
        {
            Name = "Fault",
            Text = fault.ToString(),
            Margin = new Thickness(16),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
        };
    }
}
