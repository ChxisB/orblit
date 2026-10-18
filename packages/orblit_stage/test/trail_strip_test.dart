import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_particles/orblit_particles.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

/// A strip of [xs] along x, newest first, each [width] wide.
TrailStrip strip(List<double> xs, {double width = 0.4, int? capacity}) {
  final out = TrailStrip(capacity ?? xs.length);
  final trail = Trail(life: 1, rate: xs.length - 1, width: [width]);
  var at = 0;
  trail.sampleAlong(10, (time) => Vector3(xs[at++], 0, 0), out);
  return out;
}

Vector3 where(OrblitJointPose pose) => pose.transform.getTranslation();

void main() {
  test('a ribbon needs two to 128 points', () {
    expect(() => OrblitTrailStrip(points: 1), throwsArgumentError);
    expect(() => OrblitTrailStrip(points: 129), throwsArgumentError);
    expect(OrblitTrailStrip(points: 128).toGlb(), isNotEmpty);
  });

  test('the glb has an edge on each side of every point', () {
    final parts = gltfParts(OrblitTrailStrip(points: 5).toGlb());
    final read = GltfAccessors(parts.json, parts.binary);
    final primitive =
        ((parts.json['meshes'] as List).first as Map)['primitives'] as List;
    final attributes = (primitive.first as Map)['attributes'] as Map;

    expect(read.count(attributes['POSITION'] as int), 10);
    final indices = read.indices((primitive.first as Map)['indices'] as int);
    expect(indices.length, 4 * 6);
  });

  group('poses', () {
    final ribbon = OrblitTrailStrip(points: 4);
    final eye = Vector3(0, 0, 10);

    test('put an edge each side of the point, a width apart', () {
      final poses = ribbon.poses(strip([3, 2, 1, 0]), eye);

      expect(poses.length, 8);
      for (var i = 0; i < 4; i++) {
        final left = where(poses[i * 2]);
        final right = where(poses[i * 2 + 1]);
        expect((right - left).length, closeTo(0.4, 1e-6));
        expect((left + right) / 2, _near(Vector3(3.0 - i, 0, 0)));
      }
    });

    test(
      'lay the band across the way the point goes and the way it is seen',
      () {
        final poses = ribbon.poses(strip([3, 2, 1, 0]), eye);

        final across = where(poses[3]) - where(poses[2]);
        expect(across.dot(Vector3(1, 0, 0)), closeTo(0, 1e-6));
        expect(across.dot(eye - Vector3(2, 0, 0)), closeTo(0, 1e-6));
      },
    );

    test('follow the camera round to the other side', () {
      final front = ribbon.poses(strip([3, 2, 1, 0]), Vector3(0, 0, 10));
      final above = ribbon.poses(strip([3, 2, 1, 0]), Vector3(0, 10, 0));

      final seenFront = where(front[3]) - where(front[2]);
      final seenAbove = where(above[3]) - where(above[2]);
      expect(seenFront.y.abs(), closeTo(0.4, 1e-6));
      expect(seenAbove.z.abs(), closeTo(0.4, 1e-6));
    });

    test('fold rows past the end of the strip onto its last point', () {
      final poses = ribbon.poses(strip([3, 2], capacity: 4), eye);

      expect(where(poses[4]), _near(Vector3(2, 0, 0)));
      expect(where(poses[5]), _near(Vector3(2, 0, 0)));
      expect(where(poses[7]), _near(Vector3(2, 0, 0)));
    });

    test('an empty strip draws nothing', () {
      final poses = ribbon.poses(TrailStrip(4), eye);

      for (final pose in poses) {
        expect(where(pose), _near(Vector3.zero()));
      }
    });

    test(
      'pinch where there is no width, no direction or the eye is in line',
      () {
        final thin = ribbon.poses(strip([3, 2, 1, 0], width: 0), eye);
        final still = ribbon.poses(strip([1, 1, 1, 1]), eye);
        final head = ribbon.poses(strip([3, 2, 1, 0]), Vector3(-10, 0, 0));

        for (final poses in [thin, still, head]) {
          for (var i = 0; i < 4; i++) {
            expect(where(poses[i * 2]), _near(where(poses[i * 2 + 1])));
          }
        }
      },
    );
  });
}

Matcher _near(Vector3 want) =>
    predicate<Vector3>((got) => (got - want).length < 1e-6, 'is near $want');
