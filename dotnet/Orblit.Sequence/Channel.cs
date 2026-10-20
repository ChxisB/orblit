using Orblit.VectorMath;

namespace Orblit.Sequence;

/// <summary>How a value gets from one keyframe to the next.</summary>
public enum Hold
{
    /// <summary>It does not. The value jumps at the second key and holds until the one after.</summary>
    Step,

    /// <summary>Straight there, at a constant rate. Corners at every key.</summary>
    Linear,

    /// <summary>Eased at both ends of each span, so the value arrives and leaves at rest. The default.</summary>
    Smooth,

    /// <summary>Eased with the shape the key names, so one span can snap and the next can settle.</summary>
    Shaped,

    /// <summary>
    /// Along a curve that leaves this key, and arrives at the next, at the
    /// slopes they name. A key that names none has one worked out from its
    /// neighbours. The first and last keys, and any peak or trough, are left at
    /// rest. A value that cannot curve, a flag, travels as <see cref="Linear"/> does.
    /// </summary>
    Curve,
}

/// <summary>
/// A slope named on a key. Dart writes it as a nullable <c>T</c>. Here a slope
/// of zero and no slope at all have to differ, and <c>T?</c> does not say that
/// for a value type.
/// </summary>
public readonly record struct Slope<T>(T Value)
{
    public static implicit operator Slope<T>(T value) => new(value);
}

/// <summary>One value at one moment.</summary>
public sealed class Key<T>
{
    // F2: six parameters, and the four after the value are optional and called by name, as in Dart.
    public Key(
        double at,
        T value,
        Hold hold = Hold.Smooth,
        Easing? shape = null,
        Slope<T>? slopeIn = null,
        Slope<T>? slopeOut = null)
    {
        At = at;
        Value = value;
        Hold = hold;
        Shape = shape;
        SlopeIn = slopeIn;
        SlopeOut = slopeOut;
    }

    /// <summary>Seconds from the start of the sequence.</summary>
    public double At { get; }

    public T Value { get; }

    /// <summary>
    /// How the value travels from this key to the next. Held on the earlier key,
    /// because a span belongs to the key that starts it.
    /// </summary>
    public Hold Hold { get; }

    /// <summary>Which easing <see cref="Hold.Shaped"/> uses. Ignored otherwise.</summary>
    public Easing? Shape { get; }

    /// <summary>
    /// How fast the value is changing as it arrives here, in units a second,
    /// when the span before this key is a curve. Null works one out.
    /// </summary>
    public Slope<T>? SlopeIn { get; }

    /// <summary>
    /// How fast it is changing as it leaves, when <see cref="Hold"/> is a curve.
    /// Separate from <see cref="SlopeIn"/> so a key can be a corner.
    /// </summary>
    public Slope<T>? SlopeOut { get; }
}

/// <summary>
/// How two values of one kind are mixed. Two rotations are mixed by turning
/// between them, not by averaging four numbers, and two flags are not mixed at all.
/// </summary>
public interface IMixer<T>
{
    /// <summary><paramref name="a"/> at nought, <paramref name="b"/> at one.</summary>
    T Lerp(T a, T b, double t);

    /// <summary>Several at once, each with a weight. Used where clips overlap.</summary>
    T Mix(IReadOnlyList<T> values, IReadOnlyList<double> weights);
}

/// <summary>A mixer for values that can travel along a curve: every kind here but a flag.</summary>
public interface ICurveMixer<T> : IMixer<T>
{
    /// <summary>A value that is not changing.</summary>
    T Still { get; }

    /// <summary>
    /// The slope a curve through <paramref name="at"/> should have there, given
    /// the keys either side of it and the seconds between them.
    /// </summary>
    T Through(T before, T at, T after, double seconds);

    /// <summary>
    /// The value <paramref name="t"/> of the way from <paramref name="a"/> to
    /// <paramref name="b"/> over <paramref name="seconds"/>, leaving at the
    /// slope <paramref name="leave"/> and arriving at <paramref name="arrive"/>.
    /// </summary>
    // F2: six parameters, the signature of the Dart curve, which has one caller.
    T Curve(T a, T leave, T b, T arrive, double seconds, double t);
}

internal static class CurveMath
{
    /// <summary>The four weights of a cubic Hermite curve at <paramref name="t"/>.</summary>
    internal static (double A, double Leave, double B, double Arrive) Hermite(double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return (
            (2 * t3) - (3 * t2) + 1,
            t3 - (2 * t2) + t,
            (-2 * t3) + (3 * t2),
            t3 - t2);
    }

    /// <summary>A peak or a trough is left flat. Anything else takes the line from its neighbours.</summary>
    internal static double Clamped(double before, double at, double after, double seconds)
    {
        if (((at - before) * (after - at)) <= 0)
        {
            return 0;
        }

        return seconds <= 0 ? 0 : (after - before) / seconds;
    }
}

public sealed class DoubleMixer : ICurveMixer<double>
{
    public double Still => 0;

    public double Through(double before, double at, double after, double seconds) =>
        CurveMath.Clamped(before, at, after, seconds);

    public double Curve(double a, double leave, double b, double arrive, double seconds, double t)
    {
        var w = CurveMath.Hermite(t);
        return (w.A * a) + (w.Leave * leave * seconds) + (w.B * b) + (w.Arrive * arrive * seconds);
    }

    public double Lerp(double a, double b, double t) => a + ((b - a) * t);

    public double Mix(IReadOnlyList<double> values, IReadOnlyList<double> weights)
    {
        var total = 0.0;
        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i] * weights[i];
            total += weights[i];
        }

        return total == 0 ? 0 : sum / total;
    }
}

public sealed class Vector3Mixer : ICurveMixer<Vector3>
{
    public Vector3 Still => Vector3.Zero();

    /// <summary>
    /// Worked out one axis at a time, so a key that is the top of a jump is flat
    /// in height without also stopping the run forwards.
    /// </summary>
    public Vector3 Through(Vector3 before, Vector3 at, Vector3 after, double seconds) => new(
        CurveMath.Clamped(before.X, at.X, after.X, seconds),
        CurveMath.Clamped(before.Y, at.Y, after.Y, seconds),
        CurveMath.Clamped(before.Z, at.Z, after.Z, seconds));

    public Vector3 Curve(Vector3 a, Vector3 leave, Vector3 b, Vector3 arrive, double seconds, double t)
    {
        var w = CurveMath.Hermite(t);
        return (a * w.A) + (leave * (w.Leave * seconds)) + (b * w.B) + (arrive * (w.Arrive * seconds));
    }

    public Vector3 Lerp(Vector3 a, Vector3 b, double t) => a + ((b - a) * t);

    public Vector3 Mix(IReadOnlyList<Vector3> values, IReadOnlyList<double> weights)
    {
        var sum = Vector3.Zero();
        var total = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i] * weights[i];
            total += weights[i];
        }

        return total == 0 ? Vector3.Zero() : sum / total;
    }
}

public sealed class QuaternionMixer : ICurveMixer<Quaternion>
{
    public Quaternion Still => new(0, 0, 0, 0);

    /// <summary>
    /// The line from the key before to the key after, both written the same way
    /// round as <paramref name="at"/>. Not flattened at peaks, because a
    /// rotation has no peak: it is the four numbers together that mean something.
    /// </summary>
    public Quaternion Through(Quaternion before, Quaternion at, Quaternion after, double seconds)
    {
        if (seconds <= 0)
        {
            return Still;
        }

        var from = Alike(at, before);
        var to = Alike(at, after);
        return new Quaternion(
            (to.X - from.X) / seconds,
            (to.Y - from.Y) / seconds,
            (to.Z - from.Z) / seconds,
            (to.W - from.W) / seconds);
    }

    /// <summary>
    /// Four numbers along four curves, then made a rotation again. What glTF
    /// specifies for a cubic spline on a rotation.
    /// </summary>
    public Quaternion Curve(Quaternion a, Quaternion leave, Quaternion b, Quaternion arrive, double seconds, double t)
    {
        // The short way round, as Lerp does, and the slope turned with it.
        var flip = Dot(a, b) < 0 ? -1.0 : 1.0;
        var w = CurveMath.Hermite(t);
        var m0 = w.Leave * seconds;
        var m1 = w.Arrive * seconds * flip;
        var bw = w.B * flip;
        var result = new Quaternion(
            (w.A * a.X) + (m0 * leave.X) + (bw * b.X) + (m1 * arrive.X),
            (w.A * a.Y) + (m0 * leave.Y) + (bw * b.Y) + (m1 * arrive.Y),
            (w.A * a.Z) + (m0 * leave.Z) + (bw * b.Z) + (m1 * arrive.Z),
            (w.A * a.W) + (m0 * leave.W) + (bw * b.W) + (m1 * arrive.W));
        if (result.Length2 == 0)
        {
            return a.Clone();
        }

        result.Normalize();
        return result;
    }

    private static double Dot(Quaternion a, Quaternion b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);

    /// <summary><paramref name="q"/>, negated if that is what it takes to be written the same way round as <paramref name="like"/>.</summary>
    private static Quaternion Alike(Quaternion like, Quaternion q) =>
        Dot(like, q) < 0 ? new Quaternion(-q.X, -q.Y, -q.Z, -q.W) : q;

    /// <summary>
    /// Round the arc from <paramref name="a"/> to <paramref name="b"/> at an even
    /// rate, as glTF specifies. A <paramref name="t"/> outside 0 to 1 carries on
    /// round the same arc, for a shape that overshoots.
    /// </summary>
    public Quaternion Lerp(Quaternion a, Quaternion b, double t)
    {
        // The short way round. Without the sign check a turn of a hundred and
        // eighty-one degrees goes the other hundred and seventy-nine.
        var to = Alike(a, b);
        var cosine = Doubles.Clamp(Dot(a, to), -1.0, 1.0);
        double wa;
        double wb;
        if (cosine > 0.9995)
        {
            // Too short an arc to divide by its sine, and short enough that the
            // four numbers blended are the arc to well under a thousandth of a degree.
            wa = 1 - t;
            wb = t;
        }
        else
        {
            var angle = Math.Acos(cosine);
            var sine = Math.Sin(angle);
            wa = Math.Sin((1 - t) * angle) / sine;
            wb = Math.Sin(t * angle) / sine;
        }

        var result = new Quaternion(
            (a.X * wa) + (to.X * wb),
            (a.Y * wa) + (to.Y * wb),
            (a.Z * wa) + (to.Z * wb),
            (a.W * wa) + (to.W * wb));
        result.Normalize();
        return result;
    }

    public Quaternion Mix(IReadOnlyList<Quaternion> values, IReadOnlyList<double> weights)
    {
        if (values.Count == 0)
        {
            return Quaternion.Identity();
        }

        // Accumulated pairwise rather than summed, because the weighted sum of
        // four quaternions is only a rotation by accident.
        var result = values[0];
        var carried = weights[0];
        for (var i = 1; i < values.Count; i++)
        {
            var total = carried + weights[i];
            if (total <= 0)
            {
                continue;
            }

            result = Lerp(result, values[i], weights[i] / total);
            carried = total;
        }

        return result;
    }
}

/// <summary>Flags do not blend. Whichever clip has the most say decides.</summary>
public sealed class BoolMixer : IMixer<bool>
{
    public bool Lerp(bool a, bool b, double t) => t < 0.5 ? a : b;

    public bool Mix(IReadOnlyList<bool> values, IReadOnlyList<double> weights)
    {
        var best = false;
        var most = -1.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (weights[i] > most)
            {
                most = weights[i];
                best = values[i];
            }
        }

        return best;
    }
}

/// <summary>Dart's four const mixers, <c>doubleMixer</c> and the rest.</summary>
public static class Mixers
{
    public static readonly DoubleMixer OfDouble = new();

    public static readonly Vector3Mixer OfVector3 = new();

    public static readonly QuaternionMixer OfQuaternion = new();

    public static readonly BoolMixer OfBool = new();
}

/// <summary>
/// A value over time: keys, and the rule for getting between them. Sampling is
/// a pure function of the moment asked for, which is what makes scrubbing
/// backwards give the same answer as playing forwards to the same place.
/// </summary>
public sealed class Channel<T>
{
    public Channel(IReadOnlyList<Key<T>> keys, IMixer<T> mixer)
    {
        if (keys.Count == 0)
        {
            throw new ArgumentException("a channel with no keys has no value", nameof(keys));
        }

        Keys = keys;
        Mixer = mixer;
    }

    /// <summary>
    /// In ascending order of <see cref="Key{T}.At"/>. Not sorted here: a channel
    /// is built once and sampled constantly.
    /// </summary>
    public IReadOnlyList<Key<T>> Keys { get; }

    public IMixer<T> Mixer { get; }

    public double Start => Keys[0].At;

    public double End => Keys[^1].At;

    /// <summary>
    /// The value at <paramref name="at"/>. Before the first key it is the first
    /// key's value and after the last it is the last's: a channel does not
    /// extrapolate, because a curve run off its end is a value nobody chose.
    /// </summary>
    public T At(double at)
    {
        if (at <= Keys[0].At)
        {
            return Keys[0].Value;
        }

        if (at >= Keys[^1].At)
        {
            return Keys[^1].Value;
        }

        return Across(SpanStart(at), at);
    }

    /// <summary>
    /// The slope worked out for the key at <paramref name="index"/>, for a key
    /// that names none. Public because a curve editor draws it.
    /// </summary>
    public T SlopeAt(int index)
    {
        if (Mixer is not ICurveMixer<T> curves)
        {
            throw new InvalidOperationException($"a {typeof(T).Name} channel has no slopes");
        }

        return SlopeAt(index, curves);
    }

    private int SpanStart(double at)
    {
        var low = 0;
        var high = Keys.Count - 1;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            if (Keys[middle].At <= at)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private T Across(int low, double at)
    {
        var from = Keys[low];
        var to = Keys[low + 1];
        if (from.Hold == Hold.Step)
        {
            return from.Value;
        }

        var span = to.At - from.At;
        // Two keys at the same moment: the second wins, which is how a cut is written.
        if (span <= 0)
        {
            return to.Value;
        }

        var part = (at - from.At) / span;
        if (from.Hold == Hold.Curve && Mixer is ICurveMixer<T> curves)
        {
            return curves.Curve(
                from.Value,
                from.SlopeOut is { } leave ? leave.Value : SlopeAt(low, curves),
                to.Value,
                to.SlopeIn is { } arrive ? arrive.Value : SlopeAt(low + 1, curves),
                span,
                part);
        }

        return Mixer.Lerp(from.Value, to.Value, Shaped(from, part));
    }

    private static double Shaped(Key<T> from, double part) => from.Hold switch
    {
        Hold.Linear or Hold.Curve => part,
        Hold.Smooth => Easings.Ease(Easing.InOut, part),
        Hold.Shaped => Easings.Ease(from.Shape ?? Easing.InOut, part),
        Hold.Step => 0.0,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private T SlopeAt(int index, ICurveMixer<T> curves)
    {
        // The ends start and finish at rest: a curve run past its last key is a
        // value nobody chose, and so is a slope that assumes one.
        if (index <= 0 || index >= Keys.Count - 1)
        {
            return curves.Still;
        }

        var before = Keys[index - 1];
        var after = Keys[index + 1];
        return curves.Through(before.Value, Keys[index].Value, after.Value, after.At - before.At);
    }
}
