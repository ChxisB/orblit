using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>One player's pad, and everything tracked about it between polls.</summary>
internal sealed class PadSlot
{
    private static readonly (PadAxis X, PadAxis Y)[] StickAxes =
    [
        (PadAxis.LeftX, PadAxis.LeftY),
        (PadAxis.RightX, PadAxis.RightY),
    ];

    private readonly EvdevDecoder _decoder = new();

    // Raw values by evdev code. Ordered because the order the hats arrived in is the order their
    // buttons are listed in.
    private readonly OrderedDictionary<int, int> _axes = [];

    private OrderedSet<int> _down = [];
    private OrderedSet<int> _was = [];

    internal PadSlot(IPadDevice device)
    {
        Device = device;
        Layout = PadLayout.Of(device.Identity, device.Capabilities);
    }

    internal IPadDevice Device { get; }

    internal PadLayout Layout { get; }

    internal string Token => Device.Identity.Token;

    /// <summary>
    /// Takes the device's current state as the starting point. Without it a trigger already held
    /// when the game starts reads as released until somebody lets go and pulls it again, because
    /// the state is a running total of differences and a total needs a first term.
    /// </summary>
    internal void Adopt()
    {
        var now = Device.Resync();
        _down = new OrderedSet<int>(now.Buttons);
        _was = new OrderedSet<int>(now.Buttons);
        TakeAxes(now);
    }

    /// <summary>Lets go of everything held, with the events a player letting go would have made.</summary>
    internal void ReleaseAll(int slot, List<PadEvent> events)
    {
        foreach (var code in _down)
        {
            Report(slot, code, pressed: false, events);
        }

        _down = [];
    }

    /// <summary>Reads whatever has arrived and folds it in.</summary>
    internal void Pump(int slot, List<PadEvent> events)
    {
        _was = new OrderedSet<int>(_down);

        var bytes = Device.Drain();
        if (bytes.Length == 0)
        {
            return;
        }

        foreach (var evdevEvent in _decoder.Decode(bytes))
        {
            Fold(slot, evdevEvent, events);
        }
    }

    internal PadState StateAt(int slot, Shaping sticks, Shaping triggers)
    {
        var named = new OrderedSet<PadButton>(HeldButtons(_down));
        var raw = ReadAxes(named);
        var shaped = Shape(raw, ReferenceEquals(Layout.Sticks, Shaping.None) ? sticks : Layout.Sticks, triggers);

        // A trigger that travels is also a button, so a game can treat it as either. Half way is far
        // enough not to fire on a resting trigger and near enough to feel immediate.
        if (shaped.GetValueOrDefault(PadAxis.LeftTrigger) > 0.5)
        {
            named.Add(PadButton.LeftTrigger);
        }

        if (shaped.GetValueOrDefault(PadAxis.RightTrigger) > 0.5)
        {
            named.Add(PadButton.RightTrigger);
        }

        var wasNamed = new OrderedSet<PadButton>(HeldButtons(_was));
        return new PadState(
            slot: slot,
            identity: Device.Identity,
            down: named,
            pressed: named.Where(button => !wasNamed.Contains(button)),
            released: wasNamed.Where(button => !named.Contains(button)),
            axes: shaped,
            raw: raw);
    }

    private IEnumerable<PadButton> HeldButtons(IEnumerable<int> codes) =>
        codes.Where(Layout.Buttons.ContainsKey).Select(code => Layout.Buttons[code]);

    private void TakeAxes(PadSnapshot now)
    {
        _axes.Clear();
        foreach (var (code, value) in now.Axes)
        {
            _axes[code] = value;
        }
    }

    private void Report(int slot, int code, bool pressed, List<PadEvent> events)
    {
        if (Layout.Buttons.TryGetValue(code, out var button))
        {
            events.Add(pressed ? new PadPressed(slot, button) : new PadReleased(slot, button));
        }
    }

    private void Fold(int slot, EvdevEvent evdevEvent, List<PadEvent> events)
    {
        switch (evdevEvent.Type)
        {
            case Evdev.Key:
                FoldKey(slot, evdevEvent, events);
                break;
            case Evdev.Abs:
                _axes[evdevEvent.Code] = evdevEvent.Value;
                break;
            case Evdev.Syn when evdevEvent.Value == Evdev.SynDropped || evdevEvent.Code == Evdev.SynDropped:
                Resync(slot, events);
                break;
        }
    }

    // A value of 2 is the kernel repeating a held key. A held button is already down, and treating a
    // repeat as a fresh press would fire a jump every few frames.
    private void FoldKey(int slot, EvdevEvent evdevEvent, List<PadEvent> events)
    {
        if (evdevEvent.Value == 1 && _down.Add(evdevEvent.Code))
        {
            Report(slot, evdevEvent.Code, pressed: true, events);
        }
        else if (evdevEvent.Value == 0 && _down.Remove(evdevEvent.Code))
        {
            Report(slot, evdevEvent.Code, pressed: false, events);
        }
    }

    // The kernel threw events away, so every difference since is unreliable. Ask the device what it
    // is rather than carry on from a state that has already diverged.
    private void Resync(int slot, List<PadEvent> events)
    {
        _decoder.Reset();
        var now = Device.Resync();
        var held = new OrderedSet<int>(now.Buttons);
        foreach (var code in _down.Where(code => !held.Contains(code)).ToList())
        {
            Report(slot, code, pressed: false, events);
        }

        foreach (var code in held.Where(code => !_down.Contains(code)).ToList())
        {
            Report(slot, code, pressed: true, events);
        }

        _down = held;
        TakeAxes(now);
    }

    private Dictionary<PadAxis, double> ReadAxes(OrderedSet<PadButton> named)
    {
        var raw = new Dictionary<PadAxis, double>();
        foreach (var (code, value) in _axes)
        {
            if (!AddHat(named, code, value) && Layout.Axes.TryGetValue(code, out var axis))
            {
                raw[axis] = Normalise(code, axis, value);
            }
        }

        return raw;
    }

    // The hats are the d-pad, not axes. A hat rests at nought and goes to -1 or 1, and every game
    // wants that as four buttons. Positive is down in the hardware's numbering and up in ours.
    private static bool AddHat(OrderedSet<PadButton> named, int code, int value)
    {
        if (code == PadLayout.HatX)
        {
            AddIf(named, value < 0, PadButton.DpadLeft);
            AddIf(named, value > 0, PadButton.DpadRight);
            return true;
        }

        if (code == PadLayout.HatY)
        {
            AddIf(named, value < 0, PadButton.DpadUp);
            AddIf(named, value > 0, PadButton.DpadDown);
            return true;
        }

        return false;
    }

    private static void AddIf(OrderedSet<PadButton> named, bool when, PadButton button)
    {
        if (when)
        {
            named.Add(button);
        }
    }

    private double Normalise(int code, PadAxis axis, int value)
    {
        var isTrigger = axis is PadAxis.LeftTrigger or PadAxis.RightTrigger;
        var range = Device.Ranges.GetValueOrDefault(code) ?? (isTrigger ? PadAxisRange.Trigger : PadAxisRange.Stick);
        var normal = isTrigger ? range.ToUnsigned(value) : range.ToSigned(value);

        // Up is positive here and down is positive in the hardware.
        return axis is PadAxis.LeftY or PadAxis.RightY ? -normal : normal;
    }

    private static Dictionary<PadAxis, double> Shape(Dictionary<PadAxis, double> raw, Shaping sticks, Shaping triggers)
    {
        var shaped = new Dictionary<PadAxis, double>();
        foreach (var (xAxis, yAxis) in StickAxes)
        {
            var stick = sticks.Stick(new Vector2(raw.GetValueOrDefault(xAxis), raw.GetValueOrDefault(yAxis)));
            shaped[xAxis] = stick.X;
            shaped[yAxis] = stick.Y;
        }

        shaped[PadAxis.LeftTrigger] = triggers.Scalar(raw.GetValueOrDefault(PadAxis.LeftTrigger));
        shaped[PadAxis.RightTrigger] = triggers.Scalar(raw.GetValueOrDefault(PadAxis.RightTrigger));
        return shaped;
    }
}
