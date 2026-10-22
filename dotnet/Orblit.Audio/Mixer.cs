namespace Orblit.Audio;

/// <summary>
/// The checks a gain and a length of time share. Not part of the public
/// surface.
/// </summary>
internal static class Checks
{
    /// <summary>A linear gain, or an exception for a negative or non-finite one.</summary>
    internal static double Gain(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "must be zero or more");
        }

        return value;
    }

    /// <summary>A length of time, or an exception for a negative or non-finite one.</summary>
    internal static double Seconds(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "must be zero or more");
        }

        return value;
    }
}

/// <summary>
/// A node in the mixer tree. A voice plays on a bus and every bus above it
/// scales what is heard.
/// </summary>
public sealed class Bus
{
    private double _volume = 1;

    internal Bus(string name, Bus? parent)
    {
        Name = name;
        Parent = parent;
    }

    public string Name { get; }

    /// <summary>Null for the master bus.</summary>
    public Bus? Parent { get; }

    public bool Muted { get; set; }

    public double Volume
    {
        get => _volume;
        set => _volume = Checks.Gain(value, nameof(Volume));
    }

    /// <summary>What this bus and every bus above it do to a voice. A mute anywhere in the chain silences it.</summary>
    public double Gain => Muted ? 0 : _volume * (Parent?.Gain ?? 1);
}

/// <summary>The tree of buses, with the master bus at the root.</summary>
public sealed class Mixer
{
    private readonly Dictionary<string, Bus> _buses = [];

    public Mixer()
    {
        Master = new Bus("master", null);
        _buses[Master.Name] = Master;
    }

    public Bus Master { get; }

    public Bus Bus(string name) =>
        _buses.TryGetValue(name, out var bus)
            ? bus
            : throw new ArgumentOutOfRangeException(nameof(name), name, "there is no such bus");

    /// <summary>Whether <paramref name="bus"/> is one of this mixer's.</summary>
    public bool Owns(Bus bus) => _buses.TryGetValue(bus.Name, out var found) && ReferenceEquals(found, bus);

    /// <summary>Adds a bus named <paramref name="name"/> below <paramref name="under"/>, or below the master bus.</summary>
    public Bus Add(string name, Bus? under = null)
    {
        if (_buses.ContainsKey(name))
        {
            throw new ArgumentOutOfRangeException(nameof(name), name, "there is already a bus");
        }

        var parent = under ?? Master;
        if (!Owns(parent))
        {
            throw new ArgumentOutOfRangeException(nameof(under), under, "belongs to another mixer");
        }

        var bus = new Bus(name, parent);
        _buses[name] = bus;
        return bus;
    }
}
