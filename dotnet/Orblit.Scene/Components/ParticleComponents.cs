using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Orblit.Scene;

/// <summary>Where an emitter's particles start.</summary>
public enum EmitterShape
{
    Point,
    Sphere,
    Cone,
    Box,
}

/// <summary>Whether particles stay where they were born or travel with the entity.</summary>
public enum EmitterSpace
{
    World,
    Local,
}

/// <summary><paramref name="Count"/> particles all born <paramref name="Time"/> seconds after the emitter began.</summary>
public sealed record EmitterBurst(double Time, long Count);

/// <summary>
/// An entity that gives off particles: smoke, sparks, dust or fire, from where the entity is.
/// </summary>
/// <remarks>
/// This holds the settings and nothing else. A particle is worked out from them and the time, so the
/// file says what an effect is and never where its particles are. The shape reads
/// <see cref="Radius"/> for a sphere and for the disc a cone starts on, <see cref="Angle"/> for how
/// far a cone spreads from straight up, and <see cref="Extents"/>, half a side each way, for a box. A
/// point reads none of them.
/// <para>
/// A file whose numbers do not make an emitter reads as the default for that number. A lifetime that
/// is not above nothing, a range that runs backwards, a colour that is not red, green and blue, or a
/// size below nothing is one of those, since an effect that looks wrong is better than a scene that
/// will not open.
/// </para>
/// </remarks>
public sealed class ParticleEmitterComponent : SceneComponent
{
    private static readonly double[] DefaultSize = [0.1];

    private static readonly double[] DefaultColour = [1, 1, 1];

    // F2: twenty-two fields of one emitter, called by name.
    public ParticleEmitterComponent(
        long seed = 0,
        EmitterShape shape = EmitterShape.Point,
        double radius = 0.5,
        double angle = 0.4,
        Vector3? extents = null,
        double rate = 10,
        IReadOnlyList<EmitterBurst>? bursts = null,
        double? duration = null,
        double lifetimeMin = 1,
        double lifetimeMax = 2,
        double speedMin = 1,
        double speedMax = 2,
        double spinMin = 0,
        double spinMax = 0,
        IReadOnlyList<double>? size = null,
        IReadOnlyList<double>? colour = null,
        Vector3? gravity = null,
        double drag = 0,
        double noiseStrength = 0,
        double noiseScale = 1,
        EmitterSpace space = EmitterSpace.World,
        long maxParticles = 1000)
    {
        Seed = seed;
        Shape = shape;
        Radius = radius;
        Angle = angle;
        Extents = extents?.Clone() ?? Vector3.All(0.5);
        Rate = rate;
        Bursts = new ReadOnlyCollection<EmitterBurst>([.. bursts ?? []]);
        Duration = duration;
        LifetimeMin = lifetimeMin;
        LifetimeMax = lifetimeMax;
        SpeedMin = speedMin;
        SpeedMax = speedMax;
        SpinMin = spinMin;
        SpinMax = spinMax;
        Size = new ReadOnlyCollection<double>([.. size ?? DefaultSize]);
        Colour = new ReadOnlyCollection<double>([.. colour ?? DefaultColour]);
        Gravity = gravity?.Clone() ?? Vector3.Zero();
        Drag = drag;
        NoiseStrength = noiseStrength;
        NoiseScale = noiseScale;
        Space = space;
        MaxParticles = maxParticles;
    }

    /// <summary>
    /// Which particles these are. Two emitters with one seed and the same settings give the same
    /// particles.
    /// </summary>
    public long Seed { get; }

    public EmitterShape Shape { get; }

    /// <summary>Metres.</summary>
    public double Radius { get; }

    /// <summary>Radians from straight up.</summary>
    public double Angle { get; }

    /// <summary>Half a side each way, in metres.</summary>
    public Vector3 Extents { get; }

    /// <summary>Particles born each second.</summary>
    public double Rate { get; }

    public IReadOnlyList<EmitterBurst> Bursts { get; }

    /// <summary>Seconds after which no more are born, or null for no end.</summary>
    public double? Duration { get; }

    /// <summary>Seconds a particle lives, from <see cref="LifetimeMin"/> to <see cref="LifetimeMax"/>.</summary>
    public double LifetimeMin { get; }

    public double LifetimeMax { get; }

    /// <summary>Metres per second at birth.</summary>
    public double SpeedMin { get; }

    public double SpeedMax { get; }

    /// <summary>Radians per second.</summary>
    public double SpinMin { get; }

    public double SpinMax { get; }

    /// <summary>Edge length in metres, spread evenly over a life.</summary>
    public IReadOnlyList<double> Size { get; }

    /// <summary>Red, green, blue triples, spread evenly over a life.</summary>
    public IReadOnlyList<double> Colour { get; }

    /// <summary>Metres per second squared.</summary>
    public Vector3 Gravity { get; }

    /// <summary>Per second, the share of speed lost to the air.</summary>
    public double Drag { get; }

    /// <summary>Metres of push about by smooth noise.</summary>
    public double NoiseStrength { get; }

    public double NoiseScale { get; }

    public EmitterSpace Space { get; }

    /// <summary>Particles alive at once. The oldest die first when more are due.</summary>
    public long MaxParticles { get; }

    public override string Type => SceneComponents.Particles;

    public static ParticleEmitterComponent FromJson(JsonMap json)
    {
        var lifetime = Pair(Values.At(json, "lifetime"), 1, 2, static (low, _) => low > 0);
        var speed = Pair(Values.At(json, "speed"), 1, 2);
        var spin = Pair(Values.At(json, "spin"), 0, 0);
        return new ParticleEmitterComponent(
            SeedOf(json),
            Values.Named<EmitterShape>(Values.At(json, "shape")) ?? EmitterShape.Point,
            AtLeast(json, "radius", 0.5),
            Spread(json),
            ExtentsOf(Values.At(json, "extents")),
            AtLeast(json, "rate", 10),
            BurstsOf(Values.At(json, "bursts")),
            DurationOf(json),
            lifetime.Low,
            lifetime.High,
            speed.Low,
            speed.High,
            spin.Low,
            spin.High,
            Ramp(Values.At(json, "size"), 1, DefaultSize, nonNegative: true),
            Ramp(Values.At(json, "colour"), 3, DefaultColour),
            GravityOf(Values.At(json, "gravity")),
            AtLeast(json, "drag", 0),
            AtLeast(json, "noiseStrength", 0),
            AtLeast(json, "noiseScale", 1, above: true),
            Values.Named<EmitterSpace>(Values.At(json, "space")) ?? EmitterSpace.World,
            CapOf(json));
    }

    /// <summary>
    /// The same emitter with some of it changed. A <paramref name="duration"/> cannot be cleared
    /// here, only set; build a new emitter to give it no end.
    /// </summary>
    // F2: twenty-two fields of one emitter, called by name.
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of fields is data.")]
    public ParticleEmitterComponent CopyWith(
        long? seed = null,
        EmitterShape? shape = null,
        double? radius = null,
        double? angle = null,
        Vector3? extents = null,
        double? rate = null,
        IReadOnlyList<EmitterBurst>? bursts = null,
        double? duration = null,
        double? lifetimeMin = null,
        double? lifetimeMax = null,
        double? speedMin = null,
        double? speedMax = null,
        double? spinMin = null,
        double? spinMax = null,
        IReadOnlyList<double>? size = null,
        IReadOnlyList<double>? colour = null,
        Vector3? gravity = null,
        double? drag = null,
        double? noiseStrength = null,
        double? noiseScale = null,
        EmitterSpace? space = null,
        long? maxParticles = null) => new(
        seed ?? Seed,
        shape ?? Shape,
        radius ?? Radius,
        angle ?? Angle,
        extents ?? Extents,
        rate ?? Rate,
        bursts ?? Bursts,
        duration ?? Duration,
        lifetimeMin ?? LifetimeMin,
        lifetimeMax ?? LifetimeMax,
        speedMin ?? SpeedMin,
        speedMax ?? SpeedMax,
        spinMin ?? SpinMin,
        spinMax ?? SpinMax,
        size ?? Size,
        colour ?? Colour,
        gravity ?? Gravity,
        drag ?? Drag,
        noiseStrength ?? NoiseStrength,
        noiseScale ?? NoiseScale,
        space ?? Space,
        maxParticles ?? MaxParticles);

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["seed"] = Seed,
        ["shape"] = EnumNames.Of(Shape),
        ["radius"] = Radius,
        ["angle"] = Angle,
        ["extents"] = Values.VectorToJson(Extents),
        ["rate"] = Rate,
        ["bursts"] = Bursts.Select(static burst => (object?)new OrderedDictionary<string, object?>
        {
            ["time"] = burst.Time,
            ["count"] = burst.Count,
        }).ToList(),
        ["duration"] = Duration,
        ["lifetime"] = Values.NumbersToJson([LifetimeMin, LifetimeMax]),
        ["speed"] = Values.NumbersToJson([SpeedMin, SpeedMax]),
        ["spin"] = Values.NumbersToJson([SpinMin, SpinMax]),
        ["size"] = Values.NumbersToJson(Size),
        ["colour"] = Values.NumbersToJson(Colour),
        ["gravity"] = Values.VectorToJson(Gravity),
        ["drag"] = Drag,
        ["noiseStrength"] = NoiseStrength,
        ["noiseScale"] = NoiseScale,
        ["space"] = EnumNames.Of(Space),
        ["maxParticles"] = MaxParticles,
    });

    /// <summary>A number of nought or more, or <paramref name="fallback"/>. With <paramref name="above"/>, above nought.</summary>
    private static double AtLeast(JsonMap json, string key, double fallback, bool above = false)
    {
        var value = Values.Number(json, key, fallback);
        var ok = double.IsFinite(value) && (above ? value > 0 : value >= 0);
        return ok ? value : fallback;
    }

    private static long SeedOf(JsonMap json)
    {
        var value = Values.Number(json, "seed", 0);
        return double.IsFinite(value) ? Doubles.Round(value) : 0;
    }

    private static Vector3 GravityOf(object? raw)
    {
        var read = Values.Vector(raw);
        return AllFinite(read) ? read : Vector3.Zero();
    }

    private static double Spread(JsonMap json)
    {
        var value = Values.Number(json, "angle", 0.4);
        return value >= 0 && value <= Math.PI ? value : 0.4;
    }

    private static double? DurationOf(JsonMap json)
    {
        var value = Values.MaybeNumber(json, "duration");
        return value is { } seconds && double.IsFinite(seconds) && seconds >= 0 ? seconds : null;
    }

    private static long CapOf(JsonMap json)
    {
        var value = Values.Number(json, "maxParticles", 1000);
        return double.IsFinite(value) && value >= 1 ? Doubles.Round(value) : 1000;
    }

    private static Vector3 ExtentsOf(object? raw)
    {
        var read = Values.Vector(raw, 0.5);
        var ok = AllFinite(read) && read.X >= 0 && read.Y >= 0 && read.Z >= 0;
        return ok ? read : Vector3.All(0.5);
    }

    private static bool AllFinite(Vector3 value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    /// <summary>
    /// Two numbers that run from low to high, or the fallback pair. <paramref name="accept"/> can
    /// refuse a pair that is in order but still no good.
    /// </summary>
    private static (double Low, double High) Pair(
        object? raw,
        double low,
        double high,
        Func<double, double, bool>? accept = null)
    {
        var read = Values.Numbers(raw);
        if (read.Count != 2)
        {
            return (low, high);
        }

        var ok = double.IsFinite(read[0])
            && double.IsFinite(read[1])
            && read[0] <= read[1]
            && (accept?.Invoke(read[0], read[1]) ?? true);
        return ok ? (read[0], read[1]) : (low, high);
    }

    /// <summary>
    /// Stops of <paramref name="stride"/> numbers each, or <paramref name="fallback"/> when they do
    /// not make whole stops, or when one is not finite.
    /// </summary>
    private static IReadOnlyList<double> Ramp(
        object? raw,
        int stride,
        IReadOnlyList<double> fallback,
        bool nonNegative = false)
    {
        var read = Values.Numbers(raw);
        var ok = read.Count > 0
            && read.Count % stride == 0
            && read.All(n => double.IsFinite(n) && (!nonNegative || n >= 0));
        return ok ? read : fallback;
    }

    private static List<EmitterBurst> BurstsOf(object? raw)
    {
        var read = new List<EmitterBurst>();
        if (raw is not JsonList list)
        {
            return read;
        }

        foreach (var one in list)
        {
            if (one is not JsonMap map)
            {
                continue;
            }

            var time = Values.Number(map, "time", -1);
            var count = Values.Number(map, "count", 0);

            // A burst is dropped whole: counting its particles from nowhere would invent a burst the
            // file did not describe.
            if (time >= 0 && double.IsFinite(time) && count >= 1 && count <= 65535)
            {
                read.Add(new EmitterBurst(time, Doubles.Round(count)));
            }
        }

        return read;
    }
}
