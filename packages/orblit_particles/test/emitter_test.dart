import 'package:orblit_particles/orblit_particles.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  group('Emitter', () {
    test('a plain emitter is valid', () {
      expect(Emitter().maxParticles, 1000);
    });

    test('bad numbers are refused where the emitter is made', () {
      final bad = <String, Emitter Function()>{
        'rate': () => Emitter(rate: -1),
        'nan rate': () => Emitter(rate: double.nan),
        'duration': () => Emitter(duration: -1),
        'lifetime zero': () => Emitter(lifetime: const Range(0, 1)),
        'lifetime backwards': () => Emitter(lifetime: const Range(2, 1)),
        'speed': () => Emitter(speed: const Range(2, 1)),
        'no size': () => Emitter(size: const []),
        'negative size': () => Emitter(size: const [-1]),
        'colour pair': () => Emitter(colour: const [1, 1]),
        'no colour': () => Emitter(colour: const []),
        'drag': () => Emitter(drag: -1),
        'noise strength': () => Emitter(noiseStrength: -1),
        'noise scale': () => Emitter(noiseScale: 0),
        'gravity': () => Emitter(gravity: Vector3(0, double.nan, 0)),
        'cap': () => Emitter(maxParticles: 0),
      };
      for (final entry in bad.entries) {
        expect(entry.value, throwsArgumentError, reason: entry.key);
      }
    });

    test('a list handed in cannot be changed from outside afterwards', () {
      final sizes = [0.1];
      final colours = <double>[1, 1, 1];
      final bursts = [Burst(0, 2)];
      final emitter = Emitter(size: sizes, colour: colours, bursts: bursts);

      sizes.clear();
      colours.clear();
      bursts.clear();

      final out = ParticleBuffer(10);
      emitter.sample(0.5, out);
      expect(emitter.size, [0.1]);
      expect(emitter.colour, [1, 1, 1]);
      expect(emitter.bursts, hasLength(1));
      expect(() => emitter.size.add(1), throwsUnsupportedError);
    });

    test('a burst needs a time and a count it can carry', () {
      expect(() => Burst(-1, 5), throwsArgumentError);
      expect(() => Burst(0, 0), throwsArgumentError);
      expect(() => Burst(0, 65536), throwsArgumentError);
      expect(Burst(0, 65535).count, 65535);
    });

    test('shapes refuse bad sizes', () {
      expect(() => SphereShape(radius: -1), throwsArgumentError);
      expect(() => ConeShape(angle: 4), throwsArgumentError);
      expect(() => ConeShape(radius: -1), throwsArgumentError);
      expect(() => BoxShape(Vector3(1, -1, 1)), throwsArgumentError);
    });

    test('copyWith changes one thing and keeps the rest', () {
      final a = Emitter(seed: 3, rate: 5, drag: 0.5);

      final b = a.copyWith(rate: 20);

      expect(b.rate, 20);
      expect(b.seed, 3);
      expect(b.drag, 0.5);
      expect(() => a.copyWith(rate: -1), throwsArgumentError);
    });

    test('gravity is copied so a later change outside does not reach it', () {
      final g = Vector3(0, -9.8, 0);
      final emitter = Emitter(gravity: g);

      g.y = 0;

      expect(emitter.gravity.y, -9.8);
    });
  });
}
