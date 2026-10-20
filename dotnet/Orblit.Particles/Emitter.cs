using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Orblit.VectorMath;

namespace Orblit.Particles;

/// <summary>
/// Where an emitter was at a time. A particle that rides with its emitter asks
/// for the moment it is drawn, one that is left behind asks for the moment it
/// was born. A fixed emitter passes <c>_ => matrix</c>.
/// </summary>
public delegate Matrix4 Placement(double time);

/// <summary>Whether particles stay put once born or travel with the emitter.</summary>
public enum ParticleSpace
{
    World,
    Local,
}

/// <summary>A span of values. A particle takes one at random, but always the same one.</summary>
public sealed record Range(double Min, double Max)
{
    public static Range Of(double value) => new(value, value);

    public double At(double fraction) => Min + ((Max - Min) * fraction);
}

/// <summary><c>Count</c> particles all born at <c>Time</c> seconds.</summary>
public sealed class Burst
{
    internal const int Stride = 65536;

    public Burst(double time, int count)
    {
        Require.That(time >= 0 && double.IsFinite(time), nameof(time), time, "must be 0 or more");
        Require.That(count >= 1 && count <= Stride - 1, nameof(count), count, "must be 1 to 65535");
        Time = time;
        Count = count;
    }

    public double Time { get; }

    public int Count { get; }
}

/// <summary>
/// What makes smoke, sparks, dust or fire: where particles start, how many, how
/// long they live and what happens to them on the way.
/// </summary>
/// <remarks>
/// An emitter is data. It keeps no particles. Ask it for the frame at a time
/// with <see cref="EmitterSampling.Sample"/>, and the same time gives the same
/// frame on every machine.
/// </remarks>
public sealed class Emitter
{
    private const long BurstBase = 0x40000000;

    // F2: sixteen settings, all optional and called by name, as in Dart.
    public Emitter(
        long seed = 0,
        Shape? shape = null,
        double rate = 10,
        IEnumerable<Burst>? bursts = null,
        double? duration = null,
        Range? lifetime = null,
        Range? speed = null,
        IEnumerable<double>? size = null,
        IEnumerable<double>? colour = null,
        Vector3? gravity = null,
        double drag = 0,
        double noiseStrength = 0,
        double noiseScale = 1,
        Range? spin = null,
        ParticleSpace space = ParticleSpace.World,
        int maxParticles = 1000)
    {
        Seed = seed;
        Shape = shape ?? new PointShape();
        Rate = rate;
        Bursts = Frozen(bursts ?? []);
        Duration = duration;
        Lifetime = lifetime ?? new Range(1, 2);
        Speed = speed ?? new Range(1, 2);
        Size = Frozen(size ?? [0.1]);
        Colour = Frozen(colour ?? [1, 1, 1]);
        Gravity = (gravity ?? Vector3.Zero()).Clone();
        Drag = drag;
        NoiseStrength = noiseStrength;
        NoiseScale = noiseScale;
        Spin = spin ?? Range.Of(0);
        Space = space;
        MaxParticles = maxParticles;
        CheckTiming();
        CheckLooks();
        CheckForces();
    }

    public long Seed { get; }

    public Shape Shape { get; }

    /// <summary>Particles born each second, from time 0 until <see cref="Duration"/>.</summary>
    public double Rate { get; }

    public IReadOnlyList<Burst> Bursts { get; }

    /// <summary>
    /// Seconds after which no more steady births happen, or null for no end. A
    /// particle born before then lives its whole life. Bursts ignore it.
    /// </summary>
    public double? Duration { get; }

    /// <summary>Seconds a particle lives.</summary>
    public Range Lifetime { get; }

    /// <summary>Metres per second at birth, along the direction the shape gives.</summary>
    public Range Speed { get; }

    /// <summary>Edge length over a particle's life, spread evenly from birth to death.</summary>
    public IReadOnlyList<double> Size { get; }

    /// <summary>Red, green, blue triples over a particle's life.</summary>
    public IReadOnlyList<double> Colour { get; }

    /// <summary>Metres per second squared. Do not change it after construction.</summary>
    public Vector3 Gravity { get; }

    /// <summary>Per second, the share of speed lost to the air.</summary>
    public double Drag { get; }

    /// <summary>Metres a particle is pushed about by smooth noise.</summary>
    public double NoiseStrength { get; }

    /// <summary>Noise cycles per metre.</summary>
    public double NoiseScale { get; }

    /// <summary>Radians per second.</summary>
    public Range Spin { get; }

    public ParticleSpace Space { get; }

    /// <summary>Particles alive at once. When more are due, the oldest die first.</summary>
    public int MaxParticles { get; }

    /// <summary>
    /// Id of particle <paramref name="number"/> of the burst at
    /// <paramref name="index"/> in <see cref="Bursts"/>. Ids of the steady
    /// stream are 0, 1, 2 and so on, so the two never meet.
    /// </summary>
    public static long BurstId(long index, long number) => BurstBase + (index * Burst.Stride) + number;

    // shortcut: a copy cannot clear Duration. Build a new Emitter to remove it.
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    // F2: sixteen settings, all optional and called by name, as in Dart.
    public Emitter CopyWith(
        long? seed = null,
        Shape? shape = null,
        double? rate = null,
        IEnumerable<Burst>? bursts = null,
        double? duration = null,
        Range? lifetime = null,
        Range? speed = null,
        IEnumerable<double>? size = null,
        IEnumerable<double>? colour = null,
        Vector3? gravity = null,
        double? drag = null,
        double? noiseStrength = null,
        double? noiseScale = null,
        Range? spin = null,
        ParticleSpace? space = null,
        int? maxParticles = null) =>
        new(
            seed: seed ?? Seed,
            shape: shape ?? Shape,
            rate: rate ?? Rate,
            bursts: bursts ?? Bursts,
            duration: duration ?? Duration,
            lifetime: lifetime ?? Lifetime,
            speed: speed ?? Speed,
            size: size ?? Size,
            colour: colour ?? Colour,
            gravity: gravity ?? Gravity,
            drag: drag ?? Drag,
            noiseStrength: noiseStrength ?? NoiseStrength,
            noiseScale: noiseScale ?? NoiseScale,
            spin: spin ?? Spin,
            space: space ?? Space,
            maxParticles: maxParticles ?? MaxParticles);

    /// <summary>Seconds particle <paramref name="id"/> lives.</summary>
    public double LifeOf(long id) => Lifetime.At(UnitRandom.At(Seed, id, 0));

    /// <summary>
    /// Writes where particle <paramref name="id"/> starts and how fast it heads
    /// off, in the emitter's own space.
    /// </summary>
    public void Birth(long id, Vector3 position, Vector3 velocity)
    {
        Shape.Pick(Seed, id, position, velocity);
        velocity.Scale(Speed.At(UnitRandom.At(Seed, id, 1)));
    }

    /// <summary>
    /// Writes the size, colour and angle of particle <paramref name="id"/> to
    /// <paramref name="slot"/> of <paramref name="output"/>, <paramref name="age"/>
    /// seconds into a life of <paramref name="life"/>.
    /// </summary>
    // F2: the five values are the Dart signature, and both callers pass them as they are.
    public void Look(ParticleBuffer output, int slot, long id, double age, double life)
    {
        var fraction = age / life;
        output.Sizes[slot] = (float)Ramp.At(Size, 1, 0, fraction);
        for (var channel = 0; channel < 3; channel++)
        {
            output.Colours[(slot * 3) + channel] = (float)Ramp.At(Colour, 3, channel, fraction);
        }

        output.Angles[slot] = (float)((UnitRandom.At(Seed, id, 8) * 2 * Math.PI) + (Spin.At(UnitRandom.At(Seed, id, 7)) * age));
    }

    private static ReadOnlyCollection<T> Frozen<T>(IEnumerable<T> items) => new([.. items]);

    private void CheckTiming()
    {
        Require.That(Rate >= 0 && double.IsFinite(Rate), nameof(Rate), Rate, "must be 0 or more");
        var span = Duration ?? 0;
        Require.That(span >= 0 && double.IsFinite(span), nameof(Duration), Duration, "must be 0 or more");
        CheckSpan(Lifetime, nameof(Lifetime));
        Require.That(Lifetime.Min > 0, nameof(Lifetime), Lifetime.Min, "must be above 0");
        CheckSpan(Speed, nameof(Speed));
        CheckSpan(Spin, nameof(Spin));
    }

    private void CheckLooks()
    {
        Require.That(
            Size.Count > 0 && Size.All(s => s >= 0 && double.IsFinite(s)),
            nameof(Size),
            Size,
            "needs one or more sizes of 0 or more");
        Require.That(
            Colour.Count > 0 && Colour.Count % 3 == 0 && Colour.All(double.IsFinite),
            nameof(Colour),
            Colour,
            "needs red, green, blue triples");
    }

    private void CheckForces()
    {
        Require.That(Drag >= 0 && double.IsFinite(Drag), nameof(Drag), Drag, "must be 0 or more");
        Require.That(
            NoiseStrength >= 0 && double.IsFinite(NoiseStrength),
            nameof(NoiseStrength),
            NoiseStrength,
            "must be 0 or more");
        Require.That(NoiseScale > 0 && double.IsFinite(NoiseScale), nameof(NoiseScale), NoiseScale, "must be above 0");
        Require.That(
            double.IsFinite(Gravity.X) && double.IsFinite(Gravity.Y) && double.IsFinite(Gravity.Z),
            nameof(Gravity),
            Gravity,
            "must be finite");
        Require.That(MaxParticles >= 1, nameof(MaxParticles), MaxParticles, "must be 1 or more");
    }

    private static void CheckSpan(Range range, string name) =>
        Require.That(
            double.IsFinite(range.Min) && double.IsFinite(range.Max) && range.Min <= range.Max,
            name,
            $"{range.Min} to {range.Max}",
            "must run from low to high");
}
