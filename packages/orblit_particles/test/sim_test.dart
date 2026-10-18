import 'package:orblit_particles/orblit_particles.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A floor at y = 0 that faces up.
Hit? floor(Vector3 from, Vector3 to) =>
    to.y < 0 ? Hit(Vector3(to.x, 0, to.z), Vector3(0, 1, 0)) : null;

/// One particle thrown straight up from the origin at 5 m/s.
Emitter thrown() => Emitter(
  rate: 0,
  bursts: [Burst(0, 1)],
  lifetime: const Range.of(20),
  speed: const Range.of(5),
  shape: ConeShape(angle: 0),
  gravity: Vector3(0, -10, 0),
);

/// Height after each of [steps] steps of 0.01 seconds.
List<double> heights(ParticleSim sim, int steps) {
  final out = ParticleBuffer(4);
  final seen = <double>[];
  for (var i = 0; i < steps; i++) {
    sim.step(0.01);
    sim.fill(out);
    seen.add(out.count == 0 ? double.nan : out.positions[1]);
  }
  return seen;
}

void main() {
  test('a stream holds about rate times lifetime once it has settled', () {
    final sim = ParticleSim(Emitter(rate: 10, lifetime: const Range.of(1)));

    for (var i = 0; i < 50; i++) {
      sim.step(0.1);
    }

    expect(sim.count, inInclusiveRange(9, 11));
  });

  test('a burst is born once', () {
    final sim = ParticleSim(
      Emitter(rate: 0, bursts: [Burst(0.5, 4)], lifetime: const Range.of(9)),
    );

    sim.step(0.4);
    expect(sim.count, 0);
    sim.step(0.2);
    expect(sim.count, 4);
    sim.step(0.2);
    expect(sim.count, 4);
  });

  test('without a hit test a particle falls through the floor', () {
    final low = heights(
      ParticleSim(thrown()),
      200,
    ).reduce((a, b) => a < b ? a : b);

    expect(low, lessThan(-1));
  });

  test('a floor stops a particle going below it', () {
    final seen = heights(ParticleSim(thrown(), hit: floor, bounce: 0.5), 400);

    expect(seen.every((y) => y >= -1e-9), isTrue);
  });

  test('a bounce keeps the share of speed it was given', () {
    final seen = heights(ParticleSim(thrown(), hit: floor, bounce: 0.5), 400);
    final apex = seen.take(120).reduce((a, b) => a > b ? a : b);
    final landed = seen.indexWhere((y) => y < 0.02, 60);
    final again = seen
        .skip(landed + 5)
        .take(100)
        .reduce((a, b) => a > b ? a : b);

    // Heights go with speed squared, so half the speed is a quarter the height.
    expect(apex, closeTo(1.25, 0.1));
    expect(again / apex, closeTo(0.25, 0.05));
  });

  test('with no bounce a particle dies on the floor', () {
    final sim = ParticleSim(thrown(), hit: floor);

    for (var i = 0; i < 400; i++) {
      sim.step(0.01);
    }

    expect(sim.count, 0);
  });

  test('turbulence moves a particle off its path', () {
    final out = ParticleBuffer(4);
    final calm = ParticleSim(thrown());
    final shaken = ParticleSim(thrown(), turbulence: 8);
    for (var i = 0; i < 50; i++) {
      calm.step(0.01);
      shaken.step(0.01);
    }
    final other = ParticleBuffer(4);

    calm.fill(out);
    shaken.fill(other);

    expect(other.positions[0], isNot(out.positions[0]));
  });

  test('when over the cap the oldest die', () {
    final sim = ParticleSim(
      Emitter(rate: 100, lifetime: const Range.of(5), maxParticles: 5),
    );

    sim.step(1);

    expect(sim.count, 5);
  });

  test('a local particle follows its emitter when drawn', () {
    final sim = ParticleSim(
      Emitter(
        rate: 0,
        bursts: [Burst(0, 1)],
        lifetime: const Range.of(9),
        speed: const Range.of(0),
        space: ParticleSpace.local,
      ),
    );
    final out = ParticleBuffer(4);

    sim.step(1);
    sim.fill(out, place: (t) => Matrix4.translationValues(t * 3, 0, 0));

    expect(out.positions[0], closeTo(3, 1e-6));
  });

  test('a buffer too small gets what fits', () {
    final sim = ParticleSim(
      Emitter(rate: 0, bursts: [Burst(0, 10)], lifetime: const Range.of(9)),
    );
    final out = ParticleBuffer(3);

    sim.step(0.1);
    sim.fill(out);

    expect(out.count, 3);
  });

  test('bad settings are refused', () {
    expect(() => ParticleSim(Emitter(), bounce: 1.5), throwsArgumentError);
    expect(() => ParticleSim(Emitter(), bounce: -0.1), throwsArgumentError);
    expect(() => ParticleSim(Emitter(), turbulence: -1), throwsArgumentError);
  });
}
