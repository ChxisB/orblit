using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What floats over the top right of the viewport: a pill with the projection button, the preview buttons and the
/// preview settings, and the axes under it. The projection button opens a menu of Perspective, Orthogonal and Focus
/// Selection, and its label shows the projection in force. The previews are not built, so their buttons and the
/// settings popover say so. The overlay has no fill, so a press on any other point goes to the viewport under it.
/// </summary>
internal sealed class ViewportOverlay : Grid
{
    readonly TextBlock _label = new() { Text = "Perspective", VerticalAlignment = VerticalAlignment.Center };
    readonly Button _projection;
    readonly MenuItem _perspective = new() { Header = "Perspective", ToggleType = MenuItemToggleType.Radio, IsChecked = true };
    readonly MenuItem _orthogonal = new() { Header = "Orthogonal", ToggleType = MenuItemToggleType.Radio };
    readonly MenuItem _focus;
    readonly Button _previewSettings = Parts.Tight(Parts.IconButton("GuiDropdown", "Preview settings"));

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
            Tool("PreviewSun", "Sun preview", lit: false),
            Tool("PreviewEnvironment", "Environment preview", lit: true),
            _previewSettings,
        ]);
        _previewSettings.Flyout = CardHead.Popover("Preview settings", "What the viewport adds when the scene has no light or sky.", 300, new StackPanel
        {
            Spacing = 10,
            Children = { SoonRow("PreviewSun", "Sun preview"), SoonRow("PreviewEnvironment", "Environment preview") },
        });
        View.HorizontalAlignment = HorizontalAlignment.Right;
        _corner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Children = { View, _axes } };
        Children.Add(_corner);
    }

    /// <summary>Raised with true when the user chose Orthogonal and false for Perspective. The menu and the label already show it.</summary>
    public event Action<bool>? ProjectionChanged;

    /// <summary>The pill at the top right.</summary>
    internal Border View { get; }

    internal Button ProjectionButton => _projection;

    internal MenuItem PerspectiveItem => _perspective;

    internal MenuItem OrthogonalItem => _orthogonal;

    internal MenuItem FocusItem => _focus;

    internal Button PreviewSettingsButton => _previewSettings;

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

    static FixedToggle Tool(string icon, string name, bool lit) => Unbuilt.Mark(new FixedToggle(icon, name, lit), $"{name} is not built yet.");

    // A setting that is not built shows Soon where its switch will go.
    static DockPanel SoonRow(string icon, string label)
    {
        var soon = new TextBlock { Text = "Soon", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.Warn, VerticalAlignment = VerticalAlignment.Center };
        var mark = new EditorIcon(icon) { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(soon, Dock.Right);
        DockPanel.SetDock(mark, Dock.Left);
        return new DockPanel { Children = { soon, mark, new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
    }

    void Choose(bool orthogonal)
    {
        _perspective.IsChecked = !orthogonal;
        _orthogonal.IsChecked = orthogonal;
        _label.Text = orthogonal ? "Orthogonal" : "Perspective";
        ProjectionChanged?.Invoke(orthogonal);
    }
}
