import 'dart:typed_data';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_progress/orblit_progress.dart';

/// A schema as JSON can say it: `["w", name]` or `["s", name, of, low, bits]`.
typedef _Spec = List<List<Object>>;

ProgressSchema _schema(int version, _Spec names) => ProgressSchema(
  version: version,
  names: [
    for (final n in names)
      if (n[0] == 'w')
        Whole(n[1] as String)
      else
        Slice(
          n[1] as String,
          of: n[2] as String,
          low: n[3] as int,
          bits: n[4] as int,
        ),
  ],
);

ProgressSchema? _tryBuild(int version, _Spec names) {
  try {
    return _schema(version, names);
  } on ArgumentError {
    return null;
  }
}

/// Values on both sides of every width a name can have.
const _probes = <int>[
  -1,
  0,
  1,
  2,
  7,
  8,
  255,
  256,
  0xFFFF,
  0xFFFFFFFF,
  0x100000000,
];

const _handmade = <(int, _Spec)>[
  (
    1,
    [
      ['w', 'a'],
      ['w', 'b'],
      ['s', 'b.low', 'b', 0, 4],
    ],
  ),
  (
    1,
    [
      ['s', 's', 'w', 8, 8],
      ['w', 'w'],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 'a', 'w', 0, 8],
      ['s', 'b', 'w', 8, 8],
    ],
  ),
  (
    7,
    [
      ['w', 'flags'],
      ['w', 'count'],
      ['s', 'flag.bottom', 'flags', 0, 1],
      ['s', 'flag.middle', 'flags', 4, 8],
      ['s', 'flag.top', 'flags', 31, 1],
    ],
  ),
  (
    1,
    [
      ['w', 'a'],
      ['w', 'b'],
      ['s', 'a.low', 'a', 0, 8],
      ['s', 'b.low', 'b', 0, 8],
    ],
  ),
  (
    65535,
    [
      ['w', 'a'],
    ],
  ),
  (1, []),
  // Refused.
  (
    1,
    [
      ['w', ''],
    ],
  ),
  (
    1,
    [
      ['w', 'a'],
      ['w', 'a'],
    ],
  ),
  (
    1,
    [
      ['w', 'a'],
      ['s', 'a', 'a', 0, 1],
    ],
  ),
  (
    1,
    [
      ['s', 's', 'w', 0, 1],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 's', 'w', 0, 4],
      ['s', 't', 's', 0, 1],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 's', 'w', 0, 0],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 's', 'w', -1, 1],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 's', 'w', 31, 2],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 's', 'w', 0, 33],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 'a', 'w', 0, 8],
      ['s', 'b', 'w', 7, 8],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 'a', 'w', 0, 32],
      ['s', 'b', 'w', 0, 1],
    ],
  ),
  (
    1,
    [
      ['w', 'w'],
      ['s', 'a', 'w', 0, 1],
      ['s', 'b', 'w', 0, 32],
    ],
  ),
  (0, []),
  (65536, []),
];

/// A small generator, so the fixture is the same on every run.
final class _Random {
  _Random(this._seed);

  int _seed;

  int next(int below) {
    _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
    return (_seed >> 8) % below;
  }
}

/// Schemas with slices that mostly fit and sometimes do not.
List<(int, _Spec)> _random(int count) {
  final random = _Random(2024);
  return [
    for (var i = 0; i < count; i++)
      (
        1 + random.next(3),
        [
          for (var w = 0; w <= random.next(4); w++) ['w', 'w$w'],
          for (var s = 0; s < random.next(5); s++)
            [
              's',
              's$s',
              random.next(12) == 0 ? 'nope' : 'w${random.next(2)}',
              random.next(34) - 1,
              random.next(34),
            ],
        ],
      ),
  ];
}

Map<String, Object?> _schemaCase(int version, _Spec names) {
  final schema = _tryBuild(version, names);
  return {
    'op': 'schema',
    'version': version,
    'names': names,
    'expect': schema == null
        ? null
        : {
            'slotCount': schema.slotCount,
            'places': [
              for (final n in names)
                [
                  n[1],
                  schema.placeOf(n[1] as String).slot,
                  schema.placeOf(n[1] as String).low,
                  schema.placeOf(n[1] as String).bits,
                ],
            ],
            'fits': [
              for (final n in names)
                [for (final v in _probes) schema.fits(n[1] as String, v)],
            ],
          },
  };
}

/// Writes that fit about half the time, in order, with what each one gave.
Map<String, Object?> _tableCase(
  int version,
  _Spec names,
  ProgressSchema schema,
  _Random random,
) {
  final steps = <List<Object>>[];
  final expect = <Map<String, Object?>>[];
  var table = ProgressTable(schema);
  for (var i = 0; i < 12; i++) {
    final name = names[random.next(names.length)][1] as String;
    final place = schema.placeOf(name);
    final room = (1 << place.bits) - 1;
    final value = random.next(3) == 0
        ? _probes[random.next(_probes.length)]
        : random.next(room < 0x7fffffff ? room + 1 : 0x7fffffff);
    steps.add([name, value]);
    final bool ok = schema.fits(name, value);
    if (ok) {
      final (table: next, :changes) = table.set(name, value);
      table = next;
      expect.add({
        'ok': true,
        'changes': hex(changes.encode()),
        'table': hex(table.encode()),
        'gets': [for (final n in names) table.get(n[1] as String)],
      });
    } else {
      expect.add({'ok': false});
    }
  }
  return {
    'op': 'table',
    'version': version,
    'names': names,
    'steps': steps,
    'expect': expect,
  };
}

Map<String, Object?> _decodeTableCase(int version, _Spec names, Uint8List b) {
  final schema = _schema(version, names);
  ProgressTable? table;
  try {
    table = ProgressTable.decode(schema, b);
  } on FormatException {
    table = null;
  }
  return {
    'op': 'decodeTable',
    'version': version,
    'names': names,
    'bytes': hex(b),
    'expect': table == null
        ? null
        : {
            'table': hex(table.encode()),
            'gets': [for (final n in names) table.get(n[1] as String)],
          },
  };
}

Map<String, Object?> _decodeChangesCase(int version, _Spec names, Uint8List b) {
  final changes = Changes.tryDecode(_schema(version, names), b);
  return {
    'op': 'decodeChanges',
    'version': version,
    'names': names,
    'bytes': hex(b),
    'expect': changes == null ? null : hex(changes.encode()),
  };
}

/// The bytes a table or a delta really is, then each way of spoiling them.
List<Uint8List> _spoiled(Uint8List good) => [
  good,
  good.sublist(0, good.length - 1),
  Uint8List.fromList([...good, 0]),
  Uint8List.fromList(good)..[0] = good[0] ^ 2,
  if (good.length > 2) Uint8List.fromList(good)..[2] = 9,
  if (good.length > 4) Uint8List.fromList(good)..[1] = 0xFF,
];

List<Map<String, Object?>> _decodes(_Random random) {
  final cases = <Map<String, Object?>>[];
  for (final (version, names) in _handmade.where(
    (c) => _tryBuild(c.$1, c.$2) != null && c.$2.isNotEmpty,
  )) {
    final schema = _schema(version, names);
    var table = ProgressTable(schema);
    final name = names.first[1] as String;
    final (table: next, :changes) = table.set(name, 1);
    table = next;
    // The same save under a later schema, and under an earlier one.
    final earlier = names.where((n) => n[0] == 'w').take(1).toList();
    final later = [
      ...names,
      ['w', 'added'],
    ];
    for (final bytes in _spoiled(table.encode())) {
      cases
        ..add(_decodeTableCase(version, names, bytes))
        ..add(
          _decodeTableCase(
            version == 65535 ? version : version + 1,
            later,
            bytes,
          ),
        )
        ..add(_decodeTableCase(version, earlier, bytes));
    }
    for (final bytes in _spoiled(changes.encode())) {
      cases.add(_decodeChangesCase(version, names, bytes));
    }
  }
  final schema = _handmade[3];
  for (var i = 0; i < 200; i++) {
    final bytes = Uint8List.fromList([
      for (var j = 0; j < random.next(30); j++) random.next(256),
    ]);
    cases
      ..add(_decodeTableCase(schema.$1, schema.$2, bytes))
      ..add(_decodeChangesCase(schema.$1, schema.$2, bytes));
  }
  return cases;
}

Map<String, Object?> _conditionCase(String op, Object? json) {
  Object? expect;
  try {
    expect = op == 'condition'
        ? Condition.fromJson(json).toJson()
        : Condition.listFromJson(json).map((c) => c.toJson()).toList();
  } on FormatException {
    expect = null;
  }
  return {'op': op, 'json': json, 'expect': expect};
}

Map<String, Object?> _entry(Object? name, Object? op, Object? value) => {
  'name': name,
  'op': op,
  'value': value,
};

List<Map<String, Object?>> _conditions() {
  final good = _entry('gold', '>', 1);
  final bad = <Object?>[
    null,
    'gold',
    <String, Object?>{},
    _entry('gold', '==', '1'),
    _entry('gold', '=>', 1),
    _entry('', '==', 1),
    _entry('gold', '==', -1),
    _entry('gold', '==', 0x100000000),
    _entry('gold', '==', 0xFFFFFFFF),
    _entry('gold', '==', 1.5),
    _entry('gold', '==', 1.0),
    _entry(1, '==', 1),
    _entry('gold', 1, 1),
    {'name': 'gold', 'op': '=='},
    {'op': '==', 'value': 1},
    good,
    for (final symbol in Compare.values.map((c) => c.symbol))
      _entry('gold', symbol, 0),
  ];
  return [
    for (final json in bad) _conditionCase('condition', json),
    for (final json in <Object?>[
      null,
      <Object?>[],
      [good],
      [good, good],
      [good, 1],
      [1],
      <String, Object?>{'a': 1},
      'gold',
    ])
      _conditionCase('conditionList', json),
    ..._holds(),
  ];
}

List<Map<String, Object?>> _holds() {
  final schema = ProgressSchema(
    version: 1,
    names: const [
      Whole('flags'),
      Whole('gold'),
      Slice('quest.cook.stage', of: 'flags', low: 0, bits: 4),
    ],
  );
  const tables = <(int, int)>[(0, 0), (2, 5), (15, 0xFFFFFFFF)];
  return [
    for (final compare in Compare.values)
      for (final want in const [0, 1, 5, 0xFFFFFFFF])
        for (final (stage, gold) in tables)
          for (final name in const ['quest.cook.stage', 'gold'])
            {
              'op': 'holds',
              'compare': compare.symbol,
              'name': name,
              'want': want,
              'stage': stage,
              'gold': gold,
              'expect': Condition(name, compare, want).holds(
                ProgressTable(
                  schema,
                ).set('quest.cook.stage', stage).table.set('gold', gold).table,
              ),
            },
  ];
}

/// Usage: `dart run bin/progress.dart <repository root>`.
void main(List<String> args) {
  final random = _Random(77);
  final schemas = [..._handmade, ..._random(120)];
  writeFixture(
    root: args.single,
    package: 'orblit_progress',
    name: 'progress',
    cases: [
      for (final (version, names) in schemas) _schemaCase(version, names),
      for (final (version, names) in schemas)
        if (_tryBuild(version, names) case final schema? when names.isNotEmpty)
          _tableCase(version, names, schema, random),
      ..._decodes(random),
      ..._conditions(),
    ],
  );
}
