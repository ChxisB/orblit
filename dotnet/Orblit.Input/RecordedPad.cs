namespace Orblit.Input;

/// <summary>A pad driven by a test. What it is sent comes out of <see cref="Drain"/> as the bytes a real device would write.</summary>
public sealed class RecordedPad : IPadDevice
{
    private readonly OrderedSet<int> _held;
    private readonly OrderedDictionary<int, int> _resting;
    private readonly List<byte> _queued = [];
    private bool _alive = true;

    // F2: the Dart named parameters count as the options object.
    public RecordedPad(
        PadIdentity identity,
        EvdevCapabilities? capabilities = null,
        IReadOnlyDictionary<int, PadAxisRange>? ranges = null,
        IEnumerable<int>? held = null,
        IEnumerable<KeyValuePair<int, int>>? resting = null,
        bool canRumble = false)
    {
        Identity = identity;
        Capabilities = capabilities ?? EvdevCapabilities.None;
        Ranges = ranges ?? new Dictionary<int, PadAxisRange>();
        _held = new OrderedSet<int>(held ?? []);
        _resting = [];
        foreach (var (code, value) in resting ?? [])
        {
            _resting[code] = value;
        }

        CanRumble = canRumble;
    }

    public PadIdentity Identity { get; }

    public EvdevCapabilities Capabilities { get; }

    public IReadOnlyDictionary<int, PadAxisRange> Ranges { get; }

    public bool CanRumble { get; }

    /// <summary>Every call to <see cref="Rumble"/>, in order.</summary>
    public List<(double Strong, double Weak, TimeSpan Duration)> Rumbles { get; } = [];

    public bool IsAlive => _alive;

    /// <summary>Makes the device go away, as a pad pulled from its port does.</summary>
    public void Unplug() => _alive = false;

    /// <summary>Queues one raw event.</summary>
    public void Send(int type, int code, int value) => _queued.AddRange(Evdev.Encode(type, code, value));

    /// <summary>Queues bytes exactly as given, which a test can cut in the middle of a record.</summary>
    public void SendBytes(IEnumerable<byte> bytes) => _queued.AddRange(bytes);

    /// <summary>A button going down or up, followed by the report that ends the group.</summary>
    public void Button(int code, bool down)
    {
        Send(Evdev.Key, code, down ? 1 : 0);
        Send(Evdev.Syn, Evdev.SynReport, 0);
        if (down)
        {
            _held.Add(code);
        }
        else
        {
            _held.Remove(code);
        }
    }

    /// <summary>An axis moving, followed by the report.</summary>
    public void Axis(int code, int value)
    {
        Send(Evdev.Abs, code, value);
        Send(Evdev.Syn, Evdev.SynReport, 0);
        _resting[code] = value;
    }

    /// <summary>
    /// Changes what <see cref="Resync"/> reports without queueing an event, which is how the kernel
    /// dropping events looks from outside.
    /// </summary>
    public void Drift(IEnumerable<int>? buttons = null, IEnumerable<KeyValuePair<int, int>>? axes = null)
    {
        if (buttons is not null)
        {
            _held.Clear();
            foreach (var code in buttons)
            {
                _held.Add(code);
            }
        }

        if (axes is not null)
        {
            _resting.Clear();
            foreach (var (code, value) in axes)
            {
                _resting[code] = value;
            }
        }
    }

    public byte[] Drain()
    {
        var bytes = _queued.ToArray();
        _queued.Clear();
        return bytes;
    }

    public PadSnapshot Resync() =>
        new([.. _held], new Dictionary<int, int>(_resting));

    public void Rumble(double strong, double weak, TimeSpan duration) => Rumbles.Add((strong, weak, duration));

    public void Close() => _alive = false;
}

/// <summary>A backend a test fills and empties.</summary>
public sealed class RecordedPads(IEnumerable<RecordedPad>? pads = null) : IPadBackend
{
    public List<RecordedPad> Devices { get; } = [.. pads ?? []];

    public void Attach(RecordedPad pad) => Devices.Add(pad);

    public void Detach(RecordedPad pad)
    {
        pad.Unplug();
        Devices.Remove(pad);
    }

    public IReadOnlyList<IPadDevice> Scan() => [.. Devices.Where(pad => pad.IsAlive)];

    public void Close()
    {
        foreach (var pad in Devices)
        {
            pad.Close();
        }

        Devices.Clear();
    }
}
