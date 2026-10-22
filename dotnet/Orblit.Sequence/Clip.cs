using Orblit.VectorMath;

namespace Orblit.Sequence;

/// <summary>
/// A span of a track, and how it fades at each end. A clip is a placement
/// rather than content: what it does is decided by the track it is on.
/// </summary>
public sealed class Clip
{
    // F2: nine parameters, and every one after the first two is optional and called by name.
    public Clip(
        double start,
        double duration,
        double easeIn = 0.0,
        double easeOut = 0.0,
        Easing shapeIn = Easing.InOut,
        Easing shapeOut = Easing.InOut,
        double clipIn = 0.0,
        double speed = 1.0,
        string? name = null)
    {
        if (!(duration > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "a clip with no length never runs");
        }

        if (speed == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "a clip at no speed never advances");
        }

        Start = start;
        Duration = duration;
        EaseIn = easeIn;
        EaseOut = easeOut;
        ShapeIn = shapeIn;
        ShapeOut = shapeOut;
        ClipIn = clipIn;
        Speed = speed;
        Name = name;
    }

    /// <summary>Seconds from the start of the sequence.</summary>
    public double Start { get; }

    public double Duration { get; }

    /// <summary>
    /// How long the clip takes to reach full say, and to give it up. A
    /// sequence does not match these to an overlap, because a deliberate
    /// mismatch is a legitimate effect.
    /// </summary>
    public double EaseIn { get; }

    public double EaseOut { get; }

    public Easing ShapeIn { get; }

    public Easing ShapeOut { get; }

    /// <summary>How far into its own content the clip starts, to trim the front of a take.</summary>
    public double ClipIn { get; }

    /// <summary>How fast the content runs against the sequence's clock. Two is double, a half is slow motion.</summary>
    public double Speed { get; }

    /// <summary>What it is called on the track. Not an identity: two clips may share a name.</summary>
    public string? Name { get; }

    public double End => Start + Duration;

    /// <summary>The start is inside the clip and the end is not.</summary>
    public bool Covers(double at) => at >= Start && at < End;

    /// <summary>
    /// How far into the clip's own content <paramref name="at"/> is. Where the
    /// sequence has reached is not where the content has, because the clip may
    /// be trimmed and may run at a different rate.
    /// </summary>
    public double LocalAt(double at) => ClipIn + ((at - Start) * Speed);

    /// <summary>
    /// How much say the clip has at <paramref name="at"/>, from nought outside
    /// it to one in the middle. The two fades are separate curves, because a
    /// clip can blend into the one after it while holding against the one before.
    /// </summary>
    public double WeightAt(double at)
    {
        if (!Covers(at))
        {
            return 0;
        }

        var weight = 1.0;
        if (EaseIn > 0 && at < Start + EaseIn)
        {
            weight = Easings.Ease(ShapeIn, (at - Start) / EaseIn);
        }

        if (EaseOut > 0 && at > End - EaseOut)
        {
            var fade = Easings.Ease(ShapeOut, (End - at) / EaseOut);
            weight = weight < fade ? weight : fade;
        }

        return Doubles.Clamp(weight, 0.0, 1.0);
    }
}
