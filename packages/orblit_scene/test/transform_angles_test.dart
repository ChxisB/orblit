import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  group('a transform\'s angles', () {
    test('come back as they went in', () {
      for (final angles in [
        Vector3(0, 0, 0),
        Vector3(30, 0, 0),
        Vector3(0, -45, 0),
        Vector3(10, 20, 30),
        Vector3(-170, 80, 120),
      ]) {
        final back = TransformComponent.anglesOf(
          TransformComponent.rotationOf(angles),
        );
        expect(back.x, closeTo(angles.x, 1e-9), reason: '$angles');
        expect(back.y, closeTo(angles.y, 1e-9), reason: '$angles');
        expect(back.z, closeTo(angles.z, 1e-9), reason: '$angles');
      }
    });

    test('compose Z, then Y, then X', () {
      final composed = TransformComponent.rotationOf(Vector3(90, 0, 90));
      // X first: Y goes to Z. Then Z: nothing moves Z.
      final y = composed.transformed(Vector3(0, 1, 0));
      expect(y.z, closeTo(1, 1e-12));
      // X leaves X alone, then Z turns it to Y.
      final x = composed.transformed(Vector3(1, 0, 0));
      expect(x.y, closeTo(1, 1e-12));
    });

    test('at the pole, still make the same rotation', () {
      final pole = TransformComponent.rotationOf(Vector3(25, 90, 40));
      final again = TransformComponent.rotationOf(
        TransformComponent.anglesOf(pole),
      );
      for (var i = 0; i < 9; i++) {
        expect(again.storage[i], closeTo(pole.storage[i], 1e-9));
      }
    });
  });
}
