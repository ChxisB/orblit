using Avalonia.Controls;
using Avalonia.Interactivity;

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

    /// <summary>Makes a click on the button raise <see cref="Event"/> with the answer to show.</summary>
    public static T Mark<T>(T button, string message) where T : Button
    {
        button.Click += (_, _) => button.RaiseEvent(new UnbuiltEventArgs(message));
        return button;
    }
}
