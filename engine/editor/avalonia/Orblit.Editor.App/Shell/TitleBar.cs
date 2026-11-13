using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The top strip: the window title, the workspace switcher in the middle and the run buttons at the right.</summary>
internal sealed class TitleBar : Grid
{
    internal const double BarHeight = 41;

    // The system draws the traffic lights over the left of the bar, so the title starts after them.
    const double TitleLeft = 92;

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

    public TitleBar(Button play, Button stop)
    {
        Height = BarHeight;
        Background = EditorBrushes.Window;
        ColumnDefinitions = new ColumnDefinitions("*,Auto,*");
        Title = new TextBlock { Margin = new Thickness(TitleLeft, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var tabs = Workspaces.Select(Workspace).ToArray();
        tabs.Single(tab => tab.Label == Spatial).IsChecked = true;
        var switcher = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        switcher.Children.AddRange(tabs);
        SetColumn(switcher, 1);
        var run = RunButtons(play, stop);
        SetColumn(run, 2);
        Children.Add(Title);
        Children.Add(switcher);
        Children.Add(run);
        PointerPressed += BeginMove;
    }

    /// <summary>Raised with the name of a workspace after its tab lights.</summary>
    public event Action<string>? WorkspaceChosen;

    internal TextBlock Title { get; }

    internal IEnumerable<WorkspaceTab> Tabs => ((StackPanel)Children[1]).Children.Cast<WorkspaceTab>();

    // Pause is off because the game runs in its own process and the editor cannot pause it.
    static StackPanel RunButtons(Button play, Button stop)
    {
        var pause = Parts.IconButton("Pause", "Pause the game");
        pause.IsEnabled = false;
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            Children =
            {
                play,
                pause,
                stop,
                Unbuilt.Mark(Parts.IconButton("PlayScene", "Run the current scene"), "Run current scene is the Play button."),
                Unbuilt.Mark(Parts.IconButton("PlayCustom", "Run a chosen scene"), "Run a chosen scene is not built yet."),
                Unbuilt.Mark(Parts.IconButton("MainMovieWrite", "Movie maker mode"), "Movie maker mode is not built yet."),
            },
        };
        // The reference run buttons are 32 pixels apart: the icon and 8 pixels each side. The buttons that
        // start a scene sit 4 pixels clear of the three before them.
        foreach (var button in bar.Children.Cast<Button>())
        {
            button.Padding = new Thickness(8);
            button.BorderThickness = new Thickness(0);
        }

        bar.Children[3].Margin = new Thickness(4, 0, 0, 0);

        return bar;
    }

    WorkspaceTab Workspace((string Name, string Icon) workspace)
    {
        var tab = new WorkspaceTab(workspace.Icon, workspace.Name) { Padding = new Thickness(10, 4), MinHeight = 0 };
        tab.Click += (_, _) => Choose(workspace.Name);
        return tab;
    }

    void Choose(string name)
    {
        foreach (var tab in Tabs)
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
