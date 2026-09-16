import 'dart:convert';
import 'dart:math' as math;

import 'package:orblit_light/orblit_light.dart' show LightType;
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_weather/orblit_weather.dart' show WeatherCondition;
import 'package:test/test.dart';

/// A file at an older version, as one was actually written.
String oldScene(int version, Map<String, Object?> rest) =>
    jsonEncode({'formatVersion': version, ...rest});

void main() {
  group('the shape that predates versions', () {
    test('names what it could not recover instead of stacking it silently', () {
      final load = SceneDocument.decode(
        oldScene(1, {
          'name': 'Old',
          'entities': [
            {
              'name': 'Key',
              'components': ['Light', 'Transform'],
            },
            {
              'name': 'Eye',
              'components': ['Camera'],
            },
            {
              'name': 'Crate',
              'components': ['MeshRenderer'],
            },
          ],
        }),
      );

      final document = load.document;
      expect(document.name, 'Old');
      expect(document.entities.map((e) => e.id), [
        'legacy0',
        'legacy1',
        'legacy2',
      ]);
      expect(document['legacy0']!.has(SceneComponents.light), isTrue);
      expect(document['legacy1']!.has(SceneComponents.camera), isTrue);
      expect(document['legacy2']!.has(SceneComponents.mesh), isTrue);
      expect(load.problems.single, contains('all at the origin'));
    });
  });

  group('version one, where a light was stated in watts', () {
    test(
      'a sun keeps its brightness by losing the sphere it was spread on',
      () {
        final load = SceneDocument.decode(
          oldScene(1, {
            'objects': [
              {
                'id': 'sun',
                'kind': 'light',
                'lightType': 'sun',
                'power': 1000.0,
              },
            ],
          }),
        );

        final light =
            load.document['sun']![SceneComponents.light]! as LightComponent;
        expect(light.power, closeTo(1000 / (4 * math.pi), 1e-9));
        expect(load.problems.single, contains('watts per square metre'));
      },
    );

    test('an unnamed light was drawn as a sun, so it is converted as one', () {
      final load = SceneDocument.decode(
        oldScene(1, {
          'objects': [
            {'id': 'light', 'kind': 'light'},
          ],
        }),
      );

      final light =
          load.document['light']![SceneComponents.light]! as LightComponent;
      expect(light.kind, LightType.sun);
      // The default power of a thousand, over the same sphere.
      expect(light.power, closeTo(1000 / (4 * math.pi), 1e-9));
    });

    test('a point light is left alone, because it always was a bulb', () {
      final load = SceneDocument.decode(
        oldScene(1, {
          'objects': [
            {
              'id': 'bulb',
              'kind': 'light',
              'lightType': 'point',
              'power': 60.0,
            },
          ],
        }),
      );

      final light =
          load.document['bulb']![SceneComponents.light]! as LightComponent;
      expect(light.power, 60.0);
    });
  });

  group('version two, where the air was a block on the scene', () {
    test('fog becomes a weather entity that can hold a change', () {
      final load = SceneDocument.decode(
        oldScene(2, {
          'objects': <Object?>[],
          'fog': {
            'colour': '#405060',
            'density': 0.4,
            'height': 12.0,
            'falloff': 0.3,
            'mist': 0.5,
            'mistSize': 20.0,
            'mistSpeed': 0.25,
          },
        }),
      );

      final weather =
          load.document['weather']![SceneComponents.weather]!
              as WeatherComponent;
      expect(weather.condition, WeatherCondition.misty);
      expect(weather.air.fogDensity, 0.4);
      expect(weather.air.mist, 0.5);
      // Nothing in the old shape said anything about cloud.
      expect(weather.air.cloudCover, 0);
      // The old drift was a rate, not a speed: four metres a second per unit.
      expect(weather.air.windSpeed, closeTo(1.0, 1e-9));
      expect(load.problems.single, contains('now a Weather object'));
    });

    test('fog with no mist in it is hazy rather than misty', () {
      final load = SceneDocument.decode(
        oldScene(2, {
          'objects': <Object?>[],
          'fog': {'density': 0.2},
        }),
      );

      final weather =
          load.document['weather']![SceneComponents.weather]!
              as WeatherComponent;
      expect(weather.condition, WeatherCondition.hazy);
    });

    test('a scene with no fog in it gains nothing', () {
      final load = SceneDocument.decode(
        oldScene(2, {
          'objects': [
            {'id': 'crate', 'kind': 'mesh'},
          ],
          'fog': {'density': 0.0},
        }),
      );

      expect(load.document.entities.map((e) => e.id), ['crate']);
      expect(load.problems, isEmpty);
    });

    test('the invented entity does not take an id something else is using', () {
      final load = SceneDocument.decode(
        oldScene(2, {
          'objects': [
            {'id': 'weather', 'kind': 'mesh'},
          ],
          'fog': {'density': 0.3},
        }),
      );

      expect(load.document.entities.map((e) => e.id), ['weather', 'weather2']);
      expect(load.document['weather2']!.has(SceneComponents.weather), isTrue);
    });
  });

  group('version three, where an object said what it was', () {
    test('a mesh becomes an entity with a transform and a mesh', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {
              'id': 'floor',
              'name': 'Floor',
              'kind': 'mesh',
              'position': [0.0, -1.0, 0.0],
              'scale': [30.0, 1.0, 30.0],
              'colour': '#8899AA',
              'mesh': 'models/floor.glb',
              'material': 'materials/stone.omat',
              'castShadows': false,
              'sway': 0.25,
              'data': ['scripts/floor.js'],
            },
          ],
        }),
      );

      final entity = load.document['floor']!;
      expect(entity.name, 'Floor');

      final transform =
          entity[SceneComponents.transform]! as TransformComponent;
      expect(transform.position.y, -1.0);
      expect(transform.scale.x, 30.0);

      final mesh = entity[SceneComponents.mesh]! as MeshComponent;
      expect(mesh.asset, 'models/floor.glb');
      expect(mesh.castShadows, isFalse);
      expect(mesh.receiveShadows, isTrue);
      expect(mesh.sway, 0.25);
      expect(mesh.colour.red, closeTo(0x88 / 255, 0.001));

      expect(
        (entity[SceneComponents.material]! as MaterialComponent).asset,
        'materials/stone.omat',
      );
      expect((entity[SceneComponents.data]! as DataComponent).paths, [
        'scripts/floor.js',
      ]);
      expect(load.problems, isEmpty);
    });

    test('a shape is drawn too, so it gets a mesh component as well', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {'id': 'box', 'kind': 'shape'},
          ],
        }),
      );

      expect(load.document['box']!.has(SceneComponents.mesh), isTrue);
    });

    test('a light keeps every field, including the ones it is not using', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {
              'id': 'spot',
              'kind': 'light',
              'lightType': 'spot',
              'power': 60.0,
              'spotSize': 30.0,
              'colour': '#FFEEDD',
              'castShadows': false,
            },
          ],
        }),
      );

      final light =
          load.document['spot']![SceneComponents.light]! as LightComponent;
      expect(light.kind, LightType.spot);
      expect(light.power, 60.0);
      expect(light.spotSize, 30.0);
      expect(light.castShadows, isFalse);
      // A spot has no sun angle, and it is written anyway, so flipping this
      // light to a sun and back does not lose what it was.
      expect(light.sunAngle, 0.526);
      expect(light.colour.blue, closeTo(0xDD / 255, 0.001));

      // A light is not drawn, so it gets no mesh.
      expect(load.document['spot']!.has(SceneComponents.mesh), isFalse);
    });

    test('a group is a place in the tree and nothing else', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {'id': 'props', 'kind': 'group'},
            {'id': 'crate', 'kind': 'mesh', 'parent': 'props'},
          ],
        }),
      );

      expect(load.document['props']!.components.keys, [
        SceneComponents.transform,
      ]);
      expect(load.document.childrenOf('props').single.id, 'crate');
    });

    test('a kind this editor does not know is dropped and named', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {'id': 'crate', 'kind': 'mesh'},
            {'id': 'portal', 'kind': 'wormhole'},
          ],
        }),
      );

      expect(load.document.entities.map((e) => e.id), ['crate']);
      expect(load.problems.single, contains('"wormhole"'));
    });

    test('hidden survives the conversion', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {'id': 'crate', 'kind': 'mesh', 'visible': false},
          ],
        }),
      );

      expect(load.document['crate']!.visible, isFalse);
    });

    test('the scene keeps its own settings across the conversion', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'name': 'Courtyard',
          'sky': '#223344',
          'ambient': 12000.0,
          'time': {'hour': 18.5, 'cycle': true, 'hoursPerSecond': 0.25},
          'objects': <Object?>[],
        }),
      );

      final settings = load.document.settings;
      expect(load.document.name, 'Courtyard');
      expect(settings.sky.green, closeTo(0x33 / 255, 0.001));
      expect(settings.ambient, 12000.0);
      expect(settings.timeOfDay, 18.5);
      expect(settings.dayCycle, isTrue);
      expect(settings.hoursPerSecond, 0.25);
    });

    test('a file with no objects list in it is refused', () {
      expect(
        () => SceneDocument.decode(oldScene(3, {'name': 'Nothing'})),
        throwsA(isA<SceneFormatException>()),
      );
    });
  });

  group('what a drawable is made of', () {
    test('a shape says the geometry is this document\'s to edit', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {'id': 'arch', 'kind': 'shape'},
            {'id': 'model', 'kind': 'mesh', 'mesh': 'models/tree.glb'},
          ],
        }),
      );

      final arch =
          load.document['arch']![SceneComponents.mesh]! as MeshComponent;
      final model =
          load.document['model']![SceneComponents.mesh]! as MeshComponent;
      expect(arch.authored, isTrue);
      expect(model.authored, isFalse);

      // And it survives the file, which is the whole point of stating it:
      // guessed from which fields are filled in, an untouched shape looks
      // exactly like a referenced model that names nothing.
      final again = SceneDocument.decode(load.document.encode()).document;
      expect(
        (again['arch']![SceneComponents.mesh]! as MeshComponent).authored,
        isTrue,
      );
      expect(
        (again['model']![SceneComponents.mesh]! as MeshComponent).authored,
        isFalse,
      );
    });

    test('a placed model says nothing, so it stays out of the diff', () {
      expect(const MeshComponent().toJson().containsKey('authored'), isFalse);
    });
  });

  group('one object on its own, for a clipboard or a prefab', () {
    test('runs the same chain a file does', () {
      final notes = <String>[];
      final entity = SceneMigrations.entity(
        {'id': 'sun', 'kind': 'light', 'lightType': 'sun', 'power': 1000.0},
        version: 1,
        notes: notes,
      );

      final light =
          SceneEntity.fromJson(entity!)![SceneComponents.light]!
              as LightComponent;
      // Version two's conversion, applied to something that was never in a
      // file — which is exactly what a prefab written that long ago is.
      expect(light.power, closeTo(1000 / (4 * math.pi), 1e-9));
      expect(notes, isNotEmpty);
    });

    test('a current one is handed back as it came', () {
      final already = {
        'id': 'crate',
        'name': 'Crate',
        'components': {'mesh': const MeshComponent().toJson()},
      };
      expect(SceneMigrations.entity(already, version: 4), same(already));
    });

    test('something with nothing usable in it comes back null', () {
      expect(SceneMigrations.entity({'kind': 'mesh'}, version: 3), isNull);
    });
  });

  group('every version arrives at the same place', () {
    test('a version-one file runs the whole chain', () {
      final load = SceneDocument.decode(
        oldScene(1, {
          'objects': [
            {'id': 'sun', 'kind': 'light', 'lightType': 'sun', 'power': 1000.0},
          ],
          'fog': {'density': 0.3, 'mist': 0.2},
        }),
      );

      final light =
          load.document['sun']![SceneComponents.light]! as LightComponent;
      // Converted by version two...
      expect(light.power, closeTo(1000 / (4 * math.pi), 1e-9));
      // ...given a home by version three...
      expect(load.document['weather']!.has(SceneComponents.weather), isTrue);
      // ...and made components by version four.
      expect(load.document['sun']!.has(SceneComponents.transform), isTrue);
    });

    test('a converted scene saves as version four and reloads unchanged', () {
      final load = SceneDocument.decode(
        oldScene(3, {
          'objects': [
            {
              'id': 'crate',
              'kind': 'mesh',
              'position': [1.0, 2.0, 3.0],
            },
          ],
        }),
      );

      final saved = load.document.encode();
      expect(jsonDecode(saved), containsPair('formatVersion', 4));

      final again = SceneDocument.decode(saved);
      expect(again.problems, isEmpty);
      expect(again.document.encode(), saved);
    });
  });
}
