using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>
/// What the action layer reads from the devices, once a frame. The game fills one per frame.
/// <see cref="FrameControls"/> does it for a keyboard, a touch screen and one pad. A test or a
/// replay can implement it directly.
/// </summary>
public interface IControlSource
{
    /// <summary>
    /// How far a control must travel to count as pressed. A key is 0 or 1. A trigger or a stick
    /// axis is pressed past halfway.
    /// </summary>
    const double PressPoint = 0.5;

    /// <summary>
    /// How far <paramref name="control"/> is pressed or pushed. 0 to 1 for a key, a button, a touch
    /// region, a trigger or half of a stick axis. -1 to 1 for a whole axis. For a whole stick, how
    /// far it is from the middle.
    /// </summary>
    double Read(Control control);

    /// <summary>A stick as a vector, positive up and to the right.</summary>
    Vector2 Stick(PadStick stick);
}

/// <summary>
/// One frame of a keyboard, a touch screen and the pad one player holds. It sees what is held at
/// the moment of the poll. A pad button pressed and released between two polls is in
/// <c>PadFrame.Events</c> and not here, so poll at the frame rate.
/// </summary>
public sealed class FrameControls : IControlSource, IPressedControls
{
    public FrameControls(
        IReadOnlyCollection<string>? keys = null,
        IReadOnlyCollection<string>? touch = null,
        IReadOnlyDictionary<PadStick, Vector2>? sticks = null,
        PadState? pad = null)
    {
        Keys = keys ?? [];
        Touch = touch ?? [];
        Sticks = sticks ?? new Dictionary<PadStick, Vector2>();
        Pad = pad ?? PadState.Absent;
    }

    /// <summary>The keys held now, by name.</summary>
    public IReadOnlyCollection<string> Keys { get; }

    /// <summary>The touch regions being touched now, by name.</summary>
    public IReadOnlyCollection<string> Touch { get; }

    /// <summary>
    /// The sticks on a touch screen, by the pad stick each stands in for. When a pad and a screen
    /// both push the same stick, the longer push is read. A binding to <see cref="PadStickControl"/>
    /// or to one of the stick's axes sees it.
    /// </summary>
    public IReadOnlyDictionary<PadStick, Vector2> Sticks { get; }

    public PadState Pad { get; }

    public IEnumerable<Control> Active => Keys.Select(key => (Control)new KeyControl(key))
        // A trigger that travels is listed as its axis, which keeps the analogue value for an
        // action that wants one.
        .Concat(Pad.Held.Where(button => !ListedAsAxis(button)).Select(button => new PadButtonControl(button)))
        .Concat(Enum.GetValues<PadStick>().Where(which => Stick(which).Length >= IControlSource.PressPoint).Select(which => new PadStickControl(which)))
        .Concat(PushedAxes())
        .Concat(Touch.Select(region => new TouchControl(region)));

    public double Read(Control control) => control switch
    {
        KeyControl key => Keys.Contains(key.Key) ? 1 : 0,
        PadButtonControl button => Pad.IsDown(button.Button) ? 1 : 0,
        PadAxisControl axis => axis.Side.Read(Axis(axis.Axis)),
        PadStickControl stick => Stick(stick.Stick).Length,
        TouchControl region => Touch.Contains(region.Region) ? 1 : 0,
        _ => throw new UnreachableException(),
    };

    public Vector2 Stick(PadStick stick)
    {
        var padded = Pad.Stick(stick);
        var touched = Sticks.GetValueOrDefault(stick);
        // A copy, because a game may normalise or scale what it is handed.
        return touched is not null && touched.Length2 > padded.Length2 ? touched.Clone() : padded;
    }

    /// <summary>A pad axis, or the touch stick that stands in for it when that is pushed further. A trigger has no stick.</summary>
    private double Axis(PadAxis axis)
    {
        var padded = Pad.Axis(axis);
        var touched = axis switch
        {
            PadAxis.LeftX => Sticks.GetValueOrDefault(PadStick.Left)?.X,
            PadAxis.LeftY => Sticks.GetValueOrDefault(PadStick.Left)?.Y,
            PadAxis.RightX => Sticks.GetValueOrDefault(PadStick.Right)?.X,
            PadAxis.RightY => Sticks.GetValueOrDefault(PadStick.Right)?.Y,
            _ => null,
        };
        return touched is { } value && Math.Abs(value) > Math.Abs(padded) ? value : padded;
    }

    private IEnumerable<Control> PushedAxes()
    {
        foreach (var axis in Enum.GetValues<PadAxis>())
        {
            var value = Axis(axis);
            // A reading that is not a number counts as pushed, as it always has (decision 80).
            if (!(Math.Abs(value) < IControlSource.PressPoint))
            {
                yield return new PadAxisControl(axis, value > 0 ? AxisSide.Positive : AxisSide.Negative);
            }
        }
    }

    private bool ListedAsAxis(PadButton button) => button switch
    {
        PadButton.LeftTrigger => Pad.Axis(PadAxis.LeftTrigger) >= IControlSource.PressPoint,
        PadButton.RightTrigger => Pad.Axis(PadAxis.RightTrigger) >= IControlSource.PressPoint,
        _ => false,
    };
}
