using Orblit.Filament;
using Orblit.Particles;
using Orblit.Scene;
using Orblit.VectorMath;
using ParticleRange = Orblit.Particles.Range;

namespace Orblit.Stage;

/// <summary>The particles of a staged scene.</summary>
/// <remarks>
/// A <see cref="ParticleEmitterComponent"/> gives off particles from where its entity is, for as
/// long as the entity is shown. Each entity has its own clock. It starts at 0 when the entity is
/// first shown, and starts again if the entity is hidden and shown. A change to the component keeps
/// the clock, so an effect being edited does not restart under the artist's hands.
///
/// Call <see cref="Advance"/> once a frame and draw <see cref="Populations"/>. Each emitter is one
/// population, keyed from <c>key</c> up, and is left out while it has no particle. A particle is a
/// cube that faces the camera, turned by its angle and as wide as its size, so a size ramp that ends
/// at 0 is how one fades. A population has no alpha, no material and no flipbook, and the built-in
/// cube is lit, so smoke is a grey that shrinks and not a soft cloud.
///
/// <c>budget</c> is how many particles the whole scene may draw. The emitters that started first keep
/// theirs, and the later ones are cut to what is left.
///
/// A component that does not make an emitter, which only one built in code can be, is left out and
/// named in <see cref="Rejected"/> until the scene changes.
/// </remarks>
public sealed class ParticleView(DocumentView view, int key, int budget = 20000)
{
    private readonly OrderedDictionary<string, Live> _live = [];
    private readonly Dictionary<string, int> _keys = [];
    private readonly Dictionary<string, string> _rejected = [];
    private List<Population> _drawn = [];
    private SceneDocument? _synced;
    private int _revision;

    /// <summary>What to draw this frame. Each population owns its arrays, so a later frame leaves it as it was.</summary>
    public IReadOnlyList<Population> Populations => _drawn;

    /// <summary>Why an entity's emitter was left out, by entity id.</summary>
    public IReadOnlyDictionary<string, string> Rejected => new Dictionary<string, string>(_rejected);

    /// <summary>Moves every emitter's clock on by <paramref name="seconds"/> and works out what is alive.</summary>
    public void Advance(double seconds)
    {
        if (!ReferenceEquals(view.Document, _synced))
        {
            Reconcile(Emitters());
        }

        var step = double.IsFinite(seconds) && seconds > 0 ? seconds : 0.0;
        var facing = new Facing(view.Camera);
        var drawn = new List<Population>();
        var left = budget;
        foreach (var (id, live) in _live)
        {
            live.Advance(step, view.WorldOf(id));
            if (left == 0)
            {
                continue;
            }

            var count = live.Fill(facing, left);
            left -= count;
            if (count > 0)
            {
                drawn.Add(PopulationOf(id, live, count));
            }
        }

        _drawn = drawn;
    }

    // The emitters of the shown entities, by id, in document order.
    private OrderedDictionary<string, ParticleEmitterComponent> Emitters()
    {
        var found = new OrderedDictionary<string, ParticleEmitterComponent>();
        foreach (var entity in view.Document.Entities)
        {
            if (entity[SceneComponents.Particles] is ParticleEmitterComponent emitter && view.IsShown(entity.Id))
            {
                found[entity.Id] = emitter;
            }
        }

        return found;
    }

    private void Reconcile(OrderedDictionary<string, ParticleEmitterComponent> wanted)
    {
        _synced = view.Document;
        _rejected.Clear();
        foreach (var id in _live.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
        {
            _live.Remove(id);
        }

        foreach (var (id, component) in wanted)
        {
            if (!(_live.TryGetValue(id, out var known) && ReferenceEquals(known.Component, component)))
            {
                Apply(id, component);
            }
        }
    }

    private void Apply(string id, ParticleEmitterComponent component)
    {
        try
        {
            var emitter = EmitterOf(component);
            var capacity = (int)Math.Min(component.MaxParticles, budget);
            if (_live.TryGetValue(id, out var live))
            {
                live.Use(component, emitter, capacity);
            }
            else
            {
                _live[id] = new Live(component, emitter, capacity);
            }
        }
        catch (ArgumentException error)
        {
            _live.Remove(id);
            _rejected[id] = error.Message;
        }
    }

    private Population PopulationOf(string id, Live live, int count) =>
        new(
            key + KeyFor(id),
            live.Transforms[..(count * 16)],
            live.Buffer.Colours[..(count * 3)],
            new Bounds(live.Minimum, live.Maximum))
        {
            Fade = Fade.None,
            Revision = ++_revision,
            ReceiveShadows = false,
        };

    // An emitter keeps its key for as long as the view lives, so the renderer matches it to its last frame.
    private int KeyFor(string id)
    {
        if (!_keys.TryGetValue(id, out var assigned))
        {
            assigned = _keys.Count;
            _keys[id] = assigned;
        }

        return assigned;
    }

    private static Emitter EmitterOf(ParticleEmitterComponent c) => new(
        seed: c.Seed,
        shape: ShapeOf(c),
        rate: c.Rate,
        bursts: c.Bursts.Select(burst => new Burst(burst.Time, (int)Math.Clamp(burst.Count, int.MinValue, int.MaxValue))),
        duration: c.Duration,
        lifetime: new ParticleRange(c.LifetimeMin, c.LifetimeMax),
        speed: new ParticleRange(c.SpeedMin, c.SpeedMax),
        size: c.Size,
        colour: c.Colour,
        gravity: c.Gravity,
        drag: c.Drag,
        noiseStrength: c.NoiseStrength,
        noiseScale: c.NoiseScale,
        spin: new ParticleRange(c.SpinMin, c.SpinMax),
        space: c.Space == EmitterSpace.World ? ParticleSpace.World : ParticleSpace.Local,
        maxParticles: (int)Math.Clamp(c.MaxParticles, int.MinValue, int.MaxValue));

    private static Shape ShapeOf(ParticleEmitterComponent c) => c.Shape switch
    {
        EmitterShape.Point => new PointShape(),
        EmitterShape.Sphere => new SphereShape(radius: c.Radius),
        EmitterShape.Cone => new ConeShape(angle: c.Angle, radius: c.Radius),
        _ => new BoxShape(c.Extents),
    };

    /// <summary>One emitter that is running: its clock, where its entity has been, and the buffers it is drawn from.</summary>
    private sealed class Live
    {
        private readonly List<double> _times = [];
        private readonly List<Matrix4> _worlds = [];
        private double _time;

        public Live(ParticleEmitterComponent component, Emitter emitter, int capacity)
        {
            Component = component;
            Emitter = emitter;
            Buffer = new ParticleBuffer(capacity);
            Transforms = new float[capacity * 16];
        }

        public ParticleEmitterComponent Component { get; private set; }

        public Emitter Emitter { get; private set; }

        public ParticleBuffer Buffer { get; private set; }

        public float[] Transforms { get; private set; }

        public Vector3 Minimum { get; private set; } = Vector3.Zero();

        public Vector3 Maximum { get; private set; } = Vector3.Zero();

        /// <summary>Swaps in a changed component and keeps the clock and the history.</summary>
        public void Use(ParticleEmitterComponent next, Emitter made, int capacity)
        {
            Component = next;
            Emitter = made;
            if (Buffer.Capacity == capacity)
            {
                return;
            }

            Buffer = new ParticleBuffer(capacity);
            Transforms = new float[capacity * 16];
        }

        public void Advance(double step, Matrix4 world)
        {
            _time += step;
            Remember(world);
        }

        /// <summary>
        /// Samples the emitter, writes a camera-facing transform for each particle and the box around
        /// them. Returns how many are alive, at most <paramref name="limit"/>.
        /// </summary>
        public int Fill(Facing facing, int limit)
        {
            Emitter.Sample(_time, Buffer, place: WorldAt, limit: limit);
            var count = Buffer.Count;
            for (var i = 0; i < count; i++)
            {
                Write(i, facing);
            }

            Box(count);
            return count;
        }

        private void Write(int i, Facing facing)
        {
            var size = Buffer.Sizes[i];
            var turn = Buffer.Angles[i];
            var cos = Math.Cos(turn);
            var sin = Math.Sin(turn);
            var o = i * 16;
            for (var a = 0; a < 3; a++)
            {
                var x = facing.X[a];
                var y = facing.Y[a];
                Transforms[o + a] = (float)(((x * cos) + (y * sin)) * size);
                Transforms[o + 4 + a] = (float)(((y * cos) - (x * sin)) * size);
                Transforms[o + 8 + a] = (float)(facing.Z[a] * size);
                Transforms[o + 12 + a] = Buffer.Positions[(i * 3) + a];
            }

            Transforms[o + 3] = Transforms[o + 7] = Transforms[o + 11] = 0;
            Transforms[o + 15] = 1;
        }

        // A cube's corner is 0.87 of its width from its centre.
        private void Box(int count)
        {
            var low = Vector3.All(double.PositiveInfinity);
            var high = Vector3.All(double.NegativeInfinity);
            var widest = 0.0;
            for (var i = 0; i < count; i++)
            {
                widest = Math.Max(widest, Buffer.Sizes[i]);
                for (var a = 0; a < 3; a++)
                {
                    low[a] = Math.Min(low[a], Buffer.Positions[(i * 3) + a]);
                    high[a] = Math.Max(high[a], Buffer.Positions[(i * 3) + a]);
                }
            }

            Minimum = low - Vector3.All(widest);
            Maximum = high + Vector3.All(widest);
        }

        private void Remember(Matrix4 world)
        {
            if (_worlds.Count == 0 || !Same(_worlds[^1], world))
            {
                _times.Add(_time);
                _worlds.Add(world.Clone());
            }

            // Keep the entry at or before the oldest birth that can still be alive, since a particle
            // born between two entries is placed by the earlier one.
            var oldest = _time - Component.LifetimeMax;
            while (_times.Count > 1 && _times[1] <= oldest)
            {
                _times.RemoveAt(0);
                _worlds.RemoveAt(0);
            }
        }

        // shortcut: stepped to the last frame's place, so a fast emitter leaves a staircase. Blend the
        // two entries around the time when that shows.
        private Matrix4 WorldAt(double time)
        {
            var low = 0;
            var high = _times.Count - 1;
            while (low < high)
            {
                var mid = (low + high + 1) >> 1;
                if (_times[mid] <= time)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return _worlds[low];
        }

        private static bool Same(Matrix4 a, Matrix4 b)
        {
            for (var i = 0; i < 16; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The axes a particle is built on so that it faces the camera, one for the whole view.</summary>
    private sealed class Facing
    {
        public Facing(RenderCamera camera)
        {
            Z = Towards(camera);
            var side = new Vector3(0, 1, 0).Cross(Z);

            // Looking straight up or down, where "up" says nothing about turn.
            if (side.Length2 < 1e-12)
            {
                side.SetValues(1, 0, 0);
            }

            side.Normalize();
            X = side;
            Y = Z.Cross(side);
        }

        public Vector3 X { get; }

        public Vector3 Y { get; }

        public Vector3 Z { get; }

        private static Vector3 Towards(RenderCamera camera)
        {
            var back = camera.Position - camera.Target;
            if (back.Length2 == 0)
            {
                return new Vector3(0, 0, 1);
            }

            back.Normalize();
            return back;
        }
    }
}
