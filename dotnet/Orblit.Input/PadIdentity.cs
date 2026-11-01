using System.Globalization;

namespace Orblit.Input;

/// <summary>What a device says it is. Two pads of one model can share everything but <see cref="Uniq"/> and <see cref="Path"/>.</summary>
public sealed class PadIdentity
{
    // F2: optional parameters, so a call names only what it sets.
    public PadIdentity(string name, int vendor = 0, int product = 0, int version = 0, int bus = 0, string uniq = "", string path = "")
    {
        Name = name;
        Vendor = vendor;
        Product = product;
        Version = version;
        Bus = bus;
        Uniq = uniq;
        Path = path;
    }

    public string Name { get; }

    public int Vendor { get; }

    public int Product { get; }

    public int Version { get; }

    public int Bus { get; }

    /// <summary>The unique id, such as a Bluetooth address. Empty when the device has none.</summary>
    public string Uniq { get; }

    /// <summary>The physical path, such as a USB port. Empty when unknown.</summary>
    public string Path { get; }

    /// <summary>What stays the same for one pad between a disconnect and the next connect.</summary>
    public string Token =>
        Uniq.Length > 0 ? $"uniq:{Uniq}" : (Path.Length > 0 ? $"path:{Path}" : Name);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Name} ({Vendor:x4}:{Product:x4})");
}
