import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/gltf/atlas_pixels.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/gltf/model_atlas.dart';
import 'package:orblit_asset/src/gltf/texture_roles.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_sprite/orblit_sprite.dart' show decodePng, encodePng;

Uint8List _glb(Map<String, Object?> json, List<int> binary) {
  final text = utf8.encode(jsonEncode(json));
  final jsonLength = text.length + (4 - text.length % 4) % 4;
  final binLength = binary.length + (4 - binary.length % 4) % 4;
  final out = Uint8List(12 + 8 + jsonLength + 8 + binLength);
  final data = ByteData.sublistView(out)
    ..setUint32(0, 0x46546C67, Endian.little)
    ..setUint32(4, 2, Endian.little)
    ..setUint32(8, out.length, Endian.little)
    ..setUint32(12, jsonLength, Endian.little)
    ..setUint32(16, 0x4E4F534A, Endian.little);
  out
    ..fillRange(20, 20 + jsonLength, 0x20)
    ..setRange(20, 20 + text.length, text);
  final at = 20 + jsonLength;
  data
    ..setUint32(at, binLength, Endian.little)
    ..setUint32(at + 4, 0x004E4942, Endian.little);
  out.setRange(at + 8, at + 8 + binary.length, binary);
  return out;
}

/// Texels from a linear congruential generator, so a case is the same on every
/// run. A [sparse] image has every third texel clear.
Uint8List _noise(
  int width,
  int height,
  int seed, {
  bool opaque = false,
  bool sparse = false,
}) {
  var state = seed;
  final out = Uint8List(width * height * 4);
  for (var i = 0; i < out.length; i++) {
    state = (state * 1103515245 + 12345) & 0x7fffffff;
    out[i] = (state >> 16) & 0xff;
  }
  for (var i = 3; i < out.length; i += 4) {
    if (opaque) out[i] = 255;
    if (sparse && (i ~/ 4) % 3 == 0) out[i] = 0;
  }
  return out;
}

Uint8List _solid(int width, int height, List<int> rgba) {
  final out = Uint8List(width * height * 4);
  for (var i = 0; i < out.length; i += 4) {
    out.setRange(i, i + 4, rgba);
  }
  return out;
}

const _red = [220, 40, 40, 255];
const _blue = [40, 40, 220, 255];
const _green = [40, 220, 40, 255];
const _bump = [200, 120, 255, 255];

/// Quads drawn with materials and textures the case picks.
class _Model {
  final Map<String, Object?> json = {
    'asset': {'version': '2.0'},
    'images': <Object?>[],
    'textures': <Object?>[],
    'materials': <Object?>[],
    'meshes': <Object?>[
      <String, Object?>{'primitives': <Object?>[]},
    ],
    'nodes': <Object?>[
      <String, Object?>{'mesh': 0},
    ],
    'scenes': <Object?>[
      <String, Object?>{
        'nodes': [0],
      },
    ],
    'scene': 0,
    'accessors': <Object?>[],
    'bufferViews': <Object?>[],
  };
  final BytesBuilder _bin = BytesBuilder(copy: false);

  List<Object?> get _images => json['images']! as List<Object?>;
  List<Object?> get _textures => json['textures']! as List<Object?>;
  List<Object?> get _materials => json['materials']! as List<Object?>;
  List<Object?> get _accessors => json['accessors']! as List<Object?>;
  List<Object?> get primitives =>
      ((json['meshes']! as List).first as Map)['primitives']! as List<Object?>;

  int _view(List<int> bytes) {
    final views = json['bufferViews']! as List;
    views.add({
      'buffer': 0,
      'byteOffset': _bin.length,
      'byteLength': bytes.length,
    });
    _bin
      ..add(bytes)
      ..add(Uint8List((4 - _bin.length % 4) % 4));
    return views.length - 1;
  }

  int _accessor(Map<String, Object?> value) {
    _accessors.add(value);
    return _accessors.length - 1;
  }

  /// A texture over [pixels], returning its texture index.
  int texture(int width, int height, Uint8List pixels) =>
      rawTexture(encodePng(width, height, pixels));

  int solid(List<int> rgba, {int size = 8}) =>
      texture(size, size, _solid(size, size, rgba));

  /// A texture whose image is [bytes], whatever they are.
  int rawTexture(List<int> bytes) {
    _images.add({'bufferView': _view(bytes), 'mimeType': 'image/png'});
    _textures.add({'source': _images.length - 1});
    return _textures.length - 1;
  }

  int material(Map<String, Object?> value) {
    _materials.add(value);
    return _materials.length - 1;
  }

  /// A quad from [x] to x + 1. [sets] says which TEXCOORD sets it carries, and
  /// [reuse] points TEXCOORD_0 at an accessor another quad made.
  Map<String, Object?> quad(
    Object? material, {
    double x = 0,
    List<double>? uv,
    List<int> sets = const [0],
    int? reuse,
  }) {
    final positions = Float32List.fromList([
      x,
      0,
      0,
      x + 1,
      0,
      0,
      x + 1,
      1,
      0,
      x,
      1,
      0,
    ]);
    final coords = Float32List.fromList(uv ?? const [0, 0, 1, 0, 1, 1, 0, 1]);
    final attributes = <String, Object?>{
      'POSITION': _accessor({
        'bufferView': _view(Uint8List.sublistView(positions)),
        'componentType': 5126,
        'count': 4,
        'type': 'VEC3',
        'min': [x, 0, 0],
        'max': [x + 1, 1, 0],
      }),
    };
    for (final set in sets) {
      attributes['TEXCOORD_$set'] = reuse != null && set == 0
          ? reuse
          : _accessor({
              'bufferView': _view(Uint8List.sublistView(coords)),
              'componentType': 5126,
              'count': 4,
              'type': 'VEC2',
            });
    }
    final index = _accessor({
      'bufferView': _view(
        Uint8List.sublistView(Uint16List.fromList(const [0, 1, 2, 0, 2, 3])),
      ),
      'componentType': 5123,
      'count': 6,
      'type': 'SCALAR',
    });
    final primitive = <String, Object?>{
      'attributes': attributes,
      'indices': index,
      if (material != null) 'material': material,
      'mode': 4,
    };
    primitives.add(primitive);
    return primitive;
  }

  Uint8List toGlb() {
    final bytes = _bin.toBytes();
    json['buffers'] = [
      {'byteLength': bytes.length},
    ];
    return _glb(json, bytes);
  }
}

Uint8List _build(void Function(_Model model) body) {
  final model = _Model();
  body(model);
  return model.toGlb();
}

Map<String, Object?> _slot(
  int texture, [
  Map<String, Object?> extra = const {},
]) => {'index': texture, ...extra};

Map<String, Object?> _colour(
  int texture, [
  Map<String, Object?> extra = const {},
]) => {
  'pbrMetallicRoughness': {'baseColorTexture': _slot(texture), ...extra},
};

Future<Map<String, Object?>> _attempt(Future<Object?> Function() body) async {
  try {
    return {'value': await body()};
  } on Object {
    return {'throws': true};
  }
}

Map<String, Object?> _parts(Uint8List glb) {
  final data = ByteData.sublistView(glb);
  final at = 20 + data.getUint32(12, Endian.little);
  final end = at + 8 + data.getUint32(at, Endian.little);
  return {
    'json': utf8.decode(glb.sublist(20, at)),
    'bin': b64(glb.sublist(at + 8, end)),
  };
}

Map<String, Object?> _rgba(Rgba image) => {
  'width': image.width,
  'height': image.height,
  'pixels': b64(image.pixels),
};

Rgba _image(int width, int height, Uint8List pixels) =>
    Rgba(width, height, Uint8List.fromList(pixels));

// resample

Future<Map<String, Object?>> _resampleCase(
  (int, int) from,
  (int, int) to,
  int seed, {
  bool normal = false,
  bool sparse = false,
}) async {
  final pixels = _noise(from.$1, from.$2, seed, sparse: sparse);
  return {
    'op': 'resample',
    'in': [b64(pixels), from.$1, from.$2, to.$1, to.$2, normal],
    'expect': await _attempt(
      () async => _rgba(
        resample(
          _image(from.$1, from.$2, pixels),
          to.$1,
          to.$2,
          normal: normal,
        ),
      ),
    ),
  };
}

Future<List<Map<String, Object?>>> _resampleCases() async {
  const sizes = [
    ((1, 1), (1, 1)),
    ((2, 2), (1, 1)),
    ((4, 4), (2, 2)),
    ((3, 5), (2, 2)),
    ((5, 3), (7, 2)),
    ((1, 1), (4, 4)),
    ((8, 8), (3, 3)),
    ((7, 9), (16, 16)),
    ((16, 16), (1, 16)),
    ((6, 6), (0, 3)),
    ((6, 6), (3, 0)),
    ((6, 6), (-2, 3)),
    ((2, 2), (2, 2)),
    ((10, 1), (3, 1)),
    ((9, 9), (4, 4)),
    ((13, 7), (5, 11)),
    ((32, 32), (7, 5)),
    ((2, 3), (3, 2)),
    // One side the same and the other not, and sizes where the far edge of the
    // last texel lands a rounding error past the source.
    ((6, 6), (6, 3)),
    ((4, 4), (4, 8)),
    ((7, 7), (25, 25)),
    ((7, 3), (25, 5)),
  ];
  return [
    for (final (i, (from, to)) in sizes.indexed)
      for (final normal in [false, true])
        for (final sparse in [false, true])
          await _resampleCase(
            from,
            to,
            17 + i * 5,
            normal: normal,
            sparse: sparse,
          ),
  ];
}

// renormalise

Future<Map<String, Object?>> _renormaliseCase(
  int width,
  int height,
  Uint8List pixels,
) async => {
  'op': 'renormalise',
  'in': [b64(pixels), width, height],
  'expect': await _attempt(() async {
    final image = _image(width, height, pixels);
    renormalise(image);
    return _rgba(image);
  }),
};

Future<List<Map<String, Object?>>> _renormaliseCases() async => [
  await _renormaliseCase(2, 1, _solid(2, 1, const [255, 128, 128, 255])),
  await _renormaliseCase(1, 1, _solid(1, 1, const [128, 128, 128, 255])),
  await _renormaliseCase(1, 1, _solid(1, 1, const [127, 128, 127, 0])),
  await _renormaliseCase(1, 1, _solid(1, 1, const [0, 0, 0, 255])),
  await _renormaliseCase(1, 1, _solid(1, 1, const [255, 255, 255, 255])),
  await _renormaliseCase(1, 1, _solid(1, 1, const [128, 128, 255, 255])),
  await _renormaliseCase(1, 1, _solid(1, 1, const [255, 0, 0, 7])),
  await _renormaliseCase(0, 0, Uint8List(0)),
  for (var seed = 1; seed <= 6; seed++)
    await _renormaliseCase(5, 4, _noise(5, 4, seed * 31)),
  await _renormaliseCase(16, 16, _noise(16, 16, 99)),
];

// multiplyInto

Future<Map<String, Object?>> _multiplyCase(
  Uint8List pixels,
  int width,
  int height,
  List<double> factor,
  bool srgb,
) async => {
  'op': 'multiply',
  'in': [
    b64(pixels),
    width,
    height,
    [for (final f in factor) number(f)],
    srgb,
  ],
  'expect': await _attempt(() async {
    final image = _image(width, height, pixels);
    multiplyInto(image, factor, srgb: srgb);
    return _rgba(image);
  }),
};

Future<List<Map<String, Object?>>> _multiplyCases() async {
  // Every value of every channel, so the whole sRGB table is read.
  final ramp = Uint8List(256 * 4);
  for (var i = 0; i < 256; i++) {
    ramp[i * 4] = i;
    ramp[i * 4 + 1] = 255 - i;
    ramp[i * 4 + 2] = (i * 7) & 255;
    ramp[i * 4 + 3] = (i * 3) & 255;
  }
  const factors = <List<double>>[
    [0.5, 0.5, 0.5, 1.0],
    [1.0, 1.0, 1.0, 1.0],
    [0.0, 0.0, 0.0, 0.0],
    [2.0, 2.0, 2.0, 2.0],
    [-1.0, 0.3, 1.5, 0.7],
    [0.1234, 0.5678, 0.9, 0.25],
    [1e-9, 1e-3, 0.999, 0.001],
    [0.5, 0.25, 0.125, 0.0625],
    [1.0, 0.0, 1.0, 0.5],
    [0.7, 0.7, 0.7, 1.0],
  ];
  return [
    for (final factor in factors)
      for (final srgb in [false, true])
        await _multiplyCase(ramp, 256, 1, factor, srgb),
    // What a number JSON cannot hold does to each path.
    for (final srgb in [false, true])
      for (final odd in [double.nan, double.infinity, double.negativeInfinity])
        await _multiplyCase(ramp, 256, 1, [odd, 1.0, 1.0, 1.0], srgb),
    // A short factor and a longer one.
    await _multiplyCase(ramp, 256, 1, const [0.5, 0.5, 0.5], true),
    await _multiplyCase(ramp, 256, 1, const [0.5, 0.5, 0.5, 0.5, 0.5], false),
    await _multiplyCase(Uint8List(0), 0, 0, const [0.5, 0.5, 0.5, 0.5], true),
  ];
}

// blit

Future<Map<String, Object?>> _blitCase(
  int pageWidth,
  int pageHeight,
  Uint8List page,
  List<(int, int, Uint8List, int, int, int)> blits,
) async => {
  'op': 'blit',
  'in': [
    b64(page),
    pageWidth,
    pageHeight,
    [
      for (final (w, h, pixels, x, y, extrude) in blits)
        [b64(pixels), w, h, x, y, extrude],
    ],
  ],
  'expect': await _attempt(() async {
    final canvas = _image(pageWidth, pageHeight, page);
    for (final (w, h, pixels, x, y, extrude) in blits) {
      blit(canvas, _image(w, h, pixels), x, y, extrude: extrude);
    }
    return _rgba(canvas);
  }),
};

Future<List<Map<String, Object?>>> _blitCases() async {
  final page = _solid(12, 10, const [255, 255, 255, 255]);
  final noisy = _noise(12, 10, 5);
  final cell = _noise(4, 3, 9, opaque: true);
  final one = _solid(1, 1, _red);
  return [
    for (final extrude in [0, 1, 2, 3, 4, -1])
      await _blitCase(12, 10, page, [(4, 3, cell, 4, 3, extrude)]),
    for (final (x, y) in const [
      (0, 0),
      (8, 7),
      (9, 8),
      (-1, -1),
      (-3, 4),
      (10, -2),
      (20, 20),
      (-20, -20),
      (5, 12),
      (14, 2),
      // Cells whose edge texels are off the page while their skirt is on it.
      (12, 3),
      (13, 3),
      (4, 10),
      (4, 11),
      (-4, 3),
      (-5, 3),
      (4, -3),
      (4, -4),
    ])
      for (final extrude in [0, 2])
        await _blitCase(12, 10, noisy, [(4, 3, cell, x, y, extrude)]),
    // Cells whose skirts overlap, in both orders.
    await _blitCase(12, 10, page, [
      (4, 3, cell, 2, 2, 2),
      (4, 3, _solid(4, 3, _blue), 6, 3, 2),
    ]),
    await _blitCase(12, 10, page, [
      (4, 3, _solid(4, 3, _blue), 6, 3, 2),
      (4, 3, cell, 2, 2, 2),
    ]),
    await _blitCase(12, 10, page, [
      (4, 3, cell, 0, 0, 4),
      (4, 3, _solid(4, 3, _green), 4, 0, 4),
      (4, 3, _solid(4, 3, _blue), 2, 3, 4),
    ]),
    await _blitCase(8, 8, _solid(8, 8, const [0, 0, 0, 0]), [
      (1, 1, one, 3, 3, 3),
    ]),
    await _blitCase(8, 8, _solid(8, 8, const [0, 0, 0, 0]), [
      (1, 1, one, 0, 0, 5),
    ]),
    // A cell bigger than the page.
    await _blitCase(3, 3, _solid(3, 3, const [0, 0, 0, 0]), [
      (5, 4, _noise(5, 4, 3), 1, 1, 1),
    ]),
    await _blitCase(3, 3, _solid(3, 3, const [0, 0, 0, 0]), [
      (5, 4, _noise(5, 4, 3), -2, -1, 0),
    ]),
    await _blitCase(0, 0, Uint8List(0), [(1, 1, one, 0, 0, 1)]),
  ];
}

Future<List<Map<String, Object?>>> _blankCases() async => [
  for (final role in TextureRole.values)
    for (final (w, h) in const [(1, 1), (3, 2), (0, 4)])
      {
        'op': 'blank',
        'in': [role.index, w, h],
        'expect': await _attempt(() async => _rgba(blankPage(role, w, h))),
      },
];

// atlasModel

typedef _Spec = (Uint8List, ModelAtlasOptions, Set<int>);

ImageDecoder _decoder(GltfDocument document, Set<int> blind) =>
    (index, definition) async {
      if (blind.contains(index)) return null;
      final view = definition['bufferView'];
      if (view is! int) return null;
      final png = decodePng(document.viewBytes(view));
      return Rgba(png.width, png.height, png.pixels);
    };

Future<Map<String, Object?>> _atlasCase(_Spec spec) async {
  final (model, options, blind) = spec;
  return {
    'op': 'atlas',
    'in': [
      b64(model),
      [
        options.maxPageSize,
        options.padding,
        options.extrude,
        options.maxCellSize,
        options.minMaterials,
        options.mergePrimitives,
        options.maxMergedVertices,
      ],
      [...blind],
    ],
    'expect': await _attempt(() async {
      final document = await GltfDocument.read(
        AssetId.parse('models/thing.glb'),
        model,
        MemoryAssetSource(const {}),
      );
      final result = await atlasModel(
        document,
        options: options,
        decode: _decoder(document, blind),
      );
      return {
        'changed': result.changed,
        'notes': result.notes,
        'pages': [
          for (final page in result.pages)
            {'stem': page.stem, 'role': page.role.name, ..._rgba(page.image)},
        ],
        ..._parts(document.toGlb()),
      };
    }),
  };
}

const _plain = ModelAtlasOptions();
const _separate = ModelAtlasOptions(mergePrimitives: false);
const _noBlind = <int>{};

/// Two quads, each with one solid base colour.
Uint8List _pair({double second = 2, int size = 8}) => _build((m) {
  final a = m.solid(_red, size: size);
  final b = m.solid(_blue, size: size);
  m.quad(m.material(_colour(a)));
  m.quad(m.material(_colour(b)), x: second);
});

/// Models with a texture of each of the five roles in the first material.
Uint8List _allRoles({
  Map<String, Object?> pbr = const {},
  Map<String, Object?> extra = const {},
  Map<String, Object?> normal = const {},
  Map<String, Object?> occlusion = const {},
}) => _build((m) {
  final colour = m.texture(8, 8, _noise(8, 8, 1, opaque: true));
  final rough = m.texture(8, 8, _noise(8, 8, 2, opaque: true));
  final bumps = m.texture(8, 8, _noise(8, 8, 3, opaque: true));
  final ao = m.texture(8, 8, _noise(8, 8, 4, opaque: true));
  final glow = m.texture(8, 8, _noise(8, 8, 5, opaque: true));
  final plain = m.solid(_green);
  m.quad(
    m.material({
      'pbrMetallicRoughness': {
        'baseColorTexture': _slot(colour),
        'metallicRoughnessTexture': _slot(rough),
        ...pbr,
      },
      'normalTexture': _slot(bumps, normal),
      'occlusionTexture': _slot(ao, occlusion),
      'emissiveTexture': _slot(glow),
      ...extra,
    }),
  );
  m.quad(m.material(_colour(plain)), x: 2);
});

List<_Spec> _atlasSpecs() {
  final specs = <_Spec>[
    (_pair(), _separate, _noBlind),
    (_pair(), _plain, _noBlind),
    (_pair(size: 4), _plain, _noBlind),
    // Two materials that point at one image.
    (
      _build((m) {
        final a = m.solid(_red);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(a)), x: 2);
        m.quad(m.material(_colour(a)), x: 4);
      }),
      _plain,
      _noBlind,
    ),
    // Different sizes, noise rather than colour.
    (
      _build((m) {
        final a = m.texture(8, 8, _noise(8, 8, 11));
        final b = m.texture(4, 6, _noise(4, 6, 12, sparse: true));
        final c = m.texture(16, 16, _noise(16, 16, 13, opaque: true));
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        m.quad(m.material(_colour(c)), x: 4);
      }),
      _plain,
      _noBlind,
    ),
  ];

  final sizes = _build((m) {
    final a = m.texture(8, 8, _noise(8, 8, 21));
    final b = m.texture(16, 12, _noise(16, 12, 22));
    final c = m.texture(3, 7, _noise(3, 7, 23));
    m.quad(m.material(_colour(a)));
    m.quad(m.material(_colour(b)), x: 2);
    m.quad(m.material(_colour(c)), x: 4);
  });
  for (final cap in [0, 1, 2, 4, 5, 10, 100, 4000]) {
    specs.add((
      sizes,
      ModelAtlasOptions(maxCellSize: cap, mergePrimitives: false),
      _noBlind,
    ));
  }
  for (final (page, padding, extrude) in const [
    (2048, 4, 2),
    (2048, 0, 2),
    (2048, 2, 4),
    (2048, 4, 4),
    (2048, 1, 1),
    (2048, 6, 3),
    (2048, 3, 0),
    (64, 4, 2),
    (32, 4, 2),
    (16, 2, 2),
    (16, 4, 2),
    (8, 4, 2),
    (8, 0, 0),
    (4, 0, 0),
    (1, 0, 0),
  ]) {
    specs.add((
      sizes,
      ModelAtlasOptions(
        maxPageSize: page,
        padding: padding,
        extrude: extrude,
        mergePrimitives: false,
      ),
      _noBlind,
    ));
  }

  // Several pages: many cells and a small page.
  final many = _build((m) {
    for (var i = 0; i < 6; i++) {
      final t = m.texture(8, 8, _noise(8, 8, 40 + i, opaque: true));
      m.quad(m.material(_colour(t)), x: i * 2.0);
    }
  });
  for (final page in [64, 32, 24, 20]) {
    specs.add((
      many,
      ModelAtlasOptions(maxPageSize: page, mergePrimitives: false),
      _noBlind,
    ));
    specs.add((many, ModelAtlasOptions(maxPageSize: page), _noBlind));
  }
  specs.add((many, const ModelAtlasOptions(maxMergedVertices: 4), _noBlind));
  specs.add((many, const ModelAtlasOptions(maxMergedVertices: 8), _noBlind));
  specs.add((many, const ModelAtlasOptions(maxMergedVertices: 13), _noBlind));
  specs.add((many, const ModelAtlasOptions(maxMergedVertices: 0), _noBlind));

  // Every role, then the same with a different shape of rewrite.
  specs
    ..add((_allRoles(), _separate, _noBlind))
    ..add((_allRoles(), _plain, _noBlind))
    ..add((
      _allRoles(
        normal: {
          'scale': 2.5,
          'extras': {'n': 1},
        },
        occlusion: {'strength': 0.5, 'texCoord': 0},
        extra: {
          'emissiveFactor': [0.5, 1.0, 0.25],
          'extensions': {
            'KHR_materials_emissive_strength': {'emissiveStrength': 3},
          },
        },
        pbr: {
          'metallicFactor': 0.5,
          'roughnessFactor': 0.2,
          'baseColorFactor': [0.5, 0.25, 1.0, 0.5],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((
      _allRoles(
        extra: {
          'emissiveFactor': [2.0, 1.0, 1.0],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((
      _allRoles(
        extra: {
          'emissiveFactor': [1, 1, 1],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((
      _allRoles(
        extra: {
          'emissiveFactor': [0, 0, 0],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((_allRoles(extra: {'emissiveFactor': 'x'}), _separate, _noBlind))
    ..add((
      _allRoles(
        extra: {
          'emissiveFactor': [1, 'a', 0.5],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((_allRoles(pbr: {'metallicFactor': 0}), _separate, _noBlind))
    ..add((
      _allRoles(pbr: {'metallicFactor': 1, 'roughnessFactor': 1}),
      _separate,
      _noBlind,
    ))
    ..add((_allRoles(pbr: {'roughnessFactor': 0.0}), _separate, _noBlind))
    ..add((_allRoles(pbr: {'metallicFactor': 'x'}), _separate, _noBlind))
    ..add((
      _allRoles(
        pbr: {
          'baseColorFactor': [1, 'a', 0.5, 1],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((
      _allRoles(
        pbr: {
          'baseColorFactor': [1, 0, 0, 1],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((
      _allRoles(
        pbr: {
          'baseColorFactor': [1.0, 1.0, 1.0, 1.0],
        },
      ),
      _separate,
      _noBlind,
    ))
    ..add((_allRoles(pbr: {'baseColorFactor': 'red'}), _separate, _noBlind))
    ..add((
      _allRoles(
        pbr: {
          'baseColorFactor': [0.5],
        },
      ),
      _separate,
      _noBlind,
    ));

  // A factor with no texture to carry it.
  specs.add((
    _build((m) {
      final a = m.solid(_red);
      final b = m.solid(_blue);
      m.quad(
        m.material({
          'pbrMetallicRoughness': {
            'baseColorTexture': _slot(a),
            'baseColorFactor': [0.5, 0.5, 0.5, 1.0],
          },
          'normalTexture': _slot(b),
        }),
      );
      m.quad(
        m.material({
          'pbrMetallicRoughness': {
            'baseColorTexture': _slot(a),
            'metallicFactor': 0.25,
            'roughnessFactor': 0.75,
          },
          'emissiveFactor': [0.3, 0.6, 0.9],
        }),
        x: 2,
      );
      m.quad(
        m.material({
          'pbrMetallicRoughness': {
            'baseColorFactor': [1.0, 0.0, 0.0, 1.0],
          },
        }),
        x: 4,
      );
      m.quad(m.material(<String, Object?>{}), x: 6);
    }),
    _separate,
    _noBlind,
  ));

  // Two tints of one image become one material.
  specs.add((
    _build((m) {
      final white = m.solid(const [255, 255, 255, 255]);
      m.quad(
        m.material(
          _colour(white, {
            'baseColorFactor': [1.0, 0.0, 0.0, 1.0],
          }),
        ),
      );
      m.quad(
        m.material(
          _colour(white, {
            'baseColorFactor': [0.0, 0.0, 1.0, 1.0],
          }),
        ),
        x: 2,
      );
    }),
    _plain,
    _noBlind,
  ));

  // The blank a material gets for a map it never had.
  specs.add((
    _build((m) {
      final colour = m.solid(_red);
      final bumps = m.solid(_bump);
      m.quad(
        m.material({
          'pbrMetallicRoughness': {'baseColorTexture': _slot(colour)},
          'normalTexture': _slot(bumps),
        }),
      );
      m.quad(m.material(_colour(colour)), x: 2);
    }),
    _separate,
    _noBlind,
  ));

  specs
    ..addAll(_leftAlone())
    ..addAll(_oddModels())
    ..addAll(_sizesAndLimits());
  return specs;
}

List<_Spec> _leftAlone() {
  Uint8List tiled(List<double> uv) => _build((m) {
    final a = m.solid(_red);
    final b = m.solid(_blue);
    m.quad(m.material(_colour(a)), uv: uv);
    m.quad(m.material(_colour(b)), x: 2);
  });

  Uint8List withSlot(Map<String, Object?> Function(int a) slot) => _build((m) {
    final a = m.solid(_red);
    final b = m.solid(_blue);
    m.quad(
      m.material({
        'pbrMetallicRoughness': {'baseColorTexture': slot(a)},
      }),
    );
    m.quad(m.material(_colour(b)), x: 2);
  });

  Uint8List withMaterial(Map<String, Object?> extra, {int quads = 1}) =>
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final c = m.solid(_green);
        m.quad(m.material({..._colour(a), ...extra}));
        for (var i = 0; i < quads; i++) {
          m.quad(m.material(_colour(i.isEven ? b : c)), x: 2.0 + i * 2);
        }
      });

  return [
    for (final uv in const <List<double>>[
      [0, 0, 4, 0, 4, 4, 0, 4],
      [0, 0, 1, 0, 1, 1, 0, 1],
      [0, 0, 1.0001, 0, 1, 1, 0, 1],
      [0, 0, 1.001, 0, 1, 1, 0, 1],
      [0, 0, 1.01, 0, 1, 1, 0, 1],
      [-0.0009, 0, 1, 0, 1, 1, 0, 1],
      [-0.001, 0, 1, 0, 1, 1, 0, 1],
      [-0.002, 0, 1, 0, 1, 1, 0, 1],
      [0, 0, 1, 0, 1, 1, 0, -0.5],
      [0, 0, 1, 0, 1, 1, 0, 1.0005],
      [0.2, 0.2, 0.8, 0.2, 0.8, 0.8, 0.2, 0.8],
      [0, 0, 0, 0, 0, 0, 0, 0],
      [1, 1, 1, 1, 1, 1, 1, 1],
      [0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5],
      [0, 0, 1, 0, 1, 1, 0, double.nan],
      [0, 0, 1, 0, 1, 1, 0, double.infinity],
    ])
      (tiled(uv), _plain, _noBlind),
    (
      withSlot(
        (a) => _slot(a, {
          'extensions': {
            'KHR_texture_transform': {
              'offset': [0.5, 0.5],
            },
          },
        }),
      ),
      _plain,
      _noBlind,
    ),
    (
      withSlot(
        (a) => _slot(a, {
          'extensions': {'KHR_texture_transform': <String, Object?>{}},
        }),
      ),
      _plain,
      _noBlind,
    ),
    (
      withSlot(
        (a) => _slot(a, {
          'extensions': {'OTHER': <String, Object?>{}},
        }),
      ),
      _plain,
      _noBlind,
    ),
    (withSlot((a) => _slot(a, {'extensions': 'x'})), _plain, _noBlind),
    (
      withSlot((a) => _slot(a, {'extensions': <String, Object?>{}})),
      _plain,
      _noBlind,
    ),
    (withSlot((a) => _slot(99)), _plain, _noBlind),
    (withSlot((a) => _slot(-1)), _plain, _noBlind),
    (withSlot((a) => _slot(a, {'texCoord': 1})), _plain, _noBlind),
    (withSlot((a) => _slot(a, {'texCoord': 0})), _plain, _noBlind),
    (withSlot((a) => <String, Object?>{'index': 'a'}), _plain, _noBlind),
    (withSlot((a) => <String, Object?>{'index': 1.5}), _plain, _noBlind),
    (withSlot((a) => <String, Object?>{}), _plain, _noBlind),
    (withSlot((a) => _slot(a, {'texCoord': 'x'})), _plain, _noBlind),
    (
      withMaterial({
        'extensions': {
          'KHR_materials_clearcoat': {'clearcoatFactor': 1.0},
        },
      }),
      _plain,
      _noBlind,
    ),
    (
      withMaterial({
        'name': 'shiny',
        'extensions': {
          'KHR_materials_clearcoat': <String, Object?>{},
          'KHR_materials_sheen': <String, Object?>{},
        },
      }),
      _plain,
      _noBlind,
    ),
    (
      withMaterial({
        'name': 7,
        'extensions': {'KHR_materials_clearcoat': <String, Object?>{}},
      }),
      _plain,
      _noBlind,
    ),
    (
      withMaterial({
        'extensions': {
          'KHR_materials_emissive_strength': {'emissiveStrength': 3},
          'KHR_materials_unlit': <String, Object?>{},
          'KHR_materials_ior': {'ior': 1.4},
        },
      }),
      _plain,
      _noBlind,
    ),
    (
      withMaterial({
        'extensions': {
          'KHR_materials_ior': <String, Object?>{},
          'KHR_materials_volume': <String, Object?>{},
        },
      }),
      _plain,
      _noBlind,
    ),
    (withMaterial({'extensions': 'x'}), _plain, _noBlind),
    (withMaterial({'extensions': <String, Object?>{}}), _plain, _noBlind),
    (
      withMaterial({'name': 'ok', 'doubleSided': true}, quads: 2),
      _plain,
      _noBlind,
    ),
    (
      withMaterial({'name': 'ok'}, quads: 2),
      const ModelAtlasOptions(minMaterials: 3),
      _noBlind,
    ),
    (
      withMaterial({}, quads: 2),
      const ModelAtlasOptions(minMaterials: 4),
      _noBlind,
    ),
    (
      withMaterial({}, quads: 2),
      const ModelAtlasOptions(minMaterials: 1),
      _noBlind,
    ),
    (
      withMaterial({}, quads: 2),
      const ModelAtlasOptions(minMaterials: 0),
      _noBlind,
    ),
    (
      withMaterial({}, quads: 2),
      const ModelAtlasOptions(minMaterials: -1),
      _noBlind,
    ),
    // One of three cannot be decoded, then two, then all.
    (withMaterial({}, quads: 2), _plain, {0}),
    (withMaterial({}, quads: 2), _plain, {1}),
    (withMaterial({}, quads: 2), _plain, {0, 1}),
    (withMaterial({}, quads: 2), _plain, {0, 1, 2}),
    (
      withMaterial({}, quads: 2),
      const ModelAtlasOptions(minMaterials: 1),
      {0, 1},
    ),
    (_pair(), _plain, {0}),
    (_pair(), const ModelAtlasOptions(minMaterials: 1), {0}),
    // Two roles for one texture, with a spare material.
    (
      _build((m) {
        final both = m.solid(_bump);
        final plain = m.solid(_red);
        final other = m.solid(_blue);
        m.quad(m.material({..._colour(both), 'normalTexture': _slot(both)}));
        m.quad(m.material(_colour(plain)), x: 2);
        m.quad(m.material(_colour(other)), x: 4);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final both = m.solid(_bump);
        final plain = m.solid(_red);
        m.quad(m.material({..._colour(both), 'normalTexture': _slot(both)}));
        m.quad(m.material(_colour(plain)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // A texture another material uses for something else.
    (
      _build((m) {
        final shared = m.solid(_bump);
        final other = m.solid(_red);
        m.quad(m.material(_colour(shared)));
        m.quad(
          m.material({..._colour(other), 'normalTexture': _slot(shared)}),
          x: 2,
        );
      }),
      _plain,
      _noBlind,
    ),
  ];
}

List<_Spec> _sizesAndLimits() {
  // A texture whose edge texels are clear, so a packer that trims would shrink it.
  Uint8List bordered(int seed) {
    final pixels = _noise(8, 8, seed, opaque: true);
    for (var i = 0; i < 64; i++) {
      final x = i % 8;
      final y = i ~/ 8;
      if (x == 0 || y == 0 || x == 7 || y == 7) pixels[i * 4 + 3] = 0;
    }
    return pixels;
  }

  final sizes = _build((m) {
    final a = m.texture(8, 8, _noise(8, 8, 21));
    final b = m.texture(16, 12, _noise(16, 12, 22));
    final c = m.texture(3, 7, _noise(3, 7, 23));
    m.quad(m.material(_colour(a)));
    m.quad(m.material(_colour(b)), x: 2);
    m.quad(m.material(_colour(c)), x: 4);
  });
  final many = _build((m) {
    for (var i = 0; i < 4; i++) {
      final t = m.texture(8, 8, _noise(8, 8, 70 + i, opaque: true));
      m.quad(m.material(_colour(t)), x: i * 2.0);
    }
  });
  return [
    // A model with no materials stays untouched even when nothing is required.
    (
      _build((m) => m.quad(null)),
      const ModelAtlasOptions(minMaterials: 0),
      _noBlind,
    ),
    // The cell follows the largest map, and a normal map is renormalised when
    // it is stretched to it.
    (
      _build((m) {
        final colour = m.texture(16, 8, _noise(16, 8, 61, opaque: true));
        final bumps = m.texture(4, 4, _noise(4, 4, 62, opaque: true));
        final rough = m.texture(8, 4, _noise(8, 4, 63, opaque: true));
        final plain = m.solid(_green);
        m.quad(
          m.material({
            'pbrMetallicRoughness': {
              'baseColorTexture': _slot(colour),
              'metallicRoughnessTexture': _slot(rough),
            },
            'normalTexture': _slot(bumps),
          }),
        );
        m.quad(m.material(_colour(plain)), x: 2);
      }),
      _separate,
      _noBlind,
    ),
    // A texture index one past the last texture.
    (
      _build((m) {
        final a = m.solid(_red);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(1)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // A cell cap bigger than the page leaves the page as the limit.
    (
      sizes,
      const ModelAtlasOptions(
        maxPageSize: 16,
        padding: 2,
        maxCellSize: 100,
        mergePrimitives: false,
      ),
      _noBlind,
    ),
    // A page too small for the padding alone, so every cell is a problem.
    (
      many,
      const ModelAtlasOptions(
        maxPageSize: 8,
        padding: 8,
        mergePrimitives: false,
      ),
      _noBlind,
    ),
    // A material reference that is not an integer is not a use of a material.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        m.quad(1.0, x: 4);
        m.quad('0', x: 6);
      }),
      _separate,
      _noBlind,
    ),
    // Cells with a clear border keep it: nothing is trimmed.
    (
      _build((m) {
        final a = m.texture(8, 8, bordered(81));
        final b = m.texture(8, 8, bordered(82));
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // Two primitives of one material, other than the first, share a UV accessor.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final c = m.solid(_green);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        final third = m.material(_colour(c));
        final first = m.quad(third, x: 4);
        m.quad(
          third,
          x: 6,
          reuse:
              (first['attributes']! as Map<String, Object?>)['TEXCOORD_0']!
                  as int,
        );
      }),
      _separate,
      _noBlind,
    ),
  ];
}

List<_Spec> _oddModels() {
  return [
    // No materials at all, and a document with the key missing.
    (_build((m) => m.quad(null)), _plain, _noBlind),
    (
      _build((m) {
        m.json.remove('materials');
        m.quad(null);
      }),
      _plain,
      _noBlind,
    ),
    // Materials with nothing to pack.
    (
      _build((m) {
        m.quad(m.material(<String, Object?>{}));
        m.quad(m.material({'name': 'flat'}), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        m.quad(m.material(<String, Object?>{}));
        m.quad(m.material({'name': 'flat'}), x: 2);
      }),
      const ModelAtlasOptions(minMaterials: 0),
      _noBlind,
    ),
    // A texture with no image, then one naming an image that is not there.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        (m.json['textures']! as List)[1] = <String, Object?>{};
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        (m.json['textures']! as List)[1] = {'source': 99};
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        (m.json['textures']! as List)[1] = {'source': 'x'};
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // An image the decoder has no bytes for, and a KTX2 one named through basisu.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        (m.json['images']! as List).add({'uri': 'b.png'});
        (m.json['textures']! as List)[1] = {'source': 2};
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        (m.json['textures']! as List)[1] = {
          'source': 0,
          'extensions': {
            'KHR_texture_basisu': {'source': 1},
          },
        };
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        (m.json['textures']! as List)[1] = {
          'extensions': {
            'KHR_texture_basisu': {'source': 'x'},
          },
          'source': 1,
        };
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // An image that is not a PNG.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.rawTexture(const [1, 2, 3, 4]);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // UV sets.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.quad(
          m.material({
            ..._colour(a),
            'normalTexture': _slot(b, {'texCoord': 1}),
          }),
          sets: const [0, 1],
        );
        m.quad(m.material(_colour(b)), x: 2);
        m.quad(m.material(_colour(a)), x: 4);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.quad(
          m.material({
            'pbrMetallicRoughness': {
              'baseColorTexture': _slot(a, {'texCoord': 1}),
            },
          }),
          sets: const [1],
        );
        m.quad(
          m.material({
            'pbrMetallicRoughness': {
              'baseColorTexture': _slot(b, {'texCoord': 1}),
            },
          }),
          x: 2,
          sets: const [0, 1],
        );
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.quad(
          m.material({
            'pbrMetallicRoughness': {
              'baseColorTexture': _slot(a, {'texCoord': 1}),
            },
          }),
          sets: const [0],
        );
        m.quad(
          m.material({
            'pbrMetallicRoughness': {
              'baseColorTexture': _slot(b, {'texCoord': 1}),
            },
          }),
          x: 2,
          sets: const [1],
        );
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final c = m.solid(_green);
        m.quad(m.material(_colour(a)), sets: const []);
        m.quad(m.material(_colour(b)), x: 2);
        m.quad(m.material(_colour(c)), x: 4);
      }),
      _plain,
      _noBlind,
    ),
    // One UV accessor shared across two materials, then across two quads of one.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final first = m.quad(m.material(_colour(a)));
        m.quad(
          m.material(_colour(b)),
          x: 2,
          reuse: (first['attributes']! as Map)['TEXCOORD_0']! as int,
        );
      }),
      _separate,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final one = m.material(_colour(a));
        final first = m.quad(one);
        m.quad(
          one,
          x: 2,
          reuse: (first['attributes']! as Map)['TEXCOORD_0']! as int,
        );
        m.quad(m.material(_colour(b)), x: 4);
      }),
      _separate,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final one = m.material(_colour(a));
        final first = m.quad(one);
        m.quad(
          one,
          x: 2,
          reuse: (first['attributes']! as Map)['TEXCOORD_0']! as int,
        );
        m.quad(m.material(_colour(b)), x: 4);
      }),
      _plain,
      _noBlind,
    ),
    // A UV accessor that is not a VEC2.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final first = m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        final accessor = (first['attributes']! as Map)['TEXCOORD_0']! as int;
        ((m.json['accessors']! as List)[accessor]
                as Map<String, Object?>)['type'] =
            'VEC3';
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final first = m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        final accessor = (first['attributes']! as Map)['TEXCOORD_0']! as int;
        ((m.json['accessors']! as List)[accessor]
                as Map<String, Object?>)['componentType'] =
            5123;
      }),
      _plain,
      _noBlind,
    ),
    // A primitive with no attributes, one that is not a map, a mesh with no
    // primitives, and a primitive with no material.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final first = m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        first.remove('attributes');
      }),
      _separate,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        m.primitives.add('x');
        (m.json['meshes']! as List)
          ..add(<String, Object?>{})
          ..add({'primitives': 'x'});
      }),
      _separate,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        m.quad(null, x: 4);
        m.quad(7, x: 6);
      }),
      _plain,
      _noBlind,
    ),
    // A material no primitive draws with, then two primitives with one material.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        final c = m.solid(_green);
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
        m.material(_colour(c));
      }),
      _plain,
      _noBlind,
    ),
    // Several materials the packer places in rows, with sizes that do not agree.
    (
      _build((m) {
        const sizes = [(3, 7), (5, 2), (9, 9), (1, 1), (6, 4), (2, 8)];
        for (final (i, (w, h)) in sizes.indexed) {
          m.quad(
            m.material(_colour(m.texture(w, h, _noise(w, h, 70 + i)))),
            x: i * 2.0,
          );
        }
      }),
      _plain,
      _noBlind,
    ),
    // Already packed once: the extension lists and the samplers exist.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.json['extensionsUsed'] = ['KHR_texture_basisu', 'OTHER'];
        m.json['extensionsRequired'] = <Object?>[];
        m.json['samplers'] = [
          {'wrapS': 10497},
        ];
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.json['extensionsUsed'] = 'x';
        m.json['extensionsRequired'] = ['KHR_texture_basisu'];
        m.quad(m.material(_colour(a)));
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _plain,
      _noBlind,
    ),
    // The document's other parts ride through.
    (
      _build((m) {
        final a = m.solid(_red);
        final b = m.solid(_blue);
        m.json['extras'] = {'orblit': 'hello'};
        ((m.json['nodes']! as List).first as Map<String, Object?>)['extras'] = {
          'collider': 'box',
        };
        m.quad(
          m.material({
            ..._colour(a),
            'name': 'first',
            'extras': {'mine': 1},
            'doubleSided': true,
          }),
        );
        m.quad(m.material(_colour(b)), x: 2);
      }),
      _separate,
      _noBlind,
    ),
  ];
}

Future<void> main(List<String> args) async {
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'gltf_atlas',
    cases: [
      ...await _resampleCases(),
      ...await _renormaliseCases(),
      ...await _multiplyCases(),
      ...await _blitCases(),
      ...await _blankCases(),
      for (final spec in _atlasSpecs()) await _atlasCase(spec),
    ],
  );
}
