using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The middle column. One island holds the scene tabs and the viewport, with the viewport's tools floating over it.
/// The Output panel opens under it when asked, and the bottom bar is an island of its own.
/// </summary>
internal sealed class CenterArea : Grid
{
    internal const double BottomBarHeight = 38;

    internal const double GapHeight = 6;

    readonly Dictionary<string, Control> _views = [];
    readonly Grid _stage = new() { RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},*") };
    readonly Grid _main = new();

    public CenterArea(Control tabs, ViewportToolbar toolbar, Control viewport, BottomBar bottom)
    {
        RowDefinitions = new RowDefinitions($"*,Auto,{GapHeight},{BottomBarHeight}");
        SceneTabs = new Border { BorderBrush = EditorBrushes.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = tabs };
        Toolbar = toolbar;
        Stage = new Border { Classes = { EditorTheme.Island }, Child = _stage };
        BottomBar = new Border { Classes = { EditorTheme.Island }, Child = bottom };
        Overlay = new ViewportOverlay(toolbar.FrameButton) { IsVisible = false };
        Output = new OutputPanel { IsVisible = false, Margin = new Thickness(0, GapHeight, 0, 0) };
        bottom.OutputOpenChanged += open => Output.IsVisible = open;
        _main.Children.Add(viewport);
        _main.Children.Add(Overlay);
        _main.Children.Add(toolbar);
        Place(_stage, SceneTabs, 0);
        Place(_stage, _main, 1);
        Place(this, Stage, 0);
        Place(this, Output, 1);
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

    /// <summary>Over the viewport, and hidden until a viewport is shown.</summary>
    internal ViewportOverlay Overlay { get; }

    /// <summary>
    /// Adds a view that fills the island under the scene tabs while its workspace is chosen. It starts hidden.
    /// </summary>
    public void AddWorkspace(string name, Control view)
    {
        view.IsVisible = false;
        SetRow(view, 1);
        _stage.Children.Add(view);
        _views.Add(name, view);
    }

    /// <summary>
    /// Shows the viewport and its tools for the spatial workspace, the viewport alone for the planar one, which has no
    /// tools, and the view added under this name for any other.
    /// </summary>
    public void ShowWorkspace(string name)
    {
        var spatial = name == TitleBar.Spatial;
        Toolbar.IsVisible = spatial;
        _main.IsVisible = spatial || name == TitleBar.Planar;
        foreach (var (key, view) in _views)
        {
            view.IsVisible = key == name;
        }
    }

    static void Place(Grid grid, Control child, int row)
    {
        SetRow(child, row);
        grid.Children.Add(child);
    }
}
