using Avalonia.Input;

namespace Orblit.Ui;

/// <summary>Turns the engine's key codes and button indexes into Avalonia's. Anything unmapped answers null.</summary>
internal static class EngineInput
{
    // The engine's special keys count up from here, and its letters and digits are their ASCII codes.
    const long Special = 1L << 22;

    public static Key? ToKey(long keycode) => keycode switch
    {
        >= 'A' and <= 'Z' => Key.A + (int)(keycode - 'A'),
        >= '0' and <= '9' => Key.D0 + (int)(keycode - '0'),
        ' ' => Key.Space,
        Special + 1 => Key.Escape,
        Special + 2 => Key.Tab,
        Special + 4 => Key.Back,
        Special + 5 or Special + 6 => Key.Enter,
        Special + 8 => Key.Delete,
        Special + 13 => Key.Home,
        Special + 14 => Key.End,
        Special + 15 => Key.Left,
        Special + 16 => Key.Up,
        Special + 17 => Key.Right,
        Special + 18 => Key.Down,
        Special + 19 => Key.PageUp,
        Special + 20 => Key.PageDown,
        _ => null,
    };

    // shortcut: wheel and extra buttons have no mapping, so a control swallows them. Add a wheel call to IUiInput when a list needs scrolling.
    public static MouseButton? ToButton(long button) => button switch
    {
        1 => MouseButton.Left,
        2 => MouseButton.Right,
        3 => MouseButton.Middle,
        _ => null,
    };
}
