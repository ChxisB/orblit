using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Filament;
using Orblit.Stage.Avalonia;
using Orblit.VectorMath;
using Orblit.Input;

namespace Orblit.Game.Avalonia;

/// <summary>
/// Listens to one control and says what the player is doing as a <see cref="GameInput"/>. The runner's
/// window and the editor's game view both use it, so a game sees the same keys and the same pointer in
/// either.
/// </summary>
/// <remarks>
/// Keys reach the control that has the focus, so the source makes its surface focusable and the host
/// gives it the focus. Losing the focus lets go of every key, because the key-up would go to the
/// control that took the focus and the key would otherwise stay held.
/// <para>
/// A click, the wheel and a drag are sums since the last <see cref="EndFrame"/>. The host reads once a
/// frame and ends the frame after, which is what makes a click one update and not many.
/// </para>
/// </remarks>
public sealed class GameInputSource
{
    private readonly InputElement _surface;
    private readonly HashSet<string> _keys = [];
    private Point? _at;
    private Point? _dragFrom;
    private Vector _dragged;
    private double _wheel;
    private bool _pressed;

    public GameInputSource(InputElement surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        surface.Focusable = true;
        surface.KeyDown += OnKeyDown;
        surface.KeyUp += OnKeyUp;
        surface.LostFocus += OnLostFocus;
        surface.PointerPressed += OnPressed;
        surface.PointerMoved += OnMoved;
        surface.PointerReleased += OnReleased;
        surface.PointerCaptureLost += OnCaptureLost;
        surface.PointerExited += OnExited;
        surface.PointerWheelChanged += OnWheel;
    }

    /// <summary>
    /// What the player is doing now. The pointer is null until it is over the surface and after it
    /// leaves. <paramref name="camera"/> is the camera the view is drawn through, which gives the ray.
    /// </summary>
    public GameInput Read(RenderCamera camera)
    {
        var size = _surface.Bounds.Size;
        if (_at is not { } at || size.Width <= 0 || size.Height <= 0)
        {
            return new GameInput(new FrameControls([.. _keys]));
        }

        var ray = PointerRay.FromCamera(camera, at, size);
        // A drag released before the frame was read still moved the view, so the sum is reported once.
        var drag = _dragFrom is null && _dragged == default ? null : new Vector2(_dragged.X, _dragged.Y);
        return new GameInput(new FrameControls([.. _keys]), new GamePointer(ray.Origin, ray.Direction, _pressed, drag, _wheel));
    }

    /// <summary>Forgets the click, the wheel and the drag so far. Held keys stay held.</summary>
    public void EndFrame()
    {
        _pressed = false;
        _wheel = 0;
        _dragged = default;
    }

    /// <summary>Stops listening, for a host that is closing. Nothing is held afterwards.</summary>
    public void Detach()
    {
        _surface.KeyDown -= OnKeyDown;
        _surface.KeyUp -= OnKeyUp;
        _surface.LostFocus -= OnLostFocus;
        _surface.PointerPressed -= OnPressed;
        _surface.PointerMoved -= OnMoved;
        _surface.PointerReleased -= OnReleased;
        _surface.PointerCaptureLost -= OnCaptureLost;
        _surface.PointerExited -= OnExited;
        _surface.PointerWheelChanged -= OnWheel;
        _keys.Clear();
        _at = null;
        _dragFrom = null;
        EndFrame();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (KeyNames.Of(e.PhysicalKey) is { } name)
        {
            _keys.Add(name);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (KeyNames.Of(e.PhysicalKey) is { } name)
        {
            _keys.Remove(name);
        }
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e) => _keys.Clear();

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_surface);
        _at = point.Position;
        switch (point.Properties.PointerUpdateKind)
        {
            case PointerUpdateKind.LeftButtonPressed:
                _pressed = true;
                break;
            case PointerUpdateKind.RightButtonPressed:
                // Captured, so a drag that runs off the view still turns it.
                _dragFrom = point.Position;
                e.Pointer.Capture(_surface);
                break;
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var at = e.GetPosition(_surface);
        _at = at;
        if (_dragFrom is { } from)
        {
            _dragged += at - from;
            _dragFrom = at;
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _dragFrom = null;
            e.Pointer.Capture(null);
        }
    }

    // The system can take the capture, as it does on a task switch, and then no release arrives.
    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _dragFrom = null;

    // A captured drag can run off the view with the pointer still ours.
    private void OnExited(object? sender, PointerEventArgs e)
    {
        if (_dragFrom is null)
        {
            _at = null;
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        _at = e.GetPosition(_surface);
        _wheel += e.Delta.Y;
    }
}
