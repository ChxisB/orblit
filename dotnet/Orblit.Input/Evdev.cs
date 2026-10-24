using System.Runtime.InteropServices;

namespace Orblit.Input;

/// <summary>The Linux input event constants a pad reader needs, and the record they come in.</summary>
public static class Evdev
{
    public const int Syn = 0x00;

    public const int Key = 0x01;

    public const int Rel = 0x02;

    public const int Abs = 0x03;

    /// <summary>The code of a <see cref="Syn"/> event that ends a group of changes.</summary>
    public const int SynReport = 0;

    /// <summary>The code of a <see cref="Syn"/> event that says the kernel threw events away.</summary>
    public const int SynDropped = 3;

    /// <summary>The first gamepad button code. A device with it is a pad.</summary>
    public const int BtnGamepad = 0x130;

    /// <summary>One record as the kernel writes it, in the byte order of this machine.</summary>
    public static byte[] Encode(int type, int code, int value, int timeBytes = 16)
    {
        var bytes = new byte[timeBytes + 8];
        MemoryMarshal.Write(bytes.AsSpan(timeBytes), (ushort)type);
        MemoryMarshal.Write(bytes.AsSpan(timeBytes + 2), (ushort)code);
        MemoryMarshal.Write(bytes.AsSpan(timeBytes + 4), value);
        return bytes;
    }
}

public readonly record struct EvdevEvent(int Type, int Code, int Value)
{
    public override string ToString() => $"EvdevEvent({Type}, {Code}, {Value})";
}
