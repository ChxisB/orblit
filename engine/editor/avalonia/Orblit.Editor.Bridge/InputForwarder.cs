using System.Numerics;
using Orblit.Ui;

namespace Orblit.Editor.Bridge;

/// <summary>Sends keyboard and mouse events from the host toolkit into the engine's input.</summary>
public sealed class InputForwarder(EngineHost host) : ISceneInput
{
    readonly nint _input = host.Singleton("Input");

    public void Key(long keycode, bool pressed)
    {
        Send("InputEventKey", e =>
        {
            host.InputEventKeySetKeycode(e, keycode);
            host.InputEventKeySetPressed(e, pressed);
        });
    }

    public void MouseButton(long button, Vector2 position, bool pressed)
    {
        Send("InputEventMouseButton", e =>
        {
            host.InputEventMouseButtonSetButtonIndex(e, button);
            host.InputEventMouseButtonSetPressed(e, pressed);
            host.InputEventMouseSetPosition(e, position);
        });
    }

    public void MouseMove(Vector2 position, Vector2 relative)
    {
        Send("InputEventMouseMotion", e =>
        {
            host.InputEventMouseSetPosition(e, position);
            host.InputEventMouseMotionSetRelative(e, relative);
        });
    }

    void Send(string className, Action<nint> fill)
    {
        var made = host.Construct(className);
        try
        {
            fill(made);
            host.InputParseInputEvent(_input, made);
        }
        finally
        {
            host.Release(made);
        }
    }
}
