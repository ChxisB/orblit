using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace Orblit.Stage.Avalonia;

/// <summary>
/// An <see cref="IPanel"/> that is a tree of real controls, made by <see cref="PanelHost.Open"/>. A
/// button clicks, a slider follows a drag and a scroller scrolls, as they do in a window.
/// </summary>
/// <remarks>
/// The picture is the control drawn into a bitmap and saved as PNG. A pointer is turned into routed
/// events and raised on the control under it, or on the control that captured the pointer, so a drag
/// keeps going after it leaves the panel. What the panel does not get is a window of its own: no hover,
/// no keyboard focus, so a text box cannot be typed into.
/// <para>
/// A panel is dirty when a pointer reached it, when its layout ran, and when <see cref="Invalidate"/>
/// says so. A change that moves nothing, such as a colour, shows in neither of the first two.
/// </para>
/// A pointer lands only once the host has drawn a frame or two with the panel in it.
/// </remarks>
public sealed class ControlPanel : IPanel, IDisposable
{
    private readonly PanelHost _host;
    private readonly Panel _frame;
    private readonly double _scale;

    // The panel's own pointer, so a press here never takes the capture of a real one.
    private readonly Pointer _pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
    private bool _pressed;
    private bool _dirty = true;
    private bool _disposed;

    internal ControlPanel(PanelHost host, Panel frame, double scale)
    {
        _host = host;
        _frame = frame;
        _scale = scale;
        frame.LayoutUpdated += (_, _) => _dirty = true;
    }

    public Size Size => new(_frame.Width, _frame.Height);

    public bool IsDirty => _dirty;

    /// <summary>Marks the picture out of date, for a change that does not lay anything out again.</summary>
    public void Invalidate() => _dirty = true;

    public byte[] Paint()
    {
        _frame.UpdateLayout();
        var pixels = new PixelSize((int)Math.Ceiling(Size.Width * _scale), (int)Math.Ceiling(Size.Height * _scale));
        using var picture = new RenderTargetBitmap(pixels, new Vector(96 * _scale, 96 * _scale));
        picture.Render(_frame);
        using var bytes = new MemoryStream();
        picture.Save(bytes, new PngBitmapEncoderOptions());
        _dirty = false;
        return bytes.ToArray();
    }

    public void Send(PanelPointer input)
    {
        if (_disposed)
        {
            return;
        }

        switch (input.Phase)
        {
            case PointerPhase.Down:
                Press(input.Position);
                break;
            case PointerPhase.Move when _pressed:
                Raise(input.Position, PointerUpdateKind.Other, (source, root, at, how) => new PointerEventArgs(InputElement.PointerMovedEvent, source, _pointer, root, at, 0, how, KeyModifiers.None));
                break;
            case PointerPhase.Up when _pressed:
                Release(input.Position);
                break;
            case PointerPhase.Cancel when _pressed:
                // Letting go of the capture tells the control under it that the press was taken back.
                _pressed = false;
                _dirty = true;
                _pointer.Capture(null);
                break;
        }
    }

    /// <summary>Takes the panel out of the host. A pointer sent after is ignored.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pointer.Capture(null);
        _host.Remove(_frame);
    }

    private void Press(Point at)
    {
        _frame.UpdateLayout();
        if (_pressed)
        {
            return;
        }

        _pressed = Raise(at, PointerUpdateKind.LeftButtonPressed, (source, root, where, how) => new PointerPressedEventArgs(source, _pointer, root, where, 0, how, KeyModifiers.None));
    }

    private void Release(Point at)
    {
        _pressed = false;
        Raise(at, PointerUpdateKind.LeftButtonReleased, (source, root, where, how) => new PointerReleasedEventArgs(source, _pointer, root, where, 0, how, KeyModifiers.None, MouseButton.Left));
        _pointer.Capture(null);
    }

    // The control that holds the pointer, or the one under it. False if there was none to raise on. Nothing
    // in a panel reads the time of an event, so the timestamp is 0.
    private bool Raise(Point at, PointerUpdateKind kind, Func<object, Visual, Point, PointerPointProperties, PointerEventArgs> make)
    {
        if ((_pointer.Captured ?? _frame.InputHitTest(at)) is not Interactive target || _frame.TranslatePoint(at, _host.Root) is not { } inRoot)
        {
            return false;
        }

        _dirty = true;
        var held = kind == PointerUpdateKind.LeftButtonReleased ? RawInputModifiers.None : RawInputModifiers.LeftMouseButton;
        target.RaiseEvent(make(target, _host.Root, inRoot, new PointerPointProperties(held, kind)));
        return true;
    }
}
