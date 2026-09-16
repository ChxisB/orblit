import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/orblit_examples.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// The gallery loading a scene file, with no editor in the process.
///
/// This is the criterion Phase 2 was written against, so it is checked rather
/// than demonstrated. An example that claims to draw a scene *file* is only
/// worth having if the file is genuinely being read, and "it looked right on
/// my machine" is not a test.
void main() {
  OrblitCamera anywhere() =>
      OrblitCamera(position: Vector3(0, 2, 10), target: Vector3.zero());

  OrblitScene sceneOf(SceneFilesExample example) =>
      example.scene(anywhere(), 0);

  test('the 3D document becomes lit geometry under weather', () {
    final example = SceneFilesExample();
    final scene = sceneOf(example);

    // A floor, two stacked crates and a loose one.
    expect(scene.objects, hasLength(4));
    expect(scene.lights, hasLength(1));
    expect(scene.lights.single.kind, OrblitLightKind.directional);
    expect(scene.fog.density, greaterThan(0));
    expect(example.note, isNull, reason: 'the document read cleanly');
  });

  test('a stacked crate is placed by its parent as well as by itself', () {
    // The stack sits at (-3, 0.5), and the upper crate at (0, 1.05) within
    // it. The sum, 1.55, appears nowhere in the file — so a crate at
    // (-3, 1.55) can only have come from the two being composed.
    final upper = sceneOf(SceneFilesExample()).objects
        .firstWhere((object) => object.transform.getTranslation().y > 1);

    expect(upper.transform.getTranslation().x, closeTo(-3, 0.001));
    expect(upper.transform.getTranslation().y, closeTo(1.55, 0.001));
  });

  test('the 2D document becomes sprite layers and no geometry', () {
    final example = SceneFilesExample()..flat = true;
    final scene = sceneOf(example);

    expect(scene.objects, isEmpty);
    expect(scene.sprites, hasLength(5));
    // The spark is parented to the coin, two units left of centre.
    expect(
      scene.sprites.any(
        (layer) => (layer.transform.getTranslation().x + 2).abs() < 0.001,
      ),
      isTrue,
    );
    expect(example.note, isNull);
  });

  test('switching back re-reads the other document', () {
    final example = SceneFilesExample()..flat = true;
    expect(sceneOf(example).objects, isEmpty);

    example.flat = false;
    expect(sceneOf(example).objects, hasLength(4));
  });

  test('an edit moves one entity, as a diff rather than a rebuild', () {
    final example = SceneFilesExample();
    final before = [for (final object in sceneOf(example).objects) object.key];

    example.liftTo(3);
    final after = sceneOf(example);

    expect(
      after.objects.map((object) => object.key),
      before,
      reason: 'the renderer keeps what it already built',
    );
    expect(
      after.objects.any(
        (object) => (object.transform.getTranslation().y - 3).abs() < 0.001,
      ),
      isTrue,
      reason: 'the entity the edit named actually moved',
    );
    expect(
      example.lastOperations,
      1,
      reason: 'moving one thing along one axis is one operation',
    );
  });
}
