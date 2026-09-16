import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

SceneDocument documentOf(
  List<SceneEntity> entities, {
  String name = 'Scene',
  SceneSettings settings = const SceneSettings(),
}) => SceneDocument(name: name, settings: settings, entities: entities);

SceneEntity thing(
  String id, {
  String? name,
  String? parent,
  bool visible = true,
  Map<String, SceneComponent> components = const {},
}) => SceneEntity(
  id: id,
  name: name ?? id,
  parent: parent,
  visible: visible,
  components: components,
);

/// What the two criteria actually say: the diff gets you there, and its
/// inverse gets you back. Checked on the encoded bytes, because that is the
/// document as it is actually kept.
void expectRoundTrip(SceneDocument a, SceneDocument b) {
  final diff = SceneDiff.between(a, b);
  expect(
    diff.applyTo(a).encode(),
    b.encode(),
    reason: 'diff(a, b) applied to a',
  );
  expect(
    diff.inverse.applyTo(diff.applyTo(a)).encode(),
    a.encode(),
    reason: 'the inverse applied to b',
  );
}

void main() {
  group('what changed', () {
    test('nothing, between a document and itself', () {
      final document = documentOf([thing('a'), thing('b')]);
      expect(SceneDiff.between(document, document).isEmpty, isTrue);
    });

    test('a field, as one operation rather than the whole entity', () {
      final a = documentOf([
        thing(
          'crate',
          components: {SceneComponents.transform: TransformComponent()},
        ),
      ]);
      final b = documentOf([
        thing(
          'crate',
          components: {
            SceneComponents.transform: TransformComponent(
              position: Vector3(0, 3, 0),
            ),
          },
        ),
      ]);

      final diff = SceneDiff.between(a, b);
      expect(diff.operations.single, isA<SetField>());
      expect((diff.operations.single as SetField).field, 'position');
      expectRoundTrip(a, b);
    });

    test('a component gained, which is not the same as a field changing', () {
      final a = documentOf([thing('lamp')]);
      final b = documentOf([
        thing(
          'lamp',
          components: {SceneComponents.light: const LightComponent(power: 40)},
        ),
      ]);

      final diff = SceneDiff.between(a, b);
      expect(diff.operations.single, isA<SetComponent>());
      expect((diff.operations.single as SetComponent).from, isNull);
      expectRoundTrip(a, b);
    });

    test('a component lost, and undoing it brings back what it held', () {
      final a = documentOf([
        thing(
          'lamp',
          components: {
            SceneComponents.light: const LightComponent(
              power: 40,
              spotSize: 12,
            ),
          },
        ),
      ]);
      final b = documentOf([thing('lamp')]);

      expectRoundTrip(a, b);

      final back = SceneDiff.between(a, b).inverse.applyTo(b);
      final light = back['lamp']![SceneComponents.light]! as LightComponent;
      expect(light.power, 40);
      expect(light.spotSize, 12);
    });

    test('a rename', () {
      final a = documentOf([thing('crate', name: 'Crate')]);
      final b = documentOf([thing('crate', name: 'Barrel')]);

      expect(SceneDiff.between(a, b).operations.single, isA<SetEntityName>());
      expectRoundTrip(a, b);
    });

    test('hiding something', () {
      final a = documentOf([thing('crate')]);
      final b = documentOf([thing('crate', visible: false)]);

      expect(SceneDiff.between(a, b).operations.single, isA<SetVisible>());
      expectRoundTrip(a, b);
    });

    test('the scene\'s own settings, and its name', () {
      final a = documentOf([], name: 'Scene');
      final b = documentOf(
        [],
        name: 'Courtyard',
        settings: const SceneSettings(
          ambient: 4000,
          timeOfDay: 21,
          dayCycle: true,
        ),
      );

      expect(SceneDiff.between(a, b).operations, hasLength(4));
      expectRoundTrip(a, b);
    });
  });

  group('the tree', () {
    test('adding an entity puts it where it belongs, not at the end', () {
      final a = documentOf([thing('a'), thing('c')]);
      final b = documentOf([thing('a'), thing('b'), thing('c')]);

      expect(SceneDiff.between(a, b).applyTo(a).entities.map((e) => e.id), [
        'a',
        'b',
        'c',
      ]);
      expectRoundTrip(a, b);
    });

    test('adding at the head', () {
      final a = documentOf([thing('b')]);
      final b = documentOf([thing('a'), thing('b')]);
      expectRoundTrip(a, b);
    });

    test('removing several in a row, and putting them all back', () {
      final a = documentOf([thing('a'), thing('b'), thing('c'), thing('d')]);
      final b = documentOf([thing('a'), thing('d')]);
      expectRoundTrip(a, b);
    });

    test('reparenting', () {
      final a = documentOf([thing('props'), thing('crate')]);
      final b = documentOf([thing('props'), thing('crate', parent: 'props')]);

      expect(SceneDiff.between(a, b).operations.single, isA<Reparent>());
      expectRoundTrip(a, b);
    });

    test('reordering siblings', () {
      final a = documentOf([
        thing('props'),
        thing('crate', parent: 'props'),
        thing('barrel', parent: 'props'),
      ]);
      final b = documentOf([
        thing('props'),
        thing('barrel', parent: 'props'),
        thing('crate', parent: 'props'),
      ]);

      expect(
        SceneDiff.between(a, b).applyTo(a).siblingsOf('props').map((e) => e.id),
        ['barrel', 'crate'],
      );
      expectRoundTrip(a, b);
    });

    test('a reversal of everything', () {
      final a = documentOf([thing('a'), thing('b'), thing('c'), thing('d')]);
      final b = documentOf([thing('d'), thing('c'), thing('b'), thing('a')]);
      expectRoundTrip(a, b);
    });

    test('all of it at once', () {
      final a = documentOf([
        thing('group'),
        thing(
          'crate',
          name: 'Crate',
          parent: 'group',
          components: {
            SceneComponents.transform: TransformComponent(),
            SceneComponents.mesh: const MeshComponent(asset: 'crate.glb'),
          },
        ),
        thing(
          'lamp',
          components: {SceneComponents.light: const LightComponent(power: 100)},
        ),
        thing('old'),
      ]);
      final b = documentOf([
        thing('new'),
        thing(
          'lamp',
          components: {
            SceneComponents.light: const LightComponent(power: 250),
            SceneComponents.mesh: const MeshComponent(asset: 'lamp.glb'),
          },
        ),
        thing('group'),
        thing(
          'crate',
          name: 'Barrel',
          visible: false,
          components: {
            SceneComponents.transform: TransformComponent(
              position: Vector3(1, 0, 2),
            ),
          },
        ),
      ], name: 'Courtyard');

      expectRoundTrip(a, b);
    });
  });

  group('a diff as something that can be stored', () {
    test('survives being written out and read back', () {
      final a = documentOf([
        thing(
          'crate',
          components: {SceneComponents.transform: TransformComponent()},
        ),
        thing('gone'),
      ]);
      final b = documentOf([
        thing(
          'crate',
          name: 'Barrel',
          components: {
            SceneComponents.transform: TransformComponent(
              position: Vector3(4, 0, 0),
            ),
            SceneComponents.mesh: const MeshComponent(),
          },
        ),
        thing('added'),
      ]);

      final diff = SceneDiff.between(a, b);
      final stored = SceneDiff.fromJson(diff.toJson());

      expect(stored.operations, hasLength(diff.operations.length));
      expect(stored.applyTo(a).encode(), b.encode());
      expect(stored.inverse.applyTo(stored.applyTo(a)).encode(), a.encode());
    });

    test('an unknown component diffs by its fields like any other', () {
      final a = documentOf([
        thing(
          'thing',
          components: {
            'buoyancy': const UnknownComponent('buoyancy', {
              'displacement': 1.0,
            }),
          },
        ),
      ]);
      final b = documentOf([
        thing(
          'thing',
          components: {
            'buoyancy': const UnknownComponent('buoyancy', {
              'displacement': 9.0,
            }),
          },
        ),
      ]);

      final diff = SceneDiff.between(a, b);
      expect(diff.operations.single, isA<SetField>());
      expectRoundTrip(a, b);
    });
  });
}
