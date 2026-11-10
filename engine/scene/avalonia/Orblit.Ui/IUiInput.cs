using Avalonia;
using Avalonia.Input;

namespace Orblit.Ui;

/// <summary>What the engine's input events call. Points are in pixels from the top left.</summary>
public interface IUiInput
{
    void PointerMoved(Point point);

    void PointerButton(Point point, MouseButton button, bool pressed);

    void Key(Key key, bool pressed);

    void Text(string text);

    /// <summary>
    /// True when a click at this point lands on a control, so the scene must not see it.
    /// Empty space answers false. The answer holds for the last rendered frame.
    /// </summary>
    bool WantsPointer(Point point);

    /// <summary>True while a text box has the keyboard focus, so keys and text belong to the UI.</summary>
    bool WantsKeys { get; }
}
