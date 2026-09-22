import 'dart:math';

import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// Documents nobody would think to write by hand.
///
/// The worked examples in `diff_test.dart` check the cases somebody thought
/// of. This checks the ones nobody did: two hundred pairs of scenes that
/// differ in every way at once, each of which has to survive being turned into
/// a diff and back. A diff is what an undo stack is made of, and an undo that
/// is right for the edits the author imagined is not worth having.
///
/// Seeded, so a failure is a failure somebody else can reproduce rather than a
/// story about a build that went red once.
SceneDocument randomDocument(Random random, {required int size}) {
  final entities = <SceneEntity>[];
  final ids = <String>[];

  for (var i = 0; i < size; i++) {
    final id = 'e${random.nextInt(size * 2)}';
    if (ids.contains(id)) continue;

    // Parented only to something already placed, so the generator cannot
    // invent a loop — what a loop does to a document is decode's problem and
    // it has its own tests.
    final parent = ids.isEmpty || random.nextBool()
        ? null
        : ids[random.nextInt(ids.length)];

    final components = <String, SceneComponent>{};
    if (random.nextBool()) {
      components[SceneComponents.transform] = TransformComponent(
        position: Vector3(
          random.nextInt(5).toDouble(),
          random.nextInt(5).toDouble(),
          random.nextInt(5).toDouble(),
        ),
        scale: Vector3.all(random.nextInt(3) + 1),
      );
    }
    if (random.nextBool()) {
      components[SceneComponents.mesh] = MeshComponent(
        asset: random.nextBool() ? 'm${random.nextInt(3)}.glb' : null,
        castShadows: random.nextBool(),
        sway: random.nextInt(3) / 2,
      );
    }
    if (random.nextBool()) {
      components[SceneComponents.light] = LightComponent(
        power: random.nextInt(500).toDouble(),
      );
    }
    if (random.nextBool()) {
      components[SceneComponents.sprite] = SpriteComponent(
        depth: random.nextInt(4).toDouble(),
      );
    }
    if (random.nextBool()) {
      components[SceneComponents.body] = BodyComponent(
        shape: BodyShape.values[random.nextInt(BodyShape.values.length)],
        motion: BodyMotion.values[random.nextInt(BodyMotion.values.length)],
        mass: random.nextInt(4) + 1,
      );
    }
    if (random.nextInt(5) == 0) {
      components['unheardof'] = UnknownComponent('unheardof', {
        'value': random.nextInt(9),
      });
    }

    ids.add(id);
    entities.add(
      SceneEntity(
        id: id,
        name: 'name${random.nextInt(3)}',
        parent: parent,
        visible: random.nextInt(4) != 0,
        components: components,
      ),
    );
  }

  return SceneDocument(
    name: 'scene${random.nextInt(2)}',
    settings: SceneSettings(
      ambient: random.nextInt(5) * 1000,
      timeOfDay: random.nextInt(24).toDouble(),
      dayCycle: random.nextBool(),
    ),
    entities: entities,
  );
}

void main() {
  test('any two documents diff, apply and invert', () {
    for (var seed = 0; seed < 200; seed++) {
      final random = Random(seed);
      final a = randomDocument(random, size: 1 + random.nextInt(8));
      final b = randomDocument(random, size: 1 + random.nextInt(8));

      final diff = SceneDiff.between(a, b);
      final forward = diff.applyTo(a);
      expect(
        forward.encode(),
        b.encode(),
        reason: 'seed $seed: the diff did not arrive at b',
      );
      expect(
        diff.inverse.applyTo(forward).encode(),
        a.encode(),
        reason: 'seed $seed: the inverse did not get back to a',
      );
    }
  });

  test('a diff stored as JSON does the same as the one that made it', () {
    for (var seed = 0; seed < 200; seed++) {
      final random = Random(seed);
      final a = randomDocument(random, size: 1 + random.nextInt(8));
      final b = randomDocument(random, size: 1 + random.nextInt(8));

      final stored = SceneDiff.fromJson(SceneDiff.between(a, b).toJson());
      expect(stored.applyTo(a).encode(), b.encode(), reason: 'seed $seed');
      expect(
        stored.inverse.applyTo(stored.applyTo(a)).encode(),
        a.encode(),
        reason: 'seed $seed',
      );
    }
  });
}
