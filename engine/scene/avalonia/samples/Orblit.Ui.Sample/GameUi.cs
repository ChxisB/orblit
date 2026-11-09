using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Ui.Sample;

/// <summary>
/// A health bar at the top left, a score at the top right and a pause menu in the middle. The tree paints
/// nothing else, so the scene behind a <see cref="UiLayer"/> shows through everywhere but the controls.
/// </summary>
public sealed class GameUi
{
    public ProgressBar Health { get; } = new() { Minimum = 0, Maximum = 100, Value = 100, Width = 200 };

    public TextBlock Score { get; } = new() { Text = "Score 0", Foreground = Brushes.White, FontSize = 20 };

    public Button Resume { get; } = new() { Content = "Resume", HorizontalAlignment = HorizontalAlignment.Stretch };

    public Button Quit { get; } = new() { Content = "Quit", HorizontalAlignment = HorizontalAlignment.Stretch };

    public Border Menu { get; }

    public Control Root { get; }

    public GameUi()
    {
        var hud = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { new TextBlock { Text = "Health", Foreground = Brushes.White }, Health },
        };
        Score.Margin = new Thickness(16);
        Score.HorizontalAlignment = HorizontalAlignment.Right;
        Score.VerticalAlignment = VerticalAlignment.Top;
        Menu = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x20, 0x28, 0x30)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { new TextBlock { Text = "Paused", Foreground = Brushes.White, FontSize = 24 }, Resume, Quit },
            },
        };
        Root = new Grid { Children = { hud, Score, Menu } };
    }
}
