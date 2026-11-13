using Avalonia;

namespace Orblit.Editor.App.Graph;

/// <summary>Which edge or middle of the selected steps they line up on.</summary>
internal enum Edge
{
    Left,
    Center,
    Right,
    Top,
    Middle,
    Bottom,
}

/// <summary>The direction steps are spread or stacked in.</summary>
internal enum Axis
{
    Horizontal,
    Vertical,
}

/// <summary>A step and the box it fills in the graph.</summary>
internal readonly record struct Placed(int Id, Rect Bounds);

/// <summary>How far one step moves.</summary>
internal readonly record struct Move(int Id, Vector By);

/// <summary>A wire between two steps and the height of the pin at each end.</summary>
internal readonly record struct Link(int From, double FromY, int To, double ToY);

/// <summary>
/// Where steps should go to line up, spread out, stack or sit level with the steps that feed them. Each answer is
/// a list of moves. Nothing here touches the graph, so the canvas decides how the moves are applied.
/// A step that would not move is left out.
/// </summary>
internal static class StepLayout
{
    const double Gap = 24;

    /// <summary>Moves every step onto one edge or middle of the box that holds them all.</summary>
    public static IReadOnlyList<Move> Align(IReadOnlyList<Placed> steps, Edge edge)
    {
        if (steps.Count < 2)
        {
            return [];
        }

        var all = steps.Select(step => step.Bounds).Aggregate((a, b) => a.Union(b));
        return edge switch
        {
            Edge.Left => Shift(steps, box => box.Left, all.Left, Axis.Horizontal),
            Edge.Center => Shift(steps, box => box.Center.X, all.Center.X, Axis.Horizontal),
            Edge.Right => Shift(steps, box => box.Right, all.Right, Axis.Horizontal),
            Edge.Top => Shift(steps, box => box.Top, all.Top, Axis.Vertical),
            Edge.Middle => Shift(steps, box => box.Center.Y, all.Center.Y, Axis.Vertical),
            _ => Shift(steps, box => box.Bottom, all.Bottom, Axis.Vertical),
        };
    }

    /// <summary>Gives the gaps between steps one size. The first and last step stay where they are. It needs three steps.</summary>
    public static IReadOnlyList<Move> Distribute(IReadOnlyList<Placed> steps, Axis axis)
    {
        if (steps.Count < 3)
        {
            return [];
        }

        var ordered = Ordered(steps, axis);
        var first = Start(ordered[0].Bounds, axis);
        var end = ordered.Max(step => Start(step.Bounds, axis) + Size(step.Bounds, axis));
        var gap = (end - first - ordered.Sum(step => Size(step.Bounds, axis))) / (ordered.Count - 1);
        return Sequence(ordered, first, gap, axis, null);
    }

    /// <summary>Puts the steps in a row or a column from the first one, one gap apart, on the line of the first edge they share.</summary>
    public static IReadOnlyList<Move> Stack(IReadOnlyList<Placed> steps, Axis axis)
    {
        if (steps.Count < 2)
        {
            return [];
        }

        var ordered = Ordered(steps, axis);
        var cross = steps.Min(step => Cross(step.Bounds, axis));
        return Sequence(ordered, Start(ordered[0].Bounds, axis), Gap, axis, cross);
    }

    /// <summary>
    /// Moves steps up or down until the pin a wire ends at is level with the pin it starts from. The links come left
    /// to right, so a chain of steps follows the first. A step takes the height of its first wire.
    /// </summary>
    public static IReadOnlyList<Move> Straighten(IReadOnlyList<Link> links)
    {
        var shift = new Dictionary<int, double>();
        foreach (var link in links)
        {
            if (!shift.ContainsKey(link.To))
            {
                shift[link.To] = link.FromY + shift.GetValueOrDefault(link.From) - link.ToY;
            }
        }

        return [.. shift.Where(pair => pair.Value != 0).Select(pair => new Move(pair.Key, new Vector(0, pair.Value)))];
    }

    /// <summary>The steps joined to the start by wires, in either direction, and the start itself.</summary>
    public static IReadOnlyCollection<int> Reach(IEnumerable<int> start, IEnumerable<(int From, int To)> wires)
    {
        var next = wires.SelectMany(wire => new (int From, int To)[] { wire, (wire.To, wire.From) }).ToLookup(wire => wire.From, wire => wire.To);
        var seen = new HashSet<int>(start);
        var queue = new Queue<int>(seen);
        while (queue.Count > 0)
        {
            foreach (var other in next[queue.Dequeue()].Where(seen.Add))
            {
                queue.Enqueue(other);
            }
        }

        return seen;
    }

    static List<Placed> Ordered(IReadOnlyList<Placed> steps, Axis axis) => [.. steps.OrderBy(step => Start(step.Bounds, axis)).ThenBy(step => step.Id)];

    static double Start(Rect box, Axis axis) => axis == Axis.Horizontal ? box.Left : box.Top;

    static double Size(Rect box, Axis axis) => axis == Axis.Horizontal ? box.Width : box.Height;

    static double Cross(Rect box, Axis axis) => axis == Axis.Horizontal ? box.Top : box.Left;

    static Vector Along(double along, double across, Axis axis) => axis == Axis.Horizontal ? new Vector(along, across) : new Vector(across, along);

    static List<Move> Shift(IReadOnlyList<Placed> steps, Func<Rect, double> edgeOf, double target, Axis axis) =>
        [.. steps.Select(step => new Move(step.Id, Along(target - edgeOf(step.Bounds), 0, axis))).Where(move => move.By != default)];

    // The steps go one after the other from the start, and a cross line, when there is one, puts them on one line.
    static List<Move> Sequence(List<Placed> ordered, double start, double gap, Axis axis, double? cross)
    {
        var moves = new List<Move>();
        var at = start;
        foreach (var step in ordered)
        {
            var by = Along(at - Start(step.Bounds, axis), cross is { } line ? line - Cross(step.Bounds, axis) : 0, axis);
            if (by != default)
            {
                moves.Add(new Move(step.Id, by));
            }

            at += Size(step.Bounds, axis) + gap;
        }

        return moves;
    }
}
