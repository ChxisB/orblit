using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Shapes;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// A thumb stick. <see cref="Tilted"/> gives where the thumb is when that changes, each way from -1 to
/// 1 and positive up and to the right. A stick let go gives zero. The knob is drawn over the face,
/// where the thumb is, and the base is whatever the face draws.
/// </summary>
public sealed class TouchStick : TouchArea
{
    private readonly Control _face;
    private readonly Ellipse _knob;
    private Vector _at;

    public TouchStick(Control face, IBrush knob)
        : base(face)
    {
        _face = face;
        _knob = new Ellipse { Fill = knob, IsHitTestVisible = false };
        Children.Add(_knob);
    }

    public event Action<Vector>? Tilted;

    /// <summary>Where a point sits in the circle that fills <paramref name="size"/>, from -1 to 1 each way with y down. Outside the circle it is brought back to it.</summary>
    private static Vector StickAt(Point local, Size size)
    {
        var radius = Math.Min(size.Width, size.Height) / 2;
        if (radius <= 0)
        {
            return default;
        }

        var offset = new Vector(local.X - (size.Width / 2), local.Y - (size.Height / 2)) / radius;
        return offset.Length > 1 ? offset / offset.Length : offset;
    }

    protected override void Landed(Point at) => Push(at);

    protected override void Moved(Point at) => Push(at);

    protected override void Lifted()
    {
        _at = default;
        InvalidateArrange();
        Tilted?.Invoke(default);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _knob.Measure(Size.Infinity);
        _face.Measure(availableSize);
        return _face.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _face.Arrange(new Rect(finalSize));
        var radius = Math.Min(finalSize.Width, finalSize.Height) / 2;
        var across = radius * 0.8;
        var middle = new Point(finalSize.Width / 2, finalSize.Height / 2) + (_at * radius);
        _knob.Arrange(new Rect(middle.X - (across / 2), middle.Y - (across / 2), across, across));
        return finalSize;
    }

    private void Push(Point local)
    {
        var at = StickAt(local, Bounds.Size);
        if (at == _at)
        {
            return;
        }

        _at = at;
        InvalidateArrange();

        // 0 - y, because -y turns a y of 0 into -0.0.
        Tilted?.Invoke(new Vector(at.X, 0.0 - at.Y));
    }
}
