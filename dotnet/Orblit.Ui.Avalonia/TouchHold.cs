using Avalonia;
using Avalonia.Controls;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// A button held under a finger. <see cref="Held"/> says <c>true</c> when a finger lands and
/// <c>false</c> when it lifts, slides off the screen or is taken by the system. The whole face dims
/// while it is held.
/// </summary>
public sealed class TouchHold : TouchArea
{
    private const double HeldOpacity = 0.6;

    public TouchHold(Control face)
        : base(face)
    {
    }

    public event Action<bool>? Held;

    protected override void Landed(Point at)
    {
        Opacity = HeldOpacity;
        Held?.Invoke(true);
    }

    protected override void Lifted()
    {
        Opacity = 1;
        Held?.Invoke(false);
    }
}
