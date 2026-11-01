namespace Orblit.Input;

/// <summary>Where every pad stands, and what happened getting here.</summary>
public sealed class PadFrame(IReadOnlyList<PadState> pads, IReadOnlyList<PadEvent> events)
{
    /// <summary>Every slot, connected or not, so a two player game can index by player number.</summary>
    public IReadOnlyList<PadState> Pads { get; } = pads;

    /// <summary>What happened since the last poll, in the order it happened.</summary>
    public IReadOnlyList<PadEvent> Events { get; } = events;

    /// <summary>The lowest numbered pad that is there. What a single player game wants.</summary>
    public PadState First => Pads.FirstOrDefault(pad => pad.Connected) ?? PadState.Absent;

    public IEnumerable<PadState> Connected => Pads.Where(pad => pad.Connected);

    public int Count => Connected.Count();

    public bool IsActive => Connected.Any(pad => pad.IsActive);

    /// <summary>One player's pad. <see cref="PadState.Absent"/> when nobody is in that slot.</summary>
    public PadState Pad(int slot) => slot >= 0 && slot < Pads.Count ? Pads[slot] : PadState.Absent;
}
