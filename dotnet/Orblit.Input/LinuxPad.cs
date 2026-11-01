using System.Runtime.InteropServices;
using System.Text;

namespace Orblit.Input;

/// <summary>One open event device.</summary>
internal sealed class LinuxPad : IPadDevice
{
    // Sixteen records at a time. A pad at its fastest sends a few hundred records a second, so one
    // call empties it at any frame rate worth having, and the loop in Drain covers the rest.
    private const int BufferBytes = 24 * 16;

    private readonly int _fd;
    private readonly byte[] _buffer = new byte[BufferBytes];
    private readonly byte[] _scratch = new byte[256];
    private readonly Dictionary<int, PadAxisRange> _ranges = [];

    // Whether the pad is still there. Separate from _closed, because a device stops being alive
    // several ways (unplugged mid-read, dropped by a rescan) before anything is given back.
    private bool _alive = true;

    private bool _closed;

    internal LinuxPad(int fd, PadIdentity identity, EvdevCapabilities capabilities)
    {
        _fd = fd;
        Capabilities = capabilities;
        ReadRanges();
        Identity = Completed(identity);
    }

    public PadIdentity Identity { get; }

    public EvdevCapabilities Capabilities { get; }

    public IReadOnlyDictionary<int, PadAxisRange> Ranges => _ranges;

    public bool IsAlive => _alive;

    // Force feedback on Linux is an ioctl per effect and nothing has built it, so a Linux pad says
    // it cannot rumble and does not pretend.
    public bool CanRumble => false;

    public byte[] Drain()
    {
        if (!_alive)
        {
            return [];
        }

        using var output = new MemoryStream();
        while (true)
        {
            var count = (int)LibC.Read(_fd, _buffer, BufferBytes);
            if (count > 0)
            {
                output.Write(_buffer, 0, count);

                // A short read has emptied the queue. A full one may not have.
                if (count < BufferBytes)
                {
                    break;
                }

                continue;
            }

            var errno = count == 0 ? 0 : Marshal.GetLastPInvokeError();
            if (errno == LibC.Interrupted)
            {
                continue;
            }

            // ENODEV is the pad being unplugged while it was read, which is the ordinary way a
            // wireless pad goes away and not an error.
            if (errno == LibC.NoDevice)
            {
                _alive = false;
            }

            break;
        }

        return output.ToArray();
    }

    public PadSnapshot Resync()
    {
        var buttons = new HashSet<int>();
        var axes = new Dictionary<int, int>();
        if (!_alive)
        {
            return new PadSnapshot(buttons, axes);
        }

        if (LibC.Ioctl(_fd, LibC.GetKeys96, _scratch) >= 0)
        {
            for (var bit = 0; bit < 96 * 8; bit++)
            {
                if ((_scratch[bit / 8] & (1 << (bit % 8))) != 0)
                {
                    buttons.Add(bit);
                }
            }
        }

        foreach (var code in Capabilities.Axes)
        {
            if (TryReadAbs(code))
            {
                axes[code] = BitConverter.ToInt32(_scratch, 0);
            }
        }

        return new PadSnapshot(buttons, axes);
    }

    public void Rumble(double strong, double weak, TimeSpan duration)
    {
    }

    // Idempotent, because being closed twice is the ordinary path. A rescan closes a device that
    // has gone, and the poll that notices the same absence closes it again on the way to reporting
    // the disconnection. The second close would hit a descriptor number another file may now hold.
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _alive = false;
        LibC.Close(_fd);
    }

    /// <summary>
    /// Fills in what sysfs did not give, from the driver. A device opened by path has no description
    /// beside it, and a pad with no vendor or product would be handed the fallback layout when the
    /// driver knows what it is.
    /// </summary>
    private PadIdentity Completed(PadIdentity identity)
    {
        var name = identity.Name.Length == 0 ? AskName() : identity.Name;
        if (identity.Vendor == 0 && identity.Product == 0 && LibC.Ioctl(_fd, LibC.GetId, _scratch) >= 0)
        {
            return new PadIdentity(name, Short(2), Short(4), Short(6), Short(0), identity.Uniq, identity.Path);
        }

        return name == identity.Name
            ? identity
            : new PadIdentity(name, identity.Vendor, identity.Product, identity.Version, identity.Bus, identity.Uniq, identity.Path);
    }

    private int Short(int offset) => BitConverter.ToUInt16(_scratch, offset);

    private string AskName()
    {
        if (LibC.Ioctl(_fd, LibC.GetName256, _scratch) < 0)
        {
            return "";
        }

        var end = Array.IndexOf(_scratch, (byte)0);
        return Encoding.Latin1.GetString(_scratch, 0, end < 0 ? 0 : end);
    }

    private bool TryReadAbs(int code) => LibC.Ioctl(_fd, LibC.GetAbs(code), _scratch) >= 0;

    /// <summary>
    /// What each axis can report. The one thing sysfs does not expose and the one thing that cannot
    /// be guessed: a trigger that runs 0 to 255 and one that runs 0 to 1023 look the same until
    /// somebody pulls them, and then the second reads a quarter strength for its whole travel.
    /// </summary>
    private void ReadRanges()
    {
        foreach (var code in Capabilities.Axes)
        {
            if (TryReadAbs(code))
            {
                _ranges[code] = new PadAxisRange(
                    BitConverter.ToInt32(_scratch, 4),
                    BitConverter.ToInt32(_scratch, 8),
                    flat: BitConverter.ToInt32(_scratch, 16),
                    fuzz: BitConverter.ToInt32(_scratch, 12));
            }
        }
    }
}
