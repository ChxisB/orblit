using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The tools that float over the viewport. A pill at the top left holds the modes, the coordinate and trackball
/// buttons, Snap and its steps, the lock, group and keep buttons, and two tools still to come. A pill at the bottom
/// left holds the Transform and View menus, and a Mesh menu while a mesh is selected, because each acts on the
/// selection. The Transform, Move, Rotate, Scale and Select modes light one at a time, the Local coordinates button
/// flips the handles between global and local axes, the Trackball button gives Rotate mode its ball, the Snap
/// button and its three steps round the drags, the Lock and Group buttons show the flags of the selection, the Keep
/// child transforms button lights while it is on, Focus Selection in the View menu works, and so do the Transform
/// menu and the Mesh menu. The rest answer in the status line. The grid has no fill, so a press between the pills
/// goes to the viewport.
/// </summary>
internal sealed class ViewportToolbar : Grid
{
    readonly FixedToggle _transform = new("ToolTransform", "Transform mode", lit: true);
    readonly FixedToggle _move = new("ToolMove", "Move mode", lit: false);
    readonly FixedToggle _rotate = new("ToolRotate", "Rotate mode", lit: false);
    readonly FixedToggle _scale = new("ToolScale", "Scale mode", lit: false);
    readonly FixedToggle _select = new("ToolSelect", "Select mode", lit: false);
    readonly FixedToggle[] _modes;
    readonly FixedToggle _local = new("Object", "Local coordinates", lit: false);
    readonly FixedToggle _trackball = new("Trackball", "Trackball", lit: false);
    readonly FixedToggle _snap = new("Snap", "Snap. Hold Ctrl or Command to flip it for one drag.", lit: false);
    readonly Button _snapOptions = Parts.Tight(Parts.IconButton("GuiDropdown", "Snap options"));
    readonly TextBox _snapMove = StepBox(GizmoSnap.Default.Move);
    readonly TextBox _snapTurn = StepBox(GizmoSnap.Default.Turn);
    readonly TextBox _snapScale = StepBox(GizmoSnap.Default.Scale);
    readonly FixedToggle _lock = new("Lock", "Lock the selection", lit: false);
    readonly FixedToggle _group = new("Group", "Group the selection", lit: false);
    readonly FixedToggle _keep = new("Pin", "Keep child transforms", lit: false);
    readonly Button _view = Text("View");
    readonly Button _transformMenu = Text("Transform");
    readonly Button _mesh = MeshChip();
    readonly Border _meshGap = Parts.Rule();
    readonly MenuItem _focus = new() { Header = "Focus Selection" };
    readonly MenuItem _resetPosition = new() { Header = "Reset Position" };
    readonly MenuItem _resetRotation = new() { Header = "Reset Rotation" };
    readonly MenuItem _resetScale = new() { Header = "Reset Scale" };
    readonly MenuItem _snapPosition = new() { Header = "Snap Position to Steps" };
    readonly MenuItem _snapRotation = new() { Header = "Snap Rotation to Steps" };
    readonly MenuItem _trimesh = new() { Header = "Create Trimesh Collision" };
    readonly MenuItem _convex = new() { Header = "Create Convex Collision" };

    public ViewportToolbar(Button focus)
    {
        _meshGap.IsVisible = false;
        _lock.IsEnabled = false;
        _group.IsEnabled = false;
        _transformMenu.IsEnabled = false;
        _modes = [_transform, _move, _rotate, _scale, _select];
        foreach (var mode in _modes)
        {
            mode.Click += (_, _) => Light(mode);
        }
        foreach (var toggle in new[] { _local, _trackball, _snap, _keep })
        {
            toggle.Click += (_, _) => toggle.IsChecked = toggle.IsChecked != true;
        }
        _snapOptions.Flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = 6,
                Children = { StepRow("Move step in units", _snapMove), StepRow("Rotate step in degrees", _snapTurn), StepRow("Scale step", _snapScale) },
            },
        };
        _focus.Click += (_, _) => focus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        _focus.Bind(IsEnabledProperty, focus.GetObservable(IsEnabledProperty));
        _view.Flyout = Menu(_focus);
        _transformMenu.Flyout = Menu(_resetPosition, _resetRotation, _resetScale, _snapPosition, _snapRotation);
        _mesh.Flyout = Menu(_trimesh, _convex);

        Tools = Parts.Float(
        [
            _transform,
            _move,
            _rotate,
            _scale,
            _select,
            Parts.Rule(),
            _local,
            _trackball,
            Parts.Rule(),
            _snap,
            _snapOptions,
            Parts.Rule(),
            _lock,
            _group,
            _keep,
            Parts.Rule(),
            Tool("ListSelect", "Selection from a list"),
            Tool("Ruler", "Ruler"),
        ]);
        Tools.HorizontalAlignment = HorizontalAlignment.Left;
        Tools.VerticalAlignment = VerticalAlignment.Top;
        Menus = Parts.Float([_transformMenu, _view, _meshGap, _mesh]);
        Menus.HorizontalAlignment = HorizontalAlignment.Left;
        Menus.VerticalAlignment = VerticalAlignment.Bottom;
        Children.Add(Tools);
        Children.Add(Menus);
    }

    /// <summary>The pill at the top left.</summary>
    internal Border Tools { get; }

    /// <summary>The pill at the bottom left.</summary>
    internal Border Menus { get; }

    internal FixedToggle TransformButton => _transform;

    internal FixedToggle MoveButton => _move;

    internal FixedToggle RotateButton => _rotate;

    internal FixedToggle ScaleButton => _scale;

    internal FixedToggle SelectButton => _select;

    internal FixedToggle LocalButton => _local;

    internal FixedToggle TrackballButton => _trackball;

    internal FixedToggle SnapButton => _snap;

    internal Button SnapOptionsButton => _snapOptions;

    internal TextBox SnapMoveBox => _snapMove;

    internal TextBox SnapTurnBox => _snapTurn;

    internal TextBox SnapScaleBox => _snapScale;

    /// <summary>The mode that is lit. Transform is the one lit at the start.</summary>
    internal GizmoMode Mode =>
        _move.IsChecked == true ? GizmoMode.Move
        : _rotate.IsChecked == true ? GizmoMode.Rotate
        : _scale.IsChecked == true ? GizmoMode.Scale
        : _select.IsChecked == true ? GizmoMode.Select
        : GizmoMode.Transform;

    /// <summary>Whether Rotate mode shows its trackball.</summary>
    internal bool Trackball => _trackball.IsChecked == true;

    /// <summary>Whether the Snap button is lit. A drag with Ctrl or Command down flips it for that drag.</summary>
    internal bool Snaps => _snap.IsChecked == true;

    /// <summary>The steps in the Snap options. A box that does not hold a positive number gives the default step.</summary>
    internal GizmoSnap Steps => new(
        Step(_snapMove, GizmoSnap.Default.Move),
        Step(_snapTurn, GizmoSnap.Default.Turn),
        Step(_snapScale, GizmoSnap.Default.Scale));

    /// <summary>
    /// The space the handles are in. Global is the default. Scale and Transform modes are always local, because a
    /// stretch along a world axis would shear an entity that is turned.
    /// </summary>
    internal GizmoSpace Space => Mode is GizmoMode.Scale or GizmoMode.Transform || _local.IsChecked == true ? GizmoSpace.Local : GizmoSpace.Global;

    internal FixedToggle LockButton => _lock;

    internal FixedToggle GroupButton => _group;

    internal FixedToggle KeepChildrenButton => _keep;

    /// <summary>Whether a change to a parent leaves its children where they stand.</summary>
    internal bool KeepChildren => _keep.IsChecked == true;

    internal Button ViewButton => _view;

    internal Button TransformMenuButton => _transformMenu;

    internal Button MeshMenuButton => _mesh;

    internal Border MeshGap => _meshGap;

    internal MenuItem FocusItem => _focus;

    internal MenuItem ResetPositionItem => _resetPosition;

    internal MenuItem ResetRotationItem => _resetRotation;

    internal MenuItem ResetScaleItem => _resetScale;

    internal MenuItem SnapPositionItem => _snapPosition;

    internal MenuItem SnapRotationItem => _snapRotation;

    internal MenuItem TrimeshItem => _trimesh;

    internal MenuItem ConvexItem => _convex;

    /// <summary>
    /// Lock and group act on an entity that has a place, so they are off for any other. They are lit for the
    /// flags the entity holds. The Mesh menu is for a mesh, so it is shown for that class alone. Pass null when
    /// nothing is selected.
    /// </summary>
    public void Select(OutlineNode? selected)
    {
        var placed = selected is not null && OutlineNode.IsPlaced(selected.ClassName);
        _lock.IsEnabled = placed;
        _group.IsEnabled = placed;
        _lock.IsChecked = selected?.Locked == true;
        _group.IsChecked = selected?.Grouped == true;
        _mesh.IsVisible = selected?.ClassName == "MeshInstance3D";
        _meshGap.IsVisible = _mesh.IsVisible;
    }

    void Light(FixedToggle lit)
    {
        foreach (var mode in _modes)
        {
            mode.IsChecked = mode == lit;
        }
    }

    static MenuFlyout Menu(params MenuItem[] items)
    {
        var menu = new MenuFlyout();
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }
        return menu;
    }

    static FixedToggle Tool(string icon, string name) => Unbuilt.Mark(new FixedToggle(icon, name, lit: false), $"{name} is not built yet.");

    // The class icon and the label, the height of the other menus.
    static Button MeshChip()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new EditorIcon("MeshInstance3D"));
        content.Children.Add(new TextBlock { Text = "Mesh", VerticalAlignment = VerticalAlignment.Center });
        var chip = Text("Mesh");
        chip.Content = content;
        chip.Padding = new Thickness(6, 0, 8, 0);
        chip.IsVisible = false;
        return chip;
    }

    static TextBox StepBox(float step) => new()
    {
        Text = step.ToString(CultureInfo.InvariantCulture),
        Width = 80,
        Height = 28,
        MinHeight = 0,
    };

    static StackPanel StepRow(string label, TextBox box)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Width = 150, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(box);
        return row;
    }

    static float Step(TextBox box, float fallback) =>
        float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var step) && float.IsFinite(step) && step > 0 ? step : fallback;

    static Button Text(string label) => new()
    {
        Content = label,
        Padding = new Thickness(8, 0),
        BorderThickness = new Thickness(0),
        MinWidth = 0,
        Height = 26,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
}
