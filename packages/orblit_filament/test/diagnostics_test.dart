import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

OrblitObject thing(
  int key, {
  int? material,
  String? mesh,
  bool visible = true,
  Matrix4? at,
}) => OrblitObject(
  key: key,
  transform: at ?? Matrix4.identity(),
  colour: Vector3(1, 0, 0),
  material: material,
  mesh: mesh,
  visible: visible,
);

final brass = OrblitMaterial(
  key: 7,
  metallic: 1,
  roughness: 0.25,
  baseColour: Vector4(0.5, 0.25, 0.125, 1),
  baseColourMap: const OrblitTexture('/tex/brass.png'),
);

final camera = OrblitCamera(position: Vector3(0, 2, 8), target: Vector3.zero());

OrblitScene scene() => OrblitScene(
  objects: [
    thing(1, material: 7, at: Matrix4.translation(Vector3(1, 2, 3))),
    thing(2, material: 7, mesh: '/models/rock.glb'),
    thing(3, material: 99),
    thing(4, visible: false),
  ],
  populations: [
    OrblitPopulation(
      key: 1,
      transforms: Float32List(3 * 16),
      colours: Float32List(3 * 3),
      minimum: Vector3(-10, 0, -10),
      maximum: Vector3(10, 4, 10),
      range: 50,
      revision: 5,
    ),
  ],
  materials: [brass],
  camera: camera,
);

void main() {
  group('drawRows', () {
    test('has a row for each object, then each population', () {
      expect(scene().drawRows().map((row) => row['id']), [
        'object:1',
        'object:2',
        'object:3',
        'object:4',
        'population:1',
      ]);
    });

    test('an object row says what it is made of and where it is', () {
      final rows = scene().drawRows();
      expect(rows[0]['kind'], 'object');
      expect(rows[0]['mesh'], isNull);
      expect(rows[0]['material'], 7);
      expect(rows[0]['materialListed'], isTrue);
      expect(rows[0]['instances'], 1);
      expect((rows[0]['transform']! as List<double>).sublist(12, 15), [
        1,
        2,
        3,
      ]);
      expect(rows[1]['mesh'], '/models/rock.glb');
    });

    test('says when an object names a material the scene does not list', () {
      final rows = scene().drawRows();
      expect(rows[2]['material'], 99);
      expect(rows[2]['materialListed'], isFalse);
    });

    test('a row with no material has no answer to give about one', () {
      final rows = scene().drawRows();
      expect(rows[3]['material'], isNull);
      expect(rows[3]['materialListed'], isNull);
      expect(rows.last['materialListed'], isNull);
    });

    test('reports the layer the renderer is sent', () {
      final rows = OrblitScene(
        objects: [
          OrblitObject(
            key: 1,
            transform: Matrix4.identity(),
            colour: Vector3(1, 0, 0),
            layer: 9,
          ),
        ],
        camera: camera,
      ).drawRows();
      expect(rows.single['layer'], OrblitScene.maxLayer);
    });

    test('a hidden object is listed and says it is hidden', () {
      expect(scene().drawRows()[3]['visible'], isFalse);
      expect(scene().drawRows()[0]['visible'], isTrue);
    });

    test('a population row counts its members and keeps its range', () {
      final row = scene().drawRows().last;
      expect(row['kind'], 'population');
      expect(row['instances'], 3);
      expect(row['range'], 50);
      expect(row['fade'], 'sink');
      expect(row['revision'], 5);
      expect(row['minimum'], [-10, 0, -10]);
      expect(row['maximum'], [10, 4, 10]);
    });
  });

  group('materialRow', () {
    test('lists the values the renderer is sent and the draws on it', () {
      final row = scene().materialRow(7)!;
      expect(row['key'], 7);
      expect(row['usedBy'], ['object:1', 'object:2']);
      final values = row['values']! as Map<String, Object?>;
      expect(values['metallic'], 1);
      expect(values['roughness'], 0.25);
      expect(values['baseColour.g'], 0.25);
      expect(row['maps'], {
        'baseColour': {'path': '/tex/brass.png', 'srgb': true},
      });
      expect(row['shading'], 'lit');
      expect(row['blendMode'], 'none');
    });

    test('answers the last material when a key is listed twice', () {
      final twice = OrblitScene(
        objects: const [],
        camera: camera,
        materials: [
          OrblitMaterial(key: 7),
          OrblitMaterial(key: 7, metallic: 1),
        ],
      );
      final values = twice.materialRow(7)!['values']! as Map<String, Object?>;
      expect(values['metallic'], 1);
    });

    test('is null for a key the scene does not hold', () {
      expect(scene().materialRow(99), isNull);
    });

    test('names every float of the packed material', () {
      // A float added to pack without a name would go unread here.
      expect(OrblitMaterial.packedNames, hasLength(OrblitMaterial.stride));
      expect(OrblitMaterial.mapNames, hasLength(OrblitMaterial.mapCount));
    });

    test('each name is the field pack writes at its place', () {
      // Every float gets its own value, so a name out of step with pack shows.
      final distinct = {
        for (final (at, name) in OrblitMaterial.packedNames.indexed)
          name: (at + 1) / 64,
      };
      double v(String name) => distinct[name]!;
      final material = OrblitMaterial(
        key: 1,
        baseColour: Vector4(
          v('baseColour.r'),
          v('baseColour.g'),
          v('baseColour.b'),
          v('baseColour.a'),
        ),
        metallic: v('metallic'),
        roughness: v('roughness'),
        reflectance: v('reflectance'),
        emissive: Vector3(v('emissive.r'), v('emissive.g'), v('emissive.b')),
        emissiveIntensity: v('emissiveIntensity'),
        ambientOcclusion: v('ambientOcclusion'),
        normalScale: v('normalScale'),
        tiling: Vector2(v('tiling.x'), v('tiling.y')),
        offset: Vector2(v('offset.x'), v('offset.y')),
        maskThreshold: v('maskThreshold'),
        depthBias: v('depthBias'),
        blendAmount: v('blendAmount'),
        blendSharpness: v('blendSharpness'),
        blendTiling: Vector2(v('blendTiling.x'), v('blendTiling.y')),
        blendOffset: Vector2(v('blendOffset.x'), v('blendOffset.y')),
        clearCoat: v('clearCoat'),
        clearCoatRoughness: v('clearCoatRoughness'),
        anisotropy: v('anisotropy'),
        sheenColour: Vector3(
          v('sheenColour.r'),
          v('sheenColour.g'),
          v('sheenColour.b'),
        ),
        sheenRoughness: v('sheenRoughness'),
      );
      final values = material.toJson()['values']! as Map<String, Object?>;
      // The blend mode and the wind are not plain fields, so they have their
      // own checks.
      for (final name in distinct.keys) {
        if (name == 'blendMode' || name.startsWith('wind')) continue;
        expect(values[name], distinct[name], reason: name);
      }
    });
  });

  test('a pass timing is a row of its name, time and draws', () {
    const pass = OrblitPassTiming(name: 'colour', milliseconds: 1.5, draws: 12);
    expect(pass.toJson(), {'name': 'colour', 'milliseconds': 1.5, 'draws': 12});
  });
}
