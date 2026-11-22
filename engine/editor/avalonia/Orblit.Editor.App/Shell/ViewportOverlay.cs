using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What floats over the top right of the viewport: a pill with the projection button, the preview buttons and the
/// preview settings, and the axes under it. The projection button opens a menu of Perspective, Orthogonal and Focus
/// Selection, and its label shows the projection in force. The preview buttons and the switches in the settings
/// popover turn the preview sun and sky on and off, and stay in step. The overlay has no fill, so a press on any
/// other point goes to the viewport under it.
/// </summary>
internal sealed class ViewportOverlay : Grid
{
    readonly TextBlock _label = new() { Text = "Perspective", VerticalAlignment = VerticalAlignment.Center };
    readonly Button _projection;
    readonly MenuItem _perspective = new() { Header = "Perspective", ToggleType = MenuItemToggleType.Radio, IsChecked = true };
    readonly MenuItem _orthogonal = new() { Header = "Orthogonal", ToggleType = MenuItemToggleType.Radio };
    readonly MenuItem _focus;
    readonly Button _previewSettings = Parts.Tight(Parts.IconButton("GuiDropdown", "Preview settings"));
    readonly FixedToggle _sun = new("PreviewSun", "Sun preview", lit: true);
    readonly FixedToggle _environment = new("PreviewEnvironment", "Environment preview", lit: true);
    readonly ToggleSwitch _sunSwitch = Switch();
    readonly ToggleSwitch _environmentSwitch = Switch();

    readonly AxisGizmo _axes = new() { HorizontalAlignment = HorizontalAlignment.Right };
    readonly StackPanel _corner;

    public ViewportOverlay(Button focus)
    {
        _focus = Parts.PressItem("Focus Selection", focus);
        _focus.Icon = new EditorIcon("FocusFrame");
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(chevron, EditorBrushes.TextDim);
        _projection = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _label, chevron } },
            Height = 26,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(10, 0, 6, 0),
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Flyout = new MenuFlyout { Items = { Parts.MenuHeading("Projection"), _perspective, _orthogonal, new Separator(), _focus } },
        };
        _perspective.Click += (_, _) => Choose(false);
        _orthogonal.Click += (_, _) => Choose(true);
        View = Parts.Float(
        [
            _projection,
            Parts.Rule(),
            _sun,
            _environment,
            _previewSettings,
        ]);
        _previewSettings.Flyout = CardHead.Popover("Preview settings", "What the viewport adds when the scene has no light or sky.", 300, new StackPanel
        {
            Spacing = 10,
            Children = { SwitchRow("PreviewSun", "Sun preview", _sunSwitch), SwitchRow("PreviewEnvironment", "Environment preview", _environmentSwitch) },
        });
        _sun.Click += (_, _) => ShowPreviews(!Sun, Environment);
        _environment.Click += (_, _) => ShowPreviews(Sun, !Environment);
        _sunSwitch.IsCheckedChanged += (_, _) => ShowPreviews(_sunSwitch.IsChecked == true, Environment);
        _environmentSwitch.IsCheckedChanged += (_, _) => ShowPreviews(Sun, _environmentSwitch.IsChecked == true);
        View.HorizontalAlignment = HorizontalAlignment.Right;
        _corner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Children = { View, _axes } };
        Children.Add(_corner);
    }

    /// <summary>Raised with true when the user chose Orthogonal and false for Perspective. The menu and the label already show it.</summary>
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

    static ToggleSwitch Switch() => new() { IsChecked = true, OnContent = null, OffContent = null, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };

    static DockPanel SwitchRow(string icon, string label, ToggleSwitch toggle)
    {
        var mark = new EditorIcon(icon) { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(toggle, Dock.Right);
        DockPanel.SetDock(mark, Dock.Left);
        AutomationProperties.SetName(toggle, label);
        return new DockPanel { Children = { toggle, mark, new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
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
        _label.Text = orthogonal ? "Orthogonal" : "Perspective";
        ProjectionChanged?.Invoke(orthogonal);
    }
}
