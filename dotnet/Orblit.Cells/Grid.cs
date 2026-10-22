using System.Diagnostics.CodeAnalysis;

namespace Orblit.Cells;

/// <summary>One square of a <see cref="Grid"/>, by how many cells it is from the origin.</summary>
public readonly record struct Cell(long X, long Z);

/// <summary>
/// Square cells on the ground plane. The cell size lives here and nowhere
/// else, so everything that cuts the world up agrees on where the cuts are.
/// </summary>
public sealed class Grid
{
    /// <summary>The farthest a cell number may be from zero.</summary>
    public const double Reach = 1e9;

    public Grid(double cellSize)
    {
        if (!double.IsFinite(cellSize) || cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "not above zero");
        }

        CellSize = cellSize;
    }

    public double CellSize { get; }

    /// <summary>
    /// The cell holding (<paramref name="x"/>, <paramref name="z"/>), or null
    /// when either is not a finite number or lies more than <see cref="Reach"/>
    /// cells from the origin.
    /// A position that far is a physics blow-up and not a place. Cell numbers
    /// that large overflow when a window is drawn round them.
    /// </summary>
    public Cell? CellOf(double x, double z)
    {
        var cx = x / CellSize;
        var cz = z / CellSize;
        if (!double.IsFinite(cx) || !double.IsFinite(cz))
        {
            return null;
        }

        if (Math.Abs(cx) > Reach || Math.Abs(cz) > Reach)
        {
            return null;
        }

        return new Cell((long)Math.Floor(cx), (long)Math.Floor(cz));
    }

    /// <summary>The middle of <paramref name="cell"/>.</summary>
    public (double X, double Z) CenterOf(Cell cell) =>
        ((cell.X + 0.5) * CellSize, (cell.Z + 0.5) * CellSize);

    /// <summary>How many cells apart <paramref name="a"/> and <paramref name="b"/> are, counting a diagonal step as one.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Callers ask the grid, so it stays an instance method.")]
    public long Apart(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));

    /// <summary><paramref name="from"/> and every cell within <paramref name="radius"/> steps of it, as a square.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Callers ask the grid, so it stays an instance method.")]
    public IEnumerable<Cell> Around(Cell from, long radius)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "negative");
        }

        for (var x = from.X - radius; x <= from.X + radius; x++)
        {
            for (var z = from.Z - radius; z <= from.Z + radius; z++)
            {
                yield return new Cell(x, z);
            }
        }
    }
}
