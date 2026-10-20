using Orblit.Noise;
using Orblit.VectorMath;

namespace Orblit.Particles;

/// <summary>Where a segment meets a surface and which way that surface faces.</summary>
public sealed record Hit(Vector3 Point, Vector3 Normal);

/// <summary>
/// Answers whether the segment from <paramref name="from"/> to
/// <paramref name="to"/> meets anything. The two vectors are reused, so a caller
/// must not keep them. Points are in the space the particles move in, which is
/// the world for <see cref="ParticleSpace.World"/>.
/// </summary>
public delegate Hit? HitTest(Vector3 from, Vector3 to);

/// <summary>
/// An <see cref="Emitter"/> that keeps its particles and steps them forward, for
/// what a pure function cannot do: bounce off the world and be pushed about by
/// turbulence that depends on where a particle has been.
/// </summary>
/// <remarks>
/// It is opt in. Two peers that step it with different time slices drift apart,
/// so it is for looks and not for anything a game decides on.
/// </remarks>
public sealed class ParticleSim
{
    // shortcut: a fixed gap off the surface so the next segment starts clear of it.
    // Make it a field if a game works at a very different scale.
    private const double Skin = 1e-4;

    private readonly GradientNoise _noise;
    private readonly List<Live> _live = [];
    private readonly bool[] _fired;
    private readonly Vector3 _to = Vector3.Zero();
    private readonly Vector3 _push = Vector3.Zero();
    private double _time;
    private long _next;

    public ParticleSim(Emitter emitter, HitTest? hit = null, double? bounce = null, double turbulence = 0)
    {
        Require.That(bounce is null || (bounce >= 0 && bounce <= 1), nameof(bounce), bounce, "must be 0 to 1");
        Require.That(turbulence >= 0 && double.IsFinite(turbulence), nameof(turbulence), turbulence, "must be 0 or more");
        Emitter = emitter;
        Hit = hit;
        Bounce = bounce;
        Turbulence = turbulence;
        _noise = new GradientNoise(emitter.Seed);
        _fired = new bool[emitter.Bursts.Count];
    }

    public Emitter Emitter { get; }

    public HitTest? Hit { get; }

    /// <summary>Share of speed a particle keeps off a surface, or null to die on it.</summary>
    public double? Bounce { get; }

    /// <summary>Metres per second squared of smooth push about.</summary>
    public double Turbulence { get; }

    public int Count => _live.Count;

    /// <summary>Moves on by <paramref name="dt"/> seconds. <paramref name="place"/> is where the emitter is, as for <c>Sample</c>.</summary>
    public void Step(double dt, Placement? place = null)
    {
        Require.That(dt >= 0 && double.IsFinite(dt), nameof(dt), dt, "must be 0 or more");
        var old = _live.Count;
        _time += dt;
        BirthAll(place);
        for (var i = 0; i < _live.Count; i++)
        {
            var p = _live[i];
            Advance(p, i < old ? dt : _time - p.Born);
        }

        _live.RemoveAll(p => p.Dead);
        var excess = _live.Count - Emitter.MaxParticles;
        if (excess > 0)
        {
            _live.RemoveRange(0, excess);
        }
    }

    /// <summary>Fills <paramref name="output"/> with the particles alive now.</summary>
    public void Fill(ParticleBuffer output, Placement? place = null)
    {
        output.Count = 0;
        var matrix = Emitter.Space == ParticleSpace.Local ? place?.Invoke(_time) : null;
        foreach (var p in _live)
        {
            if (output.Count == output.Capacity)
            {
                break;
            }

            var slot = output.Count++;
            _to.SetFrom(p.Position);
            matrix?.Transform3(_to);
            VectorArrays.Write(_to, output.Positions, slot * 3);
            Emitter.Look(output, slot, p.Id, p.Age, p.Life);
        }
    }

    private void BirthAll(Placement? place)
    {
        var rate = Emitter.Rate;
        var end = Emitter.Duration ?? double.PositiveInfinity;

        // Those born more than a longest life ago are dead on arrival, so a long
        // pause does not birth them only to drop them.
        _next = Math.Max(_next, Doubles.Floor((_time - Emitter.Lifetime.Max) * rate));
        while (rate > 0 && _next / rate <= Math.Min(_time, end))
        {
            Birth(_next, _next / rate, place);
            _next++;
        }

        for (var index = 0; index < Emitter.Bursts.Count; index++)
        {
            var burst = Emitter.Bursts[index];
            if (_fired[index] || burst.Time > _time)
            {
                continue;
            }

            _fired[index] = true;
            for (var number = 0; number < burst.Count; number++)
            {
                Birth(Emitter.BurstId(index, number), burst.Time, place);
            }
        }
    }

    private void Birth(long id, double born, Placement? place)
    {
        var p = new Live(id, born, Emitter.LifeOf(id));
        Emitter.Birth(id, p.Position, p.Velocity);
        var matrix = Emitter.Space == ParticleSpace.World ? place?.Invoke(born) : null;
        matrix?.Transform3(p.Position);
        matrix?.Rotate3(p.Velocity);
        _live.Add(p);
    }

    private void Advance(Live p, double span)
    {
        p.Age += span;
        if (p.Age >= p.Life)
        {
            p.Dead = true;
            return;
        }

        p.Velocity.AddScaled(Emitter.Gravity, span);
        if (Turbulence > 0)
        {
            var scale = Emitter.NoiseScale;
            var x = (p.Position.X * scale) + (UnitRandom.At(Emitter.Seed, p.Id, 9) * 64);
            Swirl.Write(_noise, x, p.Position.Y * scale, (p.Position.Z * scale) + (_time * scale), _push);
            p.Velocity.AddScaled(_push, Turbulence * span);
        }

        p.Velocity.Scale(Math.Exp(-Emitter.Drag * span));
        _to.SetFrom(p.Position);
        _to.AddScaled(p.Velocity, span);
        Land(p);
    }

    private void Land(Live p)
    {
        var found = Hit?.Invoke(p.Position, _to);
        if (found is null)
        {
            p.Position.SetFrom(_to);
        }
        else if (Bounce is { } keep)
        {
            var into = p.Velocity.Dot(found.Normal);
            if (into < 0)
            {
                p.Velocity.AddScaled(found.Normal, -(1 + keep) * into);
            }

            p.Position.SetFrom(found.Point);
            p.Position.AddScaled(found.Normal, Skin);
        }
        else
        {
            p.Dead = true;
        }
    }

    private sealed class Live(long id, double born, double life)
    {
        public long Id { get; } = id;

        public double Born { get; } = born;

        public double Life { get; } = life;

        public Vector3 Position { get; } = Vector3.Zero();

        public Vector3 Velocity { get; } = Vector3.Zero();

        public double Age { get; set; }

        public bool Dead { get; set; }
    }
}
