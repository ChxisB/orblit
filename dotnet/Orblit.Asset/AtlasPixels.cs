using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>
/// The pixel work a model atlas needs: resizing a role's map to its cell, blitting it into a page, and
/// extruding its edge into the padding. It is kept apart from the rewrite because it is the part with no glTF
/// in it, and therefore the part that can be tested by looking at texels.
/// </summary>
internal static class AtlasPixels
{
    // A table rather than Math.Pow per texel: baking a factor into a 2048-square page is four million calls,
    // and the input is a byte, so there are only 256 answers.
    private static readonly float[] ToLinear = BuildToLinear();

    /// <summary>
    /// <paramref name="source"/> resized to <paramref name="width"/> by <paramref name="height"/> by
    /// averaging the source texels each destination texel covers. Area averaging rather than a filter with a
    /// kernel: it is exact when the ratio is a whole number, which is the case that actually happens (a 2048
    /// map into a 1024 cell), it never rings, and it is the same answer on every machine, which a cook has to
    /// be or the cache is lying.
    /// </summary>
    public static Rgba Resample(Rgba source, int width, int height, bool normal = false)
    {
        if (source.Width == width && source.Height == height)
        {
            return source;
        }

        if (width <= 0 || height <= 0)
        {
            return new Rgba(0, 0, []);
        }

        var output = new byte[width * height * 4];
        var area = new Area(source, (double)source.Width / width, (double)source.Height / height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var to = ((y * width) + x) * 4;
                var (r, g, b, a) = area.Average(x, y);
                output[to] = r;
                output[to + 1] = g;
                output[to + 2] = b;
                output[to + 3] = a;
            }
        }

        var result = new Rgba(width, height, output);
        // An averaged normal is shorter than the ones it averaged, and a shorter normal lights as though the
        // surface were flatter than it is. Putting it back on the unit sphere is the difference between a
        // resized normal map and a slightly deflated one.
        if (normal)
        {
            Renormalise(result);
        }

        return result;
    }

    /// <summary>Puts every texel of a normal map back on the unit sphere.</summary>
    public static void Renormalise(Rgba image)
    {
        var pixels = image.Pixels;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var x = (pixels[i] / 127.5) - 1.0;
            var y = (pixels[i + 1] / 127.5) - 1.0;
            var z = (pixels[i + 2] / 127.5) - 1.0;
            var length = Math.Sqrt((x * x) + (y * y) + (z * z));
            if (length < 1e-6)
            {
                pixels[i] = 128;
                pixels[i + 1] = 128;
                pixels[i + 2] = 255;
                continue;
            }

            pixels[i] = Channel(((x / length) + 1.0) * 127.5);
            pixels[i + 1] = Channel(((y / length) + 1.0) * 127.5);
            pixels[i + 2] = Channel(((z / length) + 1.0) * 127.5);
        }
    }

    /// <summary>
    /// Multiplies a factor into an image, in the space the factor is defined in. glTF's factors are linear and
    /// its colour maps are sRGB-encoded, so a base colour factor cannot simply be multiplied into the bytes:
    /// doing that darkens midtones by about the amount people then try to fix with a light. Folding the factor
    /// in is what lets two materials that differ only by tint become one material afterwards, which is where
    /// the draw calls go.
    /// </summary>
    public static void MultiplyInto(Rgba image, IReadOnlyList<double> factor, bool srgb)
    {
        var pixels = image.Pixels;
        var channels = Math.Min(4, factor.Count);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            for (var c = 0; c < channels; c++)
            {
                if (factor[c] == 1.0)
                {
                    continue;
                }

                // Alpha is linear whatever the colour channels are.
                pixels[i + c] = srgb && c < 3
                    ? ToSrgb(ToLinear[pixels[i + c]] * factor[c])
                    : Channel(pixels[i + c] * factor[c]);
            }
        }
    }

    /// <summary>
    /// Draws <paramref name="source"/> into <paramref name="page"/> at (<paramref name="x"/>,
    /// <paramref name="y"/>), then copies its edge texels <paramref name="extrude"/> texels outwards. The
    /// extrusion is not optional decoration. A page is sampled with bilinear filtering and read at every mip
    /// level, and both reach past a cell's edge. Without a border of its own colour to reach into, a cell
    /// picks up its neighbour, which shows up as a fringe of the wrong texture along every seam and gets worse
    /// the further away the object is.
    /// </summary>
    // F2: four positional parameters and one named.
    public static void Blit(Rgba page, Rgba source, int x, int y, int extrude = 0)
    {
        for (var row = 0; row < source.Height; row++)
        {
            var to = (((y + row) * page.Width) + x) * 4;
            var from = row * source.Width * 4;
            if (to < 0 || to + (source.Width * 4) > page.Pixels.Length)
            {
                continue;
            }

            Array.Copy(source.Pixels, from, page.Pixels, to, source.Width * 4);
        }

        for (var e = 1; e <= extrude; e++)
        {
            Extrude(page, source, (x, y), e);
        }
    }

    /// <summary>A page filled with the value that makes <paramref name="role"/> do nothing.</summary>
    public static Rgba BlankPage(TextureRole role, int width, int height) =>
        Rgba.Filled(width, height, role.Blank());

    // One ring of the skirt, e texels out from the cell.
    private static void Extrude(Rgba page, Rgba source, (int X, int Y) at, int e)
    {
        var (x, y) = at;
        // Left and right of every row.
        for (var row = 0; row < source.Height; row++)
        {
            var py = y + row;
            CopyTexel(page, (x, py), (x - e, py));
            CopyTexel(page, (x + source.Width - 1, py), (x + source.Width - 1 + e, py));
        }

        // Above and below every column, corners included, so the diagonal neighbours a bilinear tap can
        // reach are filled too.
        for (var column = -e; column < source.Width + e; column++)
        {
            var px = x + column;
            var clamped = x + Math.Clamp(column, 0, source.Width - 1);
            CopyTexel(page, (clamped, y), (px, y - e));
            CopyTexel(page, (clamped, y + source.Height - 1), (px, y + source.Height - 1 + e));
        }
    }

    private static void CopyTexel(Rgba page, (int X, int Y) from, (int X, int Y) to)
    {
        if (!Inside(page, to) || !Inside(page, from))
        {
            return;
        }

        Array.Copy(page.Pixels, ((from.Y * page.Width) + from.X) * 4, page.Pixels, ((to.Y * page.Width) + to.X) * 4, 4);
    }

    private static bool Inside(Rgba page, (int X, int Y) at) =>
        at.X >= 0 && at.Y >= 0 && at.X < page.Width && at.Y < page.Height;

    private static float[] BuildToLinear()
    {
        var table = new float[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255.0;
            table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
        }

        return table;
    }

    private static byte ToSrgb(double linear)
    {
        var c = linear <= 0.0
            ? 0.0
            : linear <= 0.0031308
                ? linear * 12.92
                : (1.055 * Math.Pow(linear, 1 / 2.4)) - 0.055;
        return (byte)(int)Doubles.Clamp((c * 255.0) + 0.5, 0.0, 255.0);
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Doubles.Round(value), 0, 255);

    // The texels of the source one destination texel covers.
    private readonly struct Area(Rgba source, double xScale, double yScale)
    {
        public (byte R, byte G, byte B, byte A) Average(int x, int y)
        {
            var y0 = (int)Math.Floor(y * yScale);
            var y1 = Math.Clamp(Math.Max(y0 + 1, (int)Math.Ceiling((y + 1) * yScale)), 0, source.Height);
            var x0 = (int)Math.Floor(x * xScale);
            var x1 = Math.Clamp(Math.Max(x0 + 1, (int)Math.Ceiling((x + 1) * xScale)), 0, source.Width);

            double r = 0, g = 0, b = 0, a = 0;
            var n = 0;
            for (var sy = y0; sy < y1; sy++)
            {
                var at = ((sy * source.Width) + x0) * 4;
                for (var sx = x0; sx < x1; sx++)
                {
                    // Premultiplied, so a transparent texel's colour does not bleed into the average and
                    // leave a dark halo where the edge was.
                    var alpha = source.Pixels[at + 3] / 255.0;
                    r += source.Pixels[at] * alpha;
                    g += source.Pixels[at + 1] * alpha;
                    b += source.Pixels[at + 2] * alpha;
                    a += source.Pixels[at + 3];
                    at += 4;
                    n++;
                }
            }

            if (n == 0)
            {
                n = 1;
            }

            var mean = a / n;
            var back = mean <= 0.0 ? 0.0 : 255.0 / mean;
            return (Channel(r / n * back), Channel(g / n * back), Channel(b / n * back), Channel(mean));
        }
    }
}
