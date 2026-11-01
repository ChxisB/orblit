using Orblit.Json;

namespace Orblit.Sprite;

/// <summary>Packs loose pictures into atlas pages, and writes and reads them back.</summary>
public static class AtlasPacker
{
    /// <summary>
    /// Packs <paramref name="sprites"/> into as few pages as <paramref name="options"/> allow.
    /// Pure and synchronous: it does no I/O and returns as soon as the CPU work is done.
    /// Deterministic for the same sprites and options regardless of call order or platform:
    /// sprites are placed in an order derived only from their own size and name, never from a
    /// dictionary's iteration, so two runs produce the same pages down to the byte.
    /// </summary>
    public static AtlasPackResult PackAtlas(IReadOnlyList<AtlasSprite> sprites, AtlasPackOptions? options = null)
    {
        options ??= new AtlasPackOptions();
        var emptyRegions = new OrderedDictionary<string, Region>();
        var prepared = PrepareAll(sprites, options, emptyRegions);

        var pieces = options.MergeDuplicates
            ? GroupDuplicates(prepared)
            : [.. prepared.Select(sprite => new Piece(sprite.Width, sprite.Height, sprite.Pixels, [sprite]))];

        var problems = new List<AtlasPackProblem>();
        // Equal keys only arise from two sprites sharing a name, which is not defined.
        var packable = Packable(pieces, options, problems)
            .OrderByDescending(piece => Math.Max(piece.Width, piece.Height))
            .ThenByDescending(piece => Math.Min(piece.Width, piece.Height))
            .ThenBy(piece => piece.Key, StringComparer.Ordinal)
            .ToList();

        var (bestRun, bestHeuristic) = BestRun(packable, options);
        var pieceByKey = new Dictionary<string, Piece>();
        foreach (var piece in packable)
        {
            pieceByKey[piece.Key] = piece;
        }

        var pages = Render(bestRun, options, pieceByKey, emptyRegions);
        return new AtlasPackResult(pages, problems, bestHeuristic);
    }

    /// <summary>
    /// Packs on a worker thread, so a folder of sprites large enough to take real time never
    /// stalls the thread that asked for it. The sprites are shared with the worker and not copied,
    /// so they are not to be changed until the task is done.
    /// </summary>
    public static Task<AtlasPackResult> PackAtlasInBackground(
        IReadOnlyList<AtlasSprite> sprites,
        AtlasPackOptions? options = null) => Task.Run(() => PackAtlas(sprites, options));

    /// <summary>
    /// <paramref name="atlas"/> written as the same TexturePacker-shaped JSON <see cref="Atlas.Read"/>
    /// already parses, every field <see cref="Region"/> has, verbatim, so reading it back gives an
    /// atlas equal to <paramref name="atlas"/> field for field. Frames are written in name order
    /// rather than insertion order, so packing the same sprites twice writes the same bytes and a
    /// diff of a cooked atlas shows only what actually changed.
    /// </summary>
    public static string WriteAtlas(Atlas atlas)
    {
        var frames = new OrderedDictionary<string, object?>();
        foreach (var name in atlas.Regions.Keys.Order(StringComparer.Ordinal))
        {
            frames[name] = RegionJson(atlas.Regions[name]);
        }

        var json = new OrderedDictionary<string, object?>
        {
            ["frames"] = frames,
            ["meta"] = new OrderedDictionary<string, object?>
            {
                ["image"] = atlas.Image,
                ["size"] = new OrderedDictionary<string, object?> { ["w"] = atlas.Width, ["h"] = atlas.Height },
            },
        };
        return JsonText.Encode(json, "  ") + "\n";
    }

    /// <summary>
    /// The pixels <paramref name="region"/> names, read back out of the page it was packed onto and
    /// returned exactly as the sprite was given to <see cref="PackAtlas"/>: un-rotated, and, when
    /// the region was trimmed, padded back out to its source size with the trimmed pixels at its
    /// offset, everywhere else transparent. What a rotation or a trim actually did is only provable
    /// by reading pixels back through the region that describes them, which is what this is for.
    /// </summary>
    public static byte[] ExtractRegionPixels(long pageWidth, long pageHeight, byte[] pagePixels, Region region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            return new byte[Math.Max(region.SourceWidth, 0) * Math.Max(region.SourceHeight, 0) * 4];
        }

        var (physWidth, physHeight) = region.Rotated ? (region.Height, region.Width) : (region.Width, region.Height);
        var block = Texels.Copy(pagePixels, pageWidth, new Box(region.X, region.Y, physWidth, physHeight));
        var trimmedBlock = region.Rotated ? Texels.RotateCcw(block, physWidth, physHeight) : block;

        if (!region.Trimmed || region.SourceWidth <= 0 || region.SourceHeight <= 0)
        {
            return trimmedBlock;
        }

        var canvas = new byte[region.SourceWidth * region.SourceHeight * 4];
        Texels.Paste(canvas, region.SourceWidth, new Box(region.OffsetX, region.OffsetY, region.Width, region.Height), trimmedBlock);
        return canvas;
    }

    private static OrderedDictionary<string, object?> RegionJson(Region region) => new()
    {
        ["frame"] = new OrderedDictionary<string, object?>
        {
            ["x"] = region.X,
            ["y"] = region.Y,
            ["w"] = region.Width,
            ["h"] = region.Height,
        },
        ["rotated"] = region.Rotated,
        ["trimmed"] = region.Trimmed,
        ["spriteSourceSize"] = new OrderedDictionary<string, object?> { ["x"] = region.OffsetX, ["y"] = region.OffsetY },
        ["sourceSize"] = new OrderedDictionary<string, object?> { ["w"] = region.SourceWidth, ["h"] = region.SourceHeight },
    };

    // Sprites with nothing to place are left out and given a zero-size region, so a game laying
    // things out by name still gets one back.
    private static List<Prepared> PrepareAll(
        IReadOnlyList<AtlasSprite> sprites,
        AtlasPackOptions options,
        OrderedDictionary<string, Region> emptyRegions)
    {
        var prepared = new List<Prepared>();
        foreach (var sprite in sprites)
        {
            if (sprite.Width <= 0 || sprite.Height <= 0)
            {
                emptyRegions[sprite.Name] = new Region(sprite.Name, 0, 0, 0, 0);
                continue;
            }

            var box = options.Trim
                ? Texels.TrimBox(sprite.Pixels, sprite.Width, sprite.Height, options.TrimAlphaThreshold)
                : new Box(0, 0, sprite.Width, sprite.Height);

            if (box.W <= 0 || box.H <= 0)
            {
                // Fully transparent: nothing to place, but the sprite still had a size.
                emptyRegions[sprite.Name] = new Region(
                    sprite.Name, 0, 0, 0, 0, Trimmed: true, SourceWidth: sprite.Width, SourceHeight: sprite.Height);
                continue;
            }

            prepared.Add(Prepare(sprite, box));
        }

        return prepared;
    }

    private static Prepared Prepare(AtlasSprite sprite, Box box)
    {
        var trimmed = box.W != sprite.Width || box.H != sprite.Height;
        return new Prepared
        {
            Name = sprite.Name,
            TrimmedX = box.X,
            TrimmedY = box.Y,
            Width = box.W,
            Height = box.H,
            SourceWidth = trimmed ? sprite.Width : 0,
            SourceHeight = trimmed ? sprite.Height : 0,
            Trimmed = trimmed,
            Pixels = Texels.Copy(sprite.Pixels, sprite.Width, box),
        };
    }

    private static List<Piece> GroupDuplicates(List<Prepared> prepared)
    {
        var buckets = new OrderedDictionary<long, List<Piece>>();
        foreach (var candidate in prepared)
        {
            var hash = ContentHash(candidate.Pixels, candidate.Width, candidate.Height);
            if (!buckets.TryGetValue(hash, out var bucket))
            {
                bucket = [];
                buckets.Add(hash, bucket);
            }

            var match = bucket.Find(piece => piece.Width == candidate.Width
                && piece.Height == candidate.Height
                && piece.Pixels.AsSpan().SequenceEqual(candidate.Pixels));
            if (match is not null)
            {
                match.Add(candidate);
            }
            else
            {
                bucket.Add(new Piece(candidate.Width, candidate.Height, candidate.Pixels, [candidate]));
            }
        }

        return [.. buckets.Values.SelectMany(bucket => bucket)];
    }

    // FNV-1a. Only ever used as a bucket key, since every match is confirmed with a full byte
    // comparison, so a collision costs a few extra comparisons and nothing else.
    private static long ContentHash(byte[] bytes, long width, long height)
    {
        const long prime = 0x01000193;
        const long mask = 0xffffffff;
        var hash = 0x811c9dc5L;
        hash = ((hash ^ width) & mask) * prime & mask;
        hash = ((hash ^ height) & mask) * prime & mask;
        foreach (var value in bytes)
        {
            hash = ((hash ^ value) & mask) * prime & mask;
        }

        return hash;
    }

    private static bool FitsFootprint(long w, long h, long padding, long available) =>
        w + padding <= available && h + padding <= available;

    // The pieces that fit a page, turned if turning is allowed. The rest are reported, each
    // member by name.
    private static List<Piece> Packable(List<Piece> pieces, AtlasPackOptions options, List<AtlasPackProblem> problems)
    {
        var available = options.MaxPageSize - (2 * options.Border);
        var packable = new List<Piece>();
        foreach (var piece in pieces)
        {
            var fits = FitsFootprint(piece.Width, piece.Height, options.Padding, available)
                || (options.AllowRotation && FitsFootprint(piece.Height, piece.Width, options.Padding, available));
            if (fits)
            {
                packable.Add(piece);
                continue;
            }

            problems.AddRange(piece.Members.Select(member => new AtlasPackProblem(
                member.Name,
                $"is {piece.Width}x{piece.Height}, too large for a {options.MaxPageSize}x{options.MaxPageSize} page even rotated")));
        }

        return packable;
    }

    private static (PackRun? Run, MaxRectsHeuristic Heuristic) BestRun(List<Piece> packable, AtlasPackOptions options)
    {
        MaxRectsHeuristic[] heuristics = options.Heuristic is { } pinned ? [pinned] : Enum.GetValues<MaxRectsHeuristic>();
        PackRun? bestRun = null;
        var bestHeuristic = heuristics[0];
        foreach (var heuristic in heuristics)
        {
            var run = PackRun.Once(packable, options, heuristic);
            if (bestRun is null || run.IsBetterThan(bestRun))
            {
                bestRun = run;
                bestHeuristic = heuristic;
            }
        }

        return (bestRun, bestHeuristic);
    }

    private static List<AtlasPackPage> Render(
        PackRun? run,
        AtlasPackOptions options,
        Dictionary<string, Piece> pieceByKey,
        OrderedDictionary<string, Region> emptyRegions)
    {
        var builds = run?.Pages ?? [];
        if (builds.Count == 0)
        {
            return emptyRegions.Count == 0 ? [] : [EmptyPage(options, emptyRegions)];
        }

        var pages = new List<AtlasPackPage>();
        for (var i = 0; i < builds.Count; i++)
        {
            pages.Add(RenderPage(builds[i], i == builds.Count - 1, options, pieceByKey, emptyRegions));
        }

        return pages;
    }

    private static AtlasPackPage EmptyPage(AtlasPackOptions options, OrderedDictionary<string, Region> emptyRegions)
    {
        var size = Math.Min(options.MinPageSize, options.MaxPageSize);
        return new AtlasPackPage(size, size, new byte[size * size * 4], new OrderedDictionary<string, Region>(emptyRegions));
    }

    private static AtlasPackPage RenderPage(
        PageBuild build,
        bool isLast,
        AtlasPackOptions options,
        Dictionary<string, Piece> pieceByKey,
        OrderedDictionary<string, Region> emptyRegions)
    {
        var width = isLast ? ShrunkSize(build.MaxX + build.Border, options) : options.MaxPageSize;
        var height = isLast ? ShrunkSize(build.MaxY + build.Border, options) : options.MaxPageSize;
        var finalWidth = options.Square ? Math.Max(width, height) : width;
        var finalHeight = options.Square ? finalWidth : height;

        var canvas = new PagePixels(finalWidth, finalHeight);
        var regions = new OrderedDictionary<string, Region>();
        foreach (var (key, placement) in build.Placements)
        {
            Draw(canvas, placement, pieceByKey[key], options);
            AddRegions(regions, placement, pieceByKey[key]);
        }

        if (isLast)
        {
            foreach (var (name, region) in emptyRegions)
            {
                regions[name] = region;
            }
        }

        return new AtlasPackPage(finalWidth, finalHeight, canvas.Pixels, regions);
    }

    private static void Draw(PagePixels canvas, Placement placement, Piece piece, AtlasPackOptions options)
    {
        var block = placement.Rotated ? Texels.RotateCw(piece.Pixels, piece.Width, piece.Height) : piece.Pixels;
        var (physWidth, physHeight) = placement.Rotated ? (piece.Height, piece.Width) : (piece.Width, piece.Height);
        var box = new Box(placement.X, placement.Y, physWidth, physHeight);
        canvas.Paste(box, block);

        // Never further than the padding there is to extrude into. With no padding at all the
        // amount is left as given.
        canvas.Extrude(box, Math.Clamp(options.Extrude, 0, options.Padding == 0 ? options.Extrude : options.Padding));
    }

    private static void AddRegions(OrderedDictionary<string, Region> regions, Placement placement, Piece piece)
    {
        foreach (var member in piece.Members)
        {
            regions[member.Name] = new Region(
                member.Name,
                placement.X,
                placement.Y,
                piece.Width,
                piece.Height,
                placement.Rotated,
                member.Trimmed,
                member.TrimmedX,
                member.TrimmedY,
                member.SourceWidth,
                member.SourceHeight);
        }
    }

    // minPageSize is a floor, but a caller is free to set maxPageSize below the default floor for
    // a small pack, a 32-texel page for a handful of icons, say, and that has to win rather than
    // making clamp's own range check the one that reports the mistake.
    private static long ShrunkSize(long content, AtlasPackOptions options)
    {
        var floor = Math.Min(options.MinPageSize, options.MaxPageSize);
        var size = Math.Clamp(content, floor, options.MaxPageSize);
        return options.PowerOfTwo ? Math.Clamp(NextPowerOfTwo(size), floor, options.MaxPageSize) : size;
    }

    private static long NextPowerOfTwo(long value)
    {
        long size = 1;
        while (size < value)
        {
            size <<= 1;
        }

        return size;
    }
}
