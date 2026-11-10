using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace Orblit.Editor.Live;

/// <summary>
/// Sends raw pointer and key events into a window's platform input callback, the place a real mouse and
/// keyboard reach Avalonia. Hit testing, capture, focus and the editor's own handlers run as they do for a
/// person. These are not physical devices: the operating system never sees the events.
/// </summary>
internal sealed class InjectedInput
{
    // A pause longer than an engine frame lets the viewport tick between events.
    const int PauseMilliseconds = 40;
    const int DragSteps = 12;

    readonly MouseDevice _mouse = new();
    readonly KeyboardDevice _keyboard = new();

    public static Point CentreOf(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), TopLevel.GetTopLevel(control)!)!.Value;

    public Task Click(Control target) => Click(TopLevel.GetTopLevel(target)!, CentreOf(target));

    public async Task Click(TopLevel top, Point at)
    {
        Pointer(top, RawPointerEventType.Move, at, RawInputModifiers.None);
        await Pause();
        Pointer(top, RawPointerEventType.LeftButtonDown, at, RawInputModifiers.LeftMouseButton);
        await Pause();
        Pointer(top, RawPointerEventType.LeftButtonUp, at, RawInputModifiers.None);
        await Pause();
    }

    public async Task Drag(TopLevel top, Point from, Point to)
    {
        Pointer(top, RawPointerEventType.Move, from, RawInputModifiers.None);
        await Pause();
        Pointer(top, RawPointerEventType.LeftButtonDown, from, RawInputModifiers.LeftMouseButton);
        for (var step = 1; step <= DragSteps; step++)
        {
            await Pause();
            var along = (double)step / DragSteps;
            Pointer(top, RawPointerEventType.Move, from + (to - from) * along, RawInputModifiers.LeftMouseButton);
        }
        await Pause();
        Pointer(top, RawPointerEventType.LeftButtonUp, to, RawInputModifiers.None);
        await Pause();
    }

    public async Task Wheel(TopLevel top, Point at, double notches)
    {
        Pointer(top, RawPointerEventType.Move, at, RawInputModifiers.None);
        await Pause();
        Send(top, root => new RawMouseWheelEventArgs(_mouse, Now(), root, at, new Vector(0, notches), RawInputModifiers.None));
        await Pause();
    }

    public async Task Tap(TopLevel top, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Send(top, root => new RawKeyEventArgs(_keyboard, Now(), root, RawKeyEventType.KeyDown, key, modifiers, PhysicalKey.None, null));
        await Pause();
        Send(top, root => new RawKeyEventArgs(_keyboard, Now(), root, RawKeyEventType.KeyUp, key, modifiers, PhysicalKey.None, null));
        await Pause();
    }

    public static Task Pause() => Task.Delay(PauseMilliseconds);

    void Pointer(TopLevel top, RawPointerEventType type, Point at, RawInputModifiers modifiers) =>
        Send(top, root => new RawPointerEventArgs(_mouse, Now(), root, type, at, modifiers));

    static void Send(TopLevel top, Func<IInputRoot, RawInputEventArgs> make)
    {
        // The input root is a private field of the top level in Avalonia 12, so it is read by name. A rename fails here at once.
        var root = (IInputRoot)typeof(TopLevel).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(top)!;
        top.PlatformImpl!.Input!(make(root));
    }

    static ulong Now() => (ulong)Environment.TickCount64;
}
