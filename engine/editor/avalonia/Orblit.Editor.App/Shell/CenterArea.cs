using Avalonia.Controls;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The middle column: the scene tabs, the viewport toolbar, the viewport, the Output panel when it is open and the bottom bar.</summary>
internal sealed class CenterArea : Grid
{
    internal const double ToolbarHeight = 42;

    internal const double BottomBarHeight = 41;

    const double GapHeight = 3;

    readonly Dictionary<string, Control> _views = [];
    readonly Grid _main = new();

    public CenterArea(Control tabs, Control toolbar, Control viewport, BottomBar bottom)
    {
        RowDefinitions = new RowDefinitions($"{DockTabs.StripHeight},{ToolbarHeight},*,Auto,{GapHeight},{BottomBarHeight}");
        SceneTabs = new Border { Background = EditorBrushes.Window, Child = tabs };
        Toolbar = new Border { Background = EditorBrushes.Panel, Child = toolbar };
        BottomBar = new Border { Background = EditorBrushes.Panel, Child = bottom };
        Overlay = new ViewportOverlay { IsVisible = false };
        Output = new OutputPanel { IsVisible = false };
        bottom.OutputOpenChanged += open => Output.IsVisible = open;
        Place(SceneTabs, 0);
        Place(Toolbar, 1);
        _main.Children.Add(viewport);
        _main.Children.Add(Overlay);
        Place(_main, 2);
        Place(Output, 3);
        Place(BottomBar, 5);
    }

    internal Border SceneTabs { get; }

    internal Border Toolbar { get; }

    internal Border BottomBar { get; }

    /// <summary>Between the viewport and the bar, and hidden until the bar's Output button opens it.</summary>
    internal OutputPanel Output { get; }

    /// <summary>Over the viewport, and hidden until a viewport is shown.</summary>
    internal ViewportOverlay Overlay { get; }

    /// <summary>
    /// Adds a view that fills the toolbar and viewport rows while its workspace is chosen. It starts hidden.
    /// </summary>
    public void AddWorkspace(string name, Control view)
    {
        view.IsVisible = false;
        SetRow(view, 1);
        SetRowSpan(view, 2);
        Children.Add(view);
        _views.Add(name, view);
    }

    /// <summary>
    /// Shows the toolbar and viewport for the spatial workspace, the viewport alone for the planar one, which has no
    /// toolbar, and the view added under this name for any other.
    /// </summary>
    public void ShowWorkspace(string name)
    {
        var spatial = name == TitleBar.Spatial;
        Toolbar.IsVisible = spatial;
        _main.IsVisible = spatial || name == TitleBar.Planar;
        SetRow(_main, spatial ? 2 : 1);
        SetRowSpan(_main, spatial ? 1 : 2);
        foreach (var (key, view) in _views)
        {
            view.IsVisible = key == name;
        }
    }

    void Place(Control child, int row)
    {
        SetRow(child, row);
        Children.Add(child);
    }
}
