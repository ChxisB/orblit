using Avalonia;
using Avalonia.Controls;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// An area a finger drags across. <see cref="Swiped"/> gives each move in logical pixels, positive up
/// and to the right. A touch alone is not a swipe, so landing says nothing.
/// </summary>
public sealed class TouchSwipe : TouchArea
{
    private Point _last;

    public TouchSwipe(Control face)
        : base(face)
    {
    }

    public event Action<Vector>? Swiped;

    protected override void Landed(Point at) => _last = at;

    protected override void Moved(Point at)
    {
        var step = at - _last;
        _last = at;
        if (step != default)
        {
            // 0 - y, because -y turns a y of 0 into -0.0.
            Swiped?.Invoke(new Vector(step.X, 0.0 - step.Y));
        }
    }
}
