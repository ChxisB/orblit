using System.Numerics;
using Avalonia;
using Avalonia.Input;

namespace Orblit.Ui.Tests;

/// <summary>A UI whose left 100 pixels are a control. It keeps what it was told.</summary>
internal sealed class FakeUi : IUiInput
{
    public List<string> Calls { get; } = [];

    public bool Keys { get; set; }

    public bool WantsKeys => Keys;

    public void PointerMoved(Point point) => Calls.Add("move");

    public void PointerButton(Point point, MouseButton button, bool pressed) => Calls.Add($"button {button} {pressed}");

    public void Key(Key key, bool pressed) => Calls.Add($"key {key} {pressed}");

    public void Text(string text) => Calls.Add($"text {text}");

    public bool WantsPointer(Point point) => point.X < 100;
}

/// <summary>A scene that keeps what it was told.</summary>
internal sealed class FakeScene : ISceneInput
{
    public List<string> Calls { get; } = [];

    public void Key(long keycode, bool pressed) => Calls.Add($"key {keycode} {pressed}");

    public void MouseButton(long button, Vector2 position, bool pressed) => Calls.Add($"button {button} {pressed}");

    public void MouseMove(Vector2 position, Vector2 relative) => Calls.Add("move");
}
