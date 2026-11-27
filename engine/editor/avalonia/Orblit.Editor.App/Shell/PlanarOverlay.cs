using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
/// the tool pill at the top left, the View menu at the top right and the zoom pill at the bottom right. Each tool
/// shows its icon, with its name and key on its card, as the 3D bar does. In Pick a drag on the mark moves the selection. In Move, Turn and
/// Resize a drag anywhere moves, turns or resizes it, and in Pan every drag pans. Snap, Lock and Group press the 3D
/// toolbar's buttons and show what they show. A scene that has nothing the 2D view draws gets a note over the whole
/// view that says why and offers Go to 3D, Open a 2D scene and Add a 2D thing. The overlay has no fill, so a press
/// between the pills goes to the viewport.
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
    readonly FixedToggle _gridButton = new("GridToggle", "Grid", lit: true);
    readonly MenuItem _gridItem = Shown("Grid", "Lines to line things up with");
    readonly Ruler _top = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Ruler.Thickness, 0, 0, 0) };
    readonly Ruler _side = new();
    readonly Grid _rulers = new();
    readonly MenuItem _rulersItem = Shown("Rulers", "The pixel rulers at the edges");
    readonly Button _view = ViewButton();
    readonly Button _center = Parts.IconButton("CenterView", "Center the view", 15);
    readonly Button _zoomOut = new();
    readonly Button _zoomIn = new();
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
    readonly TextBlock _noteLine = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    readonly Button _switch = new() { Classes = { EditorTheme.Primary } };
    readonly Button _open = new() { Classes = { EditorTheme.Ghost } };
    readonly Button _add = new() { Classes = { EditorTheme.Ghost } };
    readonly FixedToggle _snap;
    readonly Border _note;
    IEditorScene? _scene;

    /// <summary>The 2D tools over the 3D bar's lock, group and snap buttons. Cards name keys with this command key.</summary>
    public PlanarOverlay(FixedToggle locks, FixedToggle groups, FixedToggle snaps, KeyModifiers command)
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
        _rulers.Children.AddRange([Corner(), _top, new LayoutTransformControl
        {
            Width = Ruler.Thickness,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, Ruler.Thickness, 0, 0),
            LayoutTransform = new RotateTransform(-90),
            Child = _side,
        }]);
        var hold = Parts.KeyText(new KeyGesture(Key.None, command));
        _snap = Mirror(snaps, "Snap", new HoverCard("Snap", $"Moves go in grid steps. Hold {hold} to flip it for one drag.", "Y", "Use Snap"));
        Tools = Parts.Float(
        [
            .. DressTools(hold),
            Parts.Rule(),
            _snap,
            _gridButton,
            Parts.Rule(),
            Mirror(locks, "Lock", new HoverCard("Lock", "Stops the picked thing being moved by accident.", Parts.KeyText(new KeyGesture(Key.L, command)), "Lock"), "Lock stops it being moved by accident."),
            Mirror(groups, "Group", new HoverCard("Group", "A click on a part picks this thing, so the parts move as one.", Parts.KeyText(new KeyGesture(Key.G, command)), "Group"), "Group makes its parts move as one."),
        ]);
        // The pill sits 12 pixels clear of the rulers.
        Tools.Margin = new Thickness(Ruler.Thickness + Gaps.Group, Ruler.Thickness + Gaps.Group, 0, 0);
        Tools.HorizontalAlignment = HorizontalAlignment.Left;
        Tools.VerticalAlignment = VerticalAlignment.Top;
        Children.AddRange([_grid, _rulers, Tools, .. ViewPills(command)]);
        _note = NoteCard(_noteTitle, _noteLine, [Way(_switch, ("Cube", "Go to 3D"), new KeyGesture(Key.D2, command)), Way(_open, ("Load", "Open a 2D scene…"), new KeyGesture(Key.O, command | KeyModifiers.Shift)), Way(_add, ("Add", "Add a 2D thing"), new KeyGesture(Key.A, command))]);
        Children.Add(_note);
    }

    /// <summary>The pill at the top left.</summary>
    internal Border Tools { get; }

    internal FixedToggle SelectButton => _select;

    internal FixedToggle MoveButton => _move;

    internal FixedToggle RotateButton => _rotate;

    internal FixedToggle ScaleButton => _scale;

    internal FixedToggle PanButton => _pan;

    internal PlanarGrid Grid => _grid;

    internal FixedToggle GridButton => _gridButton;

    /// <summary>The Snap button, which presses the 3D bar's.</summary>
    internal FixedToggle SnapButton => _snap;

    internal MenuItem GridItem => _gridItem;

    /// <summary>The menu of the View button at the top right.</summary>
    internal MenuFlyout ViewMenu => (MenuFlyout)_view.Flyout!;

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

    internal TextBlock NoteLine => _noteLine;

    internal Button SwitchButton => _switch;

    internal Button OpenButton => _open;

    /// <summary>Add a 2D thing in the note. The window has it press the Add button.</summary>
    internal Button AddButton => _add;

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
        var name = Path.GetFileNameWithoutExtension(file);
        _noteTitle.Text = $"{name} is a 3D scene";
        _noteLine.Text = $"This view shows 2D scenes and menus. {name} has no 2D things yet.";
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

    // The four modes and Pan, each card with its name, its key and what a drag does in it.
    Control[] DressTools(string hold) =>
    [
        Parts.Dress(_select, "ToolSelect", new HoverCard("Pick", "Click a thing to pick it. Drag its mark to move it.", "Q", "Select mode")),
        Parts.Dress(_move, "ToolMove", new HoverCard("Move", $"Drag anywhere to move the picked thing. Hold {hold} to snap to the grid.", "W", "Move mode")),
        Parts.Dress(_rotate, "ToolRotate", new HoverCard("Turn", "Drag anywhere to turn the picked thing.", "E", "Rotate mode")),
        Parts.Dress(_scale, "ToolScale", new HoverCard("Resize", "Drag anywhere to make the picked thing bigger or smaller.", "R", "Scale mode")),
        Parts.Rule(),
        Parts.Dress(_pan, "ToolPan", new HoverCard("Pan", "Drag to look around. Nothing in the scene moves.", engine: "Pan mode")),
    ];

    // The View pill at the top right and the zoom pill at the bottom right. Each zoom button's card gives its key.
    Border[] ViewPills(KeyModifiers command)
    {
        var actual = new KeyGesture(Key.D0, command);
        _view.Flyout = new MenuFlyout
        {
            Items =
            {
                _rulersItem,
                _gridItem,
                new Separator(),
                Parts.PressItem("Center the view", _center, null, "Puts the point 0, 0 in the middle"),
                Parts.PressItem("Actual size", _zoom, actual, "One pixel on screen is one in the game"),
            },
        };
        Parts.Dress(_gridButton, "GridToggle", new HoverCard("Grid", "Lines to line things up with."));
        new HoverCard("Center the view", "Puts the point 0, 0 in the middle.").On(_center);
        new HoverCard("Actual size", "One pixel on screen is one in the game.", Parts.KeyText(actual)).On(_zoom);
        Parts.Dress(_zoomOut, "ZoomLess", new HoverCard("Zoom out", "See more of the scene.", Parts.KeyText(new KeyGesture(Key.OemMinus, command))));
        Parts.Dress(_zoomIn, "ZoomMore", new HoverCard("Zoom in", "See the scene closer.", Parts.KeyText(new KeyGesture(Key.OemPlus, command))));
        var viewPill = Parts.Float([_view, _center]);
        viewPill.Margin = new Thickness(0, Ruler.Thickness + Gaps.Group, Gaps.Group, 0);
        viewPill.HorizontalAlignment = HorizontalAlignment.Right;
        viewPill.VerticalAlignment = VerticalAlignment.Top;
        var zoomPill = Parts.Float([_zoomOut, _zoom, _zoomIn]);
        zoomPill.HorizontalAlignment = HorizontalAlignment.Right;
        zoomPill.VerticalAlignment = VerticalAlignment.Bottom;
        return [viewPill, zoomPill];
    }

    // The 3D toolbar's button keeps the flag, so this one presses it and shows what it shows. Lock and Group act on
    // a thing that has a place, so their cards say so while they are off.
    static FixedToggle Mirror(FixedToggle of, string icon, HoverCard card, string? off = null)
    {
        var mirror = Parts.Dress(new FixedToggle(icon, card.Title, lit: false), icon, card);
        mirror.Bind(ToggleButton.IsCheckedProperty, of.GetObservable(ToggleButton.IsCheckedProperty));
        mirror.Bind(IsEnabledProperty, of.GetObservable(IsEnabledProperty));
        mirror.Click += (_, _) => of.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (off is not null)
        {
            card.SaysWhyOff(mirror, $"Pick a thing you can move first. {off}");
        }

        return mirror;
    }

    static MenuItem Shown(string name, string note)
    {
        var item = new MenuItem { Header = Parts.Noted(name, note), ToggleType = MenuItemToggleType.CheckBox, IsChecked = true };
        AutomationProperties.SetName(item, name);
        return item;
    }

    // A way out of the note: its icon, its words and its key.
    static Button Way(Button button, (string Icon, string Words) name, KeyGesture key)
    {
        button.Content = Parts.Worded(name.Icon, name.Words, Parts.KeyText(key));
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
                Spacing = Gaps.Tight,
                Children = { new TextBlock { Text = "View", VerticalAlignment = VerticalAlignment.Center }, chevron },
            },
            Height = 26,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(Gaps.Item, 0, Gaps.Tight, 0),
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

    static Border NoteCard(TextBlock title, TextBlock line, Button[] ways)
    {
        var mark = new EditorIcon("Cube") { Width = 26, Height = 26, HorizontalAlignment = HorizontalAlignment.Center };
        TextElement.SetForeground(mark, EditorBrushes.Spatial);
        var buttons = new WrapPanel { Margin = new Thickness(0, Gaps.Tight, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var way in ways)
        {
            way.Margin = new Thickness(Gaps.Tight);
            buttons.Children.Add(way);
        }

        return new Border
        {
            Background = EditorBrushes.Stage,
            IsVisible = false,
            Child = new StackPanel
            {
                MaxWidth = 460,
                Spacing = Gaps.Item,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { mark, title, line, buttons },
            },
        };
    }
}
