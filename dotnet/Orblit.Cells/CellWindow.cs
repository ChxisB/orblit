using System.Collections.Frozen;

namespace Orblit.Cells;

/// <summary>What to bring in and what to let go of, as of one look at the world.</summary>
public sealed class CellPlan
{
    /// <summary>Cells wanted and not yet loaded, the one to load first at the head.</summary>
    public IReadOnlyList<Cell> Load { get; init; } = [];

    /// <summary>Loaded cells nobody needs any more, the farthest at the head.</summary>
    public IReadOnlyList<Cell> Drop { get; init; } = [];
}

/// <summary>
/// The cells worth holding around a point that moves.
/// Cells within <see cref="LoadRadius"/> steps of the point are wanted. Cells
/// stay until they are more than <see cref="DropRadius"/> steps away. The gap
/// between the two is the margin that keeps a point walking up and down a
/// border from loading and dropping the same cell every few frames.
/// </summary>
public sealed class CellWindow
{
    private static readonly IReadOnlySet<Cell> None = FrozenSet<Cell>.Empty;

    public CellWindow(Grid grid, long loadRadius, long? dropRadius = null, double behindWeight = 1)
    {
        Grid = grid;
        LoadRadius = loadRadius;
        DropRadius = dropRadius ?? loadRadius + 1;
        BehindWeight = behindWeight;

        if (loadRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(loadRadius), loadRadius, "negative");
        }

        if (DropRadius < loadRadius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dropRadius),
                dropRadius,
                "below loadRadius, so a cell would drop the frame it loads");
        }

        if (!double.IsFinite(behindWeight) || behindWeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(behindWeight), behindWeight, "negative");
        }
    }

    public Grid Grid { get; }

    public long LoadRadius { get; }

    public long DropRadius { get; }

    /// <summary>
    /// How much farther a cell behind the point counts than one in front. A cell
    /// straight behind ranks as if it were this much farther again than its real
    /// distance. Zero ranks by distance alone.
    /// </summary>
    public double BehindWeight { get; }

    /// <summary>Whether <paramref name="cell"/> is close enough to <paramref name="focus"/> to stay loaded.</summary>
    public bool Keeps(Cell cell, Cell focus) => Grid.Apart(cell, focus) <= DropRadius;

    /// <summary>
    /// What to load and drop for a point at (<paramref name="x"/>,
    /// <paramref name="z"/>) facing (<paramref name="facingX"/>,
    /// <paramref name="facingZ"/>), given the cells already
    /// <paramref name="loaded"/>.
    /// A <paramref name="pinned"/> cell is wanted wherever it is and is never
    /// dropped. Something that must not lose its ground, such as a character in
    /// contact with it, pins the cell it stands on.
    /// A point that is not a finite number plans nothing, since dropping the
    /// world because a position went bad is the worse mistake.
    /// </summary>
    public CellPlan Plan(
        double x,
        double z,
        double facingX = 0,
        double facingZ = 0,
        IReadOnlySet<Cell>? loaded = null,
        IReadOnlySet<Cell>? pinned = null)
    {
        if (Grid.CellOf(x, z) is not { } focus)
        {
            return new CellPlan();
        }

        loaded ??= None;
        pinned ??= None;
        var ranking = Ranking(x, z, facingX, facingZ);

        // The cell the point stands in comes first, whichever way it faces. A
        // neighbour ahead can otherwise outrank it from near a border.
        double Score(Cell cell) => cell == focus ? -1 : ranking(cell);

        var wanted = new HashSet<Cell>(Grid.Around(focus, LoadRadius));
        wanted.UnionWith(pinned);
        var load = wanted.Where(cell => !loaded.Contains(cell)).ToList();
        load.Sort((a, b) => ByScore(Score(a), Score(b), a, b));
        var drop = loaded.Where(cell => !Keeps(cell, focus) && !pinned.Contains(cell)).ToList();
        drop.Sort((a, b) => ByScore(Score(b), Score(a), a, b));
        return new CellPlan { Load = load, Drop = drop };
    }

    /// <summary>How far a cell is from the point, counted longer for a cell behind it.</summary>
    private Func<Cell, double> Ranking(double x, double z, double facingX, double facingZ)
    {
        var length = Math.Sqrt(facingX * facingX + facingZ * facingZ);
        var facing = length > 0 && double.IsFinite(length);
        return cell =>
        {
            var middle = Grid.CenterOf(cell);
            var dx = middle.X - x;
            var dz = middle.Z - z;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            if (!facing || distance == 0)
            {
                return distance;
            }

            var cosine = (dx * facingX + dz * facingZ) / (distance * length);
            return distance * (1 + BehindWeight * (1 - cosine) / 2);
        };
    }

    // The coordinates break a tie, so a plan is the same plan every run.
    private static int ByScore(double a, double b, Cell first, Cell second)
    {
        var byScore = NanLast(a, b);
        if (byScore != 0)
        {
            return byScore;
        }

        return first.X != second.X ? first.X.CompareTo(second.X) : first.Z.CompareTo(second.Z);
    }

    // Dart orders a NaN above every number and C# below. A rank is NaN when a
    // cell's middle overflows to infinity. Negative zero is not reachable here.
    private static int NanLast(double a, double b) =>
        double.IsNaN(a) || double.IsNaN(b) ? double.IsNaN(a).CompareTo(double.IsNaN(b)) : a.CompareTo(b);
}
