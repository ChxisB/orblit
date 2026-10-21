import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/kit/kit_rules.dart';
import 'package:orblit_parity_tool/fixture.dart';

Map<String, Object?> _case(
  String op,
  List<Object?> input,
  Object? Function() body,
) => {'op': op, 'in': input, 'expect': attempt(body)};

// A manifest as a pack holds it, with some fields changed or dropped.
Map<String, Object?> _packed([
  Map<String, Object?> change = const {},
  List<String> drop = const [],
]) {
  final manifest = <String, Object?>{
    'kind': 'orblit.kit',
    'formatVersion': 1,
    'containsScripts': false,
    'slug': 'crate-pack',
    'publisher': 'brass-lamp',
    'name': 'Crate pack',
    'summary': 'Twelve crates.',
    'version': '1.2.0',
    'licence': {'spdx': 'CC-BY-4.0', 'url': 'https://example.com/licence'},
    'engine': {'from': '0.30.0', 'to': '0.40.0'},
    'tags': ['props'],
    'previews': <String>[],
    ...change,
  };
  drop.forEach(manifest.remove);
  return manifest;
}

Uint8List _bytes(Object? json) =>
    Uint8List.fromList(utf8.encode(jsonEncode(json)));

Map<String, Object?> _licence(String spdx, String url) => {
  'spdx': spdx,
  'url': url,
};

Map<String, Object?> _engine(String from, String to) => {
  'from': from,
  'to': to,
};

final _words = [
  '',
  'Crate',
  'a_b',
  '-a',
  'a-',
  'a--b',
  'a b',
  'a',
  'a1',
  '1a',
  'a-b-c',
  'a' * 40,
  'a' * 41,
  'caf\u00e9',
  'a\u001b[31m',
  'a\u200bb',
  '\uff41',
];

final _lines = [
  '  ',
  '\u3000',
  '\u00a0',
  '\u180e',
  '\u200b',
  '\u0085',
  '\ufeff',
  ' x ',
  'x',
  'a\nb',
  'a\rb',
  'a\tb',
  'a\u0085b',
  'a\u202eb',
  'a\u200bb',
  'a\u2028b',
  'a\u2029b',
  'a\u007fb',
  'a\u2066b',
  'a\ufffab',
  'a' * 60,
  'a' * 61,
  '\u{1f600}' * 30,
  '\u{1f600}' * 31,
  'caf\u00e9',
];

final _summaries = [
  ..._lines,
  'a' * 200,
  'a' * 201,
  '\u{1f600}' * 100,
  '\u{1f600}' * 101,
];

final _versions = [
  '1',
  '1.2',
  '1.2.3',
  '1.2.3.4',
  '01.2.3',
  'v1.2.3',
  '1.2.x',
  '1.2.1234567',
  '1.2.123456',
  '0.0.0',
  '999999.999999.999999',
  '1.2.3 ',
  ' 1.2.3',
  '1.2.3\n',
  '\uff11.2.3',
  '',
];

final _licences = [
  _licence('MIT', 'https://x.org/l'),
  _licence('MIT OR Apache-2.0', 'http://x.org/l'),
  _licence('GPL-2.0+', 'https://x.org/l'),
  _licence('(MIT)', 'https://x.org/l'),
  _licence('', 'https://x.org/l'),
  _licence('none', 'https://x.org/l'),
  _licence('NONE', 'https://x.org/l'),
  _licence('NOASSERTION', 'https://x.org/l'),
  _licence('noassertion', 'https://x.org/l'),
  _licence('MIT;rm', 'https://x.org/l'),
  _licence('-MIT', 'https://x.org/l'),
  _licence('a' * 80, 'https://x.org/l'),
  _licence('a' * 81, 'https://x.org/l'),
  _licence('MIT', ''),
  _licence('MIT', 'file:///etc/passwd'),
  _licence('MIT', 'example.com'),
  _licence('MIT', 'https://'),
  _licence('MIT', 'https:///path'),
  _licence('MIT', 'ftp://x.org/l'),
  _licence('MIT', 'HTTPS://X.ORG/L'),
  _licence('MIT', 'https://user:pw@x.org/l'),
  _licence('MIT', 'https://user@x.org/l'),
  _licence('MIT', 'https://x.org/${'a' * 200}'),
  _licence('MIT', 'https://x.org/${'a' * 178}'),
  _licence('MIT', 'https://x.org/a b'),
  _licence('MIT', 'https://x.org/\u202e'),
  _licence('MIT', 'https://x.org/\u00e9'),
  _licence('MIT', 'https://x.org:8080/l?q=1#f'),
  _licence('MIT', 'https://[::1]/l'),
];

final _engines = [
  _engine('0.9.0', '0.40.0'),
  _engine('0.40.0', '0.40.0'),
  _engine('0.40.0', '0.9.0'),
  _engine('0.40.1', '0.40.0'),
  _engine('1', '0.40.0'),
  _engine('0.30.0', 'x'),
  _engine('', ''),
  _engine('999999.0.0', '999999.0.1'),
  _engine('0.30.0', '999999.999999.999999'),
];

final _tags = <Object?>[
  <String>[],
  ['a'],
  ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'],
  ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i'],
  ['a', 'a'],
  ['A b'],
  ['a' * 24],
  ['a' * 25],
  ['a-b'],
  ['a--b'],
  [''],
  [1],
  [null],
  [<String>[]],
  'props',
  null,
  {'a': 1},
];

final _previews = <Object?>[
  <String>[],
  ['a.png'],
  ['previews/a.PNG'],
  ['a.jpg'],
  ['a.jpeg'],
  ['a.webp'],
  ['a.gif'],
  ['a'],
  ['a.png', 'a.png'],
  ['a.png', 'b.png', 'c.png', 'd.png', 'e.png', 'f.png'],
  ['a.png', 'b.png', 'c.png', 'd.png', 'e.png', 'f.png', 'g.png'],
  ['../a.png'],
  ['/a.png'],
  [''],
  [1],
  'a.png',
  null,
  {'a': 1},
];

final _formats = <Object?>[
  1,
  1.0,
  2,
  2.0,
  3,
  0,
  -1,
  1.5,
  '1',
  null,
  true,
  <Object?>[],
  4294967296,
  9223372036854775807,
];

// The text of a valid manifest with one piece changed, for the cases that no
// map of values can make: number spellings and keys that appear twice.
final _spellings = <String>[
  for (final text in ['1e0', '1E0', '10e-1', '1.0e0', '0.1e1', '1.0', '1.00'])
    jsonEncode(
      _packed(),
    ).replaceFirst('"formatVersion":1', '"formatVersion":$text'),
  for (final text in ['01', '+1', '1.', '.5', '-0', '0x1', 'NaN'])
    jsonEncode(
      _packed(),
    ).replaceFirst('"formatVersion":1', '"formatVersion":$text'),
  jsonEncode(_packed()).replaceFirst('{', '{"slug":"dup",'),
  jsonEncode(
    _packed(),
  ).replaceFirst('"formatVersion":1', '"formatVersion":1e400'),
  jsonEncode(_packed()).replaceFirst('{', '{"containsScripts":true,'),
  '${jsonEncode(_packed())}x',
  '${jsonEncode(_packed())}  \n',
  ' \n${jsonEncode(_packed())}',
  '\ufeff${jsonEncode(_packed())}',
  jsonEncode(_packed()).replaceFirst('Crate pack', r'Crate pack'),
  jsonEncode(_packed()).replaceFirst('Crate pack', r'Crate \u{1f600}'),
  jsonEncode(_packed()).replaceFirst('Crate pack', r'Crate \ud83d'),
  jsonEncode(_packed()).replaceFirst('Crate pack', r'Crate \x'),
  jsonEncode(_packed()).replaceFirst('Crate pack', 'Crate\tpack'),
  jsonEncode(_packed()).replaceFirst('Crate pack', r'Crate\u0000pack'),
  jsonEncode(_packed()).replaceFirst('"props"', '"props",'),
  jsonEncode(
    _packed(),
  ).replaceFirst('{', '{"extra":{"deep":[1,2,{"x":null}]},'),
  '',
  ' ',
  '{',
  '{}',
  '[]',
  '[1]',
  'null',
  '"kit"',
  '1',
  'true',
];

List<Map<String, Object?>> _manifests() {
  final changes = <Map<String, Object?>>[
    {},
    {'containsScripts': true},
    for (final bad in _words) ...[
      {'slug': bad},
      {'publisher': bad},
    ],
    for (final bad in _lines) {'name': bad},
    for (final bad in _summaries) {'summary': bad},
    for (final bad in _versions) {'version': bad},
    for (final bad in _licences) {'licence': bad},
    for (final bad in _engines) {'engine': bad},
    for (final bad in _tags) {'tags': bad},
    for (final bad in _previews) {'previews': bad},
    for (final bad in _formats) {'formatVersion': bad},
    for (final bad in <Object?>[
      'orblit.kit ',
      'orblit.pack',
      'Orblit.Kit',
      '',
      5,
      null,
      true,
    ])
      {'kind': bad},
    for (final bad in <Object?>['yes', null, 1, 0, 'true', <Object?>[]])
      {'containsScripts': bad},
    for (final field in ['slug', 'publisher', 'name', 'summary', 'version'])
      for (final bad in <Object?>[
        1,
        null,
        true,
        <Object?>[],
        {'a': 1},
      ])
        {field: bad},
    for (final field in ['licence', 'engine'])
      for (final bad in <Object?>[
        'x',
        1,
        null,
        <Object?>[],
        <String, Object?>{},
        {'spdx': 'MIT'},
        {'url': 'https://x.org/l'},
        {'from': '0.30.0'},
        {'to': '0.40.0'},
        {'spdx': 1, 'url': 'https://x.org/l', 'from': 1, 'to': 1},
      ])
        {field: bad},
    {'extra': 1, 'tags': <String>[], 'previews': <String>[]},
  ];
  return [
    for (final change in changes)
      _case('manifest', [
        b64(_bytes(_packed(change))),
        'read',
      ], () => _manifestOut(_bytes(_packed(change)), 'read')),
    for (final change in changes)
      _case('manifest', [
        b64(_bytes(_packed(change))),
        'authored',
      ], () => _manifestOut(_bytes(_packed(change)), 'authored')),
    for (final change in changes.take(2))
      _case('manifest', [
        b64(_bytes(_packed(change))),
        'authoredScripts',
      ], () => _manifestOut(_bytes(_packed(change)), 'authoredScripts')),
    for (final field in _packed().keys)
      for (final mode in ['read', 'authored'])
        _case('manifest', [
          b64(_bytes(_packed(const {}, [field]))),
          mode,
        ], () => _manifestOut(_bytes(_packed(const {}, [field])), mode)),
    for (final text in _spellings)
      for (final mode in ['read', 'authored'])
        _case('manifest', [
          b64(utf8.encode(text)),
          mode,
        ], () => _manifestOut(Uint8List.fromList(utf8.encode(text)), mode)),
    for (final bytes in <List<int>>[
      [0xff, 0xfe],
      [0xef, 0xbb, 0xbf, 0x7b, 0x7d],
      [0x7b, 0xc0, 0x80, 0x7d],
      [0x7b, 0xed, 0xa0, 0x80, 0x7d],
      [0xf4, 0x90, 0x80, 0x80],
      <int>[],
    ])
      for (final mode in ['read', 'authored'])
        _case('manifest', [
          b64(bytes),
          mode,
        ], () => _manifestOut(Uint8List.fromList(bytes), mode)),
    for (final length in [65535, 65536, 65537, 70000])
      for (final mode in ['read', 'authored'])
        _case(
          'manifestPadded',
          [length, mode],
          () => _manifestOut(_padded(length), mode),
        ),
  ];
}

// A valid manifest followed by spaces up to [length] bytes, so the size limit
// is met at its edge by a manifest that is otherwise good.
Uint8List _padded(int length) =>
    Uint8List.fromList(utf8.encode(jsonEncode(_packed()).padRight(length)));

Map<String, Object?> _manifestOut(Uint8List bytes, String mode) {
  final manifest = switch (mode) {
    'read' => KitManifest.read(bytes),
    'authored' => KitManifest.readAuthored(bytes, containsScripts: false),
    _ => KitManifest.readAuthored(bytes, containsScripts: true),
  };
  return {'hex': hex(manifest.encode()), 'scripts': manifest.containsScripts};
}

// A list of files for the rules. Each is a name or a name and a size, and a
// count makes that many small files.
Map<AssetId, int> _files(List<Object?> spec) {
  if (spec case [{'count': final int count}]) {
    return {for (var i = 0; i < count; i++) AssetId.parse('f/$i.bin'): 1};
  }
  return {
    for (final entry in spec)
      if (entry case [final String name, final Object size])
        AssetId.parse(name): size is String ? int.parse(size) : size as int
      else
        AssetId.parse(entry! as String): 10,
  };
}

KitManifest _kit({bool scripts = false, List<String> previews = const []}) =>
    KitManifest(
      slug: 'crate-pack',
      publisher: 'brass-lamp',
      name: 'Crate pack',
      summary: 'Twelve crates.',
      version: '1.0.0',
      licence: (spdx: 'MIT', url: 'https://example.com/l'),
      engine: (from: '0.30.0', to: '0.40.0'),
      containsScripts: scripts,
      previews: [for (final name in previews) AssetId.parse(name)],
    );

final _fileNames = <String>[
  'models/crate.glb',
  'textures/wall.png',
  '.env',
  '.git/hooks/pre-commit',
  'a/.b/c.png',
  'con',
  'CON',
  'Con.txt',
  'models/NUL.txt',
  'a/aux.png',
  'lpt1.png',
  'LPT9.png',
  'lpt0.png',
  'com0.png',
  'com1',
  'com1.tar.gz',
  'conx',
  'console.png',
  'auxiliary/a.png',
  'prn.',
  'a.png.',
  'a.png ',
  'models /a.png',
  'a .png',
  'a\u0001b.png',
  'caf\u00e9.png',
  'a<b.png',
  'a>b.png',
  'a|b.png',
  'a?.png',
  'a*b.png',
  'a"b.png',
  'a:b.png',
  'a%20b.png',
  'a#b.png',
  'a&b.png',
  'a,b.png',
  'a;b.png',
  'a=b.png',
  'a@b.png',
  'a!b.png',
  'a\$b.png',
  'a^b.png',
  'a`b.png',
  "a'b.png",
  'a[0].png',
  'a{b}.png',
  'a\u202eb.png',
  'a\u200bb.png',
  'a\u007fb.png',
  'a\u0085b.png',
  'a\u2028b.png',
  'a\u00a0b.png',
  'a\u3000b.png',
  'a\ufeffb.png',
  'a\u{1f600}b.png',
  '-a.png',
  '+a.png',
  '_a.png',
  '(a).png',
  ' a.png',
  'a~b.png',
  'a\\b.png',
  'a\u001b[31m.png',
  'Wall (2).png',
  'a_b-c+d.png',
  '1.png',
  'a/B.v2.glb',
  'A.PNG',
  'a b/c d.png',
  '${'a' * 100}.png',
  '${'a' * 96}.png',
  '${'a' * 97}.png',
  'a' * 100,
  'a' * 101,
  ['b' * 60, 'b' * 60, 'b' * 60, 'b' * 60].join('/'),
  ['b' * 79, 'b' * 79, 'b' * 80].join('/'),
  ['b' * 79, 'b' * 80, 'b' * 80].join('/'),
  ['b' * 100, 'c' * 100, 'd' * 38].join('/'),
  ['b' * 100, 'c' * 100, 'd' * 39].join('/'),
  'kits.lock',
  'Kits.Lock',
  'a/kits.lock',
  'script.js',
  'Run.SH',
  'a/b/c/Makefile',
  'x.exe',
  'x.dart',
];

List<Map<String, Object?>> _rules() {
  final limit = kitFileLimit;
  final half = kitTotalLimit ~/ 2;
  return [
    for (final name in _fileNames)
      _case('rules', [
        [name],
        {'scripts': false},
        null,
      ], () => kitProblems(_kit(), _files([name]))),
    for (final name in _fileNames)
      _case('rules', [
        [name],
        {'scripts': true},
        null,
      ], () => kitProblems(_kit(scripts: true), _files([name]))),
    for (final (files, previews, scripts, fileLimit) in [
      (<Object?>[], <String>[], false, null),
      (<Object?>['a.png', 'b.png'], <String>['a.png'], false, null),
      (
        <Object?>['a.png', 'b.png'],
        <String>['a.png', 'c.png', 'd.png'],
        false,
        null,
      ),
      (<Object?>['previews/a.png'], <String>['previews/a.png'], false, null),
      (<Object?>['a', 'a/b.png'], <String>[], false, null),
      (<Object?>['A', 'a/b.png', 'a/B.png'], <String>[], false, null),
      (<Object?>['a/b', 'a/b/c', 'a/b/c/d'], <String>[], false, null),
      (
        <Object?>['a/Wall.png', 'a/wall.png', 'A/WALL.png'],
        <String>[],
        false,
        null,
      ),
      (<Object?>['scripts/run.js'], <String>[], true, null),
      (<Object?>['scripts/run.js', 'a.png'], <String>[], false, null),
      (<Object?>['a.png'], <String>[], true, null),
      (<Object?>['a.bin'], <String>[], false, 9),
      (<Object?>['a.bin'], <String>[], false, 10),
      (<Object?>['a.bin'], <String>[], false, 11),
      (<Object?>['a.bin'], <String>[], false, 0),
      (<Object?>['a.bin'], <String>[], false, -1),
      (
        <Object?>[
          {'count': 4999},
        ],
        <String>[],
        false,
        null,
      ),
      (
        <Object?>[
          {'count': 5000},
        ],
        <String>[],
        false,
        null,
      ),
      (
        <Object?>[
          {'count': 5001},
        ],
        <String>[],
        false,
        null,
      ),
      (
        <Object?>[
          {'count': 5002},
        ],
        <String>['previews/a.png'],
        true,
        null,
      ),
    ])
      _case(
        'rules',
        [
          files,
          {'scripts': scripts, 'previews': previews},
          fileLimit,
        ],
        () => kitProblems(
          _kit(scripts: scripts, previews: previews),
          _files(files),
          fileLimit: fileLimit ?? kitFileLimit,
        ),
      ),
    for (final (sizes, fileLimit) in [
      ([('a.bin', 0)], null),
      ([('a.bin', 1)], null),
      ([('a.bin', limit)], null),
      ([('a.bin', limit + 1)], null),
      ([('a.bin', 1 << 40)], null),
      ([('a.bin', 1 << 62)], null),
      ([('a.bin', 1 << 62)], 1 << 62),
      ([('a.bin', 1 << 62), ('b.bin', 1 << 62)], 1 << 62),
      ([('a.bin', 1 << 62), ('b.bin', 1 << 62), ('c.bin', 1 << 62)], 1 << 62),
      (
        [
          ('a.bin', 1 << 62),
          ('b.bin', 1 << 62),
          ('c.bin', 1 << 62),
          ('d.bin', 1 << 62),
        ],
        1 << 62,
      ),
      ([('a.bin', 9223372036854775807), ('b.bin', 1)], 9223372036854775807),
      ([('a.bin', -1)], null),
      ([('a.bin', -9223372036854775808)], null),
      ([('a.bin', half), ('b.bin', half)], kitTotalLimit),
      ([('a.bin', half + 1), ('b.bin', half)], kitTotalLimit),
      ([('a.bin', half + 1), ('b.bin', half + 1)], kitTotalLimit),
      ([('a.bin', kitTotalLimit), ('b.bin', 1)], kitTotalLimit),
      ([('a.bin', kitTotalLimit + 1)], kitTotalLimit + 1),
    ])
      _case(
        'rules',
        [
          [
            for (final (name, size) in sizes) [name, big(size)],
          ],
          {'scripts': false},
          fileLimit == null ? null : big(fileLimit),
        ],
        () => kitProblems(_kit(), {
          for (final (name, size) in sizes) AssetId.parse(name): size,
        }, fileLimit: fileLimit ?? kitFileLimit),
      ),
  ];
}

final _folders = <String>[
  '',
  'assets',
  'assets/kits/crate-pack',
  '.git',
  'a/.b',
  'a//b',
  'a/',
  '/a',
  'con',
  'a b.',
  'a ',
  'a.',
  'CON.txt',
  'a/con',
  'caf\u00e9',
  'a\u202eb',
  'a\u001b[31m',
  'a\\b',
  'a/../b',
  '..',
  '.',
  'a' * 100,
  'a' * 101,
  ['a' * 80, 'b' * 80, 'c' * 70].join('/'),
  ['a' * 80, 'b' * 80, 'c' * 76].join('/'),
  ['a' * 80, 'b' * 80, 'c' * 77].join('/'),
  ['a' * 80, 'b' * 80, 'c' * 78].join('/'),
  'kits.lock',
  'Kits',
  'a b',
  'A_b-c+d.(1)',
];

final _placeFiles = <List<String>>[
  ['models/crate.glb'],
  ['kits.lock'],
  ['Kits.Lock'],
  ['a/kits.lock'],
  ['models/crate.glb', 'kits.lock'],
  ['a.png', 'models/${'z' * 80}.png'],
  ['${'z' * 100}.png'],
  ['${'y' * 80}/${'y' * 80}.png'],
  <String>[],
];

final _versionsToCompare = [
  '0.30.0',
  '0.40.0',
  '0.35.2',
  '0.29.9',
  '0.40.1',
  '1.0.0',
  '0.9.0',
  '0.100.0',
  '0.30.0-dev',
  '0.30',
  '0.30.0.1',
  'unknown',
  '',
  'v0.35.0',
  '00.35.0',
  '0.35.1000000',
  '0.35.999999',
  '999999.0.0',
  '1.2.3 ',
];

final _updateNames = ['B/x', 'a/x', 'a/y', 'a/X', 'a/b/c', 'a-b', 'a/b'];

List<Map<String, Object?>> _updates() {
  Map<String, Object?> run(
    Map<String, String> locked,
    Map<String, String> incoming,
    Map<String, String?> onDisk,
  ) {
    ContentHash hashOf(String text) => ContentHash.of(utf8.encode(text));
    final plan = planKitUpdate(
      locked: {
        for (final e in locked.entries) AssetId.parse(e.key): hashOf(e.value),
      },
      incoming: {
        for (final e in incoming.entries) AssetId.parse(e.key): hashOf(e.value),
      },
      onDisk: {
        for (final e in onDisk.entries)
          AssetId.parse(e.key): e.value == null ? null : hashOf(e.value!),
      },
    );
    List<String> names(List<AssetId> ids) => [for (final id in ids) '$id'];
    return {
      'replace': names(plan.replace),
      'add': names(plan.add),
      'remove': names(plan.remove),
      'kept': names(plan.kept),
      'empty': plan.isEmpty,
    };
  }

  const locks = <String?>[null, '1'];
  const incomings = <String?>[null, '1', '2'];
  const disks = <Object?>['missing', null, '1', '2', 'mine'];
  return [
    // Every state of one place.
    for (final lock in locks)
      for (final incoming in incomings)
        if (lock != null || incoming != null)
          for (final disk in disks)
            _case(
              'update',
              [
                {'a/x': ?lock},
                {'a/x': ?incoming},
                {if (disk != 'missing') 'a/x': disk},
              ],
              () => run({'a/x': ?lock}, {'a/x': ?incoming}, {
                if (disk != 'missing') 'a/x': disk as String?,
              }),
            ),
    // Several places at once, so the order is the order the plan keeps.
    for (final (locked, incoming, onDisk) in [
      (
        {for (final name in _updateNames) name: '1'},
        {for (final name in _updateNames) name: '2'},
        <String, String?>{for (final name in _updateNames) name: '1'},
      ),
      (
        {for (final name in _updateNames) name: '1'},
        <String, String>{},
        <String, String?>{for (final name in _updateNames) name: '1'},
      ),
      (
        <String, String>{},
        {for (final name in _updateNames) name: '1'},
        <String, String?>{for (final name in _updateNames) name: null},
      ),
      (
        {for (final name in _updateNames) name: '1'},
        {for (final name in _updateNames.reversed) name: '2'},
        <String, String?>{
          for (final (i, name) in _updateNames.indexed)
            name: i.isEven ? '1' : 'mine',
        },
      ),
      (
        {for (final name in _updateNames.take(4)) name: '1'},
        {for (final name in _updateNames.skip(2)) name: '1'},
        <String, String?>{for (final name in _updateNames) name: 'mine'},
      ),
    ])
      _case('update', [
        locked,
        incoming,
        onDisk,
      ], () => run(locked, incoming, onDisk)),
  ];
}

Map<String, Object?> _idOut(String name) {
  final id = AssetId.parse(name);
  return {
    'script': isKitScript(id),
    'carries': carriesScripts([id]),
  };
}

void main(List<String> args) {
  final cases = <Map<String, Object?>>[
    ..._manifests(),
    ..._rules(),
    for (final folder in _folders)
      for (final names in _placeFiles)
        _case(
          'placeProblem',
          [folder, names],
          () => kitPlaceProblem(folder, [
            for (final n in names) AssetId.parse(n),
          ]),
        ),
    for (final folder in _folders)
      for (final name in ['a.png', 'models/crate.glb', 'kits.lock'])
        _case('place', [
          name,
          folder,
        ], () => '${kitPlace(AssetId.parse(name), folder)}'),
    for (final version in _versionsToCompare)
      for (final range in [
        (from: '0.30.0', to: '0.40.0'),
        (from: '0.30.0', to: '0.30.0'),
        (from: '0.40.0', to: '0.30.0'),
        (from: 'x', to: '0.40.0'),
        (from: '0.30.0', to: 'x'),
        (from: '0.30.0', to: '1.0.0'),
        (from: '0.9.0', to: '0.100.0'),
      ])
        _case('fits', [
          range.from,
          range.to,
          version,
        ], () => kitFitsEngine(range, version)),
    ..._updates(),
    for (final name in [
      'a.js',
      'a.DART',
      'x/y.sh',
      'b.ps1',
      'c.py',
      'd.ts',
      'e.EXE',
      'Makefile',
      'MAKEFILE',
      'tools/pubspec.yaml',
      'tools/Pubspec.Yaml',
      'gradlew',
      'Gradlew',
      'a/CMakeLists.txt',
      'cmakelists.txt',
      'a.glb',
      'b.png',
      'c.json',
      'package.json',
      'a/package.json',
      'a/package.JSON',
      'a.js.png',
      'a.png.js',
      'js',
      '.js',
      'a.',
      'x/.sh',
      'a.kts',
      'a.jar',
      'a.dll',
      'a.so',
      'a.dylib',
      'a.msi',
      'a.lnk',
      'a.bat',
      'a.cmd',
      'a.rb',
      'a.pl',
      'a.lua',
      'a.cs',
      'a.html',
      'a.txt',
    ])
      _case('scripts', [name], () => _idOut(name)),
  ];
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'kit',
    cases: cases,
  );
}
