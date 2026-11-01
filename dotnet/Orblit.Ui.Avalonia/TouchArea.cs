using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// Claims the first finger that lands on it and follows that finger alone. A gesture recogniser would
/// put every finger into one contest, and a thumb on a stick and another on a jump button must not
/// be that. The area wraps the face it is given, so every pixel the player sees takes a touch. A
/// release is reported when the finger lifts, when the system takes it and when the area is removed
/// from the tree while it is down.
/// </summary>
public abstract class TouchArea : Panel
{
    private IPointer? _finger;

    // Only the touch controls here derive from it.
    private protected TouchArea(Control face)
    {
        // A panel is only hit where it draws, and a transparent fill is how it asks to be.
        Background = Brushes.Transparent;
        Children.Add(face);
    }

    protected virtual void Landed(Point at)
    {
    }

    protected virtual void Moved(Point at)
    {
    }

    protected virtual void Lifted()
    {
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_finger is not null)
        {
            return;
        }

        _finger = e.Pointer;
        e.Pointer.Capture(this);
        Landed(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_finger?.Id == e.Pointer.Id)
        {
            Moved(e.GetPosition(this));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_finger?.Id == e.Pointer.Id)
        {
            Let();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_finger?.Id == e.Pointer.Id)
        {
            Let();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_finger is not null)
        {
            Let();
        }
    }

    // The finger is forgotten first, so the capture lost that follows a release finds nobody to let go.
    private void Let()
    {
        _finger = null;
        Lifted();
    }
}
