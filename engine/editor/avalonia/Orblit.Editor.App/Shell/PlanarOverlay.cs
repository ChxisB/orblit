using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>What a drag does to the selection in the 2D view.</summary>
internal enum PlanarTool { Select, Move, Rotate, Scale, Pan, }

/// <summary>
/// What floats over the viewport in the 2D workspace: the grid, rulers on the top and left that follow the view,
/// the tool pill at the top left, the View menu at the top right and the zoom pill at the bottom right. In Select
/// a drag on the mark moves the selection. In Move, Rotate and Scale a drag anywhere moves, turns or scales it, and
/// in Pan every drag pans. Snap, Lock and Group press the 3D toolbar's buttons and show what they show. A scene
/// that has nothing the 2D view draws gets a note over the whole view. The overlay has no fill, so a press between
/// the pills goes to the viewport.
/// </summary>
internal sealed class PlanarOverlay : Grid
{
    // A press zooms as far as three wheel notches.
    const float ZoomPress = 3;

    readonly FixedToggle _select = new("ToolSelect", "Select mode", lit: true);
    readonly FixedToggle _move = new("ToolMove", "Move mode", lit: false);
    readonly FixedToggle _rotate = new("ToolRotate", "Rotate mode", lit: false);
    readonly FixedToggle _scale = new("ToolScale", "Scale mode", lit: false);
    readonly FixedToggle _pan = new("ToolPan", "Pan mode", lit: false);
    readonly (FixedToggle Button, PlanarTool Tool)[] _tools;
    readonly PlanarGrid _grid = new();
    readonly FixedToggle _gridButton = new("GridToggle", "Show the grid", lit: true);
    readonly MenuItem _gridItem = new() { Header = "Show Grid", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    readonly Ruler _top = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Ruler.Thickness, 0, 0, 0) };
    readonly Ruler _side = new();
    readonly Grid _rulers = new();
    readonly MenuItem _rulersItem = new() { Header = "Show Rulers", ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
    readonly Button _view = ViewButton();
    readonly Button _center = SizedIcon("CenterView", "Center the view", 15);
    readonly Button _zoomOut = SizedIcon("ZoomLess", "Zoom out", 14);
    readonly Button _zoomIn = SizedIcon("ZoomMore", "Zoom in", 14);
    readonly Button _zoom = new()
    {
        Content = "100%",
        FontFamily = EditorTheme.Mono,
        FontSize = 12,
        Width = 52,
        Height = 26,
        MinWidth = 0,
        MinHeight = 0,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
    readonly TextBlock _noteTitle = new() { FontWeight = FontWeight.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    readonly Button _switch = new()
    {
        Classes = { EditorTheme.Primary },
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new EditorIcon("Cube") { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Switch to 3D" } },
        },
    };
    readonly Button _open = new() { Classes = { EditorTheme.Ghost }, Content = "Open a Scene..." };
    readonly Border _note;
    IEditorScene? _scene;

    public PlanarOverlay(FixedToggle locks, FixedToggle groups, FixedToggle snaps)
    {
        _tools = [(_select, PlanarTool.Select), (_move, PlanarTool.Move), (_rotate, PlanarTool.Rotate), (_scale, PlanarTool.Scale), (_pan, PlanarTool.Pan)];
        foreach (var (button, _) in _tools)
        {
            button.Click += (_, _) => Light(button);
        }

        _gridButton.Click += (_, _) => ShowGrid(!_grid.IsVisible);
        _gridItem.Click += (_, _) => ShowGrid(!_grid.IsVisible);
        _zoomIn.Click += (_, _) => Change(view => view.Zoomed(ZoomPress));
        _zoomOut.Click += (_, _) => Change(view => view.Zoomed(-ZoomPress));
        _zoom.Click += (_, _) => Change(view => view with { Zoom = 1 });
        _center.Click += (_, _) => Change(view => view with { Center = Vector2.Zero });
        // The overlay keeps the flag, so the tick is right whether or not the item flips itself before this runs.
        _rulersItem.Click += (_, _) =>
        {
            _rulers.IsVisible = !_rulers.IsVisible;
            _rulersItem.IsChecked = _rulers.IsVisible;
        };
        _view.Flyout = new MenuFlyout
        {
            Items =
            {
                _rulersItem,
                _gridItem,
                new Separator(),
                Parts.PressItem("Center the View", _center),
                Parts.PressItem("Zoom to 100%", _zoom),
            },
        };
        _rulers.Children.AddRange([Corner(), _top, new LayoutTransformControl
        {
            Width = Ruler.Thickness,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, Ruler.Thickness, 0, 0),
            LayoutTransform = new RotateTransform(-90),
            Child = _side,
        }]);
        Tools = Parts.Float(
        [
            _select,
            _move,
            _rotate,
            _scale,
            Parts.Rule(),
            _pan,
            Unbuilt.Tool("Ruler", "Ruler"),
            Parts.Rule(),
            Mirror(snaps, "Snap", "Snap to the grid. Hold Ctrl or Command to flip it for one drag."),
            _gridButton,
            Parts.Rule(),
            Mirror(locks, "Lock", "Lock the selection"),
            Mirror(groups, "Group", "Group the selection"),
        ]);
        // The pill sits 10 pixels clear of the rulers.
        Tools.Margin = new Thickness(Ruler.Thickness + 10, Ruler.Thickness + 10, 0, 0);
        Tools.HorizontalAlignment = HorizontalAlignment.Left;
        Tools.VerticalAlignment = VerticalAlignment.Top;
        var viewPill = Parts.Float([_view, _center]);
        viewPill.Margin = new Thickness(0, Ruler.Thickness + 10, 10, 0);
        viewPill.HorizontalAlignment = HorizontalAlignment.Right;
        viewPill.VerticalAlignment = VerticalAlignment.Top;
        var zoomPill = Parts.Float([_zoomOut, _zoom, _zoomIn]);
        zoomPill.HorizontalAlignment = HorizontalAlignment.Right;
        zoomPill.VerticalAlignment = VerticalAlignment.Bottom;
        _note = NoteCard(_noteTitle, _switch, _open);
        Children.AddRange([_grid, _rulers, Tools, viewPill, zoomPill, _note]);
    }

    /// <summary>The pill at the top left.</summary>
    internal Border Tools { get; }

    internal FixedToggle MoveButton => _move;

    internal FixedToggle RotateButton => _rotate;

    internal FixedToggle ScaleButton => _scale;

    internal FixedToggle PanButton => _pan;

    internal PlanarGrid Grid => _grid;

    internal FixedToggle GridButton => _gridButton;

    internal MenuItem GridItem => _gridItem;

    /// <summary>The world units between two grid lines, which is the step a snapped move keeps to.</summary>
    internal double GridStep => _grid.Step;

    internal Grid Rulers => _rulers;

    internal MenuItem RulersItem => _rulersItem;

    internal Ruler TopRuler => _top;

    internal Button CenterButton => _center;

    internal Button ZoomOutButton => _zoomOut;

    internal Button ZoomInButton => _zoomIn;

    /// <summary>The percent of the zoom. A press goes back to 100%.</summary>
    internal Button ZoomButton => _zoom;

    /// <summary>Over the whole view while the scene has nothing the 2D view draws.</summary>
    internal Border Note => _note;

    internal TextBlock NoteTitle => _noteTitle;

    internal Button SwitchButton => _switch;

    internal Button OpenButton => _open;

    /// <summary>The lit mode.</summary>
    internal PlanarTool Tool => _tools.First(tool => tool.Button.IsChecked == true).Tool;

    /// <summary>Which drags take the selection: the mark in Select, none in Pan and any in the other modes.</summary>
    internal MarkGrip Grip => Tool switch
    {
        PlanarTool.Select => MarkGrip.OnMark,
        PlanarTool.Pan => MarkGrip.Never,
        _ => MarkGrip.Anywhere,
    };

    /// <summary>
    /// Shows the open scene: its view on the rulers and the zoom pill, and the note when the 2D view draws none of
    /// its entities. The pills change this scene's view from now on. Pass a null root for a scene that is not open.
    /// </summary>
    public void ShowScene(IEditorScene scene, OutlineNode? root, string file)
    {
        _scene = scene;
        _noteTitle.Text = $"{file} is a 3D scene";
        _note.IsVisible = root is not null && !Draws(scene, root);
        Refresh();
    }

    /// <summary>Shows the scene's view again on the rulers and the zoom pill, as after the viewport panned or zoomed it.</summary>
    public void Refresh()
    {
        var view = _scene?.PlanarView ?? PlanarView.Start;
        // The frame has a pixel for each device pixel, so a pixel here holds the scaling's worth of frame pixels.
        var perPixel = (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1) / view.Zoom;
        _top.Show(view.Center.X + (Ruler.Thickness - Bounds.Width / 2) * perPixel, perPixel);
        // The left ruler is turned a quarter to the left, so it starts at the bottom and goes up.
        _side.Show(view.Center.Y + Bounds.Height / 2 * perPixel, -perPixel);
        _grid.Show(view.Center, perPixel);
        _zoom.Content = string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(view.Zoom * 100)}%");
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Refresh();
    }

    void Change(Func<PlanarView, PlanarView> change)
    {
        if (_scene is { } scene)
        {
            scene.PlanarView = change(scene.PlanarView);
            Refresh();
        }
    }

    void Light(FixedToggle lit)
    {
        foreach (var (button, _) in _tools)
        {
            button.IsChecked = button == lit;
        }
    }

    // The overlay keeps the flag, so the button and the tick stay in step whichever was pressed.
    void ShowGrid(bool shown)
    {
        _grid.IsVisible = shown;
        _gridButton.IsChecked = shown;
        _gridItem.IsChecked = shown;
    }

    static bool Draws(IEditorScene scene, OutlineNode node) => scene.IsCanvasItem(node.Handle) || node.Children.Any(child => Draws(scene, child));

    // The 3D toolbar's button keeps the flag, so this one presses it and shows what it shows.
    static FixedToggle Mirror(FixedToggle of, string icon, string tip)
    {
        var mirror = new FixedToggle(icon, tip, lit: false);
        mirror.Bind(ToggleButton.IsCheckedProperty, of.GetObservable(ToggleButton.IsCheckedProperty));
        mirror.Bind(IsEnabledProperty, of.GetObservable(IsEnabledProperty));
        mirror.Click += (_, _) => of.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return mirror;
    }

    // The design draws these icons smaller than 16 in the usual 26 pixel button.
    static Button SizedIcon(string icon, string tip, double side)
    {
        var button = Parts.IconButton(icon, tip);
        button.Content = new EditorIcon(icon) { Width = side, Height = side };
        button.Padding = new Thickness((26 - side) / 2);
        return button;
    }

    static Button ViewButton()
    {
        var chevron = new EditorIcon("GuiDropdown") { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        return new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { new TextBlock { Text = "View", VerticalAlignment = VerticalAlignment.Center }, chevron },
            },
            Height = 26,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(10, 0, 6, 0),
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
    }

    // The square where the two rulers meet.
    static Border Corner() => new()
    {
        Width = Ruler.Thickness,
        Height = Ruler.Thickness,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Background = EditorBrushes.Ruler,
        BorderBrush = EditorBrushes.RulerEdge,
        BorderThickness = new Thickness(0, 0, 1, 1),
    };

    static Border NoteCard(TextBlock title, Button switchTo, Button open)
    {
        var mark = new EditorIcon("Cube") { Width = 26, Height = 26, HorizontalAlignment = HorizontalAlignment.Center };
        TextElement.SetForeground(mark, EditorBrushes.Spatial);
        return new Border
        {
            Background = EditorBrushes.Stage,
            IsVisible = false,
            Child = new StackPanel
            {
                MaxWidth = 280,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    mark,
                    title,
                    new TextBlock
                    {
                        Text = "The 2D view draws 2D and UI entities. This scene has none.",
                        FontSize = 12,
                        Foreground = EditorBrushes.TextDim,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Margin = new Thickness(0, 4, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = { switchTo, open },
                    },
                },
            },
        };
    }
}
