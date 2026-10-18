import 'package:orblit_particles/orblit_particles.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A point moving along x at two metres a second.
Vector3 along(double time) => Vector3(2 * time, 0, 0);

List<double> xs(TrailStrip strip) => [
  for (var i = 0; i < strip.count; i++) strip.positions[i * 3],
];

void main() {
  group('Trail', () {
    test('holds a point for each sample in its life, and one to start', () {
      expect(Trail(life: 0.5, rate: 10).points, 6);
      expect(Trail(life: 1, rate: 2.5).points, 4);
    });

    test('bad settings are refused where the trail is made', () {
      final bad = <String, Trail Function()>{
        'life': () => Trail(life: 0),
        'nan life': () => Trail(life: double.nan),
        'rate': () => Trail(rate: 0),
        'infinite rate': () => Trail(rate: double.infinity),
        'no width': () => Trail(width: const []),
        'negative width': () => Trail(width: const [0.1, -1]),
        'too many points': () => Trail(life: 10, rate: 60),
      };
      for (final entry in bad.entries) {
        expect(entry.value, throwsArgumentError, reason: entry.key);
      }
    });

    test('a list handed in cannot be changed from outside afterwards', () {
      final width = <double>[0.2, 0];
      final trail = Trail(width: width);

      width.clear();

      expect(trail.width, [0.2, 0]);
    });
  });

  group('a trail along a known path', () {
    final trail = Trail(life: 1, rate: 4, width: const [0.4, 0]);
    late TrailStrip strip;
    setUp(() => strip = TrailStrip(trail.points));

    test('runs from the head back along where the point has been', () {
      trail.sampleAlong(3, along, strip);

      expect(xs(strip), [6, 5.5, 5, 4.5, 4]);
    });

    test('tapers from the first width to the last across the life', () {
      trail.sampleAlong(3, along, strip);

      expect(strip.widths[0], closeTo(0.4, 1e-6));
      expect(strip.widths[2], closeTo(0.2, 1e-6));
      expect(strip.widths[4], closeTo(0, 1e-6));
    });

    test('has no points from before time began', () {
      trail.sampleAlong(0.6, along, strip);

      expect(xs(strip), [1.2, 0.7, 0.2].map((x) => closeTo(x, 1e-6)));
      expect(strip.count, 3);
    });

    test('a seek gives the same strip with nothing kept, so no jump', () {
      trail.sampleAlong(3, along, strip);
      final first = xs(strip);

      trail.sampleAlong(40, along, strip);
      trail.sampleAlong(0.2, along, strip);
      trail.sampleAlong(3, along, strip);

      expect(xs(strip), first);
    });

    test('a time that is not a time gives an empty strip', () {
      trail.sampleAlong(-1, along, strip);
      expect(strip.count, 0);
      trail.sampleAlong(double.nan, along, strip);
      expect(strip.count, 0);
    });

    test('a strip too small gets the newest points', () {
      final small = TrailStrip(2);

      trail.sampleAlong(3, along, small);

      expect(xs(small), [6, 5.5]);
    });
  });

  group('a trail that remembers', () {
    final trail = Trail(life: 1, rate: 4, width: const [0.4, 0]);
    late TrailRing ring;
    late TrailStrip strip;
    setUp(() {
      ring = TrailRing(trail);
      strip = TrailStrip(trail.points);
    });

    void run(double from, double to, {double dt = 0.05}) {
      for (var t = from; t <= to + 1e-9; t += dt) {
        ring.record(t, along(t));
      }
    }

    test('reaches the point as it is now, and keeps older samples apart', () {
      run(0, 1);
      ring.fill(strip);

      expect(strip.positions[0], closeTo(2, 1e-6));
      expect(strip.count, greaterThan(2));
      // The head is the point as it is. The ones kept behind it are a sample
      // period apart.
      for (var i = 2; i < strip.count; i++) {
        expect(
          strip.positions[(i - 1) * 3] - strip.positions[i * 3],
          greaterThanOrEqualTo(0.45),
          reason: 'samples are at least a sample period apart',
        );
      }
    });

    test('lets go of samples older than the life', () {
      run(0, 5);
      ring.fill(strip);

      expect(strip.positions[(strip.count - 1) * 3], greaterThan(10 - 2.01));
    });

    test('a seek back clears it', () {
      run(0, 3);
      ring.record(1, along(1));
      ring.fill(strip);

      expect(strip.count, 1);
      expect(strip.positions[0], 2);
    });

    test('a jump past the whole life clears it', () {
      run(0, 1);
      ring.record(9, along(9));
      ring.fill(strip);

      expect(strip.count, 1);
      expect(strip.positions[0], 18);
    });

    test('clear empties it', () {
      run(0, 1);
      ring.clear();
      ring.fill(strip);

      expect(strip.count, 0);
    });

    test('a time that is not a time is left out', () {
      run(0, 1);
      ring.record(double.nan, Vector3(99, 0, 0));
      ring.record(-1, Vector3(99, 0, 0));
      ring.fill(strip);

      expect(strip.positions[0], closeTo(2, 1e-6));
    });

    test('a strip too small gets the newest points', () {
      run(0, 1);
      final small = TrailStrip(2);

      ring.fill(small);

      expect(small.count, 2);
      expect(small.positions[0], closeTo(2, 1e-6));
    });
  });
}
