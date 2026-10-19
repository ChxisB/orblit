# Changelog

## 0.2.0

- `Trail`, `TrailStrip` and `TrailRing`: a strip that follows a point, with a
  width ramp over its life. `sampleAlong` builds it from a path that can be
  asked for any time, so a seek never jumps. `TrailRing` keeps the history of
  a point whose path is not known, and clears when time runs backwards or a
  step is longer than the life.
  `TrailStrip.head` is the newest point. A strip keeps positions as doubles.

## 0.1.0

- First cut of `orblit_particles`. Pre-alpha: everything is subject to change.
- `Emitter` with point, sphere, cone and box shapes, a rate and bursts, lifetime
  and speed ranges, size and colour ramps, gravity, drag, noise and spin.
- `sample` fills a `ParticleBuffer` from the seed and the time alone. Drag and
  gravity are solved in closed form, so no step is taken.
- When more particles are due than the cap allows, the oldest die first.
- `ParticleSim`, opt in, for bounce, death on contact and turbulence.
