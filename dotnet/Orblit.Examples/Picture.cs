namespace Orblit.Examples;

/// <summary>Square RGBA8 pictures, drawn a pixel at a time.</summary>
internal static class Picture
{
    /// <summary>Texels on a side of every picture the examples draw.</summary>
    public const int Size = 256;

    /// <summary>
    /// A picture whose pixel at column x and row y is <paramref name="at"/> of
    /// them. Each channel is rounded half away from nought and held to a byte.
    /// </summary>
    public static byte[] Drawn(Func<int, int, (double R, double G, double B, double A)> at)
    {
        var rgba = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b, a) = at(x, y);
                var i = (y * Size + x) * 4;
                rgba[i] = Channel(r);
                rgba[i + 1] = Channel(g);
                rgba[i + 2] = Channel(b);
                rgba[i + 3] = Channel(a);
            }
        }

        return rgba;
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}
