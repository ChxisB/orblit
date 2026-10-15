import 'package:orblit_camera/orblit_camera.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A wall standing across the Z axis at [z], from [minX] to [maxX] and as
/// tall as anyone needs. A ball going from one side of it towards the other
/// stops [radius] short of it.
ArmProbe wallAt(double z, {double minX = -2, double maxX = 2}) {
  return (from, to, radius) {
    final stand = z + radius * (from.z > z ? 1 : -1);
    final run = to.z - from.z;
    if ((from.z - z).abs() <= radius) return 0;
    if (run.abs() < 1e-9) return null;
    final share = (stand - from.z) / run;
    if (share < 0 || share > 1) return null;
    final x = from.x + (to.x - from.x) * share;
    if (x < minX || x > maxX) return null;
    return share * (to - from).length;
  };
}

ArmProbe get openAir =>
    (from, to, radius) => null;

const lens = Lens();

Vector3 step(ArmBody body, Vector3 current, CameraTarget? target) => body.solve(
  current,
  target,
  rotation: Quaternion.identity(),
  lens: lens,
  aspect: 16 / 9,
  delta: 1 / 60,
);

FollowBody behind({double damping = 0}) => FollowBody(
  offset: Vector3(0, 0, 6),
  binding: FollowBinding.world,
  damping: Vector3.all(damping),
);

void main() {
  group('a camera arm', () {
    test('leaves a camera alone when nothing is in the way', () {
      final arm = ArmBody(inner: behind(), probe: openAir, height: 1);
      final target = FixedTarget(Vector3(3, 0, 0));
      final place = step(arm, Vector3.zero(), target);
      expect(place.x, closeTo(3, 1e-9));
      expect(place.z, closeTo(6, 1e-9));
    });

    test('stops a camera short of a wall behind the target', () {
      final arm = ArmBody(
        inner: behind(),
        probe: wallAt(3),
        radius: 0.25,
        height: 1,
      );
      final place = step(arm, Vector3.zero(), FixedTarget(Vector3.zero()));
      expect(place.z, closeTo(2.75, 1e-9));
      // It stays on the line from the target's head to where it wanted to be.
      expect(place.y, closeTo(1 - 2.75 / 6, 1e-9));
    });

    test('lands at the target when the wall is against the target', () {
      final arm = ArmBody(inner: behind(), probe: wallAt(0.1), height: 1);
      final place = step(arm, Vector3.zero(), FixedTarget(Vector3.zero()));
      expect(place.z, closeTo(0, 1e-9));
      expect(place.y, closeTo(1, 1e-9));
    });

    test('is not stopped by a wall the arm goes round', () {
      final arm = ArmBody(inner: behind(), probe: wallAt(3), height: 1);
      final place = step(arm, Vector3.zero(), FixedTarget(Vector3(5, 0, 0)));
      expect(place.z, closeTo(6, 1e-9));
    });

    test('is not stopped by a wall beyond where it wants to be', () {
      final arm = ArmBody(inner: behind(), probe: wallAt(9), height: 1);
      final place = step(arm, Vector3.zero(), FixedTarget(Vector3.zero()));
      expect(place.z, closeTo(6, 1e-9));
    });

    test('hands a camera with no target to the body inside', () {
      final arm = ArmBody(inner: behind(), probe: wallAt(3));
      final place = step(arm, Vector3(1, 2, 3), null);
      expect(place.x, closeTo(1, 1e-9));
      expect(place.z, closeTo(3, 1e-9));
    });

    test('takes its framing guides from the body inside', () {
      final inner = ScreenFollowBody(screenX: 0.3, screenY: 0.6);
      final arm = ArmBody(inner: inner, probe: openAir);
      expect(arm.guides?.screenX, 0.3);
      expect(arm.guides?.screenY, 0.6);
    });
  });

  group('a camera arm over a moving target', () {
    test('never ends up beyond a wall while the target walks at it', () {
      final arm = ArmBody(
        inner: behind(damping: 0.3),
        probe: wallAt(4),
        radius: 0.25,
        height: 1,
      );
      final target = FixedTarget(Vector3(0, 0, -6));
      var place = Vector3(0, 1, 0);
      var held = 0;
      for (var frame = 0; frame < 600; frame++) {
        // The target walks towards the wall and then away from it.
        final back = frame < 300 ? frame : 600 - frame;
        target.position = Vector3(0, 0, -6 + back * 0.03);
        place = step(arm, place, target);
        if (place.z < target.position.z + 5.99) held++;
        if (target.position.z < 4 - 0.25) {
          expect(place.z, lessThanOrEqualTo(4 - 0.25 + 1e-9), reason: '$frame');
        }
      }
      // The wall was in the way for a good part of the walk, or the test
      // would pass by never meeting it.
      expect(held, greaterThan(100));
    });

    test('keeps a camera in the room while the target circles it', () {
      // The camera orbits a target standing in a room whose far wall is at
      // z = -3. Whatever the bearing, the camera is never past that wall.
      final orbit = OrbitBody(radius: 7, elevation: 10, damping: 0);
      final arm = ArmBody(
        inner: orbit,
        probe: wallAt(-3, minX: -50, maxX: 50),
        radius: 0.25,
        height: 1.5,
      );
      final target = FixedTarget(Vector3(0, 0, 0));
      var place = Vector3.zero();
      for (var degrees = 0; degrees < 360; degrees += 5) {
        orbit.azimuth = degrees.toDouble();
        place = step(arm, place, target);
        expect(
          place.z,
          greaterThanOrEqualTo(-3 + 0.25 - 1e-9),
          reason: '$degrees',
        );
      }
    });

    test('comes back out at the pace the body inside catches up', () {
      var wallUp = true;
      final arm = ArmBody(
        inner: behind(damping: 0.5),
        probe: (from, to, radius) =>
            wallUp ? wallAt(3)(from, to, radius) : null,
        height: 1,
      );
      final target = FixedTarget(Vector3.zero());
      var place = Vector3(0, 1, 6);
      for (var frame = 0; frame < 30; frame++) {
        place = step(arm, place, target);
      }
      expect(place.z, closeTo(2.75, 1e-9));

      wallUp = false;
      place = step(arm, place, target);
      expect(place.z, greaterThan(2.75));
      expect(place.z, lessThan(6), reason: 'it eases out and does not jump');
      for (var frame = 0; frame < 600; frame++) {
        place = step(arm, place, target);
      }
      expect(place.z, closeTo(6, 0.01));
    });
  });

  group('a camera arm in a shot', () {
    test('puts a cut to the shot on the near side of the wall', () {
      final shot = VirtualCamera(
        name: 'behind',
        body: ArmBody(inner: behind(damping: 0.5), probe: wallAt(3), height: 1),
        follow: FixedTarget(Vector3.zero()),
      );
      shot.snap(aspect: 16 / 9);
      expect(shot.state.position.z, closeTo(2.75, 1e-9));
    });
  });
}
