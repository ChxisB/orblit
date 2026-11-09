using System.Numerics;
using Avalonia;

namespace Orblit.Ui;

/// <summary>
/// Sends each engine input event to the UI or to the scene, never to both. A press goes to the UI when it lands
/// on a control, and its release goes to the same side, so a drag that leaves the control does not stick.
/// </summary>
public sealed class UiInputRouter(IUiInput ui, ISceneInput scene)
{
    readonly HashSet<long> _uiButtons = [];
    readonly HashSet<long> _uiKeys = [];

    public void MouseMove(Vector2 position, Vector2 relative)
    {
        var point = ToPoint(position);
        ui.PointerMoved(point);
        // shortcut: a scene drag that crosses a control loses its moves over it. Track the scene's presses if that matters.
        if (_uiButtons.Count == 0 && !ui.WantsPointer(point))
        {
            scene.MouseMove(position, relative);
        }
    }

    public void MouseButton(long button, Vector2 position, bool pressed)
    {
        var point = ToPoint(position);
        if (!(pressed ? ui.WantsPointer(point) : _uiButtons.Contains(button)))
        {
            scene.MouseButton(button, position, pressed);
            return;
        }

        Track(_uiButtons, button, pressed);
        if (EngineInput.ToButton(button) is { } mapped)
        {
            ui.PointerButton(point, mapped, pressed);
        }
    }

    public void Key(long keycode, bool pressed)
    {
        if (!(pressed ? ui.WantsKeys : _uiKeys.Contains(keycode)))
        {
            scene.Key(keycode, pressed);
            return;
        }

        Track(_uiKeys, keycode, pressed);
        if (EngineInput.ToKey(keycode) is { } mapped)
        {
            ui.Key(mapped, pressed);
        }
    }

    public void Text(string text)
    {
        if (ui.WantsKeys)
        {
            ui.Text(text);
        }
    }

    static Point ToPoint(Vector2 position) => new(position.X, position.Y);

    static void Track(HashSet<long> held, long id, bool pressed)
    {
        if (pressed)
        {
            held.Add(id);
        }
        else
        {
            held.Remove(id);
        }
    }
}
