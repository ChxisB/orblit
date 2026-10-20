// Usage: `dart run bin/save.dart <repository root>`
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_save/orblit_save.dart';
import 'package:orblit_save/src/store.dart' show checkKey;

/// The generator the other exporters use, so a case list is the same on every
/// run.
final class _Rng {
  _Rng(this._seed);

  int _seed;

  int next(int below) {
    _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
    return (_seed >> 8) % below;
  }

  T pick<T>(List<T> from) => from[next(from.length)];

  bool chance(int outOf) => next(outOf) == 0;
}

/// The random numbers of an autosave, from the same generator on both sides.
/// It records every bound it was asked for, so a fixture checks the draws
/// as well as the picks.
final class _Scripted implements Random {
  _Scripted(int seed) : _rng = _Rng(seed);

  final _Rng _rng;
  final List<int> draws = [];

  @override
  int nextInt(int max) {
    draws.add(max);
    return _rng.next(max);
  }

  @override
  double nextDouble() => _rng.next(1000) / 1000;

  @override
  bool nextBool() => _rng.chance(2);
}

List<int> _bytes(_Rng rng, int most) => [
  for (var i = rng.next(most + 1); i > 0; i--) rng.next(256),
];

/// Text as UTF-16 code units, so a lone surrogate survives the fixture.
List<int> _units(String text) => text.codeUnits;

const _kinds = <String>[
  'autosave',
  'leave',
  'disconnect',
  'shutdown',
  'error',
  'custom:pre-restore',
];

/// Characters a custom reason may meet: JSON escapes, control characters,
/// non-ASCII, a pair and a lone surrogate.
const _awkward = <int>[
  0x61,
  0x20,
  0x22,
  0x5C,
  0x2F,
  0x08,
  0x09,
  0x0C,
  0x0D,
  0x00,
  0x01,
  0x1F,
  0x7F,
  0xE9,
  0x2028,
  0xD83D,
  0xDE00,
  0xD800,
  0xDC00,
  0xFFFF,
];

String _kind(SaveReason reason) => switch (reason) {
  Autosave() => 'autosave',
  Leave() => 'leave',
  Disconnect() => 'disconnect',
  Shutdown() => 'shutdown',
  Failed() => 'failed',
  Custom() => 'custom',
};

Map<String, Object?> _parseCase(String label) {
  final c = <String, Object?>{'op': 'reasonParse', 'label': _units(label)};
  try {
    final reason = SaveReason.parse(label);
    c['result'] = 'ok';
    c['kind'] = _kind(reason);
    c['text'] = reason is Custom ? _units(reason.text) : null;
    c['roundTrip'] = _units(reason.label);
  } on FormatException {
    c['result'] = 'format';
  } on ArgumentError {
    c['result'] = 'argument';
  }
  return c;
}

Map<String, Object?> _customCase(String text) {
  final c = <String, Object?>{'op': 'reasonCustom', 'text': _units(text)};
  try {
    c['label'] = _units(Custom(text).label);
    c['result'] = 'ok';
  } on ArgumentError {
    c['result'] = 'argument';
  }
  return c;
}

List<Map<String, Object?>> _reasonCases(_Rng rng) {
  final labels = <String>[
    ..._kinds,
    '',
    'nap',
    'AUTOSAVE',
    'autosave ',
    'custom',
    'custom:',
    'custom:a',
    'custom:${'a' * 64}',
    'custom:${'a' * 65}',
    'custom:a\nb',
    'custom:\n',
    'custom: ',
    'Custom:x',
    'custom:custom:x',
    'error:x',
  ];
  final texts = <String>['x', 'a b', 'ünï', '😀', 'é' * 64, 'é' * 65];
  for (var i = 0; i < 150; i++) {
    final text = String.fromCharCodes([
      for (var n = rng.next(70); n >= 0; n--) rng.pick(_awkward),
    ]);
    labels.add('custom:$text');
    texts.add(text);
  }
  return [
    for (final label in labels) _parseCase(label),
    for (final text in texts) _customCase(text),
  ];
}

const _keyChars =
    'abcxyzABCXYZ019_-.'
    r'/\ :é';

List<Map<String, Object?>> _keyCases(_Rng rng) {
  final keys = <String>[
    '',
    'a',
    'ana',
    'bo.2',
    '.hidden',
    '..',
    '../x',
    'a/b',
    r'a\b',
    'a b',
    'a\n',
    '\na',
    'a\u0000',
    '-x',
    '_x',
    'x' * 127,
    'x' * 128,
    'x' * 129,
    'é',
    'a.b.c',
    'Zz09_-.',
    'a ',
    '٣',
  ];
  for (var i = 0; i < 250; i++) {
    keys.add(
      String.fromCharCodes([
        for (var n = rng.next(8); n >= 0; n--)
          _keyChars.codeUnitAt(
            rng.chance(8) ? rng.next(_keyChars.length) : rng.next(18),
          ),
      ]),
    );
  }
  return [
    for (final key in keys)
      <String, Object?>{
        'op': 'key',
        'key': _units(key),
        'result': _keyResult(key),
      },
  ];
}

String _keyResult(String key) {
  try {
    checkKey(key);
    return 'ok';
  } on ArgumentError {
    return 'argument';
  }
}

SaveRecord _record(List<int> bytes, SaveReason reason, int at) => SaveRecord(
  bytes: Uint8List.fromList(bytes),
  reason: reason,
  at: DateTime.fromMillisecondsSinceEpoch(at, isUtc: true),
);

SaveReason _reasonOf(_Rng rng) {
  final label = rng.pick(_kinds);
  if (!rng.chance(3)) return SaveReason.parse(label);
  return Custom(
    String.fromCharCodes([
      for (var n = rng.next(20); n >= 0; n--) rng.pick(_awkward),
    ]).replaceAll('\n', ' '),
  );
}

/// An instant that both runtimes can hold: years 1 to 9999.
int _instant(_Rng rng) => switch (rng.next(6)) {
  0 => 0,
  1 => -rng.next(1000000000),
  2 => 253402300799999 - rng.next(1000),
  3 => -62135596800000 + rng.next(1000),
  _ => rng.next(2000000000) * 1000 + rng.next(1000),
};

String _key(_Rng rng) => 'p${rng.next(100000)}${rng.pick(['', '.x', '-y'])}';

Future<List<Map<String, Object?>>> _fileCases(_Rng rng, Directory root) async {
  final cases = <Map<String, Object?>>[];
  final store = FileSaveStore('${root.path}/saves');
  for (var i = 0; i < 60; i++) {
    final key = _key(rng);
    final bytes = _bytes(rng, i < 6 ? 0 : 120);
    final reason = i < 6 ? SaveReason.parse(_kinds[i]) : _reasonOf(rng);
    final at = _instant(rng);
    await store.write(key, _record(bytes, reason, at));
    final file = File('${root.path}/saves/$key.save');
    cases.add({
      'op': 'file',
      'key': key,
      'bytes': hex(bytes),
      'reason': _units(reason.label),
      'at': at,
      'file': hex(file.readAsBytesSync()),
    });
    file.deleteSync();
  }
  return cases;
}

List<int> _framed(
  String header,
  List<int> payload, {
  int? length,
  List<int> magic = const [0x4F, 0x53, 0x41, 0x56],
}) {
  final text = utf8.encode(header);
  final out = Uint8List(8 + text.length + payload.length);
  out.setRange(0, 4, magic);
  ByteData.sublistView(out).setUint32(4, length ?? text.length, Endian.little);
  out.setRange(8, 8 + text.length, text);
  out.setRange(8 + text.length, out.length, payload);
  return out;
}

const _okHeader = '{"format":1,"reason":"autosave","at":5}';

/// Headers a damaged or foreign file might hold, as JSON text.
const _headers = <String>[
  _okHeader,
  '{"extra":[1,2,{"a":null}],"at":5,"reason":"leave","format":1}',
  ' { "format" : 1 , "reason" : "shutdown" , "at" : 7 } ',
  '{"format":1.0,"reason":"autosave","at":5}',
  '{"format":1e0,"reason":"autosave","at":5}',
  '{"format":2,"reason":"autosave","at":5}',
  '{"format":"1","reason":"autosave","at":5}',
  '{"format":true,"reason":"autosave","at":5}',
  '{"format":null,"reason":"autosave","at":5}',
  '{"reason":"autosave","at":5}',
  '{"format":1,"at":5}',
  '{"format":1,"reason":"autosave"}',
  '{"format":1,"reason":"autosave","at":5.0}',
  '{"format":1,"reason":"autosave","at":5e0}',
  '{"format":1,"reason":"autosave","at":5.5}',
  '{"format":1,"reason":"autosave","at":"5"}',
  '{"format":1,"reason":"autosave","at":null}',
  '{"format":1,"reason":"autosave","at":-3}',
  '{"format":1,"reason":"autosave","at":0}',
  '{"format":1,"reason":"autosave","at":253402300799999}',
  '{"format":1,"reason":5,"at":5}',
  '{"format":1,"reason":null,"at":5}',
  '{"format":1,"reason":"nap","at":5}',
  '{"format":1,"reason":"custom:ok","at":5}',
  '{"format":1,"reason":"custom:","at":5}',
  r'{"format":1,"reason":"custom:a\nb","at":5}',
  r'{"format":1,"reason":"custom:a\u0000b\"\\\/","at":5}',
  '[]',
  '{}',
  'null',
  '"x"',
  '5',
  '',
  '{',
  '{"format":1,"reason":"autosave","at":5} x',
  '{"format":1,"format":2,"reason":"leave","at":1}',
  '{"format":2,"format":1,"reason":"leave","at":1}',
  '{"format":1,"reason":"leave","reason":"nap","at":1}',
];

String _classify(String message) {
  if (message.contains('not a save file')) return 'notSave';
  if (message.contains('cut short')) return 'cutShort';
  if (message.contains('not one this version wrote')) return 'header';
  if (message.contains('Not a save reason')) return 'reasonLabel';
  return 'json';
}

Future<Map<String, Object?>> _readCase(
  Directory root,
  List<int> file,
  String note,
) async {
  final c = <String, Object?>{
    'op': 'fileRead',
    'note': note,
    'file': hex(file),
  };
  final path = File('${root.path}/saves/ana.save');
  path.parent.createSync(recursive: true);
  path.writeAsBytesSync(file);
  try {
    final record = (await FileSaveStore('${root.path}/saves').read('ana'))!;
    c['result'] = 'ok';
    c['bytes'] = hex(record.bytes);
    c['reason'] = _units(record.reason.label);
    c['at'] = record.at.millisecondsSinceEpoch;
  } on FormatException catch (e) {
    c['result'] = _classify(e.message);
  } on ArgumentError {
    c['result'] = 'argument';
  }
  path.deleteSync();
  return c;
}

Future<List<Map<String, Object?>>> _fileReadCases(Directory root) async {
  final payload = [1, 2, 3, 250];
  final whole = _framed(_okHeader, payload);
  final inputs = <(String, List<int>)>[
    for (final header in _headers) ('header $header', _framed(header, payload)),
    for (var n = 0; n <= whole.length; n++)
      ('cut at $n', whole.sublist(0, n)),
    ('wrong magic', _framed(_okHeader, payload, magic: [0x58, 0x53, 0x41, 0x56])),
    ('length past the end', _framed(_okHeader, payload, length: 9999)),
    ('length at the top', _framed(_okHeader, payload, length: 0xFFFFFFFF)),
    ('length zero', _framed(_okHeader, payload, length: 0)),
    ('length one short', _framed(_okHeader, payload, length: 38)),
    ('header that is not UTF-8', [0x4F, 0x53, 0x41, 0x56, 2, 0, 0, 0, 0xFF, 0xFE]),
    ('empty payload', _framed(_okHeader, const [])),
    ('long payload', _framed(_okHeader, List.filled(300, 9))),
    ('magic and zero length', [0x4F, 0x53, 0x41, 0x56, 0, 0, 0, 0]),
  ];
  return [for (final (note, bytes) in inputs) await _readCase(root, bytes, note)];
}

Future<List<Map<String, Object?>>> _recordCases(_Rng rng, Directory root) async {
  final cases = <Map<String, Object?>>[];
  for (var i = 0; i < 60; i++) {
    final dir = Directory('${root.path}/h$i');
    final key = _key(rng);
    final bytes = _bytes(rng, i < 4 ? 0 : 200);
    final reason = _reasonOf(rng);
    final at = _instant(rng);
    await SaveHistory('${dir.path}/history').record(
      key,
      _record(bytes, reason, at),
    );
    final entry = Directory(
      '${dir.path}/history/entries/$key',
    ).listSync().whereType<File>().single;
    final blob = Directory(
      '${dir.path}/history/blobs',
    ).listSync().whereType<File>().single;
    final packed = blob.readAsBytesSync();
    cases.add({
      'op': 'record',
      'key': key,
      'bytes': hex(bytes),
      'reason': _units(reason.label),
      'at': at,
      'entryName': entry.uri.pathSegments.last,
      'entryText': entry.readAsStringSync(),
      'blobName': blob.uri.pathSegments.last,
      'blob': hex(packed),
      'plain': hex(gzip.decode(packed)),
    });
    dir.deleteSync(recursive: true);
  }
  return cases;
}

Future<List<Map<String, Object?>>> _entryCases(Directory root) async {
  final texts = <String>[
    for (final header in _headers)
      header.replaceAll('"at":5', '"at":5,"hash":"${'ab' * 32}"'),
    '{"format":1,"at":5,"reason":"autosave","hash":5}',
    '{"format":1,"at":5,"reason":"autosave","hash":null}',
    '{"format":1,"at":5,"reason":"autosave","hash":"abc"}',
    '{"format":1,"at":5,"reason":"autosave","hash":""}',
  ];
  final cases = <Map<String, Object?>>[];
  final history = SaveHistory('${root.path}/history');
  final dir = Directory('${root.path}/history/entries/ana')
    ..createSync(recursive: true);
  for (final text in texts) {
    final file = File('${dir.path}/1000-aaaaaaaaaaaa.json')
      ..writeAsStringSync(text);
    final c = <String, Object?>{'op': 'entry', 'text': text};
    try {
      final entry = (await history.list('ana')).single;
      c['result'] = 'ok';
      c['at'] = entry.at.millisecondsSinceEpoch;
      c['reason'] = _units(entry.reason.label);
      c['hash'] = entry.hash;
    } on FormatException {
      c['result'] = 'format';
    } on ArgumentError {
      c['result'] = 'argument';
    }
    cases.add(c);
    file.deleteSync();
  }
  return cases;
}

List<Map<String, Object?>> _spacingCases() {
  const day = Duration.millisecondsPerDay;
  const hour = Duration.millisecondsPerHour;
  final ages = <int>[
    for (final edge in [day, 7 * day, 30 * day]) ...[edge - 1, edge, edge + 1],
    0,
    1,
    hour,
    23 * hour,
    -1,
    -day,
    -100 * day,
    31 * day,
    365 * day,
    100000000000000,
  ];
  return [
    for (final age in ages)
      {
        'op': 'spacing',
        'age': age,
        'gap': defaultSpacing(Duration(milliseconds: age))?.inMilliseconds,
      },
  ];
}

const _pool = <String>[
  'a',
  'B',
  '_x',
  'a.b',
  'a-b',
  'Z9',
  'p1',
  'p10',
  'p2',
  'é',
  'ab',
  'aB',
  'A',
];

Map<String, Object?> _autosaveCase(
  _Rng rng, {
  required int seed,
  required int perTick,
  required int every,
  required int jitter,
  required int ticks,
  required int keys,
  required int churn,
}) {
  final names = [..._pool, for (var i = 0; i < keys; i++) 'k$i'];
  final random = _Scripted(seed);
  final auto = AutoSave(
    perTick: perTick,
    every: every,
    jitter: jitter,
    random: random,
  );
  final lives = <List<String>>[];
  final claims = <List<String>>[];
  final at = <int>[];
  var tick = 0;
  for (var n = 0; n < ticks; n++) {
    tick += 1 + (rng.chance(5) ? rng.next(3) : 0);
    final live = [
      for (final name in names)
        if (!rng.chance(churn)) ...[name, if (rng.chance(12)) name],
    ]..shuffle(Random(rng.next(1000)));
    at.add(tick);
    lives.add(live);
    claims.add(auto.claim(live, tick));
  }
  return {
    'op': 'autosave',
    'seed': seed,
    'perTick': perTick,
    'every': every,
    'jitter': jitter,
    'ticks': at,
    'live': lives,
    'claims': claims,
    'draws': random.draws,
  };
}

List<Map<String, Object?>> _autosaveCases(_Rng rng) {
  final cases = <Map<String, Object?>>[];
  var seed = 1;
  for (final (perTick, every, jitter, ticks, keys, churn) in [
    (3, 20, 0, 80, 5, 8),
    (3, 20, 6, 80, 5, 8),
    (1, 1, 0, 30, 4, 20),
    (1000, 30, 10, 60, 20, 20),
    (2, 7, 3, 120, 30, 3),
    (10, 50, 5, 120, 40, 2),
    (4, 100, 0, 100, 25, 4),
    (100, 5, 5, 60, 10, 6),
    (1, 12, 12, 100, 10, 3),
    (5, 2, 1, 100, 20, 4),
  ]) {
    cases.add(
      _autosaveCase(
        rng,
        seed: seed++,
        perTick: perTick,
        every: every,
        jitter: jitter,
        ticks: ticks,
        keys: keys,
        churn: churn,
      ),
    );
  }
  for (final (perTick, every, jitter) in [
    (0, 1, 0),
    (-1, 1, 0),
    (1, 0, 0),
    (1, -4, 0),
    (1, 1, -1),
    (1, 1, 0),
    (1, 1, 1),
  ]) {
    final c = <String, Object?>{
      'op': 'autosaveRefuse',
      'perTick': perTick,
      'every': every,
      'jitter': jitter,
    };
    try {
      AutoSave(
        perTick: perTick,
        every: every,
        jitter: jitter,
        random: _Scripted(1),
      );
      c['result'] = 'ok';
    } on ArgumentError {
      c['result'] = 'argument';
    }
    cases.add(c);
  }
  return cases;
}

Future<void> main(List<String> args) async {
  final root = args.single;
  final rng = _Rng(2026);
  final scratch = Directory.systemTemp.createTempSync('orblit_save_parity');
  try {
    writeFixture(
      root: root,
      package: 'orblit_save',
      name: 'save',
      cases: [
        ..._reasonCases(rng),
        ..._keyCases(rng),
        ...await _fileCases(rng, scratch),
        ...await _fileReadCases(scratch),
        ...await _recordCases(rng, scratch),
        ...await _entryCases(scratch),
        ..._spacingCases(),
        ..._autosaveCases(rng),
      ],
    );
  } finally {
    scratch.deleteSync(recursive: true);
  }
}
