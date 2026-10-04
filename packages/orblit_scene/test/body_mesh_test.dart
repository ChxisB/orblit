import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

void main() {
  test(
    'mesh collider geometry survives JSON and copies without aliasing inputs',
    () {
      final vertices = [-1.0, 0.0, -1.0, 1.0, 0.0, -1.0, 0.0, 0.0, 1.0];
      final indices = [0, 2, 1];
      final body = BodyComponent(
        shape: BodyShape.mesh,
        meshVertices: vertices,
        meshIndices: indices,
      );
      vertices[0] = 99;
      indices[0] = 99;
      final loaded = BodyComponent.fromJson(body.toJson()).copyWith(mass: 5);
      expect(loaded.shape, BodyShape.mesh);
      expect(loaded.meshVertices.first, -1);
      expect(loaded.meshIndices, [0, 2, 1]);
      expect(() => loaded.meshIndices.add(1), throwsUnsupportedError);
    },
  );

  test(
    'malformed indices stay invalid instead of becoming a different triangle',
    () {
      final body = BodyComponent.fromJson({
        'shape': 'mesh',
        'meshIndices': [0, 'x', 2],
      });
      expect(body.meshIndices, [0, -1, 2]);
      final bad = BodyComponent.fromJson({
        'meshVertices': [0, 'x', 0, 1, 0, 0, 0, 0, 1],
      });
      expect(bad.meshVertices, isEmpty);
    },
  );
}
