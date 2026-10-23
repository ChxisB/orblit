using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>One place on a <see cref="BlendPlane"/>, and what plays there.</summary>
/// <param name="X">Where, in the plane's <see cref="BlendPlane.X"/> input.</param>
/// <param name="Y">Where, in the plane's <see cref="BlendPlane.Y"/> input.</param>
/// <param name="Plays">What plays there.</param>
public sealed record PlanePoint(double X, double Y, BlendSource Plays);

/// <summary>
/// Clips over two inputs: walks forwards, backwards and to either side, with the direction of travel choosing
/// between them.
/// </summary>
/// <remarks>
/// The points go anywhere, in no pattern. Each has everything at its own place, and its say falls away
/// towards each of the others along the line between the two, so the mix is smooth everywhere, exact at every
/// point, and settles on the nearest points outside them all. The two inputs are measured against each
/// other, so they want to be in comparable units: a velocity's two parts, not a speed and an angle.
/// </remarks>
public sealed class BlendPlane : BlendSource
{
    public BlendPlane(string x, string y, IReadOnlyList<PlanePoint> points)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("A plane needs a point.", nameof(points));
        }

        foreach (var point in points)
        {
            if (point.Plays is BlendGraph)
            {
                throw new ArgumentException("A nested graph belongs in a state, not a plane point.", nameof(points));
            }

            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            {
                throw new ArgumentException("A point on a plane is somewhere.", nameof(points));
            }
        }

        X = x;
        Y = y;
        Points = points.ToList().AsReadOnly();
    }

    /// <summary>The input across.</summary>
    public string X { get; }

    /// <summary>The input up.</summary>
    public string Y { get; }

    public IReadOnlyList<PlanePoint> Points { get; }

    public override IEnumerable<string> Clips => Points.SelectMany(static point => point.Plays.Clips);

    public override IEnumerable<string> Inputs => Points.SelectMany(static point => point.Plays.Inputs).Prepend(Y).Prepend(X).Distinct();

    /// <summary>How much say each point has at (<paramref name="atX"/>, <paramref name="atY"/>), adding to one.</summary>
    public IReadOnlyList<double> WeightsAt(double atX, double atY)
    {
        var weights = new double[Points.Count];
        var total = 0.0;
        for (var i = 0; i < Points.Count; i++)
        {
            weights[i] = SayOf(i, atX, atY);
            total += weights[i];
        }

        if (total > 0)
        {
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] /= total;
            }

            return weights;
        }

        // Nothing has a say only where every point is shadowed by another, which rounding can make true at
        // a corner. The nearest has it all.
        weights[Nearest(atX, atY)] = 1;
        return weights;
    }

    public override OrderedDictionary<string, object?> ToJson()
    {
        var points = new List<object?>();
        foreach (var point in Points)
        {
            var written = new OrderedDictionary<string, object?> { ["at"] = new List<object?> { point.X, point.Y } };
            foreach (var (key, value) in point.Plays.ToJson())
            {
                written[key] = value;
            }

            points.Add(written);
        }

        return new() { ["plane"] = new OrderedDictionary<string, object?> { ["x"] = X, ["y"] = Y, ["points"] = points } };
    }

    internal static BlendPlane? Read(JsonMap raw, string subject, List<string> problems)
    {
        if (Values.Text(raw, "x") is not { Length: > 0 } x || Values.Text(raw, "y") is not { Length: > 0 } y)
        {
            problems.Add($"The plane in {subject} needs an input across and one up.");
            return null;
        }

        var points = new List<PlanePoint>();
        foreach (var entry in raw.GetValueOrDefault("points") as JsonList ?? Array.Empty<object?>())
        {
            if (ReadPoint(entry, subject, problems) is { } point)
            {
                points.Add(point);
            }
        }

        if (points.Count == 0)
        {
            problems.Add($"The plane in {subject} has no points.");
            return null;
        }

        return new BlendPlane(x, y, points);
    }

    internal override void AddWeights(Func<string, double> read, double scale, OrderedDictionary<string, double> into)
    {
        var weights = WeightsAt(read(X), read(Y));
        for (var i = 0; i < Points.Count; i++)
        {
            if (weights[i] > 0)
            {
                Points[i].Plays.AddWeights(read, scale * weights[i], into);
            }
        }
    }

    private static PlanePoint? ReadPoint(object? entry, string subject, List<string> problems)
    {
        if (entry is not JsonMap map || Across(map) is not { } at)
        {
            problems.Add($"A point on the plane in {subject} is not at two numbers, so it was left out.");
            return null;
        }

        var (atX, atY) = at;
        var where = $"the point at ({Doubles.ToFileString(atX)}, {Doubles.ToFileString(atY)}) on the plane in {subject}";
        var plays = FromJson(map, where, problems);
        if (plays is BlendGraph)
        {
            problems.Add($"A nested graph in {subject} belongs in a state, so the point was left out.");
            return null;
        }

        return plays is null ? null : new PlanePoint(atX, atY, plays);
    }

    // The two finite numbers a point is written at, or null when it is written at anything else.
    private static (double X, double Y)? Across(JsonMap map)
    {
        if (map.GetValueOrDefault("at") is not JsonList { Count: 2 } at
            || Values.Numbers(at) is not { Count: 2 } numbers
            || !double.IsFinite(numbers[0])
            || !double.IsFinite(numbers[1]))
        {
            return null;
        }

        return (numbers[0], numbers[1]);
    }

    // The say of point i: the least that any other point's shadow leaves it, nought to one.
    private double SayOf(int i, double atX, double atY)
    {
        var from = Points[i];
        var toX = atX - from.X;
        var toY = atY - from.Y;
        var weight = 1.0;
        for (var j = 0; j < Points.Count && weight > 0; j++)
        {
            var acrossX = Points[j].X - from.X;
            var acrossY = Points[j].Y - from.Y;
            var length2 = (acrossX * acrossX) + (acrossY * acrossY);

            // Two points in one place share it.
            if (j == i || length2 < 1e-12)
            {
                continue;
            }

            var along = ((toX * acrossX) + (toY * acrossY)) / length2;
            weight = Math.Min(weight, Doubles.Clamp(1 - along, 0, 1));
        }

        return weight;
    }

    private int Nearest(double atX, double atY)
    {
        var nearest = 0;
        var best = double.PositiveInfinity;
        for (var i = 0; i < Points.Count; i++)
        {
            var dx = atX - Points[i].X;
            var dy = atY - Points[i].Y;
            var distance = (dx * dx) + (dy * dy);
            if (distance < best)
            {
                best = distance;
                nearest = i;
            }
        }

        return nearest;
    }
}
