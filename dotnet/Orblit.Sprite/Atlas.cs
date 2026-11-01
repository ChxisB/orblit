using Orblit.Json;

namespace Orblit.Sprite;

/// <summary>
/// Many images packed into one, and where each of them is. The reason to bother is that a
/// draw call can only bind one texture, so a hundred sprites in one image is one draw call.
/// </summary>
public sealed class Atlas(string image, IReadOnlyDictionary<string, Region> regions, long width = 0, long height = 0)
{
    /// <summary>The file the regions are cut from.</summary>
    public string Image { get; } = image;

    /// <summary>Every region, by name.</summary>
    public IReadOnlyDictionary<string, Region> Regions { get; } = regions;

    /// <summary>The packed image's width in pixels, when the file said.</summary>
    public long Width { get; } = width;

    /// <summary>The packed image's height in pixels, when the file said.</summary>
    public long Height { get; } = height;

    public Region? this[string name] => Regions.GetValueOrDefault(name);

    public int Length => Regions.Count;

    /// <summary>
    /// Every region whose name begins with <paramref name="prefix"/>, in name order. How a sheet
    /// becomes an animation: packers name frames <c>run_00</c>, <c>run_01</c> and the sort puts
    /// them back in order.
    /// </summary>
    // Names that compare equal keep the file's order.
    public IReadOnlyList<Region> Sequence(string prefix) =>
        [.. Regions.Values.Where(region => region.Name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(region => region.Name, Comparer<string>.Create(Naturally))];

    /// <summary>
    /// A whole image cut into equal cells, named <c>name_0</c> and up in reading order. The
    /// other kind of sheet, with no metadata file at all.
    /// </summary>
    // F2: nine parameters, and every one after the cell size is optional and called by name.
    public static Atlas Grid(
        string image,
        long imageWidth,
        long imageHeight,
        long cellWidth,
        long cellHeight,
        string name = "frame",
        int count = 0,
        long spacing = 0,
        long margin = 0)
    {
        var regions = new OrderedDictionary<string, Region>();
        if (cellWidth <= 0 || cellHeight <= 0)
        {
            return new Atlas(image, regions, imageWidth, imageHeight);
        }

        var across = (imageWidth - (margin * 2) + spacing) / (cellWidth + spacing);
        var down = (imageHeight - (margin * 2) + spacing) / (cellHeight + spacing);
        var made = 0;
        for (var row = 0; row < down; row++)
        {
            for (var column = 0; column < across; column++)
            {
                if (count > 0 && made >= count)
                {
                    break;
                }

                var cell = $"{name}_{made}";
                regions[cell] = new Region(
                    cell,
                    margin + (column * (cellWidth + spacing)),
                    margin + (row * (cellHeight + spacing)),
                    cellWidth,
                    cellHeight);
                made++;
            }
        }

        return new Atlas(image, regions, imageWidth, imageHeight);
    }

    /// <summary>
    /// Reads what TexturePacker, Aseprite and the rest write. <c>frames</c> may be an object keyed
    /// by name or an array with the name inside each entry. Null rather than an exception for a
    /// file that is not one of these, because refusing the whole level over one malformed atlas
    /// is worse than drawing it without.
    /// </summary>
    public static Atlas? Read(string text, string? image = null)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (FormatException)
        {
            return null;
        }

        if (parsed is not IReadOnlyDictionary<string, object?> root || ReadFrames(root.GetValueOrDefault("frames")) is not { } regions)
        {
            return null;
        }

        var meta = JsonValues.Box(root.GetValueOrDefault("meta"));
        var size = JsonValues.Box(meta.GetValueOrDefault("size"));
        return new Atlas(
            image ?? (meta.GetValueOrDefault("image") is string named ? named : ""),
            regions,
            JsonValues.Int(size.GetValueOrDefault("w")),
            JsonValues.Int(size.GetValueOrDefault("h")));
    }

    private static OrderedDictionary<string, Region>? ReadFrames(object? frames)
    {
        var regions = new OrderedDictionary<string, Region>();
        switch (frames)
        {
            case IReadOnlyDictionary<string, object?> byName:
                foreach (var (key, value) in byName)
                {
                    Take(regions, key, value);
                }

                return regions;
            case IReadOnlyList<object?> entries:
                foreach (var entry in entries.OfType<IReadOnlyDictionary<string, object?>>())
                {
                    Take(regions, entry.GetValueOrDefault("filename") is string filename ? filename : $"{regions.Count}", entry);
                }

                return regions;
            default:
                return null;
        }
    }

    private static void Take(OrderedDictionary<string, Region> regions, string name, object? entry)
    {
        if (entry is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("frame") is not IReadOnlyDictionary<string, object?> box)
        {
            return;
        }

        var source = JsonValues.Box(map.GetValueOrDefault("spriteSourceSize"));
        var size = JsonValues.Box(map.GetValueOrDefault("sourceSize"));
        regions[name] = new Region(
            name,
            JsonValues.Int(box.GetValueOrDefault("x")),
            JsonValues.Int(box.GetValueOrDefault("y")),
            JsonValues.Int(box.GetValueOrDefault("w")),
            JsonValues.Int(box.GetValueOrDefault("h")),
            Rotated: map.GetValueOrDefault("rotated") is true,
            Trimmed: map.GetValueOrDefault("trimmed") is true,
            OffsetX: JsonValues.Int(source.GetValueOrDefault("x")),
            OffsetY: JsonValues.Int(source.GetValueOrDefault("y")),
            SourceWidth: JsonValues.Int(size.GetValueOrDefault("w")),
            SourceHeight: JsonValues.Int(size.GetValueOrDefault("h")));
    }

    /// <summary>
    /// Compares names so that <c>run_2</c> comes before <c>run_10</c>. Plain string order
    /// reverses the middle of every animation with more than nine frames.
    /// </summary>
    internal static int Naturally(string a, string b)
    {
        var i = 0;
        var j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                var x = DigitRun(a, ref i);
                var y = DigitRun(b, ref j);
                if (x != y)
                {
                    return x.CompareTo(y);
                }

                continue;
            }

            if (a[i] != b[j])
            {
                return a[i].CompareTo(b[j]);
            }

            i++;
            j++;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    // An integer wraps at 64 bits, so a run of digits too long for one wraps here.
    private static long DigitRun(string text, ref int at)
    {
        long value = 0;
        while (at < text.Length && char.IsAsciiDigit(text[at]))
        {
            value = unchecked((value * 10) + (text[at] - '0'));
            at++;
        }

        return value;
    }
}

/// <summary>
/// Several <see cref="Atlas"/> pages a packer split one job across, with one lookup that does not
/// care which page a name landed on.
/// </summary>
public sealed class AtlasSet(IReadOnlyList<Atlas> pages)
{
    /// <summary>Every page, in the order a packer produced them.</summary>
    public IReadOnlyList<Atlas> Pages { get; } = pages;

    /// <summary>The region named <paramref name="name"/>, from whichever page has it, or null when none does.</summary>
    public Region? Find(string name) => Locate(name)?.Region;

    /// <summary>
    /// The page and region named <paramref name="name"/>, or null when none does. What drawing a
    /// sprite needs and <see cref="Find"/> alone cannot give it: a rectangle means nothing
    /// without the page it is a rectangle of.
    /// </summary>
    public (Atlas Atlas, Region Region)? Locate(string name)
    {
        foreach (var page in Pages)
        {
            if (page[name] is { } region)
            {
                return (page, region);
            }
        }

        return null;
    }

    /// <summary>How many regions there are, across every page.</summary>
    public int Length => Pages.Sum(page => page.Length);
}
