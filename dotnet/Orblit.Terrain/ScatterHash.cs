namespace Orblit.Terrain;

/// <summary>
/// The number mixing and colour conversion the scatter pattern is made of. Every step works on
/// 32-bit whole numbers, so the same ground scatters the same on every machine.
/// </summary>
internal static class ScatterHash
{
    /// <summary>Stirs a 32-bit number so that each bit out depends on every bit in.</summary>
    public static uint Mix(uint x)
    {
        var h = x;
        h ^= h >> 16;
        h *= 0x7FEB352D;
        h ^= h >> 15;
        h *= 0x846CA68B;
        h ^= h >> 16;
        return h;
    }

    /// <summary>
    /// The <paramref name="n"/>th number between 0 and 1 drawn for the spot <paramref name="spot"/>.
    /// Each is its own hash rather than the next of a sequence, so a layer that draws fewer does
    /// not shift what the rest draw.
    /// </summary>
    public static double Draw(uint spot, int n) => Mix(spot ^ ((uint)(n + 1) * 0x9E3779B9)) / 4294967296.0;

    /// <summary>The low 32 bits of <paramref name="value"/>, so a negative cell counts the way it did in Dart.</summary>
    public static uint Low(long value) => (uint)(value & 0xFFFFFFFFL);

    /// <summary>An sRGB <c>0xRRGGBB</c> colour as linear RGB.</summary>
    public static (double Red, double Green, double Blue) Linear(long colour) =>
        (Channel((colour >> 16) & 0xFF), Channel((colour >> 8) & 0xFF), Channel(colour & 0xFF));

    /// <summary>One sRGB byte as a linear value, 0 to 1.</summary>
    public static double Channel(long value)
    {
        var scaled = value / 255.0;
        return scaled <= 0.04045 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
    }
}
