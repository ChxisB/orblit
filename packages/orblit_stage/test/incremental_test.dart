import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_light/orblit_light.dart' show Tint;
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_weather/orblit_weather.dart'
    show WeatherCondition, WeatherState;
import 'package:vector_math/vector_math_64.dart';

/// Scenes nobody would think to build.
///
/// The worked examples check the edits somebody imagined. This checks the ones
/// nobody did — and the incremental path is exactly where that matters, since
/// its failures are silent: a view that has been moved by four hundred diffs
/// and is one object out looks like a scene, not like a bug.
SceneDocument randomDocument(Random random, {required int size}) {
  final entities = <SceneEntity>[];
  final ids = <String>[];

  for (var i = 0; i < size; i++) {
    final id = 'e${random.nextInt(size * 2)}';
    if (ids.contains(id)) continue;

    final parent = ids.isEmpty || random.nextBool()
        ? null
        : ids[random.nextInt(ids.length)];

    final components = <String, SceneComponent>{};
    if (random.nextInt(4) != 0) {
      components[SceneComponents.transform] = TransformComponent(
        position: Vector3(
          random.nextInt(5).toDouble(),
          random.nextInt(5).toDouble(),
          random.nextInt(5).toDouble(),
        ),
        rotation: Vector3(0, random.nextInt(4) * 90, 0),
        scale: Vector3.all(random.nextInt(3) + 1),
      );
    }
    if (random.nextBool()) {
      components[SceneComponents.mesh] = MeshComponent(
        asset: random.nextBool() ? 'm${random.nextInt(3)}.glb' : null,
        colour: Tint.hex(random.nextInt(0xFFFFFF)),
        castShadows: random.nextBool(),
      );
    }
    if (random.nextInt(3) == 0) {
      components[SceneComponents.light] = LightComponent(
        power: random.nextInt(500).toDouble(),
      );
    }
    if (random.nextInt(4) == 0) {
      components[SceneComponents.sprite] = SpriteComponent(
        depth: random.nextInt(4).toDouble(),
      );
    }
    if (random.nextInt(5) == 0) {
      components[SceneComponents.camera] = CameraComponent(
        fieldOfView: 30 + random.nextInt(40).toDouble(),
      );
    }
    if (random.nextInt(6) == 0) {
      final condition = WeatherCondition
          .values[random.nextInt(WeatherCondition.values.length)];
      components[SceneComponents.weather] = WeatherComponent(
        condition: condition,
        air: WeatherState.of(condition),
      );
    }

    ids.add(id);
    entities.add(
      SceneEntity(
        id: id,
        name: id,
        parent: parent,
        visible: random.nextInt(4) != 0,
        components: components,
      ),
    );
  }

  return SceneDocument(
    settings: SceneSettings(
      sky: Tint.hex(random.nextInt(0xFFFFFF)),
      ambient: random.nextInt(5) * 1000,
    ),
    entities: entities,
  );
}

/// Everything about the staged scene that a frame actually depends on.
String shapeOf(OrblitDocumentView view) {
  final scene = view.scene;
  return [
    for (final object in scene.objects)
      'object ${object.transform.storage.join(",")} ${object.mesh} '
          '${object.visible} ${object.colour} ${object.castShadows}',
    for (final light in scene.lights)
      'light ${light.kind} ${light.intensity} ${light.position} '
          '${light.direction} ${light.colour}',
    for (final layer in scene.sprites)
      'sprites ${layer.transform.storage.join(",")} ${layer.blend}',
    for (final cloud in scene.splats) 'splats ${cloud.path} ${cloud.limit}',
    'sky ${scene.sky.colour} ${scene.sky.ambient}',
    'fog ${scene.fog.density} ${scene.fog.colour} ${scene.fog.height}',
    'rain ${scene.precipitation.amount} ${scene.precipitation.wind}',
    'camera ${scene.camera.position} ${scene.camera.target} '
        '${scene.camera.fieldOfView}',
  ].join('\n');
}

void main() {
  test('a view moved by a diff is the view built from the result', () {
    for (var seed = 0; seed < 200; seed++) {
      final random = Random(seed);
      final a = randomDocument(random, size: 1 + random.nextInt(8));
      final b = randomDocument(random, size: 1 + random.nextInt(8));

      final moved = OrblitDocumentView(a)..replace(b);
      expect(
        shapeOf(moved),
        shapeOf(OrblitDocumentView(b)),
        reason: 'seed $seed',
      );
    }
  });

  test('and stays right over a long run of edits', () {
    final random = Random(7);
    var current = randomDocument(random, size: 6);
    final view = OrblitDocumentView(current);

    for (var step = 0; step < 150; step++) {
      final next = randomDocument(random, size: 1 + random.nextInt(8));
      view.replace(next);
      current = next;
      expect(
        shapeOf(view),
        shapeOf(OrblitDocumentView(current)),
        reason: 'drifted at step $step',
      );
    }
  });
}
