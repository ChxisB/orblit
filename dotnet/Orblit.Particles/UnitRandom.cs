namespace Orblit.Particles;

internal static class UnitRandom
{
    private const long Low32 = 0xFFFFFFFF;

    /// <summary>
    /// A number from 0 up to, and not including, 1 that depends on its three
    /// arguments and nothing else.
    /// </summary>
    /// <remarks>
    /// Written out rather than taken from <see cref="Random"/> because it must
    /// give the same answer on every machine and for ever: a particle is found
    /// again by asking for it, not by remembering it.
    /// </remarks>
    public static double At(long seed, long index, long channel)
    {
        var h = Times(index, 0x9E3779B1) ^ Times(channel + 1, 0x85EBCA6B) ^ Times(seed, 0xC2B2AE35);
        h ^= h >> 16;
        h = Times(h, 0x7FEB352D);
        h ^= h >> 15;
        h = Times(h, 0x846CA68B);
        h ^= h >> 16;
        return h / 4294967296.0;
    }

    /// <summary>32-bit wrapping multiply, which does not depend on the platform's integer width.</summary>
    private static long Times(long a, long b)
    {
        var x = a & Low32;
        return (((x & 0xFFFF) * b) + ((((x >> 16) * b) & 0xFFFF) * 0x10000)) & Low32;
    }
}
