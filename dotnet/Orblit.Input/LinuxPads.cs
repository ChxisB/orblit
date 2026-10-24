using System.Globalization;
using System.Runtime.InteropServices;

namespace Orblit.Input;

/// <summary>
/// Gamepads on Linux, through evdev. Finding pads and deciding which event devices are pads is done
/// by reading sysfs, which is plain text. Reading a pad is four libc calls: open, read, ioctl and
/// close.
/// </summary>
/// <remarks>
/// evdev and not <c>/dev/input/js*</c>. The joystick interface is older and easier to read, but it
/// is deprecated, cannot say what a button means, reports axes without their ranges, and on a
/// modern distribution comes from a compatibility layer that may not be loaded. evdev is what the
/// kernel's gamepad drivers describe themselves in and what a Steam Deck presents.
/// </remarks>
public sealed class LinuxPads : IPadBackend
{
    private const string NodePrefix = "event";

    private readonly string _devices;
    private readonly string _sysfs;
    private readonly OrderedDictionary<string, LinuxPad> _open = [];
    private readonly List<string> _problems = [];

    /// <param name="devices">
    /// Where the device nodes are. A container's <c>/dev</c> is a tmpfs with nothing in it, so a
    /// harness mounts a real devtmpfs elsewhere and points here at it.
    /// </param>
    /// <param name="sysfs">Where the descriptions are.</param>
    public LinuxPads(string devices = "/dev/input", string sysfs = "/sys/class/input")
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("LinuxPads reads evdev, which is Linux only");
        }

        _devices = devices;
        _sysfs = sysfs;
    }

    /// <summary>
    /// Devices that look like pads but could not be opened, and why. Almost always a permission:
    /// reading an event device needs membership of the <c>input</c> group on most distributions.
    /// The alternative is a game that reports no controller while one is plainly plugged in.
    /// </summary>
    public IReadOnlyList<string> Problems => _problems;

    public IReadOnlyList<IPadDevice> Scan()
    {
        _problems.Clear();
        var found = new OrderedDictionary<string, LinuxPad>();
        foreach (var node in EventNodes())
        {
            var device = _open.TryGetValue(node, out var held) && held.IsAlive ? held : Open(node);
            if (device is not null)
            {
                found[node] = device;
            }
        }

        // Anything that was open and is no longer listed has gone.
        foreach (var (node, device) in _open)
        {
            if (!found.TryGetValue(node, out var current) || current != device)
            {
                device.Close();
            }
        }

        _open.Clear();
        foreach (var (node, device) in found)
        {
            _open[node] = device;
        }

        return [.. found.Values];
    }

    public void Close()
    {
        foreach (var device in _open.Values)
        {
            device.Close();
        }

        _open.Clear();
    }

    /// <summary>The event nodes in number order, so player numbers do not depend on the order the directory lists them.</summary>
    private List<string> EventNodes() => !Directory.Exists(_sysfs)
        ? []
        : [.. Directory.EnumerateFileSystemEntries(_sysfs)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => EventNumber(name) is not null)
            .OrderBy(name => EventNumber(name))];

    private static int? EventNumber(string node) =>
        node.StartsWith(NodePrefix, StringComparison.Ordinal)
        && int.TryParse(node.AsSpan(NodePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private LinuxPad? Open(string node)
    {
        var description = Path.Combine(_sysfs, node, "device");
        var capabilities = EvdevCapabilities.Parse(
            Text(Path.Combine(description, "capabilities", "ev")),
            Text(Path.Combine(description, "capabilities", "key")),
            Text(Path.Combine(description, "capabilities", "abs")));
        if (!capabilities.IsGamepad)
        {
            return null;
        }

        var identity = new PadIdentity(
            Text(Path.Combine(description, "name")),
            Hex(Path.Combine(description, "id", "vendor")),
            Hex(Path.Combine(description, "id", "product")),
            Hex(Path.Combine(description, "id", "version")),
            Hex(Path.Combine(description, "id", "bustype")),
            Text(Path.Combine(description, "uniq")),
            Text(Path.Combine(description, "phys")));

        var path = Path.Combine(_devices, node);
        var fd = LibC.Open(path, LibC.ReadOnly | LibC.NonBlocking | LibC.CloseOnExec);
        if (fd >= 0)
        {
            return new LinuxPad(fd, identity, capabilities);
        }

        _problems.Add($"{path}: errno {Marshal.GetLastPInvokeError()}");
        return null;
    }

    private static string Text(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static int Hex(string path) =>
        int.TryParse(Text(path), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
