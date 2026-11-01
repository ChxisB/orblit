namespace Orblit.Input;

/// <summary>
/// A pad on a platform that tells what is held rather than what happened. It writes the difference
/// between two reads as the events a Linux pad would have written, so everything above reads one
/// kind of device.
/// </summary>
public sealed class SnapshotPad : IPadDevice
{
    private readonly Func<PadSnapshot> _read;
    private readonly Func<bool> _alive;
    private readonly RumbleFunction? _onRumble;
    private PadSnapshot _last = new([], new Dictionary<int, int>());

    // F2: optional parameters, so a call names only what it sets.
    public SnapshotPad(
        PadIdentity identity,
        Func<PadSnapshot> read,
        Func<bool> alive,
        IReadOnlyDictionary<int, PadAxisRange>? ranges = null,
        RumbleFunction? onRumble = null)
    {
        Identity = identity;
        _read = read;
        _alive = alive;
        Ranges = ranges ?? new Dictionary<int, PadAxisRange>();
        _onRumble = onRumble;
    }

    public PadIdentity Identity { get; }

    public EvdevCapabilities Capabilities => EvdevCapabilities.None;

    public IReadOnlyDictionary<int, PadAxisRange> Ranges { get; }

    public bool IsAlive => _alive();

    public bool CanRumble => _onRumble is not null;

    public void Rumble(double strong, double weak, TimeSpan duration) => _onRumble?.Invoke(strong, weak, duration);

    public byte[] Drain()
    {
        if (!IsAlive)
        {
            return [];
        }

        var now = _read();
        var bytes = new List<byte>();
        foreach (var code in now.Buttons.Where(code => !_last.Buttons.Contains(code)))
        {
            bytes.AddRange(Evdev.Encode(Evdev.Key, code, 1));
        }

        foreach (var code in _last.Buttons.Where(code => !now.Buttons.Contains(code)))
        {
            bytes.AddRange(Evdev.Encode(Evdev.Key, code, 0));
        }

        foreach (var (code, value) in now.Axes)
        {
            if (!_last.Axes.TryGetValue(code, out var was) || was != value)
            {
                bytes.AddRange(Evdev.Encode(Evdev.Abs, code, value));
            }
        }

        _last = now;
        if (bytes.Count == 0)
        {
            return [];
        }

        bytes.AddRange(Evdev.Encode(Evdev.Syn, Evdev.SynReport, 0));
        return [.. bytes];
    }

    public PadSnapshot Resync() => _alive() ? _read() : new PadSnapshot([], new Dictionary<int, int>());

    public void Close()
    {
    }
}
