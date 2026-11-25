using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The top of a dialog or a popover: the title with the engine's name beside it in small dim mono, a line on what
/// it is for, and a cross at the top right. The owner decides what the cross does.
/// </summary>
internal sealed class CardHead : DockPanel
{
    readonly TextBlock _engine;

    public CardHead(string title, string why)
    {
        Heading = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold };
        _engine = new TextBlock { FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        Why = new TextBlock { Text = why, FontSize = 12, Foreground = EditorBrushes.TextDim, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
        // The design draws this cross at 14 in the usual 26 pixel button.
        CloseButton = Parts.IconButton("Close", "Close");
        CloseButton.Content = new EditorIcon("Close") { Width = 14, Height = 14 };
        CloseButton.Padding = new Thickness(6);
        CloseButton.VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(14, 12, 12, 0);
        SetDock(CloseButton, Dock.Right);
        Children.Add(CloseButton);
        Children.Add(new StackPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Heading, _engine } }, Why } });
    }

    /// <summary>The engine's name for what the card does. Empty shows none.</summary>
    internal string EngineName
    {
        get => _engine.Text ?? "";
        set
        {
            _engine.Text = value;
            _engine.IsVisible = value != "";
        }
    }

    internal TextBlock Heading { get; }

    internal TextBlock Why { get; }

    internal Button CloseButton { get; }

    /// <summary>A popover of this width with the head over the body. The cross closes it.</summary>
    public static Flyout Popover(string title, string why, double width, Control body)
    {
        var head = new CardHead(title, why);
        var popover = new Flyout
        {
            FlyoutPresenterClasses = { EditorTheme.Flush },
            Content = new StackPanel { Width = width, Children = { head, new Border { Padding = new Thickness(14, 12), Child = body } } },
        };
        head.CloseButton.Click += (_, _) => popover.Hide();
        return popover;
    }
}
