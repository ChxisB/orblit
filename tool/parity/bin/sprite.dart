import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:vector_math/vector_math_64.dart';

/// An int JSON can carry exactly. A reader hands back a double past 2^53, so
/// those travel as strings.
Object big(int value) => value > 9007199254740992 || value < -9007199254740992
    ? value.toString()
    : value;

String b64(List<int> bytes) => base64Encode(bytes);

Map<String, Object?> regionJson(Region r) => {
  'name': r.name,
  'x': big(r.x),
  'y': big(r.y),
  'width': big(r.width),
  'height': big(r.height),
  'rotated': r.rotated,
  'trimmed': r.trimmed,
  'offsetX': big(r.offsetX),
  'offsetY': big(r.offsetY),
  'sourceWidth': big(r.sourceWidth),
  'sourceHeight': big(r.sourceHeight),
};

Map<String, Object?>? atlasJson(Atlas? a) => a == null
    ? null
    : {
        'image': a.image,
        'width': big(a.width),
        'height': big(a.height),
        'regions': [for (final r in a.regions.values) regionJson(r)],
      };

Object? tree(Object? value) => switch (value) {
  final double d => number(d),
  final int i => big(i),
  final Map<Object?, Object?> m => {
    for (final e in m.entries) '${e.key}': tree(e.value),
  },
  final List<Object?> l => [for (final e in l) tree(e)],
  _ => value,
};

Map<String, Object?> rectJson(({int x, int y, int width, int height})? r) =>
    r == null
    ? {'none': true}
    : {
        'x': big(r.x),
        'y': big(r.y),
        'width': big(r.width),
        'height': big(r.height),
      };

// --- Atlas ------------------------------------------------------------------

Map<String, Object?> atlasReadCase(String text, {String? image}) => {
  'op': 'atlasRead',
  'text': text,
  'image': image,
  'expect': attempt(() => atlasJson(Atlas.read(text, image: image))),
};

Map<String, Object?> frameEntry(
  int x,
  int y,
  int w,
  int h, {
  Object? rotated,
  Object? trimmed,
}) => {
  'frame': {'x': x, 'y': y, 'w': w, 'h': h},
  'rotated': ?rotated,
  'trimmed': ?trimmed,
  'spriteSourceSize': {'x': 5, 'y': 6, 'w': w, 'h': h},
  'sourceSize': {'w': w + 10, 'h': h + 12},
};

List<Map<String, Object?>> atlasReadCases() {
  final raw = <String>[
    jsonEncode({
      'frames': {
        'run_0': frameEntry(0, 0, 16, 16),
        'run_10': frameEntry(16, 0, 16, 16, rotated: true, trimmed: true),
        'run_2': frameEntry(32, 0, 16, 16, rotated: 'yes', trimmed: 1),
      },
      'meta': {
        'image': 'sheet.png',
        'size': {'w': 64, 'h': 32},
      },
    }),
    jsonEncode({
      'frames': [
        {'filename': 'a', ...frameEntry(1, 2, 3, 4)},
        {
          'frame': {'x': 1},
        },
        5,
        {'filename': 3, 'frame': <String, Object?>{}},
        {'filename': 'a', ...frameEntry(9, 9, 9, 9)},
      ],
      'meta': {'image': 4, 'size': 'big'},
    }),
    '{"frames": {"a": {"frame": {"x": 1e999, "y": 0, "w": 1, "h": 1}}}}',
    '{"frames": {"a": {"frame": {"x": 1e19, "y": -1e19, "w": 2.5, "h": 3.5}}}}',
    '{"frames": {"a": {"frame": {"x": -2.5, "y": -0.5, "w": 0.5, "h": 1.5}}}}',
    '{"frames": {"a": {"frame": {"x": 9007199254740993, "y": -9007199254740993, "w": 1, "h": 1}}}}',
    '{"frames": {"a": {"frame": {"x": "5", "y": null, "w": true, "h": [1]}}}}',
    '{"frames": {"a": 5, "b": {"frame": 5}, "c": {"frame": {}}, "d": {"frame": {"w": 1}, "spriteSourceSize": 4, "sourceSize": []}}}',
    '{"frames": {"b": {"frame": {}}, "a": {"frame": {}}, "a": {"frame": {"x": 2}}}}',
    '{"frames": {"\\ud83d\\ude00 é": {"frame": {"x": 1}}, "": {"frame": {"x": 2}}}}',
    '{"frames": {}, "meta": {"image": "s.png", "size": {"w": 64.4, "h": 31.5}}}',
    '{"frames": 5}',
    '{"frames": null}',
    '{}',
    '[]',
    '"x"',
    'null',
    '',
    '{bad',
    '{"frames": {}} trailing',
    ' \n{"frames": {"a": {"frame": {"x": 1}}}}\t',
  ];
  return [
    for (final text in raw) atlasReadCase(text),
    atlasReadCase(raw.first, image: 'override.png'),
    atlasReadCase(raw[10], image: ''),
  ];
}

Atlas atlasOfNames(List<String> names) => Atlas(
  image: 'a.png',
  regions: {
    for (var i = 0; i < names.length; i++)
      names[i]: Region(name: names[i], x: i, y: 0, width: 1, height: 1),
  },
);

/// Names that compare as equal when read as numbers, so only the sort's
/// handling of ties decides their order. Dart's sort is stable up to 33 items
/// and not above that, so the cases stop at 33 and the C# port keeps a stable
/// sort. See decision 75.
List<String> tiedNames(int count) => [
  for (var i = 0; i < count; i++)
    i.isEven ? 'k${i ~/ 2}' : 'k${'0' * (1 + i % 3)}${i ~/ 2}',
];

List<Map<String, Object?>> sequenceCases() {
  final sets = <(List<String>, String)>[
    (['run_10', 'run_2', 'run_1', 'run_0', 'walk_1', 'run', 'runner_3'], 'run'),
    (['a', 'b', 'A', 'B', 'é', 'z', '10', '9', '1a', '1'], ''),
    (['x2', 'x02', 'x002', 'x1', 'x01', 'x10', 'x010'], 'x'),
    (['a99999999999999999999', 'a9', 'a1', 'a18446744073709551616'], 'a'),
    (['a1b2', 'a1b10', 'a01b2', 'a1b', 'a1', 'a'], 'a'),
    (['frame_001', 'frame_01', 'frame_1', 'frame_0001'], 'frame'),
    (['😀2', '😀10', '😀1'], '😀'),
    (<String>[], 'x'),
    (tiedNames(10), 'k'),
    (tiedNames(32), 'k'),
    (tiedNames(33), 'k'),
    ([for (var i = 120; i >= 0; i--) 'f$i'], 'f'),
  ];
  return [
    for (final (names, prefix) in sets)
      {
        'op': 'sequence',
        'names': names,
        'prefix': prefix,
        'expect': [
          for (final r in atlasOfNames(names).sequence(prefix)) r.name,
        ],
      },
  ];
}

List<Map<String, Object?>> gridCases() {
  const specs = <Map<String, int>>[
    {'w': 64, 'h': 32, 'cw': 16, 'ch': 16},
    {'w': 70, 'h': 40, 'cw': 16, 'ch': 16},
    {'w': 66, 'h': 36, 'cw': 16, 'ch': 16, 'spacing': 2, 'margin': 1},
    {'w': 22, 'h': 22, 'cw': 16, 'ch': 16, 'margin': 4},
    {'w': 35, 'h': 35, 'cw': 16, 'ch': 16, 'margin': 2},
    {'w': 64, 'h': 64, 'cw': 16, 'ch': 16, 'count': 5},
    {'w': 64, 'h': 64, 'cw': 16, 'ch': 16, 'count': 500},
    {'w': 64, 'h': 64, 'cw': 0, 'ch': 16},
    {'w': 64, 'h': 64, 'cw': 16, 'ch': -3},
    {'w': 10, 'h': 10, 'cw': 16, 'ch': 16},
    {'w': 10, 'h': 10, 'cw': 4, 'ch': 4, 'margin': 20},
    {'w': 10, 'h': 10, 'cw': 4, 'ch': 4, 'spacing': -4},
    {'w': 10, 'h': 10, 'cw': 4, 'ch': 4, 'spacing': -2},
    {'w': 10, 'h': 10, 'cw': 4, 'ch': 4, 'spacing': -5},
    {'w': 10, 'h': 10, 'cw': 4, 'ch': 4, 'margin': -2},
    {'w': -10, 'h': 10, 'cw': 4, 'ch': 4},
    {'w': 0, 'h': 0, 'cw': 4, 'ch': 4},
    {'w': 9007199254740993, 'h': 3, 'cw': 9007199254740993, 'ch': 3},
    {'w': 9223372036854775807, 'h': 3, 'cw': 9223372036854775807, 'ch': 3},
  ];
  return [
    for (final s in specs)
      {
        'op': 'grid',
        'image': 'g.png',
        'imageWidth': big(s['w']!),
        'imageHeight': big(s['h']!),
        'cellWidth': big(s['cw']!),
        'cellHeight': big(s['ch']!),
        'name': s['count'] == 5 ? 'tile' : 'frame',
        'count': s['count'] ?? 0,
        'spacing': s['spacing'] ?? 0,
        'margin': s['margin'] ?? 0,
        'expect': attempt(
          () => atlasJson(
            Atlas.grid(
              image: 'g.png',
              imageWidth: s['w']!,
              imageHeight: s['h']!,
              cellWidth: s['cw']!,
              cellHeight: s['ch']!,
              name: s['count'] == 5 ? 'tile' : 'frame',
              count: s['count'] ?? 0,
              spacing: s['spacing'] ?? 0,
              margin: s['margin'] ?? 0,
            ),
          ),
        ),
      },
  ];
}

List<Map<String, Object?>> regionCases() {
  const sizes = <(int, int)>[(64, 32), (0, 32), (64, -1), (1, 1), (3, 7)];
  final regions = <Region>[
    const Region(name: 'a', x: 8, y: 4, width: 16, height: 12),
    const Region(
      name: 'b',
      x: 8,
      y: 4,
      width: 16,
      height: 12,
      trimmed: true,
      sourceWidth: 20,
      sourceHeight: 30,
    ),
    const Region(name: 'c', x: 8, y: 4, width: 16, height: 12, trimmed: true),
    const Region(
      name: 'd',
      x: 8,
      y: 4,
      width: 16,
      height: 12,
      sourceWidth: 20,
      sourceHeight: 30,
    ),
    const Region(name: 'e', x: -3, y: 100, width: 0, height: 9007199254740993),
  ];
  return [
    for (final r in regions)
      for (final (w, h) in sizes)
        {
          'op': 'region',
          'region': regionJson(r),
          'imageWidth': w,
          'imageHeight': h,
          'expect': () {
            final uv = r.uv(w, h);
            return {
              'placedWidth': big(r.placedSize.$1),
              'placedHeight': big(r.placedSize.$2),
              'u0': number(uv.u0),
              'v0': number(uv.v0),
              'u1': number(uv.u1),
              'v1': number(uv.v1),
            };
          }(),
        },
  ];
}

// --- Tiles ------------------------------------------------------------------

List<Map<String, Object?>> tilesetCases() {
  const sets = <Tileset>[
    Tileset(
      name: 'a',
      image: 'a.png',
      firstId: 1,
      tileWidth: 16,
      tileHeight: 16,
      columns: 4,
      count: 12,
    ),
    Tileset(
      name: 'b',
      image: 'b.png',
      firstId: 5,
      tileWidth: 8,
      tileHeight: 12,
      columns: 3,
      spacing: 1,
      margin: 2,
    ),
    Tileset(
      name: 'c',
      image: 'c.png',
      firstId: 1,
      tileWidth: 8,
      tileHeight: 8,
      columns: 0,
      count: 4,
    ),
    Tileset(
      name: 'd',
      image: 'd.png',
      firstId: 1,
      tileWidth: 8,
      tileHeight: 8,
      columns: -2,
    ),
    Tileset(
      name: 'e',
      image: 'e.png',
      firstId: -9223372036854775807,
      tileWidth: 8,
      tileHeight: 8,
      columns: 3,
    ),
    Tileset(
      name: 'f',
      image: 'f.png',
      firstId: 9223372036854775805,
      tileWidth: 8,
      tileHeight: 8,
      columns: 3,
      count: 5,
    ),
    Tileset(
      name: 'g',
      image: 'g.png',
      firstId: 0,
      tileWidth: 9007199254740993,
      tileHeight: 8,
      columns: 7,
      spacing: 9223372036854775807,
      margin: 3,
    ),
    Tileset(
      name: 'h',
      image: 'h.png',
      firstId: -3,
      tileWidth: 8,
      tileHeight: 8,
      columns: 2,
      count: 6,
    ),
  ];
  const ids = <int>[
    -9223372036854775808,
    -9223372036854775807,
    -4,
    -3,
    -1,
    0,
    1,
    2,
    4,
    5,
    6,
    12,
    13,
    16,
    1000,
    9223372036854775805,
    9223372036854775806,
    9223372036854775807,
  ];
  return [
    for (final s in sets)
      {
        'op': 'tileset',
        'name': s.name,
        'firstId': big(s.firstId),
        'tileWidth': big(s.tileWidth),
        'tileHeight': big(s.tileHeight),
        'columns': big(s.columns),
        'count': big(s.count),
        'spacing': big(s.spacing),
        'margin': big(s.margin),
        'ids': [for (final id in ids) big(id)],
        'expect': [
          for (final id in ids)
            {'has': s.has(id), 'rect': rectJson(s.rectOf(id))},
        ],
      },
  ];
}

Map<String, Object?> layerJson(
  String name,
  int w,
  int h,
  List<Object?> data, {
  Map<String, Object?> extra = const {},
}) => {
  'type': 'tilelayer',
  'name': name,
  'width': w,
  'height': h,
  'data': data,
  ...extra,
};

Map<String, Object?> chunk(int x, int y, int w, int h, List<Object?> data) => {
  'x': x,
  'y': y,
  'width': w,
  'height': h,
  'data': data,
};

Map<String, Object?> mapJson(
  List<Object?> layers, {
  List<Object?>? tilesets,
  Map<String, Object?> extra = const {},
}) => {
  'width': 4,
  'height': 3,
  'tilewidth': 16,
  'tileheight': 16,
  'orientation': 'orthogonal',
  'layers': layers,
  'tilesets':
      tilesets ??
      [
        {
          'name': 'terrain',
          'image': 't.png',
          'firstgid': 1,
          'tilewidth': 16,
          'tileheight': 16,
          'columns': 4,
          'tilecount': 12,
        },
        {
          'name': 'props',
          'image': 'p.png',
          'firstgid': 13,
          'tilewidth': 16,
          'tileheight': 32,
          'columns': 2,
          'tilecount': 4,
          'spacing': 1,
          'margin': 1,
        },
        {
          'name': 'also',
          'image': 'q.png',
          'firstgid': 13,
          'tilewidth': 4,
          'tileheight': 4,
          'columns': 1,
        },
      ],
  ...extra,
};

const _probes = <List<int>>[
  [0, 0],
  [1, 0],
  [0, 1],
  [1, 1],
  [2, 2],
  [3, 0],
  [-1, 0],
  [0, -1],
  [-2, -2],
  [-3, 4],
  [5, 5],
  [7, 1],
  [100, 100],
  [-100, -100],
  [-16, -16],
  [-15, -15],
  [16, 16],
];

const _doubles = <List<double>>[
  [0, 0],
  [15.9, 16],
  [-0.1, -16],
  [-0.0, 0.5],
  [1e300, 0],
  [-1e300, 1e18],
  [double.nan, 0],
  [0, double.nan],
  [double.infinity, 0],
  [0, double.negativeInfinity],
  [9.3e18, 9.3e18],
];

const _ids = <int>[
  -5,
  0,
  1,
  2,
  12,
  13,
  14,
  100,
  4294967296,
  2147483648,
  9223372036854775807,
];

const _onlys = <List<String>?>[
  null,
  ['ground'],
  [],
  ['nope', 'sky'],
];

Map<String, Object?> tileMapCase(String text) {
  Object? body() {
    final map = TileMap.read(text);
    if (map == null) return null;
    final layerAt = <List<Object?>>[];
    final probes = <List<int>>[
      ..._probes,
      for (final l in map.layers) ...[
        [l.left, l.top],
        [l.left + l.width - 1, l.top + l.height - 1],
        [l.left + l.width, l.top + l.height],
        [l.left - 1, l.top - 1],
      ],
    ];
    for (final l in map.layers) {
      layerAt.add([
        for (final p in probes)
          attempt(() {
            final f = l.flipsAt(p[0], p[1]);
            return {
              'at': big(l.at(p[0], p[1])),
              'empty': l.isEmptyAt(p[0], p[1]),
              'h': f.horizontal,
              'v': f.vertical,
              'd': f.diagonal,
            };
          }),
      ]);
    }
    return {
      'width': big(map.width),
      'height': big(map.height),
      'tileWidth': big(map.tileWidth),
      'tileHeight': big(map.tileHeight),
      'properties': tree(map.properties),
      'probes': probes,
      'layers': [
        for (final l in map.layers)
          {
            'name': l.name,
            'width': big(l.width),
            'height': big(l.height),
            'left': big(l.left),
            'top': big(l.top),
            'visible': l.visible,
            'opacity': number(l.opacity),
            'tiles': [for (final t in l.tiles) big(t)],
          },
      ],
      'tilesets': [
        for (final s in map.tilesets)
          {
            'name': s.name,
            'image': s.image,
            'firstId': big(s.firstId),
            'tileWidth': big(s.tileWidth),
            'tileHeight': big(s.tileHeight),
            'columns': big(s.columns),
            'count': big(s.count),
            'spacing': big(s.spacing),
            'margin': big(s.margin),
          },
      ],
      'layerAt': layerAt,
      'layerIndex': [
        for (final name in [...map.layers.map((l) => l.name), 'missing'])
          map.layers.indexOf(map.layer(name) ?? map.layers.first),
      ],
      'layerMissing': map.layer('missing') == null,
      'setFor': [for (final id in _ids) map.setFor(id)?.name],
      'placeOf': [
        for (final id in _ids) attempt(() => rectJson(map.placeOf(id, 3, -2))),
      ],
      'cellAt': [
        for (final p in _doubles)
          attempt(() {
            final c = map.cellAt(p[0], p[1]);
            return [big(c.$1), big(c.$2)];
          }),
      ],
      'solid': [
        for (final only in _onlys)
          [
            for (final p in probes)
              attempt(() => map.isSolidAt(p[0], p[1], only: only?.toSet())),
          ],
      ],
    };
  }

  return {
    'op': 'tileMap',
    'text': text,
    'doubles': [
      for (final p in _doubles) [number(p[0]), number(p[1])],
    ],
    'ids': [for (final id in _ids) big(id)],
    'onlys': _onlys,
    'extraProbes': _probes,
    'expect': attempt(body),
  };
}

List<Map<String, Object?>> tileMapCases() {
  final plain = layerJson('ground', 4, 3, [
    1,
    2,
    0,
    3,
    4,
    5,
    6,
    0,
    0,
    0,
    13,
    14,
  ]);
  final raw = <String>[
    jsonEncode(mapJson([plain])),
    jsonEncode(
      mapJson(
        [
          plain,
          layerJson(
            'sky',
            2,
            2,
            [1, 0, 0, 2],
            extra: {'x': -1, 'y': 5, 'visible': false, 'opacity': 0.5},
          ),
          {'type': 'objectgroup', 'name': 'ignored'},
          5,
          layerJson(
            'later',
            1,
            1,
            [7],
            extra: {'opacity': 1, 'x': 2.5, 'y': 0.5},
          ),
          layerJson('ground', 1, 1, [9], extra: {'opacity': 'x', 'visible': 0}),
        ],
        extra: {
          'properties': {
            'gravity': 9.8,
            'big': 9007199254740993,
            'name': 'cave',
            'nested': {
              'list': [1, 2.5, null, true, 'x'],
            },
          },
        },
      ),
    ),
    jsonEncode(
      mapJson([
        layerJson('flags', 3, 1, [0x80000001, 0x40000002, 0x20000003]),
        layerJson('mixed', 4, 1, [0xE0000005, 0xFFFFFFFF, -1, 4294967297]),
        layerJson('odd', 5, 1, [1e3, 2.5, 'x', null, 9007199254740993]),
        layerJson('wide', 2, 1, [9223372036854775807, -9223372036854775808]),
      ]),
    ),
    '{"layers": [{"type": "tilelayer", "name": "o", "opacity": 1e999, '
        '"width": 1, "height": 1, "data": [1]}], "tilesets": []}',
    '{"layers": [{"type": "tilelayer", "name": "o", "width": 1, "height": 1, '
        '"data": [1e999]}]}',
    '{"layers": [{"type": "tilelayer", "name": "o", "width": 1e999, "height": 1, '
        '"data": [1]}]}',
    '{"layers": [{"type": "tilelayer", "name": "o", "width": 1, "height": 1, '
        '"data": [1e19, -1e19]}]}',
    jsonEncode(mapJson([layerJson('huge', 4294967296, 4294967296, [])])),
    jsonEncode(
      mapJson([
        layerJson('huge', 4294967296, 4294967297, [1, 2, 3]),
      ]),
    ),
    jsonEncode(
      mapJson([
        layerJson('short', 4, 3, [1, 2, 3]),
      ]),
    ),
    jsonEncode(
      mapJson([
        layerJson('long', 2, 1, [1, 2, 3, 4, 5]),
      ]),
    ),
    jsonEncode(mapJson([layerJson('neg', -1, 3, [])])),
    jsonEncode(mapJson([layerJson('zero', 0, 0, [])])),
    jsonEncode(
      mapJson([
        layerJson('nodata', 1, 1, [1])..['data'] = 'csv',
      ]),
    ),
    jsonEncode(
      mapJson([
        layerJson('b64', 1, 1, [1], extra: {'encoding': 'base64'}),
      ]),
    ),
    jsonEncode(
      mapJson([
        layerJson('csv', 1, 1, [1], extra: {'encoding': 'csv'}),
      ]),
    ),
    jsonEncode(
      mapJson([
        layerJson('z', 1, 1, [1], extra: {'compression': 'zlib'}),
      ]),
    ),
    jsonEncode(
      mapJson([
        layerJson('e', 1, 1, [1], extra: {'compression': ''}),
      ]),
    ),
    jsonEncode(mapJson([plain], extra: {'orientation': 'isometric'})),
    jsonEncode(mapJson([plain], extra: {'orientation': 5})),
    jsonEncode(
      mapJson(
        [plain],
        tilesets: [
          {'source': 'external.tsx', 'firstgid': 1},
        ],
      ),
    ),
    jsonEncode(
      mapJson(
        [plain],
        tilesets: [
          5,
          {
            'firstgid': 2.5,
            'tilewidth': 'x',
            'columns': 1e3,
            'tilecount': null,
            'name': 4,
            'image': null,
          },
        ],
      ),
    ),
    jsonEncode(mapJson([plain], extra: {'tilesets': 7})),
    jsonEncode(
      mapJson(
        [plain],
        extra: {
          'width': 3.7,
          'height': 'x',
          'tilewidth': 0,
          'tileheight': -16,
          'properties': [1],
        },
      ),
    ),
    jsonEncode({'tilesets': <Object?>[]}),
    jsonEncode({'layers': 5}),
    jsonEncode({'layers': <Object?>[]}),
    '[]',
    '5',
    '',
    '{broken',
    // Infinite maps.
    jsonEncode(
      mapJson([
        layerJson(
          'inf',
          0,
          0,
          [],
          extra: {
            'chunks': [
              chunk(0, 0, 2, 2, [1, 2, 3, 4]),
            ],
          },
        )..remove('data'),
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'visible': true,
          'chunks': [
            chunk(-2, -2, 2, 2, [1, 2, 3, 4]),
            chunk(2, 0, 2, 2, [5, 0, 0, 6]),
            chunk(-2, 2, 2, 2, [0x80000007, 0, 0, 0xC0000008]),
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [
            chunk(0, 0, 2, 2, [1, 1, 1, 1]),
            chunk(1, 1, 2, 2, [2, 2, 2, 2]),
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [
            chunk(0, 0, 2, 2, [-1, 4294967297, 4294967296, 8589934593]),
            chunk(0, 2, 2, 1, [1e3, 2.5]),
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {'type': 'tilelayer', 'name': 'inf', 'chunks': <Object?>[]},
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [
            chunk(0, 0, 2, 2, [1, 2, 3]),
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [
            chunk(0, 0, 2, 2, [1, 2, 3, 4]),
            5,
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [
            chunk(0, 0, 2, 2, [1, 2, 3, 4]),
            chunk(3000000, 0, 2, 2, [1, 2, 3, 4]),
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [
            chunk(0, 0, 2, 2, [1, 2, 3, 4]),
            chunk(100, 0, 2, 2, [1, 2, 3, 4]),
          ],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [chunk(0, 0, -2, 2, [])],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': [chunk(0, 0, 0, 0, []), chunk(5, 5, 0, 0, [])],
        },
      ]),
    ),
    jsonEncode(
      mapJson([
        {
          'type': 'tilelayer',
          'name': 'inf',
          'chunks': 'none',
          'width': 1,
          'height': 1,
          'data': [4],
        },
      ]),
    ),
  ];
  return [for (final text in raw) tileMapCase(text)];
}

// --- Animation ----------------------------------------------------------

const _times = <double>[
  0,
  0.01,
  0.0833,
  0.1,
  0.25,
  0.3,
  0.5,
  0.75,
  1,
  1.4,
  2,
  2.5,
  3,
  7.77,
  -0.5,
  -100,
  1e6,
  1e300,
  double.nan,
  double.infinity,
  double.negativeInfinity,
];

Map<String, Object?> animationJson(SpriteAnimation a, List<double> times) => {
  'length': a.length,
  'isEmpty': a.isEmpty,
  'duration': number(a.duration),
  'cycle': number(a.cycle),
  'frames': [
    for (final f in a.frames)
      {'name': f.region.name, 'seconds': number(f.seconds)},
  ],
  'samples': [
    for (final t in times)
      {'index': a.indexAt(t), 'frame': a.at(t)?.name, 'done': a.doneBy(t)},
  ],
};

Region named(String name) =>
    Region(name: name, x: 0, y: 0, width: 1, height: 1);

List<Map<String, Object?>> animationCases() {
  const timings = <List<double>>[
    [0.1, 0.1, 0.1, 0.1],
    [0.1, 0.3, 0.2],
    [0.5],
    [],
    [0, 0, 0],
    [0.1, 0, 0.1],
    [-1, 0.5],
    [-1, -1],
    [double.nan, 0.1],
    [double.infinity, 1],
    [1e-9, 1e-9, 1e-9],
    [1 / 3, 1 / 3, 1 / 3],
  ];
  final cases = <Map<String, Object?>>[];
  for (final seconds in timings) {
    for (final (loop, pingPong) in const [
      (true, false),
      (false, false),
      (true, true),
      (false, true),
    ]) {
      final anim = SpriteAnimation(
        [
          for (var i = 0; i < seconds.length; i++)
            Frame(named('f$i'), seconds[i]),
        ],
        loop: loop,
        pingPong: pingPong,
      );
      cases.add({
        'op': 'animation',
        'seconds': [for (final s in seconds) number(s)],
        'loop': loop,
        'pingPong': pingPong,
        'times': [for (final t in _times) number(t)],
        'expect': animationJson(anim, _times),
      });
    }
  }
  return cases;
}

List<Map<String, Object?>> animationFromCases() {
  const fpses = <double>[12, 1, 60, 0, -5, double.nan, double.infinity, 0.001];
  const sets = <List<String>>[
    ['run_1', 'run_10', 'run_2', 'idle_0', 'run_0'],
    [],
    ['x'],
  ];
  return [
    for (final names in sets)
      for (final fps in fpses)
        for (final (loop, pingPong) in const [(true, false), (false, true)])
          {
            'op': 'animationFrom',
            'names': names,
            'prefix': names.length == 1 ? 'y' : 'run',
            'fps': number(fps),
            'loop': loop,
            'pingPong': pingPong,
            'times': [for (final t in _times) number(t)],
            'expect': animationJson(
              SpriteAnimation.from(
                atlasOfNames(names),
                names.length == 1 ? 'y' : 'run',
                fps: fps,
                loop: loop,
                pingPong: pingPong,
              ),
              _times,
            ),
          },
  ];
}

Map<String, Object?> flipbookState(Flipbook book) => {
  'playing': book.playing,
  'since': number(book.since),
  'frame': book.frame?.name,
  'done': book.done,
};

List<Map<String, Object?>> flipbookCases() {
  const clips = <Map<String, Object?>>[
    {
      'name': 'idle',
      'seconds': [0.2, 0.2],
      'loop': true,
      'pingPong': false,
    },
    {
      'name': 'jump',
      'seconds': [0.1, 0.1, 0.1],
      'loop': false,
      'pingPong': false,
    },
    {'name': 'empty', 'seconds': <double>[], 'loop': false, 'pingPong': false},
    {
      'name': 'sway',
      'seconds': [0.25, 0.25],
      'loop': true,
      'pingPong': true,
    },
  ];
  const scripts = <(String?, List<Map<String, Object?>>)>[
    (
      null,
      [
        {'do': 'advance', 'seconds': 0.15},
        {'do': 'advance', 'seconds': 0.15},
        {'do': 'play', 'name': 'jump'},
        {'do': 'advance', 'seconds': 0.25},
        {'do': 'play', 'name': 'jump'},
        {'do': 'advance', 'seconds': 0.1},
        {'do': 'play', 'name': 'jump', 'restart': true},
        {'do': 'play', 'name': 'nope'},
        {'do': 'advance', 'seconds': 5},
        {'do': 'play', 'name': 'empty'},
        {'do': 'play', 'name': 'sway'},
        {'do': 'advance', 'seconds': 0.6},
        {'do': 'advance', 'seconds': -1},
        {'do': 'advance', 'seconds': 1e300},
        {'do': 'advance', 'seconds': double.nan},
      ],
    ),
    (
      'jump',
      [
        {'do': 'advance', 'seconds': 0.1},
        {'do': 'play', 'name': 'idle'},
        {'do': 'play', 'name': 'idle', 'restart': true},
      ],
    ),
    (
      'missing',
      [
        {'do': 'advance', 'seconds': 1},
        {'do': 'play', 'name': 'idle'},
      ],
    ),
  ];
  return [
    for (final (playing, steps) in scripts)
      {
        'op': 'flipbook',
        'clips': [
          for (final c in clips)
            {
              ...c,
              'seconds': [
                for (final s in c['seconds']! as List<double>) number(s),
              ],
            },
        ],
        'playing': playing,
        'steps': [
          for (final s in steps)
            s['seconds'] is num
                ? {...s, 'seconds': number((s['seconds']! as num).toDouble())}
                : s,
        ],
        'expect': () {
          final book = Flipbook({
            for (final c in clips)
              c['name']! as String: SpriteAnimation(
                [
                  for (
                    var i = 0;
                    i < (c['seconds']! as List<double>).length;
                    i++
                  )
                    Frame(
                      named('${c['name']}_$i'),
                      (c['seconds']! as List<double>)[i],
                    ),
                ],
                loop: c['loop']! as bool,
                pingPong: c['pingPong']! as bool,
              ),
          }, playing: playing);
          final states = [flipbookState(book)];
          for (final s in steps) {
            if (s['do'] == 'advance') {
              book.advance((s['seconds']! as num).toDouble());
            } else {
              book.play(s['name']! as String, restart: s['restart'] == true);
            }
            states.add(flipbookState(book));
          }
          return states;
        }(),
      },
  ];
}

// --- Parallax and the view ------------------------------------------------

List<Map<String, Object?>> parallaxCases() {
  const layerSets = <List<Layer>>[
    [
      Layer(image: 'far.png', depth: 0.2, size: 320, drift: 4),
      Layer(image: 'mid.png', depth: 0.6, size: 256, offset: 10),
      Layer(image: 'near.png', depth: 1),
    ],
    [Layer(image: 'x', depth: -1, size: 100, offset: -50, drift: -3)],
    [Layer(image: 'x', depth: 0.5, size: -10)],
    [Layer(image: 'x', depth: 0.5, size: 1e-9, drift: 1e9)],
    [Layer(image: 'x', depth: double.nan, size: 10)],
    [Layer(image: 'x', depth: 1, size: double.infinity)],
    [Layer(image: 'x', depth: 1, size: 10, offset: double.infinity)],
    [Layer(image: 'x', depth: 1e308, size: 7)],
    <Layer>[],
  ];
  const points = <List<double>>[
    [0, 0],
    [10, 0],
    [-10, 3.5],
    [123456.789, 1e6],
    [-1e9, -1e9],
    [1e300, 0],
    [double.nan, 0],
    [0, double.infinity],
    [320, 0],
    [-320, 0],
  ];
  const across = <double>[
    0,
    1,
    16,
    100.5,
    320,
    -3,
    1e18,
    double.nan,
    double.infinity,
  ];
  return [
    for (final layers in layerSets)
      {
        'op': 'parallax',
        'layers': [
          for (final l in layers)
            {
              'depth': number(l.depth),
              'size': number(l.size),
              'offset': number(l.offset),
              'drift': number(l.drift),
            },
        ],
        'points': [
          for (final p in points) [number(p[0]), number(p[1])],
        ],
        'across': [for (final a in across) number(a)],
        'expect': () {
          final p = Parallax(layers);
          return {
            'length': p.length,
            'at': [
              for (final pt in points)
                [for (final v in p.at(pt[0], pt[1])) number(v)],
            ],
            'placeOf': [
              for (final pt in points)
                [
                  for (var i = -1; i <= layers.length; i++)
                    number(p.placeOf(i, pt[0], pt[1])),
                ],
            ],
            'copies': [
              for (final l in layers)
                [
                  for (final a in across)
                    attempt(() => Parallax.copiesFor(l, a)),
                ],
            ],
          };
        }(),
      },
  ];
}

List<Map<String, Object?>> viewCases() {
  const bounds = <List<double>?>[
    null,
    [0, 0, 100, 50],
    [0, 0, 10, 5],
    [-20, -10, 20, 10],
    [0, 0, 16, 100],
    [5, 5, double.nan, 50],
    [0, 0, 1e300, 1e300],
  ];
  const sizes = <List<double>>[
    [16, 9],
    [0, 0],
    [40, 4],
    [double.nan, 9],
  ];
  const steps = <Map<String, Object?>>[
    {'do': 'follow', 'x': 30.0, 'y': 20.0, 'seconds': 0.016, 'ease': 6.0},
    {'do': 'follow', 'x': 30.0, 'y': 20.0, 'seconds': 1.0},
    {'do': 'follow', 'x': -500.0, 'y': 700.0, 'seconds': 0.5, 'ease': 0.0},
    {'do': 'follow', 'x': 1.0, 'y': 2.0, 'seconds': 0.2, 'ease': -3.0},
    {'do': 'follow', 'x': 8.0, 'y': 4.0, 'seconds': 0.0},
    {'do': 'follow', 'x': 8.0, 'y': 4.0, 'seconds': -1.0},
    {'do': 'follow', 'x': 8.0, 'y': 4.0, 'seconds': 100.0, 'ease': 60.0},
    {'do': 'follow', 'x': 8.0, 'y': 4.0, 'seconds': 1e300},
    {'do': 'follow', 'x': 8.0, 'y': 4.0, 'seconds': double.nan},
    {'do': 'follow', 'x': 8.0, 'y': 4.0, 'seconds': double.infinity},
    {'do': 'follow', 'x': double.nan, 'y': 4.0, 'seconds': 0.1},
    {'do': 'jump', 'x': 50.0, 'y': 25.0},
    {'do': 'jump', 'x': double.infinity, 'y': -1e300},
    {'do': 'jump', 'x': 0.0, 'y': 0.0},
  ];
  return [
    for (final b in bounds)
      for (final s in sizes)
        {
          'op': 'view',
          'width': number(s[0]),
          'height': number(s[1]),
          'bounds': b == null ? null : [for (final v in b) number(v)],
          'steps': [
            for (final step in steps)
              {
                for (final e in step.entries)
                  e.key: e.value is double
                      ? number(e.value! as double)
                      : e.value,
              },
          ],
          'expect': () {
            final view = View2(
              width: s[0],
              height: s[1],
              bounds: b == null
                  ? null
                  : (
                      minimum: Vector2(b[0], b[1]),
                      maximum: Vector2(b[2], b[3]),
                    ),
            );
            Object? place() => [number(view.at.x), number(view.at.y)];
            return [
              place(),
              for (final step in steps)
                attempt(() {
                  final target = Vector2(
                    step['x']! as double,
                    step['y']! as double,
                  );
                  if (step['do'] == 'jump') {
                    view.jumpTo(target);
                  } else {
                    view.follow(
                      target,
                      step['seconds']! as double,
                      ease: (step['ease'] as double?) ?? 6,
                    );
                  }
                  return place();
                }),
            ];
          }(),
        },
  ];
}

// --- The packer -----------------------------------------------------------

/// A sprite with an opaque core and a transparent margin of [inset] pixels
/// (left, top, right, bottom). The margin has [edgeAlpha], so a trim
/// threshold can cut some of it and keep the rest.
AtlasSprite sprite(
  String name,
  int w,
  int h, {
  int seed = 1,
  List<int> inset = const [0, 0, 0, 0],
  int edgeAlpha = 0,
}) {
  final pixels = Uint8List(w * h * 4);
  for (var y = 0; y < h; y++) {
    for (var x = 0; x < w; x++) {
      final i = (y * w + x) * 4;
      final inside =
          x >= inset[0] &&
          x < w - inset[2] &&
          y >= inset[1] &&
          y < h - inset[3];
      pixels[i] = (x * 37 + y * 91 + seed * 53) % 256;
      pixels[i + 1] = (x * 11 + y * 7 + seed * 29) % 256;
      pixels[i + 2] = (x * 5 + y * 3 + seed * 101) % 256;
      pixels[i + 3] = inside ? 255 : edgeAlpha;
    }
  }
  return AtlasSprite(name: name, width: w, height: h, pixels: pixels);
}

List<AtlasSprite> mixed(int count, {String prefix = 's', int seed = 1}) => [
  for (var i = 0; i < count; i++)
    sprite(
      '$prefix$i',
      5 + (i * 7) % 13,
      4 + (i * 5) % 11,
      seed: seed + i,
      inset: [i % 3, i % 2, (i + 1) % 3, (i + 2) % 2],
    ),
];

Map<String, Object?> optionsJson(Map<String, Object?> o) => {
  for (final e in o.entries)
    e.key: e.value is MaxRectsHeuristic
        ? (e.value! as MaxRectsHeuristic).name
        : e.value,
};

AtlasPackOptions optionsOf(Map<String, Object?> o) => AtlasPackOptions(
  maxPageSize: o['maxPageSize'] as int? ?? 2048,
  minPageSize: o['minPageSize'] as int? ?? 64,
  padding: o['padding'] as int? ?? 2,
  border: o['border'] as int? ?? 0,
  allowRotation: o['allowRotation'] as bool? ?? false,
  trim: o['trim'] as bool? ?? true,
  trimAlphaThreshold: o['trimAlphaThreshold'] as int? ?? 0,
  extrude: o['extrude'] as int? ?? 1,
  powerOfTwo: o['powerOfTwo'] as bool? ?? true,
  square: o['square'] as bool? ?? false,
  mergeDuplicates: o['mergeDuplicates'] as bool? ?? true,
  heuristic: o['heuristic'] as MaxRectsHeuristic?,
);

Map<String, Object?> packCase(
  List<AtlasSprite> sprites,
  Map<String, Object?> options,
) {
  final result = packAtlas(sprites, optionsOf(options));
  return {
    'op': 'pack',
    'sprites': [
      for (final s in sprites)
        {
          'name': s.name,
          'width': s.width,
          'height': s.height,
          'pixels': b64(s.pixels),
        },
    ],
    'options': optionsJson(options),
    'expect': {
      'heuristic': result.heuristic.name,
      'problems': [
        for (final p in result.problems) {'name': p.name, 'reason': p.reason},
      ],
      'imageNames': [for (final a in result.toAtlasSet().pages) a.image],
      'pages': [
        for (var i = 0; i < result.pages.length; i++)
          {
            'width': result.pages[i].width,
            'height': result.pages[i].height,
            'fillRatio': number(result.pages[i].fillRatio),
            'pixels': b64(result.pages[i].pixels),
            'regions': [
              for (final r in result.pages[i].regions.values) regionJson(r),
            ],
            'atlasText': writeAtlas(result.pages[i].toAtlas('p$i.png')),
            'extracted': [
              for (final r in result.pages[i].regions.values)
                b64(
                  extractRegionPixels(
                    pageWidth: result.pages[i].width,
                    pageHeight: result.pages[i].height,
                    pagePixels: result.pages[i].pixels,
                    region: r,
                  ),
                ),
            ],
          },
      ],
    },
  };
}

List<Map<String, Object?>> packCases() {
  const small = {'minPageSize': 16};
  return [
    packCase(mixed(6), const {}),
    for (final h in MaxRectsHeuristic.values)
      packCase(mixed(14, seed: 3), {...small, 'heuristic': h}),
    packCase(mixed(14, seed: 5), small),
    packCase(
      [
        sprite('tall', 4, 20, seed: 1),
        sprite('wide', 20, 4, seed: 2),
        sprite('tall2', 3, 18, seed: 3),
        sprite('block', 10, 10, seed: 4),
      ],
      {...small, 'maxPageSize': 24, 'allowRotation': true},
    ),
    packCase(
      [sprite('tall', 4, 20, seed: 1), sprite('wide', 20, 4, seed: 2)],
      {...small, 'maxPageSize': 24, 'allowRotation': false},
    ),
    packCase(mixed(8, seed: 9), {...small, 'padding': 0, 'extrude': 1}),
    packCase(mixed(8, seed: 9), {...small, 'padding': 2, 'extrude': 5}),
    packCase(mixed(8, seed: 9), {...small, 'padding': 3, 'extrude': 0}),
    packCase(mixed(8, seed: 9), {...small, 'padding': 1, 'extrude': 1}),
    packCase(mixed(8, seed: 9), {...small, 'border': 3}),
    packCase(mixed(8, seed: 9), {
      ...small,
      'border': 3,
      'padding': 0,
      'extrude': 0,
    }),
    packCase(mixed(8, seed: 2), {...small, 'trim': false}),
    for (final threshold in const [0, 5, 10, 200, 255])
      packCase(
        [
          sprite('a', 12, 12, seed: 1, inset: [2, 2, 2, 2], edgeAlpha: 5),
          sprite('b', 12, 12, seed: 2, inset: [3, 1, 2, 4], edgeAlpha: 20),
          sprite('c', 9, 9, seed: 3, inset: [1, 1, 1, 1], edgeAlpha: 200),
        ],
        {...small, 'trimAlphaThreshold': threshold},
      ),
    packCase([
      sprite('one', 8, 8, seed: 7, inset: [1, 1, 1, 1]),
      sprite('two', 8, 8, seed: 7, inset: [1, 1, 1, 1]),
      sprite('three', 9, 8, seed: 7),
      sprite('four', 8, 8, seed: 7),
      sprite('five', 8, 8, seed: 7, inset: [1, 1, 1, 1]),
    ], small),
    packCase(
      [
        sprite('one', 8, 8, seed: 7, inset: [1, 1, 1, 1]),
        sprite('two', 8, 8, seed: 7, inset: [1, 1, 1, 1]),
        sprite('three', 8, 8, seed: 7, inset: [1, 1, 1, 1]),
      ],
      {...small, 'mergeDuplicates': false},
    ),
    packCase(mixed(9, seed: 4), {
      'minPageSize': 10,
      'square': true,
      'powerOfTwo': false,
    }),
    packCase(mixed(9, seed: 4), {
      'minPageSize': 10,
      'square': false,
      'powerOfTwo': false,
    }),
    packCase(mixed(9, seed: 4), {...small, 'square': true}),
    packCase(
      [for (var i = 0; i < 20; i++) sprite('m$i', 12, 12, seed: i)],
      {...small, 'maxPageSize': 32, 'padding': 1},
    ),
    packCase(
      [
        sprite('fits', 6, 6),
        sprite('huge', 40, 40, seed: 2),
        sprite('empty', 0, 5),
        sprite('empty2', 5, 0),
        AtlasSprite(name: 'clear', width: 4, height: 4, pixels: Uint8List(64)),
        sprite('faint', 4, 4, seed: 3, inset: [4, 4, 4, 4], edgeAlpha: 9),
      ],
      {...small, 'maxPageSize': 32},
    ),
    packCase(
      [sprite('a', 6, 6), sprite('b', 6, 6, seed: 2)],
      {...small, 'maxPageSize': 8, 'border': 2},
    ),
    packCase(
      [sprite('a', 6, 6), sprite('b', 6, 6, seed: 2)],
      {...small, 'maxPageSize': 10, 'border': 2, 'padding': 0},
    ),
    packCase(<AtlasSprite>[], small),
    packCase(mixed(40, seed: 11), small),
    packCase(mixed(70, seed: 21), {...small, 'maxPageSize': 64}),
    packCase([
      for (var i = 0; i < 40; i++) sprite('same$i', 6, 6, seed: i),
    ], small),
    packCase([
      sprite('é', 5, 5, seed: 1),
      sprite('B', 5, 5, seed: 2),
      sprite('a"\\b', 5, 5, seed: 3),
      sprite('a\nb', 5, 5, seed: 4),
      sprite('😀', 5, 5, seed: 5),
      sprite('\u0001', 5, 5, seed: 6),
      sprite('10', 5, 5, seed: 7),
      sprite('9', 5, 5, seed: 8),
    ], small),
  ];
}

List<Map<String, Object?>> writeAtlasCases() {
  const sets = <List<String>>[
    ['b', 'a', 'B', 'é', '10', '9', ''],
    ['a"b', 'c\\d', 'e\nf', '\u0001', '\u007f', '😀', 'x/y'],
    <String>[],
  ];
  return [
    for (final names in sets)
      {
        'op': 'writeAtlas',
        'names': names,
        'image': 'a "sheet".png',
        'width': 64,
        'height': 32,
        'expect': writeAtlas(
          Atlas(
            image: 'a "sheet".png',
            regions: {
              for (var i = 0; i < names.length; i++)
                names[i]: Region(
                  name: names[i],
                  x: i,
                  y: i * 2,
                  width: 3,
                  height: 4,
                  rotated: i.isEven,
                  trimmed: i % 3 == 0,
                  offsetX: i,
                  offsetY: -i,
                  sourceWidth: 7,
                  sourceHeight: 9,
                ),
            },
            width: 64,
            height: 32,
          ),
        ),
      },
  ];
}

// --- PNG ------------------------------------------------------------------

int _crc(List<int> bytes) {
  var crc = 0xFFFFFFFF;
  for (final b in bytes) {
    crc ^= b;
    for (var k = 0; k < 8; k++) {
      crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
    }
  }
  return crc ^ 0xFFFFFFFF;
}

Uint8List _chunk(String type, List<int> data, {int? crc}) {
  final body = [...type.codeUnits, ...data];
  final out = BytesBuilder()
    ..add(_u32(data.length))
    ..add(body)
    ..add(_u32(crc ?? _crc(body)));
  return out.toBytes();
}

Uint8List _u32(int v) => Uint8List.fromList([
  (v >> 24) & 255,
  (v >> 16) & 255,
  (v >> 8) & 255,
  v & 255,
]);

int _paeth(int a, int b, int c) {
  final p = a + b - c;
  final pa = (p - a).abs();
  final pb = (p - b).abs();
  final pc = (p - c).abs();
  if (pa <= pb && pa <= pc) return a;
  return pb <= pc ? b : c;
}

/// A PNG built by hand, so each colour type, bit depth and filter the reader
/// accepts can be sent through Dart's own reader and the port's.
Uint8List buildPng({
  required int width,
  required int height,
  required int colorType,
  required int bitDepth,
  required int channels,
  required Uint8List rows,
  List<int>? palette,
  List<int>? trns,
  int Function(int row)? filterOf,
  int interlace = 0,
  int idatParts = 1,
}) {
  final bits = channels * bitDepth;
  final stride = (width * bits + 7) ~/ 8;
  final bpp = (bits + 7) ~/ 8;
  final raw = BytesBuilder();
  for (var y = 0; y < height; y++) {
    final f = filterOf?.call(y) ?? 0;
    raw.addByte(f);
    for (var x = 0; x < stride; x++) {
      final cur = rows[y * stride + x];
      final a = x >= bpp ? rows[y * stride + x - bpp] : 0;
      final b = y > 0 ? rows[(y - 1) * stride + x] : 0;
      final c = x >= bpp && y > 0 ? rows[(y - 1) * stride + x - bpp] : 0;
      final predictor = switch (f) {
        0 => 0,
        1 => a,
        2 => b,
        3 => (a + b) >> 1,
        4 => _paeth(a, b, c),
        _ => 0,
      };
      raw.addByte((cur - predictor) & 255);
    }
  }
  final compressed = ZLibEncoder().convert(raw.toBytes());
  final out = BytesBuilder()..add([137, 80, 78, 71, 13, 10, 26, 10]);
  out.add(
    _chunk('IHDR', [
      ..._u32(width),
      ..._u32(height),
      bitDepth,
      colorType,
      0,
      0,
      interlace,
    ]),
  );
  if (palette != null) out.add(_chunk('PLTE', palette));
  if (trns != null) out.add(_chunk('tRNS', trns));
  final piece = (compressed.length / idatParts).ceil();
  for (var i = 0; i < idatParts; i++) {
    final end = (i + 1) * piece > compressed.length
        ? compressed.length
        : (i + 1) * piece;
    out.add(_chunk('IDAT', compressed.sublist(i * piece, end)));
  }
  out.add(_chunk('IEND', const []));
  return out.toBytes();
}

Uint8List _rowsOf(int length, int seed) => Uint8List.fromList([
  for (var i = 0; i < length; i++)
    (i * 73 + 41 + seed * 17 + (i >> 2) * 5) & 255,
]);

Map<String, Object?> pngDecodeCase(String label, Uint8List png) => {
  'op': 'pngDecode',
  'label': label,
  'png': b64(png),
  'expect': attempt(() {
    final d = decodePng(png);
    return {'width': d.width, 'height': d.height, 'pixels': b64(d.pixels)};
  }),
};

List<Map<String, Object?>> pngCases() {
  final cases = <Map<String, Object?>>[];
  // Valid colour type and depth pairs, with a width that does not fill a byte.
  const kinds = <(int, int, int)>[
    (0, 1, 1),
    (0, 2, 1),
    (0, 4, 1),
    (0, 8, 1),
    (0, 16, 1),
    (2, 8, 3),
    (2, 16, 3),
    (3, 1, 1),
    (3, 2, 1),
    (3, 4, 1),
    (3, 8, 1),
    (4, 8, 2),
    (4, 16, 2),
    (6, 8, 4),
    (6, 16, 4),
    // Not allowed by the PNG rules, accepted by the reader.
    (2, 4, 3),
    (4, 4, 2),
    (6, 2, 4),
  ];
  final palette = [
    for (var i = 0; i < 16; i++) ...[i * 16, 255 - i * 9, (i * 37) & 255],
  ];
  for (final (colorType, depth, channels) in kinds) {
    for (final width in const [1, 5, 7]) {
      const height = 6;
      final stride = (width * channels * depth + 7) ~/ 8;
      final rows = _rowsOf(stride * height, colorType * 16 + depth + width);
      final keys = <List<int>?>[
        null,
        if (colorType == 0) [0, 1],
        if (colorType == 2) [0, 1, 0, 2, 0, 3],
        if (colorType == 3) [200, 9, 0],
      ];
      for (final trns in keys) {
        for (final filters in <int Function(int)>[
          (y) => 0,
          (y) => y % 5,
          (y) => 4 - y % 5,
          (y) => 4,
        ]) {
          cases.add(
            pngDecodeCase(
              'type $colorType depth $depth width $width',
              buildPng(
                width: width,
                height: height,
                colorType: colorType,
                bitDepth: depth,
                channels: channels,
                rows: rows,
                palette: colorType == 3 ? palette : null,
                trns: trns,
                filterOf: filters,
              ),
            ),
          );
        }
      }
    }
  }

  // A palette with fewer entries than the indices use.
  cases.add(
    pngDecodeCase(
      'short palette',
      buildPng(
        width: 6,
        height: 2,
        colorType: 3,
        bitDepth: 8,
        channels: 1,
        rows: Uint8List.fromList([0, 1, 2, 3, 4, 5, 5, 4, 3, 2, 1, 0]),
        palette: [1, 2, 3, 4, 5, 6, 7, 8, 9],
        trns: [10, 20],
      ),
    ),
  );
  // Paeth where the upper and the upper-left neighbour tie and differ.
  cases.add(
    pngDecodeCase(
      'paeth tie',
      buildPng(
        width: 2,
        height: 2,
        colorType: 0,
        bitDepth: 8,
        channels: 1,
        rows: Uint8List.fromList([10, 8, 11, 13]),
        filterOf: (y) => y == 0 ? 0 : 4,
      ),
    ),
  );
  cases.add(
    pngDecodeCase(
      'split idat',
      buildPng(
        width: 9,
        height: 9,
        colorType: 6,
        bitDepth: 8,
        channels: 4,
        rows: _rowsOf(9 * 4 * 9, 3),
        filterOf: (y) => y % 5,
        idatParts: 4,
      ),
    ),
  );
  cases.add(
    pngDecodeCase(
      'one by one',
      buildPng(
        width: 1,
        height: 1,
        colorType: 6,
        bitDepth: 8,
        channels: 4,
        rows: Uint8List.fromList([1, 2, 3, 4]),
      ),
    ),
  );
  cases.add(
    pngDecodeCase(
      'no width',
      buildPng(
        width: 0,
        height: 3,
        colorType: 6,
        bitDepth: 8,
        channels: 4,
        rows: Uint8List(0),
      ),
    ),
  );
  cases.add(
    pngDecodeCase(
      'no height',
      buildPng(
        width: 3,
        height: 0,
        colorType: 6,
        bitDepth: 8,
        channels: 4,
        rows: Uint8List(0),
      ),
    ),
  );

  final good = buildPng(
    width: 4,
    height: 4,
    colorType: 6,
    bitDepth: 8,
    channels: 4,
    rows: _rowsOf(64, 9),
    filterOf: (y) => y,
  );
  Uint8List patched(void Function(Uint8List bytes) edit) {
    final copy = Uint8List.fromList(good);
    edit(copy);
    return copy;
  }

  // Offsets: signature 8, IHDR chunk 8..33 (data from 16), so width at 16,
  // height at 20, bit depth 24, colour type 25, interlace 28, CRC 29..32.
  cases.addAll([
    pngDecodeCase('corrupt crc', patched((b) => b[29] ^= 255)),
    pngDecodeCase('corrupt idat crc', patched((b) => b[b.length - 20] ^= 1)),
    pngDecodeCase('bad signature', patched((b) => b[1] = 0)),
    pngDecodeCase('interlaced', patched((b) => b[28] = 1)),
    pngDecodeCase('depth 3', patched((b) => b[24] = 3)),
    pngDecodeCase('depth 0', patched((b) => b[24] = 0)),
    pngDecodeCase('type 1', patched((b) => b[25] = 1)),
    pngDecodeCase('type 5', patched((b) => b[25] = 5)),
    pngDecodeCase('palette without PLTE', patched((b) => b[25] = 3)),
    pngDecodeCase('truncated', Uint8List.sublistView(good, 0, 40)),
    pngDecodeCase('signature only', Uint8List.sublistView(good, 0, 8)),
    pngDecodeCase(
      'shorter than the signature',
      Uint8List.sublistView(good, 0, 5),
    ),
    pngDecodeCase('empty', Uint8List(0)),
    pngDecodeCase('no IEND', Uint8List.sublistView(good, 0, good.length - 12)),
    pngDecodeCase(
      'trailing bytes',
      Uint8List.fromList([...good, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]),
    ),
    pngDecodeCase(
      'trailing chunk after IEND',
      Uint8List.fromList([
        ...good,
        ..._chunk('tEXt', [65, 66]),
      ]),
    ),
    pngDecodeCase(
      'taller than the data',
      patched((b) {
        b[23] = 9;
      }),
    ),
    pngDecodeCase(
      'wider than the data',
      patched((b) {
        b[19] = 9;
      }),
    ),
    pngDecodeCase(
      'narrower than the data',
      patched((b) {
        b[19] = 2;
      }),
    ),
    pngDecodeCase(
      'filter 7',
      buildPng(
        width: 2,
        height: 2,
        colorType: 6,
        bitDepth: 8,
        channels: 4,
        rows: _rowsOf(16, 1),
        filterOf: (y) => y == 1 ? 7 : 0,
      ),
    ),
    pngDecodeCase('ancillary chunks', () {
      final raw = BytesBuilder()..add(good.sublist(0, 33));
      raw.add(_chunk('tEXt', [1, 2, 3]));
      raw.add(_chunk('gAMA', [0, 0, 0, 1]));
      raw.add(good.sublist(33));
      return raw.toBytes();
    }()),
    pngDecodeCase(
      'IEND first',
      Uint8List.fromList([
        137, 80, 78, 71, 13, 10, 26, 10, //
        ..._chunk('IEND', const []),
      ]),
    ),
    pngDecodeCase(
      'IHDR of the wrong size',
      Uint8List.fromList([
        137, 80, 78, 71, 13, 10, 26, 10, //
        ..._chunk('IHDR', [0, 0, 0, 1]),
        ..._chunk('IEND', const []),
      ]),
    ),
    pngDecodeCase(
      'garbage idat',
      Uint8List.fromList([
        ...good.sublist(0, 33),
        ..._chunk('IDAT', [1, 2, 3, 4, 5, 6, 7, 8]),
        ..._chunk('IEND', const []),
      ]),
    ),
    pngDecodeCase(
      'no idat',
      Uint8List.fromList([...good.sublist(0, 33), ..._chunk('IEND', const [])]),
    ),
    pngDecodeCase('two rows short', () {
      final raw = BytesBuilder()..addByte(0);
      raw.add(Uint8List(8));
      final z = ZLibEncoder().convert(raw.toBytes());
      return Uint8List.fromList([
        ...good.sublist(0, 33),
        ..._chunk('IDAT', z),
        ..._chunk('IEND', const []),
      ]);
    }()),
    pngDecodeCase('a row short', () {
      final raw = BytesBuilder();
      for (var y = 0; y < 4; y++) {
        raw.addByte(0);
        raw.add(Uint8List(y == 3 ? 12 : 16));
      }
      final z = ZLibEncoder().convert(raw.toBytes());
      return Uint8List.fromList([
        ...good.sublist(0, 33),
        ..._chunk('IDAT', z),
        ..._chunk('IEND', const []),
      ]);
    }()),
  ]);

  // Encoding. The compressed bytes are the library's own, so what is
  // compared is the header, the inflated rows and the checksums around them.
  const shapes = <(int, int)>[(1, 1), (3, 2), (13, 9), (6, 6), (0, 0), (4, 0)];
  for (final (w, h) in shapes) {
    final rgba = _rowsOf(w * h * 4, w + h);
    final png = encodePng(w, h, rgba);
    final chunks = <String>[];
    var offset = 8;
    Uint8List? ihdr;
    Uint8List? idat;
    final iend = <int>[];
    while (offset + 8 <= png.length) {
      final length = ByteData.sublistView(png).getUint32(offset);
      final type = String.fromCharCodes(png, offset + 4, offset + 8);
      chunks.add(type);
      final data = png.sublist(offset + 8, offset + 8 + length);
      if (type == 'IHDR') ihdr = png.sublist(offset, offset + 12 + length);
      if (type == 'IDAT') idat = data;
      if (type == 'IEND') {
        iend.addAll(png.sublist(offset, offset + 12 + length));
      }
      offset += 12 + length;
    }
    cases.add({
      'op': 'pngEncode',
      'width': w,
      'height': h,
      'rgba': b64(rgba),
      'expect': {
        'chunks': chunks,
        'ihdr': b64(ihdr!),
        'raw': b64(ZLibDecoder().convert(idat!)),
        'iend': b64(iend),
      },
    });
  }
  for (final (w, h, length) in const [
    (2, 2, 15),
    (2, 2, 17),
    (1, 1, 0),
    (3, 3, 4),
  ]) {
    cases.add({
      'op': 'pngEncodeRefused',
      'width': w,
      'height': h,
      'length': length,
      'expect': attempt(() => b64(encodePng(w, h, Uint8List(length)))),
    });
  }
  return cases;
}

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_sprite',
    name: 'sprite',
    cases: [
      ...atlasReadCases(),
      ...sequenceCases(),
      ...gridCases(),
      ...regionCases(),
      ...tilesetCases(),
      ...tileMapCases(),
      ...animationCases(),
      ...animationFromCases(),
      ...flipbookCases(),
      ...parallaxCases(),
      ...viewCases(),
      ...packCases(),
      ...writeAtlasCases(),
      ...pngCases(),
    ],
  );
}
