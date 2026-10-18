# orblit_particles

Smoke, sparks, dust and fire as data.

An `Emitter` keeps no particles. Ask it for the frame at a time and it works
out every particle alive then from the emitter's seed, each particle's id and
its age. The same time gives the same frame on every machine, including a web
build, so a seek or a pause shows what a play-through showed, and a network
client needs nothing sent.

```dart
final sparks = Emitter(
  seed: 7,
  shape: ConeShape(angle: 0.6),
  rate: 0,
  bursts: [Burst(0, 40)],
  lifetime: const Range(0.4, 0.9),
  speed: const Range(3, 6),
  gravity: Vector3(0, -9.8, 0),
  size: const [0.05, 0.02, 0],
  colour: const [1, 0.9, 0.4, 1, 0.3, 0.1],
);

final frame = ParticleBuffer(sparks.maxParticles);
sparks.sample(seconds, frame, place: (_) => Matrix4.translationValues(0, 1, 0));
```

`frame.count` slots of `positions`, `sizes`, `colours` and `angles` are the
particles to draw.

Collision and turbulence depend on where a particle has been, so a pure
function cannot give them. `ParticleSim` is the opt-in form. It keeps the
particles and steps them, and asks a `HitTest` you supply whether a move meets
the world. Two peers that step it differently drift apart, so keep it to looks.

## Using it

```yaml
dependencies:
  orblit_particles:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_particles
```

Needs Dart alone. `orblit_stage` draws an emitter in a scene.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
