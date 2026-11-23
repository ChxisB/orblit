using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    // Name and icon file.
    static readonly (string Name, string Icon)[] Workspaces =
    [
        (Planar, "2D"),
        (Spatial, "3D"),
        (Script, "Script"),
        (Graph, "GraphEdit"),
        (Game, "Game"),
    ];

    readonly WorkspaceTab[] _tabs;

    public TitleBar(Button play, Button playScene, Button stop, KeyModifiers command)
    {
        Height = BarHeight;
        Background = EditorBrushes.Window;
        ColumnDefinitions = new ColumnDefinitions("*,Auto,*");
        Heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children = { Logo, _project, _slash, _scene, EditedDot },
        };
        // The system bar of macOS shows the window's menu, and this bar hides itself there.
        var start = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(TitleLeft, 0, 0, 0),
            Children = { Heading, Menus },
        };
        _tabs = [.. Workspaces.Select(Workspace)];
        _tabs.Single(tab => tab.Label == Spatial).IsChecked = true;
        Switcher = Parts.Pill(_tabs);
        SetColumn(Switcher, 1);
        MoreButton.Flyout = RunMenu(play, playScene, command);
        Run = RunButtons(play, playScene, stop, MoreButton);
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
    internal StackPanel Heading { get; }

    internal Ellipse Logo { get; } = Parts.LogoMark(15);

    readonly TextBlock _project = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };

    readonly TextBlock _slash = new() { Text = "/", Foreground = EditorBrushes.Disabled, VerticalAlignment = VerticalAlignment.Center };

    readonly TextBlock _scene = new() { Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Shown while the scene has edits that are not saved.</summary>
    internal Ellipse EditedDot { get; } = new() { Width = 7, Height = 7, Fill = EditorBrushes.Accent, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    /// <summary>The window's menus as a row of tabs, for a system that has no menu bar of its own.</summary>
    internal NativeMenuBar Menus { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

    internal Button MoreButton { get; } = Parts.IconButton("GuiDropdown", "More ways to run");

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
    }

    // Pause is off because the game runs in its own process and the editor cannot pause it.
    static Border RunButtons(Button play, Button playScene, Button stop, Button more)
    {
        var pause = Parts.IconButton("Pause", "Pause the game");
        pause.IsEnabled = false;
        return Parts.Pill(
        [
            play,
            playScene,
            pause,
            stop,
            Parts.Rule(),
            more,
        ]);
    }

    // The game runs the main scene or the open one, so the other ways answer that they are not built.
    MenuFlyout RunMenu(Button play, Button playScene, KeyModifiers command) => new()
    {
        Placement = PlacementMode.BottomEdgeAlignedRight,
        Items =
        {
            Parts.PressItem("Run the project", play, new KeyGesture(Key.B, command)),
            Parts.PressItem("Run the current scene", playScene, new KeyGesture(Key.R, command)),
            Parts.SoonItem("Run a chosen scene...", MoreButton, new KeyGesture(Key.R, command | KeyModifiers.Shift)),
            new Separator(),
            Parts.SoonItem("Movie maker mode", MoreButton),
            new Separator(),
            Parts.SoonItem("Customize Run Instances...", MoreButton),
        },
    };

    // Drawn as a field with its key, because it opens the command palette's search.
    static Button Search(KeyModifiers command)
    {
        var icon = Parts.SearchIcon();
        icon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(icon, Dock.Left);
        var key = Parts.Kbd(Parts.KeyText(new KeyGesture(Key.K, command)));
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
        ToolTip.SetTip(button, "Search");
        return button;
    }

    WorkspaceTab Workspace((string Name, string Icon) workspace)
    {
        var tab = new WorkspaceTab(workspace.Icon, workspace.Name);
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
        if (e.Source == this && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }
}
