using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>Raised by a control that the shell shows before its feature exists, so a click gets an answer.</summary>
internal sealed class UnbuiltEventArgs : RoutedEventArgs
{
    public UnbuiltEventArgs(string message) : base(Unbuilt.Event)
    {
        Message = message;
    }

    public string Message { get; }
}

internal static class Unbuilt
{
    public static readonly RoutedEvent<UnbuiltEventArgs> Event =
        RoutedEvent.Register<Control, UnbuiltEventArgs>("Unbuilt", RoutingStrategies.Bubble);

    public const string Soon = "Not built yet";

    /// <summary>Off, and says why when the pointer rests on it. For a surface with no toast host to answer a click.</summary>
    public static void Disable(Control control)
    {
        control.IsEnabled = false;
        ToolTip.SetTip(control, Soon);
        ToolTip.SetShowOnDisabled(control, true);
    }

    /// <summary>A tool of a viewport pill that is not built. Lit shows it on, as the design draws it.</summary>
    public static FixedToggle Tool(string icon, string name, bool lit = false) => Mark(new FixedToggle(icon, name, lit), $"{name} is not built yet.");

    /// <summary>
    /// Makes a click on the button raise <see cref="Event"/> with the answer to show. An icon button also dims
    /// and takes a small warn dot at its top right, so it reads as not built before anyone clicks it.
    /// </summary>
    public static T Mark<T>(T button, string message) where T : Button
    {
        button.Click += (_, _) => button.RaiseEvent(new UnbuiltEventArgs(message));
        if (button.Content is EditorIcon icon)
        {
            // The icon leaves the button before the panel takes it, because a control has one logical parent.
            button.Content = null;
            button.Content = new Panel { Children = { icon, Dot() } };
            // A lit toggle keeps the accent, because it shows a mode the engine draws today.
            if (button is not ToggleButton { IsChecked: true })
            {
                button.Foreground = EditorBrushes.Unbuilt;
            }
        }

        return button;
    }

    // 4 pixels square, 4 in from the top right of a 26 pixel button, so it hangs a pixel past the icon.
    static Ellipse Dot() => new()
    {
        Width = 4,
        Height = 4,
        Fill = EditorBrushes.Warn,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, -1, -1, 0),
    };
}
