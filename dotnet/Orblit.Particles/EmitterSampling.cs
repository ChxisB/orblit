using Orblit.Noise;
using Orblit.VectorMath;

namespace Orblit.Particles;

/// <summary>The frame an emitter makes at a time.</summary>
public static class EmitterSampling
{
    /// <summary>
    /// Fills <paramref name="output"/> with every particle alive
    /// <paramref name="time"/> seconds after the emitter began.
    /// </summary>
    /// <remarks>
    /// Nothing is kept between calls. A particle is found from its id and its
    /// age, so a seek, a pause and a replay all give the same frame.
    /// <paramref name="limit"/> lowers the cap below
    /// <see cref="Emitter.MaxParticles"/>, for a scene that shares a budget.
    /// </remarks>
    public static void Sample(
        this Emitter emitter,
        double time,
        ParticleBuffer output,
        Placement? place = null,
        int? limit = null)
    {
        output.Count = 0;
        if (!(time >= 0 && double.IsFinite(time)))
        {
            return;
        }

        var cap = Math.Min(Math.Min(limit ?? emitter.MaxParticles, emitter.MaxParticles), output.Capacity);
        if (cap > 0)
        {
            new Frame(emitter, time, output, place).Fill(cap);
        }
    }

    private delegate void Visit(long id, double born, double life);

    private sealed class Frame(Emitter emitter, double time, ParticleBuffer output, Placement? place)
    {
        // Below this the closed form loses its digits to cancellation, and the
        // drag-free path is off by less than a hair.
        private const double NegligibleDrag = 1e-6;

        private readonly GradientNoise _noise = new(emitter.Seed);
        private readonly Vector3 _start = Vector3.Zero();
        private readonly Vector3 _position = Vector3.Zero();
        private readonly Vector3 _velocity = Vector3.Zero();
        private readonly Vector3 _push = Vector3.Zero();

        public void Fill(int cap)
        {
            var alive = 0;
            Each((_, _, _) => alive++);
            var cutoff = double.PositiveInfinity;
            var ties = 0;
            if (alive > cap)
            {
                (cutoff, ties) = Cut(alive, cap);
            }

            Each((id, born, life) =>
            {
                var age = time - born;
                if (age > cutoff || (age == cutoff && ties-- <= 0))
                {
                    return;
                }

                Write(id, born, life);
            });
        }

        /// <summary>The age of the youngest particle that must die, and how many of that age live.</summary>
        private (double Cutoff, int Ties) Cut(int alive, int cap)
        {
            var ages = new double[alive];
            var next = 0;
            Each((_, born, _) => ages[next++] = time - born);
            Array.Sort(ages);
            var cutoff = ages[cap - 1];
            return (cutoff, cap - ages.Count(age => age < cutoff));
        }

        /// <summary>
        /// Offers every particle that could be alive. Those born long enough
        /// ago to be dead are never looked at.
        /// </summary>
        private void Each(Visit visit)
        {
            EachSteady(visit);
            EachBurst(visit);
        }

        private void EachSteady(Visit visit)
        {
            var rate = emitter.Rate;
            if (rate <= 0)
            {
                return;
            }

            var until = Math.Min(time, emitter.Duration ?? time);
            var first = Math.Max(0, Doubles.Floor((time - emitter.Lifetime.Max) * rate));
            for (var id = first; id <= Doubles.Floor(until * rate); id++)
            {
                Offer(id, id / rate, visit);
            }
        }

        private void EachBurst(Visit visit)
        {
            for (var index = 0; index < emitter.Bursts.Count; index++)
            {
                var burst = emitter.Bursts[index];
                if (burst.Time > time || burst.Time + emitter.Lifetime.Max <= time)
                {
                    continue;
                }

                for (var number = 0; number < burst.Count; number++)
                {
                    Offer(Emitter.BurstId(index, number), burst.Time, visit);
                }
            }
        }

        private void Offer(long id, double born, Visit visit)
        {
            var life = emitter.LifeOf(id);
            var age = time - born;
            if (age >= 0 && age < life)
            {
                visit(id, born, life);
            }
        }

        private void Write(long id, double born, double life)
        {
            var age = time - born;
            var world = emitter.Space == ParticleSpace.World;
            emitter.Birth(id, _position, _velocity);
            if (world)
            {
                Place(born);
            }

            _start.SetFrom(_position);
            Fly(age);
            Wander(id, age);
            if (!world)
            {
                Place(time);
            }

            var slot = output.Count++;
            VectorArrays.Write(_position, output.Positions, slot * 3);
            emitter.Look(output, slot, id, age, life);
        }

        private void Place(double at)
        {
            var matrix = place?.Invoke(at);
            if (matrix is null)
            {
                return;
            }

            matrix.Transform3(_position);
            matrix.Rotate3(_velocity);
        }

        /// <summary>
        /// Moves the particle on from its birth by <paramref name="age"/>
        /// seconds, in the closed form of constant gravity and drag, so no
        /// step is taken.
        /// </summary>
        private void Fly(double age)
        {
            var gravity = emitter.Gravity;
            var drag = emitter.Drag;
            if (drag < NegligibleDrag)
            {
                _position.AddScaled(_velocity, age);
                _position.AddScaled(gravity, 0.5 * age * age);
                return;
            }

            var slide = (1 - Math.Exp(-drag * age)) / drag;
            _position.AddScaled(gravity, (age - slide) / drag);
            _position.AddScaled(_velocity, slide);
        }

        /// <summary>
        /// A smooth push about, which depends on where the particle is and not
        /// on where it has been. The push a newborn would get is taken off, so a
        /// particle starts on the shape and drifts from there.
        /// </summary>
        private void Wander(long id, double age)
        {
            var strength = emitter.NoiseStrength;
            if (strength == 0)
            {
                return;
            }

            Swirl(id, _position, age);
            _position.AddScaled(_push, strength);
            Swirl(id, _start, 0);
            _position.AddScaled(_push, -strength);
        }

        private void Swirl(long id, Vector3 at, double age)
        {
            var scale = emitter.NoiseScale;
            Particles.Swirl.Write(
                _noise,
                (at.X * scale) + (UnitRandom.At(emitter.Seed, id, 9) * 64),
                at.Y * scale,
                (at.Z * scale) + (age * scale),
                _push);
        }
    }
}
