import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_weather/orblit_weather.dart' show CelestialBody;
import 'package:vector_math/vector_math_64.dart';

import 'fixture.dart';

/// The cases for scene export: a scene, a format, the materials and files it
/// can reach, and the files and problems that come out.
List<Map<String, Object?>> exportCases() => [
  for (final scenario in exportScenarios())
    {'op': 'export', 'in': scenario, 'expect': _exported(scenario)},
];

/// Every scenario the export cases run, in the order they are written. The
/// import cases read what these write, by position.
List<Map<String, Object?>> exportScenarios() => [
  ..._treeScenarios(),
  ..._lightScenarios(),
  ..._meshScenarios(),
  ..._materialScenarios(),
  ..._lookScenarios(),
  ..._modelScenarios(),
  ..._objScenarios(),
];

const _cube = MeshComponent(shape: Shape(kind: ShapeKind.cube));

// The three kinds that default to more than 32 faces are built small.
Shape _small(ShapeKind kind) => switch (kind) {
  ShapeKind.sphere => const Shape(kind: ShapeKind.sphere, subdivisions: 1),
  ShapeKind.torus => const Shape(kind: ShapeKind.torus, rings: 3, columns: 5),
  ShapeKind.stairs => const Shape(kind: ShapeKind.stairs, steps: 4),
  _ => Shape(kind: kind),
};

SceneEntity _thing(
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

Map<String, SceneComponent> _placed(
  Vector3 position, {
  Vector3? rotation,
  Vector3? scale,
  SceneComponent mesh = _cube,
}) => {
  SceneComponents.transform: TransformComponent(
    position: position,
    rotation: rotation,
    scale: scale,
  ),
  SceneComponents.mesh: mesh,
};

Map<String, Object?> _scenario(
  SceneDocument scene,
  SceneFormat format, {
  String name = 'scene',
  Map<String, Object?>? materials,
  Map<String, List<int>> files = const {},
}) => {
  'scene': _within32Faces(scene).encode(),
  'format': format.name,
  'name': name,
  'materials': materials,
  'files': {for (final e in files.entries) e.key: b64(e.value)},
};

// Dart sorts faces by material with a sort that is stable only up to 32
// items, so a larger mesh comes out in an order no other language can match
// (decision 75). Every mesh a case exports stays under that limit.
SceneDocument _within32Faces(SceneDocument scene) {
  for (final entity in scene.entities) {
    final mesh = entity.components[SceneComponents.mesh] as MeshComponent?;
    final faces = (mesh?.geometry ?? mesh?.shape?.build())?.faces.length ?? 0;
    if (faces > 32) {
      throw StateError('${entity.id} has $faces faces, past the 32 a stable sort holds');
    }
  }
  return scene;
}

// The scene is read back from its text, as the C# side reads it, so both sides
// export the same document and not the same object.
Map<String, Object?> _exported(Map<String, Object?> scenario) => attempt(() {
  final written = writeScenario(scenario);
  return {
    'files': [for (final file in written.files) _file(file)],
    'problems': written.problems,
  };
});

/// What a scenario writes. Throws when the export does.
SceneWritten writeScenario(Map<String, Object?> scenario) {
  final scene = SceneDocument.decode(scenario['scene']! as String).document;
  final materials = scenario['materials'] as Map<String, Object?>?;
  final files = scenario['files']! as Map<String, Object?>;
  return scene.writeAs(
    SceneFormat.values.byName(scenario['format']! as String),
    name: scenario['name']! as String,
    materials: materials == null
        ? null
        : MaterialLibrary(
            materials: {
              for (final e in materials.entries)
                e.key: MaterialDocument.fromJson(
                  e.value! as Map<String, Object?>,
                ).document,
            },
          ),
    files: {
      for (final e in files.entries) e.key: base64Decode(e.value! as String),
    },
  );
}

// A glb is held as its header tree and its bytes, so a difference shows up as
// a key and not as a changed letter in a base64 string.
Map<String, Object?> _file(Written file) {
  if (file.name.endsWith('.glb')) {
    final chunks = glbChunks(file.bytes)!;
    return {
      'name': file.name,
      'json': tree(chunks.json),
      'binary': b64(chunks.binary),
    };
  }
  if (file.name.endsWith('.bin')) {
    return {'name': file.name, 'binary': b64(file.bytes)};
  }
  return {'name': file.name, 'text': file.text};
}

List<Map<String, Object?>> _treeScenarios() {
  const angles = [0.0, 90.0, 180.0, -60.0];
  const turns = [0.0, 90.0, 180.0, -90.0, 35.0];
  final spun = SceneDocument(
    name: 'Spun',
    entities: [
      for (final x in angles)
        for (final y in turns)
          for (final z in [0.0, 45.0, 270.0])
            _thing('r${x}_${y}_$z', {
              SceneComponents.transform: TransformComponent(
                position: Vector3(x / 10, y / 10, z / 10),
                rotation: Vector3(x, y, z),
                scale: Vector3(1, 2, 3),
              ),
            }),
    ],
  );
  final nested = SceneDocument(
    name: 'Nested',
    entities: [
      _thing('a', _placed(Vector3(1, 2, 3), rotation: Vector3(20, 40, 60))),
      _thing('b', const {}, parent: 'a'),
      _thing('c', _placed(Vector3(0.1234567891, -1e-9, 12345.6789)), parent: 'b'),
      _thing('d', {SceneComponents.transform: TransformComponent()}),
      _thing('orphan', const {}, parent: 'gone'),
      _thing('hidden', _placed(Vector3.zero()), visible: false),
      _thing('odd', const {}, name: 'He said "hi" \\ and left'),
      _thing('uni', const {}, name: 'Café 世界'),
    ],
  );
  final everything = SceneDocument(
    name: 'Round trip',
    settings: const SceneSettings(
      sky: Tint.hex(0x336699),
      ambient: 4200,
      timeOfDay: 7.5,
      dayCycle: true,
      hoursPerSecond: 2,
      layerNames: ['ground', 'props'],
    ),
    entities: [
      _thing('e', {
        SceneComponents.transform: TransformComponent(
          position: Vector3(1, 2, 3),
          rotation: Vector3(10, 20, 30),
          scale: Vector3(1, 2, 1),
        ),
        SceneComponents.mesh: const MeshComponent(
          shape: Shape(kind: ShapeKind.stairs, steps: 4),
          colour: Tint.hex(0x123456),
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
      }, name: 'Thing', visible: false),
    ],
  );
  return [
    _scenario(SceneDocument(name: 'Nothing'), SceneFormat.glb),
    _scenario(SceneDocument(name: 'Nothing'), SceneFormat.gltf),
    _scenario(spun, SceneFormat.glb),
    _scenario(nested, SceneFormat.glb),
    _scenario(nested, SceneFormat.gltf, name: 'yard'),
    _scenario(everything, SceneFormat.glb),
    _scenario(everything, SceneFormat.gltf),
  ];
}

List<Map<String, Object?>> _lightScenarios() {
  SceneDocument lights(LightType kind) => SceneDocument(
    entities: [
      for (final power in [0.0, 2.0, 100.0, 1000.0])
        _thing('p$power', {
          SceneComponents.light: LightComponent(
            kind: kind,
            power: power,
            colour: Tint.hex(0x808080 + power.toInt()),
          ),
        }),
      for (final size in [20.0, 60.0, 300.0])
        for (final blend in [0.0, 0.5, 1.0])
          _thing('s$size$blend', {
            SceneComponents.light: LightComponent(
              kind: kind,
              power: 10,
              spotSize: size,
              spotBlend: blend,
            ),
          }),
      _thing('r', const {
        SceneComponents.light: LightComponent(sourceRadius: 0.75),
      }),
      _thing('quiet', {
        SceneComponents.light: LightComponent(kind: kind, castShadows: false),
      }),
    ],
  );
  final cameras = SceneDocument(
    entities: [
      _thing('plain', const {SceneComponents.camera: CameraComponent()}),
      _thing('narrow', const {
        SceneComponents.camera: CameraComponent(fieldOfView: 41),
      }),
      _thing('wide', const {
        SceneComponents.camera: CameraComponent(
          fieldOfView: 90,
          near: 0.01,
          far: 200,
        ),
      }),
    ],
  );
  final bodies = SceneDocument(
    entities: [
      for (final body in CelestialBody.values)
        _thing('b${body.name}', {
          SceneComponents.light: LightComponent(body: body),
        }),
    ],
  );
  return [
    for (final kind in LightType.values) _scenario(lights(kind), SceneFormat.glb),
    _scenario(lights(LightType.spot), SceneFormat.gltf),
    _scenario(cameras, SceneFormat.glb),
    _scenario(bodies, SceneFormat.glb),
    _scenario(
      SceneDocument(
        entities: [
          _thing('both', {
            ..._placed(Vector3(1, 0, 0)),
            SceneComponents.light: const LightComponent(kind: LightType.point),
            SceneComponents.camera: const CameraComponent(),
          }),
          _thing('splat', const {
            SceneComponents.splats: SplatsComponent(asset: 'a.osplat'),
          }),
        ],
      ),
      SceneFormat.glb,
    ),
  ];
}

Mesh _paintedCube() {
  final mesh = Shape.of(ShapeKind.cube).build();
  for (var i = 0; i < mesh.faces.length; i++) {
    mesh.faces[i].material = i % 3;
  }
  return mesh;
}

List<Map<String, Object?>> _meshScenarios() {
  final shapes = SceneDocument(
    entities: [
      for (final kind in ShapeKind.values)
        _thing(kind.name, _placed(Vector3.zero(), mesh: MeshComponent(shape: _small(kind)))),
    ],
  );
  final shaped = SceneDocument(
    entities: [
      _thing('can', _placed(Vector3.zero(), mesh: const MeshComponent(shape: Shape(kind: ShapeKind.cylinder, sides: 12, capped: false)))),
      _thing('steps', _placed(Vector3.zero(), mesh: const MeshComponent(shape: Shape(kind: ShapeKind.stairs, steps: 4, width: 2)))),
      _thing('ring', _placed(Vector3.zero(), mesh: const MeshComponent(shape: Shape(kind: ShapeKind.torus, rings: 4, columns: 6, smooth: false)))),
      _thing('ball', _placed(Vector3.zero(), mesh: const MeshComponent(shape: Shape(kind: ShapeKind.sphere, subdivisions: 1)))),
    ],
  );
  final built = SceneDocument(
    entities: [
      _thing('painted', _placed(Vector3(1, 1, 1), mesh: MeshComponent(geometry: _paintedCube()))),
      _thing('flat', _placed(Vector3.zero(), mesh: MeshComponent(
        geometry: Mesh(
          positions: [Vector3(0, 0, 0), Vector3(1, 0, 0), Vector3(0, 1, 0)],
          faces: [Face([0, 1, 2])],
        ),
      ))),
      _thing('empty', _placed(Vector3.zero(), mesh: MeshComponent(geometry: Mesh()))),
      _thing('none', const {SceneComponents.mesh: MeshComponent()}),
      _thing('kept', _placed(Vector3.zero(), mesh: const MeshComponent(
        shape: Shape(kind: ShapeKind.cube),
        boundary: {'kind': 'box', 'size': [1, 2, 3]},
        surfaces: ['a.omat', null, 3],
        receiveShadows: false,
        authored: true,
        sway: 0.25,
      ))),
    ],
  );
  return [
    _scenario(shapes, SceneFormat.glb),
    _scenario(shapes, SceneFormat.gltf),
    _scenario(shaped, SceneFormat.glb),
    _scenario(built, SceneFormat.glb),
    _scenario(built, SceneFormat.gltf),
  ];
}

// Just enough of each picture format to be told apart by its first bytes.
final Uint8List _png = Uint8List.fromList([0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);
final Uint8List _jpeg = Uint8List.fromList([0xFF, 0xD8, 0xFF, 0xE0, 4, 5]);
final Uint8List _ktx2 = Uint8List.fromList([0xAB, 0x4B, 0x54, 0x58, 6, 7, 8]);
final Uint8List _webp = Uint8List.fromList([
  ...'RIFF'.codeUnits,
  1, 0, 0, 0,
  ...'WEBP'.codeUnits,
  9,
]);

final Map<String, List<int>> _pictures = {
  'tex/a.png': _png,
  'tex/mr.jpg': _jpeg,
  'tex/n.ktx2': _ktx2,
  'tex/o.webp': _webp,
  'tex/e.png': _png,
  'tex/b.png': _jpeg,
  'tex/m.png': _png,
};

SceneDocument _wearing(List<String?> assets, {Tint? colour}) => SceneDocument(
  entities: [
    for (var i = 0; i < assets.length; i++)
      _thing('e$i', {
        SceneComponents.mesh: MeshComponent(
          shape: const Shape(kind: ShapeKind.cube),
          colour: colour ?? Tint.hex(0x100000 * i + 0x203040),
        ),
        SceneComponents.material: MaterialComponent(asset: assets[i]),
      }),
  ],
);

List<Map<String, Object?>> _materialScenarios() {
  final plain = <String, Object?>{
    'a.omat': {
      'values': {
        'baseColour': [0.2, 0.4, 0.6, 1.0],
        'roughness': 0.3,
        'metallic': 1.0,
        'blend': 'masked',
        'maskThreshold': 0.25,
        'doubleSided': true,
      },
    },
    'b.omat': {
      'values': {
        'emissive': [1.0, 0.5, 0.0],
        'emissiveIntensity': 4.0,
        'shading': 'unlit',
      },
    },
  };
  final rich = <String, Object?>{
    'rich.omat': {
      'values': {
        'baseColour': [0.2, 0.4, 0.6, 0.5],
        'metallic': 0.7,
        'roughness': 0.35,
        'emissive': [2.0, 1.0, 0.5],
        'emissiveIntensity': 3.0,
        'blend': 'transparent',
        'culling': 'none',
        'clearCoat': 0.8,
        'clearCoatRoughness': 0.1,
        'sheenColour': [0.3, 0.2, 0.1],
        'sheenRoughness': 0.4,
        'anisotropy': -0.6,
        'reflectance': 0.8,
        'normalScale': 1.5,
        'ambientOcclusion': 0.7,
        'tiling': [2.0, 3.0],
        'offset': [0.25, 0.5],
        'wrap': 'mirror',
        'filter': 'sharp',
      },
      'maps': {
        'baseColour': 'tex/a.png',
        'metallicRoughness': 'tex/mr.jpg',
        'normal': 'tex/n.ktx2',
        'occlusion': 'tex/o.webp',
        'emissive': 'tex/e.png',
        'blendBaseColour': 'tex/b.png',
        'blendMask': 'tex/m.png',
      },
    },
    'lost.omat': {
      'maps': {'baseColour': 'tex/missing.png', 'normal': 'tex/a.png'},
      'values': {'tiling': [4.0, 4.0]},
    },
    'child.omat': {
      'parent': 'rich.omat',
      'values': {'roughness': 0.9, 'tiling': [1.0, 1.0]},
    },
  };

  final blends = <String, Object?>{
    for (final blend in ['opaque', 'masked', 'transparent', 'fade', 'add'])
      '$blend.omat': {
        'values': {'blend': blend, 'maskThreshold': 0.4, 'culling': 'front'},
      },
    'cutout.omat': {
      'values': {'blend': 'masked'},
    },
  };
  final strengths = <String, Object?>{
    for (final v in [0.0, 0.2, 0.5, 1.0, -0.5, 2.0])
      'k$v.omat': {
        'values': {
          'anisotropy': v,
          'reflectance': v.abs(),
          'clearCoat': v.abs(),
          'emissive': [v.abs(), 0.0, 0.0],
          'emissiveIntensity': v.abs() + 0.5,
        },
      },
  };
  final samplers = <String, Object?>{
    for (final wrap in ['repeat', 'clamp', 'mirror', 'bogus'])
      for (final filter in ['smooth', 'sharp'])
        '$wrap$filter.omat': {
          'values': {'wrap': wrap, 'filter': filter},
          'maps': {'baseColour': 'tex/a.png'},
        },
  };

  return [
    _scenario(_wearing(['a.omat', 'b.omat', 'a.omat']), SceneFormat.glb, materials: plain),
    _scenario(_wearing(['a.omat', 'a.omat', 'a.omat']), SceneFormat.glb, materials: plain),
    _scenario(_wearing([null, null]), SceneFormat.glb, materials: plain),
    _scenario(_wearing(['a.omat', 'a.omat', 'b.omat']), SceneFormat.glb),
    _scenario(_wearing(['missing.omat']), SceneFormat.glb, materials: plain),
    _scenario(
      _wearing(['rich.omat', 'lost.omat', 'child.omat', 'rich.omat']),
      SceneFormat.glb,
      materials: rich,
      files: _pictures,
    ),
    _scenario(
      _wearing(['rich.omat', 'child.omat']),
      SceneFormat.gltf,
      name: 'rich',
      materials: rich,
      files: _pictures,
    ),
    _scenario(
      _wearing(blends.keys.toList()),
      SceneFormat.glb,
      materials: blends,
    ),
    _scenario(
      _wearing(strengths.keys.toList()),
      SceneFormat.glb,
      materials: strengths,
    ),
    _scenario(
      _wearing(samplers.keys.toList()),
      SceneFormat.glb,
      materials: samplers,
      files: _pictures,
    ),
  ];
}

List<Map<String, Object?>> _lookScenarios() {
  SceneDocument dressed(Map<String, MaterialComponent> by) => SceneDocument(
    entities: [
      for (final e in by.entries)
        _thing(e.key, {SceneComponents.mesh: _cube, SceneComponents.material: e.value}),
    ],
  );
  final library = <String, Object?>{
    'summer.omat': {
      'values': {'baseColour': [0.9, 0.8, 0.2, 1.0]},
    },
    'snow.omat': {
      'values': {'baseColour': [1.0, 1.0, 1.0, 1.0], 'roughness': 0.9},
      'maps': {'baseColour': 'tex/a.png', 'normal': 'tex/n.ktx2'},
    },
    'dark.omat': {
      'values': {'baseColour': [0.0, 0.0, 0.1, 1.0]},
    },
  };
  const summer = MaterialComponent(
    asset: 'summer.omat',
    looks: {'winter': 'snow.omat', 'night': 'dark.omat'},
  );
  return [
    _scenario(
      dressed({'e': summer}),
      SceneFormat.glb,
    ),
    _scenario(
      dressed({
        'a': summer,
        'b': const MaterialComponent(
          asset: 'dark.omat',
          looks: {'night': 'summer.omat', 'dusk': 'snow.omat'},
        ),
        'c': const MaterialComponent(looks: {'winter': 'snow.omat'}),
      }),
      SceneFormat.glb,
      materials: library,
      files: _pictures,
    ),
    _scenario(
      dressed({'a': summer}),
      SceneFormat.gltf,
      materials: library,
      files: _pictures,
    ),
  ];
}

Uint8List _cubeGlb() => Uint8List.fromList(
  Shape.of(ShapeKind.cube).build().toGlb(
    name: 'Block',
    materials: const [GlbMaterial(name: 'Paint')],
  ),
);

// A model with something in every array the graft has to move, so a link left
// out of its table moves a number in the wrong place.
Uint8List _rig() => glbBytes({
  'asset': {'version': '2.0'},
  'scene': 1,
  'scenes': [
    {
      'nodes': [2],
    },
    {
      'nodes': [0, 2],
    },
  ],
  'nodes': [
    {
      'name': 'root',
      'children': [1],
      'skin': 0,
    },
    {
      'name': 'bone',
      'mesh': 0,
      'translation': [0, 1, 0],
    },
    {
      'name': 'lamp',
      'camera': 0,
      'extensions': {
        'KHR_lights_punctual': {'light': 0},
        'EXT_mesh_gpu_instancing': {
          'attributes': {'TRANSLATION': 3},
        },
      },
    },
  ],
  'meshes': [
    {
      'primitives': [
        {
          'attributes': {'POSITION': 0, 'NORMAL': 1},
          'indices': 2,
          'material': 0,
          'targets': [
            {'POSITION': 3},
          ],
          'extensions': {
            'KHR_materials_variants': {
              'mappings': [
                {
                  'material': 1,
                  'variants': [0],
                },
              ],
            },
            'KHR_draco_mesh_compression': {'bufferView': 1},
          },
        },
      ],
    },
  ],
  'materials': [
    {
      'pbrMetallicRoughness': {
        'baseColorTexture': {'index': 0},
        'metallicRoughnessTexture': {'index': 1, 'texCoord': 1},
      },
      'normalTexture': {'index': 0, 'scale': 0.5},
      'extensions': {
        'KHR_materials_clearcoat': {
          'clearcoatTexture': {'index': 1},
          'clearcoatFactor': 1,
        },
      },
    },
    {'name': 'alt'},
  ],
  'textures': [
    {'source': 0, 'sampler': 0},
    {
      'extensions': {
        'EXT_texture_webp': {'source': 1},
      },
    },
  ],
  'images': [
    {'bufferView': 5, 'mimeType': 'image/png'},
    {'uri': 'data:image/png;base64,AAAA'},
  ],
  'samplers': [
    {'magFilter': 9729},
  ],
  'cameras': [
    {
      'type': 'perspective',
      'perspective': {'yfov': 0.8, 'znear': 0.1},
    },
  ],
  'skins': [
    {
      'joints': [1],
      'skeleton': 1,
      'inverseBindMatrices': 4,
    },
  ],
  'animations': [
    {
      'samplers': [
        {'input': 4, 'output': 3, 'interpolation': 'LINEAR'},
      ],
      'channels': [
        {
          'sampler': 0,
          'target': {'node': 1, 'path': 'translation'},
        },
      ],
    },
  ],
  'accessors': [
    {'bufferView': 0, 'componentType': 5126, 'count': 3, 'type': 'VEC3'},
    {'bufferView': 1, 'componentType': 5126, 'count': 3, 'type': 'VEC3'},
    {'bufferView': 2, 'componentType': 5123, 'count': 3, 'type': 'SCALAR'},
    {
      'componentType': 5126,
      'count': 3,
      'type': 'VEC3',
      'sparse': {
        'count': 1,
        'indices': {'bufferView': 3, 'componentType': 5123},
        'values': {'bufferView': 4},
      },
    },
    {'bufferView': 4, 'componentType': 5126, 'count': 2, 'type': 'SCALAR'},
  ],
  'bufferViews': [
    for (var i = 0; i < 6; i++)
      {'buffer': 0, 'byteOffset': i * 8, 'byteLength': 8},
  ],
  'buffers': [
    {'byteLength': 48},
  ],
  'extensions': {
    'KHR_lights_punctual': {
      'lights': [
        {'type': 'point', 'intensity': 5},
      ],
    },
    'KHR_materials_variants': {
      'variants': [
        {'name': 'x'},
      ],
    },
  },
  'extensionsUsed': [
    'KHR_lights_punctual',
    'KHR_materials_variants',
    'EXT_texture_webp',
  ],
  'extensionsRequired': ['EXT_texture_webp'],
}, Uint8List.fromList(List.generate(48, (i) => i)));

Uint8List _library() => glbBytes({
  'asset': {'version': '2.0'},
  'nodes': [
    {
      'children': [1],
    },
    {'mesh': 0},
    {'name': 'loose'},
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
  'accessors': [
    {'bufferView': 0, 'componentType': 5126, 'count': 1, 'type': 'VEC3'},
  ],
  'bufferViews': [
    {'buffer': 0, 'byteLength': 12},
  ],
  'buffers': [
    {'byteLength': 12},
  ],
}, Uint8List(12));

Uint8List _loose(Map<String, Object?> extra) => glbBytes({
  'asset': {'version': '2.0'},
  ...extra,
}, Uint8List(0));

List<Map<String, Object?>> _modelScenarios() {
  SceneDocument placing(String asset, int times, {bool children = false}) =>
      SceneDocument(
        entities: [
          for (var i = 0; i < times; i++)
            _thing(
              'e$i',
              _placed(
                Vector3(i.toDouble(), 0, 0),
                mesh: MeshComponent(asset: asset),
              ),
              parent: children && i > 0 ? 'e${i - 1}' : null,
            ),
        ],
      );
  return [
    _scenario(placing('block.glb', 1), SceneFormat.glb, files: {'block.glb': _cubeGlb()}),
    _scenario(placing('block.glb', 3), SceneFormat.glb, files: {'block.glb': _cubeGlb()}),
    _scenario(placing('block.glb', 3, children: true), SceneFormat.gltf, files: {'block.glb': _cubeGlb()}),
    _scenario(placing('rig.glb', 1), SceneFormat.glb, files: {'rig.glb': _rig()}),
    _scenario(placing('rig.glb', 2), SceneFormat.glb, files: {'rig.glb': _rig()}),
    _scenario(placing('lib.glb', 2), SceneFormat.glb, files: {'lib.glb': _library()}),
    _scenario(
      SceneDocument(
        entities: [
          _thing('a', _placed(Vector3.zero(), mesh: const MeshComponent(asset: 'block.glb'))),
          _thing('b', _placed(Vector3(1, 0, 0), mesh: const MeshComponent(asset: 'rig.glb'))),
          _thing('c', _placed(Vector3(2, 0, 0))),
          _thing('d', _placed(Vector3(3, 0, 0), mesh: const MeshComponent(asset: 'block.glb'))),
        ],
      ),
      SceneFormat.glb,
      files: {'block.glb': _cubeGlb(), 'rig.glb': _rig()},
    ),
    _scenario(placing('block.glb', 1), SceneFormat.glb),
    _scenario(
      placing('block.glb', 1),
      SceneFormat.glb,
      files: {'block.glb': utf8.encode('{"asset":{}}')},
    ),
    _scenario(
      placing('loose.glb', 1),
      SceneFormat.glb,
      files: {
        'loose.glb': _loose({
          'buffers': [
            {'uri': 'beside.bin', 'byteLength': 4},
          ],
        }),
      },
    ),
    _scenario(
      placing('loose.glb', 1),
      SceneFormat.glb,
      files: {
        'loose.glb': _loose({
          'buffers': [
            {'byteLength': 4},
            {'byteLength': 4},
          ],
        }),
      },
    ),
    _scenario(
      placing('loose.glb', 1),
      SceneFormat.glb,
      files: {
        'loose.glb': _loose({
          'images': [
            {'uri': 'beside.png'},
          ],
        }),
      },
    ),
  ];
}

// Characters that count as white space to a trim and to a collapse, one of each
// kind, written by code so that no editor or tool turns them into line breaks.
String _spaces() => String.fromCharCodes([
  0x20, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x85, 0xA0, 0x1680, 0x2000, 0x2003,
  0x200A, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF,
]);

List<Map<String, Object?>> _objScenarios() {
  SceneDocument two() => SceneDocument(
    entities: [
      _thing('a', _placed(Vector3(10, 0, 0)), name: 'First one'),
      _thing('b', _placed(Vector3(0, 5, 0)), parent: 'a', name: 'Second'),
    ],
  );
  final skewed = SceneDocument(
    entities: [
      _thing(
        's',
        _placed(
          Vector3(0.1234567891, -2.5e-7, 100000.5),
          rotation: Vector3(15, 45, 75),
          scale: Vector3(1, 3, 0.5),
          mesh: const MeshComponent(shape: Shape(kind: ShapeKind.stairs, steps: 4)),
        ),
        name: 'Steps',
      ),
      _thing(
        'n',
        _placed(
          Vector3(-1, -2, -3),
          scale: Vector3(-1, 1, 1),
          mesh: const MeshComponent(shape: Shape(kind: ShapeKind.sphere, subdivisions: 1)),
        ),
        name: 'Mirrored',
      ),
    ],
  );
  final names = SceneDocument(
    entities: [
      _thing('1', _placed(Vector3.zero()), name: 'Thing'),
      _thing('2', _placed(Vector3(1, 0, 0)), name: 'Thing'),
      _thing('3', _placed(Vector3(2, 0, 0)), name: 'Thing'),
      _thing('4', _placed(Vector3(3, 0, 0)), name: '  ${_spaces()}a${_spaces()}b${_spaces()}  '),
      _thing('5', _placed(Vector3(4, 0, 0)), name: ''),
      _thing('6', _placed(Vector3(5, 0, 0)), name: '   '),
      _thing('7', _placed(Vector3(6, 0, 0)), name: 'tab\there'),
    ],
  );
  final lost = SceneDocument(
    entities: [
      _thing('l', const {
        SceneComponents.light: LightComponent(kind: LightType.sun),
      }),
      _thing('c', const {SceneComponents.camera: CameraComponent()}),
      _thing('m', const {
        SceneComponents.mesh: MeshComponent(asset: 'tree.glb'),
      }),
      _thing('empty', const {SceneComponents.mesh: MeshComponent()}),
      _thing('painted', _placed(Vector3.zero(), mesh: MeshComponent(geometry: _paintedCube())), name: 'Painted'),
    ],
  );
  final library = <String, Object?>{
    'a.omat': {
      'values': {
        'baseColour': [0.2, 0.4, 0.6, 0.5],
        'roughness': 0.3,
        'metallic': 1.0,
        'emissive': [1.0, 0.5, 0.0],
        'emissiveIntensity': 2.0,
        'blend': 'transparent',
        'reflectance': 0.7,
      },
      'maps': {
        'baseColour': 'tex/a.png',
        'normal': 'tex/n.ktx2',
        'metallicRoughness': 'tex/mr.jpg',
        'occlusion': 'tex/o.webp',
        'emissive': 'tex/e.png',
      },
    },
    'b.omat': {
      'values': {'baseColour': [1.0, 1.0, 1.0, 1.0]},
    },
  };
  SceneDocument wearing(List<String?> assets) => SceneDocument(
    entities: [
      for (var i = 0; i < assets.length; i++)
        _thing('e$i', {
          SceneComponents.mesh: MeshComponent(
            shape: const Shape(kind: ShapeKind.cube),
            colour: Tint.hex(0x100000 * i + 0x203040),
          ),
          SceneComponents.material: MaterialComponent(asset: assets[i]),
        }),
    ],
  );
  return [
    _scenario(two(), SceneFormat.obj),
    _scenario(two(), SceneFormat.obj, name: 'yard'),
    _scenario(skewed, SceneFormat.obj),
    _scenario(names, SceneFormat.obj),
    _scenario(lost, SceneFormat.obj),
    _scenario(SceneDocument(), SceneFormat.obj),
    _scenario(wearing(['a.omat', 'b.omat', 'a.omat', null]), SceneFormat.obj, materials: library, files: _pictures),
    _scenario(wearing(['a.omat', 'a.omat']), SceneFormat.obj),
  ];
}
