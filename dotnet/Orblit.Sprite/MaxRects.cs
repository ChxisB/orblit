using System.Diagnostics;

namespace Orblit.Sprite;

/// <summary>Where a piece went on a page, and whether it was turned to get there.</summary>
internal readonly record struct Placement(long X, long Y, bool Rotated);

/// <summary>A free rectangle a piece could go in, turned or not, with how well it fits.</summary>
internal readonly record struct Candidate(Box Rect, bool Rotated, long Score1, long Score2)
{
    /// <summary>Lower is better, the first score then the second. A tie is not better, so the first seen wins.</summary>
    public bool Beats(Candidate other) => Score1 < other.Score1 || (Score1 == other.Score1 && Score2 < other.Score2);
}

/// <summary>A piece to pack: pixels shared by every sprite that came out identical.</summary>
internal sealed class Piece(long width, long height, byte[] pixels, IEnumerable<Prepared> members)
{
    private List<Prepared> _members = Sorted(members);

    public long Width { get; } = width;

    public long Height { get; } = height;

    public byte[] Pixels { get; } = pixels;

    public IReadOnlyList<Prepared> Members => _members;

    /// <summary>
    /// The smallest member name, so two packs of the same sprites choose the same representative
    /// regardless of what order duplicates arrived in.
    /// </summary>
    public string Key => _members[0].Name;

    public void Add(Prepared member) => _members = Sorted(_members.Append(member));

    // Stable, so a set of duplicates keeps its order.
    private static List<Prepared> Sorted(IEnumerable<Prepared> members) =>
        [.. members.OrderBy(member => member.Name, StringComparer.Ordinal)];
}

/// <summary>A sprite after trimming, ready to be grouped and packed.</summary>
internal sealed class Prepared
{
    public required string Name { get; init; }

    public required long TrimmedX { get; init; }

    public required long TrimmedY { get; init; }

    public required long Width { get; init; }

    public required long Height { get; init; }

    public required long SourceWidth { get; init; }

    public required long SourceHeight { get; init; }

    public required bool Trimmed { get; init; }

    public required byte[] Pixels { get; init; }
}

/// <summary>
/// One page being filled by the MaxRects method: a list of free rectangles, cut up each time
/// something is placed.
/// </summary>
internal sealed class PageBuild(long size, long border)
{
    private readonly List<Box> _free = [new Box(border, border, size - (2 * border), size - (2 * border))];
    private readonly List<Box> _placed = [];

    public long Size { get; } = size;

    public long Border { get; } = border;

    /// <summary>In the order pieces were placed, which is the order they are drawn.</summary>
    public OrderedDictionary<string, Placement> Placements { get; } = [];

    public long MaxX { get; private set; }

    public long MaxY { get; private set; }

    /// <summary>Places <paramref name="piece"/> in the best free rectangle, or returns false when none fits.</summary>
    public bool TryPlace(Piece piece, AtlasPackOptions options, MaxRectsHeuristic heuristic)
    {
        var footprintWidth = piece.Width + options.Padding;
        var footprintHeight = piece.Height + options.Padding;

        Candidate? best = null;
        foreach (var candidate in Candidates(footprintWidth, footprintHeight, options.AllowRotation, heuristic))
        {
            if (best is not { } current || candidate.Beats(current))
            {
                best = candidate;
            }
        }

        if (best is not { } chosen)
        {
            return false;
        }

        var rotated = chosen.Rotated;
        Place(new Box(chosen.Rect.X, chosen.Rect.Y, rotated ? footprintHeight : footprintWidth, rotated ? footprintWidth : footprintHeight));
        Placements[piece.Key] = new Placement(chosen.Rect.X, chosen.Rect.Y, rotated);
        return true;
    }

    // The footprint as it lies, then turned a quarter if turning is allowed.
    private static IEnumerable<(long W, long H, bool Rotated)> Orientations(
        long footprintWidth,
        long footprintHeight,
        bool allowRotation)
    {
        yield return (footprintWidth, footprintHeight, false);
        if (allowRotation)
        {
            yield return (footprintHeight, footprintWidth, true);
        }
    }

    private static long OverlapOneDimension(long aStart, long aEnd, long bStart, long bEnd) =>
        Math.Max(Math.Min(aEnd, bEnd) - Math.Max(aStart, bStart), 0);

    private static bool Intersects(Box a, Box b) =>
        a.X < b.X + b.W && a.X + a.W > b.X && a.Y < b.Y + b.H && a.Y + a.H > b.Y;

    private static bool Contains(Box outer, Box inner) =>
        inner.X >= outer.X
        && inner.Y >= outer.Y
        && inner.X + inner.W <= outer.X + outer.W
        && inner.Y + inner.H <= outer.Y + outer.H;

    // What is left of a free rectangle once a placed one is cut out of it: up to four slabs, which
    // overlap each other at the corners.
    private static IEnumerable<Box> Remainders(Box rect, Box placed)
    {
        if (placed.X > rect.X)
        {
            yield return new Box(rect.X, rect.Y, placed.X - rect.X, rect.H);
        }

        if (placed.X + placed.W < rect.X + rect.W)
        {
            yield return new Box(placed.X + placed.W, rect.Y, rect.X + rect.W - (placed.X + placed.W), rect.H);
        }

        if (placed.Y > rect.Y)
        {
            yield return new Box(rect.X, rect.Y, rect.W, placed.Y - rect.Y);
        }

        if (placed.Y + placed.H < rect.Y + rect.H)
        {
            yield return new Box(rect.X, placed.Y + placed.H, rect.W, rect.Y + rect.H - (placed.Y + placed.H));
        }
    }

    // Every free rectangle in turn, each turned and not turned, that the footprint fits in.
    private IEnumerable<Candidate> Candidates(
        long footprintWidth,
        long footprintHeight,
        bool allowRotation,
        MaxRectsHeuristic heuristic)
    {
        foreach (var rect in _free)
        {
            foreach (var (w, h, rotated) in Orientations(footprintWidth, footprintHeight, allowRotation))
            {
                if (w > rect.W || h > rect.H)
                {
                    continue;
                }

                var (score1, score2) = Score(heuristic, rect, w, h);
                yield return new Candidate(rect, rotated, score1, score2);
            }
        }
    }

    private (long, long) Score(MaxRectsHeuristic heuristic, Box rect, long w, long h)
    {
        var leftoverW = rect.W - w;
        var leftoverH = rect.H - h;
        var shortLeftover = Math.Min(leftoverW, leftoverH);
        var longLeftover = Math.Max(leftoverW, leftoverH);
        return heuristic switch
        {
            MaxRectsHeuristic.BestShortSideFit => (shortLeftover, longLeftover),
            MaxRectsHeuristic.BestLongSideFit => (longLeftover, shortLeftover),
            MaxRectsHeuristic.BestAreaFit => ((rect.W * rect.H) - (w * h), shortLeftover),
            MaxRectsHeuristic.BottomLeft => (rect.Y + h, rect.X),
            MaxRectsHeuristic.ContactPoint => (-ContactScore(new Box(rect.X, rect.Y, w, h)), 0),
            _ => throw new InvalidOperationException($"{heuristic} is not a heuristic"),
        };
    }

    private long ContactScore(Box box)
    {
        var contact = EdgeContact(box);
        foreach (var other in _placed)
        {
            if (other.X + other.W == box.X || box.X + box.W == other.X)
            {
                contact += OverlapOneDimension(other.Y, other.Y + other.H, box.Y, box.Y + box.H);
            }

            if (other.Y + other.H == box.Y || box.Y + box.H == other.Y)
            {
                contact += OverlapOneDimension(other.X, other.X + other.W, box.X, box.X + box.W);
            }
        }

        return contact;
    }

    private long EdgeContact(Box box)
    {
        long contact = 0;
        if (box.X == Border)
        {
            contact += box.H;
        }

        if (box.Y == Border)
        {
            contact += box.W;
        }

        if (box.X + box.W == Size - Border)
        {
            contact += box.H;
        }

        if (box.Y + box.H == Size - Border)
        {
            contact += box.W;
        }

        return contact;
    }

    private void Place(Box placed)
    {
        var next = new List<Box>();
        foreach (var rect in _free)
        {
            if (Intersects(rect, placed))
            {
                next.AddRange(Remainders(rect, placed));
            }
            else
            {
                next.Add(rect);
            }
        }

        _free.Clear();
        _free.AddRange(next.Where(rect => rect.W > 0 && rect.H > 0));
        Prune();
        _placed.Add(placed);
        MaxX = Math.Max(MaxX, placed.X + placed.W);
        MaxY = Math.Max(MaxY, placed.Y + placed.H);
    }

    // Drops every free rectangle that lies inside another. Of two equal ones, the later goes.
    private void Prune()
    {
        var keep = new List<Box>();
        for (var i = 0; i < _free.Count; i++)
        {
            if (!IsDominated(i))
            {
                keep.Add(_free[i]);
            }
        }

        _free.Clear();
        _free.AddRange(keep);
    }

    private bool IsDominated(int i)
    {
        for (var j = 0; j < _free.Count; j++)
        {
            if (i != j && Contains(_free[j], _free[i]) && (_free[i] != _free[j] || j < i))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Every page one heuristic built.</summary>
internal sealed class PackRun(IReadOnlyList<PageBuild> pages)
{
    public IReadOnlyList<PageBuild> Pages { get; } = pages;

    /// <summary>Fewer pages win, then less covered area. A tie is not better.</summary>
    public bool IsBetterThan(PackRun current)
    {
        if (Pages.Count != current.Pages.Count)
        {
            return Pages.Count < current.Pages.Count;
        }

        return Area(Pages) < Area(current.Pages);
    }

    public static PackRun Once(IReadOnlyList<Piece> pieces, AtlasPackOptions options, MaxRectsHeuristic heuristic)
    {
        var pages = new List<PageBuild>();
        foreach (var piece in pieces)
        {
            var placed = false;
            foreach (var page in pages)
            {
                if (page.TryPlace(piece, options, heuristic))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                var page = new PageBuild(options.MaxPageSize, options.Border);
                var ok = page.TryPlace(piece, options, heuristic);
                Debug.Assert(ok, "oversize pieces are filtered out before packing starts");
                pages.Add(page);
            }
        }

        return new PackRun(pages);
    }

    private static long Area(IReadOnlyList<PageBuild> pages) => pages.Sum(page => page.MaxX * page.MaxY);
}
