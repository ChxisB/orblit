namespace Orblit.Sprite;

/// <summary>A rectangle of texels, or of free page.</summary>
internal readonly record struct Box(long X, long Y, long W, long H);

/// <summary>
/// Whole-texel copies, rotations and trims over RGBA8 buffers. Nothing here blends, so what goes
/// in comes out byte for byte.
/// </summary>
internal static class Texels
{
    /// <summary>The smallest box holding every texel whose alpha is above <paramref name="threshold"/>, or an empty one.</summary>
    public static Box TrimBox(byte[] pixels, long width, long height, long threshold)
    {
        var (minX, minY, maxX, maxY) = (width, height, -1L, -1L);
        for (long y = 0; y < height; y++)
        {
            for (long x = 0; x < width; x++)
            {
                if (pixels[(((y * width) + x) * 4) + 3] > threshold)
                {
                    (minX, maxX) = (Math.Min(minX, x), Math.Max(maxX, x));
                    (minY, maxY) = (Math.Min(minY, y), Math.Max(maxY, y));
                }
            }
        }

        return maxX < minX || maxY < minY ? new Box(0, 0, 0, 0) : new Box(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static byte[] Copy(byte[] source, long sourceWidth, Box box)
    {
        var output = new byte[box.W * box.H * 4];
        for (long row = 0; row < box.H; row++)
        {
            var sourceStart = ((((box.Y + row) * sourceWidth) + box.X) * 4);
            Array.Copy(source, sourceStart, output, row * box.W * 4, box.W * 4);
        }

        return output;
    }

    public static void Paste(byte[] destination, long destinationWidth, Box box, byte[] block)
    {
        for (long row = 0; row < box.H; row++)
        {
            var destinationStart = ((((box.Y + row) * destinationWidth) + box.X) * 4);
            Array.Copy(block, row * box.W * 4, destination, destinationStart, box.W * 4);
        }
    }

    /// <summary>
    /// A quarter turn clockwise: <c>destination(height - 1 - y, x) = source(x, y)</c>, in a buffer
    /// <paramref name="height"/> wide and <paramref name="width"/> tall, since a quarter turn swaps them.
    /// </summary>
    public static byte[] RotateCw(byte[] source, long width, long height)
    {
        var output = new byte[width * height * 4];
        for (long y = 0; y < height; y++)
        {
            for (long x = 0; x < width; x++)
            {
                CopyTexel(source, ((y * width) + x) * 4, output, (((x * height) + (height - 1 - y)) * 4));
            }
        }

        return output;
    }

    /// <summary>
    /// The exact inverse of <see cref="RotateCw"/>. Every destination texel runs that mapping
    /// backwards, rather than re-deriving a second formula from scratch, which is exactly the
    /// kind of one-off algebra a rotation bug hides in.
    /// </summary>
    public static byte[] RotateCcw(byte[] source, long width, long height)
    {
        var (outputWidth, outputHeight) = (height, width);
        var output = new byte[outputWidth * outputHeight * 4];
        for (long y = 0; y < outputHeight; y++)
        {
            for (long x = 0; x < outputWidth; x++)
            {
                CopyTexel(source, ((x * width) + (width - 1 - y)) * 4, output, ((y * outputWidth) + x) * 4);
            }
        }

        return output;
    }

    public static void CopyTexel(byte[] source, long sourceAt, byte[] destination, long destinationAt)
    {
        destination[destinationAt] = source[sourceAt];
        destination[destinationAt + 1] = source[sourceAt + 1];
        destination[destinationAt + 2] = source[sourceAt + 2];
        destination[destinationAt + 3] = source[sourceAt + 3];
    }
}

/// <summary>A page-sized picture being drawn into.</summary>
internal sealed class PagePixels(long width, long height)
{
    public long Width { get; } = width;

    public long Height { get; } = height;

    public byte[] Pixels { get; } = new byte[width * height * 4];

    public void Paste(Box box, byte[] block) => Texels.Paste(Pixels, Width, box, block);

    /// <summary>
    /// Copies the edge texels of <paramref name="box"/> out <paramref name="amount"/> texels on
    /// every side, so a filtered sample at the very edge reads more of the same picture rather
    /// than a seam of its neighbour's colour.
    /// </summary>
    public void Extrude(Box box, long amount)
    {
        if (amount <= 0 || box.W <= 0 || box.H <= 0)
        {
            return;
        }

        ExtrudeSides(box, amount);
        ExtrudeEnds(box, amount);
    }

    private void ExtrudeSides(Box box, long amount)
    {
        for (long row = 0; row < box.H; row++)
        {
            var py = box.Y + row;
            if (py < 0 || py >= Height)
            {
                continue;
            }

            var leftSource = Index(box.X, py);
            for (long k = 1; k <= amount && box.X - k >= 0; k++)
            {
                Texels.CopyTexel(Pixels, leftSource, Pixels, Index(box.X - k, py));
            }

            var rightSource = Index(box.X + box.W - 1, py);
            for (long k = 1; k <= amount && box.X + box.W - 1 + k < Width; k++)
            {
                Texels.CopyTexel(Pixels, rightSource, Pixels, Index(box.X + box.W - 1 + k, py));
            }
        }
    }

    private void ExtrudeEnds(Box box, long amount)
    {
        for (var col = -amount; col < box.W + amount; col++)
        {
            var px = box.X + col;
            if (px < 0 || px >= Width)
            {
                continue;
            }

            var sourceColumn = Math.Clamp(col, 0, box.W - 1);
            var topSource = Index(box.X + sourceColumn, box.Y);
            for (long k = 1; k <= amount && box.Y - k >= 0; k++)
            {
                Texels.CopyTexel(Pixels, topSource, Pixels, Index(px, box.Y - k));
            }

            var bottomSource = Index(box.X + sourceColumn, box.Y + box.H - 1);
            for (long k = 1; k <= amount && box.Y + box.H - 1 + k < Height; k++)
            {
                Texels.CopyTexel(Pixels, bottomSource, Pixels, Index(px, box.Y + box.H - 1 + k));
            }
        }
    }

    private long Index(long px, long py) => ((py * Width) + px) * 4;
}
