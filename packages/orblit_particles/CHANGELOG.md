# Changelog

## 0.1.0

- First cut of `orblit_particles`. Pre-alpha: everything is subject to change.
- `Emitter` with point, sphere, cone and box shapes, a rate and bursts, lifetime
  and speed ranges, size and colour ramps, gravity, drag, noise and spin.
- `sample` fills a `ParticleBuffer` from the seed and the time alone. Drag and
  gravity are solved in closed form, so no step is taken.
- When more particles are due than the cap allows, the oldest die first.
- `ParticleSim`, opt in, for bounce, death on contact and turbulence.
