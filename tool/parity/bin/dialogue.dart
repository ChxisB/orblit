import 'dart:convert';

import 'package:orblit_dialogue/orblit_dialogue.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_progress/orblit_progress.dart';

typedef _Json = Map<String, Object?>;

/// A rule broken in Dart is an [ArgumentError], which a [RangeError] is one
/// of. Anything else is a surprise and stops the export.
_Json _arg(Object? Function() body) {
  try {
    return {'value': body()};
  } on ArgumentError {
    return {'throws': true};
  }
}

/// A refused argument with the rule it broke, for the errors this package writes.
_Json _known(Object? Function() body) {
  try {
    return {'value': body()};
  } on ArgumentError catch (error) {
    return {
      'throws': true,
      'message': '${error.name} ${error.message} (${error.invalidValue}).',
    };
  }
}

/// How a call failed, in words both languages share.
String _kind(Object error) => switch (error) {
  RangeError() => 'range',
  ArgumentError() => 'arg',
  StateError() => 'state',
  _ => 'other',
};

String? _failure(void Function() body) {
  try {
    body();
    return null;
  } on Object catch (error) {
    return _kind(error);
  }
}

/// What the reader says up to and including [after], or all of it. A JSON
/// syntax error and a printed condition carry each language's own words after
/// a fixed lead, so those cases compare the lead.
String _cut(String message, String? after) {
  if (after == null) return message;
  final at = message.indexOf(after);
  return at < 0 ? message : message.substring(0, at + after.length);
}

const _syntax = 'This is not a dialogue file: ';
const _shown = ', not ';

_Json _file({
  Object? kind = 'orblit.dialogue',
  Object? version = 1,
  Object? key = 'guide',
  Object? variants,
  Object? nodes,
}) => {
  'kind': ?kind,
  'formatVersion': ?version,
  'key': ?key,
  'variants':
      variants ??
      [
        {'start': 'a'},
      ],
  'nodes': nodes ?? {'a': _line()},
};

_Json _without(_Json json, String field) => {...json}..remove(field);

_Json _line({
  Object? speaker = 'X',
  Object? text = 'Hi',
  Object? next,
  Object? before,
}) => {
  'type': 'line',
  'speaker': ?speaker,
  'text': ?text,
  'next': ?next,
  'before': ?before,
};

_Json _choice(Object? options, {Object? before}) => {
  'type': 'choice',
  'options': ?options,
  'before': ?before,
};

_Json _option({Object? text = 'Ok', Object? next, Object? when}) => {
  'text': ?text,
  'next': ?next,
  'when': ?when,
};

_Json _set(Object? writes, {Object? next, Object? before}) => {
  'type': 'set',
  'writes': ?writes,
  'next': ?next,
  'before': ?before,
};

_Json _jump(Object? next, {Object? before}) => {
  'type': 'jump',
  'next': ?next,
  'before': ?before,
};

_Json _write(Object? name, Object? value) => {'name': ?name, 'value': ?value};

_Json _need(Object? name, Object? op, Object? value) => {
  'name': name,
  'op': op,
  'value': value,
};

_Json _start(String start, [Object? when]) => {'start': start, 'when': ?when};

/// Dialogue text as the reader sees it.
_Json _parse(String text, {String? cutAfter}) => {
  'op': 'parse',
  'text': text,
  'cutAfter': cutAfter,
  'expect': _read(text, cutAfter),
};

_Json _read(String text, String? cutAfter) {
  try {
    final d = Dialogue.parse(text);
    return {
      'key': d.key,
      'variants': [
        for (final v in d.variants)
          [
            v.start,
            [
              for (final c in v.when) [c.name, c.compare.symbol, c.value],
            ],
          ],
      ],
      'nodes': d.nodes.keys.toList(),
      'beforeNames': d.beforeNames.toList(),
      'encoded': d.encode(),
      'stable': Dialogue.parse(d.encode()).encode() == d.encode(),
    };
  } on DialogueFormatException catch (error) {
    return {'throws': true, 'message': _cut(error.message, cutAfter)};
  }
}

_Json _raw(Object? json, {String? cutAfter}) =>
    _parse(jsonEncode(json), cutAfter: cutAfter);

List<_Json> _parses() {
  _Json withNodes(Object? nodes) => _file(nodes: nodes);
  _Json withNode(Object? node) => _file(nodes: {'a': node});
  return [
    _raw(_file()),
    _raw(_file(key: 'a b.c/é')),
    _raw(
      _file(
        variants: [
          _start('b', [
            for (final op in ['==', '!=', '<', '<=', '>', '>='])
              _need('gold', op, 4294967295),
          ]),
          _start('a'),
        ],
        nodes: {
          'a': _line(
            speaker: 'Café \u{1F52E}',
            text: 'Say "hi" \\ / \t\n\u0001 end',
            next: 'c',
            before: 'take',
          ),
          'b': _jump('a', before: 'id'),
          'c': _choice([
            _option(text: 'Yes', next: 'd', when: [_need('gold', '>=', 1)]),
            _option(text: 'No'),
            _option(text: 'Maybe', next: 'e', when: []),
          ], before: 'id'),
          'd': _set([_write('gold', 0), _write('gold', 4294967295)], next: 'e'),
          'e': _line(next: null),
        },
      ),
    ),
    _raw({
      ..._file(),
      'extra': [1, 2.5, null],
    }),
    _raw(
      _file(
        variants: [_start('n0')],
        nodes: {
          for (var i = 0; i < 40; i++)
            'n$i': _line(text: 'Line $i', next: i == 39 ? null : 'n${i + 1}'),
        },
      ),
    ),
    // A line ring is fine, a silent ring is not.
    _raw(withNodes({'a': _line(next: 'b'), 'b': _jump('a')})),
    // A repeated key in the text: the last value wins, in the first place.
    _parse(
      '{"kind":"orblit.dialogue","formatVersion":1,"key":"k",'
      '"variants":[{"start":"a"}],"nodes":{"a":{"type":"jump","next":"b"},'
      '"b":{"type":"line","speaker":"X","text":"Hi"},'
      '"a":{"type":"line","speaker":"Y","text":"Yo"}}}',
    ),
    _parse(
      '{"kind":"x","kind":"orblit.dialogue","formatVersion":1,"key":"k",'
      '"variants":[{"start":"a"}],"nodes":{"a":{"type":"line",'
      '"speaker":"X","text":"\\u00e9\\ud83d\\udd2e\\n"}}}',
    ),
    _parse(
      ' \n{ "kind" : "orblit.dialogue" , "formatVersion" : 1 ,\t"key":"k",'
      '"variants":[{"start":"a"}],"nodes":{"a":{"type":"line",'
      '"speaker":"X","text":"Hi"}}}\n',
    ),
    // Not a dialogue file.
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
    _raw(_file(kind: 'orblit.quest')),
    _raw(_file(kind: null)),
    _raw(_file(kind: 5)),
    _raw(_file(kind: ['orblit.dialogue'])),
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
      '{"kind":"orblit.dialogue","formatVersion":99999999999999999999,'
      '"key":"k","variants":[],"nodes":{}}',
    ),
    _parse(
      '{"kind":"orblit.dialogue","formatVersion":1e0,"key":"k",'
      '"variants":[],"nodes":{}}',
    ),
    // A part missing or the wrong kind.
    _raw(_without(_file(), 'key')),
    _raw(_file(key: 3)),
    _raw(_file(key: null)),
    _raw(_without(_file(), 'variants')),
    _raw(_file(variants: 'x')),
    _raw(_file(variants: {'start': 'a'})),
    _raw(_without(_file(), 'nodes')),
    _raw(_file(nodes: [1])),
    _raw(_file(nodes: 'x')),
    for (final v in <Object?>[
      1,
      1.5,
      1.0,
      null,
      true,
      'x',
      [1, 2],
      <String, Object?>{},
      {'a': 1, 'b': 'x'},
    ])
      _raw(_file(variants: [v])),
    _raw(
      _file(
        variants: [
          {'when': <Object?>[]},
        ],
      ),
    ),
    _raw(
      _file(
        variants: [
          {'start': 4},
        ],
      ),
    ),
    _raw(
      _file(
        variants: [
          {'start': null},
        ],
      ),
    ),
    for (final bad in <Object?>[
      'x',
      <String, Object?>{},
      {'a': 1},
      [1],
      [null],
      [<String, Object?>{}],
      [_need('', '>', 1)],
      [_need(3, '>', 1)],
      [_need('gold', '=>', 1)],
      [_need('gold', '>', -1)],
      [_need('gold', '>', 4294967296)],
      [_need('gold', '>', 1.5)],
      [_need('gold', '>', 1.0)],
      [_need('gold', '>', '1')],
      [
        _need('gold', '>', 1),
        {'name': 'gold'},
      ],
    ]) ...[
      _raw(_file(variants: [_start('a', bad)]), cutAfter: _shown),
      _raw(
        _file(
          nodes: {
            'a': _choice([_option(when: bad)]),
          },
        ),
        cutAfter: _shown,
      ),
    ],
    // A node that is the wrong kind.
    for (final v in <Object?>[
      1,
      1.5,
      1.0,
      null,
      true,
      'x',
      [1, 2],
      <String, Object?>{},
      {'a': 1},
    ])
      _raw(withNode(v)),
    _raw(withNode({'speaker': 'X', 'text': 'Hi'})),
    for (final t in <Object?>[
      'wave',
      '',
      5,
      1.5,
      null,
      true,
      [1],
      {'a': 1},
    ])
      _raw(withNode({'type': t})),
    _raw(withNode({'type': 'wave', 'before': 5})),
    _raw(withNode({'type': 'wave', 'before': ''})),
    _raw(withNode(_line(before: ''))),
    _raw(withNode(_line(before: 5))),
    for (final v in <Object?>[
      null,
      3,
      1.5,
      1.0,
      true,
      [1],
      {'a': 1},
    ])
      _raw(withNode(_line(speaker: v))),
    for (final v in <Object?>[
      null,
      3,
      1.5,
      1.0,
      true,
      [1],
      {'a': 1},
    ])
      _raw(withNode(_line(text: v))),
    for (final v in <Object?>[
      3,
      1.5,
      true,
      [1],
      {'a': 1},
      '',
    ])
      _raw(withNode(_line(next: v))),
    for (final v in <Object?>[
      null,
      'x',
      3,
      {'a': 1},
      [],
    ])
      _raw(withNode(_choice(v))),
    for (final v in <Object?>[
      1,
      null,
      'x',
      [1],
      <String, Object?>{},
    ])
      _raw(withNode(_choice([v]))),
    _raw(withNode(_choice([_option(text: null)]))),
    _raw(withNode(_choice([_option(text: 3)]))),
    _raw(withNode(_choice([_option(next: 3)]))),
    _raw(withNode(_choice([_option(next: 'zz')]))),
    _raw(withNode(_choice([_option(next: '')]))),
    for (final v in <Object?>[
      null,
      'x',
      3,
      {'a': 1},
    ])
      _raw(withNode(_set(v))),
    for (final v in <Object?>[
      1,
      null,
      'x',
      [1],
      <String, Object?>{},
    ])
      _raw(withNode(_set([v]))),
    _raw(withNode(_set([_write('gold', null)]))),
    _raw(withNode(_set([_write(null, 1)]))),
    _raw(withNode(_set([_write(3, 1)]))),
    _raw(withNode(_set([_write('', 1)]))),
    for (final v in <Object?>[
      '1',
      1.5,
      1.0,
      true,
      -1,
      4294967296,
      4294967295,
      0,
      9223372036854775807,
    ])
      _raw(withNode(_set([_write('gold', v)]))),
    _raw(withNode(_jump(null))),
    _raw(withNode(_jump(3))),
    _raw(withNode(_jump(''))),
    _raw(withNode(_jump('a'))),
    // A rule broken, with the text that says which.
    _raw(_file(key: '')),
    _raw(_file(variants: [])),
    _raw(_file(nodes: {})),
    _raw(_file(nodes: {'': _line()})),
    _raw(_file(variants: [_start('zz')])),
    _raw(
      _file(
        variants: [
          _start('a', [_need('gold', '>', 1)]),
          _start('zz'),
        ],
      ),
    ),
    _raw(
      _file(
        variants: [
          _start('a'),
          _start('a', [_need('gold', '>', 1)]),
        ],
      ),
    ),
    _raw(_file(variants: [_start('a'), _start('a')])),
    _raw(
      _file(
        variants: [
          _start('a'),
          _start('b', [_need('gold', '>', 1)]),
        ],
      ),
    ),
    _raw(withNode(_line(next: 'zz'))),
    _raw(withNode(_choice([]))),
    _raw(withNode(_set([], next: 'zz'))),
    _raw(withNode(_jump('zz'))),
    _raw(withNodes({'a': _jump('b'), 'b': _set([], next: 'a')})),
    _raw(withNodes({'a': _jump('b'), 'b': _jump('c'), 'c': _jump('b')})),
    _raw(withNodes({'a': _set([], next: 'a')})),
    _raw(withNodes({'a': _line(next: 'b'), 'b': _jump('a')})),
    // Two errors in one file: the first the reader finds is the one it says.
    _raw(_file(key: '', variants: [])),
    _raw(_file(variants: [], nodes: {'': _line()})),
    _raw(
      _file(
        variants: [_start('zz')],
        nodes: {'a': _line(next: 'yy')},
      ),
    ),
    _raw(_file(variants: [_start('a')], nodes: {'a': _line(speaker: 3)})),
    _raw(
      _file(
        variants: [_start('a')],
        nodes: {
          'a': _line(text: {'a': 1, 'b': 2.5}),
        },
      ),
    ),
  ];
}

const _flags = <List<Object>>[
  ['w', 'flags'],
  ['w', 'gold'],
  ['s', 'quest.lamp.stage', 'flags', 0, 2],
  ['s', 'met', 'flags', 2, 1],
];

const _noGold = <List<Object>>[
  ['w', 'flags'],
  ['s', 'quest.lamp.stage', 'flags', 0, 2],
];

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

/// The functions a case may register. Both languages hold the same four.
Before _hook(String name, List<String> log) => switch (name) {
  'take' => (table) {
    log.add('take:${table.get('gold')}');
    return table.set('gold', 0).table;
  },
  'bump' => (table) {
    final gold = table.get('gold');
    log.add('bump:$gold');
    return table.set('gold', gold + 1).table;
  },
  'id' => (table) {
    log.add('id');
    return table;
  },
  'boom' => (table) {
    log.add('boom');
    throw StateError('no');
  },
  _ => throw ArgumentError.value(name, 'name', 'is not a hook'),
};

const _maxDepth = 8;

/// What a runner shows and holds, with the hook calls so far.
_Json _state(DialogueRunner runner, List<String> log) {
  final node = runner.current;
  return {
    'over': runner.isOver,
    'at': node?.id,
    'kind': switch (node) {
      LineNode() => 'line',
      ChoiceNode() => 'choice',
      _ => null,
    },
    'say': node is LineNode ? [node.speaker, node.text, node.next] : null,
    'options': [
      for (final o in runner.options) [o.text, o.next, o.when.length],
    ],
    'table': hex(runner.table.encode()),
    'log': [...log],
  };
}

/// Every state a conversation can reach in [_maxDepth] steps, depth first.
/// Each state is replayed from the start, so a hook sees the same calls in
/// both languages. A step that throws is recorded with the state it left.
final class _Walker {
  _Walker(this.book, this.key, this.table, this.log);

  final DialogueBook book;
  final String key;
  final ProgressTable table;
  final List<String> log;
  final states = <_Json>[];

  DialogueRunner _replay(List<int> path) {
    log.clear();
    final runner = book.start(key, table);
    for (final step in path) {
      if (step < 0) {
        runner.next();
      } else {
        runner.choose(step);
      }
    }
    return runner;
  }

  void explore(List<int> path) {
    final runner = _replay(path);
    final node = runner.current;
    states.add({
      'path': path.join(','),
      ..._state(runner, log),
      'probes': [
        if (node is! LineNode) _failure(runner.next),
        _failure(() => runner.choose(-1)),
        _failure(() => runner.choose(runner.options.length)),
      ],
    });
    final steps = [
      if (node is LineNode) -1,
      if (node is ChoiceNode)
        for (var i = 0; i < runner.options.length; i++) i,
    ];
    for (final step in steps) {
      final next = [...path, step];
      final again = _replay(path);
      final failed = _failure(
        () => step < 0 ? again.next() : again.choose(step),
      );
      if (failed != null) {
        states.add({
          'path': next.join(','),
          'threw': failed,
          'after': _state(again, log),
        });
      } else if (next.length < _maxDepth) {
        explore(next);
      }
    }
  }
}

_Json _walk(
  DialogueBook book,
  String key,
  ProgressTable table,
  List<String> log,
) {
  final walker = _Walker(book, key, table, log);
  final failed = _failure(() => walker.explore([]));
  return {'key': key, 'threw': failed, 'states': walker.states};
}

_Json _tabled(
  DialogueBook book,
  ProgressSchema schema,
  List<List<Object>> sets,
  List<String> log,
) => _arg(() {
  var table = ProgressTable(schema);
  for (final a in sets) {
    table = table.set(a[0] as String, a[1] as int).table;
  }
  return {
    'table': hex(table.encode()),
    'startFor': [
      for (final d in book.dialogues) [d.key, d.startFor(table)],
    ],
    'unknown': [
      _known(() => book.dialogue('nope').key),
      _known(() => book.start('nope', table).isOver),
    ],
    'walks': [for (final d in book.dialogues) _walk(book, d.key, table, log)],
  };
});

_Json _book(
  List<List<Object>> names,
  List<String> hooks,
  List<_Json> dialogues,
  List<List<List<Object>>> tables,
) {
  final input = {
    'op': 'book',
    'names': names,
    'hooks': hooks,
    'dialogues': dialogues,
    'tables': tables,
  };
  final schema = _schema(names);
  final log = <String>[];
  final parsed = [for (final d in dialogues) Dialogue.parse(jsonEncode(d))];
  try {
    final book = DialogueBook(
      schema: schema,
      hooks: {for (final h in hooks) h: _hook(h, log)},
      dialogues: parsed,
    );
    return {
      ...input,
      'cutAfter': null,
      'expect': {
        'keys': [for (final d in book.dialogues) d.key],
        'before': [for (final d in book.dialogues) d.beforeNames.toList()],
        'tables': [for (final sets in tables) _tabled(book, schema, sets, log)],
      },
    };
  } on ArgumentError catch (error) {
    // A repeated key prints the list of dialogues, which has no twin.
    final cut = error.invalidValue is List ? 'repeat a key' : null;
    return {
      ...input,
      'cutAfter': cut,
      'expect': {
        'throws': true,
        'message': _cut(
          '${error.name} ${error.message} (${error.invalidValue}).',
          cut,
        ),
      },
    };
  }
}

List<List<Object>> _at({int stage = 0, int gold = 0}) => [
  ['quest.lamp.stage', stage],
  ['gold', gold],
];

_Json _guide() => _file(
  key: 'guide',
  variants: [
    _start('again', [_need('quest.lamp.stage', '>=', 1)]),
    _start('hello'),
  ],
  nodes: {
    'hello': _line(speaker: 'Guide', text: 'Hello.', next: 'ask'),
    'ask': _choice([
      _option(text: 'The lamp?', next: 'lamp'),
      _option(
        text: 'I have gold.',
        next: 'pay',
        when: [_need('gold', '>=', 5)],
      ),
      _option(text: 'Goodbye.'),
    ]),
    'lamp': _line(speaker: 'Guide', text: 'In the cave.', next: 'begin'),
    'begin': _set([_write('quest.lamp.stage', 1)], next: 'told'),
    'told': _line(speaker: 'Guide', text: 'Go and find it.'),
    'pay': _line(speaker: 'Guide', text: 'Thanks.', before: 'take'),
    'again': _line(speaker: 'Guide', text: 'Back so soon?'),
  },
);

/// A hook on every kind of node, and a ring that goes through a line.
_Json _hooky() => _file(
  key: 'hooky',
  variants: [_start('j')],
  nodes: {
    'j': _jump('s', before: 'id'),
    's': _set([_write('met', 1)], next: 'l', before: 'bump'),
    'l': _line(text: 'Round', next: 'c', before: 'id'),
    'c': _choice([
      _option(text: 'Again', next: 'j', when: [_need('gold', '>=', 2)]),
      _option(text: 'Rich', next: 'l', when: [_need('gold', '>=', 1)]),
      _option(text: 'Out'),
    ], before: 'id'),
  },
);

_Json _boom() => _file(
  key: 'boom',
  variants: [_start('a')],
  nodes: {
    'a': _line(text: 'One', next: 'w'),
    'w': _set([], next: 'b', before: 'bump'),
    'b': _line(text: 'Two', before: 'boom'),
  },
);

_Json _single(String key, Object? nodes, {Object? when}) =>
    _file(key: key, variants: [_start('a', when)], nodes: nodes);

List<List<List<Object>>> _tables(List<int> stages, List<int> golds) => [
  for (final s in stages)
    for (final g in golds) _at(stage: s, gold: g),
];

List<_Json> _books() {
  final guideTables = _tables([0, 1, 2, 3], [0, 4, 5, 7, 4294967295]);
  final stuck = _single('stuck', {
    'a': _choice([
      _option(text: 'No', when: [_need('quest.lamp.stage', '>=', 3)]),
    ]),
  });
  final quiet = _single(
    'quiet',
    {'a': _line()},
    when: [_need('quest.lamp.stage', '>=', 2)],
  );
  final after = _single('after', {
    'a': _set([_write('gold', 9)], next: 'c'),
    'c': _choice([
      _option(text: 'Rich', when: [_need('gold', '>=', 9)]),
    ]),
  });
  final twice = _single('twice', {
    'a': _set([_write('gold', 1), _write('gold', 2)], next: 'l'),
    'l': _line(),
  });
  return [
    _book(_flags, ['take'], [_guide()], guideTables),
    _book(_flags, ['take', 'bump', 'id', 'boom'], [_hooky()], [
      ..._tables([0], [0, 1, 2, 4294967295]),
    ]),
    _book(
      _flags,
      ['bump', 'boom'],
      [_boom()],
      _tables([0], [0, 3, 4294967295]),
    ),
    _book(_flags, [], [stuck, quiet, after, twice], _tables([0, 3], [0, 9])),
    _book(
      _flags,
      ['take', 'bump', 'id', 'boom'],
      [_guide(), _hooky(), _boom(), stuck],
      _tables([0, 1], [0, 5]),
    ),
    // A hook that reads a name the table does not have fails when it runs.
    _book(
      _noGold,
      ['take'],
      [
        _file(
          key: 'k',
          nodes: {'a': _line(before: 'take')},
        ),
      ],
      [
        [
          ['quest.lamp.stage', 1],
        ],
      ],
    ),
    // A table that cannot be made, and a name that is not there.
    _book(_flags, ['take'], [_guide()], [
      [
        ['quest.lamp.stage', 4],
      ],
      [
        ['quest.lamp.stage', 3],
      ],
      [
        ['nope', 1],
      ],
      [],
    ]),
    // A hook nobody registered, and one registered that no node uses.
    _book(_flags, [], [_guide()], []),
    _book(_flags, ['bump'], [_guide()], []),
    _book(_flags, ['take', 'bump', 'id'], [_guide()], _tables([0], [0])),
    // Names the schema does not have.
    _book(_flags, [], [
      _single('k', {'a': _line()}, when: [_need('ghost', '==', 1)]),
    ], []),
    _book(_flags, [], [
      _single('k', {
        'a': _choice([
          _option(when: [_need('ghost', '==', 1)]),
        ]),
      }),
    ], []),
    _book(_flags, [], [
      _single('k', {
        'a': _set([_write('ghost', 1)]),
      }),
    ], []),
    // The slice limits: met is one bit, the stage two, gold all 32.
    for (final (name, value) in [
      ('met', 1),
      ('met', 2),
      ('quest.lamp.stage', 3),
      ('quest.lamp.stage', 4),
      ('gold', 4294967295),
    ]) ...[
      _book(_flags, [], [
        _single('k', {
          'a': _set([_write(name, value)]),
        }),
      ], _tables([0], [0])),
      _book(_flags, [], [
        _single('k', {'a': _line()}, when: [_need(name, '==', value)]),
      ], _tables([0], [0])),
      _book(_flags, [], [
        _single('k', {
          'a': _choice([
            _option(when: [_need(name, '<=', value)]),
          ]),
        }),
      ], _tables([0], [0])),
    ],
    // The first fault in a book is the one it says: hooks, then conditions,
    // then writes, dialogue by dialogue.
    _book(_flags, [], [
      _single(
        'k',
        {
          'a': _set([_write('ghost', 1)], before: 'zz'),
        },
        when: [_need('phantom', '==', 1)],
      ),
    ], []),
    _book(_flags, [], [
      _single(
        'k',
        {
          'a': _set([_write('ghost', 1)]),
        },
        when: [_need('phantom', '==', 1)],
      ),
    ], []),
    _book(_flags, [], [
      _single('j', {
        'a': _set([_write('ghost', 1)]),
      }),
      _single('k', {'a': _line(before: 'zz')}),
    ], []),
    _book(_flags, [], [
      _single('k', {'a': _line(before: 'zz')}),
      _single('j', {
        'a': _set([_write('ghost', 1)]),
      }),
    ], []),
    // A key used twice.
    _book(_flags, ['take'], [_guide(), _guide()], []),
    _book(_flags, [], [
      _single('k', {'a': _line()}),
      _single('j', {'a': _line()}),
      _single('k', {'a': _line()}),
    ], []),
    _book(_flags, [], [], [[]]),
  ];
}

/// Usage: `dart run bin/dialogue.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_dialogue',
    name: 'dialogue',
    cases: [..._parses(), ..._books()],
  );
}
