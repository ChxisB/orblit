using System.Runtime.InteropServices;
using System.Text;

namespace Orblit.Input.Tests;

/// <summary>What the uinput tests need from the machine, and the reason they skip without it.</summary>
public static class LinuxRig
{
    public const string SkipReason =
        "needs Linux, a writable /dev/uinput and real device nodes. See tool/check_input_linux.sh and ORBLIT_INPUT_DEV";

    public const string LinuxOnlyReason = "reads sysfs and opens files through libc, which is Linux only";

    public const string HasBackendReason = "Linux has a backend, so there is nothing to refuse";

    public static bool IsLinux => OperatingSystem.IsLinux();

    /// <summary>Where device nodes appear. <c>/dev/input</c> on a real machine.</summary>
    public static string DeviceDirectory => Environment.GetEnvironmentVariable("ORBLIT_INPUT_DEV") ?? "/dev/input";

    // Writable, not merely present. A CI runner has the uinput module loaded, so the node exists,
    // while the unprivileged user cannot open it. Opening for write creates no device. That takes
    // the create request VirtualPad sends.
    public static bool Available { get; } = IsLinux && CanWrite("/dev/uinput") && Directory.Exists(DeviceDirectory);

    private static bool CanWrite(string path)
    {
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Write);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>
/// A gamepad that does not exist, for exercising the one that would. Linux can be asked to invent
/// one: <c>/dev/uinput</c> takes the same description a driver would give (which buttons, which
/// axes, what each axis can report) and creates a real event device that the rest of the system,
/// including <see cref="LinuxPads"/>, cannot tell from hardware. The bytes come from the kernel
/// through the same device node a real pad's would. This exercises every line of ours and none of
/// any real driver's.
/// </summary>
internal sealed partial class VirtualPad : IDisposable
{
    private const int ReadWrite = 2;
    private const int NonBlocking = 0x800;

    private const uint SetEventBit = 0x40045564;
    private const uint SetKeyBit = 0x40045565;
    private const uint SetAbsBit = 0x40045567;
    private const uint DeviceSetup = 0x405c5503;
    private const uint AbsSetup = 0x401c5504;
    private const uint DeviceCreate = 0x5501;
    private const uint DeviceDestroy = 0x5502;

    private const int EvSyn = 0x00;
    private const int EvKey = 0x01;
    private const int EvAbs = 0x03;
    private const int SynReport = 0;

    // struct uinput_setup: an eight-byte id, an eighty-byte name and a count.
    private const int SetupBytes = 92;

    // struct uinput_abs_setup: a code, two bytes of padding and an absinfo.
    private const int AbsSetupBytes = 28;

    // One record, on a 64-bit kernel.
    private const int EventBytes = 24;

    /// <summary>The kernel's gamepad buttons: four faces, two shoulders, two triggers, select, start, guide and the two sticks.</summary>
    private static readonly int[] Buttons = [0x130, 0x131, 0x133, 0x134, 0x136, 0x137, 0x138, 0x139, 0x13a, 0x13b, 0x13c, 0x13d, 0x13e];

    /// <summary>Two sticks on a signed sixteen-bit range, two triggers on a byte, and the d-pad as a hat.</summary>
    private static readonly (int Code, int Minimum, int Maximum)[] Axes =
    [
        (0x00, -32768, 32767),
        (0x01, -32768, 32767),
        (0x03, -32768, 32767),
        (0x04, -32768, 32767),
        (0x02, 0, 255),
        (0x05, 0, 255),
        (0x10, -1, 1),
        (0x11, -1, 1),
    ];

    private readonly int _fd;
    private bool _alive = true;

    private VirtualPad(int fd) => _fd = fd;

    /// <summary>
    /// Creates one, or throws with what went wrong. It reports what a Steam Deck does: Valve's vendor
    /// id and the Deck's product id.
    /// </summary>
    public static VirtualPad Create(string name = "Orblit Virtual Pad")
    {
        var fd = Open("/dev/uinput", ReadWrite | NonBlocking);
        if (fd < 0)
        {
            throw new InvalidOperationException("could not open /dev/uinput. It needs the uinput module and write permission on the node.");
        }

        var pad = new VirtualPad(fd);
        try
        {
            pad.Describe(name);
            return pad;
        }
        catch (InvalidOperationException)
        {
            pad.Dispose();
            throw;
        }
    }

    /// <summary>A button down or up, and the report that publishes it.</summary>
    public void Button(int code, bool down)
    {
        Send(EvKey, code, down ? 1 : 0);
        Send(EvSyn, SynReport, 0);
    }

    /// <summary>An axis moving, and the report that publishes it.</summary>
    public void Axis(int code, int value)
    {
        Send(EvAbs, code, value);
        Send(EvSyn, SynReport, 0);
    }

    /// <summary>Unplugs it. The event node disappears, as it does for a pad whose battery has died.</summary>
    public void Dispose()
    {
        if (!_alive)
        {
            return;
        }

        _alive = false;
        _ = IoctlValue(_fd, DeviceDestroy, 0);
        Close(_fd);
    }

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "ioctl")]
    private static partial int IoctlValue(int fd, nuint request, nint value);

    [LibraryImport("libc", EntryPoint = "ioctl")]
    private static partial int IoctlBytes(int fd, nuint request, Span<byte> data);

    [LibraryImport("libc", EntryPoint = "write")]
    private static partial nint Write(int fd, ReadOnlySpan<byte> data, nint count);

    [LibraryImport("libc", EntryPoint = "close")]
    private static partial void Close(int fd);

    private static void Must(int result, string what)
    {
        if (result < 0)
        {
            throw new InvalidOperationException($"{what} failed");
        }
    }

    private void Describe(string name)
    {
        Must(IoctlValue(_fd, SetEventBit, EvKey), "UI_SET_EVBIT EV_KEY");
        foreach (var key in Buttons)
        {
            Must(IoctlValue(_fd, SetKeyBit, key), $"UI_SET_KEYBIT {key}");
        }

        Must(IoctlValue(_fd, SetEventBit, EvAbs), "UI_SET_EVBIT EV_ABS");
        foreach (var (code, minimum, maximum) in Axes)
        {
            Must(IoctlValue(_fd, SetAbsBit, code), $"UI_SET_ABSBIT {code}");
            var setup = new byte[AbsSetupBytes];
            BitConverter.TryWriteBytes(setup.AsSpan(0), (ushort)code);
            BitConverter.TryWriteBytes(setup.AsSpan(8), minimum);
            BitConverter.TryWriteBytes(setup.AsSpan(12), maximum);
            Must(IoctlBytes(_fd, AbsSetup, setup), $"UI_ABS_SETUP {code}");
        }

        var device = new byte[SetupBytes];
        BitConverter.TryWriteBytes(device.AsSpan(0), (ushort)0x03);
        BitConverter.TryWriteBytes(device.AsSpan(2), (ushort)0x28de);
        BitConverter.TryWriteBytes(device.AsSpan(4), (ushort)0x1205);
        BitConverter.TryWriteBytes(device.AsSpan(6), (ushort)0x0111);
        Encoding.ASCII.GetBytes(name.AsSpan(0, Math.Min(79, name.Length)), device.AsSpan(8));
        Must(IoctlBytes(_fd, DeviceSetup, device), "UI_DEV_SETUP");
        Must(IoctlValue(_fd, DeviceCreate, 0), "UI_DEV_CREATE");
    }

    private void Send(int type, int code, int value)
    {
        if (!_alive)
        {
            return;
        }

        var record = new byte[EventBytes];
        BitConverter.TryWriteBytes(record.AsSpan(16), (ushort)type);
        BitConverter.TryWriteBytes(record.AsSpan(18), (ushort)code);
        BitConverter.TryWriteBytes(record.AsSpan(20), value);
        _ = Write(_fd, record, EventBytes);
    }
}
