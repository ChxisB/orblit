using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>One pad as it stood at one poll. A pad that is not there is <see cref="Absent"/>, so asking about it never throws.</summary>
public sealed class PadState
{
    private readonly OrderedSet<PadButton> _down;
    private readonly OrderedSet<PadButton> _pressed;
    private readonly OrderedSet<PadButton> _released;
    private readonly IReadOnlyDictionary<PadAxis, double> _axes;
    private readonly IReadOnlyDictionary<PadAxis, double> _raw;

    // F2: optional parameters, so a call names only what it sets.
    public PadState(
        int slot,
        PadIdentity identity,
        IEnumerable<PadButton> down,
        IEnumerable<PadButton> pressed,
        IEnumerable<PadButton> released,
        IReadOnlyDictionary<PadAxis, double> axes,
        IReadOnlyDictionary<PadAxis, double> raw,
        bool connected = true)
    {
        Slot = slot;
        Identity = identity;
        Connected = connected;
        _down = new OrderedSet<PadButton>(down);
        _pressed = new OrderedSet<PadButton>(pressed);
        _released = new OrderedSet<PadButton>(released);
        _axes = axes;
        _raw = raw;
    }

    public static PadState Absent { get; } = new(
        slot: -1,
        identity: new PadIdentity(string.Empty),
        down: [],
        pressed: [],
        released: [],
        axes: new Dictionary<PadAxis, double>(),
        raw: new Dictionary<PadAxis, double>(),
        connected: false);

    public int Slot { get; }

    public PadIdentity Identity { get; }

    public bool Connected { get; }

    /// <summary>The buttons that are down, in the order they went down.</summary>
    public IEnumerable<PadButton> Held => _down;

    /// <summary>Whether anything is down or an axis is away from rest.</summary>
    public bool IsActive => _down.Count > 0 || _axes.Values.Any(value => Math.Abs(value) > 0.001);

    /// <summary>The d-pad as a vector, with up positive.</summary>
    public Vector2 Dpad => new(
        (IsDown(PadButton.DpadRight) ? 1 : 0) - (IsDown(PadButton.DpadLeft) ? 1 : 0),
        (IsDown(PadButton.DpadUp) ? 1 : 0) - (IsDown(PadButton.DpadDown) ? 1 : 0));

    public bool IsDown(PadButton button) => _down.Contains(button);

    public bool WasPressed(PadButton button) => _pressed.Contains(button);

    public bool WasReleased(PadButton button) => _released.Contains(button);

    /// <summary>The shaped value of an axis. Zero for one this pad lacks.</summary>
    public double Axis(PadAxis axis) => _axes.GetValueOrDefault(axis);

    /// <summary>The value before the dead zone and curve. Zero for one this pad lacks.</summary>
    public double RawAxis(PadAxis axis) => _raw.GetValueOrDefault(axis);

    public Vector2 Stick(PadStick which) => which switch
    {
        PadStick.Left => new Vector2(Axis(PadAxis.LeftX), Axis(PadAxis.LeftY)),
        PadStick.Right => new Vector2(Axis(PadAxis.RightX), Axis(PadAxis.RightY)),
        _ => throw new UnreachableException(),
    };

    public double Trigger(PadSide side) => side switch
    {
        PadSide.Left => Axis(PadAxis.LeftTrigger),
        PadSide.Right => Axis(PadAxis.RightTrigger),
        _ => throw new UnreachableException(),
    };

    public override string ToString() =>
        Connected ? $"PadState({Slot}, {Identity.Name}, {_down.Count} down)" : "PadState(absent)";
}
