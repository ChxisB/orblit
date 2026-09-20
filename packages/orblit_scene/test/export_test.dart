import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

SceneEntity entity(
  String id,
  Map<String, SceneComponent> components, {
  String? parent,
  String? name,
  bool visible = true,
}) => SceneEntity(
  id: id,
  name: name ?? id,
  parent: parent,
  visible: visible,
  components: components,
);

Map<String, Object?> headerOf(Uint8List glb) => glbChunks(glb)!.json;

Uint8List cubeGlb() => Uint8List.fromList(
  Shape.of(ShapeKind.cube).build().toGlb(
    name: 'Block',
    materials: const [GlbMaterial(name: 'Paint')],
  ),
);

void main() {
  group('the tree it writes', () {
    test('an empty scene is a file every loader opens', () {
      final written = SceneDocument(name: 'Nothing').writeAs(SceneFormat.glb);
      final json = headerOf(written.first.bytes);
      expect(json['scenes'], hasLength(1));
      // Not an empty list: every array in glTF must hold something.
      expect((json['scenes']! as List).first, isNot(contains('nodes')));
      expect(json.containsKey('buffers'), isFalse);
      expect(json.containsKey('accessors'), isFalse);
    });

    test('a child is a child, and only the roots are in the scene', () {
      final scene = SceneDocument(
        entities: [
          entity('a', const {}),
          entity('b', const {}, parent: 'a'),
          entity('c', const {}, parent: 'b'),
          entity('d', const {}),
        ],
      );
      final json = headerOf(scene.writeAs(SceneFormat.glb).first.bytes);
      final nodes = json['nodes']! as List;
      expect((json['scenes']! as List).first, containsPair('nodes', [0, 3]));
      expect(nodes[0], containsPair('children', [1]));
      expect(nodes[1], containsPair('children', [2]));
      expect(nodes[2], isNot(contains('children')));
    });

    test('an entity whose parent is not in the scene is a root', () {
      final scene = SceneDocument(
        entities: [entity('orphan', const {}, parent: 'gone')],
      );
      final json = headerOf(scene.writeAs(SceneFormat.glb).first.bytes);
      expect((json['scenes']! as List).first, containsPair('nodes', [0]));
    });

    test('a rotation is the one the scene draws, in the order it draws it', () {
      // Three axes at once, because the order only shows itself when more
      // than one is turned: any order agrees about a single axis.
      final euler = Vector3(20, 40, 60);
      final scene = SceneDocument(
        entities: [
          entity('e', {
            SceneComponents.transform: TransformComponent(
              position: Vector3(1, 2, 3),
              rotation: euler,
              scale: Vector3(2, 2, 2),
            ),
          }),
        ],
      );
      final node =
          (headerOf(scene.writeAs(SceneFormat.glb).first.bytes)['nodes']!
                      as List)
                  .first
              as Map<String, Object?>;

      expect(node['translation'], [1, 2, 3]);
      expect(node['scale'], [2, 2, 2]);

      // Z, then Y, then X: the order document_view draws with, and so the
      // only one that reopens a saved scene the way it was saved.
      final expected =
          (Matrix4.rotationZ(radians(euler.z))
                ..multiply(Matrix4.rotationY(radians(euler.y)))
                ..multiply(Matrix4.rotationX(radians(euler.x))))
              .getRotation();

      final q = (node['rotation']! as List).cast<num>();
      final written = Quaternion(
        q[0].toDouble(),
        q[1].toDouble(),
        q[2].toDouble(),
        q[3].toDouble(),
      ).asRotationMatrix();

      for (var i = 0; i < 3; i++) {
        for (var j = 0; j < 3; j++) {
          expect(written.entry(i, j), closeTo(expected.entry(i, j), 1e-12));
        }
      }
    });

    test('a transform that does nothing is not written down', () {
      final scene = SceneDocument(
        entities: [
          entity('e', {SceneComponents.transform: TransformComponent()}),
        ],
      );
      final node =
          (headerOf(scene.writeAs(SceneFormat.glb).first.bytes)['nodes']!
                      as List)
                  .first
              as Map<String, Object?>;
      expect(node.containsKey('translation'), isFalse);
      expect(node.containsKey('rotation'), isFalse);
      expect(node.containsKey('scale'), isFalse);
    });
  });

  group('the lights it writes', () {
    Map<String, Object?> lightOf(LightComponent light) {
      final scene = SceneDocument(
        entities: [
          entity('l', {SceneComponents.light: light}),
        ],
      );
      final json = headerOf(scene.writeAs(SceneFormat.glb).first.bytes);
      final extensions = json['extensions']! as Map<String, Object?>;
      final punctual =
          extensions['KHR_lights_punctual']! as Map<String, Object?>;
      return (punctual['lights']! as List).first as Map<String, Object?>;
    }

    test('a sun is stated in lux', () {
      final light = lightOf(
        const LightComponent(kind: LightType.sun, power: 2),
      );
      expect(light['type'], 'directional');
      expect(light['intensity'], closeTo(2 * 683, 1e-6));
    });

    test('a lamp is stated in candela', () {
      final light = lightOf(
        const LightComponent(kind: LightType.point, power: 100),
      );
      expect(light['type'], 'point');
      expect(light['intensity'], closeTo(100 * 683 / (4 * math.pi), 1e-6));
    });

    test('a spot states the half angle, not the whole cone', () {
      final light = lightOf(
        const LightComponent(
          kind: LightType.spot,
          power: 10,
          spotSize: 60,
          spotBlend: 0.5,
        ),
      );
      final spot = light['spot']! as Map<String, Object?>;
      expect(spot['outerConeAngle'], closeTo(radians(30), 1e-9));
      expect(spot['innerConeAngle'], closeTo(radians(15), 1e-9));
    });

    test(
      'a cone wider than the format allows is brought back to the limit',
      () {
        final light = lightOf(
          const LightComponent(kind: LightType.spot, power: 1, spotSize: 300),
        );
        final spot = light['spot']! as Map<String, Object?>;
        expect(spot['outerConeAngle'], closeTo(math.pi / 2, 1e-9));
      },
    );

    test('an area light becomes a point, and says it was one', () {
      final light = lightOf(
        const LightComponent(
          kind: LightType.area,
          power: 10,
          sourceRadius: 0.75,
        ),
      );
      expect(light['type'], 'point');
      final extras = light['extras']! as Map<String, Object?>;
      expect((extras['orblit']! as Map)['sourceRadius'], 0.75);
    });

    test('a colour is linear, not the sRGB it was picked in', () {
      final light = lightOf(
        const LightComponent(kind: LightType.point, colour: Tint.hex(0x808080)),
      );
      final colour = (light['color']! as List).cast<num>();
      expect(colour[0], lessThan(0.5));
      expect(colour[0], greaterThan(0.2));
    });
  });

  group('the materials it writes', () {
    MaterialLibrary library() => MaterialLibrary(
      materials: {
        'a.omat': const MaterialDocument(
          values: {
            'baseColour': [0.2, 0.4, 0.6, 1.0],
            'roughness': 0.3,
            'metallic': 1.0,
            'blend': 'masked',
            'maskThreshold': 0.25,
            'doubleSided': true,
          },
        ),
        'b.omat': const MaterialDocument(
          values: {
            'emissive': [1.0, 0.5, 0.0],
            'emissiveIntensity': 4.0,
            'shading': 'unlit',
          },
        ),
      },
    );

    List<Object?> materialsOf(SceneDocument scene) =>
        headerOf(
              scene.writeAs(SceneFormat.glb, materials: library()).first.bytes,
            )['materials']!
            as List;

    SceneDocument wearing(List<String?> assets) => SceneDocument(
      entities: [
        for (var i = 0; i < assets.length; i++)
          entity('e$i', {
            SceneComponents.mesh: MeshComponent(
              shape: const Shape(kind: ShapeKind.cube),
              colour: Tint.hex(0x100000 * i + 0x203040),
            ),
            SceneComponents.material: MaterialComponent(asset: assets[i]),
          }),
      ],
    );

    test('a resolved material brings its values and its extensions', () {
      final material =
          materialsOf(wearing(['a.omat'])).first as Map<String, Object?>;
      final pbr = material['pbrMetallicRoughness']! as Map<String, Object?>;
      expect(pbr['baseColorFactor'], [0.2, 0.4, 0.6, 1]);
      expect(pbr['roughnessFactor'], 0.3);
      expect(pbr['metallicFactor'], 1);
      expect(material['alphaMode'], 'MASK');
      expect(material['alphaCutoff'], 0.25);
      expect(material['doubleSided'], isTrue);
    });

    test('a glow brighter than one goes in the extension, not the clamp', () {
      final material =
          materialsOf(wearing(['b.omat'])).first as Map<String, Object?>;
      final factor = (material['emissiveFactor']! as List).cast<num>();
      expect(factor[0], 1);
      expect(factor[1], closeTo(0.5, 1e-9));

      final extensions = material['extensions']! as Map<String, Object?>;
      final strength =
          extensions['KHR_materials_emissive_strength']!
              as Map<String, Object?>;
      expect(strength['emissiveStrength'], closeTo(4, 1e-9));
      expect(extensions.containsKey('KHR_materials_unlit'), isTrue);
    });

    test('a colour the material overrides is not a second material', () {
      // Three entities, three different mesh colours, one material that
      // states its own base colour. One material comes out.
      expect(
        materialsOf(wearing(['a.omat', 'a.omat', 'a.omat'])),
        hasLength(1),
      );
    });

    test('a colour nothing overrides still tells them apart', () {
      expect(materialsOf(wearing([null, null])), hasLength(2));
    });

    test('an unresolved material is reported, once, not per entity', () {
      final written = wearing(['a.omat', 'a.omat']).writeAs(SceneFormat.glb);
      expect(written.problems, hasLength(1));
      expect(written.problems.single, contains('a.omat'));
    });
  });

  group('the looks it writes', () {
    test('a look is a variant, and each entity maps into it', () {
      final scene = SceneDocument(
        entities: [
          entity('e', {
            SceneComponents.mesh: MeshComponent(
              shape: const Shape(kind: ShapeKind.cube),
            ),
            SceneComponents.material: const MaterialComponent(
              asset: 'summer.omat',
              looks: {'winter': 'snow.omat', 'night': 'dark.omat'},
            ),
          }),
        ],
      );
      final json = headerOf(scene.writeAs(SceneFormat.glb).first.bytes);

      final variants =
          (json['extensions']!
                  as Map<String, Object?>)['KHR_materials_variants']!
              as Map<String, Object?>;
      expect(variants['variants'], [
        {'name': 'winter'},
        {'name': 'night'},
      ]);
      expect(json['extensionsUsed'], contains('KHR_materials_variants'));

      final primitive =
          ((json['meshes']! as List).first
                  as Map<String, Object?>)['primitives']!
              as List;
      final mappings =
          ((primitive.first as Map<String, Object?>)['extensions']!
                  as Map<String, Object?>)['KHR_materials_variants']!
              as Map<String, Object?>;
      expect(mappings['mappings'], hasLength(2));
    });
  });

  group('the models it copies in', () {
    SceneDocument placing(int times) => SceneDocument(
      entities: [
        for (var i = 0; i < times; i++)
          entity('e$i', const {
            SceneComponents.mesh: MeshComponent(asset: 'block.glb'),
          }),
      ],
    );

    test('an imported model arrives whole, under the node that places it', () {
      final json = headerOf(
        placing(
          1,
        ).writeAs(SceneFormat.glb, files: {'block.glb': cubeGlb()}).first.bytes,
      );
      final nodes = json['nodes']! as List;
      expect(nodes, hasLength(2));
      expect(nodes[0], containsPair('children', [1]));
      expect((nodes[1] as Map<String, Object?>)['mesh'], 0);
      expect(json['meshes'], hasLength(1));
      expect(json['materials'], hasLength(1));
    });

    test('the same model twice is copied once and placed twice', () {
      final once = placing(
        1,
      ).writeAs(SceneFormat.glb, files: {'block.glb': cubeGlb()});
      final twice = placing(
        2,
      ).writeAs(SceneFormat.glb, files: {'block.glb': cubeGlb()});
      final json = headerOf(twice.first.bytes);

      expect(json['meshes'], hasLength(1));
      expect(
        json['accessors'],
        hasLength((headerOf(once.first.bytes)['accessors']! as List).length),
      );
      final nodes = (json['nodes']! as List).cast<Map<String, Object?>>();
      expect(nodes[2]['mesh'], nodes[3]['mesh']);
      // The bytes are the model's, once, plus the few a second node costs.
      expect(
        twice.first.bytes.length - once.first.bytes.length,
        lessThan(once.first.bytes.length ~/ 4),
      );
    });

    test('a model whose bytes were not supplied is reported, not dropped', () {
      final written = placing(1).writeAs(SceneFormat.glb);
      expect(written.problems.single, contains('block.glb'));
      expect(headerOf(written.first.bytes)['nodes'], hasLength(1));
    });

    test('a model that is not a GLB is refused by name', () {
      final written = placing(1).writeAs(
        SceneFormat.glb,
        files: {'block.glb': Uint8List.fromList(utf8.encode('{"asset":{}}'))},
      );
      expect(written.problems.single, contains('not a GLB'));
    });

    test('a model whose bytes live elsewhere is refused with a reason', () {
      final loose = glbBytes({
        'asset': {'version': '2.0'},
        'buffers': [
          {'uri': 'beside.bin', 'byteLength': 4},
        ],
      }, Uint8List(0));
      final written = placing(
        1,
      ).writeAs(SceneFormat.glb, files: {'block.glb': loose});
      expect(written.problems.single, contains('beside.bin'));
    });
  });

  group('the round trip', () {
    test('every component comes back exactly as it went in', () {
      final components = <String, SceneComponent>{
        SceneComponents.transform: TransformComponent(
          position: Vector3(1, 2, 3),
          rotation: Vector3(10, 20, 30),
          scale: Vector3(1, 2, 1),
        ),
        SceneComponents.mesh: MeshComponent(
          shape: const Shape(kind: ShapeKind.stairs),
          colour: const Tint.hex(0x123456),
          sway: 0.4,
          castShadows: false,
        ),
        SceneComponents.material: const MaterialComponent(
          asset: 'x.omat',
          looks: {'winter': 'snow.omat'},
        ),
        SceneComponents.light: const LightComponent(
          kind: LightType.spot,
          power: 12,
          spotSize: 33,
        ),
        SceneComponents.camera: const CameraComponent(fieldOfView: 41),
        SceneComponents.splats: const SplatsComponent(
          asset: 'cloud.osplat',
          budget: 1000,
        ),
      };
      final scene = SceneDocument(
        name: 'Round trip',
        entities: [entity('e', components, name: 'Thing', visible: false)],
      );

      final json = headerOf(scene.writeAs(SceneFormat.glb).first.bytes);
      final node = (json['nodes']! as List).first as Map<String, Object?>;
      final kept =
          (node['extras']! as Map<String, Object?>)['orblit']!
              as Map<String, Object?>;

      expect(kept['id'], 'e');
      expect(kept['visible'], isFalse);
      final back = kept['components']! as Map<String, Object?>;
      for (final entry in components.entries) {
        expect(back[entry.key], entry.value.toJson(), reason: entry.key);
      }
    });

    test('the settings ride on the scene', () {
      final scene = SceneDocument(
        settings: const SceneSettings(ambient: 4200, timeOfDay: 7.5),
      );
      final json = headerOf(scene.writeAs(SceneFormat.glb).first.bytes);
      final extras =
          ((json['scenes']! as List).first as Map<String, Object?>)['extras']!
              as Map<String, Object?>;
      expect(
        (extras['orblit']! as Map<String, Object?>)['settings'],
        scene.settings.toJson(),
      );
    });
  });

  group('the files it hands back', () {
    test('a gltf comes with the bytes it points at', () {
      final written = SceneDocument(
        entities: [
          entity('e', {
            SceneComponents.mesh: MeshComponent(
              shape: const Shape(kind: ShapeKind.cube),
            ),
          }),
        ],
      ).writeAs(SceneFormat.gltf, name: 'yard');

      expect(written.files.map((f) => f.name), ['yard.gltf', 'yard.bin']);
      final json = jsonDecode(written.first.text) as Map<String, Object?>;
      expect((json['buffers']! as List).first, {
        'uri': 'yard.bin',
        'byteLength': written.files[1].bytes.length,
      });
    });

    test('a gltf with nothing to point at names no buffer and no sidecar', () {
      final written = SceneDocument().writeAs(SceneFormat.gltf);
      expect(written.files, hasLength(1));
      expect(written.first.text, isNot(contains('buffers')));
    });

    test('a glb is one file', () {
      final written = SceneDocument(
        entities: [
          entity('e', {
            SceneComponents.mesh: MeshComponent(
              shape: const Shape(kind: ShapeKind.cube),
            ),
          }),
        ],
      ).writeAs(SceneFormat.glb, name: 'yard');
      expect(written.files.map((f) => f.name), ['yard.glb']);
      expect(glbChunks(written.first.bytes), isNotNull);
    });
  });

  group('the obj it writes', () {
    SceneDocument two() => SceneDocument(
      entities: [
        entity('a', {
          SceneComponents.transform: TransformComponent(
            position: Vector3(10, 0, 0),
          ),
          SceneComponents.mesh: MeshComponent(
            shape: const Shape(kind: ShapeKind.cube),
          ),
        }, name: 'First one'),
        entity(
          'b',
          {
            SceneComponents.transform: TransformComponent(
              position: Vector3(0, 5, 0),
            ),
            SceneComponents.mesh: MeshComponent(
              shape: const Shape(kind: ShapeKind.cube),
            ),
          },
          parent: 'a',
          name: 'Second',
        ),
      ],
    );

    test('it is flattened into world space, a group each', () {
      final text = two().writeAs(SceneFormat.obj).first.text;
      expect(text, contains('o First_one'));
      expect(text, contains('o Second'));

      // The child sits at ten across and five up: its parent's place plus
      // its own, because an OBJ has no tree to hang it from.
      final corners = [
        for (final line in text.split('\n'))
          if (line.startsWith('v ')) line,
      ];
      final child = corners.sublist(corners.length ~/ 2);
      for (final line in child) {
        final parts = line.split(' ');
        expect(double.parse(parts[1]), greaterThan(9));
        expect(double.parse(parts[2]), greaterThan(4));
      }
    });

    test('a name with a space in it does not become two names', () {
      expect(
        two().writeAs(SceneFormat.obj).first.text,
        isNot(contains('o First one')),
      );
    });

    test('faces count from one', () {
      final text = two().writeAs(SceneFormat.obj).first.text;
      final faces = [
        for (final line in text.split('\n'))
          if (line.startsWith('f ')) line,
      ];
      expect(faces, isNotEmpty);
      for (final face in faces) {
        for (final corner in face.substring(2).split(' ')) {
          expect(int.parse(corner.split('/').first), greaterThan(0));
        }
      }
    });

    test('a library comes with it, and the same material is written once', () {
      final written = two().writeAs(SceneFormat.obj, name: 'yard');
      expect(written.files.map((f) => f.name), ['yard.obj', 'yard.mtl']);
      expect(written.first.text, contains('mtllib yard.mtl'));
      expect('newmtl'.allMatches(written.files[1].text).length, 1);
    });

    test('what it cannot carry, it says', () {
      final scene = SceneDocument(
        entities: [
          entity('l', const {
            SceneComponents.light: LightComponent(kind: LightType.sun),
          }),
          entity('m', const {
            SceneComponents.mesh: MeshComponent(asset: 'tree.glb'),
          }),
        ],
      );
      final problems = scene.writeAs(SceneFormat.obj).problems;
      expect(problems, hasLength(2));
      expect(problems.first, contains('no word for'));
      expect(problems.last, contains('tree.glb'));
    });
  });
}
