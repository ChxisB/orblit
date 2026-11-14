using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The top strip: the title, the workspace switcher in a pill in the middle, then Search and the run buttons at the
/// right.
/// </summary>
internal sealed class TitleBar : Grid
{
    internal const double BarHeight = 46;

    // The system draws the traffic lights over the left of the bar, so the title starts after them.
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

    /// <summary>The name of the workspace that lists the kits of the store.</summary>
    internal const string Store = "Asset Store";

    // Name and icon file.
    static readonly (string Name, string Icon)[] Workspaces =
    [
        (Planar, "2D"),
        (Spatial, "3D"),
        (Script, "Script"),
        (Graph, "GraphEdit"),
        (Game, "Game"),
        (Store, "AssetStore"),
    ];

    readonly WorkspaceTab[] _tabs;

    public TitleBar(Button play, Button stop)
    {
        Height = BarHeight;
        Background = EditorBrushes.Window;
        ColumnDefinitions = new ColumnDefinitions("*,Auto,*");
        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(TitleLeft, 0, 0, 0),
            IsHitTestVisible = false,
            Children = { Title, EditedDot },
        };
        _tabs = [.. Workspaces.Select(Workspace)];
        _tabs.Single(tab => tab.Label == Spatial).IsChecked = true;
        Switcher = Parts.Pill(_tabs);
        SetColumn(Switcher, 1);
        Run = RunButtons(play, stop);
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
        Children.Add(heading);
        Children.Add(Switcher);
        Children.Add(end);
        PointerPressed += BeginMove;
    }

    /// <summary>Raised with the name of a workspace after its tab lights.</summary>
    public event Action<string>? WorkspaceChosen;

    internal TextBlock Title { get; } = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Shown while the scene has edits that are not saved.</summary>
    internal Ellipse EditedDot { get; } = new() { Width = 7, Height = 7, Fill = EditorBrushes.Accent, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    internal Border Switcher { get; }

    internal Border Run { get; }

    internal Button SearchButton { get; } = Search();

    internal IReadOnlyList<WorkspaceTab> Tabs => _tabs;

    internal void Show(string title, bool edited)
    {
        Title.Text = title;
        EditedDot.IsVisible = edited;
    }

    // Pause is off because the game runs in its own process and the editor cannot pause it.
    static Border RunButtons(Button play, Button stop)
    {
        var pause = Parts.IconButton("Pause", "Pause the game");
        pause.IsEnabled = false;
        return Parts.Pill(
        [
            play,
            pause,
            stop,
            Parts.Rule(),
            Unbuilt.Mark(Parts.IconButton("PlayScene", "Run the current scene"), "Run current scene is the Play button."),
            Unbuilt.Mark(Parts.IconButton("PlayCustom", "Run a chosen scene"), "Run a chosen scene is not built yet."),
            Unbuilt.Mark(Parts.IconButton("MainMovieWrite", "Movie maker mode"), "Movie maker mode is not built yet."),
        ]);
    }

    // Drawn as a field so the bar keeps its place, and marked because search is not built yet.
    static Button Search()
    {
        var icon = Parts.SearchIcon();
        icon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(icon, Dock.Left);
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
            Content = new DockPanel { Children = { icon, new TextBlock { Text = "Search", VerticalAlignment = VerticalAlignment.Center } } },
        };
        ToolTip.SetTip(button, "Search");
        return Unbuilt.Mark(button, "Search is not built yet.");
    }

    WorkspaceTab Workspace((string Name, string Icon) workspace)
    {
        var tab = new WorkspaceTab(workspace.Icon, workspace.Name);
        tab.Click += (_, _) => Choose(workspace.Name);
        return tab;
    }

    void Choose(string name)
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
