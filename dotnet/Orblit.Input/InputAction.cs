namespace Orblit.Input;

/// <summary>What shape of answer an action gives.</summary>
public enum ActionKind
{
    /// <summary>Down or up, with the edges and gestures of a button.</summary>
    Button,

    /// <summary>One number from -1 to 1.</summary>
    Axis,

    /// <summary>Two numbers, as a vector no longer than 1.</summary>
    Axis2,
}

public static class ActionKindExtensions
{
    /// <summary>Whether <paramref name="control"/> can feed an action of this kind. Only a two-axis action takes a whole stick.</summary>
    public static bool Accepts(this ActionKind kind, Control control) =>
        control is not PadStickControl || kind == ActionKind.Axis2;
}

/// <summary>How long a press must last, or how soon the next must come, for the gestures of a button.</summary>
public sealed class Gestures(TimeSpan? tap = null, TimeSpan? doubleTap = null, TimeSpan? hold = null)
{
    /// <summary>A press released within this long is a tap.</summary>
    public TimeSpan Tap { get; } = tap ?? TimeSpan.FromMilliseconds(250);

    /// <summary>A press that starts within this long of a tap's release is a double tap.</summary>
    public TimeSpan DoubleTap { get; } = doubleTap ?? TimeSpan.FromMilliseconds(300);

    /// <summary>A press still down after this long is a hold.</summary>
    public TimeSpan Hold { get; } = hold ?? TimeSpan.FromMilliseconds(400);
}

/// <summary>Something a player can do, named by the game.</summary>
public sealed class InputAction
{
    private InputAction(string name, ActionKind kind, Gestures gestures)
    {
        Name = name;
        Kind = kind;
        Gestures = gestures;
    }

    public string Name { get; }

    public ActionKind Kind { get; }

    /// <summary>Only a button reads these.</summary>
    public Gestures Gestures { get; }

    public static InputAction Button(string name, Gestures? gestures = null) =>
        new(name, ActionKind.Button, gestures ?? new Gestures());

    public static InputAction Axis(string name) => new(name, ActionKind.Axis, new Gestures());

    public static InputAction Axis2(string name) => new(name, ActionKind.Axis2, new Gestures());
}
