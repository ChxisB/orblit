using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What floats over the viewport: the projection button at the top left and the axes at the top right. The button
/// opens a menu of Perspective and Orthogonal, and the label shows the one in force. The overlay has no fill, so a
/// press on any other point goes to the viewport under it.
/// </summary>
internal sealed class ViewportOverlay : Grid
{
    // 10 pixels in from the corner, and the axes' middle is 50 from the top and the right edge.
    const double Inset = 10;

    static readonly IBrush Scrim = new ImmutableSolidColorBrush(Color.FromArgb(0x5c, 0, 0, 0));

    readonly TextBlock _label = new() { Text = "Perspective", VerticalAlignment = VerticalAlignment.Center };
    readonly Button _projection;
    readonly MenuItem _perspective = new() { Header = "Perspective", ToggleType = MenuItemToggleType.Radio, IsChecked = true };
    readonly MenuItem _orthogonal = new() { Header = "Orthogonal", ToggleType = MenuItemToggleType.Radio };

    readonly AxisGizmo _axes = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };

    public ViewportOverlay()
    {
        _projection = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children = { new EditorIcon("GuiTabMenuHl") { VerticalAlignment = VerticalAlignment.Center }, _label },
            },
            Height = 32,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(8, 0, 9, 0),
            BorderThickness = new Thickness(0),
            Background = Scrim,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Inset),
            Flyout = new MenuFlyout { Items = { _perspective, _orthogonal } },
        };
        _perspective.Click += (_, _) => Choose(false);
        _orthogonal.Click += (_, _) => Choose(true);
        Children.Add(_projection);
        Children.Add(_axes);
    }

    /// <summary>Raised with true when the user chose Orthogonal and false for Perspective. The menu and the label already show it.</summary>
    public event Action<bool>? ProjectionChanged;

    internal Button ProjectionButton => _projection;

    internal MenuItem PerspectiveItem => _perspective;

    internal MenuItem OrthogonalItem => _orthogonal;

    internal string ProjectionLabel => _label.Text!;

    internal AxisGizmo Axes => _axes;

    /// <summary>True in the 2D view, which has no projection and no axes, so the overlay hides them.</summary>
    internal bool Planar
    {
        get => !_projection.IsVisible;
        set
        {
            _projection.IsVisible = !value;
            _axes.IsVisible = !value;
        }
    }

    /// <summary>Turns the axes with the camera.</summary>
    public void Orbit(float yaw, float pitch) => _axes.Orbit(yaw, pitch);

    void Choose(bool orthogonal)
    {
        _perspective.IsChecked = !orthogonal;
        _orthogonal.IsChecked = orthogonal;
        _label.Text = orthogonal ? "Orthogonal" : "Perspective";
        ProjectionChanged?.Invoke(orthogonal);
    }
}
