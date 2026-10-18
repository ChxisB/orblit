/// Smoke, sparks, dust and fire as data.
///
/// An [Emitter] is a pure function of its seed and the time, so a seek or a
/// pause shows the same frame and a network client needs nothing sent. Collision
/// and turbulence need history, so they live in [ParticleSim], which is opt in.
library;

export 'src/buffer.dart' show ParticleBuffer;
export 'src/emitter.dart' show Burst, Emitter, ParticleSpace, Placement, Range;
export 'src/sample.dart' show EmitterSampling;
export 'src/sim.dart' show Hit, HitTest, ParticleSim;
export 'src/shape.dart'
    show BoxShape, ConeShape, PointShape, Shape, SphereShape;
