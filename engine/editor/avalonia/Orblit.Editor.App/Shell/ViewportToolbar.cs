using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The tools that float over the viewport. A pill at the top left holds the modes, the coordinate and trackball
/// buttons, Snap and its popover, the lock, group and keep buttons, and two tools still to come. A chip at the
/// bottom left names the selection and holds what acts on it: the Collision menu for a mesh, Focus Selection and
/// the Reset menu. The Transform, Move, Rotate, Scale and Select modes light one at a time, the Local coordinates
/// button flips the handles between global and local axes, the Trackball button gives Rotate mode its ball, Snap
/// and the three steps in its popover round the drags, the Lock and Group buttons show the flags of the selection,
/// and the Keep child transforms button lights while it is on. The two tools still to come answer in the status
/// line. The grid has no fill, so a press between the pills goes to the viewport.
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
    readonly TextBox _snapMove = StepBox(GizmoSnap.Default.Move, "m");
    readonly TextBox _snapTurn = StepBox(GizmoSnap.Default.Turn, "°");
    readonly TextBox _snapScale = StepBox(GizmoSnap.Default.Scale, "");
    readonly ToggleSwitch _snapPosition = SnapSwitch("Snap Position to Steps");
    readonly ToggleSwitch _snapRotation = SnapSwitch("Snap Rotation to Steps");
    readonly FixedToggle _lock = new("Lock", "Lock the selection", lit: false);
    readonly FixedToggle _group = new("Group", "Group the selection", lit: false);
    readonly FixedToggle _keep = new("Pin", "Keep child transforms", lit: false);
    readonly StackPanel _named = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0) };
    readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _className = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _collision = CollisionTab();
    readonly Border _collisionRule = Parts.Rule();
    readonly Button _focus = Parts.IconButton("FocusFrame", "Focus Selection");
    readonly Button _reset = Parts.IconButton("ResetTurn", "Reset the transform");
    readonly MenuFlyout _resetMenu = new();
    readonly MenuItem _resetPosition = new() { Header = "Reset Position" };
    readonly MenuItem _resetRotation = new() { Header = "Reset Rotation" };
    readonly MenuItem _resetScale = new() { Header = "Reset Scale" };

    // Reset All has a key, and a key presses a button, so the menu item presses this one. It is never shown.
    readonly Button _resetAll = new();
    readonly MenuItem _convex = DescribedItem("Create Convex Collision", "A fast hull around the mesh. Good for things that move.");
    readonly MenuItem _trimesh = DescribedItem("Create Trimesh Collision", "Follows every triangle. Use it for still things.");

    public ViewportToolbar(Button focus)
    {
        FrameButton = focus;
        _lock.IsEnabled = false;
        _group.IsEnabled = false;
        _modes = [_transform, _move, _rotate, _scale, _select];
        foreach (var mode in _modes)
        {
            mode.Click += (_, _) => Light(mode);
        }
        foreach (var toggle in new[] { _local, _trackball, _snap, _keep })
        {
            toggle.Click += (_, _) => toggle.IsChecked = toggle.IsChecked != true;
        }
        _snapOptions.Flyout = CardHead.Popover("Snap", "Hold Ctrl or Command to flip it for one drag.", 290, new StackPanel
        {
            Spacing = 8,
            Children =
            {
                SwitchRow("Snap Position to Steps", _snapPosition),
                SwitchRow("Snap Rotation to Steps", _snapRotation),
                new Border { Height = 1, Margin = new Thickness(0, 4), Background = EditorBrushes.Line },
                StepRow("Move step", _snapMove),
                StepRow("Rotate step", _snapTurn),
                StepRow("Scale step", _snapScale),
            },
        });
        _focus.Click += (_, _) => focus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        // Each of these acts on an entity with a place in 3D, which is when the window turns Focus on.
        foreach (var control in new Control[] { _focus, _reset, _resetAll })
        {
            control.Bind(IsEnabledProperty, focus.GetObservable(IsEnabledProperty));
        }
        _resetMenu.Items.Add(Parts.MenuHeading("Reset"));
        _resetMenu.Items.Add(_resetPosition);
        _resetMenu.Items.Add(_resetRotation);
        _resetMenu.Items.Add(_resetScale);
        _resetMenu.Items.Add(new Separator());
        _resetMenu.Items.Add(Parts.PressItem("Reset All", _resetAll, new KeyGesture(Key.R, KeyModifiers.Alt)));
        _reset.Flyout = _resetMenu;
        _collision.Flyout = new MenuFlyout { Items = { Parts.MenuHeading("Add collision to the mesh"), _convex, _trimesh } };

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
            Unbuilt.Tool("ListSelect", "Selection from a list"),
            Unbuilt.Tool("Ruler", "Ruler"),
        ]);
        Tools.HorizontalAlignment = HorizontalAlignment.Left;
        Tools.VerticalAlignment = VerticalAlignment.Top;
        _named.Children.AddRange([ClassMark("Entity"), _name, _className]);
        Chip = Parts.Float([_named, Parts.Rule(), _collision, _collisionRule, _focus, _reset]);
        // The design's chip is 36 high, so it stays that high when the 28 pixel Collision tab is hidden.
        Chip.Height = 36;
        Chip.HorizontalAlignment = HorizontalAlignment.Left;
        Chip.VerticalAlignment = VerticalAlignment.Bottom;
        Chip.IsVisible = false;
        Children.Add(Tools);
        Children.Add(Chip);
    }

    /// <summary>The pill at the top left.</summary>
    internal Border Tools { get; }

    /// <summary>The chip at the bottom left. It shows while something is selected.</summary>
    internal Border Chip { get; }

    /// <summary>The window's Focus Selection button, which the chip and the projection menu press.</summary>
    internal Button FrameButton { get; }

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

    /// <summary>
    /// The steps in the Snap options. A box that does not hold a positive number gives the default step. A switch that
    /// is off gives a step of zero, which a drag reads as no snap.
    /// </summary>
    internal GizmoSnap Steps => new(
        _snapPosition.IsChecked == true ? Step(_snapMove, GizmoSnap.Default.Move) : 0f,
        _snapRotation.IsChecked == true ? Step(_snapTurn, GizmoSnap.Default.Turn) : 0f,
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

    internal EditorIcon ClassIcon => (EditorIcon)_named.Children[0];

    internal TextBlock NameText => _name;

    internal TextBlock ClassText => _className;

    internal Button CollisionButton => _collision;

    internal Border CollisionRule => _collisionRule;

    internal Button FocusButton => _focus;

    internal Button ResetButton => _reset;

    internal MenuFlyout ResetMenu => _resetMenu;

    internal Button ResetAllButton => _resetAll;

    internal MenuItem ResetPositionItem => _resetPosition;

    internal MenuItem ResetRotationItem => _resetRotation;

    internal MenuItem ResetScaleItem => _resetScale;

    internal ToggleSwitch SnapPositionSwitch => _snapPosition;

    internal ToggleSwitch SnapRotationSwitch => _snapRotation;

    internal MenuItem TrimeshItem => _trimesh;

    internal MenuItem ConvexItem => _convex;

    /// <summary>
    /// Lock and group act on an entity that has a place, so they are off for any other. They are lit for the
    /// flags the entity holds. The chip names the entity, and its Collision menu is for a mesh, so it is shown for
    /// that class alone. Pass null when nothing is selected.
    /// </summary>
    public void Select(OutlineNode? selected)
    {
        var placed = selected is not null && OutlineNode.IsPlaced(selected.ClassName);
        _lock.IsEnabled = placed;
        _group.IsEnabled = placed;
        _lock.IsChecked = selected?.Locked == true;
        _group.IsChecked = selected?.Grouped == true;
        _collision.IsVisible = selected?.ClassName == "MeshInstance3D";
        _collisionRule.IsVisible = _collision.IsVisible;
        Chip.IsVisible = selected is not null;
        if (selected is not null)
        {
            ShowName(selected);
        }
    }

    // An icon draws one name for good, so a new selection puts a new icon in its place.
    void ShowName(OutlineNode selected)
    {
        _named.Children[0] = ClassMark(selected.ClassName);
        _name.Text = selected.Name;
        _className.Text = selected.ClassName;
        _resetMenu.Items[0] = Parts.MenuHeading($"Reset {selected.Name}");
    }

    void Light(FixedToggle lit)
    {
        foreach (var mode in _modes)
        {
            mode.IsChecked = mode == lit;
        }
    }

    // A 3D class takes the 3D colour and a 2D class the 2D colour, as in New Scene.
    static EditorIcon ClassMark(string className)
    {
        var icon = new EditorIcon(EditorIcons.ForClass(className)) { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
        var tint = className switch
        {
            _ when className.EndsWith("3D", StringComparison.Ordinal) => EditorBrushes.Spatial,
            _ when className.EndsWith("2D", StringComparison.Ordinal) => EditorBrushes.Planar,
            _ => EditorBrushes.TextSoft,
        };
        TextElement.SetForeground(icon, tint);
        return icon;
    }

    // The design's tab: a dim mark, the word and a chevron, 28 high so it fills the chip.
    static Button CollisionTab()
    {
        var mark = new EditorIcon("CollisionShape3D") { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        var chevron = new EditorIcon("GuiDropdown") { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(mark, EditorBrushes.TextDim);
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        return new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { mark, new TextBlock { Text = "Collision", FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center }, chevron },
            },
            Height = 28,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(8, 0),
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
    }

    // A collision item says when to use it under its name, so the row grows past the menu's 28 pixels.
    static MenuItem DescribedItem(string header, string line) => new()
    {
        Height = double.NaN,
        Padding = new Thickness(8),
        Header = new StackPanel
        {
            Spacing = 2,
            Children = { new TextBlock { Text = header }, new TextBlock { Text = line, FontSize = 11, Foreground = EditorBrushes.TextDim } },
        },
    };

    static ToggleSwitch SnapSwitch(string name)
    {
        var toggle = new ToggleSwitch { IsChecked = true, OnContent = null, OffContent = null, MinWidth = 0 };
        AutomationProperties.SetName(toggle, name);
        return toggle;
    }

    static DockPanel SwitchRow(string label, ToggleSwitch toggle)
    {
        DockPanel.SetDock(toggle, Dock.Right);
        return new DockPanel
        {
            Height = 28,
            Children = { toggle, new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    static TextBox StepBox(float step, string unit) => new()
    {
        Classes = { EditorTheme.Field },
        Text = step.ToString(CultureInfo.InvariantCulture),
        Width = 96,
        Height = 28,
        MinHeight = 0,
        InnerRightContent = unit.Length == 0 ? null : new TextBlock
        {
            Text = unit,
            FontSize = 11,
            Foreground = EditorBrushes.Unit,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    static DockPanel StepRow(string label, TextBox box)
    {
        DockPanel.SetDock(box, Dock.Right);
        return new DockPanel
        {
            Children = { box, new TextBlock { Text = label, FontSize = 12, Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    static float Step(TextBox box, float fallback) =>
        float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var step) && float.IsFinite(step) && step > 0 ? step : fallback;
}
