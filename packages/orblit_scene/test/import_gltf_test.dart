import 'dart:convert';
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

/// A scene with one of most things in it, to send round the trip.
SceneDocument populated() => SceneDocument(
  name: 'Old Town',
  settings: const SceneSettings(
    sky: Tint.hex(0x223344),
    ambient: 41000,
    timeOfDay: 17.25,
    dayCycle: true,
    hoursPerSecond: 0.125,
  ),
  entities: [
    entity('yard', {
      SceneComponents.transform: TransformComponent(
        position: Vector3(1.5, 0, -4),
        rotation: Vector3(0, 30, 0),
        scale: Vector3(2, 2, 2),
      ),
    }, name: 'Yard'),
    entity(
      'wall',
      {
        SceneComponents.transform: TransformComponent(
          position: Vector3(0, 1, 0),
          rotation: Vector3(12, 34, 56),
        ),
        SceneComponents.mesh: MeshComponent(
          shape: Shape.of(ShapeKind.cube),
          colour: const Tint.hex(0x8899AA),
          castShadows: false,
          sway: 0.4,
          authored: true,
        ),
        SceneComponents.material: const MaterialComponent(
          asset: 'materials/brick.omat',
          looks: {'winter': 'materials/brick_snow.omat'},
        ),
      },
      parent: 'yard',
      name: 'Wall',
    ),
    entity(
      'lamp',
      {
        SceneComponents.transform: TransformComponent(
          position: Vector3(0, 3, 0),
        ),
        SceneComponents.light: const LightComponent(
          kind: LightType.spot,
          power: 60,
          colour: Tint.hex(0xFFE7C0),
          spotSize: 70,
          spotBlend: 0.3,
        ),
      },
      parent: 'yard',
      name: 'Lamp',
    ),
    entity('sun', {
      SceneComponents.light: const LightComponent(power: 1.2),
    }, name: 'Sun'),
    entity(
      'eye',
      {
        SceneComponents.camera: const CameraComponent(
          fieldOfView: 42,
          near: 0.05,
          far: 250,
        ),
      },
      name: 'Eye',
      visible: false,
    ),
    entity('capture', {
      SceneComponents.splats: const SplatsComponent(
        asset: 'captures/street.osplat',
        budget: 400000,
      ),
    }, name: 'Capture'),
  ],
);

/// A node in a glTF nobody here wrote.
Map<String, Object?> foreign({
  required String name,
  List<int>? children,
  List<double>? translation,
  List<double>? rotation,
  List<double>? scale,
  List<double>? matrix,
  int? mesh,
  int? camera,
  int? light,
}) => {
  'name': name,
  if (children != null) 'children': children,
  if (translation != null) 'translation': translation,
  if (rotation != null) 'rotation': rotation,
  if (scale != null) 'scale': scale,
  if (matrix != null) 'matrix': matrix,
  if (mesh != null) 'mesh': mesh,
  if (camera != null) 'camera': camera,
  if (light != null)
    'extensions': {
      'KHR_lights_punctual': {'light': light},
    },
};

/// A whole glTF document as the bytes a reader is handed.
Uint8List document(Map<String, Object?> json) => Uint8List.fromList(
  utf8.encode(
    jsonEncode({
      'asset': const {'version': '2.0'},
      ...json,
    }),
  ),
);

void main() {
  group('a scene that went out as glTF comes back', () {
    test('an empty one keeps its name and its settings', () {
      final scene = SceneDocument(
        name: 'Nothing',
        settings: const SceneSettings(ambient: 900, timeOfDay: 3),
      );
      final read = readSceneFrom(scene.writeAs(SceneFormat.glb).first.bytes);

      expect(read.document.name, 'Nothing');
      expect(read.document.settings.ambient, 900);
      expect(read.document.settings.timeOfDay, 3);
    });

    test('the whole file comes back byte for byte', () {
      // The strongest thing there is to assert here, and the one the phase's
      // acceptance criterion actually asks for: not that the import is
      // reasonable, but that it is the same scene.
      final scene = populated();
      final read = readSceneFrom(scene.writeAs(SceneFormat.glb).first.bytes);

      expect(read.wasWrittenHere, isTrue);
      expect(read.problems, isEmpty);
      expect(read.document.encode(), scene.encode());
    });

    test('ids, order and parentage survive', () {
      final read = readSceneFrom(
        populated().writeAs(SceneFormat.glb).first.bytes,
      );

      expect(read.document.entities.map((one) => one.id), [
        'yard',
        'wall',
        'lamp',
        'sun',
        'eye',
        'capture',
      ]);
      expect(read.document['wall']!.parent, 'yard');
      expect(read.document['sun']!.parent, isNull);
    });

    test('hidden is still hidden', () {
      final read = readSceneFrom(
        populated().writeAs(SceneFormat.glb).first.bytes,
      );
      expect(read.document['eye']!.visible, isFalse);
      expect(read.document['wall']!.visible, isTrue);
    });

    test('a component this version has never heard of survives', () {
      // The case that decides whether a team can share a project across two
      // editor versions.
      final scene = SceneDocument(
        entities: [
          entity('odd', {
            'buoyancy': const UnknownComponent('buoyancy', {
              'litres': 12,
              'sealed': true,
            }),
          }),
        ],
      );
      final read = readSceneFrom(scene.writeAs(SceneFormat.glb).first.bytes);

      expect(read.document['odd']!['buoyancy']!.toJson(), {
        'litres': 12,
        'sealed': true,
      });
    });

    test('a .gltf with its buffer beside it reads the same as a .glb', () {
      final scene = populated();
      final written = scene.writeAs(SceneFormat.gltf, name: 'old town');

      final read = readSceneFrom(
        written.first.bytes,
        files: {
          for (final file in written.files.skip(1)) file.name: file.bytes,
        },
      );
      expect(read.document.encode(), scene.encode());
    });

    test('the grafted copy of a model does not become entities', () {
      // An imported model is copied into the document whole and its roots
      // become children of the node that draws it. Those nodes are geometry,
      // not things somebody put in the scene, and importing them as entities
      // would double the outliner every time a scene made a round trip.
      final model = Uint8List.fromList(
        Shape.of(ShapeKind.cube).build().toGlb(
          name: 'Block',
          materials: const [GlbMaterial(name: 'Paint')],
        ),
      );
      final scene = SceneDocument(
        entities: [
          entity('tree', {
            SceneComponents.mesh: const MeshComponent(asset: 'models/tree.glb'),
          }, name: 'Tree'),
        ],
      );

      final written = scene.writeAs(
        SceneFormat.glb,
        files: {'models/tree.glb': model},
      );
      expect(
        (glbChunks(written.first.bytes)!.json['nodes']! as List).length,
        greaterThan(1),
        reason: 'the model was grafted in, or this test proves nothing',
      );

      final read = readSceneFrom(written.first.bytes);
      expect(read.document.entities.map((one) => one.id), ['tree']);
      expect(
        (read.document['tree']!['mesh']! as MeshComponent).asset,
        'models/tree.glb',
      );
    });

    test('an open instance comes back open, its parts where they were', () {
      final scene = SceneDocument(
        entities: [
          entity('lamp1', {
            SceneComponents.prefab: const PrefabComponent(
              asset: 'props/lamp.oprefab',
              state: PrefabState.open,
            ),
          }),
          entity('lamp1/bulb', {
            SceneComponents.transform: TransformComponent(
              position: Vector3(0, 2, 0),
            ),
          }, parent: 'lamp1'),
        ],
      );

      final read = readSceneFrom(scene.writeAs(SceneFormat.glb).first.bytes);
      expect(read.document.entities.map((one) => one.id), [
        'lamp1',
        'lamp1/bulb',
      ]);
      expect(read.document['lamp1/bulb']!.parent, 'lamp1');
      final link = read.document['lamp1']!['prefab']! as PrefabComponent;
      expect(link.asset, 'props/lamp.oprefab');
      expect(link.state, PrefabState.open);
    });

    test('one written before instances were links comes back as the copy '
        'it was', () {
      // Every part carried the link then, and the scene said no version,
      // because the exporter did not write one down yet.
      Map<String, Object?> part(String id, {List<int>? children}) => {
        'name': id,
        if (children != null) 'children': children,
        'extras': {
          'orblit': {
            'id': id,
            'components': {
              'prefab': {'asset': 'props/lamp.oprefab'},
            },
          },
        },
      };
      final read = readSceneFrom(
        document({
          'scenes': [
            {
              'nodes': [0],
              'extras': {
                'orblit': {'settings': <String, Object?>{}},
              },
            },
          ],
          'nodes': [
            part('lamp', children: [1]),
            part('bulb'),
          ],
        }),
      );

      expect(read.document.entities.map((one) => one.id), ['lamp', 'bulb']);
      for (final one in read.document.entities) {
        expect(
          (one['prefab']! as PrefabComponent).state,
          PrefabState.stamped,
          reason: one.id,
        );
      }
    });
  });

  group('a glTF from somewhere else', () {
    test('nodes become entities, with their names and their tree', () {
      final read = readSceneFrom(
        document({
          'scene': 0,
          'scenes': [
            {
              'name': 'Imported',
              'nodes': [0],
            },
          ],
          'nodes': [
            foreign(name: 'Root', children: [1]),
            foreign(name: 'Child', translation: [1, 2, 3]),
          ],
        }),
      );

      expect(read.wasWrittenHere, isFalse);
      expect(read.document.name, 'Imported');
      expect(read.document.entities, hasLength(2));
      expect(read.document.entities.first.name, 'Root');
      expect(
        read.document.entities.last.parent,
        read.document.entities.first.id,
      );
      expect(
        (read.document.entities.last['transform']! as TransformComponent)
            .position,
        Vector3(1, 2, 3),
      );
      expect(read.problems, hasLength(1), reason: 'it should say it guessed');
    });

    test('a quaternion comes back as the angles that made it', () {
      // The exporter composes Z, then Y, then X. This is that taken apart
      // again, and it is the one piece of arithmetic here worth distrusting.
      const x = 12.0, y = 34.0, z = 56.0;
      final m = Matrix4.rotationZ(radians(z))
        ..multiply(Matrix4.rotationY(radians(y)))
        ..multiply(Matrix4.rotationX(radians(x)));
      final q = Quaternion.fromRotation(m.getRotation())..normalize();

      final read = readSceneFrom(
        document({
          'nodes': [
            foreign(name: 'Turned', rotation: [q.x, q.y, q.z, q.w]),
          ],
        }),
      );

      final turned =
          read.document.entities.single['transform']! as TransformComponent;
      expect(turned.rotation.x, closeTo(x, 1e-9));
      expect(turned.rotation.y, closeTo(y, 1e-9));
      expect(turned.rotation.z, closeTo(z, 1e-9));
    });

    test('a matrix is taken apart into where, which way and how big', () {
      final m = Matrix4.translation(Vector3(5, 6, 7))
        ..multiply(Matrix4.rotationY(radians(40)))
        ..multiply(Matrix4.diagonal3(Vector3(2, 3, 4)));

      final read = readSceneFrom(
        document({
          'nodes': [foreign(name: 'Placed', matrix: m.storage.toList())],
        }),
      );

      final placed =
          read.document.entities.single['transform']! as TransformComponent;
      expect(placed.position.x, closeTo(5, 1e-9));
      expect(placed.position.y, closeTo(6, 1e-9));
      expect(placed.position.z, closeTo(7, 1e-9));
      expect(placed.scale.x, closeTo(2, 1e-9));
      expect(placed.scale.y, closeTo(3, 1e-9));
      expect(placed.scale.z, closeTo(4, 1e-9));
      expect(placed.rotation.x, closeTo(0, 1e-9));
      expect(placed.rotation.y, closeTo(40, 1e-9));
      expect(placed.rotation.z, closeTo(0, 1e-9));
    });

    test('a punctual light comes back the brightness it left', () {
      final read = readSceneFrom(
        document({
          'extensions': {
            'KHR_lights_punctual': {
              'lights': [
                {
                  'type': 'directional',
                  'color': [1, 1, 1],
                  'intensity': 1.2 * Photometry.luminousEfficacy,
                },
                {
                  'type': 'spot',
                  'color': [1, 1, 1],
                  'intensity': Photometry.lumensToCandela(
                    Photometry.wattsToLumens(60),
                  ),
                  'spot': {
                    'innerConeAngle': radians(35) * 0.7,
                    'outerConeAngle': radians(35),
                  },
                },
              ],
            },
          },
          'nodes': [
            foreign(name: 'Sun', light: 0),
            foreign(name: 'Lamp', light: 1),
          ],
        }),
      );

      final sun = read.document.entities.first['light']! as LightComponent;
      expect(sun.kind, LightType.sun);
      expect(sun.power, closeTo(1.2, 1e-9));

      final lamp = read.document.entities.last['light']! as LightComponent;
      expect(lamp.kind, LightType.spot);
      expect(lamp.power, closeTo(60, 1e-9));
      expect(lamp.spotSize, closeTo(70, 1e-9));
      expect(lamp.spotBlend, closeTo(0.3, 1e-9));
    });

    test('an area light is one again, because the exporter said so', () {
      final read = readSceneFrom(
        document({
          'extensions': {
            'KHR_lights_punctual': {
              'lights': [
                {
                  'type': 'point',
                  'color': [1, 1, 1],
                  'intensity': 1,
                  'extras': {
                    'orblit': {'kind': 'area', 'sourceRadius': 0.75},
                  },
                },
              ],
            },
          },
          'nodes': [foreign(name: 'Softbox', light: 0)],
        }),
      );

      final light = read.document.entities.single['light']! as LightComponent;
      expect(light.kind, LightType.area);
      expect(light.sourceRadius, 0.75);
    });

    test('a camera comes back as one', () {
      final read = readSceneFrom(
        document({
          'cameras': [
            {
              'type': 'perspective',
              'perspective': {'yfov': radians(65), 'znear': 0.2, 'zfar': 800},
            },
          ],
          'nodes': [foreign(name: 'Eye', camera: 0)],
        }),
      );

      final eye = read.document.entities.single['camera']! as CameraComponent;
      expect(eye.fieldOfView, closeTo(65, 1e-9));
      expect(eye.near, 0.2);
      expect(eye.far, 800);
    });

    test('triangles become geometry somebody can still edit', () {
      final positions = Float32List.fromList([
        0, 0, 0, //
        1, 0, 0,
        0, 1, 0,
      ]);
      final indices = Uint16List.fromList([0, 1, 2]);
      final bytes =
          (BytesBuilder()
                ..add(positions.buffer.asUint8List())
                ..add(indices.buffer.asUint8List()))
              .toBytes();

      final read = readSceneFrom(
        document({
          'buffers': [
            {
              'byteLength': bytes.length,
              'uri':
                  'data:application/octet-stream;base64,${base64Encode(bytes)}',
            },
          ],
          'bufferViews': [
            {'buffer': 0, 'byteOffset': 0, 'byteLength': 36},
            {'buffer': 0, 'byteOffset': 36, 'byteLength': 6},
          ],
          'accessors': [
            {
              'bufferView': 0,
              'componentType': GltfComponent.float,
              'count': 3,
              'type': 'VEC3',
            },
            {
              'bufferView': 1,
              'componentType': GltfComponent.unsignedShort,
              'count': 3,
              'type': 'SCALAR',
            },
          ],
          'materials': [
            {
              'pbrMetallicRoughness': {
                'baseColorFactor': [1, 0, 0, 1],
              },
            },
          ],
          'meshes': [
            {
              'primitives': [
                {
                  'attributes': {'POSITION': 0},
                  'indices': 1,
                  'material': 0,
                },
              ],
            },
          ],
          'nodes': [foreign(name: 'Triangle', mesh: 0)],
        }),
      );

      final mesh = read.document.entities.single['mesh']! as MeshComponent;
      expect(mesh.authored, isTrue);
      expect(mesh.geometry!.positions, [
        Vector3(0, 0, 0),
        Vector3(1, 0, 0),
        Vector3(0, 1, 0),
      ]);
      expect(mesh.geometry!.faces.single.vertices, [0, 1, 2]);
      expect(mesh.colour.red, closeTo(1, 1e-6));
      expect(mesh.colour.green, closeTo(0, 1e-6));
    });

    test('an interleaved buffer view is read at its stride', () {
      // Reading a strided view as though it were packed gives geometry that
      // is wrong rather than geometry that is missing, which is much harder
      // to notice — so it is worth a test of its own.
      final data = ByteData(3 * 20);
      const corners = [
        [1.0, 2.0, 3.0],
        [4.0, 5.0, 6.0],
        [7.0, 8.0, 9.0],
      ];
      for (var i = 0; i < 3; i++) {
        for (var c = 0; c < 3; c++) {
          data.setFloat32(i * 20 + c * 4, corners[i][c], Endian.little);
        }
        // Where the texture coordinates would be, filled with something that
        // is not a plausible position.
        data.setFloat32(i * 20 + 12, -999, Endian.little);
        data.setFloat32(i * 20 + 16, -999, Endian.little);
      }
      final bytes = data.buffer.asUint8List();

      final read = readSceneFrom(
        document({
          'buffers': [
            {
              'byteLength': bytes.length,
              'uri':
                  'data:application/octet-stream;base64,${base64Encode(bytes)}',
            },
          ],
          'bufferViews': [
            {
              'buffer': 0,
              'byteOffset': 0,
              'byteLength': bytes.length,
              'byteStride': 20,
            },
          ],
          'accessors': [
            {
              'bufferView': 0,
              'componentType': GltfComponent.float,
              'count': 3,
              'type': 'VEC3',
            },
          ],
          'meshes': [
            {
              'primitives': [
                {
                  'attributes': {'POSITION': 0},
                },
              ],
            },
          ],
          'nodes': [foreign(name: 'Strided', mesh: 0)],
        }),
      );

      final mesh = read.document.entities.single['mesh']! as MeshComponent;
      expect(mesh.geometry!.positions, [
        Vector3(1, 2, 3),
        Vector3(4, 5, 6),
        Vector3(7, 8, 9),
      ]);
    });

    test('what it could not carry is said, not swallowed', () {
      final read = readSceneFrom(
        document({
          'cameras': [
            {
              'type': 'orthographic',
              'orthographic': {'xmag': 2, 'ymag': 2, 'znear': 0, 'zfar': 9},
            },
          ],
          'nodes': [foreign(name: 'Flat', camera: 0)],
        }),
      );

      expect(read.problems, hasLength(2));
      expect(read.problems.first, contains('not written by Orblit'));
      expect(read.problems.last, contains('orthographic'));
      expect(read.document.entities.single.has('camera'), isTrue);
    });

    test('a node naming a light that is not there is not a crash', () {
      final read = readSceneFrom(
        document({
          'nodes': [foreign(name: 'Ghost', light: 3)],
        }),
      );

      expect(read.document.entities.single.has('light'), isFalse);
      expect(read.problems.any((one) => one.contains('light 3')), isTrue);
    });
  });

  group('bytes that are not a scene', () {
    test('rubbish is refused rather than read as an empty scene', () {
      expect(
        () => readSceneFrom(Uint8List.fromList([7, 7, 7, 7, 7, 7])),
        throwsA(isA<SceneFormatException>()),
      );
    });

    test('JSON that is not a document is refused', () {
      expect(
        () => readSceneFrom(Uint8List.fromList(utf8.encode('[1, 2, 3]'))),
        throwsA(isA<SceneFormatException>()),
      );
    });

    test('a document with geometry and no buffer says so', () {
      final read = readSceneFrom(
        document({
          'accessors': [
            {'componentType': GltfComponent.float, 'count': 3},
          ],
          'meshes': [
            {
              'primitives': [
                {
                  'attributes': {'POSITION': 0},
                },
              ],
            },
          ],
          'nodes': [foreign(name: 'Hollow', mesh: 0)],
        }),
      );

      expect(read.document.entities.single.has('mesh'), isFalse);
      expect(read.problems.any((one) => one.contains('no buffer')), isTrue);
    });
  });
}
