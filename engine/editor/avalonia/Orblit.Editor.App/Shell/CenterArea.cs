using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The middle column. One island holds the scene tabs and the viewport, with the viewport's tools floating over it.
/// The bar's panels open under it one at a time, and the bottom bar is an island of its own.
/// </summary>
internal sealed class CenterArea : Grid
{
    internal const double BottomBarHeight = 38;

    internal const double GapHeight = 6;

    readonly Dictionary<string, Control> _views = [];
    readonly Dictionary<string, Decorator> _sides = [];
    readonly Grid _stage = new() { RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},*") };
    readonly Grid _main = new();

    public CenterArea(Control tabs, ViewportToolbar toolbar, Control viewport, BottomBar bottom)
    {
        RowDefinitions = new RowDefinitions($"*,Auto,{GapHeight},{BottomBarHeight}");
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        SceneTabs = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = tabs };
        Toolbar = toolbar;
        Stage = new Border { Classes = { EditorTheme.Island }, Child = _stage };
        BottomBar = new Border { Classes = { EditorTheme.Island }, Child = bottom };
        Overlay = new ViewportOverlay(toolbar.FrameButton) { IsVisible = false };
        Planar = new PlanarOverlay(toolbar.LockButton, toolbar.GroupButton) { IsVisible = false };
        Output = new OutputPanel();
        Debugger = new DebuggerNote();
        // In the order of the bar's tabs.
        Panels =
        [
            Output,
            new NotePanel("Debugger", Debugger),
            NotePanel.Soon("Audio", "AudioStreamPlayer", "Buses and effects for the project sound."),
            NotePanel.Soon("Animation", "Animation", "Tracks and keys for the picked entity."),
            NotePanel.Soon("Shader Editor", "ShaderDock", "Shader code and its preview."),
        ];
        Output.ErrorsAdded += bottom.AddErrors;
        bottom.PanelChanged += open =>
        {
            for (var at = 0; at < Panels.Count; at++)
            {
                Panels[at].IsVisible = at == open;
            }
        };
        _main.Children.Add(viewport);
        _main.Children.Add(Overlay);
        _main.Children.Add(toolbar);
        _main.Children.Add(Planar);
        Place(_stage, SceneTabs, 0);
        Place(_stage, _main, 1);
        Place(this, Stage, 0);
        foreach (var panel in Panels)
        {
            panel.IsVisible = false;
            panel.Margin = new Thickness(0, GapHeight, 0, 0);
            Place(this, panel, 1);
        }

        Place(this, BottomBar, 3);
    }

    /// <summary>The island that holds the scene tabs, the viewport and every other workspace's view.</summary>
    internal Border Stage { get; }

    internal Border SceneTabs { get; }

    /// <summary>The pills of the 3D tools over the viewport.</summary>
    internal Control Toolbar { get; }

    internal Border BottomBar { get; }

    /// <summary>Between the viewport and the bar, and hidden until the bar's Output button opens it.</summary>
    internal OutputPanel Output { get; }

    internal DebuggerNote Debugger { get; }

    /// <summary>The panels the bar's tabs open, in the order of the tabs. One shows at a time.</summary>
    internal IReadOnlyList<Border> Panels { get; }

    /// <summary>Over the viewport, and hidden until a viewport is shown.</summary>
    internal ViewportOverlay Overlay { get; }

    /// <summary>The rulers and pills of the 2D tools over the viewport.</summary>
    internal PlanarOverlay Planar { get; }

    /// <summary>
    /// Adds a view that takes the place of the whole island, scene tabs and all, while its workspace is chosen. It
    /// brings islands of its own. It starts hidden.
    /// </summary>
    public void AddBareWorkspace(string name, Control view)
    {
        view.IsVisible = false;
        Place(this, view, 0);
        _views.Add(name, view);
    }

    /// <summary>
    /// Adds a part beside the whole column, bottom bar and all, that shows while the workspace under this name is
    /// chosen. The part keeps its own visibility, so a closed drawer takes no room.
    /// </summary>
    public void AddSide(string name, Control side)
    {
        var holder = new Decorator { Child = side, IsVisible = false };
        SetColumn(holder, 1);
        SetRowSpan(holder, RowDefinitions.Count);
        Children.Add(holder);
        _sides.Add(name, holder);
    }

    /// <summary>
    /// Shows the viewport with the 3D tools for the spatial workspace, the viewport with the 2D tools for the planar
    /// one, and the view added under this name for any other.
    /// </summary>
    public void ShowWorkspace(string name)
    {
        var spatial = name == TitleBar.Spatial;
        var planar = name == TitleBar.Planar;
        Toolbar.IsVisible = spatial;
        Planar.IsVisible = planar;
        _main.IsVisible = spatial || planar;
        // An added view brings islands of its own and takes the place of this one.
        Stage.IsVisible = !_views.ContainsKey(name);
        foreach (var (key, view) in _views)
        {
            view.IsVisible = key == name;
        }

        foreach (var (key, side) in _sides)
        {
            side.IsVisible = key == name;
        }
    }

    static void Place(Grid grid, Control child, int row)
    {
        SetRow(child, row);
        grid.Children.Add(child);
    }
}
