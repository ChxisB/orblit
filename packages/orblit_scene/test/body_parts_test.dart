import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  test('compound geometry round trips without losing local placement', () {
    final part = BodyPart(
      shape: BodyShape.cylinder,
      radius: 0.3,
      height: 2,
      centre: Vector3(1, 2, 3),
      scale: Vector3(2, 1, 0.5),
      rotation: Quaternion.axisAngle(Vector3(0, 0, 1), 0.7),
    );
    final body = BodyComponent(
      shape: BodyShape.compound,
      parts: [part],
      centre: Vector3(0, 1, 0),
      shapeScale: Vector3(1, 2, 1),
    );
    final restored = BodyComponent.fromJson(body.toJson());
    expect(restored.toJson(), body.toJson());
    expect(restored.copyWith(mass: 3).parts.single.toJson(), part.toJson());
    expect(restored.shapeScale, Vector3(1, 2, 1));
  });

  test('old files keep their shape and geometry scale defaults', () {
    final body = BodyComponent.fromJson({'shape': 'sphere', 'radius': 2});
    expect(body.shape, BodyShape.sphere);
    expect(body.shapeScale, Vector3.all(1));
    expect(body.parts, isEmpty);
    final part = BodyPart.fromJson({
      'centre': [3, 4],
      'rotation': 'bad',
    });
    expect(part.centre, Vector3(3, 4, 0));
    expect(part.rotation, Quaternion.identity());
    expect(part.scale, Vector3.all(1));
  });

  test(
    'the part and hull lists are immutable and constructor inputs are copied',
    () {
      final centre = Vector3(1, 0, 0);
      final points = <double>[0, 1, 2];
      final parts = [BodyPart(centre: centre, hull: points)];
      final body = BodyComponent(shape: BodyShape.compound, parts: parts);
      centre.x = 7;
      points[0] = 9;
      parts.clear();
      expect(body.parts.single.centre.x, 1);
      expect(body.parts.single.hull.first, 0);
      expect(() => body.parts.clear(), throwsUnsupportedError);
      expect(() => body.parts.single.hull.clear(), throwsUnsupportedError);
    },
  );
}
