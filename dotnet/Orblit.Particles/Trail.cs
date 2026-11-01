using System.Collections.ObjectModel;
using Orblit.VectorMath;

namespace Orblit.Particles;

/// <summary>
/// Where a point is at a time, for a path that is known: a sequence, a ballistic
/// arc, anything that can be asked.
/// </summary>
public delegate Vector3 PathAt(double time);

/// <summary>
/// The points of a trail at one moment, newest first, as flat arrays a renderer
/// can read without unpacking.
/// </summary>
public sealed class TrailStrip
{
    public TrailStrip(int capacity)
    {
        Require.That(capacity >= 2, nameof(capacity), capacity, "must be 2 or more");
        Capacity = capacity;
        Positions = new double[capacity * 3];
        Widths = new float[capacity];
    }

    public int Capacity { get; }

    /// <summary>
    /// x, y, z per point. Doubles, because a strip far from the origin is turned
    /// into offsets from its head and single precision would step.
    /// </summary>
    public double[] Positions { get; }

    /// <summary>Width per point, in metres.</summary>
    public float[] Widths { get; }

    /// <summary>How many slots hold a point.</summary>
    public int Count { get; set; }

    /// <summary>The newest point, or the origin when the strip is empty.</summary>
    public Vector3 Head => Count == 0 ? Vector3.Zero() : new Vector3(Positions[0], Positions[1], Positions[2]);

    internal void Add(Vector3 position, double width)
    {
        VectorArrays.Write(position, Positions, Count * 3);
        Widths[Count++] = (float)width;
    }
}

/// <summary>
/// A strip that follows a point: a sword swing, a wheel mark, a streak behind
/// something fast. This holds the settings and nothing else.
/// </summary>
/// <remarks>
/// It tapers by a width ramp over the <see cref="Life"/>, so a width that ends at
/// 0 is how the tail fades, since nothing here has alpha or a colour that
/// changes.
/// </remarks>
public sealed class Trail
{
    /// <summary>
    /// Most points a strip can have. A skin holds 256 joints and a point takes
    /// two of them, one for each edge.
    /// </summary>
    public const int MaxPoints = 128;

    public Trail(double life = 0.5, double rate = 60, IEnumerable<double>? width = null)
    {
        Life = life;
        Rate = rate;
        Width = new ReadOnlyCollection<double>([.. width ?? [0.2, 0]]);
        Require.That(life > 0 && double.IsFinite(life), nameof(life), life, "must be above 0");
        Require.That(rate > 0 && double.IsFinite(rate), nameof(rate), rate, "must be above 0");
        Require.That(
            Width.Count > 0 && Width.All(w => w >= 0 && double.IsFinite(w)),
            nameof(width),
            Width,
            "needs one or more widths of 0 or more");
        Require.That(
            life * rate <= MaxPoints - 1,
            nameof(life),
            life,
            $"times a rate of {rate} must be {MaxPoints} points or fewer");
    }

    /// <summary>Seconds a point stays in the strip.</summary>
    public double Life { get; }

    /// <summary>Samples a second.</summary>
    public double Rate { get; }

    /// <summary>Width over the life, spread evenly from the head to the tail.</summary>
    public IReadOnlyList<double> Width { get; }

    /// <summary>Points a strip of this trail holds at most: one for each sample in the life, and the head.</summary>
    public int Points => (int)Doubles.Ceil(Life * Rate) + 1;

    /// <summary>
    /// The seconds between points. A <see cref="Rate"/> that does not divide the
    /// life into whole samples gives a slightly shorter step, so both ways of
    /// filling a strip use this one.
    /// </summary>
    internal double Step => Life / (Points - 1);

    /// <summary>
    /// Fills <paramref name="output"/> with the strip at <paramref name="time"/>,
    /// from a path that can be asked for any time. Nothing is kept, so a seek
    /// gives the same strip and the strip never jumps. The path is not asked for
    /// a time before 0.
    /// </summary>
    public void SampleAlong(double time, PathAt path, TrailStrip output)
    {
        output.Count = 0;
        if (!(time >= 0 && double.IsFinite(time)))
        {
            return;
        }

        var step = Step;
        var count = Math.Min(Points, output.Capacity);
        for (var i = 0; i < count && time - (i * step) >= 0; i++)
        {
            output.Add(path(time - (i * step)), Ramp.At(Width, 1, 0, i * step / Life));
        }
    }
}

/// <summary>
/// The history of a <see cref="Trail"/> whose path is not known ahead, such as a
/// physics body. Call <see cref="Record"/> each frame with the time and where the
/// point is.
/// </summary>
/// <remarks>
/// Time going backwards, or a step longer than the whole life, clears it, since
/// what it kept belongs to a different run. A scrub forward that is shorter than
/// the life cannot be told from a slow frame, so call <see cref="Clear"/> on a
/// seek you know of.
/// </remarks>
public sealed class TrailRing(Trail trail)
{
    // Oldest first.
    private readonly List<(double Time, Vector3 Position)> _kept = [];
    private readonly Vector3 _here = Vector3.Zero();
    private double _now = double.NaN;

    public Trail Trail { get; } = trail;

    public void Record(double time, Vector3 position)
    {
        if (!(time >= 0 && double.IsFinite(time)))
        {
            return;
        }

        if (time < _now || time - _now > Trail.Life)
        {
            _kept.Clear();
        }

        _now = time;
        _here.SetFrom(position);

        // A frame clock at the trail's own rate lands a rounding error short of
        // one step, so allow for it.
        if (_kept.Count == 0 || time - _kept[^1].Time >= Trail.Step * (1 - 1e-6))
        {
            _kept.Add((time, position.Clone()));
        }

        var expired = _kept.FindIndex(s => time - s.Time <= Trail.Life);
        _kept.RemoveRange(0, expired < 0 ? _kept.Count : expired);
    }

    public void Clear()
    {
        _kept.Clear();
        _now = double.NaN;
    }

    /// <summary>Fills <paramref name="output"/> with the strip as of the last <see cref="Record"/>.</summary>
    public void Fill(TrailStrip output)
    {
        output.Count = 0;
        if (double.IsNaN(_now))
        {
            return;
        }

        output.Add(_here, Ramp.At(Trail.Width, 1, 0, 0));
        for (var i = _kept.Count - 1; i >= 0 && output.Count < output.Capacity; i--)
        {
            var age = _now - _kept[i].Time;
            if (age <= 0)
            {
                continue;
            }

            output.Add(_kept[i].Position, Ramp.At(Trail.Width, 1, 0, age / Trail.Life));
        }
    }
}
