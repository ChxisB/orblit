using Avalonia;
using Avalonia.Controls;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// A button held under a finger. It sends <c>true</c> when a finger lands and <c>false</c> when it
/// lifts, slides off the screen or is taken by the system. The whole face dims while it is held.
/// </summary>
internal sealed class TouchHold : TouchArea
{
    private const double HeldOpacity = 0.6;

    private readonly TouchSend? _send;

    internal TouchHold(Control face, TouchSend? send)
        : base(face) => _send = send;

    protected override void Landed(Point at)
    {
        Opacity = HeldOpacity;
        _send?.Invoke(true);
    }

    protected override void Lifted()
    {
        Opacity = 1;
        _send?.Invoke(false);
    }
}
