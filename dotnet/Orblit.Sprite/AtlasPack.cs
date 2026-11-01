namespace Orblit.Sprite;

/// <summary>
/// One picture going into a pack. Straight (non-premultiplied) alpha, RGBA8, row nought at the
/// top, the shape a PNG decodes to and the shape <see cref="Atlas"/> already assumes. The packer
/// never blends a pixel against another, only copies and rotates whole texels, so it packs a page
/// identically whichever convention the source used.
/// </summary>
public sealed class AtlasSprite
{
    public AtlasSprite(string name, long width, long height, byte[] pixels)
    {
        // Two negative sides multiply to a plausible byte count, which a length check alone would let through.
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException(
                $"pixels must hold {width} x {height} RGBA8 texels ({width * height * 4} bytes), got {pixels.Length}",
                nameof(pixels));
        }

        Name = name;
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public string Name { get; }

    public long Width { get; }

    public long Height { get; }

    public byte[] Pixels { get; }
}

/// <summary>
/// Which corner of "A Thousand Ways to Pack the Bin" a placement is scored by. All five are the
/// ones that paper found worth keeping.
/// </summary>
public enum MaxRectsHeuristic
{
    /// <summary>
    /// Fits into the free rectangle that leaves the smallest gap on its tighter side. The one
    /// that wins most often on a mixed set of sprites.
    /// </summary>
    BestShortSideFit,

    /// <summary>
    /// Fits into the free rectangle that leaves the smallest gap on its longer side, so what
    /// remains free stays as squarish as possible.
    /// </summary>
    BestLongSideFit,

    /// <summary>Fits into the free rectangle whose leftover area, after the sprite is cut out of it, is smallest.</summary>
    BestAreaFit,

    /// <summary>Sits as low and then as far left as it can, the rule a person packing boxes by hand already uses.</summary>
    BottomLeft,

    /// <summary>
    /// Touches as much of the page's edge and of what is already placed as it can, so the packed
    /// sprites stay contiguous instead of leaving slivers between them that nothing later
    /// happens to fit.
    /// </summary>
    ContactPoint,
}

/// <summary>
/// How <see cref="AtlasPacker.PackAtlas"/> should lay sprites out. The package does not depend on
/// the renderer, so it cannot read a device profile itself: pass <see cref="MaxPageSize"/> as the
/// texture size budget, or the raw device maximum, from wherever the profile was already asked for.
/// </summary>
public sealed class AtlasPackOptions
{
    // F2: twelve parameters, every one optional and called by name.
    public AtlasPackOptions(
        long maxPageSize = 2048,
        long minPageSize = 64,
        long padding = 2,
        long border = 0,
        bool allowRotation = false,
        bool trim = true,
        long trimAlphaThreshold = 0,
        long extrude = 1,
        bool powerOfTwo = true,
        bool square = false,
        bool mergeDuplicates = true,
        MaxRectsHeuristic? heuristic = null)
    {
        Positive(maxPageSize, nameof(maxPageSize), "a page has to have some size");
        Positive(minPageSize, nameof(minPageSize), "a page has to have some size");
        NotNegative(padding, nameof(padding), "padding cannot be negative");
        NotNegative(border, nameof(border), "a border cannot be negative");
        NotNegative(extrude, nameof(extrude), "extrusion cannot be negative");

        MaxPageSize = maxPageSize;
        MinPageSize = minPageSize;
        Padding = padding;
        Border = border;
        AllowRotation = allowRotation;
        Trim = trim;
        TrimAlphaThreshold = trimAlphaThreshold;
        Extrude = extrude;
        PowerOfTwo = powerOfTwo;
        Square = square;
        MergeDuplicates = mergeDuplicates;
        Heuristic = heuristic;
    }

    /// <summary>The largest a page's width or height may be, in texels.</summary>
    public long MaxPageSize { get; }

    /// <summary>The smallest a shrunk page may come down to. Only the last page of a pack is ever shrunk.</summary>
    public long MinPageSize { get; }

    /// <summary>
    /// Empty texels kept between neighbouring sprites, so bilinear filtering at a sprite's edge
    /// samples its own extruded border rather than its neighbour's pixels.
    /// </summary>
    public long Padding { get; }

    /// <summary>Empty texels kept between every sprite and the edge of the page.</summary>
    public long Border { get; }

    /// <summary>
    /// Whether a sprite may be turned a quarter turn to fit better. Off by default, because
    /// nothing draws a rotated region yet: <see cref="Region.Uv"/> is axis-aligned, so a sprite
    /// packed on its side would be drawn on its side.
    /// </summary>
    public bool AllowRotation { get; }

    /// <summary>
    /// Whether transparent borders are cut before packing. Cutting them is almost always a win,
    /// but it costs a scan of every pixel, which a caller packing pictures it already knows have
    /// no transparent margin can skip.
    /// </summary>
    public bool Trim { get; }

    /// <summary>
    /// A texel's alpha has to be greater than this to count as content while trimming. Nought
    /// trims only fully transparent texels.
    /// </summary>
    public long TrimAlphaThreshold { get; }

    /// <summary>
    /// How far a sprite's edge texels are copied out into its padding, so a mipmap or a filtered
    /// sample at the very edge reads more of the same picture. Clamped to the padding when
    /// packing runs, except that a padding of nought leaves it as given.
    /// </summary>
    public long Extrude { get; }

    /// <summary>Whether a page's size, before any shrinking, is rounded up to a power of two.</summary>
    public bool PowerOfTwo { get; }

    /// <summary>Whether a page must come out width equal to height.</summary>
    public bool Square { get; }

    /// <summary>
    /// Whether sprites with byte-identical trimmed pixels are packed once and given the same
    /// rectangle.
    /// </summary>
    public bool MergeDuplicates { get; }

    /// <summary>
    /// Which heuristic chooses a sprite's spot. Left null, every heuristic is tried and the one
    /// that packed the input into fewer pages wins, or, tied on pages, the smaller total page
    /// area. Ties after that keep the first in declared order.
    /// </summary>
    public MaxRectsHeuristic? Heuristic { get; }

    private static void Positive(long value, string name, string reason)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, reason);
        }
    }

    private static void NotNegative(long value, string name, string reason)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, reason);
        }
    }
}

/// <summary>
/// A sprite <see cref="AtlasPacker.PackAtlas"/> would have had to place off the edge of even an
/// empty page, reported rather than thrown so the rest of a pack still happens.
/// </summary>
public sealed record AtlasPackProblem(string Name, string Reason)
{
    public override string ToString() => $"{Name}: {Reason}";
}

/// <summary>
/// One page a pack produced: the pixels of a page-sized picture, and where every sprite placed on
/// it ended up.
/// </summary>
public sealed class AtlasPackPage(long width, long height, byte[] pixels, IReadOnlyDictionary<string, Region> regions)
{
    public long Width { get; } = width;

    public long Height { get; } = height;

    /// <summary>RGBA8, straight alpha, row nought at the top.</summary>
    public byte[] Pixels { get; } = pixels;

    public IReadOnlyDictionary<string, Region> Regions { get; } = regions;

    /// <summary>The covered fraction of the page: sprites only, with padding and the border ignored.</summary>
    public double FillRatio
    {
        get
        {
            if (Width <= 0 || Height <= 0)
            {
                return 0;
            }

            long covered = 0;
            foreach (var region in Regions.Values)
            {
                covered += region.Width * region.Height;
            }

            return (double)covered / (Width * Height);
        }
    }

    /// <summary>This page as the existing <see cref="Atlas"/> type, under <paramref name="image"/>.</summary>
    public Atlas ToAtlas(string image) => new(image, Regions, Width, Height);
}

/// <summary>What a pack produced: every page, and every sprite it could not place at all.</summary>
public sealed class AtlasPackResult(
    IReadOnlyList<AtlasPackPage> pages,
    IReadOnlyList<AtlasPackProblem> problems,
    MaxRectsHeuristic heuristic)
{
    public IReadOnlyList<AtlasPackPage> Pages { get; } = pages;

    /// <summary>
    /// Sprites too big for a page even alone on it. Everything else asked for is somewhere in the
    /// pages, including a sprite that packed to nothing because it was empty or fully
    /// transparent: it still has a region, sized zero, so looking it up by name never returns null
    /// just because there was nothing to draw.
    /// </summary>
    public IReadOnlyList<AtlasPackProblem> Problems { get; } = problems;

    /// <summary>Which heuristic produced this result: the one asked for, or whichever was chosen.</summary>
    public MaxRectsHeuristic Heuristic { get; } = heuristic;

    /// <summary>
    /// Every page as the existing <see cref="Atlas"/> type, named by <paramref name="imageName"/>
    /// (by default <c>page_0.png</c>, <c>page_1.png</c> and so on) and gathered into the smallest
    /// type that can look a region up without knowing which page it landed on.
    /// </summary>
    public AtlasSet ToAtlasSet(Func<int, string>? imageName = null)
    {
        var name = imageName ?? (index => $"page_{index}.png");
        return new AtlasSet([.. Pages.Select((page, index) => page.ToAtlas(name(index)))]);
    }
}
