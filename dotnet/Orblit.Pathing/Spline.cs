using System.Collections.ObjectModel;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>
/// A curve through its knots, measured in metres so it can be walked by
/// distance and not by a parameter that bunches up at every tight bend.
/// Made once and read many times: the table that turns a distance into a
/// place is built the first time it is asked for.
/// </summary>
public sealed class Spline
{
    public const string Marker = "orblit.path";

    public const int FormatVersion = 1;

    /// <summary>
    /// Samples between two knots when measuring. A bend is followed to
    /// within a few millimetres at a knot spacing of a few metres.
    /// </summary>
    private const int Steps = 64;

    private readonly Controls[] _controls;
    private readonly Lazy<Table> _table;

    private Spline(IReadOnlyList<PathKnot> knots, bool closed)
    {
        Knots = new ReadOnlyCollection<PathKnot>(knots.ToArray());
        if (Knots.Count < 2)
        {
            throw new ArgumentException("A spline needs at least two knots.", nameof(knots));
        }

        Closed = closed;
        _controls = Enumerable.Range(0, Segments).Select(ControlsOf).ToArray();
        _table = new Lazy<Table>(BuildTable);
    }

    public IReadOnlyList<PathKnot> Knots { get; }

    /// <summary>Whether the last knot runs back round to the first.</summary>
    public bool Closed { get; }

    /// <summary>How long the curve is, in metres.</summary>
    public double Length => _table.Value.Along[^1];

    private int Segments => Closed ? Knots.Count : Knots.Count - 1;

    /// <summary>A cubic Bezier through <paramref name="knots"/>, bent by their handles.</summary>
    public static Spline Bezier(IReadOnlyList<PathKnot> knots, bool closed = false) => new(knots, closed);

    /// <summary>
    /// A smooth curve through <paramref name="points"/> that leaves each one in the direction
    /// of its neighbours. <paramref name="tilts"/> is one angle a point.
    /// Each handle is a third of the way to the neighbour it faces, so a
    /// short gap next to a long one bends gently and does not loop.
    /// </summary>
    public static Spline Through(IReadOnlyList<Vector3> points, IReadOnlyList<double>? tilts = null, bool closed = false)
    {
        var angles = TiltsFor(points, tilts);
        return Bezier(Enumerable.Range(0, points.Count).Select(i => ThroughKnot(points, angles[i], i, closed)).ToArray(), closed);
    }

    /// <summary>Straight lines between <paramref name="points"/>, with a corner at each.</summary>
    public static Spline Line(IReadOnlyList<Vector3> points, IReadOnlyList<double>? tilts = null, bool closed = false)
    {
        var angles = TiltsFor(points, tilts);
        return Bezier(Enumerable.Range(0, points.Count).Select(i => new PathKnot(points[i], angles[i])).ToArray(), closed);
    }

    /// <summary>A spline out of a file's text.</summary>
    /// <exception cref="PathFormatException">
    /// The text is not a path, is from a newer Orblit, or has a knot that is not made of numbers.
    /// </exception>
    public static Spline Decode(string text)
    {
        var header = ReadHeader(text);
        if (header.GetValueOrDefault("knots") is not IReadOnlyList<object?> raw || raw.Count < 2)
        {
            throw new PathFormatException("A path needs at least two knots.");
        }

        var knots = new List<PathKnot>();
        for (var i = 0; i < raw.Count; i++)
        {
            knots.Add(KnotFrom(raw[i], i));
        }

        return Bezier(knots, header.GetValueOrDefault("closed") is true);
    }

    /// <summary>The place <paramref name="distance"/> metres along the curve, and which way it faces.</summary>
    public PathFrame FrameAt(double distance)
    {
        var at = Locate(distance);
        var forward = ForwardAt(at.Segment, at.T);
        var from = Knots[at.Segment].Tilt;
        var to = Knots[(at.Segment + 1) % Knots.Count].Tilt;
        return new PathFrame(OnTable(at), forward, UpFor(forward, from + ((to - from) * at.T)));
    }

    public Vector3 PositionAt(double distance) => OnTable(Locate(distance));

    /// <summary>The point of the curve nearest <paramref name="point"/>, and how far along it is.</summary>
    public PathNearest Closest(Vector3 point)
    {
        var table = _table.Value;
        var best = double.PositiveInfinity;
        var bestDistance = 0.0;
        for (var k = 0; k + 1 < table.Along.Length; k++)
        {
            var a = table.PointAt(k);
            var edge = table.PointAt(k + 1) - a;
            var run = edge.Length2;
            var u = run < 1e-18 ? 0.0 : Doubles.Clamp((point - a).Dot(edge) / run, 0.0, 1.0);
            var gap = point.DistanceToSquared(a + (edge * u));
            if (gap < best)
            {
                best = gap;
                bestDistance = table.Along[k] + (u * (table.Along[k + 1] - table.Along[k]));
            }
        }

        var position = PositionAt(bestDistance);
        return new PathNearest(bestDistance, position, point.DistanceTo(position));
    }

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["kind"] = Marker,
        ["formatVersion"] = FormatVersion,
        ["closed"] = Closed,
        ["knots"] = Knots.Select(knot => (object?)KnotJson(knot)).ToList(),
    };

    /// <summary>
    /// The file's text, a knot to a line so the diff of two versions is the
    /// knots that moved.
    /// </summary>
    public string Encode()
    {
        var lines = Knots.Select(knot => "    " + DartJson.Encode(KnotJson(knot)));
        return "{\n"
            + $"  \"kind\": {DartJson.Encode(Marker)},\n"
            + $"  \"formatVersion\": {FormatVersion},\n"
            + $"  \"closed\": {(Closed ? "true" : "false")},\n"
            + $"  \"knots\": [\n{string.Join(",\n", lines)}\n  ]\n"
            + "}\n";
    }

    private static double[] TiltsFor(IReadOnlyList<Vector3> points, IReadOnlyList<double>? tilts)
    {
        if (tilts is not null && tilts.Count != points.Count)
        {
            throw new ArgumentException($"Needs one angle for each of the {points.Count} points.", nameof(tilts));
        }

        return tilts?.ToArray() ?? new double[points.Count];
    }

    private static PathKnot ThroughKnot(IReadOnlyList<Vector3> points, double tilt, int i, bool closed)
    {
        var count = points.Count;
        Vector3 Neighbour(int at) => points[closed ? (((at % count) + count) % count) : Math.Clamp(at, 0, count - 1)];
        var previous = Neighbour(i - 1);
        var next = Neighbour(i + 1);
        var heading = next - previous;
        var way = heading.Length2 > 1e-18 ? heading.Normalized() : Vector3.Zero();
        return new PathKnot(
            points[i],
            tilt,
            handleIn: way * (-points[i].DistanceTo(previous) / 3),
            handleOut: way * (points[i].DistanceTo(next) / 3));
    }

    private static Vector3 UpFor(Vector3 forward, double tilt)
    {
        var up = new Vector3(0, 1, 0) - (forward * forward.Y);
        if (up.Length2 < 1e-9)
        {
            up = new Vector3(0, 0, -1) - (forward * forward.Z);
        }

        up.Normalize();
        var rolled = (up * Math.Cos(tilt)) + (forward.Cross(up) * Math.Sin(tilt));
        rolled.Normalize();
        return rolled;
    }

    private static OrderedDictionary<string, object?> KnotJson(PathKnot knot)
    {
        var json = new OrderedDictionary<string, object?> { ["at"] = Numbers(knot.PositionRef) };
        if (knot.Tilt != 0)
        {
            json["tilt"] = knot.Tilt;
        }

        if (knot.InRef.Length2 > 0)
        {
            json["in"] = Numbers(knot.InRef);
        }

        if (knot.OutRef.Length2 > 0)
        {
            json["out"] = Numbers(knot.OutRef);
        }

        return json;
    }

    private static List<object?> Numbers(Vector3 v) => [v.X, v.Y, v.Z];

    private static IReadOnlyDictionary<string, object?> ReadHeader(string text)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(text);
        }
        catch (FormatException error)
        {
            throw new PathFormatException($"This is not a path file: {error.Message}");
        }

        if (parsed is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("kind") is not Marker)
        {
            throw new PathFormatException("This is not a path file.");
        }

        // A whole number reads as a long. 2.0 is a double, so it is no version.
        if (map.GetValueOrDefault("formatVersion") is long version && version > FormatVersion)
        {
            throw new PathFormatException("This path was written by a newer Orblit.");
        }

        return map;
    }

    private static PathKnot KnotFrom(object? raw, int index)
    {
        if (raw is not IReadOnlyDictionary<string, object?> map)
        {
            throw new PathFormatException($"Knot {index} is not an object.");
        }

        var at = VectorFrom(map.GetValueOrDefault("at"), "at", index)
            ?? throw new PathFormatException($"Knot {index} does not say where it is.");
        var tilt = map.GetValueOrDefault("tilt");
        if (tilt is not null && !IsFiniteNumber(tilt))
        {
            throw new PathFormatException($"Knot {index} has a tilt that is not a number.");
        }

        return new PathKnot(
            at,
            tilt is null ? 0 : AsDouble(tilt),
            handleIn: VectorFrom(map.GetValueOrDefault("in"), "in", index),
            handleOut: VectorFrom(map.GetValueOrDefault("out"), "out", index));
    }

    private static Vector3? VectorFrom(object? raw, string key, int index)
    {
        if (raw is null)
        {
            return null;
        }

        if (raw is not IReadOnlyList<object?> { Count: 3 } list || !list.All(IsFiniteNumber))
        {
            throw new PathFormatException($"Knot {index} has \"{key}\" that is not x, y, z.");
        }

        return new Vector3(AsDouble(list[0]), AsDouble(list[1]), AsDouble(list[2]));
    }

    private static bool IsFiniteNumber(object? value) => value is long or double && double.IsFinite(AsDouble(value));

    private static double AsDouble(object? value) => value is long whole ? whole : (double)value!;

    private Controls ControlsOf(int segment)
    {
        var from = Knots[segment];
        var to = Knots[(segment + 1) % Knots.Count];
        return new Controls(
            from.PositionRef,
            from.PositionRef + from.OutRef,
            to.PositionRef + to.InRef,
            to.PositionRef);
    }

    /// <summary>Distance along the curve at each sample, and the sample's position as three numbers.</summary>
    private Table BuildTable()
    {
        var count = (Segments * Steps) + 1;
        var along = new double[count];
        var points = new double[count * 3];
        var previous = PointAt(0, 0);
        for (var k = 0; k < count; k++)
        {
            var segment = Math.Min(k / Steps, Segments - 1);
            var here = PointAt(segment, (double)(k - (segment * Steps)) / Steps);
            if (k > 0)
            {
                along[k] = along[k - 1] + here.DistanceTo(previous);
            }

            points[k * 3] = here.X;
            points[(k * 3) + 1] = here.Y;
            points[(k * 3) + 2] = here.Z;
            previous = here;
        }

        return new Table(along, points);
    }

    private Vector3 PointAt(int segment, double t)
    {
        var c = _controls[segment];
        var u = 1 - t;
        return (c.A * (u * u * u))
            + (c.B * (3 * u * u * t))
            + (c.C * (3 * u * t * t))
            + (c.D * (t * t * t));
    }

    private Vector3 SlopeAt(int segment, double t)
    {
        var c = _controls[segment];
        var u = 1 - t;
        return ((c.B - c.A) * (3 * u * u))
            + ((c.C - c.B) * (6 * u * t))
            + ((c.D - c.C) * (3 * t * t));
    }

    /// <summary>
    /// The segment and parameter at <paramref name="distance"/>, taken as it is on a closed
    /// curve and clamped to the ends on an open one. Sample and part say
    /// where it falls between two samples of the table.
    /// </summary>
    private Place Locate(double distance)
    {
        var along = _table.Value.Along;
        var total = along[^1];
        var d = distance;
        if (Closed && total > 0)
        {
            d = Doubles.Mod(distance, total);
        }

        d = Doubles.Clamp(d, 0.0, total);
        var low = 0;
        var high = along.Length - 1;
        while (high - low > 1)
        {
            var mid = (low + high) >> 1;
            if (along[mid] <= d)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        var span = along[low + 1] - along[low];
        var part = span <= 1e-12 ? 0.0 : (d - along[low]) / span;
        var global = low + part;
        // The division is by a power of two, so it is exact, and a NaN throws as Dart's ~/ does.
        var segment = (int)Math.Min(Doubles.Floor(global / Steps), Segments - 1);
        return new Place(segment, (global - (segment * Steps)) / Steps, low, part);
    }

    /// <summary>
    /// The point of the table's polyline at <paramref name="at"/>. The table is cut finely
    /// enough that this is within a hair of the curve, and unlike the curve
    /// it moves the same distance for the same part of a sample.
    /// </summary>
    private Vector3 OnTable(Place at)
    {
        var table = _table.Value;
        var a = table.PointAt(at.Sample);
        return a + ((table.PointAt(at.Sample + 1) - a) * at.Part);
    }

    private Vector3 ForwardAt(int segment, double t)
    {
        var slope = SlopeAt(segment, t);
        if (slope.Length2 > 1e-12)
        {
            return slope.Normalized();
        }

        var c = _controls[segment];
        var chord = c.D - c.A;
        return chord.Length2 > 1e-12 ? chord.Normalized() : new Vector3(0, 0, -1);
    }

    private readonly record struct Controls(Vector3 A, Vector3 B, Vector3 C, Vector3 D);

    private readonly record struct Place(int Segment, double T, int Sample, double Part);

    private sealed record Table(double[] Along, double[] Points)
    {
        internal Vector3 PointAt(int sample) => new(Points[sample * 3], Points[(sample * 3) + 1], Points[(sample * 3) + 2]);
    }
}
