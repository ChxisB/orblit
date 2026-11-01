using System.Runtime.InteropServices;

namespace Orblit.Input;

/// <summary>
/// Turns the bytes read from a device into events. A read can end in the middle of a record, so
/// the fragment is kept and finished by the next read. A decoder that dropped it would work until
/// the first busy frame and then lose an event.
/// </summary>
public sealed class EvdevDecoder(int timeBytes = 16)
{
    private readonly List<byte> _spare = [];

    /// <summary>The bytes of the timestamp. 16 on a 64-bit kernel and 8 on a 32-bit one.</summary>
    public int TimeBytes { get; } = timeBytes;

    public int RecordBytes => TimeBytes + 8;

    public bool HasPartial => _spare.Count > 0;

    public IReadOnlyList<EvdevEvent> Decode(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty && _spare.Count == 0)
        {
            return [];
        }

        byte[] bytes;
        if (_spare.Count == 0)
        {
            bytes = chunk.ToArray();
        }
        else
        {
            _spare.AddRange(chunk);
            bytes = [.. _spare];
            _spare.Clear();
        }

        var whole = bytes.Length / RecordBytes;
        var events = new List<EvdevEvent>(whole);
        for (var i = 0; i < whole; i++)
        {
            var at = (i * RecordBytes) + TimeBytes;
            events.Add(new EvdevEvent(
                MemoryMarshal.Read<ushort>(bytes.AsSpan(at)),
                MemoryMarshal.Read<ushort>(bytes.AsSpan(at + 2)),
                MemoryMarshal.Read<int>(bytes.AsSpan(at + 4))));
        }

        _spare.AddRange(bytes.AsSpan(whole * RecordBytes));
        return events;
    }

    /// <summary>Throws the fragment away, so what follows is read from the start of a record.</summary>
    public void Reset() => _spare.Clear();
}
