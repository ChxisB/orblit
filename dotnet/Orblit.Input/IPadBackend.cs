namespace Orblit.Input;

/// <summary>
/// Where pads come from. The one thing a platform has to write, so the rest of the package can be
/// tested against <see cref="RecordedPads"/> with no hardware.
/// </summary>
public interface IPadBackend
{
    /// <summary>The pads connected now. The same object comes back for a pad that stays connected.</summary>
    IReadOnlyList<IPadDevice> Scan();

    void Close();
}

/// <summary>One physical pad.</summary>
public interface IPadDevice
{
    PadIdentity Identity { get; }

    EvdevCapabilities Capabilities { get; }

    /// <summary>The travel of each axis, by evdev code. An axis with no entry is read as the default range for its kind.</summary>
    IReadOnlyDictionary<int, PadAxisRange> Ranges { get; }

    bool IsAlive { get; }

    bool CanRumble { get; }

    /// <summary>The bytes the device has written since the last call. A fragment of a record is fine.</summary>
    byte[] Drain();

    /// <summary>What the device holds and rests at now, asked of it directly. Used on connect, and after the kernel drops events.</summary>
    PadSnapshot Resync();

    void Rumble(double strong, double weak, TimeSpan duration);

    void Close();
}

/// <summary>The buttons held and the axis values at one moment, by evdev code.</summary>
public readonly record struct PadSnapshot(IReadOnlyCollection<int> Buttons, IReadOnlyDictionary<int, int> Axes);

/// <summary>A backend with no pads. What a platform with no reader gets.</summary>
public sealed class NoPads : IPadBackend
{
    public static NoPads Instance { get; } = new();

    private NoPads()
    {
    }

    public IReadOnlyList<IPadDevice> Scan() => [];

    public void Close()
    {
    }
}

/// <summary>Called when a game asks a pad to rumble.</summary>
public delegate void RumbleFunction(double strong, double weak, TimeSpan duration);
