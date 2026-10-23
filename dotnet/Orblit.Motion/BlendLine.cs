using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>One place along a <see cref="BlendLine"/>, and what plays there.</summary>
/// <param name="At">Where along the line's input.</param>
/// <param name="Plays">What plays there.</param>
public sealed record LinePoint(double At, BlendSource Plays);

/// <summary>
/// Clips along one input: a walk at one speed and a run at another, and between them a mix of the two in
/// proportion.
/// </summary>
/// <remarks>
/// Below the first point is the first point, and above the last is the last, so a speed past the fastest clip
/// runs as the fastest clip does rather than as something nobody made.
/// </remarks>
public sealed class BlendLine : BlendSource
{
    public BlendLine(string input, IReadOnlyList<LinePoint> points)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("A line needs a point.", nameof(points));
        }

        foreach (var point in points)
        {
            if (point.Plays is BlendGraph)
            {
                throw new ArgumentException("A nested graph belongs in a state, not a line point.", nameof(points));
            }

            if (!double.IsFinite(point.At))
            {
                throw new ArgumentException("A point is somewhere.", nameof(points));
            }
        }

        Input = input;
        Points = points.OrderBy(static point => point.At, Comparer<double>.Create(Doubles.Compare)).ToList().AsReadOnly();
    }

    /// <summary>The input it runs along.</summary>
    public string Input { get; }

    /// <summary>
    /// In order along the line. Two at one place are a step: the earlier in the list has everything below, and
    /// the later everything from there up.
    /// </summary>
    public IReadOnlyList<LinePoint> Points { get; }

    public override IEnumerable<string> Clips => Points.SelectMany(static point => point.Plays.Clips);

    public override IEnumerable<string> Inputs => Points.SelectMany(static point => point.Plays.Inputs).Prepend(Input).Distinct();

    public override OrderedDictionary<string, object?> ToJson()
    {
        var points = new List<object?>();
        foreach (var point in Points)
        {
            var written = new OrderedDictionary<string, object?> { ["at"] = point.At };
            foreach (var (key, value) in point.Plays.ToJson())
            {
                written[key] = value;
            }

            points.Add(written);
        }

        return new() { ["line"] = new OrderedDictionary<string, object?> { ["input"] = Input, ["points"] = points } };
    }

    internal static BlendLine? Read(JsonMap raw, string subject, List<string> problems)
    {
        if (Values.Text(raw, "input") is not { Length: > 0 } input)
        {
            problems.Add($"The line in {subject} runs along no input.");
            return null;
        }

        var points = new List<LinePoint>();
        foreach (var entry in raw.GetValueOrDefault("points") as JsonList ?? Array.Empty<object?>())
        {
            if (ReadPoint(entry, subject, problems) is { } point)
            {
                points.Add(point);
            }
        }

        if (points.Count == 0)
        {
            problems.Add($"The line in {subject} has no points.");
            return null;
        }

        return new BlendLine(input, points);
    }

    internal override void AddWeights(Func<string, double> read, double scale, OrderedDictionary<string, double> into)
    {
        var value = read(Input);
        if (Points.Count == 1 || !(value >= Points[0].At))
        {
            Points[0].Plays.AddWeights(read, scale, into);
            return;
        }

        if (value >= Points[^1].At)
        {
            Points[^1].Plays.AddWeights(read, scale, into);
            return;
        }

        for (var i = 0; i + 1 < Points.Count; i++)
        {
            var (low, high) = (Points[i], Points[i + 1]);
            if (value >= high.At)
            {
                continue;
            }

            var along = (value - low.At) / (high.At - low.At);
            low.Plays.AddWeights(read, scale * (1 - along), into);
            high.Plays.AddWeights(read, scale * along, into);
            return;
        }
    }

    private static LinePoint? ReadPoint(object? entry, string subject, List<string> problems)
    {
        if (entry is not JsonMap map || Values.MaybeNumber(map, "at") is not { } at || !double.IsFinite(at))
        {
            problems.Add($"A point on the line in {subject} is at no number, so it was left out.");
            return null;
        }

        var plays = FromJson(map, $"the point at {Doubles.ToFileString(at)} on the line in {subject}", problems);
        if (plays is BlendGraph)
        {
            problems.Add($"A nested graph in {subject} belongs in a state, so the point was left out.");
            return null;
        }

        return plays is null ? null : new LinePoint(at, plays);
    }
}
