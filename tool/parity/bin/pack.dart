import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/pack/pack_format.dart' show PackHeader;
import 'package:orblit_parity_tool/fixture.dart';

// A world is a list of pack specs. Each is built in order with `packBundle`,
// and `base` names earlier packs by index, in the order they were mounted.
Map<String, Object?> _pack(
  Map<String, String> files, {
  List<int> base = const [],
  String target = 'android',
  String? stale,
}) => {'files': files, 'base': base, 'target': target, 'stale': ?stale};

typedef _Built = ({AssetPack pack, Uint8List bytes, PackSummary summary});

Future<Map<String, Object?>> _try(Future<Object?> Function() body) async {
  try {
    return {'value': await body()};
  } on Object {
    return {'throws': true};
  }
}

Future<Map<String, Object?>> _case(
  String op,
  List<Object?> input,
  Future<Object?> Function() body,
) async => {'op': op, 'in': input, 'expect': await _try(body)};

Future<List<_Built>> _build(Directory temp, List<Object?> world) async {
  final built = <_Built>[];
  for (final (i, raw) in world.indexed) {
    final spec = raw! as Map<String, Object?>;
    final folder = '${temp.path}/bundle$i';
    Directory(folder).createSync(recursive: true);
    final entries = <AssetId, AssetEntry>{};
    for (final MapEntry(:key, :value)
        in (spec['files']! as Map<String, String>).entries) {
      final bytes = utf8.encode(value);
      final file = File('$folder/$key');
      file.parent.createSync(recursive: true);
      file.writeAsBytesSync(bytes);
      entries[AssetId.parse(key)] = AssetEntry(
        ContentHash.of(bytes),
        bytes.length,
      );
    }
    File(
      '$folder/manifest.json',
    ).writeAsStringSync(AssetManifest(entries: entries).encode());
    final stale = spec['stale'];
    if (stale is String) File('$folder/$stale').writeAsStringSync('stale');
    final out = '${temp.path}/$i.opack';
    final summary = await packBundle(
      bundlePath: folder,
      outPath: out,
      target: CookTargets.find(spec['target']! as String)!,
      base: [for (final b in spec['base']! as List<int>) built[b].pack],
    );
    final bytes = File(out).readAsBytesSync();
    built.add((
      pack: await AssetPack.open(MemoryPackBytes(bytes)),
      bytes: bytes,
      summary: summary,
    ));
  }
  return built;
}

Future<T> _inWorld<T>(
  List<Object?> world,
  Future<T> Function(List<_Built> built) body,
) async {
  final temp = Directory.systemTemp.createTempSync('orblit_pack_parity_');
  try {
    return await body(await _build(temp, world));
  } finally {
    temp.deleteSync(recursive: true);
  }
}

Map<String, Object?> _summary(PackSummary s) => {
  'hash': s.hash.hex,
  'bytes': s.bytes,
  'assets': s.assets,
  'added': s.added,
  'replaced': s.replaced,
  'blobs': s.blobs,
  'sharedBytes': s.sharedBytes,
  'removed': s.removed,
};

// Texts of a given length, different for each length.
String _sized(int n) => String.fromCharCode(97 + n % 26) * n;

String _scene(List<Object?> models) => jsonEncode({
  'entities': [
    for (final m in models) {'name': 'thing', 'model': m},
  ],
});

Map<String, String> _plus(
  Map<String, String> files,
  Map<String, String> more,
) => {...files, ...more};

// Pack worlds.

final _packWorlds = <List<Object?>>[
  [
    _pack({'a/x.glb': 'one'}),
  ],
  [_pack({})],
  [
    _pack({
      for (final n in [0, 1, 2, 15, 16, 17, 31, 32, 33, 48, 100, 4097])
        'blob/s$n.bin': _sized(n),
    }),
  ],
  [
    _pack({
      'a/1.png': 'same',
      'a/2.png': 'same',
      'a/3.png': 'same',
      'b/4.png': 'other',
    }),
  ],
  [
    _pack({'a/empty1.bin': '', 'a/empty2.bin': '', 'a/full.bin': 'x'}),
  ],
  [
    _pack({'a/héllo.txt': 'héllo ünï ✓ 😀', 'dir/ünï.json': '{"ü":1}'}),
  ],
  [
    for (final target in CookTargets.names)
      _pack({'a/x.glb': 'one'}, target: target),
  ],
  [
    _pack({
      'a/keep.glb': 'keep',
      'a/change.glb': 'before',
      'a/drop.glb': 'drop',
      'a/same1.glb': 'twin',
      'a/same2.glb': 'twin',
    }),
    _pack(
      {
        'a/keep.glb': 'keep',
        'a/change.glb': 'after',
        'a/fresh.glb': 'fresh',
        'a/same1.glb': 'twin',
        'a/same2.glb': 'twin',
      },
      base: [0],
    ),
    _pack(
      {
        'a/keep.glb': 'keep',
        'a/change.glb': 'after again',
        'a/fresh.glb': 'fresh',
        'a/drop.glb': 'drop',
        'a/same1.glb': 'twin',
        'a/same2.glb': 'twin',
      },
      base: [0, 1],
    ),
    _pack({}, base: [0, 1, 2]),
  ],
  [
    _pack({'a/x.glb': 'one', 'a/y.glb': 'two'}),
    _pack({'a/x.glb': 'one', 'a/y.glb': 'two'}, base: [0]),
    _pack({'a/x.glb': 'one', 'a/y.glb': 'two', 'a/z.glb': 'one'}, base: [0]),
    _pack({'a/x.glb': 'two', 'a/y.glb': 'one'}, base: [0]),
  ],
  [
    _pack({
      for (var i = 0; i < 40; i++)
        'models/m$i.glb': 'model ${i % 31}' * (i % 7 + 1),
    }),
    _pack(
      {
        for (var i = 5; i < 45; i++)
          'models/m$i.glb': 'model ${i % 29}' * (i % 5 + 1),
      },
      base: [0],
    ),
  ],
  [
    _pack({'a/x.glb': 'one'}),
    _pack({'a/x.glb': 'two'}, base: [0], target: 'web'),
  ],
  [
    _pack({'a/x.glb': 'one'}),
    _pack({'a/x.glb': 'one'}, base: [0, 0]),
  ],
  [
    _pack({'a/x.glb': 'one'}, stale: 'a/x.glb'),
  ],
  [
    _pack({'a/x.glb': 'one', 'a/y.glb': 'two'}),
    _pack({'a/x.glb': 'one', 'a/y.glb': 'two'}, base: [0], stale: 'a/y.glb'),
  ],
];

Future<Object?> _packOut(List<Object?> world) => _inWorld(
  world,
  (built) async => [
    for (final b in built)
      {
        'bytes': b64(b.bytes),
        'hash': b.pack.hash.hex,
        'summary': _summary(b.summary),
      },
  ],
);

// Opening raw bytes.

Future<Object?> _openOut(Uint8List bytes) async {
  final pack = await AssetPack.open(MemoryPackBytes(bytes));
  final reads = <String, Object?>{};
  for (final id in pack.manifest.entries.keys) {
    reads['$id'] = ContentHash.of(await pack.read(id)).hex;
  }
  return {
    'hash': pack.hash.hex,
    'target': pack.target,
    'base': pack.base?.hex,
    'removed': [for (final id in pack.removed) '$id'],
    'manifest': pack.manifest.encode(),
    'corrupt': [for (final id in await pack.findCorrupt()) '$id'],
    'reads': reads,
  };
}

Uint8List _edited(Uint8List pack, void Function(ByteData view) edit) {
  final copy = Uint8List.fromList(pack);
  edit(ByteData.sublistView(copy));
  return copy;
}

void _halves(ByteData view, int at, int low, int high) => view
  ..setUint32(at, low, Endian.little)
  ..setUint32(at + 4, high, Endian.little);

Uint8List _withDirectory(Uint8List pack, List<int> directory) {
  final header = PackHeader.read(pack);
  return Uint8List.fromList([
    ...PackHeader(
      directoryAt: header.directoryAt,
      directoryBytes: directory.length,
    ).encode(),
    ...pack.sublist(PackHeader.size, header.directoryAt),
    ...directory,
  ]);
}

Uint8List _redirected(
  Uint8List pack,
  void Function(Map<String, Object?> json) edit,
) {
  final header = PackHeader.read(pack);
  final json =
      jsonDecode(
            utf8.decode(
              pack.sublist(
                header.directoryAt,
                header.directoryAt + header.directoryBytes,
              ),
            ),
          )
          as Map<String, Object?>;
  edit(json);
  return _withDirectory(pack, utf8.encode(jsonEncode(json)));
}

Map<String, Object?> _firstBlob(Map<String, Object?> json) =>
    (json['blobs']! as Map<String, Object?>).values.first!
        as Map<String, Object?>;

String _someAsset(Map<String, Object?> json) =>
    (json['assets']! as Map<String, Object?>).keys.first;

String _someHash(Map<String, Object?> json) =>
    (json['blobs']! as Map<String, Object?>).keys.first;

final _hexes = 'f' * 64;

final _plainEdits = <void Function(Map<String, Object?> d)>[
  (d) => d['kind'] = 'other',
  (d) => d.remove('kind'),
  (d) => d['kind'] = 1,
  (d) => d['formatVersion'] = 0,
  (d) => d['formatVersion'] = 2,
  (d) => d['formatVersion'] = 1.0,
  (d) => d['formatVersion'] = '1',
  (d) => d['formatVersion'] = null,
  (d) => d.remove('formatVersion'),
  (d) => d['formatVersion'] = true,
  (d) => d.remove('target'),
  (d) => d['target'] = 5,
  (d) => d['target'] = '',
  (d) => d['target'] = 'mars',
  (d) => d['target'] = 'ANDROID',
  (d) => d['base'] = 'abc',
  (d) => d['base'] = 7,
  (d) => d['base'] = _hexes,
  (d) => d['removed'] = <String, Object?>{},
  (d) => d['removed'] = [3],
  (d) => d['removed'] = ['../x'],
  (d) => d['removed'] = [_someAsset(d)],
  (d) => d['removed'] = ['q/z.glb'],
  (d) => d.remove('removed'),
  (d) => d['assets'] = <Object?>[],
  (d) => d.remove('assets'),
  (d) => d['assets'] = {'bad//id': _someHash(d)},
  (d) => d['assets'] = {'a/x.glb': 5},
  (d) => d['assets'] = {'a/x.glb': 'xyz'},
  (d) => d['assets'] = {'a/x.glb': _hexes},
  (d) => d['assets'] = <String, Object?>{},
  (d) => d['assets'] = {'a/x.glb': _someHash(d), 'a/x.glb/': _someHash(d)},
  (d) => d['blobs'] = <Object?>[],
  (d) => d.remove('blobs'),
  (d) => d['blobs'] = {
    'zz': {'at': 32, 'bytes': 1},
  },
  (d) => _firstBlob(d)['at'] = '32',
  (d) => _firstBlob(d)['at'] = 32.0,
  (d) => _firstBlob(d)['at'] = 33.5,
  (d) => _firstBlob(d)['at'] = -16,
  (d) => _firstBlob(d)['at'] = 16,
  (d) => _firstBlob(d)['at'] = 0,
  (d) => _firstBlob(d)['at'] = 48,
  (d) => _firstBlob(d)['at'] = 36,
  (d) => _firstBlob(d)['at'] = 4503599627370496,
  (d) => _firstBlob(d)['bytes'] = -1,
  (d) => _firstBlob(d)['bytes'] = '3',
  (d) => _firstBlob(d)['bytes'] = 1.0,
  (d) => _firstBlob(d)['bytes'] = 1.5,
  (d) => _firstBlob(d)['bytes'] = 9007199254740992,
  (d) => _firstBlob(d)['bytes'] = 0,
  (d) => _firstBlob(d).remove('at'),
  (d) => _firstBlob(d).remove('bytes'),
  (d) => _firstBlob(d)['extra'] = 1,
  (d) => (d['blobs']! as Map<String, Object?>)[_someHash(d)] = 5,
  (d) => (d['blobs']! as Map<String, Object?>)[_hexes] = {'at': 32, 'bytes': 0},
  (d) =>
      (d['blobs']! as Map<String, Object?>)['A' * 64] = {'at': 32, 'bytes': 0},
  (d) => d['extra'] = 1,
  (d) => d['assets'] = {..._assets(d), 'z/new.glb': _someHash(d)},
];

Map<String, Object?> _assets(Map<String, Object?> d) =>
    d['assets']! as Map<String, Object?>;

final _patchEdits = <void Function(Map<String, Object?> d)>[
  (d) => d['base'] = null,
  (d) =>
      d['removed'] = [...(d['removed']! as List), ...(d['removed']! as List)],
  (d) => d['removed'] = (d['removed']! as List).reversed.toList(),
  (d) => d['removed'] = [...(d['removed']! as List), _someAsset(d)],
  (d) => d['base'] = _hexes,
  (d) => d['removed'] = <Object?>[],
];

final _rawDirectories = <String>[
  '',
  'not json',
  '[]',
  '"text"',
  '{',
  '{}',
  'null',
  '1',
  '\u{feff}{}',
];

List<Uint8List> _openCases(Uint8List plain, Uint8List patched) {
  final header = PackHeader.read(plain);
  final at = header.directoryAt;
  final length = header.directoryBytes;
  final cases = <Uint8List>[
    plain,
    patched,
    for (final n in [
      0,
      1,
      4,
      31,
      32,
      33,
      40,
      at,
      at + 1,
      plain.length - 1,
      plain.length ~/ 2,
    ])
      plain.sublist(0, n),
    Uint8List.fromList([...plain, 1, 2, 3]),
    Uint8List.fromList([...plain, ...List.filled(100, 0)]),
    for (var i = 0; i < 4; i++)
      _edited(plain, (v) => v.setUint8(i, v.getUint8(i) ^ 0xff)),
    for (final f in [0, 2, 0xffffffff, 1])
      _edited(plain, (v) => v.setUint32(4, f, Endian.little)),
    for (final (lo, hi) in [
      (0, 0),
      (31, 0),
      (32, 0),
      (at - 1, 0),
      (at + 1, 0),
      (at, 1),
      (0xffffffff, 0xffffffff),
      (0, 1),
      (at, 0x80000000),
      (at, 0xffffffff),
    ])
      _edited(plain, (v) => _halves(v, 8, lo, hi)),
    for (final (lo, hi) in [
      (0, 0),
      (length - 1, 0),
      (length + 1, 0),
      (length, 1),
      (0xffffffff, 0xffffffff),
      (0xffffffff, 0),
      (0, 0x80000000),
    ])
      _edited(plain, (v) => _halves(v, 16, lo, hi)),
    for (final edit in _plainEdits) _redirected(plain, edit),
    for (final edit in _patchEdits) _redirected(patched, edit),
    for (final text in _rawDirectories)
      _withDirectory(plain, utf8.encode(text)),
    _withDirectory(plain, [0xff, 0xfe, 0x7b]),
    _flipped(plain, 40),
    _flipped(plain, plain.length - 1),
    _flipped(patched, 40),
  ];
  return cases;
}

// A bit flipped in the file, which a read does not notice and findCorrupt does.
Uint8List _flipped(Uint8List pack, int at) =>
    _edited(pack, (v) => v.setUint8(at, v.getUint8(at) ^ 1));

// Diffs.

String _doc(Object? json) => jsonEncode(json);

final _older = {
  'textures/gone.png': 'gone',
  'models/tree.glb': 'tree',
  'anim/gone.oclip': '{}',
};

final _names = _doc({
  'clips': ['anim/gone.oclip'],
});

final _diffs = <(List<Object?>, int, int, List<int>)>[
  (
    [
      _pack({
        'textures/wall.png': 'wall one',
        'models/tree.glb': 'tree',
        'textures/gone.png': 'gone',
      }),
      _pack({
        'textures/wall.png': 'wall two is longer',
        'models/tree.glb': 'tree',
        'models/fresh.glb': 'fresh',
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack({'models/tree.glb': 'tree', 'a/scene.oscene': '{}'}),
      _pack({'models/tree.glb': 'tree', 'a/scene.oscene': '{}'}),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack({
        'textures/wall.png': 'wall',
        'models/tree.glb': 'tree',
        'scenes/town.oscene': _scene(['models/tree.glb', 'textures/wall.png']),
      }),
      _pack({
        'models/tree.glb': 'tree',
        'scenes/town.oscene': _scene(['models/tree.glb', 'textures/wall.png']),
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({
        'models/tree.glb': 'tree',
        'a/rig.oprefab': _names,
        'a/walk.oclip': _names,
        'a/move.oblend': _doc({
          'states': [
            {'clip': 'anim/gone.oclip'},
          ],
        }),
        'a/look.omat': _doc({'albedo': 'textures/gone.png'}),
        'a/cut.ocutscene': _names,
        'a/bones.obonemap': _names,
        'a/land.oterrain': _names,
        'a/hud.oui': _names,
        'a/road.opath': _names,
        'a/Big.OSCENE': _names,
        'a/data.json': _names,
        'a/notes.txt': _names,
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({
        'models/tree.glb': 'tree',
        'scenes/town.oscene': _doc({
          'overrides': {'textures/gone.png': 1},
        }),
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_plus(_older, {'scenes/tree.glb': 'tree'})),
      _pack({
        'scenes/town.oscene': _scene([
          '../models/tree.glb',
          './tree.glb',
          './models/tree.glb',
        ]),
        'scenes/tree.glb': 'tree',
        'textures/gone.png': 'kept',
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({
        'models/tree.glb': 'tree',
        'scenes/town.oscene': _doc({
          'name': 'Hero.1',
          'note': 'see textures/gone.png',
          'looks': 'matte white',
          'other': 'readme.txt',
        }),
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({
        'models/tree.glb': 'tree',
        'scenes/town.oscene': _scene(['textures/never.png', 'models/tree.glb']),
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({
        'models/tree.glb': 'tree',
        'textures/gone.png': 'back again',
        'scenes/town.oscene': _scene(['textures/gone.png']),
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({'models/tree.glb': 'tree', 'scenes/town.oscene': 'not json'}),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack({'scenes/town.oscene': 'not json'}),
      _pack({'scenes/town.oscene': 'not json', 'models/fresh.glb': 'fresh'}),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack({
        'textures/wall.png': 'wall',
        'textures/roof.png': 'roof',
        'scenes/town.oscene': _scene([
          'textures/wall.png',
          'textures/roof.png',
        ]),
      }),
      _pack(
        {
          'textures/wall.png': 'wall repainted',
          'scenes/town.oscene': _scene([
            'textures/wall.png',
            'textures/roof.png',
          ]),
        },
        base: [0],
      ),
    ],
    0,
    1,
    [0],
  ),
  (
    [
      _pack({
        'textures/wall.png': 'wall',
        'scenes/town.oscene': _scene(['textures/wall.png']),
      }),
      _pack(
        {
          'textures/wall.png': 'wall repainted',
          'scenes/town.oscene': _scene(['textures/wall.png']),
        },
        base: [0],
      ),
    ],
    0,
    1,
    [0],
  ),
  (
    [
      _pack({'a/x.glb': 'one'}),
      _pack({'a/x.glb': 'one'}, target: 'web'),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({
        'models/tree.glb': 'tree',
        'scenes/town.oscene': _doc({
          'deep': [
            [
              {
                'a': ['textures/gone.png'],
                'b': 3,
                'c': null,
                'd': true,
              },
            ],
          ],
          'case': 'Textures/Gone.png',
          'rooted': '/textures/gone.png',
          'query': 'textures/gone.png?x=1',
          'escaped': 'textures/gone%2Epng',
          'dotdot': 'textures/../textures/gone.png',
          'twice': ['textures/gone.png', 'textures/gone.png'],
        }),
        'scenes/other.oscene': _scene(['textures/gone.png', 'anim/gone.oclip']),
      }),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack(
        {
          'models/tree.glb': 'tree',
          'scenes/town.oscene': _scene(['textures/gone.png']),
        },
        base: [0],
      ),
      _pack(
        {
          'models/tree.glb': 'tree',
          'scenes/town.oscene': _scene(['textures/gone.png']),
          'textures/gone.png': 'again',
        },
        base: [0, 1],
      ),
    ],
    1,
    2,
    [0, 1],
  ),
  (
    [
      _pack(_older),
      _pack({'models/tree.glb': 'tree'}, base: [0]),
    ],
    0,
    1,
    [],
  ),
  (
    [
      _pack(_older),
      _pack({'models/tree.glb': 'tree'}, base: [0]),
    ],
    1,
    0,
    [0],
  ),
];

Future<Object?> _diffOut(
  List<Object?> world,
  int before,
  int after,
  List<int> pool,
) => _inWorld(world, (built) async {
  final result = await PackDiff.between(
    before: MountedPacks.withBases(built[before].pack, [
      for (final i in pool) built[i].pack,
    ]),
    after: MountedPacks.withBases(built[after].pack, [
      for (final i in pool) built[i].pack,
    ]),
  );
  return {
    'added': [for (final id in result.added) '$id'],
    'removed': [for (final id in result.removed) '$id'],
    'changed': [
      for (final c in result.changed) ['${c.id}', c.before, c.after],
    ],
    'dangling': [
      for (final d in result.dangling) ['${d.document}', '${d.missing}'],
    ],
    'unreadable': [for (final id in result.unreadable) '$id'],
    'broken': result.isBroken,
  };
});

// Chains.

List<Object?> _chainWorld(Map<String, String> state, List<List<int>> bases) => [
  _pack(state),
  for (final (i, base) in bases.indexed)
    _pack(
      _plus(state, {
        'textures/wall.ktx2': 'wall v${i + 2}',
        if (i > 1) 'models/tree.glb': 'tree v2',
      }),
      base: base,
    ),
];

final _state = {
  'textures/wall.ktx2': 'wall v1',
  'models/tree.glb': 'tree',
  'scenes/town.oscene': 'town',
};

final _threeDeep = _chainWorld(_state, [
  [0],
  [0, 1],
  [0, 1, 2],
]);

final _chains = <(List<Object?>, int, List<int>)>[
  (_threeDeep, 0, [1, 2, 3]),
  (_threeDeep, 0, [3, 2, 1]),
  (_threeDeep, 0, [2, 3, 1]),
  (_threeDeep, 0, [3, 1, 2]),
  (_threeDeep, 0, [3, 1]),
  (_threeDeep, 0, [3]),
  (_threeDeep, 0, [0, 1]),
  (_threeDeep, 0, [1, 1, 2, 2]),
  (_threeDeep, 0, []),
  (
    [
      _pack(_state),
      _pack(_plus(_state, {'textures/wall.ktx2': 'left'}), base: [0]),
      _pack(_plus(_state, {'textures/wall.ktx2': 'right'}), base: [0]),
      _pack(
        _plus(_state, {
          'textures/wall.ktx2': 'wall v2',
          'models/tree.glb': 'tree v2',
        }),
        base: [0, 1],
      ),
    ],
    0,
    [1, 2, 3],
  ),
  (
    [
      _pack(_state),
      _pack(_plus(_state, {'textures/wall.ktx2': 'wall v2'}), base: [0]),
      _pack(
        _plus(_state, {
          'textures/wall.ktx2': 'wall v2',
          'models/tree.glb': 'left',
        }),
        base: [0, 1],
      ),
      _pack(
        _plus(_state, {
          'textures/wall.ktx2': 'wall v2',
          'models/tree.glb': 'right',
        }),
        base: [0, 1],
      ),
    ],
    0,
    [2, 1, 3],
  ),
  (
    [
      _pack(_state),
      _pack(_state, target: 'web'),
      _pack(_plus(_state, {'textures/wall.ktx2': 'mod wall'})),
      _pack(_plus(_state, {'textures/wall.ktx2': 'older'}), target: 'ios'),
    ],
    0,
    [1, 2, 3],
  ),
  (
    [
      _pack(_state),
      _pack(_plus(_state, {'textures/wall.ktx2': 'mod wall'})),
    ],
    0,
    [0, 1, 1],
  ),
  (
    [
      _pack(_state),
      _pack(_plus(_state, {'textures/wall.ktx2': 'wall v2'}), base: [0]),
      _pack({..._state, 'models/rock.glb': 'rock'}),
    ],
    2,
    [1],
  ),
  (
    [
      _pack(_state),
      _pack(_plus(_state, {'textures/wall.ktx2': 'wall v2'}), base: [0]),
      _pack(_plus(_state, {'textures/wall.ktx2': 'mod 1'})),
      _pack(_plus(_state, {'textures/wall.ktx2': 'mod 2'})),
    ],
    0,
    [3, 1, 2],
  ),
];

Future<Object?> _chainOut(List<Object?> world, int shipped, List<int> found) =>
    _inWorld(world, (built) async {
      final chain = PatchChain.of(built[shipped].pack, [
        for (final i in found) built[i].pack,
      ]);
      return {
        'ordered': [for (final p in chain.ordered) p.hash.hex],
        'standalone': [for (final p in chain.standalone) p.hash.hex],
        'skipped': [
          for (final s in chain.skipped) [s.pack.hash.hex, s.reason],
        ],
      };
    });

// Mounting.

const _ids = [
  'textures/wall.ktx2',
  'models/tree.glb',
  'scenes/town.oscene',
  'models/rock.glb',
  'extra/mod.txt',
];

final _mountWorld = [
  _pack(_state),
  _pack(_plus(_state, {'textures/wall.ktx2': 'wall v2'}), base: [0]),
  _pack(
    {'textures/wall.ktx2': 'wall v3', 'scenes/town.oscene': 'town'},
    base: [0, 1],
  ),
  _pack({'textures/wall.ktx2': 'mod wall', 'extra/mod.txt': 'mod'}),
  _pack({'models/tree.glb': 'mod tree', 'extra/mod.txt': 'second mod'}),
  _pack(_state, target: 'web'),
  _pack({'scenes/town.oscene': 'town'}, base: [0]),
  _pack(
    {
      'models/rock.glb': 'rock',
      'models/tree.glb': 'tree',
      'textures/wall.ktx2': 'wall v1',
    },
    base: [0],
  ),
];

final _mounts = <(String, List<List<Object?>>)>[
  (
    'android',
    [
      ['start', 0],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 1],
      ['patch', 2],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 2],
    ],
  ),
  (
    'android',
    [
      ['patch', 1],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['start', 0],
    ],
  ),
  (
    'android',
    [
      ['start', 1],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['mod', 3],
      ['mod', 4],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['mod', 4],
      ['mod', 3],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['mod', 1],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 5],
    ],
  ),
  (
    'android',
    [
      ['start', 5],
    ],
  ),
  (
    'web',
    [
      ['start', 5],
      ['mod', 3],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 1],
      ['patch', 1],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 6],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 1],
      ['patch', 2],
      ['mod', 3],
    ],
  ),
  (
    'android',
    [
      ['mod', 3],
      ['start', 0],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['mod', 3],
      ['mod', 3],
    ],
  ),
  (
    'android',
    [
      ['start', 0],
      ['patch', 7],
      ['mod', 4],
    ],
  ),
  ('android', []),
];

Future<Object?> _mountOut(
  String target,
  List<Object?> world,
  List<List<Object?>> steps,
) => _inWorld(world, (built) async {
  final mounted = MountedPacks(target: CookTargets.find(target)!);
  final outcomes = <String>[];
  for (final [kind, index] in steps) {
    final pack = built[index! as int].pack;
    try {
      switch (kind) {
        case 'start':
          mounted.start(pack);
        case 'patch':
          mounted.patch(pack);
        default:
          mounted.addMod(pack);
      }
      outcomes.add('ok');
    } on PackMountRefused {
      outcomes.add('throws');
    }
  }
  final reads = <String, Object?>{};
  final owners = <String, Object?>{};
  for (final text in _ids) {
    final id = AssetId.parse(text);
    reads[text] = await mounted.exists(id)
        ? utf8.decode(await mounted.read(id))
        : null;
    owners[text] = mounted.ownerOf(id)?.hash.hex;
  }
  return {
    'steps': outcomes,
    'target': mounted.target,
    'chainHash': mounted.chainHash?.hex,
    'manifest': mounted.manifest.encode(),
    'reads': reads,
    'owners': owners,
  };
});

// Folders.

final _folderWorld = [
  _pack({
    'textures/wall.ktx2': 'wall ' * 100,
    'models/tree.glb': 'tree ' * 100,
  }),
  _pack(
    {'textures/wall.ktx2': 'wall v2 ' * 100, 'models/tree.glb': 'tree ' * 100},
    base: [0],
  ),
  _pack({'textures/wall.ktx2': 'mod ' * 100}),
];

final _folders = <(Map<String, Object?>, List<int>)>[
  ({}, []),
  (
    {
      '{h0}.opack': {'pack': 0},
      '{h1}.opack': {'pack': 1},
      '${'a' * 64}.opack': {'pack': 1},
      '${'b' * 64}.opack': {
        'fill': [100, 7],
      },
      '${'c' * 64}.opack': {
        'cut': [1, 200],
      },
      '${'d' * 64}.part': {
        'fill': [3, 1],
      },
      'notes.txt': {'text': 'hello'},
    },
    [],
  ),
  (
    {
      '{h2}.opack': {'pack': 2},
      '{h1}.opack': {'pack': 1},
      '{h0}.opack': {'pack': 0},
      '{h0}.part': {
        'fill': [1, 1],
      },
      'sub/{h0}.opack': {'pack': 0},
    },
    [],
  ),
  (
    {
      '{h1}.opack': {'pack': 1},
      '{h1}.part': {
        'fill': [1, 1],
      },
      '{h0}.opack': {'pack': 0},
    },
    [1, 1, 2],
  ),
  (
    {
      '{h0}.OPACK': {'pack': 0},
      '{h1}.opack.part': {'pack': 1},
      '.opack': {
        'fill': [10, 1],
      },
      '${'e' * 64}.opack': {'text': ''},
      '${'f' * 63}.opack': {'pack': 2},
      '{h2}.opack': {
        'cut': [2, 10],
      },
    },
    [0],
  ),
];

Future<Object?> _folderOut(Map<String, Object?> files, List<int> removes) =>
    _inWorld(_folderWorld, (built) async {
      final root = Directory.systemTemp.createTempSync('orblit_pack_folder_');
      try {
        final folder = '${root.path}/downloads';
        String named(String key) => key.replaceAllMapped(
          RegExp(r'\{h(\d)\}'),
          (m) => built[int.parse(m[1]!)].pack.hash.hex,
        );
        for (final MapEntry(:key, :value) in files.entries) {
          final spec = value! as Map<String, Object?>;
          final bytes = switch (spec) {
            {'pack': final int i} => built[i].bytes,
            {'cut': [final int i, final int n]} => built[i].bytes.sublist(0, n),
            {'fill': [final int n, final int b]} => Uint8List.fromList(
              List.filled(n, b),
            ),
            _ => Uint8List.fromList(utf8.encode(spec['text']! as String)),
          };
          final file = File('$folder/${named(key)}');
          file.parent.createSync(recursive: true);
          file.writeAsBytesSync(bytes);
        }
        final directory = PackFolder(folder);
        final listing = await directory.list();
        String name(String path) => path.substring(folder.length + 1);
        final removed = <bool>[
          for (final i in removes) await directory.remove(built[i].pack.hash),
        ];
        return {
          'packs': [for (final p in listing.packs) p.hash.hex],
          'unreadable': [for (final u in listing.unreadable) name(u.path)]
            ..sort(),
          'unfinished': [for (final h in listing.unfinished) h.hex],
          'removed': removed,
          'left': [
            if (Directory(folder).existsSync())
              for (final e in Directory(folder).listSync(recursive: true))
                if (e is File) name(e.path),
          ]..sort(),
        };
      } finally {
        root.deleteSync(recursive: true);
      }
    });

// Names and targets.

const _targetNames = [
  'macos',
  'windows',
  'linux',
  'ios',
  'android',
  'web',
  'MacOS',
  'WEB',
  'Android',
  'IOS',
  'İos',
  ' web',
  'web ',
  'webb',
  '',
  'any',
  'mac',
  'Linux',
  'WINDOWS',
];

const _packNames = ['game', 'my game', '', 'ünï', 'a/b', 'game.macos'];

Future<void> main(List<String> args) async {
  final plain = await _inWorld([
    _pack({
      'a/x.glb': 'one',
      'a/y.png': 'two two',
      'a/dup.png': 'two two',
      'a/empty.bin': '',
      'a/long.bin': _sized(100),
    }),
  ], (built) async => built.single.bytes);
  final patched = await _inWorld([
    _pack({
      'a/x.glb': 'one',
      'a/y.png': 'two two',
      'a/gone.png': 'gone',
      'a/also.png': 'also',
    }),
    _pack(
      {'a/x.glb': 'one', 'a/y.png': 'changed', 'a/new.png': 'new'},
      base: [0],
    ),
  ], (built) async => built.last.bytes);
  final cases = <Map<String, Object?>>[
    for (final world in _packWorlds)
      await _case('pack', [world], () => _packOut(world)),
    for (final bytes in _openCases(plain, patched))
      await _case('open', [b64(bytes)], () => _openOut(bytes)),
    for (final (world, before, after, pool) in _diffs)
      await _case('diff', [
        world,
        before,
        after,
        pool,
      ], () => _diffOut(world, before, after, pool)),
    for (final (world, shipped, found) in _chains)
      await _case('chain', [
        world,
        shipped,
        found,
      ], () => _chainOut(world, shipped, found)),
    for (final (target, steps) in _mounts)
      await _case('mount', [
        target,
        _mountWorld,
        steps,
        _ids,
      ], () => _mountOut(target, _mountWorld, steps)),
    for (final (files, removes) in _folders)
      await _case('folder', [
        _folderWorld,
        files,
        removes,
      ], () => _folderOut(files, removes)),
    for (final name in _targetNames)
      await _case('target', [name], () async {
        final target = CookTargets.find(name);
        return target == null ? null : {'recipe': tree(target.recipe)};
      }),
    for (final target in CookTargets.names)
      for (final name in [null, ..._packNames])
        await _case('packName', [target, name], () async {
          final found = CookTargets.find(target)!;
          return name == null
              ? packFileName(found)
              : packFileName(found, name: name);
        }),
  ];
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'pack',
    cases: cases,
  );
}
