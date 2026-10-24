namespace Orblit.Input;

/// <summary>
/// The gamepads attached to this machine. Opened once and polled once a frame. The poll is what
/// drains the devices, so a game that stops polling stops seeing input rather than accumulating a
/// backlog of it.
/// </summary>
public sealed class Pads
{
    private readonly IPadBackend _backend;

    // In the order they arrived, which is the order their events are reported in.
    private readonly OrderedDictionary<int, PadSlot> _slots = [];

    // Which slot a pad had last time it was here, by its own token. So a controller whose battery
    // dies mid-game comes back as the same player. Kept for the life of the process.
    private readonly Dictionary<string, int> _remembered = [];

    public Pads(IPadBackend? backend = null, Shaping? sticks = null, Shaping? triggers = null, int maxPads = 8)
    {
        _backend = backend ?? NoPads.Instance;
        Sticks = sticks ?? new Shaping();
        Triggers = triggers ?? Shaping.Triggers;
        MaxPads = maxPads;
    }

    /// <summary>
    /// The pads on this machine, through whichever backend the platform has. A platform with no
    /// backend gets one that reports nothing and not an exception. A game run on a machine it was not
    /// built for should be short of a controller and not dead.
    /// </summary>
    public static Pads Open(Shaping? sticks = null, Shaping? triggers = null, int maxPads = 8) =>
        new(PlatformBackend(), sticks, triggers, maxPads);

    /// <summary>How every pad's sticks are shaped, unless its layout says otherwise.</summary>
    public Shaping Sticks { get; }

    public Shaping Triggers { get; }

    /// <summary>How many players are possible. A ninth pad is ignored rather than displacing somebody.</summary>
    public int MaxPads { get; }

    /// <summary>Drains every pad and says where things stand.</summary>
    public PadFrame Poll()
    {
        var events = new List<PadEvent>();
        var seen = new OrderedDictionary<string, IPadDevice>();
        foreach (var device in _backend.Scan())
        {
            seen[device.Identity.Token] = device;
        }

        // Gone first, so a pad unplugged and another plugged into the same port between two polls
        // does not fight over the slot.
        DropGone(seen, events);
        AddArrivals(seen, events);
        foreach (var (index, slot) in _slots)
        {
            slot.Pump(index, events);
        }

        var pads = new List<PadState>();
        for (var i = 0; i < MaxPads; i++)
        {
            pads.Add(_slots.TryGetValue(i, out var slot) ? slot.StateAt(i, Sticks, Triggers) : PadState.Absent);
        }

        return new PadFrame(pads, events);
    }

    public bool CanRumble(int slot) => _slots.TryGetValue(slot, out var held) && held.Device.CanRumble;

    /// <summary>
    /// Shakes the pad in <paramref name="slot"/>. Does nothing for an empty slot or a pad that
    /// cannot. A value outside nought to one is clamped, and one that is not a number is nought.
    /// </summary>
    public void Rumble(int slot, double strong, double weak, TimeSpan duration)
    {
        if (_slots.TryGetValue(slot, out var held) && held.Device.CanRumble)
        {
            held.Device.Rumble(Unit(strong), Unit(weak), duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
        }
    }

    /// <summary>Lets go of every device.</summary>
    public void Close()
    {
        foreach (var slot in _slots.Values)
        {
            slot.Device.Close();
        }

        _slots.Clear();
        _backend.Close();
    }

    private static double Unit(double value) => value > 0 ? (value < 1 ? value : 1) : 0;

    private static IPadBackend PlatformBackend() => OperatingSystem.IsLinux() ? new LinuxPads() : NoPads.Instance;

    private void DropGone(OrderedDictionary<string, IPadDevice> seen, List<PadEvent> events)
    {
        foreach (var (index, slot) in _slots.ToList())
        {
            if (seen.TryGetValue(slot.Token, out var still) && still.IsAlive && ReferenceEquals(still, slot.Device))
            {
                continue;
            }

            // A held button is let go before the pad is, so a game waiting for the release to end a
            // charge or a hold does not wait for ever.
            slot.ReleaseAll(index, events);
            events.Add(new PadDisconnected(index, slot.Device.Identity));
            _remembered[slot.Token] = index;
            slot.Device.Close();
            _slots.Remove(index);
        }
    }

    private void AddArrivals(OrderedDictionary<string, IPadDevice> seen, List<PadEvent> events)
    {
        foreach (var (token, device) in seen)
        {
            if (_slots.Values.Any(slot => ReferenceEquals(slot.Device, device)) || SlotFor(token) is not { } at)
            {
                continue;
            }

            var slot = new PadSlot(device);
            slot.Adopt();
            _slots[at] = slot;
            events.Add(new PadConnected(at, device.Identity, slot.Layout));
        }
    }

    // The slot a newly seen pad should take: the one it had before if that is free, and the lowest
    // free one otherwise.
    private int? SlotFor(string token)
    {
        if (_remembered.TryGetValue(token, out var remembered) && remembered < MaxPads && !_slots.ContainsKey(remembered))
        {
            return remembered;
        }

        for (var i = 0; i < MaxPads; i++)
        {
            if (!_slots.ContainsKey(i))
            {
                return i;
            }
        }

        return null;
    }
}
