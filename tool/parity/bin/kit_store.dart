import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/kit/kit_entry.dart'
    show checkKitDate, kitEntryLimit;
import 'package:orblit_parity_tool/fixture.dart';

Map<String, Object?> _case(
  String op,
  List<Object?> input,
  Object? Function() body,
) => {'op': op, 'in': input, 'expect': attempt(body)};

final String _hash = ContentHash.of([1, 2, 3]).hex;
final String _hashB = ContentHash.of(utf8.encode('b')).hex;

Uint8List _json(Object? json) =>
    Uint8List.fromList(utf8.encode(jsonEncode(json)));

Uint8List _text(String text) => Uint8List.fromList(utf8.encode(text));

// Everything below that is "raw" is bytes a person wrote by hand, which a
// jsonEncode of a map could not make: a duplicate key, a number as 1e3, a
// byte order mark.
Uint8List _asBytes(Object? json) => json is Uint8List ? json : _json(json);

// ---- entries -------------------------------------------------------------

Map<String, Object?> _release(
  String version, [
  Map<String, Object?> change = const {},
  List<String> drop = const [],
]) {
  final release = <String, Object?>{
    'version': version,
    'url': 'https://example.com/crates-$version.opack',
    'hash': _hash,
    'bytes': 1234,
    'released': '2026-10-19',
    'notes': 'Changes.',
    ...change,
  };
  drop.forEach(release.remove);
  return release;
}

Map<String, Object?> _one(Map<String, Object?> release) => {
  'releases': [release],
};

Map<String, Object?> _entryOut(Uint8List bytes, String publisher, String slug) {
  final entry = KitEntry.parse(bytes, publisher: publisher, slug: slug);
  return {
    'hex': hex(entry.encode()),
    'versions': [for (final release in entry.releases) release.version],
    'offer': '${entry.latest.offer.url}',
    'origin': '${entry.latest.origin.base}',
    'max': entry.latest.origin.policy.maxBytes,
  };
}

Map<String, Object?> _entry(
  Object? json, [
  String publisher = 'brass-lamp',
  String slug = 'crates',
]) {
  final bytes = _asBytes(json);
  return _case('entry', [
    b64(bytes),
    publisher,
    slug,
  ], () => _entryOut(bytes, publisher, slug));
}

final _urls = [
  'https://example.com/crates.opack',
  'https://github.com/o/r/releases/download/v1/a.opack',
  'https://cdn-1.example.co.uk/a.opack?raw=1',
  'https://example.com:443/a.opack',
  'https://example.com/a.opack?',
  'https://example.com',
  'https://example.com?x=1',
  'HTTPS://Example.COM/a.opack',
  'https://EXAMPLE.com/A.opack',
  'https://example.com/a%20b',
  'https://example.com/%zz',
  'https://example.com/\u00e9',
  'https://xn--mnchen-3ya.de/a.opack',
  'https://a.b.c.d.e.f.org/path/with/many/segments',
  'https://example.c0m/a',
  'https://localhost.com/a',
  'https://a.internal.b/a',
  'https://example.com/a.opack#',
  'https://example.com/${'a' * 280}',
  'https://example.com/${'a' * 281}',
  'http://example.com/a.opack',
  'ftp://example.com/a.opack',
  'https://me@example.com/a',
  'https://me:pw@example.com/a',
  'https://example.com/a b',
  'https://exa mple.com/a',
  'https://example.com/a\tb',
  'https://example.com/a\u00a0b',
  'https://example.com/a\u3000b',
  'https://example.com/a\u0085b',
  'https://example.com/a\u2028b',
  'https://example.com/a\ufeffb',
  'https://example.com/a\u202e',
  'https://example.com/a\u200b',
  'https://example.com/a\u007f',
  'https://example.com/a\n',
  'https://localhost/a.opack',
  'https://LOCALHOST/a.opack',
  'https://app.localhost/a.opack',
  'https://127.0.0.1/a.opack',
  'https://127.1/a.opack',
  'https://0x7f.0.0.1/a.opack',
  'https://2130706433/a.opack',
  'https://169.254.169.254/latest',
  'https://1.2.3.4/a',
  'https://[::1]/a.opack',
  'https://[2001:db8::1]/a',
  'https://printer.local/a.opack',
  'https://printer.LOCAL/a.opack',
  'https://build.internal/a.opack',
  'https://intranet/a.opack',
  'https://example.com./a.opack',
  'https://.example.com/a',
  'https://a..com/a',
  'https://-a.com/a',
  'https://a-.com/a',
  'https://a.b-.c/a',
  'https://example.123/a',
  'https://m\u00fcnchen.de/a.opack',
  'https://example.com:8443/a.opack',
  'https://example.com:80/a.opack',
  'https://example.com:0/a.opack',
  'https://example.com:/a.opack',
  'https://example.com:0443/a.opack',
  'https://example.com:99999/a.opack',
  'https://example.com:abc/a.opack',
  'https://example.com\\@evil.com/a.opack',
  'https://example.com@evil.com/a.opack',
  'https://example.com/a.opack#evil.com',
  'https://example.com/\\a',
  'https://example.com/a@b',
  'https:example.com/a',
  'https:/example.com/a',
  'https:///a',
  '//example.com/a',
  '/a.opack',
  'a.opack',
  'example.com/a',
  '',
  ' https://example.com/a',
  'https://example.com/a ',
];

final _versions = [
  '1.2.0',
  '0.0.0',
  '999999.999999.999999',
  '1000000.0.0',
  '1.2',
  '1',
  '01.2.3',
  '1.02.3',
  '1.2.3-beta',
  '1.2.3+1',
  '1.2.3.4',
  'v1.2.3',
  ' 1.2.3',
  '1.2.3 ',
  '1.2.3\n',
  '',
  'a.b.c',
  '1.2.-3',
  '\uff11.2.3',
];

final _hashes = [
  _hash,
  _hash.toUpperCase(),
  '${_hash.substring(0, 32)}${_hash.substring(32).toUpperCase()}',
  _hash.substring(1),
  '${_hash}0',
  '0x${_hash.substring(2)}',
  'z' * 64,
  '$_hash\n',
  ' $_hash',
  '',
  '\uff10' * 64,
];

final _sizes = <Object?>[
  1,
  0,
  -1,
  1 << 20,
  kitPackLimit,
  kitPackLimit + 1,
  1 << 40,
  1234.0,
  1.5,
  1e3,
  '12',
  null,
  true,
  [1],
];

final _days = [
  '2026-10-19',
  '0000-01-01',
  '0000-12-31',
  '0001-01-01',
  '9999-12-31',
  '2026-02-28',
  '2026-02-29',
  '2026-02-30',
  '2024-02-29',
  '2023-02-29',
  '1900-02-29',
  '2000-02-29',
  '0000-02-29',
  '0100-02-29',
  '0400-02-29',
  '2026-04-30',
  '2026-04-31',
  '2026-13-01',
  '2026-00-10',
  '2026-01-00',
  '2026-01-32',
  '2026-2-3',
  '2026-02-3',
  '26-10-19',
  '20261-10-19',
  '2026/10/19',
  '2026-10-19 ',
  ' 2026-10-19',
  '2026-10-19\n',
  '2026-10-19T00:00:00Z',
  '\uff12\uff10\uff12\uff16-10-19',
  '2026-10-1\u0663',
  '',
];

final _notes = <Object?>[
  '',
  'Changes.',
  'a' * 300,
  'a' * 301,
  '\u{1F600}' * 150,
  '\u{1F600}' * 151,
  'Caf\u00e9 \u2713',
  'tab\t',
  'two\nlines',
  'carriage\r',
  'escape\u001b',
  'bidi\u202e',
  'zero\u200b',
  'del\u007f',
  'c1\u0085',
  'bom\ufeff',
  'sep\u2028',
  null,
  5,
  ['x'],
];

const _releaseKeys = ['version', 'url', 'hash', 'bytes', 'released', 'notes'];

final _wrongTypes = <Object?>[
  null,
  5,
  1.5,
  true,
  <Object?>[],
  <String, Object?>{},
];

String _padded(Map<String, Object?> json, int length) =>
    jsonEncode(json).padRight(length);

List<Map<String, Object?>> _entries() {
  final good = _release('1.0.0');
  final raw = jsonEncode(_one(good));
  return [
    _entry(_one(good)),
    _entry({
      'releases': [
        for (final v in [
          '1.2.0',
          '1.10.0',
          '1.9.0',
          '0.30.0',
          '10.0.0',
          '2.0.0',
          '1.2.10',
          '1.2.9',
          '999999.0.0',
          '0.0.1',
        ])
          _release(v),
      ],
    }),
    _entry({
      'releases': [
        for (var i = 0; i < 200; i++) _release('${i ~/ 100}.${i % 100}.0'),
      ],
    }),
    _entry({
      'releases': [
        for (var i = 0; i < 201; i++) _release('${i ~/ 100}.${i % 100}.0'),
      ],
    }),
    for (final url in _urls) _entry(_one(_release('1.0.0', {'url': url}))),
    for (final v in _versions) _entry(_one(_release('1.0.0', {'version': v}))),
    for (final h in _hashes) _entry(_one(_release('1.0.0', {'hash': h}))),
    for (final size in _sizes) _entry(_one(_release('1.0.0', {'bytes': size}))),
    for (final day in _days) _entry(_one(_release('1.0.0', {'released': day}))),
    for (final notes in _notes)
      _entry(_one(_release('1.0.0', {'notes': notes}))),
    for (final key in _releaseKeys) ...[
      _entry(_one(_release('1.0.0', const {}, [key]))),
      for (final value in _wrongTypes)
        _entry(_one(_release('1.0.0', {key: value}))),
    ],
    _entry(_one(_release('1.0.0', {'sha': 'x'}))),
    _entry(_one(_release('1.0.0', {'\u202e': 'x'}))),
    _entry({
      'releases': [_release('1.0.0')],
      'name': 'x',
    }),
    _entry({
      'releases': [_release('1.0.0'), _release('1.0.0')],
    }),
    _entry({
      'releases': [_release('1.0.0'), _release('1.0.1'), _release('1.0.0')],
    }),
    _entry({'releases': <Object?>[]}),
    _entry({'releases': 'x'}),
    _entry({'releases': null}),
    _entry({'releases': <String, Object?>{}}),
    _entry(<String, Object?>{}),
    _entry({
      'releases': [good, 'x'],
    }),
    _entry({
      'releases': [good, null],
    }),
    _entry({
      'releases': [
        good,
        [good],
      ],
    }),
    _entry(<Object?>[]),
    _entry('nope'),
    _entry(null),
    _entry(5),
    _entry(true),
    _entry(_text('')),
    _entry(_text('{')),
    _entry(_text('{"releases":[}')),
    _entry(_text('$raw x')),
    _entry(_text(' \n$raw\n ')),
    _entry(_text('\ufeff$raw')),
    _entry(Uint8List.fromList([0xff, 0xfe])),
    _entry(Uint8List.fromList([...utf8.encode(raw), 0xe2, 0x82])),
    _entry(_text(const JsonEncoder.withIndent('  ').convert(_one(good)))),
    _entry(_text('{"releases":[$_rawRelease],"releases":[]}')),
    _entry(_text('{"releases":[],"releases":[$_rawRelease]}')),
    for (final size in [
      '1234.0',
      '1.234e3',
      '12340e-1',
      '-0',
      '1e400',
      '9007199254740993',
      '9223372036854775807',
      '9223372036854775808',
      '18446744073709551616',
    ])
      _entry(_text(raw.replaceFirst('"bytes":1234', '"bytes":$size'))),
    _entry(
      _text(
        raw.replaceFirst(
          '"url":"https://example.com/crates-1.0.0.opack"',
          '"url":"https://bad.local/a","url":"https://example.com/a"',
        ),
      ),
    ),
    for (final name in [
      ['brass-lamp', 'crates'],
      ['Brass', 'crates'],
      ['brass-lamp', 'Crates'],
      ['a b', 'crates'],
      ['brass-lamp', 'a b'],
      ['../x', 'crates'],
      ['brass-lamp', '../x'],
      ['', 'crates'],
      ['brass-lamp', ''],
      ['a' * 40, 'b' * 40],
      ['a' * 41, 'crates'],
      ['brass-lamp', 'b' * 41],
      ['a--b', 'crates'],
      ['am\u001bber', 'rocks'],
      ['a\n', 'b'],
    ])
      _entry(_one(good), name[0], name[1]),
    for (final length in [kitEntryLimit, kitEntryLimit + 1])
      _case(
        'entryPadded',
        [length, 'brass-lamp', 'crates'],
        () => _entryOut(
          _text(_padded(_one(good), length)),
          'brass-lamp',
          'crates',
        ),
      ),
    for (final day in _days)
      _case('date', [day], () {
        checkKitDate('released', day);
        return true;
      }),
  ];
}

final String _rawRelease = jsonEncode(_release('1.0.0'));

// ---- the index and release lists -----------------------------------------

Map<String, Object?> _row(
  String slug, {
  String publisher = 'brass-lamp',
  Map<String, Object?> change = const {},
  List<String> drop = const [],
}) {
  final row = <String, Object?>{
    'containsScripts': false,
    'engine': {'from': '0.30.0', 'to': '0.40.0'},
    'licence': {'spdx': 'MIT', 'url': 'https://example.com/l'},
    'name': 'Kit $slug',
    'publisher': publisher,
    'slug': slug,
    'summary': 'Some crates.',
    'tags': ['props'],
    'updated': '2026-10-19',
    'version': '1.0.0',
    ...change,
  };
  drop.forEach(row.remove);
  return row;
}

Map<String, Object?> _store(
  Object? kits, [
  Map<String, Object?> top = const {},
]) => {'formatVersion': 1, 'kits': kits, ...top};

Map<String, Object?> _indexOut(Uint8List bytes) => {
  'hex': hex(KitIndex.parse(bytes).encode()),
  'rows': [
    for (final row in KitIndex.parse(bytes).rows)
      '${row.manifest.publisher}/${row.manifest.slug}',
  ],
};

Map<String, Object?> _index(Object? json) {
  final bytes = _asBytes(json);
  return _case('index', [b64(bytes)], () => _indexOut(bytes));
}

List<Map<String, Object?>> _indexes() {
  final thumbs = [
    'kits/brass-lamp/crates/preview-0.png',
    'kits/brass-lamp/crates/preview-5.jpg',
    'kits/brass-lamp/crates/preview-3.jpeg',
    'kits/brass-lamp/crates/preview-1.webp',
    'kits/brass-lamp/other/preview-0.png',
    'kits/brass-lamp/crates/preview-6.png',
    'kits/brass-lamp/crates/preview-0.gif',
    'kits/brass-lamp/crates/preview-0.PNG',
    'kits/brass-lamp/crates/preview-0.png\n',
    'kits/brass-lamp/crates/preview-00.png',
    'kits/brass-lamp/crates/',
    'kits/brass-lamp/crates/preview-0.png/x',
    'kits/amber/crates/preview-0.png',
    '../x/preview-0.png',
    '/kits/brass-lamp/crates/preview-0.png',
    'preview-0.png',
    '',
    null,
    5,
    true,
  ];
  final rowChanges = <Map<String, Object?>>[
    {'name': 'Crates\u202e'},
    {'name': ''},
    {'name': '  '},
    {'name': 'a' * 60},
    {'name': 'a' * 61},
    {'name': 5},
    {'summary': 'a' * 200},
    {'summary': 'a' * 201},
    {'summary': ''},
    {'version': '1.0'},
    {'version': '1.0.0-x'},
    {'slug': 'Crates'},
    {'publisher': 'Brass'},
    {'slug': 'crates', 'publisher': 'amber'},
    {
      'licence': {'spdx': 'NONE', 'url': 'https://example.com/l'},
    },
    {
      'licence': {'spdx': 'MIT', 'url': 'ftp://example.com/l'},
    },
    {
      'licence': {'spdx': 'MIT', 'url': 'http://example.com/l'},
    },
    {
      'licence': {'spdx': 'MIT'},
    },
    {'licence': 'MIT'},
    {'licence': null},
    {
      'engine': {'from': '0.40.0', 'to': '0.30.0'},
    },
    {
      'engine': {'from': '0.30.0', 'to': '0.30.0'},
    },
    {
      'engine': {'from': 'x', 'to': '0.30.0'},
    },
    {
      'engine': {'from': '0.30.0'},
    },
    {'engine': 5},
    {'tags': <Object?>[]},
    {
      'tags': ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'],
    },
    {
      'tags': ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i'],
    },
    {
      'tags': ['a', 'a'],
    },
    {
      'tags': ['Props'],
    },
    {
      'tags': [5],
    },
    {
      'tags': [null],
    },
    {'tags': 'props'},
    {'tags': null},
    {'containsScripts': true},
    {'containsScripts': 'false'},
    {'containsScripts': 0},
    {'containsScripts': null},
    {'updated': '2026-02-30'},
    {'updated': '2026-2-3'},
    {'updated': 5},
    {'stars': 5},
    for (final thumbnail in thumbs) {'thumbnail': thumbnail},
  ];
  return [
    _index(_store(<Object?>[])),
    _index(_store([_row('crates')])),
    _index(
      _store([
        _row('zebra'),
        _row(
          'crates',
          change: {'thumbnail': 'kits/brass-lamp/crates/preview-0.png'},
        ),
        _row('apple', publisher: 'amber'),
      ]),
    ),
    _index(_store([_row('b'), _row('a'), _row('c', publisher: 'a')])),
    _index(
      _store([
        _row('a2'),
        _row('a10'),
        _row('a1'),
        _row('A', change: const {}),
      ]),
    ),
    for (final change in rowChanges)
      _index(_store([_row('crates', change: change)])),
    for (final key in _row('crates').keys)
      _index(
        _store([
          _row('crates', drop: [key]),
        ]),
      ),
    _index(_store([_row('crates'), _row('crates')])),
    _index(
      _store([
        _row('crates'),
        _row('crates', change: {'version': '2.0.0'}),
      ]),
    ),
    _index(_store([_row('crates'), 'x'])),
    _index(_store([_row('crates'), null])),
    _index(_store([_row('crates'), <Object?>[]])),
    for (final format in <Object?>[
      2,
      0,
      -1,
      1.0,
      1.5,
      '1',
      null,
      true,
      1 << 40,
    ])
      _index(_store([_row('crates')], {'formatVersion': format})),
    _index({'kits': <Object?>[]}),
    _index({'formatVersion': 1}),
    _index(_store('x')),
    _index(_store(null)),
    _index(_store(<String, Object?>{})),
    _index(_store([_row('crates')], {'extra': 1})),
    _index(<Object?>[]),
    _index('nope'),
    _index(_text('')),
    _index(_text('{')),
    _index(_text('\ufeff${jsonEncode(_store(<Object?>[]))}')),
    _index(Uint8List.fromList([0xff])),
    _index(_text('{"formatVersion":1,"formatVersion":2,"kits":[]}')),
    _index(_text('{"formatVersion":2,"formatVersion":1,"kits":[]}')),
  ];
}

Map<String, Object?> _published(
  String version, [
  Map<String, Object?> change = const {},
  List<String> drop = const [],
]) {
  final release = <String, Object?>{
    ..._release(version),
    'containsScripts': true,
    'engine': {'from': '0.30.0', 'to': '0.40.0'},
    ...change,
  };
  drop.forEach(release.remove);
  return release;
}

Map<String, Object?> _releasesFile(
  Object? releases, [
  Object? previews = const <Object?>[],
  Map<String, Object?> top = const {},
]) => {'formatVersion': 1, 'previews': previews, 'releases': releases, ...top};

Map<String, Object?> _releasesOut(Uint8List bytes) {
  final read = KitReleases.parse(bytes);
  return {
    'hex': hex(read.encode()),
    'previews': read.previews,
    'versions': [for (final item in read.releases) item.release.version],
  };
}

Map<String, Object?> _releases(Object? json) {
  final bytes = _asBytes(json);
  return _case('releases', [b64(bytes)], () => _releasesOut(bytes));
}

List<Map<String, Object?>> _releaseLists() {
  final one = [_published('1.0.0')];
  return [
    _releases(_releasesFile(one)),
    _releases(
      _releasesFile(
        [_published('1.2.0'), _published('1.10.0'), _published('1.9.0')],
        ['preview-0.png', 'preview-1.webp'],
      ),
    ),
    _releases(
      _releasesFile(one, [
        for (var i = 0; i < 6; i++)
          'preview-$i.${['png', 'jpg', 'jpeg', 'webp'][i % 4]}',
      ]),
    ),
    _releases(
      _releasesFile([
        for (var i = 0; i < 200; i++) _published('${i ~/ 100}.${i % 100}.0'),
      ]),
    ),
    _releases(
      _releasesFile([
        for (var i = 0; i < 201; i++) _published('${i ~/ 100}.${i % 100}.0'),
      ]),
    ),
    for (final previews in <Object?>[
      ['preview-0.png', 'preview-0.png'],
      ['preview-0.png', 'preview-0.jpg'],
      ['../a.png'],
      ['preview-0.gif'],
      ['preview-9.png'],
      ['preview-6.png'],
      ['a/preview-0.png'],
      ['preview-0.png\n'],
      ['Preview-0.png'],
      ['preview-0.PNG'],
      [''],
      [5],
      [null],
      ['preview-0.png', 5],
      'preview-0.png',
      null,
      <String, Object?>{},
      [for (var i = 0; i < 7; i++) 'preview-${i % 6}.png'],
    ])
      _releases(_releasesFile(one, previews)),
    _releases(_releasesFile(<Object?>[])),
    _releases(_releasesFile([_published('1.0.0'), _published('1.0.0')])),
    _releases(_releasesFile('x')),
    _releases(_releasesFile(null)),
    _releases(_releasesFile([_published('1.0.0'), 'x'])),
    _releases(_releasesFile([_published('1.0.0'), null])),
    for (final change in <Map<String, Object?>>[
      {'url': 'http://example.com/a'},
      {'url': 'https://localhost/a'},
      {'version': '1.0'},
      {'hash': 'abc'},
      {'bytes': 0},
      {'released': '2026-02-30'},
      {'notes': 'a\nb'},
      {
        'engine': {'from': '0.40.0', 'to': '0.30.0'},
      },
      {
        'engine': {'from': '0.30.0'},
      },
      {
        'engine': {'from': 5, 'to': '0.40.0'},
      },
      {'engine': 'x'},
      {'engine': null},
      {'containsScripts': false},
      {'containsScripts': 'true'},
      {'containsScripts': null},
      {'stars': 5},
    ])
      _releases(_releasesFile([_published('1.0.0', change)])),
    for (final key in [..._releaseKeys, 'engine', 'containsScripts'])
      _releases(
        _releasesFile([
          _published('1.0.0', const {}, [key]),
        ]),
      ),
    for (final format in <Object?>[2, 0, -1, 1.0, '1', null, true])
      _releases(
        _releasesFile(one, const <Object?>[], {'formatVersion': format}),
      ),
    _releases({'previews': <Object?>[], 'releases': one}),
    _releases({'formatVersion': 1, 'releases': one}),
    _releases({'formatVersion': 1, 'previews': <Object?>[]}),
    _releases(_releasesFile(one, const <Object?>[], {'extra': 1})),
    _releases(<Object?>[]),
    _releases(_text('{')),
    _releases(_text('')),
  ];
}

// ---- kits.lock -----------------------------------------------------------

Map<String, Object?> _locked({
  String publisher = 'brass-lamp',
  String slug = 'crate-pack',
  String version = '1.0.0',
  String into = 'assets/kits/crate-pack',
  Object? files = const {'assets/kits/crate-pack/a.png': 'b'},
  Map<String, Object?> change = const {},
  List<String> drop = const [],
}) {
  final kit = <String, Object?>{
    'files': files is Map
        ? {
            for (final e in files.entries)
              e.key: e.value == 'b' ? _hashB : e.value,
          }
        : files,
    'hash': _hash,
    'into': into,
    'publisher': publisher,
    'slug': slug,
    'version': version,
    ...change,
  };
  drop.forEach(kit.remove);
  return kit;
}

Map<String, Object?> _lockFile(
  Object? kits, [
  Map<String, Object?> top = const {},
]) => {'formatVersion': 1, 'kits': kits, ...top};

Map<String, Object?> _lockOut(Uint8List bytes) {
  final lock = KitLock.parse(bytes);
  return {
    'hex': hex(lock.encode()),
    'kits': [for (final kit in lock.kits) '${kit.publisher}/${kit.slug}'],
  };
}

Map<String, Object?> _lock(Object? json) {
  final bytes = _asBytes(json);
  return _case('lock', [b64(bytes)], () => _lockOut(bytes));
}

List<Map<String, Object?>> _locks() {
  final two = {
    'assets/kits/crate-pack/b.png': 'b',
    'assets/kits/crate-pack/a.png': _hash,
  };
  return [
    _lock(_lockFile(<Object?>[])),
    _lock(_lockFile([_locked()])),
    _lock(_lockFile([_locked(), _locked(slug: 'barrels')])),
    _lock(
      _lockFile([
        _locked(slug: 'zebra'),
        _locked(publisher: 'amber'),
        _locked(slug: 'apple'),
      ]),
    ),
    _lock(_lockFile([_locked(files: two)])),
    _lock(_lockFile([_locked(files: const <String, Object?>{})])),
    _lock(
      _lockFile([
        _locked(into: '', files: const {'a.png': 'b'}),
      ]),
    ),
    _lock(
      _lockFile([
        _locked(
          files: const {'z/y.png': 'b', 'Z/y.png': 'b', 'a.png': 'b'},
          into: '',
        ),
      ]),
    ),
    for (final into in [
      '.hidden',
      '/abs',
      'a//b',
      'a/',
      'con',
      'a b.',
      'a/.b',
      'kits.lock',
      'a' * 250,
      'caf\u00e9',
      'assets/ok',
    ])
      _lock(
        _lockFile([
          _locked(into: into, files: const {'x.png': 'b'}),
        ]),
      ),
    for (final change in <Map<String, Object?>>[
      {'publisher': 'Brass'},
      {'publisher': ''},
      {'publisher': 5},
      {'slug': 'Crate Pack'},
      {'slug': 'a' * 41},
      {'slug': null},
      {'version': '1.0'},
      {'version': 5},
      {'hash': 'xyz'},
      {'hash': _hash.toUpperCase()},
      {'hash': 5},
      {'hash': ''},
      {'into': 5},
      {'into': null},
      {'files': <Object?>[]},
      {'files': 'x'},
      {'files': null},
      {
        'files': {'a.png': 5},
      },
      {
        'files': {'a.png': null},
      },
      {
        'files': {'a.png': 'xyz'},
      },
      {
        'files': {'../a.png': 'b'},
      },
      {
        'files': {'a.png': _hashB.toUpperCase()},
      },
      {'note': 'hi'},
    ])
      _lock(_lockFile([_locked(change: change)])),
    for (final key in _locked().keys)
      _lock(
        _lockFile([
          _locked(drop: [key]),
        ]),
      ),
    _lock(_lockFile([_locked(), _locked()])),
    _lock(_lockFile([_locked(), 'x'])),
    _lock(_lockFile([_locked(), null])),
    for (final format in <Object?>[2, 0, -1, 1.0, '1', null, true])
      _lock(_lockFile([_locked()], {'formatVersion': format})),
    _lock({'kits': <Object?>[]}),
    _lock({'formatVersion': 1}),
    _lock(_lockFile('x')),
    _lock(_lockFile(null)),
    _lock(_lockFile([_locked()], {'note': 'hi'})),
    _lock(<Object?>[]),
    _lock('nope'),
    _lock(_text('')),
    _lock(_text('{')),
    _lock(_text('{"formatVersion":1}')),
    _lock(_text('\ufeff${jsonEncode(_lockFile(<Object?>[]))}')),
  ];
}

void main(List<String> args) {
  writeFixture(
    root: args.isEmpty ? '../..' : args.first,
    package: 'orblit_asset',
    name: 'kit_store',
    cases: [..._entries(), ..._indexes(), ..._releaseLists(), ..._locks()],
  );
}
