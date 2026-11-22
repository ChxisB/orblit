using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Game workspace in three islands: the run settings, the running game and its monitors. The game draws in a
/// window of its own, so the middle shows its state and no frames. Its log is in the Output panel. The Show while
/// running switches go to the game as engine flags. The other settings wait on the game link, so they show what
/// they will hold and take no input. The monitors and the live scene show what the game last sent. The view only
/// shows what it is given and asks the window to run or stop the game.
/// </summary>
internal sealed class GameView : Border
{
    const string Unknown = "—";

    // The run settings the engine takes as flags. The Debug menu ticks the same switches.
    static readonly (string Name, string Flag)[] s_shown =
    [
        ("Visible Collision Shapes", "--debug-collisions"),
        ("Visible Paths", "--debug-paths"),
        ("Visible Navigation", "--debug-navigation"),
        ("Visible Avoidance", "--debug-avoidance"),
        ("Debug CanvasItem Redraws", "--debug-canvas-item-redraw"),
    ];

    // The other groups of run settings, each row with whether it starts on. They wait on the game link.
    static readonly (string Group, (string Name, bool On)[] Rows)[] s_waiting =
    [
        ("Live edits", [("Synchronize Scene Changes", true), ("Synchronize Script Changes", true), ("Keep Debug Server Open", false)]),
        ("Deploy", [("Deploy with Remote Debug", false), ("Small Deploy with Network Filesystem", false)]),
    ];

    const double Mebibyte = 1024 * 1024;

    // Each row's value in the unit it shows. The game sends seconds and bytes.
    static readonly (string Name, string Unit, Func<DebugMonitors, string> Value)[] s_monitors =
    [
        ("Frame time", "ms", game => game.Fps > 0 ? Number(1000 / game.Fps, "0.0") : Unknown),
        ("Process", "ms", game => Number(game.Process * 1000, "0.0")),
        ("Physics", "ms", game => Number(game.Physics * 1000, "0.0")),
        ("Draw calls", "", game => Number(game.DrawCalls, "0")),
        ("Objects", "", game => Number(game.Objects, "0")),
        ("Video memory", "MB", game => Number(game.VideoMemory / Mebibyte, "0")),
    ];

    const double DepthIndent = 16;

    readonly Ellipse _dot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { FontSize = 12, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _pill = new() { CornerRadius = new CornerRadius(999), Padding = new Thickness(9, 3), Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _project = new() { FontSize = 12, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _time = new() { FontSize = 12, FontFamily = EditorTheme.Mono, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _run = Worded("MainPlay", "Run");
    readonly Button _pause = Worded("Pause", "Pause");
    readonly Button _stop = Worded("Stop", "Stop");
    readonly Button _front = Worded("Window", "Bring to front");
    readonly TextBlock _state = new() { FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly ToggleSwitch[] _shown = [.. s_shown.Select(_ => Switch(false))];
    readonly StackPanel _choices = new() { Spacing = 10, Margin = new Thickness(12) };
    readonly StackPanel _waiting = new();
    readonly StackPanel _monitors = new();
    readonly TextBlock _rate = new() { Text = Unknown, FontSize = 30, FontWeight = FontWeight.Medium, FontFamily = EditorTheme.Mono };
    readonly TextBlock[] _values = [.. s_monitors.Select(_ => new TextBlock { Text = Unknown, FontSize = 12, FontFamily = EditorTheme.Mono, VerticalAlignment = VerticalAlignment.Center })];
    readonly StackPanel _live = new() { Margin = new Thickness(6, 0, 6, 10) };
    readonly StackPanel _noScene = Parts.EmptyState("Tree", "No live scene", "Run the game to see its entities here.");
    readonly Border _liveBadge = Parts.Badge("Live");
    readonly Border[] _tabs = [Tab("Monitors"), Tab("Scene")];
    IReadOnlyList<LiveEntity> _shownScene = [];

    public GameView()
    {
        _pill.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _dot, _status } };
        _front.Classes.Add(EditorTheme.Ghost);
        foreach (var unbuilt in new Control[] { _pause, _front, _choices, _waiting })
        {
            Unbuilt.Disable(unbuilt);
        }
        _run.Click += (_, _) => RunRequested?.Invoke();
        _stop.Click += (_, _) => StopRequested?.Invoke();
        Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("288,6,*,6,300"),
            Children = { Island(RunSettings(), 0), Island(RunningGame(), 2), Island(Monitors(), 4) },
        };
        Show(false, false, "", TimeSpan.Zero);
        ShowLive(DebugState.None);
        ShowTab(0);
    }

    /// <summary>Raised when Run is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? RunRequested;

    /// <summary>Raised when Stop is pressed. The view has not changed, the window shows the new state.</summary>
    public event Action? StopRequested;

    internal Button RunButton => _run;

    internal Button PauseButton => _pause;

    internal Button StopButton => _stop;

    internal TextBlock State => _state;

    internal TextBlock Status => _status;

    internal TextBlock Project => _project;

    internal TextBlock Time => _time;

    /// <summary>The run settings past Show while running, which take no input yet.</summary>
    internal IEnumerable<Control> Unbuilts => [_choices, _waiting];

    internal TextBlock Rate => _rate;

    /// <summary>The value of each monitor row, in the order of <see cref="MonitorNames"/>.</summary>
    internal IEnumerable<string> MonitorValues => _values.Select(value => value.Text ?? "");

    internal static IEnumerable<string> MonitorNames => s_monitors.Select(row => row.Name);

    /// <summary>The section of the monitors, which the Scene tab hides.</summary>
    internal Control MonitorList => _monitors;

    /// <summary>The rows of the live scene, one for each entity of the running game.</summary>
    internal IEnumerable<Control> LiveRows => _live.Children;

    internal Control NoScene => _noScene;

    internal IReadOnlyList<Border> Tabs => _tabs;

    /// <summary>The names of the run settings that are engine flags, in the order of their rows.</summary>
    internal static IEnumerable<string> FlagNames => s_shown.Select(row => row.Name);

    /// <summary>The engine flags of the run settings that are on, in the order of their rows.</summary>
    internal IEnumerable<string> Flags => s_shown.Where((_, at) => _shown[at].IsChecked == true).Select(row => row.Flag);

    /// <summary>The switch of the run setting with this name, one of <see cref="FlagNames"/>.</summary>
    internal ToggleSwitch SwitchOf(string name) => _shown[Array.FindIndex(s_shown, row => row.Name == name)];

    /// <summary>Shows whether a game runs, whether a project is open to run, the project's name and how long the game has run.</summary>
    public void Show(bool playing, bool canRun, string project, TimeSpan elapsed)
    {
        _run.IsEnabled = canRun;
        _run.IsVisible = !playing;
        _stop.IsEnabled = playing;
        _stop.IsVisible = playing;
        _pause.IsVisible = playing;
        _status.Text = playing ? "Running" : "Stopped";
        _status.Foreground = playing ? EditorBrushes.Done : EditorBrushes.TextDim;
        _dot.Fill = _status.Foreground;
        _pill.Background = playing ? EditorBrushes.DoneSoft : EditorBrushes.Line;
        _project.Text = project;
        _time.Text = playing ? elapsed.ToString(elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture) : "";
        _state.Text = playing ? "The game is running in its own window." : "No game is running. Run opens one in its own window.";
    }

    /// <summary>Shows the monitors and the tree the running game last sent. A game that sent none shows dashes and no rows.</summary>
    public void ShowLive(DebugState state)
    {
        _rate.Text = state.Monitors is { } fps ? Number(fps.Fps, "0") : Unknown;
        for (var at = 0; at < _values.Length; at++)
        {
            _values[at].Text = state.Monitors is { } game ? s_monitors[at].Value(game) : Unknown;
        }

        // The state comes again each second with new monitors, so the rows are made again only when the tree changes.
        if (!state.Scene.SequenceEqual(_shownScene))
        {
            _shownScene = state.Scene;
            _live.Children.Clear();
            _live.Children.AddRange(state.Scene.Select(LiveRow));
        }

        _noScene.IsVisible = state.Scene.Count == 0;
        _liveBadge.IsVisible = !_noScene.IsVisible;
    }

    // Monitors shows the numbers over the live scene, and Scene gives the whole island to the scene.
    void ShowTab(int index)
    {
        _monitors.IsVisible = index == 0;
        for (var at = 0; at < _tabs.Length; at++)
        {
            // Transparent, not null, so the tab off still takes a click.
            _tabs[at].Background = at == index ? EditorBrushes.Line : Brushes.Transparent;
            // Tab always gives a tab its title block.
            ((TextBlock)_tabs[at].Child!).Foreground = at == index ? EditorBrushes.Text : EditorBrushes.TextDim;
        }
    }

    DockPanel RunSettings()
    {
        _choices.Children.AddRange([Caps("Scene"), new Segments("Main scene", "Current", "Chosen"), Caps("Instances"), new Segments("1", "2", "3", "4")]);
        var shown = Rows(s_shown.Select((row, at) => Setting(row.Name, _shown[at])));
        foreach (var (group, rows) in s_waiting)
        {
            _waiting.Children.Add(Section(group, Rows(rows.Select(row => Setting(row.Name, Switch(row.On))))));
        }
        _waiting.Children.Add(Note("Run uses the project's main scene and one instance."));
        var settings = new StackPanel { Children = { _choices, Section("Show while running", shown), _waiting } };
        var instances = Parts.IconButton("GuiTabMenuHl", "Customize Run Instances...");
        Unbuilt.Disable(instances);
        return Stack(Parts.Bar(Title("Run"), Parts.Right(instances)), new ScrollViewer { Content = settings });
    }

    DockPanel RunningGame()
    {
        var bar = Parts.Bar(_pill, _project, _time, Parts.Right(_front), Parts.Right(Parts.Rule()), Parts.Right(_stop), Parts.Right(_pause), Parts.Right(_run));
        var footer = new Border
        {
            Height = 30,
            Padding = new Thickness(12, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Parts.RowIcon("NodeInfo", EditorBrushes.TextDim), _state } },
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var stage = new Border
        {
            Background = EditorBrushes.Window,
            Child = new Border
            {
                VerticalAlignment = VerticalAlignment.Center,
                Child = Parts.EmptyState("Window", "No live copy yet", "The game draws in a window of its own. A copy of it here is not built yet."),
            },
        };
        return Stack(bar, footer, stage);
    }

    DockPanel Monitors()
    {
        var rate = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 14, 12, 8),
            Children =
            {
                _rate,
                new TextBlock { Text = "frames a second", FontSize = 12, Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) },
            },
        };
        _monitors.Margin = new Thickness(0, 0, 0, 8);
        _monitors.Children.Add(rate);
        _monitors.Children.AddRange(s_monitors.Select((monitor, at) => Monitor(monitor.Name, _values[at], monitor.Unit)));
        var scene = Section("Live scene", new StackPanel { Children = { _live, _noScene } }, _liveBadge);
        for (var at = 0; at < _tabs.Length; at++)
        {
            var (tab, index) = (_tabs[at], at);
            tab.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed)
                {
                    ShowTab(index);
                }
            };
        }
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _tabs[0], _tabs[1] } };
        var keepOnTop = Parts.IconButton("Pin", "Keep on top");
        Unbuilt.Disable(keepOnTop);
        return Stack(Parts.Bar(tabs, Parts.Right(keepOnTop)), new ScrollViewer { Content = new StackPanel { Children = { _monitors, scene } } });
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

    static Border Island(Control child, int column) => At(new Border { Classes = { EditorTheme.Island }, ClipToBounds = true, Child = child }, column);

    static T At<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    static Border Section(string title, Control body, params Control[] marks)
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Title(title) } };
        heading.Children.AddRange(marks);
        return new Border
        {
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Children = { new Border { Height = 34, Padding = new Thickness(6, 0), Child = heading }, body } },
        };
    }

    static Grid LiveRow(LiveEntity entity)
    {
        var name = new TextBlock { Text = entity.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(name, 1);
        var row = new Grid
        {
            Height = 26,
            Margin = new Thickness(6 + (entity.Depth * DepthIndent), 0, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children = { Parts.RowIcon(EditorIcons.ForClass(entity.Type)), name },
        };
        ToolTip.SetTip(row, entity.Type);
        return row;
    }

    static StackPanel Rows(IEnumerable<Control> rows)
    {
        var list = new StackPanel { Margin = new Thickness(6, 0, 6, 10) };
        list.Children.AddRange(rows);
        return list;
    }

    static ToggleSwitch Switch(bool on) => new() { IsChecked = on, OnContent = null, OffContent = null, MinWidth = 0 };

    static Grid Setting(string name, ToggleSwitch toggle)
    {
        var label = new TextBlock { Text = name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(At(toggle, 1), name);
        return new Grid { Height = 30, Margin = new Thickness(6, 0), ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { label, toggle } };
    }

    static Grid Monitor(string name, TextBlock value, string unit) => new()
    {
        Height = 28,
        Margin = new Thickness(12, 0),
        ColumnDefinitions = new ColumnDefinitions("*,Auto,30"),
        Children =
        {
            new TextBlock { Text = name, FontSize = 12, Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center },
            At(value, 1),
            At(new TextBlock { Text = unit, FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }, 2),
        },
    };

    static Border Tab(string text) => new()
    {
        Height = 26,
        Padding = new Thickness(10, 0),
        CornerRadius = EditorTheme.Corners,
        Child = new TextBlock { Text = text, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center },
    };

    static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    static TextBlock Title(string text) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    static TextBlock Caps(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.6, Foreground = EditorBrushes.TextDim };

    static TextBlock Note(string text) => new() { Text = $"{Unbuilt.Soon}. {text}", FontSize = 11, Foreground = EditorBrushes.Disabled, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 6, 12, 12) };

    static Button Worded(string icon, string text) => new()
    {
        Classes = { EditorTheme.TextButton, EditorTheme.Small },
        Margin = new Thickness(4, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new EditorIcon(icon) { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
        },
    };
}
