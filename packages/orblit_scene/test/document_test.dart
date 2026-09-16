import 'dart:convert';

import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

/// A version-four scene, written the way the encoder writes one.
String sceneOf(List<Map<String, Object?>> entities, {String name = 'Scene'}) =>
    jsonEncode({
      'formatVersion': SceneDocument.formatVersion,
      'name': name,
      'entities': entities,
    });

Map<String, Object?> entity(
  String id, {
  String? name,
  String? parent,
  Map<String, Object?> components = const {},
}) => {
  'id': id,
  'name': name ?? id,
  if (parent != null) 'parent': parent,
  'components': components,
};

void main() {
  group('reading a file', () {
    test('refuses something that is not JSON at all', () {
      expect(
        () => SceneDocument.decode('not a scene'),
        throwsA(isA<SceneFormatException>()),
      );
    });

    test('refuses JSON that is not an object', () {
      expect(
        () => SceneDocument.decode('[1, 2, 3]'),
        throwsA(isA<SceneFormatException>()),
      );
    });

    test('refuses a file that does not say what version it is', () {
      expect(
        () => SceneDocument.decode('{"entities": []}'),
        throwsA(isA<SceneFormatException>()),
      );
    });

    test('refuses a file from a newer Orblit rather than guessing', () {
      expect(
        () => SceneDocument.decode(
          jsonEncode({
            'formatVersion': SceneDocument.formatVersion + 1,
            'entities': <Object?>[],
          }),
        ),
        throwsA(
          isA<SceneFormatException>().having(
            (e) => e.message,
            'message',
            contains('newer Orblit'),
          ),
        ),
      );
    });

    test('keeps the entities it can read and names the ones it cannot', () {
      final load = SceneDocument.decode(
        sceneOf([
          entity('a'),
          {'name': 'no id of its own'},
          entity('a', name: 'the second a'),
          entity('b'),
        ]),
      );

      expect(load.document.entities.map((e) => e.id), ['a', 'b']);
      expect(load.problems, hasLength(2));
      expect(load.problems.first, contains('has no id'));
      expect(load.problems.last, contains('share the id "a"'));
    });

    test('a parent that is not in the file becomes the top level', () {
      final load = SceneDocument.decode(
        sceneOf([entity('child', name: 'Lamp', parent: 'gone')]),
      );

      expect(load.document['child']!.parent, isNull);
      expect(load.problems.single, contains('"Lamp" belonged to something'));
    });

    test('a loop of parents is cut rather than hanging the outliner', () {
      final load = SceneDocument.decode(
        sceneOf([entity('a', parent: 'b'), entity('b', parent: 'a')]),
      );

      // One link is enough to cut: the first entity walked finds itself and
      // is broken out, which leaves the other hanging from a root rather than
      // from a loop. Breaking both would be the wrong answer — it would
      // scatter a nested group across the top level to fix one bad link.
      expect(load.document.roots, hasLength(1));
      expect(load.problems.single, contains('was inside itself'));

      for (final entity in load.document.entities) {
        final walked = <String>{entity.id};
        var current = load.document[entity.id]!.parent;
        while (current != null) {
          expect(walked.add(current), isTrue, reason: 'a loop survived');
          current = load.document[current]!.parent;
        }
      }
    });
  });

  group('writing a file', () {
    test('saving the same scene twice gives identical bytes', () {
      final load = SceneDocument.decode(
        sceneOf([
          entity(
            'lamp',
            components: {
              'light': const LightComponent().toJson(),
              'transform': TransformComponent().toJson(),
            },
          ),
          entity(
            'floor',
            components: {'mesh': const MeshComponent(sway: 0.4).toJson()},
          ),
        ]),
      );

      final once = load.document.encode();
      final twice = SceneDocument.decode(once).document.encode();
      final thrice = SceneDocument.decode(twice).document.encode();

      expect(twice, once);
      expect(thrice, once);
    });

    test('components are written in a fixed order, not the order read', () {
      final load = SceneDocument.decode(
        sceneOf([
          entity(
            'thing',
            components: {
              'data': const DataComponent(paths: ['a.json']).toJson(),
              'mesh': const MeshComponent().toJson(),
              'transform': TransformComponent().toJson(),
            },
          ),
        ]),
      );

      final written = load.document.encode();
      expect(
        written.indexOf('"transform"'),
        lessThan(written.indexOf('"mesh"')),
      );
      expect(written.indexOf('"mesh"'), lessThan(written.indexOf('"data"')));
    });

    test('hidden is written and visible is not', () {
      final shown = SceneEntity(id: 'a', name: 'a');
      final hidden = shown.copyWith(visible: false);

      expect(shown.toJson().containsKey('visible'), isFalse);
      expect(hidden.toJson()['visible'], false);
    });
  });

  group('components', () {
    test('one this version has never heard of survives a round trip', () {
      final load = SceneDocument.decode(
        sceneOf([
          entity(
            'thing',
            components: {
              'buoyancy': {'displacement': 3.5, 'notes': 'from a newer editor'},
            },
          ),
        ]),
      );

      final component = load.document['thing']!['buoyancy'];
      expect(component, isA<UnknownComponent>());
      expect(component!.toJson()['displacement'], 3.5);
      expect(
        SceneDocument.decode(
          load.document.encode(),
        ).document['thing']!['buoyancy']!.toJson()['notes'],
        'from a newer editor',
      );
    });

    test('a light writes every field, including the ones it is not using', () {
      final json = const LightComponent().toJson();
      expect(
        json.keys,
        containsAll(['kind', 'power', 'spotSize', 'sunAngle', 'body']),
      );
    });

    test('an entity is what it has, so a lamp can be both at once', () {
      final lamp = SceneEntity(
        id: 'lamp',
        name: 'Lamp',
        components: {
          'mesh': const MeshComponent(asset: 'lamp.glb'),
          'light': const LightComponent(power: 40),
        },
      );

      expect(lamp.has('mesh'), isTrue);
      expect(lamp.has('light'), isTrue);
    });
  });

  group('settings', () {
    test('a file that says nothing about its sky gets a new scene\'s', () {
      final load = SceneDocument.decode(sceneOf([]));
      expect(load.document.settings.sky.red, closeTo(0x1A / 255, 0.001));
    });

    test('a sky it cannot read falls back rather than going black', () {
      final load = SceneDocument.decode(
        jsonEncode({
          'formatVersion': 4,
          'sky': 'not a colour',
          'entities': <Object?>[],
        }),
      );
      expect(load.document.settings.sky.red, closeTo(0x59 / 255, 0.001));
    });
  });
}
