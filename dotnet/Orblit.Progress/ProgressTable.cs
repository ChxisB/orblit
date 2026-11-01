using System.Buffers.Binary;

namespace Orblit.Progress;

/// <summary>
/// The slots a write changed, with the new value of each. A slot holds a whole
/// and every slice of it, so a delta carries slots and not names.
/// </summary>
public sealed class Changes : IEquatable<Changes>
{
    private readonly OrderedDictionary<int, long> _values;

    internal Changes(OrderedDictionary<int, long> values)
    {
        _values = values;
    }

    public bool IsEmpty => _values.Count == 0;

    /// <summary>The slots that changed, in order.</summary>
    public IReadOnlyList<int> Slots => _values.Keys;

    /// <summary>
    /// Reads what <see cref="Encode"/> wrote, or returns null when the bytes are
    /// not that, name a slot <paramref name="schema"/> does not have, or name
    /// one twice.
    /// </summary>
    public static Changes? TryDecode(ProgressSchema schema, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if (bytes.Length != 2 + (6 * count))
        {
            return null;
        }

        var values = new OrderedDictionary<int, long>();
        for (var i = 0; i < count; i++)
        {
            var slot = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(2 + (6 * i))..]);
            if (slot >= schema.SlotCount)
            {
                return null;
            }

            values[slot] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 + (6 * i))..]);
        }

        return values.Count == count ? new Changes(values) : null;
    }

    /// <summary>The new value of <paramref name="slot"/>, or null if it did not change.</summary>
    public long? ValueOf(int slot) => _values.TryGetValue(slot, out var value) ? value : null;

    public byte[] Encode()
    {
        var bytes = new byte[2 + (6 * _values.Count)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)_values.Count);
        var at = 2;
        foreach (var (slot, value) in _values)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), (ushort)slot);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 2), (uint)value);
            at += 6;
        }

        return bytes;
    }

    public bool Equals(Changes? other) =>
        other is not null
        && other._values.Count == _values.Count
        && _values.All(e => other._values.TryGetValue(e.Key, out var value) && value == e.Value);

    public override bool Equals(object? obj) => Equals(obj as Changes);

    // Summed, so the order the slots were written in does not matter.
    public override int GetHashCode() => _values.Aggregate(0, (hash, e) => hash + HashCode.Combine(e.Key, e.Value));
}

/// <summary>One player's named integers. A table never changes: a write gives a new one.</summary>
public sealed class ProgressTable
{
    private const byte Format = 1;
    private const long All = 0xFFFFFFFF;

    private readonly uint[] _slots;

    public ProgressTable(ProgressSchema schema)
        : this(schema, new uint[schema.SlotCount])
    {
    }

    private ProgressTable(ProgressSchema schema, uint[] slots)
    {
        Schema = schema;
        _slots = slots;
    }

    public ProgressSchema Schema { get; }

    /// <summary>
    /// Reads what <see cref="Encode"/> wrote. Names added to
    /// <paramref name="schema"/> since the save was made start at zero. Throws
    /// <see cref="FormatException"/> for bytes that are not a table, or that
    /// were written under a later schema.
    /// </summary>
    public static ProgressTable Decode(ProgressSchema schema, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 5)
        {
            throw new FormatException("Too short for a table.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[3..]);
        if (bytes[0] != Format)
        {
            throw new FormatException($"Unknown table format {bytes[0]}.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[1..]) > schema.Version
            || count > schema.SlotCount
            || bytes.Length != 5 + (4 * count))
        {
            throw new FormatException("Not a table of this schema.");
        }

        var slots = new uint[schema.SlotCount];
        for (var i = 0; i < count; i++)
        {
            slots[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(5 + (4 * i))..]);
        }

        return new ProgressTable(schema, slots);
    }

    /// <summary>The value of <paramref name="name"/>, read from its own bits.</summary>
    public long Get(string name)
    {
        var place = Schema.PlaceOf(name);
        return (_slots[place.Slot] & place.Mask) >> place.Low;
    }

    /// <summary>
    /// A table with <paramref name="name"/> set to <paramref name="value"/>, and
    /// what changed. Other names that share bits with it keep theirs. Throws
    /// <see cref="ArgumentOutOfRangeException"/> when the value does not fit the
    /// name's bits.
    /// </summary>
    public (ProgressTable Table, Changes Changes) Set(string name, long value)
    {
        var place = Schema.PlaceOf(name);
        var mask = place.Mask >> place.Low;
        if (value < 0 || value > mask)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{name} holds 0 to {mask}");
        }

        var next = (uint[])_slots.Clone();
        next[place.Slot] = (uint)((_slots[place.Slot] & ~place.Mask & All) | (value << place.Low));
        var table = new ProgressTable(Schema, next);
        return (table, table.ChangesSince(this));
    }

    /// <summary>The slots whose value differs from <paramref name="older"/>'s.</summary>
    public Changes ChangesSince(ProgressTable older)
    {
        var values = new OrderedDictionary<int, long>();
        for (var slot = 0; slot < _slots.Length; slot++)
        {
            if (_slots[slot] != older._slots[slot])
            {
                values[slot] = _slots[slot];
            }
        }

        return new Changes(values);
    }

    /// <summary>A table with <paramref name="changes"/> written over this one.</summary>
    public ProgressTable Apply(Changes changes)
    {
        var next = (uint[])_slots.Clone();
        foreach (var slot in changes.Slots)
        {
            next[slot] = (uint)changes.ValueOf(slot)!.Value;
        }

        return new ProgressTable(Schema, next);
    }

    public byte[] Encode()
    {
        var bytes = new byte[5 + (4 * _slots.Length)];
        bytes[0] = Format;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1), (ushort)Schema.Version);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(3), (ushort)_slots.Length);
        for (var i = 0; i < _slots.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(5 + (4 * i)), _slots[i]);
        }

        return bytes;
    }
}
