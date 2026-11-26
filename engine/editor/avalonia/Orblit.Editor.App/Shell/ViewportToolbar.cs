using System.Globalization;
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

/// <summary>
/// The tools that float over the viewport. Two pills at the top left wrap onto a second line in a narrow view. The
/// first holds the Pick, Move, Turn and Resize modes and the More menu of All handles, Free turn, Move alone and
/// Pick from a list. The second holds Local, Snap and its steps, Lock, Group and Measure. Each tool shows its icon,
/// and its card gives its name, its key and what it does. A chip at the bottom left names the selection and holds what acts
/// on it: Make it solid for a mesh, Zoom to it and Put it back. The modes light one at a time, and More is lit while
/// All handles is the mode. Pick from a list and Measure change what a click does, so they light one at a time and
/// a press on the lit one puts it out. The pills have no fill between them, so a press there goes to the viewport.
/// </summary>
internal sealed class ViewportToolbar : Grid
{
    // All handles, Free turn, Move alone and Pick from a list sit in the More menu. Their buttons keep the flags
    // and are never shown, so a menu item presses one as a key would.
    readonly FixedToggle _transform = new("ToolTransform", "Transform mode", lit: true);
    readonly FixedToggle _move = new("ToolMove", "Move mode", lit: false);
    readonly FixedToggle _rotate = new("ToolRotate", "Rotate mode", lit: false);
    readonly FixedToggle _scale = new("ToolScale", "Scale mode", lit: false);
    readonly FixedToggle _select = new("ToolSelect", "Select mode", lit: false);
    readonly FixedToggle[] _modes;
    readonly FixedToggle _more = new("GuiTabMenuHl", "More tools", lit: true);
    readonly FixedToggle _local = new("Object", "Local coordinates", lit: false);
    readonly FixedToggle _trackball = new("Trackball", "Trackball", lit: false);
    readonly FixedToggle _snap = new("Snap", "Snap", lit: false);
    readonly Button _snapOptions = Parts.Tight(Parts.IconButton("GuiDropdown", "Snap steps"));
    readonly TextBox _snapMove = StepBox(GizmoSnap.Default.Move, "m");
    readonly TextBox _snapTurn = StepBox(GizmoSnap.Default.Turn, "°");
    readonly TextBox _snapScale = StepBox(GizmoSnap.Default.Scale, "");
    readonly ToggleSwitch _snapPosition = SnapSwitch("Snap moving");
    readonly ToggleSwitch _snapRotation = SnapSwitch("Snap turning");
    readonly FixedToggle _lock = new("Lock", "Lock", lit: false);
    readonly FixedToggle _group = new("Group", "Group", lit: false);
    readonly FixedToggle _keep = new("Pin", "Keep child transforms", lit: false);
    readonly FixedToggle _listSelect = new("ListSelect", "Select from a list", lit: false);
    readonly FixedToggle _ruler = new("Ruler", "Measure", lit: false);
    readonly StackPanel _named = new() { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, Margin = new Thickness(Gaps.Item, 0) };
    readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _className = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly Button _collision = SolidTab();
    readonly MenuFlyout _solidMenu = new();
    readonly Border _collisionRule = Parts.Rule();
    readonly Button _focus = new();
    readonly Button _reset = new();
    readonly MenuFlyout _resetMenu = new();
    readonly MenuItem _resetPosition = new() { Header = "Position to 0, 0, 0" };
    readonly MenuItem _resetRotation = new() { Header = "Turn to 0°" };
    readonly MenuItem _resetScale = new() { Header = "Size to 1" };

    // Put all back has a key, and a key presses a button, so the menu item presses this one. It is never shown.
    readonly Button _resetAll = new();
    readonly MenuItem _convex = FitItem("Simple fit", "A fast shape around it. Best for things that move", "Convex");
    readonly MenuItem _trimesh = FitItem("Tight fit", "Follows every face. Best for floors and walls that never move", "Trimesh");

    /// <summary>The bar for the window's Focus Selection button. Its cards name keys with this command key.</summary>
    public ViewportToolbar(Button focus, KeyModifiers command)
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
        _listSelect.Click += (_, _) => (_listSelect.IsChecked, _ruler.IsChecked) = (_listSelect.IsChecked != true, false);
        _ruler.Click += (_, _) => (_ruler.IsChecked, _listSelect.IsChecked) = (_ruler.IsChecked != true, false);
        _more.Bind(ToggleButton.IsCheckedProperty, _transform.GetObservable(ToggleButton.IsCheckedProperty));
        _more.Flyout = new MenuFlyout
        {
            Items =
            {
                Parts.MenuHeading("More tools"),
                ToolItem(("All handles", "Transform mode"), "Move, turn and resize with one tool", _transform, MenuItemToggleType.Radio),
                ToolItem(("Free turn", "Trackball"), "Turn by dragging anywhere, like a ball", _trackball, MenuItemToggleType.CheckBox),
                ToolItem(("Move alone", "Keep child transforms"), "Moves a parent and leaves the things inside where they are", _keep, MenuItemToggleType.CheckBox),
                ToolItem(("Pick from a list", "List select"), "Click where things overlap, then choose one", _listSelect, MenuItemToggleType.CheckBox),
            },
        };
        var hold = Parts.KeyText(new KeyGesture(Key.None, command));
        DressTools(hold, command);
        _snapOptions.Flyout = SnapSteps(hold);
        var modes = Parts.Float([_select, _move, _rotate, _scale, _more]);
        var options = Parts.Float([_local, Parts.Rule(), _snap, _snapOptions, Parts.Rule(), _lock, _group, Parts.Rule(), _ruler]);
        modes.Margin = new Thickness(Gaps.Group, Gaps.Group, 0, 0);
        options.Margin = new Thickness(Gaps.Group, Gaps.Group, 0, 0);
        Tools = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Children = { modes, options } };
        Chip = ChipFor(focus);
        Children.Add(Tools);
        Children.Add(Chip);
    }

    /// <summary>The two pills at the top left: the modes, then the options.</summary>
    internal WrapPanel Tools { get; }

    /// <summary>The chip at the bottom left. It shows while something is selected.</summary>
    internal Border Chip { get; }

    /// <summary>The window's Focus Selection button, which the chip and the projection menu press.</summary>
    internal Button FrameButton { get; }

    internal FixedToggle TransformButton => _transform;

    internal FixedToggle MoveButton => _move;

    internal FixedToggle RotateButton => _rotate;

    internal FixedToggle ScaleButton => _scale;

    internal FixedToggle SelectButton => _select;

    /// <summary>Lit while All handles is the mode. A press opens the More menu.</summary>
    internal FixedToggle MoreButton => _more;

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

    internal FixedToggle ListSelectButton => _listSelect;

    internal FixedToggle RulerButton => _ruler;

    /// <summary>True while a click shows what is under it to choose from.</summary>
    internal bool ListSelects => _listSelect.IsChecked == true;

    /// <summary>True while a click puts down a ruler point.</summary>
    internal bool Measures => _ruler.IsChecked == true;

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

    internal MenuFlyout SolidMenu => _solidMenu;

    internal Button ResetAllButton => _resetAll;

    internal MenuItem ResetPositionItem => _resetPosition;

    internal MenuItem ResetRotationItem => _resetRotation;

    internal MenuItem ResetScaleItem => _resetScale;

    internal ToggleSwitch SnapPositionSwitch => _snapPosition;

    internal ToggleSwitch SnapRotationSwitch => _snapRotation;

    internal MenuItem TrimeshItem => _trimesh;

    internal MenuItem ConvexItem => _convex;

    /// <summary>
    /// Lock and group act on an entity that has a place, so they are off for any other and say why. They are lit
    /// for the flags the entity holds. The chip names the entity, or counts the picked ones when there are several,
    /// and Make it solid is for a mesh, so it is shown for that class alone. Pass null when nothing is selected.
    /// </summary>
    public void Select(OutlineNode? selected, int picked)
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
            ShowName(selected, picked);
        }
    }

    // An icon draws one name for good, so a new selection puts a new icon in its place.
    void ShowName(OutlineNode selected, int picked)
    {
        _named.Children[0] = ClassMark(selected.ClassName);
        _name.Text = picked > 1 ? $"{picked} selected" : selected.Name;
        _className.Text = selected.ClassName;
        _resetMenu.Items[0] = Parts.MenuHeading($"Put {selected.Name} back");
        _solidMenu.Items[0] = Parts.MenuHeading($"Give {selected.Name} a collision shape");
    }

    void Light(FixedToggle lit)
    {
        foreach (var mode in _modes)
        {
            mode.IsChecked = mode == lit;
        }
    }

    /// <summary>A menu of the entities under the cursor, nearest first, each with its class. A click on one chooses it.</summary>
    public static MenuFlyout PickList(IEnumerable<OutlineNode> under, Action<nint> choose)
    {
        var list = new MenuFlyout { Items = { Parts.MenuHeading("Pick from what is under the cursor") } };
        foreach (var node in under)
        {
            var className = new TextBlock { Text = node.ClassName, FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(Gaps.Dialog, 0, 0, 0) };
            DockPanel.SetDock(className, Dock.Right);
            var item = new MenuItem { Icon = ClassMark(node.ClassName), Header = new DockPanel { MinWidth = 220, Children = { className, new TextBlock { Text = node.Name } } } };
            item.Click += (_, _) => choose(node.Handle);
            list.Items.Add(item);
        }
        return list;
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

    // The cards give each tool its word, its key and what it does. Lock and Group say why while they are off.
    void DressTools(string hold, KeyModifiers command)
    {
        Parts.Dress(_select, "ToolSelect", new HoverCard("Pick", "Click a thing to pick it, with no handles in the way.", "Q", "Select mode"));
        Parts.Dress(_move, "ToolMove", new HoverCard("Move", $"Drag an arrow to move the picked thing. Hold {hold} to snap.", "W", "Move mode"));
        Parts.Dress(_rotate, "ToolRotate", new HoverCard("Turn", $"Drag a ring to turn the picked thing. Hold {hold} to snap.", "E", "Rotate mode"));
        Parts.Dress(_scale, "ToolScale", new HoverCard("Resize", $"Drag a handle to make the picked thing bigger or smaller. Hold {hold} to snap.", "R", "Scale mode"));
        Parts.Dress(_more, "GuiTabMenuHl", new HoverCard("More", "All handles at once, free turn and the tools you need less often."));
        Parts.Dress(_local, "Object", new HoverCard("Local", "The arrows follow the picked thing's own turn, not the world's.", "T", "Local coordinates"));
        Parts.Dress(_snap, "Snap", new HoverCard("Snap", "Moves and turns go in fixed steps. Click the arrow to set the steps.", "Y", "Use Snap"));
        new HoverCard("Snap steps", "Set how far one step moves, turns and resizes.").On(_snapOptions);
        var locks = new HoverCard("Lock", "Stops the picked thing being moved by accident.", Parts.KeyText(new KeyGesture(Key.L, command)), "Lock");
        locks.SaysWhyOff(Parts.Dress(_lock, "Lock", locks), "Pick a thing you can move first. Lock stops it being moved by accident.");
        var groups = new HoverCard("Group", "A click on a part picks this thing, so the parts move as one.", Parts.KeyText(new KeyGesture(Key.G, command)), "Group");
        groups.SaysWhyOff(Parts.Dress(_group, "Group", groups), "Pick a thing you can move first. Group makes its parts move as one.");
        Parts.Dress(_ruler, "Ruler", new HoverCard("Measure", "Click two points to see how far apart they are."));
    }

    // The switches, the three steps and the key that flips Snap for one drag. Done closes it, as Escape does.
    Flyout SnapSteps(string hold)
    {
        var done = new Button { Classes = { EditorTheme.Ghost }, Content = Parts.Keyed("Done", "esc"), HorizontalAlignment = HorizontalAlignment.Right };
        var popover = CardHead.Popover("Snap steps", "Moves, turns and resizes go in these steps while Snap is on.", 300, new StackPanel
        {
            Spacing = Gaps.Item,
            Children =
            {
                SwitchRow("Snap moving", _snapPosition),
                SwitchRow("Snap turning", _snapRotation),
                new Border { Height = 1, Margin = new Thickness(0, Gaps.Tight), Background = EditorBrushes.Line },
                StepRow("Move by", _snapMove),
                StepRow("Turn by", _snapTurn),
                StepRow("Resize by", _snapScale),
                new TextBlock { Text = $"Hold {hold} while you drag to flip Snap for that drag only.", FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap },
                done,
            },
        });
        done.Click += (_, _) => popover.Hide();
        return popover;
    }

    // The chip at the bottom left. Zoom to it and Put it back act on an entity with a place in 3D, which is when
    // the window turns Focus on, so they follow it and say why while it is off.
    Border ChipFor(Button focus)
    {
        var zoom = new HoverCard("Zoom to it", "Fills the view with the picked thing.", "F", "Focus Selection");
        zoom.SaysWhyOff(Parts.Dress(_focus, "FocusFrame", zoom), "This thing has no place in the view to zoom to.");
        var back = new HoverCard("Put it back", "Clears a move, turn or resize, one at a time or all together.", Parts.KeyText(new KeyGesture(Key.R, KeyModifiers.Alt)), "Reset transform");
        back.SaysWhyOff(Parts.Dress(_reset, "ResetTurn", back), "This thing has no position, turn or size to put back.");
        new HoverCard("Make it solid", "Gives the mesh a collision shape, so other things bump into it.", engine: "Create collision shape").On(_collision);
        _focus.Click += (_, _) => focus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach (var control in new Control[] { _focus, _reset, _resetAll })
        {
            control.Bind(IsEnabledProperty, focus.GetObservable(IsEnabledProperty));
        }
        _resetMenu.Items.Add(Parts.MenuHeading("Put it back"));
        _resetMenu.Items.Add(_resetPosition);
        _resetMenu.Items.Add(_resetRotation);
        _resetMenu.Items.Add(_resetScale);
        _resetMenu.Items.Add(new Separator());
        _resetMenu.Items.Add(Parts.PressItem("Put all back", _resetAll, new KeyGesture(Key.R, KeyModifiers.Alt), "Position, turn and size together"));
        _reset.Flyout = _resetMenu;
        _solidMenu.Items.Add(Parts.MenuHeading("Give it a collision shape"));
        _solidMenu.Items.Add(_convex);
        _solidMenu.Items.Add(_trimesh);
        _collision.Flyout = _solidMenu;
        _named.Children.AddRange([ClassMark("Entity"), _name, _className]);
        var chip = Parts.Float([_named, Parts.Rule(), _collision, _collisionRule, _focus, _reset]);
        // The chip keeps its 36 pixel height when the 28 pixel Make it solid tab is hidden.
        chip.Height = 36;
        chip.HorizontalAlignment = HorizontalAlignment.Left;
        chip.VerticalAlignment = VerticalAlignment.Bottom;
        chip.IsVisible = false;
        return chip;
    }

    // Make it solid: a dim mark, the words and a chevron, 28 high so it fills the chip.
    static Button SolidTab()
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
                Spacing = Gaps.Tight,
                Children = { mark, new TextBlock { Text = "Make it solid", FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center }, chevron },
            },
            Height = 28,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(Gaps.Item, 0),
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
    }

    static MenuItem FitItem(string name, string note, string engine)
    {
        var item = new MenuItem { Header = Parts.Noted(name, note, engine) };
        AutomationProperties.SetName(item, name);
        return item;
    }

    // A More item shows the light of its hidden button, and a press on the item presses the button. The tick is set
    // again after the press, so it is right whether or not the menu flips it first.
    static MenuItem ToolItem((string Name, string Engine) name, string note, FixedToggle tool, MenuItemToggleType type)
    {
        var item = new MenuItem { Header = Parts.Noted(name.Name, note, name.Engine), ToggleType = type, IsChecked = tool.IsChecked == true };
        AutomationProperties.SetName(item, name.Name);
        item.Click += (_, _) =>
        {
            tool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            item.IsChecked = tool.IsChecked == true;
        };
        tool.IsCheckedChanged += (_, _) => item.IsChecked = tool.IsChecked == true;
        return item;
    }

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
            Margin = new Thickness(0, 0, Gaps.Item, 0),
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
