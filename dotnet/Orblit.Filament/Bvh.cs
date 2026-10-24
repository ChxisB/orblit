using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>One thing in the tree.</summary>
/// <param name="Key">Whatever the application calls it: an object's key, an index, an id.</param>
/// <param name="Bounds">The box it fills.</param>
public sealed record Volume(long Key, Bounds Bounds);

/// <summary>
/// A tree of boxes over a scene, for asking what is where. What can the camera see and what does
/// this ray hit are the same walk: open a box, and if it is worth opening, open its children.
/// </summary>
/// <remarks>
/// Built once and rebuilt when the scene changes shape, not every frame. What moves within it is
/// handled by <see cref="Refit"/>, which keeps the structure and grows the boxes back over what is
/// inside them. The split is chosen by surface area rather than by cutting down the middle, because
/// the chance of a ray meeting a box goes with its area.
/// </remarks>
public sealed class Bvh
{
    private readonly List<Node> _nodes;
    private readonly List<Volume> _volumes;

    private Bvh(List<Node> nodes, List<Volume> volumes)
    {
        _nodes = nodes;
        _volumes = volumes;
    }

    public bool IsEmpty => _volumes.Count == 0;

    public int Length => _volumes.Count;

    /// <summary>How many boxes the tree holds, leaves and branches together.</summary>
    public int NodeCount => _nodes.Count;

    /// <summary>How deep it goes.</summary>
    public int Depth => _nodes.Count == 0 ? 0 : DepthFrom(0);

    /// <summary>A box around everything.</summary>
    public Bounds? Bounds => _nodes.Count == 0 ? null : _nodes[0].Bounds;

    /// <summary>
    /// Builds a tree over <paramref name="volumes"/>. <paramref name="leafSize"/> is how many things
    /// a leaf may hold before it is split: testing four boxes in a leaf is cheaper than three levels
    /// of tree to keep them apart.
    /// </summary>
    public static Bvh Of(IReadOnlyList<Volume> volumes, int leafSize = 4)
    {
        var held = new List<Volume>(volumes);
        var builder = new Builder(held, Math.Clamp(leafSize, 1, 64));
        if (held.Count > 0)
        {
            builder.Build(0, held.Count);
        }

        return new Bvh(builder.Nodes, held);
    }

    /// <summary>Everything whose box overlaps <paramref name="box"/>.</summary>
    public List<long> Inside(Bounds box)
    {
        var found = new List<long>();
        Walk(0, node => node.Bounds.Overlaps(box), volume =>
        {
            if (volume.Bounds.Overlaps(box))
            {
                found.Add(volume.Key);
            }
        });
        return found;
    }

    /// <summary>
    /// Everything the camera can see, given the six planes of its frustum. A plane is
    /// <c>nx, ny, nz, d</c> with its normal pointing inwards, so a point is inside when
    /// <c>dot(n, p) + d &gt;= 0</c> for all six.
    /// </summary>
    public List<long> Visible(IReadOnlyList<Vector4> planes)
    {
        var found = new List<long>();
        Walk(0, node => InFrustum(node.Bounds, planes), volume =>
        {
            if (InFrustum(volume.Bounds, planes))
            {
                found.Add(volume.Key);
            }
        });
        return found;
    }

    /// <summary>
    /// What a ray hits, nearest first. The distances are to each box, not to the geometry inside
    /// it: a tree knows where things are, not what shape they are.
    /// </summary>
    public List<(long Key, double Distance)> Along(Vector3 from, Vector3 direction, double within = double.PositiveInfinity)
    {
        var found = new List<(long Key, double Distance)>();
        if (_nodes.Count == 0)
        {
            return found;
        }

        Search(0, new RayQuery(from, direction.Normalized(), within), found);
        return [.. found.OrderBy(hit => hit.Distance)];
    }

    /// <summary>The first thing a ray hits, or null.</summary>
    public (long Key, double Distance)? First(Vector3 from, Vector3 direction, double within = double.PositiveInfinity)
    {
        var hits = Along(from, direction, within);
        return hits.Count == 0 ? null : hits[0];
    }

    /// <summary>
    /// Grows every box back over what is inside it, keeping the structure. <paramref name="boundsOf"/>
    /// is asked for the new box of each key. Returning null leaves the old one, which is the right
    /// answer for something that has not moved.
    /// </summary>
    public void Refit(Func<long, Bounds?> boundsOf)
    {
        for (var i = 0; i < _volumes.Count; i++)
        {
            if (boundsOf(_volumes[i].Key) is { } replacement)
            {
                _volumes[i] = new Volume(_volumes[i].Key, replacement);
            }
        }

        if (_nodes.Count > 0)
        {
            RefitFrom(0);
        }
    }

    private int DepthFrom(int index)
    {
        var node = _nodes[index];
        return node.IsLeaf ? 1 : 1 + Math.Max(DepthFrom(node.Left), DepthFrom(node.Right));
    }

    private Bounds RefitFrom(int index)
    {
        var node = _nodes[index];
        if (node.IsLeaf)
        {
            var box = _volumes[node.First].Bounds;
            for (var i = node.First + 1; i < node.First + node.Count; i++)
            {
                box = box.Union(_volumes[i].Bounds);
            }

            node.Bounds = box;
            return box;
        }

        var joined = RefitFrom(node.Left).Union(RefitFrom(node.Right));
        node.Bounds = joined;
        return joined;
    }

    private void Walk(int index, Func<Node, bool> worthOpening, Action<Volume> found)
    {
        if (_nodes.Count == 0)
        {
            return;
        }

        var node = _nodes[index];
        if (!worthOpening(node))
        {
            return;
        }

        if (node.IsLeaf)
        {
            for (var i = node.First; i < node.First + node.Count; i++)
            {
                found(_volumes[i]);
            }

            return;
        }

        Walk(node.Left, worthOpening, found);
        Walk(node.Right, worthOpening, found);
    }

    private void Search(int index, RayQuery ray, List<(long Key, double Distance)> found)
    {
        var node = _nodes[index];
        if (node.Bounds.Hit(ray.From, ray.Direction) is not { } entry || entry.Near > ray.Within)
        {
            return;
        }

        if (!node.IsLeaf)
        {
            Search(node.Left, ray, found);
            Search(node.Right, ray, found);
            return;
        }

        for (var i = node.First; i < node.First + node.Count; i++)
        {
            var volume = _volumes[i];
            if (volume.Bounds.Hit(ray.From, ray.Direction) is { } at && at.Near <= ray.Within)
            {
                found.Add((volume.Key, at.Near));
            }
        }
    }

    private static bool InFrustum(Bounds box, IReadOnlyList<Vector4> planes)
    {
        foreach (var plane in planes)
        {
            // The corner furthest along the plane's inward normal. If even that is behind the
            // plane, every corner is, and the box is out.
            var x = plane.X >= 0 ? box.Maximum.X : box.Minimum.X;
            var y = plane.Y >= 0 ? box.Maximum.Y : box.Minimum.Y;
            var z = plane.Z >= 0 ? box.Maximum.Z : box.Minimum.Z;
            if ((plane.X * x) + (plane.Y * y) + (plane.Z * z) + plane.W < 0)
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct RayQuery(Vector3 From, Vector3 Direction, double Within);

    private sealed class Node
    {
        private Node(Bounds bounds, int first, int count, int left, int right)
        {
            Bounds = bounds;
            First = first;
            Count = count;
            Left = left;
            Right = right;
        }

        public Bounds Bounds { get; set; }

        /// <summary>A leaf's slice of the volume list.</summary>
        public int First { get; }

        public int Count { get; }

        /// <summary>A branch's children, as indices into the node list.</summary>
        public int Left { get; }

        public int Right { get; }

        public bool IsLeaf => Left < 0;

        public static Node Leaf(Bounds bounds, int first, int count) => new(bounds, first, count, -1, -1);

        public static Node Branch(Bounds bounds, int left, int right) => new(bounds, 0, 0, left, right);
    }

    /// <summary>The buckets a split is looked for in, along one axis.</summary>
    private readonly record struct Buckets(int Axis, double Low, double Scale)
    {
        /// <summary>
        /// Twelve rather than every position: the best of twelve is within a percent or two of
        /// the best of all of them on any real scene, and it is the difference between a build that
        /// is linear in the number of things and one that is quadratic.
        /// </summary>
        public const int Count = 12;

        public int Of(Volume volume)
        {
            var at = Doubles.Floor((volume.Bounds.Centre[Axis] - Low) * Scale);
            return at < 0 ? 0 : at >= Count ? Count - 1 : (int)at;
        }
    }

    private sealed class Builder(List<Volume> volumes, int leafSize)
    {
        public List<Node> Nodes { get; } = [];

        /// <summary>Builds one node over <c>volumes[from..to)</c> and returns its index.</summary>
        public int Build(int from, int to)
        {
            var (box, centroids) = Extent(from, to);
            var count = to - from;
            if (count <= leafSize)
            {
                Nodes.Add(Node.Leaf(box, from, count));
                return Nodes.Count - 1;
            }

            var middle = Split(from, to, centroids);

            // Reserved before the children so the branch keeps its place in the list: the
            // children are appended by the recursion and would otherwise take it.
            var index = Nodes.Count;
            Nodes.Add(Node.Leaf(box, from, count));
            var left = Build(from, middle);
            var right = Build(middle, to);
            Nodes[index] = Node.Branch(box, left, right);
            return index;
        }

        private (Bounds Box, Bounds Centroids) Extent(int from, int to)
        {
            var box = volumes[from].Bounds;
            var centroids = Bounds.At(box.Centre);
            for (var i = from + 1; i < to; i++)
            {
                box = box.Union(volumes[i].Bounds);
                centroids = centroids.Union(Bounds.At(volumes[i].Bounds.Centre));
            }

            return (box, centroids);
        }

        /// <summary>Orders <c>volumes[from..to)</c> into two halves and returns where the second starts.</summary>
        private int Split(int from, int to, Bounds centroids)
        {
            // The axis the things are most spread along. Splitting across the narrowest one makes
            // two boxes that overlap almost entirely, a tree that opens both halves for every query.
            var spread = centroids.Size;
            var axis = spread.Y > spread.X ? 1 : 0;
            axis = spread.Z > spread[axis] ? 2 : axis;

            var middle = from + ((to - from) / 2);
            var low = centroids.Minimum[axis];
            var high = centroids.Maximum[axis];
            if (high - low <= 1e-9)
            {
                return middle;
            }

            var buckets = new Buckets(axis, low, Buckets.Count / (high - low));
            var cut = BestCut(Fill(from, to, buckets));
            if (cut < 0)
            {
                return middle;
            }

            // A partition that moved everything to one side is not a split. Halving always makes
            // progress and so always ends.
            var write = Partition(from, to, cut, buckets);
            return write > from && write < to ? write : middle;
        }

        private (int[] Counts, Bounds?[] Boxes) Fill(int from, int to, Buckets buckets)
        {
            var counts = new int[Buckets.Count];
            var boxes = new Bounds?[Buckets.Count];
            for (var i = from; i < to; i++)
            {
                var bucket = buckets.Of(volumes[i]);
                counts[bucket]++;
                boxes[bucket] = boxes[bucket]?.Union(volumes[i].Bounds) ?? volumes[i].Bounds;
            }

            return (counts, boxes);
        }

        /// <summary>
        /// The cut after the bucket that costs least, or -1 when none leaves things on both sides.
        /// The cost is how much area is on each side, weighted by how many things are on it: what
        /// a query pays is the chance of having to open a box times what is in it.
        /// </summary>
        private static int BestCut((int[] Counts, Bounds?[] Boxes) filled)
        {
            var bestCost = double.PositiveInfinity;
            var bestCut = -1;
            for (var cut = 0; cut < Buckets.Count - 1; cut++)
            {
                var (left, leftCount) = Joined(filled, 0, cut + 1);
                var (right, rightCount) = Joined(filled, cut + 1, Buckets.Count);
                if (left is null || right is null)
                {
                    continue;
                }

                var cost = (left.Area * leftCount) + (right.Area * rightCount);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestCut = cut;
                }
            }

            return bestCut;
        }

        private static (Bounds? Box, int Count) Joined((int[] Counts, Bounds?[] Boxes) filled, int from, int to)
        {
            Bounds? box = null;
            var count = 0;
            for (var i = from; i < to; i++)
            {
                if (filled.Boxes[i] is { } next)
                {
                    box = box?.Union(next) ?? next;
                    count += filled.Counts[i];
                }
            }

            return (box, count);
        }

        /// <summary>
        /// Everything below the cut to the left, everything above to the right, in place the way
        /// quicksort does it, and the index where the right side starts.
        /// </summary>
        private int Partition(int from, int to, int cut, Buckets buckets)
        {
            var write = from;
            for (var i = from; i < to; i++)
            {
                if (buckets.Of(volumes[i]) <= cut)
                {
                    (volumes[write], volumes[i]) = (volumes[i], volumes[write]);
                    write++;
                }
            }

            return write;
        }
    }
}
