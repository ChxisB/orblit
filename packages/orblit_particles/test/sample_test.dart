import 'dart:math' as math;

import 'package:orblit_particles/orblit_particles.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

List<double> positionsOf(ParticleBuffer b) =>
    b.positions.sublist(0, b.count * 3).toList();

/// One particle at time 0 that lives for [life], so a test can ask for it at
/// any age.
Emitter oneParticle({
  double life = 10,
  Range speed = const Range.of(0),
  Shape? shape,
  Vector3? gravity,
  double drag = 0,
  List<double> size = const [0.1],
  List<double> colour = const [1, 1, 1],
  ParticleSpace space = ParticleSpace.world,
  double noiseStrength = 0,
}) => Emitter(
  rate: 0,
  bursts: [Burst(0, 1)],
  lifetime: Range.of(life),
  speed: speed,
  shape: shape ?? const PointShape(),
  gravity: gravity,
  drag: drag,
  size: size,
  colour: colour,
  space: space,
  noiseStrength: noiseStrength,
);

void main() {
  late ParticleBuffer out;
  setUp(() => out = ParticleBuffer(2000));

  group('time', () {
    test('the same time gives the same frame with nothing kept', () {
      final emitter = Emitter(
        seed: 4,
        shape: SphereShape(radius: 2),
        speed: const Range(1, 3),
        gravity: Vector3(0, -9.8, 0),
        drag: 0.3,
        noiseStrength: 0.2,
      );
      emitter.sample(1.3, out);
      final first = positionsOf(out);
      final count = out.count;

      emitter.sample(7.9, out);
      emitter.sample(0.2, out);
      emitter.sample(1.3, out);

      expect(out.count, count);
      expect(positionsOf(out), first);
    });

    test('two emitters made alike give the same frame', () {
      Emitter make() => Emitter(seed: 9, shape: ConeShape(radius: 0.3));
      final other = ParticleBuffer(2000);

      make().sample(2.5, out);
      make().sample(2.5, other);

      expect(positionsOf(other), positionsOf(out));
    });

    test('another seed gives another frame', () {
      final other = ParticleBuffer(2000);

      Emitter(seed: 1).sample(2.5, out);
      Emitter(seed: 2).sample(2.5, other);

      expect(positionsOf(other), isNot(positionsOf(out)));
    });

    test('before time 0, or at a time that is not a number, is empty', () {
      final emitter = Emitter();
      for (final t in [-1.0, double.nan, double.infinity]) {
        out.count = 99;
        emitter.sample(t, out);
        expect(out.count, 0, reason: '$t');
      }
    });
  });

  group('births', () {
    test('a steady stream holds rate times lifetime', () {
      final emitter = Emitter(rate: 10, lifetime: const Range.of(1));

      emitter.sample(5.05, out);

      expect(out.count, 10);
    });

    test('the first particle is there at time 0', () {
      Emitter(rate: 10).sample(0, out);

      expect(out.count, 1);
    });

    test('births stop at the duration', () {
      final emitter = Emitter(
        rate: 10,
        duration: 1,
        lifetime: const Range.of(0.5),
      );

      emitter.sample(1.25, out);
      expect(out.count, 3);
      emitter.sample(3, out);
      expect(out.count, 0);
    });

    test('a burst appears at its time and is gone after its lifetime', () {
      final emitter = Emitter(
        rate: 0,
        bursts: [Burst(1, 5)],
        lifetime: const Range.of(2),
      );
      final counts = [0.5, 1.0, 2.9, 3.0].map((t) {
        emitter.sample(t, out);
        return out.count;
      });

      expect(counts, [0, 5, 5, 0]);
    });

    test('a burst and a stream do not share ids', () {
      final emitter = Emitter(
        rate: 5,
        bursts: [Burst(0, 3)],
        lifetime: const Range.of(1),
      );

      emitter.sample(0.5, out);

      expect(out.count, 3 + 3);
    });
  });

  group('caps', () {
    test('when over the cap the oldest die', () {
      final uncapped = Emitter(rate: 100, lifetime: const Range.of(1));
      final all = ParticleBuffer(2000);
      uncapped.sample(0.995, all);
      expect(all.count, 100);

      uncapped.copyWith(maxParticles: 10).sample(0.995, out);

      expect(out.count, 10);
      expect(positionsOf(out), positionsOf(all).sublist(90 * 3));
    });

    test('a crowd born at one moment is cut to the cap exactly', () {
      final emitter = Emitter(rate: 0, bursts: [Burst(0, 20)], maxParticles: 8);

      emitter.sample(0.5, out);

      expect(out.count, 8);
    });

    test('a limit lowers the cap for one call', () {
      final emitter = Emitter(rate: 100, lifetime: const Range.of(1));

      emitter.sample(0.995, out, limit: 7);
      expect(out.count, 7);
      emitter.sample(0.995, out);
      expect(out.count, 100);
    });

    test('the buffer is never overrun', () {
      final small = ParticleBuffer(4);

      Emitter(rate: 100).sample(1, small);

      expect(small.count, 4);
    });
  });

  group('motion', () {
    test('without drag a particle follows a parabola', () {
      final emitter = oneParticle(
        speed: const Range.of(3),
        shape: ConeShape(angle: 0),
        gravity: Vector3(0, -10, 0),
      );

      emitter.sample(0.8, out);

      expect(out.positions[0], closeTo(0, 1e-6));
      expect(out.positions[1], closeTo(3 * 0.8 - 5 * 0.64, 1e-5));
      expect(out.positions[2], closeTo(0, 1e-6));
    });

    test('with drag the closed form agrees with a fine numeric step', () {
      const drag = 0.8;
      final gravity = Vector3(0, -9.8, 0);
      final emitter = oneParticle(
        speed: const Range.of(6),
        shape: ConeShape(angle: 0.5),
        gravity: gravity,
        drag: drag,
      );
      final p = Vector3.zero();
      final v = Vector3.zero();
      emitter.birth(Emitter.burstId(0, 0), p, v);
      const dt = 1e-5;
      for (var i = 0; i < 70000; i++) {
        v.addScaled(gravity - v * drag, dt);
        p.addScaled(v, dt);
      }

      emitter.sample(0.7, out);

      expect(out.positions[0], closeTo(p.x, 1e-3));
      expect(out.positions[1], closeTo(p.y, 1e-3));
      expect(out.positions[2], closeTo(p.z, 1e-3));
    });

    test('drag slows a particle', () {
      final base = oneParticle(
        speed: const Range.of(6),
        shape: ConeShape(angle: 0),
      );
      final other = ParticleBuffer(10);

      base.sample(1, out);
      base.copyWith(drag: 2).sample(1, other);

      expect(other.positions[1], lessThan(out.positions[1]));
    });

    test('noise moves a particle by at most its strength per axis', () {
      final other = ParticleBuffer(10);
      final calm = oneParticle();

      calm.sample(1, out);
      calm.copyWith(noiseStrength: 0.5).sample(1, other);

      final moved = math.sqrt(
        math.pow(other.positions[0] - out.positions[0], 2) +
            math.pow(other.positions[1] - out.positions[1], 2) +
            math.pow(other.positions[2] - out.positions[2], 2),
      );
      expect(moved, greaterThan(0));
      expect(moved, lessThanOrEqualTo(0.5 * math.sqrt(3) + 1e-6));
    });
  });

  group('look', () {
    test('size and colour follow their ramps over a life', () {
      final emitter = oneParticle(
        life: 2,
        size: const [0, 1, 0],
        colour: const [1, 0, 0, 0, 0, 1],
      );
      final seen = <List<double>>[];
      for (final t in [0.5, 1.0, 1.5]) {
        emitter.sample(t, out);
        seen.add([out.sizes[0], out.colours[0], out.colours[2]]);
      }

      expect(seen[0][0], closeTo(0.5, 1e-6));
      expect(seen[1][0], closeTo(1, 1e-6));
      expect(seen[2][0], closeTo(0.5, 1e-6));
      expect(seen[1][1], closeTo(0.5, 1e-6));
      expect(seen[1][2], closeTo(0.5, 1e-6));
      expect(seen[0][1], closeTo(0.75, 1e-6));
    });

    test('a ramp of one stop is a constant', () {
      oneParticle(size: const [0.3]).sample(4, out);

      expect(out.sizes[0], closeTo(0.3, 1e-6));
    });

    test('spin turns a particle with its age', () {
      final spinning = oneParticle().copyWith(spin: const Range.of(2));
      final other = ParticleBuffer(10);

      spinning.sample(1, out);
      spinning.sample(2, other);

      expect(other.angles[0] - out.angles[0], closeTo(2, 1e-5));
    });
  });

  group('space', () {
    Matrix4 slide(double t) => Matrix4.translationValues(t, 0, 0);

    test('a world particle is left where its emitter was when it was born', () {
      oneParticle().sample(2, out, place: slide);

      expect(out.positions[0], closeTo(0, 1e-6));
    });

    test('a local particle goes with its emitter', () {
      oneParticle(space: ParticleSpace.local).sample(2, out, place: slide);

      expect(out.positions[0], closeTo(2, 1e-6));
    });

    test('a turned emitter turns the way its particles head', () {
      final up = oneParticle(
        speed: const Range.of(1),
        shape: ConeShape(angle: 0),
      );

      up.sample(1, out, place: (_) => Matrix4.rotationZ(math.pi / 2));

      expect(out.positions[0], closeTo(-1, 1e-6));
      expect(out.positions[1], closeTo(0, 1e-6));
    });

    test('with no placement the emitter sits at the origin', () {
      oneParticle(space: ParticleSpace.local).sample(2, out);

      expect(out.positions[0], 0);
    });
  });

  group('shapes', () {
    final shapes = <String, Shape>{
      'point': const PointShape(),
      'sphere': SphereShape(radius: 2),
      'cone': ConeShape(angle: 0.5, radius: 1),
      'box': BoxShape(Vector3(1, 2, 3)),
    };

    for (final entry in shapes.entries) {
      test('${entry.key} gives unit directions', () {
        final p = Vector3.zero();
        final d = Vector3.zero();
        for (var i = 0; i < 200; i++) {
          entry.value.pick(1, i, p, d);
          expect(d.length, closeTo(1, 1e-9));
        }
      });
    }

    test('a sphere stays inside its radius and a box inside its box', () {
      final p = Vector3.zero();
      final d = Vector3.zero();
      for (var i = 0; i < 500; i++) {
        SphereShape(radius: 2).pick(1, i, p, d);
        expect(p.length, lessThanOrEqualTo(2 + 1e-9));
        BoxShape(Vector3(1, 2, 3)).pick(1, i, p, d);
        expect(p.x.abs(), lessThanOrEqualTo(1));
        expect(p.y.abs(), lessThanOrEqualTo(2));
        expect(p.z.abs(), lessThanOrEqualTo(3));
      }
    });

    test('a cone heads within its angle of up', () {
      final p = Vector3.zero();
      final d = Vector3.zero();
      for (var i = 0; i < 500; i++) {
        ConeShape(angle: 0.5).pick(1, i, p, d);
        expect(d.y, greaterThanOrEqualTo(math.cos(0.5) - 1e-9));
      }
    });
  });
}
