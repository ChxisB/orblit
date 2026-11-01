namespace Orblit.Terrain;

/// <summary>Whole-number arithmetic the terrain files and texel coordinates need.</summary>
public static class TerrainMath
{
    /// <summary>
    /// <paramref name="a"/> divided by <paramref name="b"/> and rounded down, for a positive
    /// <paramref name="b"/>. C#'s <c>/</c> rounds towards zero, which puts texel -1 in region 0.
    /// </summary>
    public static long FloorDiv(long a, long b)
    {
        // The remainder is never negative.
        var remainder = a % b;
        if (remainder < 0)
        {
            remainder += Math.Abs(b);
        }

        return (a - remainder) / b;
    }
}
