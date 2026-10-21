import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_parity_tool/fixture.dart';

Map<String, Object?> _case(
  String op,
  List<Object?> input,
  Object? Function() body,
) => {'op': op, 'in': input, 'expect': attempt(body)};

ContentHash _hashOf(String text) => ContentHash.of(utf8.encode(text));

// Ids.

const _ids = <String>[
  'models/robot.glb',
  'a',
  'a.b.c',
  'dir/.hidden',
  'dir/.hidden.png',
  'dir/trailing.',
  'dir/no_extension',
  'Mixed/Case.PNG',
  'spaces in/name.png',
  'ünï/çödé.png',
  'tab\tname',
  'models/../robot.glb',
  './models/robot.glb',
  'models//robot.glb',
  '/rooted/robot.glb',
  'models/robot.glb/',
  r'models\robot.glb',
  r'\robot.glb',
  ':robot.glb',
  'c:/robot.glb',
  'models/a:b.glb',
  '',
  ' ',
  '.',
  '..',
  'a/./b',
  'a/../b',
  '\u{feff}models/robot.glb',
  'models/\u{feff}robot.glb',
  'models/robot.glb\u{feff}',
  'models/\u{212a}.glb',
  'models/\u0130.glb',
  'models/robot.glb\n',
  'models/\u0000robot.glb',
  'models/%2e%2e/robot.glb',
  'models/100%.glb',
  'models/\u{1f600}.glb',
];

Object? _idParts(String text) {
  final id = AssetId.parse(text);
  return {
    'path': id.toString(),
    'name': id.name,
    'directory': id.directory,
    'extension': id.extension,
    'same': AssetId.tryParse(text)?.toString(),
  };
}

const _bases = <String>[
  'models/robot.glb',
  'robot.glb',
  'a/b/c/d.glb',
  'dir/.hidden',
  '\u{212a}/a.glb',
];

const _relatives = <String>[
  'tex.png',
  './tex.png',
  '../tex.png',
  '../../tex.png',
  '../../../tex.png',
  'sub/tex.png',
  'sub/../tex.png',
  'sub//tex.png',
  '/tex.png',
  'tex%20one.png',
  'tex%2Fone.png',
  'tex%2e%2e/one.png',
  'tex%zz.png',
  'tex%4z.png',
  'tex%z4.png',
  'tex%4',
  'tex%',
  'tex%41',
  'tex%29.png',
  'tex%2a.png',
  'tex%2A.png',
  'tex%2f.png',
  'tex\u007f.png',
  'tex%.png',
  'tex%c3%bc.png',
  'tex%ff.png',
  'tex%00.png',
  'tex.png?x=1',
  'tex.png#frag',
  'http://host/tex.png',
  'file:///tex.png',
  r'sub\tex.png',
  '',
  '.',
  '..',
  'tex.png/',
  ' tex.png',
  '\u{feff}tex.png',
  'tex%ef%bb%bf.png',
  '%e2%84%aa.png',
];

// Hashes.

final _samples = <List<int>>[
  const [],
  const [0],
  const [0, 1, 2, 255],
  utf8.encode('hello'),
  utf8.encode('\u{feff}'),
  List.generate(1000, (i) => i % 251),
];

final _hex = _hashOf('parse me').hex;

final _hashTexts = <String>[
  _hex,
  _hex.toUpperCase(),
  '${_hex.substring(0, 32)}${_hex.substring(32).toUpperCase()}',
  _hex.substring(1),
  '${_hex}0',
  '',
  ' $_hex',
  '$_hex ',
  '$_hex\n',
  '${_hex.substring(0, 10)}g${_hex.substring(11)}',
  '${_hex.substring(0, 10)}\u{ff10}${_hex.substring(11)}',
  '0' * 64,
  'f' * 64,
  'F' * 64,
  '\u{212a}' * 64,
];

// Manifests.

String _manifestText(Object? json) => jsonEncode(json);

Map<String, Object?> _asset(Object? hash, Object? bytes) => {
  'hash': hash,
  'bytes': bytes,
};

final _a = _hashOf('a').hex;
final _b = _hashOf('b').hex;

final _manifests = <String>[
  _manifestText({'formatVersion': 1, 'assets': <String, Object?>{}}),
  _manifestText({
    'formatVersion': 1,
    'assets': {
      'zeta/b.png': _asset(_b, 20),
      'alpha/a.glb': _asset(_a, 10),
      'alpha/A.glb': _asset(_a, 0),
      'mid.txt': _asset(_a, 3),
    },
  }),
  _manifestText({
    'formatVersion': 1,
    'assets': {
      'ok.png': _asset(_a, 10),
      '../bad.png': _asset(_a, 10),
      'worse\\id.png': _asset(_a, 10),
      'hash.png': _asset('nope', 10),
      'hash2.png': _asset(_a.toUpperCase(), 10),
      'bytes.png': _asset(_a, -1),
      'bytes2.png': _asset(_a, 1.5),
      'bytes3.png': _asset(_a, 2.0),
      'bytes4.png': _asset(_a, '10'),
      'bytes5.png': _asset(_a, null),
      'hash3.png': _asset(null, 10),
      'hash4.png': _asset(5, 10),
      'notmap.png': 'text',
      'list.png': [1],
      'missing.png': <String, Object?>{},
    },
  }),
  '{"formatVersion":1,"assets":{"big.png":{"hash":"$_a","bytes":9007199254740993}}}',
  '{"formatVersion":1,"assets":{"big.png":{"hash":"$_a","bytes":9223372036854775807}}}',
  '{"formatVersion":1,"assets":{"big.png":{"hash":"$_a","bytes":9223372036854775808}}}',
  '{"formatVersion":1,"assets":{"big.png":{"hash":"$_a","bytes":-0}}}',
  '{"formatVersion":1,"assets":{"big.png":{"hash":"$_a","bytes":1e2}}}',
  '{"formatVersion":1,"assets":{"dup.png":{"hash":"$_a","bytes":1},"dup.png":{"hash":"$_b","bytes":2}}}',
  _manifestText({'formatVersion': 1, 'assets': <Object?>[]}),
  _manifestText({'formatVersion': 1}),
  _manifestText({'assets': <String, Object?>{}}),
  _manifestText({'formatVersion': 2, 'assets': <String, Object?>{}}),
  _manifestText({'formatVersion': 0, 'assets': <String, Object?>{}}),
  _manifestText({'formatVersion': -1, 'assets': <String, Object?>{}}),
  _manifestText({'formatVersion': 1.0, 'assets': <String, Object?>{}}),
  _manifestText({'formatVersion': '1', 'assets': <String, Object?>{}}),
  '[]',
  '"text"',
  'null',
  '',
  '{',
  '\u{feff}{"formatVersion":1,"assets":{}}',
  ' \n{"formatVersion":1,"assets":{}}\n',
  '{"formatVersion":1,"assets":{}} trailing',
];

Object? _manifestOut(String text) {
  final load = AssetManifest.read(text);
  return {
    'encoded': load.manifest.encode(),
    'problems': load.problems,
    'count': load.manifest.entries.length,
  };
}

// Settings.

String _settingsText(Map<String, Object?> json) => jsonEncode(json);

final _settingsTexts = <String>[
  _settingsText({'formatVersion': 1, 'settings': <String, Object?>{}}),
  _settingsText({
    'formatVersion': 1,
    'importer': 'texture',
    'settings': {'zeta': 1, 'alpha': 'two', 'mid': 3.5, 'flag': true},
  }),
  _settingsText({
    'importer': 'raw',
    'settings': {
      'nested': {
        'b': 1,
        'a': [1, 2.0, null, 'x'],
      },
      'neg': -0.0,
      'big': 1e21,
      'small': 1e-7,
      'unicode': 'ünï\u{1f600}',
      'quote': 'say "hi"\n',
    },
  }),
  _settingsText({'importer': 'gltf'}),
  _settingsText({
    'settings': {'only': 1},
  }),
  _settingsText(<String, Object?>{}),
  _settingsText({'formatVersion': 2}),
  _settingsText({'formatVersion': 1.0}),
  _settingsText({'formatVersion': 1.5}),
  _settingsText({'formatVersion': 2.0}),
  _settingsText({'importer': 1.5}),
  _settingsText({'formatVersion': '1'}),
  _settingsText({'formatVersion': null, 'settings': null, 'importer': null}),
  _settingsText({'importer': ''}),
  _settingsText({'importer': 7}),
  _settingsText({'settings': []}),
  _settingsText({'settings': 'text'}),
  _settingsText({'formatVersion': 0, 'importer': 'x'}),
  _settingsText({'formatVersion': -3, 'importer': 'x'}),
  '[]',
  'null',
  '',
  '{',
  '\u{feff}{}',
  '{"settings":{"a":1,"a":2}}',
  '{"settings":{"n":12345678901234567890}}',
];

const _settingsIds = <String>[
  'textures/wall.png',
  'wall.png',
  'textures/wall.png.import.json',
  'textures/.import.json',
  'textures/wall.import.json',
  '.import.json',
  'a.import.json.import.json',
  'x.IMPORT.JSON',
  'models/robot.glb',
];

Object? _settingsNames(String text) {
  final id = AssetId.parse(text);
  return {
    'file': ImportSettings.fileFor(id).toString(),
    'isSettings': ImportSettings.isSettingsFile(id),
    'asset': ImportSettings.assetFor(id)?.toString(),
  };
}

// Cook keys.

Map<String, Object?> _spec({
  String importer = 'texture',
  int version = 1,
  String source = 'wall.png',
  Map<String, Object?> deps = const {},
  Map<String, Object?> settings = const {},
  Map<String, Object?> target = const {},
}) => {
  'importer': importer,
  'version': version,
  'source': source,
  'deps': deps,
  'settings': settings,
  'target': target,
};

final _keySpecs = <Map<String, Object?>>[
  _spec(),
  _spec(importer: 'gltf', version: 7),
  _spec(version: 0),
  _spec(version: -1),
  _spec(importer: ''),
  _spec(importer: 'ünï'),
  _spec(importer: 'say "hi"'),
  _spec(source: ''),
  _spec(deps: {'b.png': 'b', 'a.png': 'a', 'A.png': 'A', 'a/b.png': 'ab'}),
  _spec(
    deps: {'ünï.png': 'u', 'z.png': 'z', 'Z.png': 'Z', '\u{1f600}.png': 'e'},
  ),
  _spec(deps: {'../x.png': 'x'}),
  _spec(
    settings: {
      'z': 1,
      'a': 2,
      'm': {'y': 1, 'x': 2},
    },
  ),
  _spec(settings: {'i': 1, 'd': 1.0, 'neg': -0.0, 'z': 0.0, 'e': 1e21}),
  _spec(settings: {'p': 0.1, 'q': 1e-7, 'r': 123456789012345.0, 's': 5e-324}),
  _spec(
    settings: {'t': 1.7976931348623157e308, 'u': 1e22, 'v': 4.35, 'w': 100.0},
  ),
  _spec(settings: {'big': 9007199254740992, 'small': -9007199254740992}),
  _spec(
    settings: {
      'list': [3, 1, 2],
      'nested': [
        {'b': 1, 'a': 2},
        <Object?>[],
        <String, Object?>{},
      ],
    },
  ),
  _spec(settings: {'nothing': null, 'yes': true, 'no': false, 'text': ''}),
  _spec(settings: {'quote': 'a"b\\c\n\t\u0001\u{7f}\u{80}\u{2028}\u{1f600}'}),
  _spec(settings: {'ünï': 1, 'Z': 2, 'a': 3, '': 4, '\u{1f600}': 5}),
  _spec(
    target: {
      'os': 'macos',
      'families': ['astc', 'bc'],
      'limit': 2048,
    },
  ),
  _spec(target: {'os': 'ios'}),
  _spec(settings: {'a': 1}, target: {'a': 1}),
  _spec(settings: {'1': 'x', '10': 'y', '2': 'z', 'B': 1, 'a': 2}),
];

CookKey _keyOf(Map<String, Object?> spec) => CookKey(
  importer: spec['importer'] as String,
  importerVersion: spec['version'] as int,
  source: _hashOf(spec['source'] as String),
  dependencies: {
    for (final e in (spec['deps'] as Map<String, Object?>).entries)
      AssetId.parse(e.key): _hashOf(e.value as String),
  },
  settings: spec['settings'] as Map<String, Object?>,
  target: spec['target'] as Map<String, Object?>,
);

Object? _keyOut(Map<String, Object?> spec) {
  final key = _keyOf(spec);
  return {'recipe': key.recipe, 'hash': key.hash.hex};
}

// A cook cache run: what a directory holds after a script of stores and looks.

final _epoch = DateTime.utc(2026, 10, 21, 10, 0, 0, 123);

CookKey _cacheKey(String name, String os) => CookKey(
  importer: 'texture',
  importerVersion: 1,
  source: _hashOf(name),
  target: {'os': os},
);

String _text(int length, String letter) => letter * length;

Future<Object?> _cacheRun(int? limit, List<Object?> steps) async {
  final root = Directory.systemTemp.createTempSync('orblit_parity_cache_');
  try {
    var now = _epoch;
    final cache = DirectoryCookCache(
      '${root.path}/cache',
      limitBytes: limit,
      clock: () => now,
    );
    final stored = <String, CookKey>{};
    for (final step in steps.cast<List<Object?>>()) {
      switch (step[0]) {
        case 'store':
          final key = _cacheKey(step[1]! as String, step[2]! as String);
          stored['${step[1]}/${step[2]}'] = key;
          final outputs = (step[3]! as Map<String, Object?>).map(
            (name, text) => MapEntry(name, utf8.encode(text! as String)),
          );
          await cache.store(key, outputs);
        case 'use':
          await cache.lookUp(_cacheKey(step[1]! as String, step[2]! as String));
        case 'fail':
          final key = _cacheKey(step[1]! as String, step[2]! as String);
          stored['${step[1]}/${step[2]}'] = key;
          await cache.recordFailure(key, step[3]! as String);
        default:
          now = now.add(Duration(seconds: step[1]! as int));
      }
    }

    final hits = <String, Object?>{};
    final failures = <String, Object?>{};
    for (final entry in stored.entries) {
      final hit = await cache.lookUp(entry.value);
      hits[entry.key] = hit == null
          ? null
          : {
              for (final output in hit.outputs)
                output.name: [output.hash.hex, output.bytes],
            };
      failures[entry.key] = (await cache.lookUpFailure(entry.value))?.reason;
    }
    return {
      'files': _files(Directory('${root.path}/cache')),
      'hits': hits,
      'failures': failures,
    };
  } finally {
    root.deleteSync(recursive: true);
  }
}

Map<String, String> _files(Directory root) {
  if (!root.existsSync()) {
    return {};
  }
  final found = <String, String>{};
  for (final entity in root.listSync(recursive: true)) {
    if (entity is File) {
      final relative = entity.path.substring(root.path.length + 1);
      found[relative] = b64(entity.readAsBytesSync());
    }
  }
  return Map.fromEntries(
    found.entries.toList()..sort((a, b) => a.key.compareTo(b.key)),
  );
}

List<Object?> _store(String name, String os, Map<String, Object?> outputs) => [
  'store',
  name,
  os,
  outputs,
];

List<Object?> _use(String name, String os) => ['use', name, os];

List<Object?> _fail(String name, String os, String reason) => [
  'fail',
  name,
  os,
  reason,
];

List<Object?> _tick(int seconds) => ['tick', seconds];

final _runs = <(int?, List<Object?>)>[
  (null, []),
  (
    null,
    [
      _store('wall.png', 'macos', {'wall.ktx2': 'cooked wall'}),
    ],
  ),
  (
    null,
    [
      _store('wall.png', 'macos', {
        'a.ktx2': 'astc',
        'b.ktx2': 'bc',
        'c.ktx2': 'etc2',
      }),
      _store('wall.png', 'ios', {
        'a.ktx2': 'astc',
        'b.ktx2': 'bc',
        'c.ktx2': 'etc2',
      }),
    ],
  ),
  (
    null,
    [
      _store('empty.png', 'macos', {}),
      _store('same.png', 'macos', {'x': ''}),
    ],
  ),
  (
    null,
    [
      _store('wall.png', 'macos', {'out': 'first'}),
      _store('wall.png', 'macos', {'out': 'second'}),
    ],
  ),
  (
    null,
    [
      _fail('bad.png', 'macos', 'not a png at all'),
      _fail('worse.png', 'macos', 'say "hi"\nünï \u{1f600}'),
      _fail('bad.png', 'macos', 'newer reason'),
    ],
  ),
  (
    null,
    [
      _fail('wall.png', 'macos', 'encoder missing'),
      _tick(5),
      _store('wall.png', 'macos', {'out': 'cooked'}),
    ],
  ),
  (
    null,
    [
      _store('wall.png', 'macos', {'out': 'cooked'}),
      _tick(5),
      _fail('wall.png', 'macos', 'now it fails'),
    ],
  ),
  (
    null,
    [
      _store('wall.png', 'macos', {'z': 'zed', 'a': 'ay', 'M': 'em'}),
      _tick(1),
      _use('wall.png', 'macos'),
      _tick(90061),
      _use('wall.png', 'macos'),
    ],
  ),
  (
    null,
    [
      _store('wall.png', 'macos', {'ünï.ktx2': 'unicode name', '': 'no name'}),
    ],
  ),
  (
    250,
    [
      _store('first', 'macos', {'out': _text(100, 'a')}),
      _tick(86400),
      _store('second', 'macos', {'out': _text(100, 'b')}),
      _tick(86400),
      _store('third', 'macos', {'out': _text(100, 'c')}),
    ],
  ),
  (
    250,
    [
      _store('first', 'macos', {'out': _text(100, 'a')}),
      _tick(86400),
      _store('second', 'macos', {'out': _text(100, 'b')}),
      _tick(86400),
      _use('first', 'macos'),
      _tick(86400),
      _store('third', 'macos', {'out': _text(100, 'c')}),
    ],
  ),
  (
    50,
    [
      _store('enormous', 'macos', {'out': _text(500, 'e')}),
    ],
  ),
  (
    250,
    [
      _store('first', 'macos', {'out': _text(100, 'a')}),
      _store('second', 'macos', {'out': _text(100, 'b')}),
      _store('third', 'macos', {'out': _text(100, 'c')}),
    ],
  ),
  (
    150,
    [
      _store('first', 'macos', {'out': _text(100, 'z')}),
      _tick(86400),
      _store('first', 'ios', {'out': _text(100, 'z')}),
    ],
  ),
  (
    150,
    [
      _store('first', 'macos', {'out': _text(100, 'z')}),
      _tick(86400),
      _store('second', 'macos', {'out': _text(100, 'z')}),
    ],
  ),
  (
    150,
    [
      _store('first', 'macos', {'out': _text(100, 'z')}),
      _tick(86400),
      _store('second', 'macos', {'out': _text(100, 'y')}),
      _tick(86400),
      _store('third', 'macos', {'out': _text(40, 'x')}),
    ],
  ),
  (
    100,
    [
      _store('first', 'macos', {'a': _text(60, 'a'), 'b': _text(60, 'b')}),
      _tick(1),
      _store('second', 'macos', {'a': _text(60, 'a')}),
    ],
  ),
  (
    100,
    [
      _store('first', 'macos', {'out': _text(60, 'a')}),
      _fail('gone', 'macos', 'did not cook'),
      _tick(1),
      _store('second', 'macos', {'out': _text(60, 'b')}),
    ],
  ),
  (
    1,
    [
      _store('first', 'macos', {'out': 'ab'}),
      _tick(1),
      _store('second', 'macos', {'out': 'cd'}),
    ],
  ),
  (
    null,
    [
      for (var i = 0; i < 12; i++) ...[
        _store('file$i', i.isEven ? 'macos' : 'ios', {'out': 'cooked $i'}),
        _tick(i % 3),
      ],
      _use('file4', 'macos'),
    ],
  ),
  (
    null,
    [
      _store('wall.png', 'macos', {'out': 'cooked'}),
      _tick(3600),
      _use('wall.png', 'macos'),
      _tick(1),
      _use('wall.png', 'macos'),
    ],
  ),
  (
    200,
    [
      _store('a.png', 'macos', {'out': _text(100, 'a')}),
      _tick(1),
      _store('b.png', 'macos', {'out': _text(100, 'b')}),
      _tick(1),
      _store('c.png', 'macos', {'out': _text(100, 'c')}),
    ],
  ),
  (
    199,
    [
      _store('a.png', 'macos', {'out': _text(100, 'a')}),
      _tick(1),
      _store('b.png', 'macos', {'out': _text(100, 'b')}),
    ],
  ),
  (
    600,
    [
      for (var i = 0; i < 12; i++) ...[
        _store('file$i', 'macos', {'out': _text(100, 'abcdefghijkl'[i])}),
        _tick(i % 3),
        if (i % 4 == 0) _use('file${i ~/ 2}', 'macos'),
      ],
    ],
  ),
];

Future<void> main(List<String> args) async {
  final cases = <Map<String, Object?>>[
    for (final id in _ids) _case('id', [id], () => _idParts(id)),
    for (final base in _bases)
      for (final relative in _relatives)
        _case('resolve', [
          base,
          relative,
        ], () => AssetId.parse(base).resolve(relative)?.toString()),
    for (final bytes in _samples)
      _case('hashOf', [b64(bytes)], () => ContentHash.of(bytes).hex),
    for (final text in _hashTexts)
      _case('hashParse', [text], () {
        final hash = ContentHash.parse(text);
        return {'hex': hash.hex, 'shard': hash.shard};
      }),
    for (final text in _manifests)
      _case('manifest', [text], () => _manifestOut(text)),
    for (final text in _settingsTexts)
      _case('settings', [text], () {
        final settings = ImportSettings.parse(
          text,
          from: AssetId.parse('textures/wall.png.import.json'),
        );
        return {
          'importer': settings.importer,
          'values': tree(settings.values),
          'encoded': settings.encode(),
        };
      }),
    for (final id in _settingsIds)
      _case('settingsNames', [id], () => _settingsNames(id)),
    for (final spec in _keySpecs) _case('cookKey', [spec], () => _keyOut(spec)),
  ];
  for (final (limit, steps) in _runs) {
    cases.add({
      'op': 'cacheRun',
      'in': [limit, steps],
      'expect': {'value': await _cacheRun(limit, steps)},
    });
  }
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'asset',
    cases: cases,
  );
}
