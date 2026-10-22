using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>A cell of a <see cref="NavGrid"/>.</summary>
public readonly record struct NavCell(long I, long J);

/// <summary>
/// Ground cut into square cells, each walkable or not, for finding a way
/// across it.
/// Cell (<c>i</c>, <c>j</c>) covers x from <c>OriginX + i * Cell</c> and z from
/// <c>OriginZ + j * Cell</c>, and has one height. Everything is on the ground
/// plane: a cell is open or closed, and there is one floor to a cell.
/// </summary>
public sealed class NavGrid
{
    private const double Diagonal = 1.4142135623730951;

    private static readonly (int, int)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private readonly byte[] _open;

    /// <summary>
    /// How many standing obstacles shut each cell. Apart from <see cref="_open"/> so that
    /// lifting one never opens a cell a wall closes or another obstacle shuts.
    /// </summary>
    private readonly ushort[] _shut;

    private readonly float[] _heights;
    private readonly float[] _weights;

    public NavGrid(int width, int depth, double cell, double originX = 0, double originZ = 0)
    {
        if (width <= 0 || depth <= 0)
        {
            throw new ArgumentException("A grid is at least one cell across.");
        }

        if (!(cell > 0) || !double.IsFinite(cell))
        {
            throw new ArgumentException("Must be above zero.", nameof(cell));
        }

        // An array cannot hold a larger grid.
        if ((long)width * depth > Array.MaxLength)
        {
            throw new ArgumentException("A grid cannot hold more cells than an array can.");
        }

        Width = width;
        Depth = depth;
        Cell = cell;
        OriginX = originX;
        OriginZ = originZ;
        var count = width * depth;
        _open = new byte[count];
        Array.Fill(_open, (byte)1);
        _shut = new ushort[count];
        _heights = new float[count];
        _weights = new float[count];
        Array.Fill(_weights, 1f);
    }

    public int Width { get; }

    public int Depth { get; }

    /// <summary>Metres a cell is across.</summary>
    public double Cell { get; }

    /// <summary>The world x of the corner of cell (0, 0).</summary>
    public double OriginX { get; }

    /// <summary>The world z of the corner of cell (0, 0).</summary>
    public double OriginZ { get; }

    /// <summary>
    /// Whether something can stand in cell (<paramref name="i"/>, <paramref name="j"/>). Off the grid is closed,
    /// and so is a cell a standing <see cref="NavObstacle"/> shuts.
    /// </summary>
    public bool IsOpen(long i, long j) => Inside(i, j) && _open[(j * Width) + i] == 1 && _shut[(j * Width) + i] == 0;

    public void SetOpen(long i, long j, bool open)
    {
        if (Inside(i, j))
        {
            _open[(j * Width) + i] = (byte)(open ? 1 : 0);
        }
    }

    /// <summary>The height of the floor of cell (<paramref name="i"/>, <paramref name="j"/>), or 0 off the grid.</summary>
    public double HeightOf(long i, long j) => Inside(i, j) ? _heights[(j * Width) + i] : 0;

    public void SetHeight(long i, long j, double height)
    {
        if (Inside(i, j))
        {
            _heights[(j * Width) + i] = (float)height;
        }
    }

    /// <summary>How many times dearer a metre is in cell (<paramref name="i"/>, <paramref name="j"/>) than on the flat.</summary>
    public double WeightOf(long i, long j) => Inside(i, j) ? _weights[(j * Width) + i] : 1;

    /// <summary>
    /// Sets what a metre costs in a cell. Below 1 is refused, because a search
    /// that can be rewarded for a detour cannot be steered by distance.
    /// </summary>
    public void SetWeight(long i, long j, double weight)
    {
        if (!(weight >= 1) || !double.IsFinite(weight))
        {
            throw new ArgumentException("Must be 1 or more.", nameof(weight));
        }

        if (Inside(i, j))
        {
            _weights[(j * Width) + i] = (float)weight;
        }
    }

    /// <summary>The cell holding world (<paramref name="x"/>, <paramref name="z"/>), or null off the grid.</summary>
    public NavCell? CellAt(double x, double z)
    {
        var i = Doubles.Floor((x - OriginX) / Cell);
        var j = Doubles.Floor((z - OriginZ) / Cell);
        return Inside(i, j) ? new NavCell(i, j) : null;
    }

    /// <summary>The middle of cell (<paramref name="i"/>, <paramref name="j"/>) at its floor.</summary>
    public Vector3 CentreOf(long i, long j) =>
        new(OriginX + ((i + 0.5) * Cell), HeightOf(i, j), OriginZ + ((j + 0.5) * Cell));

    /// <summary>Closes every cell the rectangle touches.</summary>
    public void BlockRect(double minX, double minZ, double maxX, double maxZ)
    {
        foreach (var at in RectCells(minX, minZ, maxX, maxZ))
        {
            _open[at] = 0;
        }
    }

    /// <summary>Closes every cell whose middle is within <paramref name="radius"/> of (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public void BlockCircle(double x, double z, double radius)
    {
        foreach (var at in CircleCells(x, z, radius))
        {
            _open[at] = 0;
        }
    }

    /// <summary>
    /// Shuts every cell the rectangle touches until the obstacle is lifted.
    /// A door that closes, a crate pushed into a corridor. Unlike <see cref="BlockRect"/>
    /// it can be undone, and only the cells it touched change either way.
    /// </summary>
    public NavObstacle AddObstacleRect(double minX, double minZ, double maxX, double maxZ) =>
        Shutting(RectCells(minX, minZ, maxX, maxZ));

    /// <summary>
    /// Shuts every cell whose middle is within <paramref name="radius"/> of (<paramref name="x"/>, <paramref name="z"/>) until the
    /// obstacle is lifted.
    /// </summary>
    public NavObstacle AddObstacleCircle(double x, double z, double radius) => Shutting(CircleCells(x, z, radius));

    /// <summary>
    /// A copy with every cell within <paramref name="radius"/> of a closed one closed too, so a
    /// route planned on it stays that far from every wall for something that
    /// size. The edge of the grid is not a wall. A cell a standing obstacle
    /// shuts counts as closed, and the copy does not change when it is lifted.
    /// </summary>
    public NavGrid Inflated(double radius)
    {
        var copy = new NavGrid(Width, Depth, Cell, OriginX, OriginZ);
        _heights.CopyTo(copy._heights, 0);
        _weights.CopyTo(copy._weights, 0);
        _open.CopyTo(copy._open, 0);
        for (var j = 0; j < Depth; j++)
        {
            for (var i = 0; i < Width; i++)
            {
                if (!IsOpen(i, j))
                {
                    copy.BlockCircle(OriginX + ((i + 0.5) * Cell), OriginZ + ((j + 0.5) * Cell), radius);
                }
            }
        }

        return copy;
    }

    /// <summary>The open cell nearest (<paramref name="x"/>, <paramref name="z"/>) within <paramref name="within"/> cells, or null.</summary>
    public NavCell? NearestOpen(double x, double z, int within = 8)
    {
        var start = CellAt(x, z);
        var ci = start?.I ?? Doubles.Floor((x - OriginX) / Cell);
        var cj = start?.J ?? Doubles.Floor((z - OriginZ) / Cell);
        NavCell? best = null;
        var bestGap = double.PositiveInfinity;
        for (var j = cj - within; j <= cj + within; j++)
        {
            for (var i = ci - within; i <= ci + within; i++)
            {
                if (!IsOpen(i, j))
                {
                    continue;
                }

                var dx = OriginX + ((i + 0.5) * Cell) - x;
                var dz = OriginZ + ((j + 0.5) * Cell) - z;
                var gap = (dx * dx) + (dz * dz);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = new NavCell(i, j);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Whether the straight line between two places crosses only open cells.
    /// A line through a corner where two cells meet needs both of the others
    /// open, so it cannot squeeze between two walls that touch.
    /// </summary>
    public bool IsClear(Vector3 from, Vector3 to) => LineOver(from, to, IsOpen);

    /// <summary>
    /// A route from <paramref name="from"/> to <paramref name="to"/> over open cells, as the points to walk
    /// through, or null when there is none.
    /// A start or a goal in a closed cell is taken to the nearest open one,
    /// which is what an agent pushed into a wall or told to go to a rock wants.
    /// The route is the cheapest over the cells and then pulled tight: every
    /// point that a straight line can skip is dropped, so it turns only where
    /// a corner makes it. Heights are the floor of the cell at each point.
    /// </summary>
    public IReadOnlyList<Vector3>? FindPath(Vector3 from, Vector3 to)
    {
        var start = OpenNear(from);
        var goal = OpenNear(to);
        if (start is null || goal is null)
        {
            return null;
        }

        var cells = Search((int)((start.Value.J * Width) + start.Value.I), (int)((goal.Value.J * Width) + goal.Value.I));
        if (cells is null)
        {
            return null;
        }

        var points = new List<Vector3> { GroundAt(from, start.Value) };
        points.AddRange(cells.Skip(1).Take(Math.Max(0, cells.Count - 2)).Select(at => CentreOf(at % Width, at / Width)));
        points.Add(GroundAt(to, goal.Value));
        return PulledTight(points);
    }

    /// <summary>Opens the cells an obstacle shut, except any another obstacle also shuts.</summary>
    internal void Release(IReadOnlyList<int> cells)
    {
        foreach (var at in cells)
        {
            _shut[at]--;
        }
    }

    private static long Abs(long value) => value < 0 ? unchecked(-value) : value;

    private bool Inside(long i, long j) => i >= 0 && j >= 0 && i < Width && j < Depth;

    private NavObstacle Shutting(IEnumerable<int> cells)
    {
        var shut = cells.ToArray();
        foreach (var at in shut)
        {
            _shut[at]++;
        }

        return new NavObstacle(this, shut);
    }

    private IEnumerable<int> RectCells(double minX, double minZ, double maxX, double maxZ)
    {
        var first = Math.Max(0, Doubles.Floor((minX - OriginX) / Cell));
        var last = Math.Min(Width - 1, Doubles.Floor((maxX - OriginX) / Cell));
        var top = Math.Max(0, Doubles.Floor((minZ - OriginZ) / Cell));
        var bottom = Math.Min(Depth - 1, Doubles.Floor((maxZ - OriginZ) / Cell));
        for (var j = top; j <= bottom; j++)
        {
            for (var i = first; i <= last; i++)
            {
                yield return (int)((j * Width) + i);
            }
        }
    }

    private IEnumerable<int> CircleCells(double x, double z, double radius)
    {
        var reach = radius / Cell;
        var ci = Doubles.Round(((x - OriginX) / Cell) - 0.5);
        var cj = Doubles.Round(((z - OriginZ) / Cell) - 0.5);
        var span = Doubles.Ceil(reach) + 1;
        for (var j = cj - span; j <= cj + span; j++)
        {
            for (var i = ci - span; i <= ci + span; i++)
            {
                if (!Inside(i, j))
                {
                    continue;
                }

                var dx = OriginX + ((i + 0.5) * Cell) - x;
                var dz = OriginZ + ((j + 0.5) * Cell) - z;
                if ((dx * dx) + (dz * dz) <= radius * radius)
                {
                    yield return (int)((j * Width) + i);
                }
            }
        }
    }

    /// <summary>
    /// Whether the line between two places crosses only cells that <paramref name="allowed"/>
    /// says yes to, with the corner rule of <see cref="IsClear"/>.
    /// </summary>
    private bool LineOver(Vector3 from, Vector3 to, Func<long, long, bool> allowed)
    {
        var ax = (from.X - OriginX) / Cell;
        var az = (from.Z - OriginZ) / Cell;
        var dx = ((to.X - OriginX) / Cell) - ax;
        var dz = ((to.Z - OriginZ) / Cell) - az;
        var i = Doubles.Floor(ax);
        var j = Doubles.Floor(az);
        var endI = Doubles.Floor((to.X - OriginX) / Cell);
        var endJ = Doubles.Floor((to.Z - OriginZ) / Cell);
        if (!allowed(i, j))
        {
            return false;
        }

        var x = new Crossing(dx, ax, i);
        var z = new Crossing(dz, az, j);
        // A far end that wraps the 64-bit difference gives a negative budget, and the answer is true there.
        var budget = unchecked(Abs(endI - i) + Abs(endJ - j) + 2);
        while ((i != endI || j != endJ) && budget-- > 0)
        {
            if (x.Next < z.Next)
            {
                i = x.Advance(i);
            }
            else if (z.Next < x.Next)
            {
                j = z.Advance(j);
            }
            else
            {
                if (!allowed(i + x.Step, j) || !allowed(i, j + z.Step))
                {
                    return false;
                }

                i = x.Advance(i);
                j = z.Advance(j);
            }

            if (!allowed(i, j))
            {
                return false;
            }
        }

        return true;
    }

    private NavCell? OpenNear(Vector3 at)
    {
        var inside = CellAt(at.X, at.Z);
        if (inside is { } cell && IsOpen(cell.I, cell.J))
        {
            return inside;
        }

        return NearestOpen(at.X, at.Z);
    }

    /// <summary>
    /// <paramref name="at"/> kept where it is when its cell is open, and the middle of <paramref name="nearest"/>
    /// otherwise, which is where it was taken to.
    /// </summary>
    private Vector3 GroundAt(Vector3 at, NavCell nearest)
    {
        var inside = CellAt(at.X, at.Z);
        if (inside == nearest)
        {
            return new Vector3(at.X, HeightOf(nearest.I, nearest.J), at.Z);
        }

        return CentreOf(nearest.I, nearest.J);
    }

    /// <summary>
    /// <paramref name="points"/> with every one dropped that a straight line can skip. A line
    /// only skips over cells that cost what the cell it leaves does, so a
    /// detour round dear ground is not cut back through it.
    /// </summary>
    private List<Vector3> PulledTight(List<Vector3> points)
    {
        var tight = new List<Vector3> { points[0] };
        var anchor = 0;
        while (anchor < points.Count - 1)
        {
            var weight = WeightAt(points[anchor]);
            var far = anchor + 1;
            while (far + 1 < points.Count
                && LineOver(points[anchor], points[far + 1], (i, j) => IsOpen(i, j) && WeightOf(i, j) == weight))
            {
                far++;
            }

            tight.Add(points[far]);
            anchor = far;
        }

        return tight;
    }

    private double WeightAt(Vector3 at) => CellAt(at.X, at.Z) is { } inside ? WeightOf(inside.I, inside.J) : 1;

    /// <summary>The cheapest run of cell numbers from <paramref name="start"/> to <paramref name="goal"/>, or null.</summary>
    private List<int>? Search(int start, int goal) => new CellSearch(this, goal).Run(start);

    /// <summary>One crossing axis of <see cref="LineOver"/>: which way it steps and when it next meets a cell edge.</summary>
    private struct Crossing
    {
        private readonly double _delta;

        internal Crossing(double d, double a, long cell)
        {
            Step = d > 0 ? 1 : (d < 0 ? -1 : 0);
            _delta = d == 0 ? double.PositiveInfinity : 1 / Math.Abs(d);
            Next = d == 0 ? double.PositiveInfinity : (d > 0 ? cell + 1 - a : a - cell) * _delta;
        }

        internal long Step { get; }

        internal double Next { get; private set; }

        internal long Advance(long cell)
        {
            Next += _delta;
            return cell + Step;
        }
    }

    /// <summary>An A* search over the cells, eight ways, with the corner rule.</summary>
    private sealed class CellSearch
    {
        private readonly NavGrid _grid;
        private readonly int _goal;
        private readonly double[] _spent;
        private readonly int[] _came;
        private readonly bool[] _done;
        private readonly MinHeap _queue = new();

        internal CellSearch(NavGrid grid, int goal)
        {
            var count = grid.Width * grid.Depth;
            _grid = grid;
            _goal = goal;
            _spent = new double[count];
            Array.Fill(_spent, double.PositiveInfinity);
            _came = new int[count];
            Array.Fill(_came, -1);
            _done = new bool[count];
        }

        internal List<int>? Run(int start)
        {
            _spent[start] = 0;
            _queue.Add(start, Estimate(start % _grid.Width, start / _grid.Width));
            while (!_queue.IsEmpty)
            {
                var at = _queue.RemoveFirst();
                if (_done[at])
                {
                    continue;
                }

                _done[at] = true;
                if (at == _goal)
                {
                    return Unwind();
                }

                foreach (var (di, dj) in Around)
                {
                    Relax(at, di, dj);
                }
            }

            return null;
        }

        private double Estimate(int i, int j)
        {
            var dx = Math.Abs(i - (_goal % _grid.Width));
            var dz = Math.Abs(j - (_goal / _grid.Width));
            return (Math.Max(dx, dz) + ((Diagonal - 1) * Math.Min(dx, dz))) * _grid.Cell;
        }

        private void Relax(int at, int di, int dj)
        {
            var width = _grid.Width;
            var i = at % width;
            var j = at / width;
            var ni = i + di;
            var nj = j + dj;
            if (!_grid.IsOpen(ni, nj) || (di != 0 && dj != 0 && !(_grid.IsOpen(i + di, j) && _grid.IsOpen(i, j + dj))))
            {
                return;
            }

            var next = (nj * width) + ni;
            if (_done[next])
            {
                return;
            }

            var run = (di != 0 && dj != 0 ? Diagonal : 1) * _grid.Cell;
            var cost = _spent[at] + (run * ((double)_grid._weights[at] + _grid._weights[next]) / 2);
            if (cost >= _spent[next])
            {
                return;
            }

            _spent[next] = cost;
            _came[next] = at;
            _queue.Add(next, cost + Estimate(ni, nj));
        }

        private List<int> Unwind()
        {
            var run = new List<int>();
            for (var at = _goal; at >= 0; at = _came[at])
            {
                run.Add(at);
            }

            run.Reverse();
            return run;
        }
    }
}

/// <summary>
/// Cells of a <see cref="NavGrid"/> shut for as long as it stands.
/// Made by <see cref="NavGrid.AddObstacleRect"/> or <see cref="NavGrid.AddObstacleCircle"/>. To move
/// one, lift it and add another.
/// </summary>
public sealed class NavObstacle
{
    private readonly NavGrid _grid;
    private readonly int[] _cells;

    internal NavObstacle(NavGrid grid, int[] cells)
    {
        _grid = grid;
        _cells = cells;
    }

    /// <summary>Whether the cells are still shut by this obstacle.</summary>
    public bool Standing { get; private set; } = true;

    /// <summary>
    /// Opens the cells it shut, except any a wall or another obstacle also
    /// shuts. Lifting one that is not standing does nothing.
    /// </summary>
    public void Lift()
    {
        if (!Standing)
        {
            return;
        }

        Standing = false;
        _grid.Release(_cells);
    }
}
