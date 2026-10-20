# Changelog

## 0.2.0

- A port of `orblit_particles` 0.2.0. It holds `Emitter`, the four shapes,
  bursts, `ParticleSim`, `EmitterSampling.Sample`, `Trail`, `TrailRing`,
  `ParticleBuffer`, `TrailStrip` and `UnitRandom`.
- A particle is a function of seed, id and time. `UnitRandom` writes the
  hash out in `long` arithmetic. It does not use `System.Random`, because
  the answer must be the same on every machine and for ever.
- `Range` is a record, so two ranges with the same bounds are equal. Dart
  compared them by identity.
- `Emitter` is a class and `CopyWith` calls its constructor, so a copy is
  checked again. A copy cannot clear `Duration`, as in Dart.
- Lists are frozen as `ReadOnlyCollection`. Counts and capacities are
  `int`, where Dart's are 64-bit. Ids and seeds stay `long`.
- A refusal is an `ArgumentException` that names the argument. A buffer or
  strip of a bad size is refused before it allocates.
- Float32 buffers take a `(float)` cast on every write, as Dart's
  `Float32List` rounds to the nearest.
