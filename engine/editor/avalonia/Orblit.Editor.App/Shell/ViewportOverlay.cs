using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What floats over the top right of the viewport: a pill with the projection button and the preview buttons, and
/// the axes under it. The projection button opens a menu of Perspective and Orthogonal, and its label shows the one
/// in force. The overlay has no fill, so a press on any other point goes to the viewport under it.
/// </summary>
internal sealed class ViewportOverlay : Grid
{
    readonly TextBlock _label = new() { Text = "Perspective", VerticalAlignment = VerticalAlignment.Center };
    readonly Button _projection;
    readonly MenuItem _perspective = new() { Header = "Perspective", ToggleType = MenuItemToggleType.Radio, IsChecked = true };
    readonly MenuItem _orthogonal = new() { Header = "Orthogonal", ToggleType = MenuItemToggleType.Radio };

    readonly AxisGizmo _axes = new() { HorizontalAlignment = HorizontalAlignment.Right };
    readonly StackPanel _corner;

    public ViewportOverlay()
    {
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
            Flyout = new MenuFlyout { Items = { _perspective, _orthogonal } },
        };
        _perspective.Click += (_, _) => Choose(false);
        _orthogonal.Click += (_, _) => Choose(true);
        View = Parts.Float(
        [
            _projection,
            Parts.Rule(),
            Tool("PreviewSun", "Sun preview", lit: false),
            Tool("PreviewEnvironment", "Environment preview", lit: true),
            Parts.Tight(Unbuilt.Mark(Parts.IconButton("GuiDropdown", "Preview settings"), "Preview settings are not built yet.")),
        ]);
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

    void Choose(bool orthogonal)
    {
        _perspective.IsChecked = !orthogonal;
        _orthogonal.IsChecked = orthogonal;
        _label.Text = orthogonal ? "Orthogonal" : "Perspective";
        ProjectionChanged?.Invoke(orthogonal);
    }
}
