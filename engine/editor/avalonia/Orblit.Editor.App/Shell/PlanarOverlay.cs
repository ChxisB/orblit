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

/// <summary>
/// What floats over the viewport in the 2D workspace: rulers on the top and left that follow the view, the tool
/// pill at the top left, the View menu at the top right and the zoom pill at the bottom right. Select and Move are
/// the modes. In Select a drag on the mark moves the selection, and in Move a drag anywhere does. Lock and Group
/// press the 3D toolbar's buttons and show what they show. A scene that has nothing the 2D view draws gets a note
/// over the whole view. The grid has no fill, so a press between the pills goes to the viewport.
/// </summary>
internal sealed class PlanarOverlay : Grid
{
    // A press zooms as far as three wheel notches.
    const float ZoomPress = 3;

    readonly FixedToggle _select = new("ToolSelect", "Select mode", lit: true);
    readonly FixedToggle _move = new("ToolMove", "Move mode", lit: false);
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

    public PlanarOverlay(FixedToggle locks, FixedToggle groups)
    {
        _select.Click += (_, _) => Light(_select);
        _move.Click += (_, _) => Light(_move);
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
                Parts.SoonItem("Show Grid", _view),
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
            Unbuilt.Tool("ToolRotate", "Rotate mode"),
            Unbuilt.Tool("ToolScale", "Scale mode"),
            Parts.Rule(),
            Unbuilt.Tool("ToolPan", "Pan mode"),
            Unbuilt.Tool("Ruler", "Ruler"),
            Parts.Rule(),
            Unbuilt.Tool("Snap", "Snap"),
            Unbuilt.Tool("GridToggle", "Show the grid"),
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
        Children.AddRange([_rulers, Tools, viewPill, zoomPill, _note]);
    }

    /// <summary>The pill at the top left.</summary>
    internal Border Tools { get; }

    internal FixedToggle MoveButton => _move;

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

    /// <summary>Whether a drag anywhere moves the selection, which is Move mode. In Select mode only the mark does.</summary>
    internal bool MovesAnywhere => _move.IsChecked == true;

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
        _select.IsChecked = lit == _select;
        _move.IsChecked = lit == _move;
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
