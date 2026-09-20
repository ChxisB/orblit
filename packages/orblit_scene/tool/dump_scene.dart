// Writes scene exports into a directory, for the validator to read.
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

void main(List<String> args) {
  final into = Directory(args.first)..createSync(recursive: true);

  void dump(
    String name,
    SceneDocument scene, {
    MaterialLibrary? materials,
    Map<String, Uint8List> files = const {},
    List<SceneFormat> formats = const [SceneFormat.glb, SceneFormat.gltf],
  }) {
    for (final format in formats) {
      final written = scene.writeAs(
        format,
        name: name,
        materials: materials,
        files: files,
      );
      for (final file in written.files) {
        File('${into.path}/${file.name}').writeAsBytesSync(file.bytes);
      }
      for (final problem in written.problems) {
        stdout.writeln('  $name.${format.name}: $problem');
      }
    }
  }

  dump('empty', SceneDocument(name: 'Empty'));

  dump(
    'staging',
    SceneDocument(
      name: 'Staging',
      entities: [
        _entity('sun', 'Sun', {
          SceneComponents.transform: TransformComponent(
            rotation: Vector3(-50, 30, 0),
          ),
          SceneComponents.light: const LightComponent(
            kind: LightType.sun,
            power: 1.2,
          ),
        }),
        _entity('lamp', 'Lamp', {
          SceneComponents.transform: TransformComponent(
            position: Vector3(0, 3, 0),
          ),
          SceneComponents.light: const LightComponent(
            kind: LightType.point,
            power: 60,
            colour: Tint.hex(0xFFE0B0),
          ),
        }),
        _entity('spot', 'Spot', {
          SceneComponents.light: const LightComponent(
            kind: LightType.spot,
            power: 40,
            spotSize: 45,
            spotBlend: 0.3,
          ),
        }),
        _entity('panel', 'Panel', {
          SceneComponents.light: const LightComponent(
            kind: LightType.area,
            power: 25,
            sourceRadius: 0.4,
          ),
        }),
        _entity('eye', 'Camera', {
          SceneComponents.transform: TransformComponent(
            position: Vector3(0, 1.7, 6),
          ),
          SceneComponents.camera: const CameraComponent(fieldOfView: 50),
        }),
      ],
    ),
  );

  dump(
    'shapes',
    _shapes(),
    materials: _library(),
    files: {
      'textures/stone.png': _png(0x8C, 0x84, 0x7A),
      'textures/stone_n.png': _png(0x80, 0x80, 0xFF),
      'textures/snow.png': _png(0xEE, 0xF2, 0xFF),
    },
  );

  // A scene that draws a model from a file. The model is one this repo can
  // make, so the graft is exercised on bytes the mesh package wrote rather
  // than on a fixture nobody checks.
  final model = Uint8List.fromList(
    Shape.of(ShapeKind.torus).build().toGlb(
      name: 'Torus',
      materials: const [GlbMaterial(name: 'Bronze', metallic: 1)],
    ),
  );
  // OBJ is not glTF and the validator has nothing to say about it, but it is
  // written beside the rest so a person can open it and so its size is
  // visible in the same place.
  dump(
    'flat',
    _shapes(),
    materials: _library(),
    formats: const [SceneFormat.obj],
  );

  dump(
    'imported',
    SceneDocument(
      name: 'Imported',
      entities: [
        _entity('a', 'First', {
          SceneComponents.transform: TransformComponent(
            position: Vector3(-2, 0, 0),
          ),
          SceneComponents.mesh: const MeshComponent(asset: 'models/ring.glb'),
        }),
        // The same model twice, to prove the second graft is numbered against
        // the first rather than against the file it came from.
        _entity('b', 'Second', {
          SceneComponents.transform: TransformComponent(
            position: Vector3(2, 0, 0),
            scale: Vector3(0.5, 0.5, 0.5),
          ),
          SceneComponents.mesh: const MeshComponent(asset: 'models/ring.glb'),
        }),
        _entity('c', 'Beside it', {
          SceneComponents.mesh: MeshComponent(
            shape: const Shape(kind: ShapeKind.cube),
          ),
        }),
      ],
    ),
    files: {'models/ring.glb': model},
  );
}

SceneDocument _shapes() => SceneDocument(
  name: 'Shapes',
  entities: [
    _entity('root', 'Yard', {
      SceneComponents.transform: TransformComponent(
        position: Vector3(1, 0, -2),
        rotation: Vector3(0, 35, 0),
      ),
    }),
    for (var i = 0; i < ShapeKind.values.length; i++)
      _entity(ShapeKind.values[i].name, ShapeKind.values[i].label, {
        SceneComponents.transform: TransformComponent(
          position: Vector3(i * 2.0, 0, 0),
          rotation: Vector3(0, i * 15.0, i.isEven ? 0 : 10),
          scale: Vector3(1, i.isEven ? 1 : 1.4, 1),
        ),
        SceneComponents.mesh: MeshComponent(
          shape: Shape(kind: ShapeKind.values[i]),
          colour: Tint.hex(0x3366CC + i * 0x0A0A0A),
        ),
        SceneComponents.material: MaterialComponent(
          asset: 'materials/stone$materialExtension',
          looks: i.isEven
              ? const {'winter': 'materials/snow$materialExtension'}
              : const {},
        ),
      }, parent: 'root'),
  ],
);

SceneEntity _entity(
  String id,
  String name,
  Map<String, SceneComponent> components, {
  String? parent,
}) => SceneEntity(id: id, name: name, parent: parent, components: components);

MaterialLibrary _library() => MaterialLibrary(
  materials: {
    'materials/stone$materialExtension': const MaterialDocument(
      values: {
        'baseColour': [0.45, 0.42, 0.4, 1.0],
        'roughness': 0.85,
        'metallic': 0.0,
        'reflectance': 0.35,
        'clearCoat': 0.2,
        'clearCoatRoughness': 0.4,
        'normalScale': 0.8,
        'ambientOcclusion': 0.9,
        'wrap': 'repeat',
        'filter': 'smooth',
      },
      maps: {
        'baseColour': 'textures/stone.png',
        'normal': 'textures/stone_n.png',
        'blendMask': 'textures/none.png',
      },
    ),
    'materials/snow$materialExtension': const MaterialDocument(
      values: {
        'baseColour': [0.92, 0.94, 0.98, 1.0],
        'roughness': 0.6,
        'sheenColour': [0.8, 0.85, 1.0],
        'sheenRoughness': 0.3,
        'emissive': [0.02, 0.02, 0.04],
        'emissiveIntensity': 2.0,
        'doubleSided': true,
        'blend': 'masked',
        'maskThreshold': 0.4,
        'tiling': [4.0, 4.0],
        'offset': [0.1, 0.0],
        'wrap': 'clamp',
        'filter': 'sharp',
      },
      maps: {'baseColour': 'textures/snow.png'},
    ),
  },
);

/// A four-by-four picture in one colour.
///
/// Written by hand rather than checked in, because a binary fixture is a file
/// nobody reads and this is thirty lines that say exactly what the bytes are.
Uint8List _png(int r, int g, int b) {
  final header = BytesBuilder()
    ..add([0, 0, 0, 4, 0, 0, 0, 4])
    ..add([8, 6, 0, 0, 0]);

  final raw = BytesBuilder();
  for (var y = 0; y < 4; y++) {
    raw.addByte(0);
    for (var x = 0; x < 4; x++) {
      raw.add([r, g, b, 255]);
    }
  }

  return Uint8List.fromList([
    0x89,
    0x50,
    0x4E,
    0x47,
    0x0D,
    0x0A,
    0x1A,
    0x0A,
    ..._chunk('IHDR', header.takeBytes()),
    ..._chunk('IDAT', ZLibEncoder().convert(raw.takeBytes())),
    ..._chunk('IEND', const []),
  ]);
}

List<int> _chunk(String kind, List<int> data) {
  final body = [...kind.codeUnits, ...data];
  final length = data.length;
  return [
    (length >> 24) & 0xFF,
    (length >> 16) & 0xFF,
    (length >> 8) & 0xFF,
    length & 0xFF,
    ...body,
    ..._crc(body),
  ];
}

List<int> _crc(List<int> bytes) {
  var crc = 0xFFFFFFFF;
  for (final byte in bytes) {
    crc ^= byte;
    for (var i = 0; i < 8; i++) {
      crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
    }
  }
  crc ^= 0xFFFFFFFF;
  return [
    (crc >> 24) & 0xFF,
    (crc >> 16) & 0xFF,
    (crc >> 8) & 0xFF,
    crc & 0xFF,
  ];
}
