using Avalonia;
using Avalonia.Controls;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// An area a finger drags across. It sends <c>[dx, dy]</c> for each move, in logical pixels and
/// positive up and to the right. A touch alone is not a swipe, so landing sends nothing.
/// </summary>
internal sealed class TouchSwipe : TouchArea
{
    private readonly TouchSend? _send;
    private Point _last;

    internal TouchSwipe(Control face, TouchSend? send)
        : base(face) => _send = send;

    protected override void Landed(Point at) => _last = at;

    protected override void Moved(Point at)
    {
        var step = at - _last;
        _last = at;
        if (step != default)
        {
            // 0 - y, because -y turns a y of 0 into -0.0.
            _send?.Invoke(new[] { step.X, 0.0 - step.Y });
        }
    }
}
