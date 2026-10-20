import 'dart:convert';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_progress/orblit_progress.dart';
import 'package:orblit_quest/orblit_quest.dart';

typedef _Json = Map<String, Object?>;

String _hex(List<int> bytes) =>
    bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();

/// A rule broken in Dart is an [ArgumentError], which a [RangeError] is one
/// of. Anything else is a surprise and stops the export.
Map<String, Object?> _arg(Object? Function() body) {
  try {
    return {'value': body()};
  } on ArgumentError {
    return {'throws': true};
  }
}

_Json _file({
  Object? kind = 'orblit.quest',
  Object? version = 1,
  Object? key = 'lamp',
  Object? title = 'The Lamp',
  Object? stages,
}) => {
  'kind': ?kind,
  'formatVersion': ?version,
  'key': ?key,
  'title': ?title,
  'stages':
      stages ??
      [
        {'id': 'unknown', 'journal': ''},
        {'id': 'find', 'journal': 'Find the lamp.'},
      ],
};

/// What the reader says up to and including [after], or all of it. A JSON
/// syntax error and a printed map carry each language's own words after a
/// fixed lead, so those cases compare the lead.
String _cut(String message, String? after) {
  if (after == null) return message;
  final at = message.indexOf(after);
  return at < 0 ? message : message.substring(0, at + after.length);
}

const _syntax = 'This is not a quest file: ';
const _shown = ', not ';

/// Quest text as the reader sees it.
_Json _parse(String text, {String? cutAfter}) => {
  'op': 'parse',
  'text': text,
  'cutAfter': cutAfter,
  'expect': _read(text, cutAfter),
};

_Json _read(String text, String? cutAfter) {
  try {
    final quest = Quest.parse(text);
    return {
      'key': quest.key,
      'title': quest.title,
      'last': quest.last,
      'stages': [
        for (final s in quest.stages)
          [
            s.id,
            s.journal,
            [
              for (final c in s.requires) [c.name, c.compare.symbol, c.value],
            ],
          ],
      ],
      'encoded': quest.encode(),
    };
  } on QuestFormatException catch (error) {
    return {'throws': true, 'message': _cut(error.message, cutAfter)};
  }
}

_Json _raw(Object? json, {String? cutAfter}) =>
    _parse(jsonEncode(json), cutAfter: cutAfter);

List<_Json> _parses() {
  Map<String, Object?> stage(String id, String journal, [Object? requires]) => {
    'id': id,
    'journal': journal,
    'requires': ?requires,
  };
  Map<String, Object?> need(Object? name, Object? op, Object? value) => {
    'name': name,
    'op': op,
    'value': value,
  };
  return [
    _raw(_file()),
    _raw(_file(key: 'a-b_C9', title: 'T')),
    _raw(_file(title: 'Café   \u{1F52E} "quoted" \\ / \t\n\u0001 end')),
    _raw(
      _file(
        stages: [
          stage('a', 'é ✓'),
          stage('b', 'Light it.', [
            for (final op in ['==', '!=', '<', '<=', '>', '>='])
              need('gold', op, 4294967295),
          ]),
          stage('c', '', []),
          stage('d', '', null),
        ],
      ),
    ),
    _raw(_file(stages: [for (var i = 0; i < 40; i++) stage('s$i', 'Step $i')])),
    _raw({
      ..._file(),
      'extra': [1, 2.5, null],
    }),
    _parse(
      '{"kind":"x","kind":"orblit.quest","formatVersion":1,'
      '"key":"k","title":"T","stages":[{"id":"a","journal":""},'
      '{"id":"b","journal":""}]}',
    ),
    _parse(
      ' \n{ "kind" : "orblit.quest" , "formatVersion" : 1 ,\t"key":"k",'
      '"title":"T","stages":[{"id":"a","journal":""},{"id":"b",'
      '"journal":"\\u00e9\\ud83d\\udd2e\\n"}]}\n',
    ),
    // Not a quest file.
    for (final text in [
      '',
      'not json',
      '{',
      '{"kind":}',
      '[1,',
      '{"a":1}}',
      'nul',
      '"unterminated',
    ])
      _parse(text, cutAfter: _syntax),
    for (final text in ['[]', 'null', '"x"', '3', 'true', '{}', '[{}]'])
      _parse(text),
    _raw(_file(kind: 'orblit.clip')),
    _raw(_file(kind: null)),
    _raw(_file(kind: 5)),
    _raw(_file(kind: ['orblit.quest'])),
    // Format version.
    for (final v in [
      0,
      -1,
      2,
      1.0,
      1.5,
      '1',
      true,
      null,
      9223372036854775807,
      -9223372036854775808,
    ])
      _raw(_file(version: v)),
    _parse(
      '{"kind":"orblit.quest","formatVersion":99999999999999999999,'
      '"key":"k","title":"T","stages":[]}',
    ),
    _parse(
      '{"kind":"orblit.quest","formatVersion":1e0,"key":"k","title":"T",'
      '"stages":[]}',
    ),
    _raw({
      'kind': 'orblit.quest',
      'key': 'k',
      'title': 'T',
      'stages': [stage('a', ''), stage('b', '')],
    }),
    // A part missing or the wrong kind.
    _raw(_file(key: null)),
    _raw(_file(key: 3)),
    _raw(_file(title: null)),
    _raw(_file(title: 3)),
    _raw(_file(stages: 'none')),
    _raw(_file(stages: {'id': 'a'})),
    _raw({
      'kind': 'orblit.quest',
      'formatVersion': 1,
      'key': 'k',
      'title': 'T',
    }),
    _raw(_file(stages: [1])),
    _raw(_file(stages: [null])),
    _raw(_file(stages: [<Object?>[]])),
    _raw(
      _file(
        stages: [
          {'id': 'a'},
        ],
      ),
    ),
    _raw(
      _file(
        stages: [
          {'journal': ''},
        ],
      ),
    ),
    _raw(
      _file(
        stages: [
          {'id': 3, 'journal': ''},
          {'id': 'b', 'journal': ''},
        ],
      ),
    ),
    _raw(
      _file(
        stages: [
          {'id': 'a', 'journal': 3},
          {'id': 'b', 'journal': ''},
        ],
      ),
    ),
    _raw(_file(stages: [stage('a', ''), 1])),
    // A rule broken, with the text that says which.
    _raw(_file(key: 'a b')),
    _raw(_file(key: 'a.b')),
    _raw(_file(key: '')),
    _raw(_file(key: 'a\n')),
    _raw(_file(key: '\nb')),
    _raw(_file(key: 'é')),
    _raw(_file(key: 'ａ')),
    _raw(_file(key: 'a/b')),
    _raw(_file(title: '')),
    _raw(_file(stages: [])),
    _raw(_file(stages: [stage('a', '')])),
    _raw(_file(stages: [stage('same', ''), stage('same', 'x')])),
    _raw(_file(stages: [stage('a', ''), stage('b', ''), stage('a', '')])),
    _raw(_file(stages: [stage('', ''), stage('b', '')])),
    _raw(_file(stages: [stage('a', ''), stage('A', '')])),
    // A condition that is wrong: the message holds the reader's own text.
    for (final bad in <Object?>[
      'gold',
      <String, Object?>{},
      {'a': 1},
      [1],
      [null],
      [<String, Object?>{}],
      [need('', '>', 1)],
      [need(3, '>', 1)],
      [need('gold', '=>', 1)],
      [need('gold', null, 1)],
      [need('gold', '>', -1)],
      [need('gold', '>', 4294967296)],
      [need('gold', '>', 1.5)],
      [need('gold', '>', 1.0)],
      [need('gold', '>', '1')],
      [need('gold', '>', null)],
      [
        need('gold', '>', 1),
        {'name': 'gold'},
      ],
    ])
      _raw(
        _file(stages: [stage('a', '', bad), stage('b', '')]),
        cutAfter: _shown,
      ),
    _raw(
      _file(
        stages: [
          stage('a', '', [need('gold', '>', 0)]),
          stage('b', '', [need('gold', '<', 4294967295)]),
        ],
      ),
    ),
  ];
}

Compare _compare(String symbol) => Compare.bySymbol(symbol)!;

Quest _quest(_Json json) => Quest(
  key: json['key'] as String,
  title: json['title'] as String,
  stages: [
    for (final s in json['stages'] as List<Object?>)
      Stage(
        id: (s as _Json)['id'] as String,
        journal: s['journal'] as String,
        requires: [
          for (final r in s['requires'] as List<Object?>)
            Condition(
              (r as List<Object?>)[0] as String,
              _compare(r[1] as String),
              r[2] as int,
            ),
        ],
      ),
  ],
);

ProgressSchema _schema(List<List<Object>> names) => ProgressSchema(
  version: 1,
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

Object? _move(QuestMove move, String key) => switch (move) {
  Moved() => {
    'stage': move.table.get(questStageName(key)),
    'completed': move.completed,
    'back': move.back,
    'changes': _hex(move.changes.encode()),
    'table': _hex(move.table.encode()),
  },
  Refused() => {'refused': move.reason.name},
};

/// Every question and every move the book can be asked of one table.
_Json _ask(QuestBook book, ProgressTable table, String key, List<int> tos) {
  return {
    'stage': _arg(() => book.stageOf(table, key)),
    'started': _arg(() => book.isStarted(table, key)),
    'complete': _arg(() => book.isComplete(table, key)),
    'journal': _arg(() => book.journal(table, key)),
    'moves': [
      for (final to in tos)
        for (final back in [false, true])
          _arg(() => _move(book.advance(table, key, to, allowBack: back), key)),
    ],
  };
}

_Json _book(
  List<List<Object>> names,
  List<_Json> quests,
  List<List<List<Object>>> tables,
) {
  final schema = _schema(names);
  try {
    final book = QuestBook(
      schema: schema,
      quests: [for (final q in quests) _quest(q)],
    );
    return _bookCase(names, quests, tables, null, {
      'keys': [for (final q in book.quests) q.key],
      'tables': [for (final assigns in tables) _tabled(book, schema, assigns)],
    });
  } on ArgumentError catch (error) {
    // A repeated key prints the list of quests, which has no twin. A bad
    // condition value comes from orblit_progress, which checks its own text.
    final cut = switch (error.invalidValue) {
      List() => 'repeat a key',
      _ when error.name == 'value' => '',
      _ => null,
    };
    return _bookCase(names, quests, tables, cut, {
      'throws': true,
      'message': _cut(
        '${error.name} ${error.message} (${error.invalidValue}).',
        cut,
      ),
    });
  }
}

_Json _tabled(QuestBook book, ProgressSchema schema, List<List<Object>> sets) =>
    _arg(() {
      var table = ProgressTable(schema);
      for (final a in sets) {
        table = table.set(a[0] as String, a[1] as int).table;
      }
      return {
        'table': _hex(table.encode()),
        'asks': [
          for (final key in [...book.quests.map((q) => q.key), 'nope'])
            _ask(book, table, key, [
              for (
                var to = -1;
                to <= (key == 'nope' ? 1 : book.quest(key).last + 1);
                to++
              )
                to,
            ]),
        ],
      };
    });

_Json _bookCase(
  List<List<Object>> names,
  List<_Json> quests,
  List<List<List<Object>>> tables,
  String? cutAfter,
  _Json expect,
) => {
  'op': 'book',
  'names': names,
  'quests': quests,
  'tables': tables,
  'cutAfter': cutAfter,
  'expect': expect,
};

_Json _stageJson(
  String id, [
  String journal = '',
  List<List<Object>> need = const [],
]) => {'id': id, 'journal': journal, 'requires': need};

_Json _questJson(String key, List<_Json> stages) => {
  'key': key,
  'title': 'Quest $key',
  'stages': stages,
};

_Json _plain(String key, int count) =>
    _questJson(key, [for (var i = 0; i < count; i++) _stageJson('s$i')]);

const _flags = <List<Object>>[
  ['w', 'flags'],
  ['w', 'gold'],
  ['s', 'quest.lamp.stage', 'flags', 0, 2],
  ['s', 'quest.well.stage', 'flags', 2, 3],
];

List<List<List<Object>>> _tables(
  String stageName,
  List<int> stages,
  List<int> golds,
) => [
  for (final s in stages)
    for (final g in golds)
      [
        [stageName, s],
        ['gold', g],
      ],
];

List<_Json> _books() {
  final lamp = _questJson('lamp', [
    _stageJson('unknown'),
    _stageJson('find', 'Find the lamp.'),
    _stageJson('light', 'Light the lamp.', [
      ['gold', '>=', 5],
    ]),
  ]);
  final well = _questJson('well', [
    for (var i = 0; i < 6; i++)
      _stageJson('w$i', 'Well $i', [
        if (i == 2) ['gold', '>', 0],
        if (i == 4) ['quest.lamp.stage', '==', 2],
        if (i == 5) ['quest.well.stage', '!=', 1],
      ]),
  ]);
  final both = _tables('quest.lamp.stage', [0, 1, 2, 3], [0, 4, 5, 6]);
  final wells = [
    for (final w in [0, 1, 2, 3, 4, 5, 6, 7])
      for (final l in [0, 2])
        [
          ['quest.well.stage', w],
          ['quest.lamp.stage', l],
          ['gold', 1],
        ],
  ];
  return [
    _book(_flags, [lamp], both),
    _book(_flags, [lamp, well], [...both, ...wells]),
    _book(_flags, [well, lamp], wells),
    _book(_flags, [], [[]]),
    // A table value too big for the stage slice is a table that cannot be made.
    _book(
      _flags,
      [lamp],
      [
        [
          ['quest.lamp.stage', 4],
        ],
        [
          ['quest.lamp.stage', 3],
        ],
        [
          ['nope', 1],
        ],
      ],
    ),
    // A requirement of every kind, on a slice and on a whole.
    for (final op in ['==', '!=', '<', '<=', '>', '>='])
      _book(
        _flags,
        [
          _questJson('lamp', [
            _stageJson('a'),
            _stageJson('b', '', [
              ['gold', op, 5],
            ]),
            _stageJson('c', '', [
              ['quest.well.stage', op, 3],
              ['gold', op, 5],
            ]),
          ]),
        ],
        [
          for (final w in [2, 3, 4])
            for (final g in [4, 5, 6])
              for (final s in [0, 1])
                [
                  ['quest.lamp.stage', s],
                  ['quest.well.stage', w],
                  ['gold', g],
                ],
        ],
      ),
    // Four stages fit two bits and five do not. Eight fit three and nine do not.
    _book(
      _flags,
      [_plain('lamp', 4)],
      [
        [
          ['quest.lamp.stage', 3],
        ],
      ],
    ),
    _book(_flags, [_plain('lamp', 5)], []),
    _book(
      _flags,
      [_plain('well', 8)],
      [
        [
          ['quest.well.stage', 7],
        ],
        [
          ['quest.well.stage', 5],
        ],
      ],
    ),
    _book(_flags, [_plain('well', 9)], []),
    // A value that is a whole by itself has a stage up to 0xFFFFFFFF.
    _book(
      [
        ['w', 'quest.big.stage'],
        ['w', 'gold'],
      ],
      [_plain('big', 70)],
      [
        [
          ['quest.big.stage', 0],
        ],
        [
          ['quest.big.stage', 69],
        ],
        [
          ['quest.big.stage', 70],
        ],
        [
          ['quest.big.stage', 4294967295],
        ],
      ],
    ),
    // Refused when the book is made.
    _book(_flags, [_plain('ghost', 2)], []),
    _book(_flags, [_plain('lamp', 2), _plain('lamp', 2)], []),
    _book(_flags, [
      _plain('lamp', 2),
      _plain('well', 2),
      _plain('lamp', 3),
    ], []),
    _book(_flags, [_plain('lamp', 2), _plain('ghost', 2)], []),
    _book(_flags, [
      _questJson('lamp', [
        _stageJson('a'),
        _stageJson('b', '', [
          ['nope', '==', 1],
        ]),
      ]),
    ], []),
    _book(_flags, [
      _questJson('lamp', [
        _stageJson('a'),
        _stageJson('b', '', [
          ['quest.well.stage', '==', 8],
        ]),
      ]),
    ], []),
    _book(
      _flags,
      [
        _questJson('lamp', [
          _stageJson('a'),
          _stageJson('b', '', [
            ['quest.well.stage', '==', 7],
          ]),
        ]),
      ],
      [
        [
          ['quest.well.stage', 7],
        ],
      ],
    ),
    _book(_flags, [
      _questJson('lamp', [
        _stageJson('a'),
        _stageJson('b', '', [
          ['gold', '==', 4294967295],
        ]),
      ]),
    ], []),
    _book(_flags, [
      _questJson('lamp', [
        _stageJson('a'),
        _stageJson('b', '', [
          ['gold', '==', 4294967296],
        ]),
      ]),
    ], []),
    _book(_flags, [
      _questJson('lamp', [
        _stageJson('a'),
        _stageJson('b', '', [
          ['gold', '==', -1],
        ]),
      ]),
    ], []),
    _book(_flags, [
      _questJson('bad key', [_stageJson('a'), _stageJson('b')]),
    ], []),
  ];
}

List<_Json> _names() => [
  for (final key in ['lamp', '', 'a b', 'é', 'a.b', 'quest.x.stage'])
    {'op': 'name', 'key': key, 'expect': questStageName(key)},
];

/// Usage: `dart run bin/quest.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_quest',
    name: 'quest',
    cases: [..._parses(), ..._books(), ..._names()],
  );
}
