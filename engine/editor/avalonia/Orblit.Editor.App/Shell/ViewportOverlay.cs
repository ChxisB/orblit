using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What floats over the top right of the viewport: a pill with the projection button, the Sun and Sky previews with
/// their popover and Zoom to it, and the axes under it. The projection button opens a menu of Perspective, Flat and
/// Zoom to it, and its label shows the projection in force. The preview buttons and the switches in the popover
/// turn the preview sun and sky on and off, and stay in step. The overlay has no fill, so a press on any other
/// point goes to the viewport under it.
/// </summary>
internal sealed class ViewportOverlay : Grid
{
    readonly TextBlock _label = new() { Text = "Perspective", VerticalAlignment = VerticalAlignment.Center };
    readonly Button _projection;
    readonly MenuItem _perspective = Choice("Perspective", "Things far away look smaller, like a camera", null, on: true);
    readonly MenuItem _orthogonal = Choice("Flat", "Sizes stay the same at any distance. Good for lining things up", "Orthogonal", on: false);
    readonly MenuItem _focus;
    readonly Button _zoomTo = new();
    readonly Button _previewSettings = Parts.Tight(Parts.IconButton("GuiDropdown", "Preview sun and sky"));
    readonly FixedToggle _sun = new("PreviewSun", "Sun", lit: true);
    readonly FixedToggle _environment = new("PreviewEnvironment", "Sky", lit: true);
    readonly ToggleSwitch _sunSwitch = Switch();
    readonly ToggleSwitch _environmentSwitch = Switch();

    readonly AxisGizmo _axes = new() { HorizontalAlignment = HorizontalAlignment.Right };
    readonly StackPanel _corner;

    public ViewportOverlay(Button focus)
    {
        _focus = Parts.PressItem("Zoom to it", focus, new KeyGesture(Key.F), "Fills the view with the picked thing");
        _focus.Icon = new EditorIcon("FocusFrame");
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        _projection = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tight, Children = { _label, chevron } },
            Height = 26,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(Gaps.Item, 0, Gaps.Tight, 0),
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Flyout = new MenuFlyout { Items = { _perspective, _orthogonal, new Separator(), _focus } },
        };
        new HoverCard("Perspective or flat", "Whether things far away look smaller.", engine: "Projection").On(_projection);
        _perspective.Click += (_, _) => Choose(false);
        _orthogonal.Click += (_, _) => Choose(true);
        var zoom = new HoverCard("Zoom to it", "Fills the view with the picked thing.", "F", "Focus Selection");
        zoom.SaysWhyOff(Parts.Dress(_zoomTo, "FocusFrame", zoom), "Pick a thing in the view first.");
        _zoomTo.Click += (_, _) => focus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        _zoomTo.Bind(IsEnabledProperty, focus.GetObservable(IsEnabledProperty));
        View = Parts.Float(
        [
            _projection,
            Parts.Rule(),
            _sun,
            _environment,
            _previewSettings,
            Parts.Rule(),
            _zoomTo,
        ]);
        WirePreviews();
        View.HorizontalAlignment = HorizontalAlignment.Right;
        _corner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Children = { View, _axes } };
        Children.Add(_corner);
    }

    /// <summary>Raised with true when the user chose Flat and false for Perspective. The menu and the label already show it.</summary>
    public event Action<bool>? ProjectionChanged;

    /// <summary>Raised with whether the sun and the sky preview are on, after the buttons and switches show it.</summary>
    public event Action<bool, bool>? PreviewsChanged;

    /// <summary>The pill at the top right.</summary>
    internal Border View { get; }

    internal Button ProjectionButton => _projection;

    internal MenuItem PerspectiveItem => _perspective;

    internal MenuItem OrthogonalItem => _orthogonal;

    internal MenuItem FocusItem => _focus;

    internal Button PreviewSettingsButton => _previewSettings;

    /// <summary>Zoom to it in the pill, which presses the window's Focus Selection button.</summary>
    internal Button ZoomToButton => _zoomTo;

    internal FixedToggle SunButton => _sun;

    internal FixedToggle EnvironmentButton => _environment;

    internal ToggleSwitch SunSwitch => _sunSwitch;

    internal ToggleSwitch EnvironmentSwitch => _environmentSwitch;

    internal bool Sun => _sun.IsChecked == true;

    internal bool Environment => _environment.IsChecked == true;

    internal string ProjectionLabel => _label.Text!;

    internal AxisGizmo Axes => _axes;

    /// <summary>True in the 2D view, which has no projection, previews or axes, so the overlay hides them.</summary>
    internal bool Planar
    {
        get => !_corner.IsVisible;
        set => _corner.IsVisible = !value;
    }

    /// <summary>Turns the axes with the camera.</summary>
    public void Orbit(float yaw, float pitch) => _axes.Orbit(yaw, pitch);

    // The Sun and Sky buttons and the switches in their popover. Each flips its preview, and the overlay keeps
    // the buttons and switches in step.
    void WirePreviews()
    {
        Parts.Dress(_sun, "PreviewSun", new HoverCard("Sun", "A preview light while your scene has none. The game never sees it.", engine: "Preview sun"));
        Parts.Dress(_environment, "PreviewEnvironment", new HoverCard("Sky", "A preview sky while your scene has none. The game never sees it.", engine: "Preview environment"));
        new HoverCard("Preview sun and sky", "Turn the preview light and sky on or off.").On(_previewSettings);
        _previewSettings.Flyout = CardHead.Popover("Preview sun and sky", "Used only while your scene has no light or sky of its own. The game never sees them.", 380, new StackPanel
        {
            Spacing = Gaps.Group,
            Children =
            {
                SwitchRow("PreviewSun", ("Sun", "A soft light from above, so shapes are easy to see."), _sunSwitch),
                SwitchRow("PreviewEnvironment", ("Sky", "A plain sky behind everything."), _environmentSwitch),
            },
        });
        _sun.Click += (_, _) => ShowPreviews(!Sun, Environment);
        _environment.Click += (_, _) => ShowPreviews(Sun, !Environment);
        _sunSwitch.IsCheckedChanged += (_, _) => ShowPreviews(_sunSwitch.IsChecked == true, Environment);
        _environmentSwitch.IsCheckedChanged += (_, _) => ShowPreviews(Sun, _environmentSwitch.IsChecked == true);
    }

    static ToggleSwitch Switch() => new() { IsChecked = true, OnContent = null, OffContent = null, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };

    // An icon, the name over a dim line, and the switch at the right.
    static DockPanel SwitchRow(string icon, (string Name, string Line) label, ToggleSwitch toggle)
    {
        var mark = new EditorIcon(icon) { Margin = new Thickness(0, 0, Gaps.Item, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(toggle, Dock.Right);
        DockPanel.SetDock(mark, Dock.Left);
        AutomationProperties.SetName(toggle, label.Name);
        return new DockPanel { Children = { toggle, mark, Parts.Noted(label.Name, label.Line) } };
    }

    // A projection with a note on when to use it and the engine's name where it is not the same word.
    static MenuItem Choice(string name, string note, string? engine, bool on)
    {
        var item = new MenuItem { Header = Parts.Noted(name, note, engine), ToggleType = MenuItemToggleType.Radio, IsChecked = on };
        AutomationProperties.SetName(item, name);
        return item;
    }

    // The overlay keeps the flags on its buttons. A switch set here raises its own change, which finds nothing new.
    void ShowPreviews(bool sun, bool environment)
    {
        if (sun == Sun && environment == Environment)
        {
            return;
        }

        _sun.IsChecked = sun;
        _environment.IsChecked = environment;
        _sunSwitch.IsChecked = sun;
        _environmentSwitch.IsChecked = environment;
        PreviewsChanged?.Invoke(sun, environment);
    }

    void Choose(bool orthogonal)
    {
        _perspective.IsChecked = !orthogonal;
        _orthogonal.IsChecked = orthogonal;
        _label.Text = orthogonal ? "Flat" : "Perspective";
        ProjectionChanged?.Invoke(orthogonal);
    }
}
