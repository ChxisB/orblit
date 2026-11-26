using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The top strip: the logo, the project and scene names and the menus, the workspace switcher in a pill in the
/// middle, then Search and the run buttons at the right.
/// </summary>
internal sealed class TitleBar : Grid
{
    internal const double BarHeight = 46;

    // The system draws the traffic lights over the left of the bar, so the heading starts after them.
    const double TitleLeft = 92;

    const double SearchWidth = 176;

    /// <summary>The name of the workspace that edits the scene in the viewport with a 3D camera.</summary>
    internal const string Spatial = "3D";

    /// <summary>The name of the workspace that edits the 2D entities of the scene in the viewport.</summary>
    internal const string Planar = "2D";

    /// <summary>The name of the workspace that edits the C# files of the project.</summary>
    internal const string Script = "Script";

    /// <summary>The name of the workspace that edits the graph scripts of the project.</summary>
    internal const string Graph = "Graph";

    /// <summary>The name of the workspace that runs the project and stops it.</summary>
    internal const string Game = "Game";

    // Name, icon file, what the card says the workspace is for, and the engine's name where it differs.
    static readonly (string Name, string Icon, string Line, string? Engine)[] Workspaces =
    [
        (Planar, "2D", "Make 2D scenes, menus and buttons.", null),
        (Spatial, "3D", "Build and arrange your 3D world.", null),
        (Script, "Script", "Write C# code for your things.", null),
        (Graph, "GraphEdit", "Join steps to make things happen. No code needed.", "Visual script editor"),
        (Game, "Game", "Play the game and watch it while it runs.", null),
    ];

    readonly WorkspaceTab[] _tabs;

    public TitleBar(Button play, Button playScene, Button stop, KeyModifiers command)
    {
        Height = BarHeight;
        Background = EditorBrushes.Window;
        // The system bar of macOS puts a note in the item's tip. This bar has room to show it under the name.
        Menus.Styles.Add(new Style(x => x.OfType<MenuItem>()) { Setters = { new Setter(HeaderedSelectingItemsControl.HeaderTemplateProperty, EditorMenu.BarHeader) } });
        ColumnDefinitions = new ColumnDefinitions("*,Auto,*");
        Heading = ProjectHeading();
        // The system bar of macOS shows the window's menu, and this bar hides itself there. A short window cuts
        // the project's name and keeps the menus whole.
        var start = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(TitleLeft, 0, 0, 0),
            Children = { Heading, Menus },
        };
        SetColumn(Menus, 1);
        _tabs = [.. Workspaces.Select((workspace, at) => Workspace(workspace, Parts.KeyText(new KeyGesture(Key.D1 + at, command))))];
        _tabs.Single(tab => tab.Label == Spatial).IsChecked = true;
        Switcher = Parts.Pill(_tabs);
        SetColumn(Switcher, 1);
        Run = RunButtons(play, playScene, stop, command);
        SearchButton = Search(command);
        var end = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Children = { SearchButton, Run },
        };
        SetColumn(end, 2);
        Children.Add(start);
        Children.Add(Switcher);
        Children.Add(end);
        PointerPressed += BeginMove;
    }

    /// <summary>Raised with the name of a workspace after its tab lights.</summary>
    public event Action<string>? WorkspaceChosen;

    /// <summary>The logo, then the project, a slash and the scene, then the dot for edits.</summary>
    internal Grid Heading { get; }

    internal Ellipse Logo { get; } = Parts.LogoMark(15);

    readonly TextBlock _project = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    readonly TextBlock _slash = new() { Text = "/", Foreground = EditorBrushes.Disabled, VerticalAlignment = VerticalAlignment.Center };

    readonly TextBlock _scene = new() { Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 200, TextTrimming = TextTrimming.CharacterEllipsis };

    /// <summary>Shown while the scene has edits that are not saved.</summary>
    internal Ellipse EditedDot { get; } = new() { Width = 7, Height = 7, Fill = EditorBrushes.Accent, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    /// <summary>The window's menus as a row of tabs, for a system that has no menu bar of its own.</summary>
    internal NativeMenuBar Menus { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

    internal Button MoreButton { get; } = new HoverCard("More ways to play", "Play the scene you are in, or the whole game.").On(Parts.IconButton("GuiDropdown", "More ways to play"));

    internal Border Switcher { get; }

    internal Border Run { get; }

    internal Button SearchButton { get; }

    internal IReadOnlyList<WorkspaceTab> Tabs => _tabs;

    /// <summary>Shows the project, the scene file when there is one, and the dot while the scene has edits.</summary>
    internal void Show(string project, string? scene, bool edited)
    {
        _project.Text = project;
        _scene.Text = scene;
        _slash.IsVisible = _scene.IsVisible = scene is not null;
        EditedDot.IsVisible = edited;
        // Either name may be cut short, so the heading's hover card gives both.
        ToolTip.SetTip(Heading, scene is null ? project : $"{project} / {scene}");
    }

    // The project's name is the one part that gives way. Each part keeps its own gap, so a hidden one leaves none.
    Grid ProjectHeading()
    {
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
        };
        Control[] parts = [Logo, _project, _slash, _scene, EditedDot];
        for (var at = 0; at < parts.Length; at++)
        {
            parts[at].Margin = new Thickness(at == 0 ? 0 : 8, 0, 0, 0);
            SetColumn(parts[at], at);
        }

        heading.Children.AddRange(parts);
        return heading;
    }

    // Play and Stop keep their words, as the one action every user looks for. Their keys are on their cards.
    static void Worded(Button button, string icon, HoverCard card)
    {
        button.Content = Parts.Worded(icon, card.Title);
        button.Height = 26;
        button.MinHeight = 0;
        button.Padding = new Thickness(8, 0);
        button.BorderThickness = new Thickness(0);
        card.On(button);
    }

    // Play this scene lives in the menu alone. No Pause, because the game runs in its own process and the editor
    // cannot pause it.
    Border RunButtons(Button play, Button playScene, Button stop, KeyModifiers command)
    {
        string Keys(Key key) => Parts.KeyText(new KeyGesture(key, command));

        var playCard = new HoverCard("Play", "Starts the scene Play settings pick, in its own window.", Keys(Key.B), "Run Project");
        playCard.SaysWhyOff(play, "Open a scene first.");
        var stopCard = new HoverCard("Stop", "Closes the game window.", Keys(Key.OemPeriod));
        stopCard.SaysWhyOff(stop, $"Nothing is playing. Press {Keys(Key.B)} first.");
        Worded(play, "MainPlay", playCard);
        Worded(stop, "Stop", stopCard);
        MoreButton.Flyout = new MenuFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            Items =
            {
                Parts.PressItem("Play the game", play, new KeyGesture(Key.B, command), "Starts the scene Play settings pick"),
                Parts.PressItem("Play this scene", playScene, new KeyGesture(Key.R, command), "Starts from the scene you are in"),
            },
        };
        return Parts.Pill([play, stop, Parts.Rule(), MoreButton]);
    }

    // Drawn as a field with its key, because it opens the command palette's search.
    static Button Search(KeyModifiers command)
    {
        var icon = Parts.SearchIcon();
        icon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(icon, Dock.Left);
        var keys = Parts.KeyText(new KeyGesture(Key.K, command));
        var key = Parts.Kbd(keys);
        key.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(key, Dock.Right);
        var button = new Button
        {
            Width = SearchWidth,
            Height = 30,
            Padding = new Thickness(10, 0),
            Background = EditorBrushes.Field,
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Foreground = EditorBrushes.TextDim,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Children = { icon, key, new TextBlock { Text = "Search", VerticalAlignment = VerticalAlignment.Center } } },
        };
        return new HoverCard("Search", "Finds any file, thing, setting or action.", keys).On(button);
    }

    WorkspaceTab Workspace((string Name, string Icon, string Line, string? Engine) workspace, string key)
    {
        var tab = new HoverCard(workspace.Name, workspace.Line, key, workspace.Engine).On(new WorkspaceTab(workspace.Icon, workspace.Name));
        tab.Click += (_, _) => Choose(workspace.Name);
        return tab;
    }

    internal void Choose(string name)
    {
        foreach (var tab in _tabs)
        {
            tab.IsChecked = tab.Label == name;
        }

        WorkspaceChosen?.Invoke(name);
    }

    // A bar with nothing under the pointer is the place to take hold of the window.
    void BeginMove(object? sender, PointerPressedEventArgs e)
    {
        // The heading takes the pointer for its hover card, so a press on it moves the window too.
        var onBar = e.Source == this || e.Source is Visual part && (part == Heading || Heading.IsVisualAncestorOf(part));
        if (onBar && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }
}
