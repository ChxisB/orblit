namespace Orblit.Input;

/// <summary>
/// Something that happened between two polls: the half a snapshot cannot hold. A button pressed and
/// released inside one frame is invisible to a state read at the ends of frames, and a pad arriving
/// or leaving is not a state at all.
/// </summary>
public abstract class PadEvent
{
    private protected PadEvent(int slot) => Slot = slot;

    /// <summary>Which player it happened to.</summary>
    public int Slot { get; }
}

/// <summary>A pad was plugged in, or was already there when the game started.</summary>
public sealed class PadConnected : PadEvent
{
    public PadConnected(int slot, PadIdentity identity, PadLayout layout)
        : base(slot)
    {
        Identity = identity;
        Layout = layout;
    }

    public PadIdentity Identity { get; }

    public PadLayout Layout { get; }

    public override string ToString() => $"PadConnected({Slot}, {Identity.Name})";
}

/// <summary>A pad went away.</summary>
public sealed class PadDisconnected : PadEvent
{
    public PadDisconnected(int slot, PadIdentity identity)
        : base(slot) => Identity = identity;

    public PadIdentity Identity { get; }

    public override string ToString() => $"PadDisconnected({Slot}, {Identity.Name})";
}

/// <summary>A button went down.</summary>
public sealed class PadPressed : PadEvent
{
    public PadPressed(int slot, PadButton button)
        : base(slot) => Button = button;

    public PadButton Button { get; }

    public override string ToString() => $"PadPressed({Slot}, {EnumNames.Of(Button)})";
}

/// <summary>A button came up.</summary>
public sealed class PadReleased : PadEvent
{
    public PadReleased(int slot, PadButton button)
        : base(slot) => Button = button;

    public PadButton Button { get; }

    public override string ToString() => $"PadReleased({Slot}, {EnumNames.Of(Button)})";
}
