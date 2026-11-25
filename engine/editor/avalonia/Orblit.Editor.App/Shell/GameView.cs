using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Game workspace in four islands: the play settings, the game, its speed and its live scene. The game draws
/// in a window of its own, so the middle says where it is and offers what to do next. Its log is in the Output
/// panel. The Show while playing boxes go to the game as engine flags, and every setting counts from the next
/// Play. The speed rows and the live scene show what the game last sent. The view only shows what it is given and
/// asks the window to play or stop the game.
/// </summary>
internal sealed class GameView : Border
{
    const string Unknown = "—";

    // The settings the engine takes as flags, by the engine names the Play menu ticks them by.
    static readonly (string Engine, string Name, string Line, string Flag)[] s_shown =
    [
        ("Visible Collision Shapes", "Collision shapes", "The shapes things bump into", "--debug-collisions"),
        ("Visible Paths", "Paths", "Lines that things follow", "--debug-paths"),
        ("Visible Navigation", "Navigation", "Where characters can walk", "--debug-navigation"),
        ("Visible Avoidance", "Avoidance", "How characters steer round each other", "--debug-avoidance"),
        ("Debug CanvasItem Redraws", "2D redraws", "Flashes each 2D thing as it redraws", "--debug-canvas-item-redraw"),
    ];

    const double Mebibyte = 1024 * 1024;

    // Under this many frames a second a game feels slow.
    const double SlowRate = 30;

    // Over this many seconds a frame the game falls below 60 frames a second.
    const double FrameBudget = 1.0 / 60;

    // Each reading by its plain name, the engine's name where it differs, and what it measures. The game sends
    // seconds and bytes, and each value shows in the unit people read it in. A reading past its good range
    // gives the line that says where to look next, in place of what it measures.
    static readonly (string Name, string? Engine, string Line, Func<DebugMonitors, string> Value, Func<DebugMonitors, Slow?> Slow)[] s_monitors =
    [
        ("Frames a second", "FPS", "60 or more feels smooth.", game => Number(game.Fps, "0"), game => game.Fps is > 0 and < SlowRate ? new("Under 30 feels slow. Time per frame shows why.", EditorBrushes.FaultText) : null),
        ("Time per frame", "Frame time", "Under 16.7 ms keeps 60 a second.", game => game.Fps > 0 ? Ms(1 / game.Fps) : Unknown, game => SlowFrame(game) ? new($"Over 16.7 ms. {(game.Process >= game.Physics ? "Game code" : "Physics")} takes most of it.", EditorBrushes.Warn) : null),
        ("Game code", "Process", "What your scripts and graphs take each frame.", game => Ms(game.Process), game => SlowFrame(game) && game.Process >= game.Physics ? new("Look at what runs Every frame.", EditorBrushes.Warn) : null),
        ("Physics", null, "Moving and bumping things.", game => Ms(game.Physics), game => SlowFrame(game) && game.Physics > game.Process ? new("Fewer moving things or simpler shapes help.", EditorBrushes.Warn) : null),
        ("Draw calls", null, "Fewer is faster.", game => Number(game.DrawCalls, "0"), _ => null),
        ("Things", "Objects", "Everything that exists in the game now.", game => Number(game.Objects, "0"), _ => null),
        ("Video memory", null, "Pictures and models on the graphics card.", game => $"{Number(game.VideoMemory / Mebibyte, "0")} MB", _ => null),
    ];

    const double DepthIndent = 16;

    readonly Ellipse _dot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { FontSize = 12, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _pill = new() { CornerRadius = new CornerRadius(999), Padding = new Thickness(9, 3), Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _project = new() { FontSize = 12, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _time = new() { FontSize = 12, FontFamily = EditorTheme.Mono, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _run;
    readonly Button _play;
    readonly Button _playScene;
    readonly Button _stop;
    readonly StackPanel _stopped = Parts.EmptyState("Window", "The game opens in its own window", "Press Play to start it. Its speed shows on the right and its things show in Live scene.");
    readonly StackPanel _playing = Parts.EmptyState("Window", "Playing in its own window", "It may be behind this one. Stop closes it.");
    readonly TextBlock _state = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly RadioButton _main = Choice("Main scene", "The first scene players see.");
    readonly RadioButton _thisScene = Choice("This scene", "The one open in the 3D view");
    readonly CheckBox[] _shown = [.. s_shown.Select(row => new HoverCard(row.Name, row.Line, engine: row.Engine).On(new CheckBox { Content = Parts.Noted(row.Name, row.Line) }))];
    readonly StackPanel _settings = new() { Spacing = 4, Margin = new Thickness(10, 4, 10, 12) };
    readonly StackPanel _waits;
    readonly TextBlock _speedHint = new() { Text = "Numbers show while the game plays.", FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(12, 10, 12, 2) };
    readonly TextBlock[] _values = [.. s_monitors.Select(_ => new TextBlock { Text = Unknown, FontSize = 12, FontFamily = EditorTheme.Mono, VerticalAlignment = VerticalAlignment.Top })];
    readonly TextBlock[] _lines = [.. s_monitors.Select(row => new TextBlock { Text = row.Line, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap })];
    readonly StackPanel _live = new() { Margin = new Thickness(6, 4, 6, 10) };
    readonly StackPanel _noScene = Parts.EmptyState("Tree", "Nothing running", "The things in the running game show here, so you can check them while it plays.");
    readonly StackPanel _liveMark;
    IReadOnlyList<LiveEntity> _shownScene = [];

    public GameView(KeyModifiers command)
    {
        string Keys(Key key) => Parts.KeyText(new KeyGesture(key, command));

        _run = PlayButton(Keys(Key.B));
        _play = PlayButton(Keys(Key.B));
        _playScene = Keyed("Play this scene", "Starts the scene open in the 3D view, in its own window.", Keys(Key.R), EditorTheme.Ghost);
        _stop = Keyed("Stop", "Closes the game window. Your scene stays as it was.", Keys(Key.OemPeriod), EditorTheme.Ghost);
        _pill.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _dot, _status } };
        _run.Click += (_, _) => RunRequested?.Invoke();
        _play.Click += (_, _) => RunRequested?.Invoke();
        _playScene.Click += (_, _) => RunSceneRequested?.Invoke();
        _stop.Click += (_, _) => StopRequested?.Invoke();
        _main.IsChecked = true;
        var thisScene = new HoverCard("This scene", "Play runs the scene open in the 3D view.");
        thisScene.SaysWhyOff(_thisScene, "Open a saved scene first.");
        thisScene.On(_thisScene);
        _stopped.Children.Add(Buttons(_play, _playScene));
        _playing.Children.Add(Buttons(_stop));
        _waits = Note("Stop the game to change these. They count from the next Play.");
        _liveMark = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Children = { new Ellipse { Width = 6, Height = 6, Fill = EditorBrushes.Done, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Live", FontSize = 11, Foreground = EditorBrushes.Done } } };
        var side = new Grid { RowDefinitions = new RowDefinitions("Auto,6,*"), Children = { Island(Speed()), At(Island(LiveScene()), 0, 2) } };
        Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("288,6,*,6,300"),
            Children = { At(Island(PlaySettings()), 0), At(Island(TheGame()), 2), At(side, 4) },
        };
        Show(false, "", TimeSpan.Zero);
        Offer(false, false);
        ShowLive(DebugState.None);
    }

    /// <summary>Raised when a Play is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? RunRequested;

    /// <summary>Raised when Play this scene is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? RunSceneRequested;

    /// <summary>Raised when Stop is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? StopRequested;

    /// <summary>The Play on the bar. The one under the middle's title raises the same request.</summary>
    internal Button RunButton => _run;

    internal Button PlayButtonBelow => _play;

    internal Button PlaySceneButton => _playScene;

    internal Button StopButton => _stop;

    internal RadioButton MainChoice => _main;

    internal RadioButton ThisSceneChoice => _thisScene;

    /// <summary>Whether Play runs the scene open in the 3D view in place of the main scene.</summary>
    internal bool RunsThisScene => _thisScene.IsChecked == true;

    internal Control Settings => _settings;

    internal Control WaitsNote => _waits;

    internal Control Stopped => _stopped;

    internal Control Playing => _playing;

    internal TextBlock State => _state;

    internal TextBlock Status => _status;

    internal TextBlock Project => _project;

    internal TextBlock Time => _time;

    internal Control SpeedHint => _speedHint;

    internal Control LiveMark => _liveMark;

    /// <summary>The value of each speed row, in the order of <see cref="MonitorNames"/>.</summary>
    internal IEnumerable<string> MonitorValues => _values.Select(value => value.Text ?? "");

    internal static IEnumerable<string> MonitorNames => s_monitors.Select(row => row.Name);

    /// <summary>The value and the line of the speed row with this plain name, one of <see cref="MonitorNames"/>.</summary>
    internal (TextBlock Value, TextBlock Line) ReadingOf(string name)
    {
        var at = Array.FindIndex(s_monitors, row => row.Name == name);
        return (_values[at], _lines[at]);
    }

    /// <summary>The rows of the live scene, one for each entity of the running game.</summary>
    internal IEnumerable<Control> LiveRows => _live.Children;

    internal Control NoScene => _noScene;

    /// <summary>The engine names of the settings that are engine flags, in the order of their rows.</summary>
    internal static IEnumerable<string> FlagNames => s_shown.Select(row => row.Engine);

    /// <summary>The engine flags of the settings that are on, in the order of their rows.</summary>
    internal IEnumerable<string> Flags => s_shown.Where((_, at) => _shown[at].IsChecked == true).Select(row => row.Flag);

    /// <summary>The box of the setting with this engine name, one of <see cref="FlagNames"/>.</summary>
    internal CheckBox SwitchOf(string name) => _shown[Array.FindIndex(s_shown, row => row.Engine == name)];

    /// <summary>Shows whether a game plays, the project's name and how long the game has played.</summary>
    public void Show(bool playing, string project, TimeSpan elapsed)
    {
        _run.IsVisible = !playing;
        _stopped.IsVisible = !playing;
        _playing.IsVisible = playing;
        _stop.IsEnabled = playing;
        _settings.IsEnabled = !playing;
        _waits.IsVisible = playing;
        _status.Text = playing ? "Playing" : "Stopped";
        _status.Foreground = playing ? EditorBrushes.Done : EditorBrushes.TextDim;
        _dot.Fill = _status.Foreground;
        _pill.Background = playing ? EditorBrushes.DoneSoft : EditorBrushes.Line;
        _project.Text = project;
        _time.Text = playing ? elapsed.ToString(elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture) : "";
        _state.Text = playing ? "Changes to the scene now count from the next Play." : "Play settings on the left apply the next time you press Play.";
    }

    /// <summary>
    /// Turns Play on while a project is open, and Play this scene while a saved scene is open. With no saved scene
    /// Play runs the main scene, and the choice says so.
    /// </summary>
    public void Offer(bool canRun, bool canRunScene)
    {
        _run.IsEnabled = canRun;
        _play.IsEnabled = canRun;
        _playScene.IsEnabled = canRunScene;
        _thisScene.IsEnabled = canRunScene;
        if (!canRunScene && RunsThisScene)
        {
            _main.IsChecked = true;
        }
    }

    /// <summary>Shows the readings and the tree the running game last sent. A game that sent none shows dashes and no rows.</summary>
    public void ShowLive(DebugState state)
    {
        _speedHint.IsVisible = state.Monitors is null;
        for (var at = 0; at < _values.Length; at++)
        {
            ShowReading(at, state.Monitors);
        }

        // The state comes again each second with new readings, so the rows are made again only when the tree changes.
        if (!state.Scene.SequenceEqual(_shownScene))
        {
            _shownScene = state.Scene;
            _live.Children.Clear();
            _live.Children.AddRange(state.Scene.Select(LiveRow));
        }

        _noScene.IsVisible = state.Scene.Count == 0;
        _liveMark.IsVisible = !_noScene.IsVisible;
    }

    void ShowReading(int at, DebugMonitors? game)
    {
        var slow = game is null ? null : s_monitors[at].Slow(game);
        _values[at].Text = game is null ? Unknown : s_monitors[at].Value(game);
        _lines[at].Text = slow?.Line ?? s_monitors[at].Line;
        if (slow is null)
        {
            _values[at].ClearValue(TextBlock.ForegroundProperty);
            return;
        }

        _values[at].Foreground = slow.Tone;
    }

    static bool SlowFrame(DebugMonitors game) => game.Fps > 0 && 1 / game.Fps > FrameBudget;

    DockPanel PlaySettings()
    {
        _settings.Children.Add(Heading("What Play runs"));
        _settings.Children.AddRange([_main, _thisScene, Heading("Show while playing"), .. _shown]);
        var body = new StackPanel { Children = { _waits, _settings } };
        return Stack(Parts.Bar(Title("Play settings")), new ScrollViewer { Content = body });
    }

    DockPanel TheGame()
    {
        var bar = Parts.Bar(_pill, _project, _time, Parts.Right(_run));
        var footer = new Border
        {
            Height = 30,
            Padding = new Thickness(12, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _state,
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var stage = new Border
        {
            Background = EditorBrushes.Window,
            Child = new StackPanel { MaxWidth = 380, VerticalAlignment = VerticalAlignment.Center, Children = { _stopped, _playing } },
        };
        return Stack(bar, footer, stage);
    }

    DockPanel Speed()
    {
        var rows = new StackPanel { Margin = new Thickness(0, 4, 0, 8), Children = { _speedHint } };
        rows.Children.AddRange(s_monitors.Select((_, at) => Reading(at)));
        new HoverCard("Frames a second", "How many pictures the game draws each second. Under 30 feels slow. 60 or more feels smooth.", engine: "FPS monitor").On(rows.Children[1]);
        return Stack(Parts.Bar(Title("Speed")), rows);
    }

    DockPanel LiveScene() => Stack(Parts.Bar(Title("Live scene"), Parts.Right(_liveMark)), new ScrollViewer { Content = new StackPanel { Children = { _live, _noScene } } });

    // The plain name with the engine's name beside it, the line under them, and the value at the right.
    Grid Reading(int at)
    {
        var words = Parts.Noted(s_monitors[at].Name, null, s_monitors[at].Engine);
        words.Children.Add(_lines[at]);
        return new Grid
        {
            Margin = new Thickness(12, 5),
            Background = Brushes.Transparent,
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { words, At(_values[at], 1) },
        };
    }

    // The last control fills what the ones before it leave.
    static DockPanel Stack(params Control[] parts)
    {
        var panel = new DockPanel();
        foreach (var part in parts[..^1].Where(part => !part.IsSet(DockPanel.DockProperty)))
        {
            DockPanel.SetDock(part, Dock.Top);
        }
        panel.Children.AddRange(parts);
        return panel;
    }

    static Border Island(Control child) => new() { Classes = { EditorTheme.Island }, ClipToBounds = true, Child = child };

    static T At<T>(T control, int column, int row = 0) where T : Control
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        return control;
    }

    static Grid LiveRow(LiveEntity entity)
    {
        var plain = PlainNames.NameOf(entity.Type);
        var words = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = entity.Name, FontSize = 12 } } };
        if (plain != entity.Name)
        {
            words.Children.Add(new TextBlock { Text = plain, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center });
        }

        var row = new Grid
        {
            Height = 26,
            Margin = new Thickness(6 + (entity.Depth * DepthIndent), 0, 0, 0),
            Background = Brushes.Transparent,
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children = { Parts.RowIcon(EditorIcons.ForClass(entity.Type)), At(words, 1) },
        };
        return new HoverCard(plain, PlainNames.Of(entity.Type)?.Line ?? "A thing in the running game.", engine: entity.Type).On(row);
    }

    // A radio choice drawn as a card, with its name over a dim line.
    static RadioButton Choice(string name, string line) => new CardChoice { Classes = { EditorTheme.TextButton, EditorTheme.Card }, Content = Parts.Noted(name, line) };

    static Button PlayButton(string key)
    {
        var button = Keyed("Play", "Starts the scene Play settings pick, in its own window. Press again to start over.", key, EditorTheme.Primary);
        ((HoverCard)ToolTip.GetTip(button)!).SaysWhyOff(button, "Open a project first.");
        return button;
    }

    static Button Keyed(string word, string line, string key, string look) =>
        new HoverCard(word, line, key).On(new Button { Classes = { EditorTheme.TextButton, EditorTheme.Small, look }, VerticalAlignment = VerticalAlignment.Center, Content = Parts.Keyed(word, key) });

    static StackPanel Buttons(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        row.Children.AddRange(buttons);
        return row;
    }

    static StackPanel Note(string text)
    {
        var mark = new EditorIcon("NodeInfo") { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Top };
        TextElement.SetForeground(mark, EditorBrushes.TextDim);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(10, 10, 10, 0),
            Children = { mark, new TextBlock { Text = text, FontSize = 11, Foreground = EditorBrushes.TextSoft, TextWrapping = TextWrapping.Wrap, MaxWidth = 240 } },
        };
    }

    static TextBlock Heading(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10.5,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 0.6,
        Foreground = EditorBrushes.Unit,
        Margin = new Thickness(2, 10, 0, 2),
    };

    static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    static string Ms(double seconds) => $"{Number(seconds * 1000, "0.0")} ms";

    static TextBlock Title(string text) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    // A reading past its good range: where to look next, and the tone its value takes.
    sealed record Slow(string Line, IBrush Tone);

    // A radio choice drawn as a worded button, so the cards share one look and a check moves between them.
    sealed class CardChoice : RadioButton
    {
        protected override Type StyleKeyOverride => typeof(Button);
    }
}
