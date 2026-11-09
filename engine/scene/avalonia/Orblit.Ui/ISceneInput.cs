using System.Numerics;

namespace Orblit.Ui;

/// <summary>
/// Where input the UI does not take goes, for example the engine's input singleton. Values are the engine's:
/// key codes, button indexes and positions in pixels from the top left.
/// </summary>
public interface ISceneInput
{
    void Key(long keycode, bool pressed);

    void MouseButton(long button, Vector2 position, bool pressed);

    void MouseMove(Vector2 position, Vector2 relative);
}
