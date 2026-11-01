using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>
/// Ground as a few large rectangles, for finding a way across it.
/// Built from a <see cref="NavGrid"/>: open cells that cost the same to cross are merged
/// into the biggest rectangles that fit, and two rectangles are joined where
/// they share an edge. A route is found over those joins and then pulled
/// straight through them, so it turns only at the corners of walls. A big
/// open field is one rectangle and costs one step to search however many
/// cells it was, which is what a grid cannot do.
/// The mesh is a snapshot. Change the grid and build another.
/// </summary>
public sealed class NavMesh
{
    private readonly NavGrid _grid;
    private readonly List<MeshBlock> _blocks;
    private readonly int[] _owner;
    private readonly List<MeshPortal> _portals;
    private readonly List<List<int>> _portalsOf;

    private NavMesh(NavGrid grid, List<MeshBlock> blocks, int[] owner, List<MeshPortal> portals, List<List<int>> portalsOf)
    {
        _grid = grid;
        _blocks = blocks;
        _owner = owner;
        _portals = portals;
        _portalsOf = portalsOf;
    }

    /// <summary>How many rectangles the ground was merged into.</summary>
    public int Count => _blocks.Count;

    public static NavMesh FromGrid(NavGrid grid, double radius = 0)
    {
        var snapshot = grid.Inflated(radius);
        var blocks = MeshBuilder.Merge(snapshot);
        var owner = new int[snapshot.Width * snapshot.Depth];
        Array.Fill(owner, -1);
        for (var b = 0; b < blocks.Count; b++)
        {
            var index = b;
            blocks[b].ForEachCell((i, j) => owner[(j * snapshot.Width) + i] = index);
        }

        var portals = MeshBuilder.Connect(snapshot, blocks, owner);
        var byBlock = blocks.Select(_ => new List<int>()).ToList();
        for (var p = 0; p < portals.Count; p++)
        {
            byBlock[portals[p].A].Add(p);
            byBlock[portals[p].B].Add(p);
        }

        return new NavMesh(snapshot, blocks, owner, portals, byBlock);
    }

    /// <summary>
    /// A route from <paramref name="from"/> to <paramref name="to"/> as the points to walk through, or null
    /// when there is none.
    /// A start or a goal on closed ground is taken to the nearest open cell.
    /// The route is the shortest the mesh allows, with every point that a
    /// straight line could skip dropped.
    /// </summary>
    public IReadOnlyList<Vector3>? FindPath(Vector3 from, Vector3 to)
    {
        var start = Standing(from);
        var goal = Standing(to);
        if (start is null || goal is null)
        {
            return null;
        }

        var route = Route(start, goal);
        if (route is null)
        {
            return null;
        }

        var gates = route.Select(step => Gate(_portals[step.Portal], step.From)).ToList();
        return new FunnelWalk(start.At, goal.At, gates).Run()
            .Select(at => new Vector3(at.X, HeightNear(at.X, at.Y), at.Y))
            .ToList();
    }

    /// <summary>
    /// Where <paramref name="at"/> stands on the mesh: which rectangle, and the point in it,
    /// which is <paramref name="at"/> unless it was taken to the nearest open cell.
    /// </summary>
    private MeshStanding? Standing(Vector3 at)
    {
        if (_grid.CellAt(at.X, at.Z) is { } inside && _grid.IsOpen(inside.I, inside.J))
        {
            return new MeshStanding(_owner[(inside.J * _grid.Width) + inside.I], new Vector2(at.X, at.Z));
        }

        if (_grid.NearestOpen(at.X, at.Z) is not { } near)
        {
            return null;
        }

        var middle = _grid.CentreOf(near.I, near.J);
        return new MeshStanding(_owner[(near.J * _grid.Width) + near.I], new Vector2(middle.X, middle.Z));
    }

    private double HeightNear(double x, double z)
    {
        var half = _grid.Cell / 2;
        var total = 0.0;
        var found = 0;
        foreach (var (dx, dz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var cell = _grid.CellAt(x + (dx * half * 0.5), z + (dz * half * 0.5));
            if (cell is not { } at || !_grid.IsOpen(at.I, at.J))
            {
                continue;
            }

            total += _grid.HeightOf(at.I, at.J);
            found++;
        }

        return found == 0 ? 0 : total / found;
    }

    /// <summary>
    /// The portals to cross from <paramref name="start"/> to <paramref name="goal"/>, in order, each with the
    /// rectangle it is crossed from.
    /// </summary>
    private List<MeshCrossing>? Route(MeshStanding start, MeshStanding goal) =>
        start.Block == goal.Block ? [] : new PortalSearch(_portals, _portalsOf, _blocks, start, goal).Run();

    /// <summary>
    /// The two ends of <paramref name="portal"/> as <paramref name="from"/> crosses it, left and right as seen
    /// looking the way it goes.
    /// </summary>
    private static MeshGate Gate(MeshPortal portal, int from)
    {
        var forward = from == portal.A;
        var lower = portal.AlongZ ? new Vector2(portal.At, portal.Low) : new Vector2(portal.Low, portal.At);
        var upper = portal.AlongZ ? new Vector2(portal.At, portal.High) : new Vector2(portal.High, portal.At);
        // Crossing along +x, left is +z. Crossing along +z, left is -x.
        var leftIsUpper = portal.AlongZ ? forward : !forward;
        return leftIsUpper ? new MeshGate(upper, lower) : new MeshGate(lower, upper);
    }
}

internal sealed record MeshStanding(int Block, Vector2 At);

internal readonly record struct MeshCrossing(int Portal, int From);

internal readonly record struct MeshGate(Vector2 Left, Vector2 Right);

/// <summary>A rectangle of cells, ends included.</summary>
internal readonly record struct MeshBlock(int I0, int J0, int I1, int J1, double Weight)
{
    internal void ForEachCell(Action<int, int> visit)
    {
        for (var j = J0; j <= J1; j++)
        {
            for (var i = I0; i <= I1; i++)
            {
                visit(i, j);
            }
        }
    }
}

/// <summary>
/// The edge two blocks share. <c>A</c> is the one with the smaller coordinate
/// across it. An edge <c>AlongZ</c> sits at x = <c>At</c> and runs from <c>Low</c> to
/// <c>High</c> in z, and the blocks lie either side of it in x.
/// </summary>
internal readonly record struct MeshPortal(int A, int B, bool AlongZ, double At, double Low, double High)
{
    internal Vector2 Middle => AlongZ ? new Vector2(At, (Low + High) / 2) : new Vector2((Low + High) / 2, At);
}

/// <summary>
/// A* over portals. A node is a portal's middle, with the start and the
/// goal as the two nodes after the last portal.
/// </summary>
internal sealed class PortalSearch
{
    private readonly List<MeshPortal> _portals;
    private readonly List<List<int>> _portalsOf;
    private readonly List<MeshBlock> _blocks;
    private readonly MeshStanding _start;
    private readonly MeshStanding _goal;
    private readonly int _startNode;
    private readonly int _goalNode;
    private readonly double[] _spent;
    private readonly int[] _came;
    private readonly int[] _through;
    private readonly bool[] _done;
    private readonly MinHeap _queue = new();

    internal PortalSearch(
        List<MeshPortal> portals,
        List<List<int>> portalsOf,
        List<MeshBlock> blocks,
        MeshStanding start,
        MeshStanding goal)
    {
        _portals = portals;
        _portalsOf = portalsOf;
        _blocks = blocks;
        _start = start;
        _goal = goal;
        _startNode = portals.Count;
        _goalNode = portals.Count + 1;
        _spent = new double[portals.Count + 2];
        Array.Fill(_spent, double.PositiveInfinity);
        _came = new int[portals.Count + 2];
        Array.Fill(_came, -1);
        _through = new int[portals.Count + 2];
        Array.Fill(_through, -1);
        _done = new bool[portals.Count + 2];
    }

    internal List<MeshCrossing>? Run()
    {
        _spent[_startNode] = 0;
        _queue.Add(_startNode, Where(_startNode).DistanceTo(_goal.At));
        while (!_queue.IsEmpty)
        {
            var node = _queue.RemoveFirst();
            if (_done[node])
            {
                continue;
            }

            _done[node] = true;
            if (node == _goalNode)
            {
                return Crossings();
            }

            Expand(node);
        }

        return null;
    }

    private Vector2 Where(int node)
    {
        if (node == _startNode)
        {
            return _start.At;
        }

        return node == _goalNode ? _goal.At : _portals[node].Middle;
    }

    private void Expand(int node)
    {
        int[] inside = node == _startNode ? [_start.Block] : [_portals[node].A, _portals[node].B];
        foreach (var block in inside)
        {
            foreach (var next in Leaving(node, block))
            {
                Relax(node, next, block);
            }
        }
    }

    private IEnumerable<int> Leaving(int node, int block)
    {
        foreach (var other in _portalsOf[block])
        {
            if (other != node)
            {
                yield return other;
            }
        }

        if (block == _goal.Block)
        {
            yield return _goalNode;
        }
    }

    private void Relax(int node, int next, int block)
    {
        if (_done[next])
        {
            return;
        }

        var cost = _spent[node] + (Where(node).DistanceTo(Where(next)) * _blocks[block].Weight);
        if (cost >= _spent[next])
        {
            return;
        }

        _spent[next] = cost;
        _came[next] = node;
        _through[next] = block;
        _queue.Add(next, cost + Where(next).DistanceTo(_goal.At));
    }

    private List<MeshCrossing> Crossings()
    {
        var crossings = new List<MeshCrossing>();
        for (var node = _came[_goalNode]; node != _startNode; node = _came[node])
        {
            crossings.Add(new MeshCrossing(node, _through[node]));
        }

        crossings.Reverse();
        return crossings;
    }
}

/// <summary>Greedy rectangles over the open cells, and the edges the rectangles share.</summary>
internal static class MeshBuilder
{
    /// <summary>
    /// Greedy rectangles over the open cells, across then down, each holding
    /// cells that cost the same.
    /// </summary>
    internal static List<MeshBlock> Merge(NavGrid grid)
    {
        var taken = new bool[grid.Width * grid.Depth];
        var blocks = new List<MeshBlock>();
        for (var j = 0; j < grid.Depth; j++)
        {
            for (var i = 0; i < grid.Width; i++)
            {
                if (taken[(j * grid.Width) + i] || !grid.IsOpen(i, j))
                {
                    continue;
                }

                var weight = grid.WeightOf(i, j);
                bool Free(int c, int r) =>
                    grid.IsOpen(c, r) && !taken[(r * grid.Width) + c] && grid.WeightOf(c, r) == weight;
                var right = i;
                while (Free(right + 1, j))
                {
                    right++;
                }

                var bottom = j;
                while (RowFree(i, right, bottom + 1, Free))
                {
                    bottom++;
                }

                var block = new MeshBlock(i, j, right, bottom, weight);
                block.ForEachCell((c, r) => taken[(r * grid.Width) + c] = true);
                blocks.Add(block);
            }
        }

        return blocks;
    }

    /// <summary>
    /// Every pair of blocks that touch along an edge, found once from the block
    /// on the left or the top.
    /// </summary>
    internal static List<MeshPortal> Connect(NavGrid grid, List<MeshBlock> blocks, int[] owner)
    {
        var portals = new List<MeshPortal>();
        int OwnerOf(int i, int j) => i < 0 || j < 0 || i >= grid.Width || j >= grid.Depth ? -1 : owner[(j * grid.Width) + i];
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            var self = b;
            Runs(block.J0, block.J1, j => OwnerOf(block.I1 + 1, j), (other, from, to) => portals.Add(new MeshPortal(
                self,
                other,
                true,
                grid.OriginX + ((block.I1 + 1) * grid.Cell),
                grid.OriginZ + (from * grid.Cell),
                grid.OriginZ + ((to + 1) * grid.Cell))));
            Runs(block.I0, block.I1, i => OwnerOf(i, block.J1 + 1), (other, from, to) => portals.Add(new MeshPortal(
                self,
                other,
                false,
                grid.OriginZ + ((block.J1 + 1) * grid.Cell),
                grid.OriginX + (from * grid.Cell),
                grid.OriginX + ((to + 1) * grid.Cell))));
        }

        return portals;
    }

    private static bool RowFree(int from, int to, int row, Func<int, int, bool> free)
    {
        for (var i = from; i <= to; i++)
        {
            if (!free(i, row))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Calls <paramref name="found"/> for each unbroken run of one owner along <paramref name="first"/> to <paramref name="last"/>.</summary>
    private static void Runs(int first, int last, Func<int, int> ownerAt, Action<int, int, int> found)
    {
        var start = first;
        while (start <= last)
        {
            var owner = ownerAt(start);
            var end = start;
            while (end + 1 <= last && ownerAt(end + 1) == owner)
            {
                end++;
            }

            if (owner >= 0)
            {
                found(owner, start, end);
            }

            start = end + 1;
        }
    }
}

/// <summary>
/// The shortest line from a start to a goal that passes through every gate,
/// by the simple stupid funnel: keep the two lines to the ends of the gates
/// so far, and when one crosses the other, its end is a corner.
/// </summary>
internal sealed class FunnelWalk
{
    private readonly List<MeshGate> _all;
    private readonly Vector2 _goal;
    private readonly List<Vector2> _out;
    private Vector2 _apex;
    private Vector2 _left;
    private Vector2 _right;
    private int _leftAt;
    private int _rightAt;

    internal FunnelWalk(Vector2 start, Vector2 goal, List<MeshGate> gates)
    {
        _goal = goal;
        _all = [new MeshGate(start, start), .. gates, new MeshGate(goal, goal)];
        _out = [start];
        _apex = start;
        _left = start;
        _right = start;
    }

    internal List<Vector2> Run()
    {
        var i = 1;
        while (i < _all.Count)
        {
            i = TightenRight(_all[i].Right, i) ?? TightenLeft(_all[i].Left, i) ?? i + 1;
        }

        if (!Same(_out[^1], _goal))
        {
            _out.Add(_goal);
        }

        return _out;
    }

    /// <summary>Which side of the line from a through b the point c is on: above nought is the counter-clockwise side in x and z.</summary>
    private static double Side(Vector2 a, Vector2 b, Vector2 c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static bool Same(Vector2 a, Vector2 b) => a.DistanceToSquared(b) < 1e-12;

    /// <summary>The gate to go on from when the right edge turned the apex at the left corner, or null.</summary>
    private int? TightenRight(Vector2 end, int i)
    {
        if (Side(_apex, _right, end) < 0)
        {
            return null;
        }

        if (Same(_apex, _right) || Side(_apex, _left, end) < 0)
        {
            _right = end;
            _rightAt = i;
            return null;
        }

        _out.Add(_left);
        _apex = _left;
        _right = _left;
        _rightAt = _leftAt;
        return _leftAt + 1;
    }

    /// <summary>The gate to go on from when the left edge turned the apex at the right corner, or null.</summary>
    private int? TightenLeft(Vector2 end, int i)
    {
        if (Side(_apex, _left, end) > 0)
        {
            return null;
        }

        if (Same(_apex, _left) || Side(_apex, _right, end) > 0)
        {
            _left = end;
            _leftAt = i;
            return null;
        }

        _out.Add(_right);
        _apex = _right;
        _left = _right;
        _leftAt = _rightAt;
        return _rightAt + 1;
    }
}
