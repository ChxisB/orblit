namespace Orblit.Asset;

/// <summary>
/// An image in the only layout anything here deals in: RGBA8, row 0 at the top, which is both what the
/// packer wants and where glTF's UV origin is.
/// </summary>
internal sealed class Rgba
{
    public Rgba(int width, int height, byte[] pixels)
    {
        if (pixels.Length != (long)width * height * 4)
        {
            throw new ArgumentException("an RGBA8 image is width x height x 4 bytes", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    /// <summary>An image of one colour.</summary>
    public static Rgba Filled(int width, int height, IReadOnlyList<int> rgba)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            for (var c = 0; c < 4; c++)
            {
                pixels[i + c] = (byte)rgba[c];
            }
        }

        return new Rgba(width, height, pixels);
    }
}
