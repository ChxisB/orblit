namespace Orblit.Progress;

/// <summary>A name a game gives to a number.</summary>
public abstract class ProgressName
{
    // Closed to this assembly.
    private protected ProgressName(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

/// <summary>A whole unsigned 32 bit integer.</summary>
public sealed class Whole(string name) : ProgressName(name);

/// <summary>
/// <paramref name="bits"/> bits of the <see cref="Whole"/> called
/// <paramref name="of"/>, from bit <paramref name="low"/> up. Writing a slice
/// leaves the other bits of that whole as they were.
/// </summary>
public sealed class Slice(string name, string of, int low, int bits) : ProgressName(name)
{
    public string Of { get; } = of;

    public int Low { get; } = low;

    public int Bits { get; } = bits;
}

/// <summary>Where a name's bits are: in slot <paramref name="Slot"/>, <paramref name="Bits"/> of them from bit <paramref name="Low"/>.</summary>
public readonly record struct Place(int Slot, int Low, int Bits)
{
    /// <summary>The bits of its slot that this place covers.</summary>
    internal long Mask => ((1L << Bits) - 1 << Low) & 0xFFFFFFFFL;
}

/// <summary>
/// The names a game uses and where each one lives.
/// A slot is the number stored for one <see cref="Whole"/>, numbered from zero
/// in the order the wholes are listed. A slice shares its whole's slot. Adding
/// names at the end keeps every older slot where it was, so a save from before
/// still loads. Taking a name out or reordering them moves slots, and the game
/// reads the old save under the old schema and writes what it wants to keep by
/// name. <see cref="Version"/> says which schema a save was written under.
/// </summary>
public sealed class ProgressSchema
{
    private const int MaxSlots = 0xFFFF;

    private readonly Dictionary<string, Place> _places = [];

    public ProgressSchema(int version, IReadOnlyList<ProgressName> names)
    {
        if (version is < 1 or > 0xFFFF)
        {
            throw new ArgumentException($"The version {version} is outside 1 to 65535.");
        }

        var slots = 0;
        foreach (var whole in names.OfType<Whole>())
        {
            Claim(whole.Name, new Place(slots++, 0, 32));
        }

        if (slots > MaxSlots)
        {
            throw new ArgumentException($"The names hold too many wholes: {slots}.");
        }

        var wholes = new Dictionary<string, Place>(_places);
        foreach (var slice in names.OfType<Slice>())
        {
            Claim(slice.Name, SlicePlace(slice, wholes));
        }

        Version = version;
        SlotCount = slots;
    }

    public int Version { get; }

    /// <summary>How many numbers a table holds.</summary>
    public int SlotCount { get; }

    public bool Has(string name) => _places.ContainsKey(name);

    /// <summary>Whether <paramref name="value"/> fits the bits of <paramref name="name"/>. Throws for a name not in the schema.</summary>
    public bool Fits(string name, long value)
    {
        var place = PlaceOf(name);
        return value >= 0 && value <= place.Mask >> place.Low;
    }

    /// <summary>Where <paramref name="name"/> lives. Throws <see cref="ArgumentException"/> for a name not in the schema.</summary>
    public Place PlaceOf(string name) =>
        _places.TryGetValue(name, out var place)
            ? place
            : throw new ArgumentException($"The name {name} is unknown.");

    private void Claim(string name, Place place)
    {
        if (name.Length == 0)
        {
            throw new ArgumentException("A name is empty.");
        }

        if (!_places.TryAdd(name, place))
        {
            throw new ArgumentException($"The name {name} is used twice.");
        }
    }

    private Place SlicePlace(Slice slice, Dictionary<string, Place> wholes)
    {
        if (!wholes.TryGetValue(slice.Of, out var parent))
        {
            throw new ArgumentException($"{slice.Of}, the whole of {slice.Name}, is not a whole.");
        }

        if (slice.Bits < 1 || slice.Low < 0 || slice.Low + slice.Bits > 32)
        {
            throw new ArgumentException($"{slice.Name}: {slice.Low}+{slice.Bits} does not fit in 32 bits.");
        }

        var place = new Place(parent.Slot, slice.Low, slice.Bits);
        foreach (var (key, other) in _places)
        {
            if (!wholes.ContainsKey(key) && other.Slot == place.Slot && (other.Mask & place.Mask) != 0)
            {
                throw new ArgumentException($"{slice.Name} overlaps another slice.");
            }
        }

        return place;
    }
}
