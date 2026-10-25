using Orblit.Input;
using Orblit.VectorMath;

namespace Orblit.Test;

/// <summary>
/// The controls a test holds down. A game reads them through <see cref="IControlSource"/> as it
/// reads a keyboard and a pad.
/// </summary>
public sealed class ScriptedControls : IControlSource
{
    // A stick is two axes, so a stick pushed and an axis pushed cannot disagree.
    private static readonly Dictionary<PadStick, (PadAxis X, PadAxis Y)> Sticks = new()
    {
        [PadStick.Left] = (PadAxis.LeftX, PadAxis.LeftY),
        [PadStick.Right] = (PadAxis.RightX, PadAxis.RightY),
    };

    private readonly Dictionary<Control, double> _held = [];
    private readonly Dictionary<PadAxis, double> _axes = [];

    /// <summary>Holds <paramref name="control"/> at <paramref name="value"/> until <see cref="Release"/>.</summary>
    public void Press(Control control, double value = 1)
    {
        if (control is PadAxisControl or PadStickControl)
        {
            throw new ArgumentException($"{control} is pushed with {nameof(PushAxis)} or {nameof(PushStick)}.", nameof(control));
        }

        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "must be a number");
        }

        _held[control] = value;
    }

    public void Release(Control control) => _held.Remove(control);

    /// <summary>Pushes a pad axis to <paramref name="value"/>, from -1 to 1. 0 lets it go.</summary>
    public void PushAxis(PadAxis axis, double value)
    {
        if (!(Math.Abs(value) <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "must be from -1 to 1");
        }

        if (value == 0)
        {
            _axes.Remove(axis);
        }
        else
        {
            _axes[axis] = value;
        }
    }

    /// <summary>Pushes a stick to <paramref name="to"/>, which a real stick never takes past length 1.</summary>
    public void PushStick(PadStick stick, Vector2 to)
    {
        var length = to.Length;
        if (!(double.IsFinite(length) && length <= 1 + 1e-9))
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, "must be no longer than 1");
        }

        var (x, y) = Sticks[stick];
        PushAxis(x, to.X);
        PushAxis(y, to.Y);
    }

    public double Read(Control control) => control switch
    {
        PadAxisControl axis => axis.Side.Read(_axes.GetValueOrDefault(axis.Axis)),
        PadStickControl stick => Stick(stick.Stick).Length,
        _ => _held.GetValueOrDefault(control),
    };

    public Vector2 Stick(PadStick stick)
    {
        var (x, y) = Sticks[stick];
        return new Vector2(_axes.GetValueOrDefault(x), _axes.GetValueOrDefault(y));
    }
}
