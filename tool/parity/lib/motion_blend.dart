import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:vector_math/vector_math_64.dart';

ClipDocument _gait(
  String name, {
  required double duration,
  required double lean,
  double travel = 0,
  double rise = 0,
  WhenDone whenDone = WhenDone.loop,
  List<Mark> marks = const [],
}) => ClipDocument(
  name: name,
  duration: duration,
  whenDone: whenDone,
  rootMotion: travel == 0 && rise == 0
      ? null
      : RootMotion(bone: 'hips', rises: rise != 0),
  channels: [
    ClipChannel<double>(
      target: '',
      property: 'pose.lean',
      kind: ChannelKind.number,
      keys: [
        const Key(0.0, 0.0, hold: Hold.linear),
        Key(duration / 2, lean, hold: Hold.linear),
        Key(duration, 0.0),
      ],
    ),
    ClipChannel<Vector3>(
      target: '',
      bone: 'hips',
      property: 'position',
      kind: ChannelKind.vector,
      keys: [
        Key(0.0, Vector3(0, 1, 0), hold: Hold.linear),
        Key(duration, Vector3(0, 1 + rise, travel)),
      ],
    ),
  ],
  marks: marks,
);

/// The clips the blends play: four gaits with root motion and marks, the
/// walk and the drive that carry turns, and two that sit on the edges.
Map<String, String> _clipTexts() => {
  'idle': _gait('Idle', duration: 1.5, lean: 0.1).encode(),
  'walk': _gait(
    'Walk',
    duration: 1,
    lean: 1,
    travel: 1.4,
    marks: const [Mark(0.25, 'left'), Mark(0.75, 'right')],
  ).encode(),
  'run': _gait(
    'Run',
    duration: 0.6,
    lean: 3,
    travel: 3,
    marks: const [Mark(0.15, 'left'), Mark(0.45, 'right')],
  ).encode(),
  'jump': _gait(
    'Jump',
    duration: 0.8,
    lean: 6,
    travel: 0.5,
    rise: 0.5,
    marks: const [Mark(0.8, 'land')],
  ).encode(),
  'boneWalk': clipTexts()['boneWalk']!,
  'entityWalk': clipTexts()['entityWalk']!,
  'edge': ClipDocument(
    name: 'Edge',
    duration: 1,
    marks: const [
      Mark(0, 'start'),
      Mark(0.3, 'a'),
      Mark(0.6, 'b'),
      Mark(0.6, 'c'),
      Mark(1, 'end'),
    ],
  ).encode(),
  'zero': ClipDocument(
    name: 'Zero',
    duration: 0,
    marks: const [Mark(0, 'a'), Mark(0, 'b')],
  ).encode(),
};

const List<LinePoint> _gaits = [
  LinePoint(1, BlendClip('walk')),
  LinePoint(3, BlendClip('run')),
];

/// Every kind of state, source and condition, and a ring of changes that
/// chase one another round.
BlendDocument _moves() => BlendDocument(
  name: 'Moves',
  inputs: const {'speed': 0, 'dir': 0, 'air': 0, 'hit': 0},
  states: [
    BlendState('idle', plays: const BlendClip('idle')),
    BlendState('move', plays: BlendLine('speed', _gaits)),
    BlendState(
      'pace',
      plays: BlendLine('speed', _gaits),
      speed: 1.2,
      sync: const ['left', 'right'],
    ),
    BlendState(
      'strafe',
      plays: BlendPlane('dir', 'speed', const [
        PlanePoint(-1, 0, BlendClip('idle')),
        PlanePoint(0, 2, BlendClip('walk')),
        PlanePoint(1, 2, BlendClip('run')),
        PlanePoint(0, 0, BlendClip('boneWalk')),
      ]),
    ),
    BlendState(
      'jump',
      plays: const BlendClip('jump'),
      whenDone: WhenDone.hold,
    ),
    BlendState(
      'turn',
      plays: const BlendClip('boneWalk'),
      speed: 0.5,
      whenDone: WhenDone.bounce,
    ),
    BlendState(
      'edgy',
      plays: const BlendClip('edge'),
      whenDone: WhenDone.bounce,
    ),
    BlendState('still', plays: const BlendClip('zero')),
    BlendState('ghost', plays: const BlendClip('missing')),
  ],
  changes: [
    const BlendChange(
      from: 'idle',
      to: 'move',
      when: BlendCondition.above('speed', 0.1),
      fade: 0.25,
      inStep: true,
    ),
    const BlendChange(
      from: 'move',
      to: 'idle',
      when: BlendCondition.below('speed', 0.1),
      fade: 0.4,
      shape: Easing.linear,
    ),
    const BlendChange(
      to: 'jump',
      when: BlendCondition.on('air'),
      fade: 0.1,
      shape: Easing.back,
    ),
    const BlendChange(
      from: 'jump',
      to: 'idle',
      when: BlendCondition.through(1),
      fade: 0.2,
    ),
    const BlendChange(
      from: 'move',
      to: 'pace',
      when: BlendCondition.all([
        BlendCondition.above('speed', 2),
        BlendCondition.not(BlendCondition.on('hit')),
      ]),
      fade: 0.3,
      fromPose: true,
    ),
    const BlendChange(
      from: 'pace',
      to: 'move',
      when: BlendCondition.any([
        BlendCondition.below('speed', 1.5),
        BlendCondition.on('hit'),
      ]),
      fade: 0.3,
    ),
    const BlendChange(
      from: 'idle',
      to: 'strafe',
      when: BlendCondition.above('dir', 0.2),
      fade: 0.2,
    ),
    const BlendChange(
      from: 'strafe',
      to: 'idle',
      when: BlendCondition.below('dir', 0.2),
    ),
    const BlendChange(
      from: 'idle',
      to: 'idle',
      when: BlendCondition.on('hit'),
      fade: 0.1,
    ),
    const BlendChange(
      from: 'idle',
      to: 'turn',
      when: BlendCondition.through(2),
      fade: 0.15,
    ),
    const BlendChange(
      from: 'turn',
      to: 'edgy',
      when: BlendCondition.through(1),
      fade: 0.1,
    ),
    const BlendChange(from: 'edgy', to: 'still'),
    const BlendChange(from: 'still', to: 'ghost'),
    const BlendChange(from: 'ghost', to: 'idle'),
  ],
  fades: [
    BlendFade('walk', 'run', 0.5),
    BlendFade('run', 'walk', 0.15),
    BlendFade('idle', 'walk', 0.05),
  ],
);

/// A graph in a state, with a state beside it.
BlendDocument _nested() => BlendDocument(
  name: 'Outer',
  inputs: const {'jump': 0, 'land': 0},
  states: [
    BlendState(
      'ground',
      speed: 2,
      plays: BlendGraph(
        BlendDocument(
          name: 'Ground',
          inputs: const {'speed': 0.5},
          states: [
            BlendState('idle', plays: const BlendClip('idle')),
            BlendState(
              'walk',
              plays: const BlendClip('walk'),
              sync: ['left', 'right'],
            ),
          ],
          changes: const [
            BlendChange(
              from: 'idle',
              to: 'walk',
              when: BlendCondition.above('speed', 1),
              fade: 0.2,
            ),
            BlendChange(to: 'idle', when: BlendCondition.below('speed', 0.2)),
          ],
          fades: [BlendFade('idle', 'walk', 0.3)],
        ),
      ),
    ),
    BlendState('air', plays: const BlendClip('jump')),
  ],
  changes: const [
    BlendChange(
      from: 'ground',
      to: 'air',
      when: BlendCondition.on('jump'),
      fade: 0.1,
    ),
    BlendChange(
      from: 'air',
      to: 'ground',
      when: BlendCondition.on('land'),
      fade: 0.2,
    ),
  ],
);

/// The smallest blend with one of each part of a file, which a decode is
/// tried against with every place in it spoilt.
BlendDocument _mini() => BlendDocument(
  name: 'Mini',
  inputs: const {'speed': 0.5, 'air': 0},
  states: [
    BlendState(
      'a',
      plays: const BlendClip('walk'),
      speed: 1.5,
      whenDone: WhenDone.hold,
      sync: ['left', 'right'],
    ),
    BlendState(
      'b',
      plays: BlendLine('speed', const [
        LinePoint(0, BlendClip('idle')),
        LinePoint(2, BlendClip('run')),
      ]),
    ),
    BlendState(
      'c',
      plays: BlendPlane('speed', 'air', const [
        PlanePoint(0, 0, BlendClip('idle')),
        PlanePoint(1, 1, BlendClip('run')),
      ]),
    ),
    BlendState(
      'd',
      plays: BlendGraph(
        BlendDocument(
          name: 'Inner',
          states: [
            BlendState('x', plays: const BlendClip('idle')),
            BlendState('y', plays: const BlendClip('walk')),
          ],
          changes: const [
            BlendChange(from: 'x', to: 'y', when: BlendCondition.on('air')),
          ],
        ),
      ),
    ),
  ],
  changes: [
    const BlendChange(
      from: 'a',
      to: 'b',
      when: BlendCondition.all([
        BlendCondition.above('speed', 1),
        BlendCondition.below('air', 2),
      ]),
      fade: 0.2,
      shape: Easing.linear,
      inStep: true,
    ),
    const BlendChange(
      to: 'c',
      when: BlendCondition.any([
        BlendCondition.on('air'),
        BlendCondition.not(BlendCondition.through(2)),
      ]),
      fromPose: true,
    ),
    const BlendChange(from: 'd', to: 'a'),
  ],
  fades: [BlendFade('walk', 'run', 0.4)],
);

/// A plane with a pair of points closer than it takes for one place, and a
/// pair exactly that far apart. 1e-6 squared is the cut-off to the last bit.
BlendDocument _near() => BlendDocument(
  name: 'Near',
  inputs: const {'x': 0, 'y': 0},
  states: [
    BlendState(
      'near',
      plays: BlendPlane('x', 'y', const [
        PlanePoint(0, 0, BlendClip('idle')),
        PlanePoint(1e-6, 0, BlendClip('walk')),
        PlanePoint(5e-7, 0, BlendClip('run')),
        PlanePoint(0, 1, BlendClip('jump')),
      ]),
    ),
  ],
);

/// States whose clips say nothing: one that lasts no time, one whose clip is
/// missing, and a line standing on a point whose clip is missing.
BlendDocument _flat() => BlendDocument(
  name: 'Flat',
  inputs: const {'x': 0},
  states: [
    BlendState('hold', plays: const BlendClip('zero')),
    BlendState(
      'half',
      plays: BlendLine('x', const [
        LinePoint(0, BlendClip('missing')),
        LinePoint(1, BlendClip('entityWalk')),
      ]),
    ),
    BlendState('gone', plays: const BlendClip('missing')),
    BlendState('go', plays: const BlendClip('entityWalk')),
  ],
);

/// A frame dump that also holds the moment the blend read its marks at.
Object _framed(ClipFrame frame) => {
  ...frameDump(frame) as Map<String, Object?>,
  'at': number(frame.at),
};

final Map<String, String> _blendTexts = {
  'moves': _moves().encode(),
  'nested': _nested().encode(),
  'near': _near().encode(),
  'flat': _flat().encode(),
};

// The Dart that runs each case reads the texts the fixture holds, as the
// C# does, so a blend is only ever compared with itself read back.
final Map<String, ClipDocument> _clips = {
  for (final e in _clipTexts().entries)
    e.key: ClipDocument.decode(e.value).clip,
};

final Map<String, BlendDocument> _blends = {
  for (final e in _blendTexts.entries) e.key: BlendDocument.decode(e.value).blend,
};

double _d(Object? value) =>
    value is String ? double.parse(value) : (value! as num).toDouble();

Map<String, double> _values(Object? raw) => {
  for (final e in (raw! as Map<String, Object?>).entries) e.key: _d(e.value),
};

Object _numbered(Map<String, double> values) => {
  for (final e in values.entries) e.key: number(e.value),
};

BlendDocument _blend(Object? name) => _blends[name]!;

BlendPlace _placed(Object? blend, Object? json) =>
    BlendPlace.fromJson(json, _blend(blend)) ?? (throw StateError('no place'));

Object? _marks(List<Mark> marks) => [
  for (final mark in marks) [number(mark.at), mark.name],
];

Object? _changeDump(BlendChange? change) =>
    change == null ? null : tree(change.toJson());

Object _placeDump(BlendPlace place, BlendDocument blend) => {
  'json': tree(place.toJson()),
  'weight': number(place.weight),
  'depth': place.depth,
  'route': place.route,
  'shares': [
    for (final (at, share) in place.shares) [at.state, number(share)],
  ],
  'numbers': [for (final n in place.toNumbers(blend)) number(n)],
};

Object _stepDump(BlendStep step) => {
  'place': tree(step.place.toJson()),
  'marks': _marks(step.marks),
  'moved': stepDump(step.moved),
  'change': _changeDump(step.change),
  'frame': _framed(step.frame),
};

Object _loadDump(BlendLoad load) => {
  'problems': load.problems,
  'text': load.blend.encode(),
  'states': [for (final state in load.blend.states) state.name],
  'entries': load.blend.entries,
  'clips': load.blend.clipNames.toList()..sort(),
  'reads': load.blend.inputsRead.toList()..sort(),
};

/// The clips and blends the other cases name.
List<Case> libraryCases() => [
  {
    'op': 'library',
    'in': <Object?>[],
    'expect': {'clips': _clipTexts(), 'blends': _blendTexts},
  },
];

Case _decodeCase(String text) => caseOf(
  'decode',
  [text],
  (input) => _loadDump(BlendDocument.decode(input[0]! as String)),
);

Map<String, Object?> _tree(BlendDocument blend) =>
    jsonDecode(blend.encode()) as Map<String, Object?>;

List<Object?> _field(Map<String, Object?> tree, String key) =>
    tree[key]! as List<Object?>;

String _edited(void Function(Map<String, Object?> tree) spoil) {
  final tree = _tree(_mini());
  spoil(tree);
  return jsonEncode(tree);
}

/// Spoilt texts that name the notes a decode makes, in the order it makes
/// them.
List<String> _spoilt() {
  final base = _tree(_mini());
  return [
    _edited((t) {
      t['start'] = 'swim';
      _field(t, 'changes').add({'to': 'nowhere'});
      t['fades'] = ['x', 5];
    }),
    _edited((t) {
      _field(t, 'states').add({'name': 'd/x', 'clip': 'walk'});
    }),
    _edited((t) {
      final d = _field(t, 'states').last! as Map<String, Object?>;
      d['speed'] = 1e308;
      ((d['graph']! as Map<String, Object?>)['states']! as List<Object?>)
          .cast<Map<String, Object?>>()
          .first['speed'] = 1e308;
    }),
    _edited((t) {
      _field(t, 'changes').insert(0, {'to': 'b', 'when': 'maybe'});
      (t['inputs']! as Map<String, Object?>)['wind'] = 'strong';
      _field(t, 'states').add({'name': 'e', 'line': 'wind'});
    }),
    _edited((t) {
      t['formatVersion'] = 1;
    }),
    String.fromCharCode(0xFEFF) + jsonEncode(base),
    '${jsonEncode(base)} trailing',
    '${jsonEncode(base)}\n${jsonEncode(base)}',
  ];
}

// Stands for a number too big to hold, which jsonEncode will not write.
const String _overflow = '@overflow';

/// A blend whose last state is written on a line of exactly 80 columns, the
/// longest the writer keeps on one line, and the same with a name one longer.
List<String> _filling() {
  for (var length = 1; length < 80; length++) {
    String blend(int name) => BlendDocument(
      name: 'Fit',
      states: [
        BlendState('a', plays: const BlendClip('walk')),
        BlendState('s' * name, plays: const BlendClip('walk')),
      ],
    ).encode();
    final fits = blend(length).split('\n').any(
      (line) => line.length == 80 && line.endsWith('}'),
    );
    if (fits) return [blend(length), blend(length + 1)];
  }
  throw StateError('no state fills a line');
}

/// Texts on the edge of what a decode takes: each place in the mini blend
/// given an empty name, nought and a number too big to hold, markers that are
/// too few or twice the same, and a state that fills a line.
List<String> _edges() => [
  for (final text in replacements(_tree(_mini()), ['', 0, _overflow]))
    text.replaceAll('"$_overflow"', '1e999'),
  for (final sync in const [
    ['left'],
    ['left', 'left'],
  ])
    _edited((t) {
      (_field(t, 'states').first! as Map<String, Object?>)['sync'] = sync;
    }),
  _clash(),
  ..._filling(),
];

/// A state written as ground/walk beside a graph state named ground that has
/// a child walk.
String _clash() {
  final tree = _tree(_nested());
  (_field(tree, 'states').last! as Map<String, Object?>)['name'] = 'ground/walk';
  return jsonEncode(tree);
}

List<Case> decodeBlendCases() {
  final mini = _mini().encode();
  return [
    for (final text in _blendTexts.values) _decodeCase(text),
    _decodeCase(mini),
    for (final text in [
      '',
      '{',
      'null',
      '[]',
      '"blend"',
      '{}',
      '{"kind": "other"}',
      '{"kind": "${BlendDocument.marker}"}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 3}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 0}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 1.5}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": "2"}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 2}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 2, "states": []}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 2, "states": [5, "a"]}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 2, "states": [{"name": "a"}]}',
      '{"kind": "${BlendDocument.marker}", "formatVersion": 2, "states": [{"name": "a", "clip": "walk"}, {"name": "a", "clip": "run"}]}',
    ])
      _decodeCase(text),
    for (final text in _spoilt()) _decodeCase(text),
    for (final text in _edges()) _decodeCase(text),
    for (final text in mutations(jsonDecode(mini))) _decodeCase(text),
  ];
}

const List<Map<String, double>> _readings = [
  {},
  {'speed': 0.05},
  {'speed': 0.5},
  {'speed': 1.5},
  {'speed': 2},
  {'speed': 2.5, 'dir': 0.5},
  {'speed': 3.5, 'dir': -1},
  {'air': 1, 'speed': 1},
  {'hit': 1, 'speed': 2.5},
];

final List<Map<String, double>> _odd = [
  {'speed': double.nan, 'dir': double.infinity},
  {'speed': -4, 'dir': 9},
];

BlendPlace _standing(String state, {double lap = 0}) =>
    BlendPlace(state, lap: lap);

/// Places in the moves blend: one state, a fade, fades inside fades, the
/// deepest, a requested route and a frozen pose.
List<BlendPlace> _movesPlaces() {
  final snapshot = PoseSnapshot(
    _blend('moves').sampleAt(
      _standing('move', lap: 0.3),
      const {'speed': 2},
      clips: _clips,
    ),
  );
  final fading = BlendPlace(
    'pace',
    lap: 0.2,
    from: BlendPlace(
      'move',
      lap: 1.6,
      from: _standing('idle', lap: 4.25),
      faded: 0.1,
      fade: 0.2,
      shape: Easing.linear,
    ),
    faded: 0.05,
    fade: 0.3,
    shape: Easing.back,
  );
  return [
    _standing('idle'),
    _standing('move', lap: 0.4),
    _standing('move', lap: 2.35),
    _standing('jump', lap: 0.9),
    _standing('idle', lap: 12.5),
    _standing('strafe', lap: 0.5),
    _standing('turn', lap: 0.99),
    _standing('edgy', lap: 0.6),
    _standing('still'),
    _standing('ghost', lap: 0.2),
    BlendPlace(
      'move',
      lap: 0.1,
      from: _standing('idle', lap: 0.7),
      faded: 0.1,
      fade: 0.25,
    ),
    fading,
    fading
        .enter('jump', fade: 0.1, shape: Easing.bounce)
        .enter('idle', fade: 0.1, inStep: true),
    BlendPlace(
      'move',
      lap: 0.25,
      from: _standing('idle', lap: 0.5),
      faded: 0.1,
      fade: 0.2,
      shape: Easing.linear,
    ),
    _standing('idle').withRoute(['move', 'pace']),
    BlendPlace(
      'pace',
      lap: 0.2,
      from: _standing('move', lap: 0.3),
      faded: 0.1,
      fade: 0.3,
      frozen: snapshot,
      journey: BlendRoute(['move']),
    ),
  ];
}

List<BlendPlace> _nestedPlaces() => [
  _standing('ground'),
  _standing('ground/walk', lap: 0.7),
  _standing('air', lap: 0.2),
  BlendPlace(
    'ground/walk',
    lap: 0.4,
    from: _standing('air', lap: 0.9),
    faded: 0.05,
    fade: 0.2,
  ),
];

/// Places a file or a network could hand over that are not good ones.
const List<Object?> _strange = [
  'walk',
  <String, Object?>{},
  {'state': 'swim'},
  {'state': 'move', 'lap': -2},
  {'state': 'move', 'lap': 'x'},
  {'state': 'move', 'lap': 3, 'extra': true},
  {
    'state': 'move',
    'fade': 0.2,
    'faded': 9,
    'from': {'state': 'idle'},
  },
  {
    'state': 'move',
    'fade': -1,
    'from': {'state': 'idle'},
  },
  {
    'state': 'move',
    'fade': 0.2,
    'from': {'state': 'swim'},
  },
  {
    'state': 'move',
    'fade': 0.2,
    'shape': 'wobble',
    'from': {
      'state': 'idle',
      'fade': 0.2,
      'from': {
        'state': 'jump',
        'fade': 0.2,
        'from': {
          'state': 'pace',
          'fade': 0.2,
          'from': {'state': 'strafe'},
        },
      },
    },
  },
  {
    'state': 'idle',
    'route': ['move', 'pace'],
  },
  {
    'state': 'idle',
    'route': ['pace'],
  },
  {
    'state': 'idle',
    'route': ['move', 5],
  },
  {
    'state': 'idle',
    'route': <Object?>[],
  },
  {
    'state': 'move',
    'fade': 0,
    'from': {'state': 'idle'},
  },
  {'state': 'move', 'fade': 0.5},
  {
    'state': 'move',
    'fade': 0.2,
    'from': {'state': 'idle'},
    'frozen': {
      'at': 0,
      'values': <String, Object?>{},
      'bones': {'arm': 5},
    },
  },
  {
    'state': 'move',
    'fade': 0.2,
    'from': {'state': 'idle'},
    'frozen': {'at': 0, 'values': <String, Object?>{}, 'bones': 5},
  },
  {
    'state': 'move',
    'fade': 0.2,
    'from': {'state': 'idle'},
    'frozen': {
      'at': 0,
      'values': <String, Object?>{},
      'bones': {
        '': {
          'arm': {
            'position': [1, 2],
          },
        },
      },
    },
  },
];

List<Case> placeCases() => [
  for (final (blend, place) in [
    for (final place in _movesPlaces()) ('moves', place.toJson()),
    for (final place in _nestedPlaces()) ('nested', place.toJson()),
    for (final place in _strange) ('moves', place),
    ('nested', {'state': 'ground', 'lap': 0.5}),
    ('nested', {'state': 'ground/idle'}),
  ])
    caseOf('place', [blend, place], (input) {
      final into = _blend(input[0]);
      final read = BlendPlace.fromJson(input[1], into);
      if (read == null) return {'read': false};
      final back = BlendPlace.fromNumbers(into, read.toNumbers(into));
      return {
        'read': true,
        ..._placeDump(read, into) as Map<String, Object?>,
        'back': back == null ? null : tree(back.toJson()),
        'same': back == read,
      };
    }),
];

const List<double> _spoils = [
  -1,
  0,
  0.5,
  1,
  2,
  3,
  5,
  9,
  99,
  double.nan,
  double.infinity,
  double.negativeInfinity,
];

List<double> _numbersOf(BlendPlace place) =>
    place.toNumbers(_blend('moves')).toList();

Case _numbersCase(List<double> numbers) => caseOf(
  'numbers',
  [
    'moves',
    [for (final n in numbers) number(n)],
  ],
  (input) {
    final placed = BlendPlace.fromNumbers(_blend(input[0]), [
      for (final n in input[1]! as List<Object?>) _d(n),
    ]);
    return placed == null ? null : tree(placed.toJson());
  },
);

/// Numbers as they were written with each of the fixed ones spoilt in turn,
/// and the extended ones cut short or given a wrong length.
List<Case> numbersCases() {
  final places = _movesPlaces();
  final plain = _numbersOf(places[11]);
  final extended = _numbersOf(places.last);
  return [
    _numbersCase(plain),
    _numbersCase(extended),
    for (var i = 0; i < BlendPlace.width; i++)
      for (final spoil in _spoils)
        _numbersCase([...plain]..[i] = spoil),
    for (final at in [0, 1, 5, 20, 21, 22, 30, extended.length - 1])
      _numbersCase(extended.take(at).toList()),
    for (final at in [BlendPlace.width, BlendPlace.width + 1, 40, 60])
      for (final spoil in [..._spoils, 7.0, 300.0])
        _numbersCase([...extended]..[at] = spoil),
    _numbersCase([...extended, 0]),
    _numbersCase([...plain, 0]),
    _numbersCase(_badBytes()),
  ];
}

/// The numbers of a route with the byte after the first letter of "move" made
/// one no text has.
List<double> _badBytes() {
  final route = _movesPlaces().firstWhere(
    (place) => place.route.isNotEmpty && place.frozen == null,
  );
  final numbers = _numbersOf(route);
  final bytes = [for (final n in numbers.skip(BlendPlace.width + 1)) n.toInt()];
  final at = BlendPlace.width + 1 + utf8.decode(bytes).indexOf('move') + 1;
  return [...numbers]..[at] = 255;
}

Case _readingCase(
  String op,
  String blend,
  Object place,
  Map<String, double> values,
  Object? Function(BlendDocument blend, BlendPlace place, Map<String, double> values)
  run,
) => caseOf(op, [blend, place, _numbered(values)], (input) {
  final into = _blend(input[0]);
  return run(into, _placed(input[0], input[1]), _values(input[2]));
});

List<Case> _readingCases(
  String op,
  Object? Function(BlendDocument blend, BlendPlace place, Map<String, double> values)
  run,
) => [
  for (final values in [..._readings, ..._odd])
    for (final place in _movesPlaces())
      _readingCase(op, 'moves', place.toJson(), values, run),
  for (final values in const [
    <String, double>{},
    {'speed': 2.0},
    {'jump': 1.0},
    {'land': 1.0, 'speed': 0.1},
  ])
    for (final place in _nestedPlaces())
      _readingCase(op, 'nested', place.toJson(), values, run),
  for (final values in const [
    <String, double>{},
    {'x': 3e-7},
    {'x': 5e-7},
    {'x': 1e-6},
    {'x': 2e-6, 'y': 0.5},
    {'x': -1e-6, 'y': 1.0},
    {'x': 5e-7, 'y': 1e-6},
  ])
    _readingCase(op, 'near', _standing('near').toJson(), values, run),
];

List<Case> weightsCases() => _readingCases(
  'weights',
  (blend, place, values) => [
    for (final w in blend.weightsAt(place, values))
      [w.state, w.clip, number(w.weight), number(w.lap)],
  ],
);

List<Case> changeCases() => _readingCases(
  'changeFor',
  (blend, place, values) => _changeDump(blend.changeFor(place, values)),
);

List<Case> sampleBlendCases() => _readingCases(
  'sample',
  (blend, place, values) =>
      _framed(blend.sampleAt(place, values, clips: _clips)),
);

/// A pose that has channels the clips lack, and bones they do not move.
Object _rest() {
  final frame = ClipFrame(0);
  frame.values[''] = {'pose.lean': 0.5, 'pose.extra': 3.0};
  frame.bones[''] = {
    'hips': BoneLocal(position: Vector3(0, 1, 0.25)),
    'tail': BoneLocal(rotation: Quaternion.identity()),
  };
  return PoseSnapshot(frame).toJson();
}

List<Case> restedCases() => [
  for (final place in _movesPlaces())
    for (final values in const [<String, double>{}, {'speed': 2.5}])
      caseOf('rested', ['moves', place.toJson(), _numbered(values), _rest()], (
        input,
      ) {
        final rest = PoseSnapshot.fromJson(input[3])!.sample();
        return _framed(
          _blend(input[0]).sampleAt(
            _placed(input[0], input[1]),
            _values(input[2]),
            clips: _clips,
            rest: rest,
          ),
        );
      }),
];

List<Case> lengthCases() => [
  for (final (blend, state) in [
    for (final state in _blend('moves').states) ('moves', state.name),
    ('moves', 'swim'),
    for (final state in _blend('nested').states) ('nested', state.name),
    ('nested', 'ground'),
    ('nested', 'swim'),
  ])
    for (final values in [..._readings, ..._odd])
      caseOf('length', [blend, state, _numbered(values)], (input) {
        return number(
          _blend(input[0]).lengthOf(
            input[1]! as String,
            _values(input[2]),
            clips: _clips,
          ),
        );
      }),
];

List<Case> routeCases() => [
  for (final blend in ['moves', 'nested'])
    for (final from in [..._blend(blend).states.map((s) => s.name), 'swim'])
      for (final to in [..._blend(blend).states.map((s) => s.name), 'swim'])
        caseOf('route', [blend, from, to], (input) {
          return _blend(input[0]).routeTo(input[1]! as String, input[2]! as String);
        }),
  caseOf('route', ['nested', 'ground', 'air'], (input) {
    return _blend('nested').routeTo('ground', 'air');
  }),
  caseOf('route', ['nested', 'air', 'ground'], (input) {
    return _blend('nested').routeTo('air', 'ground');
  }),
];

Object _entered(BlendPlayer player, List<Object?> command) {
  player.enter(
    command[1]! as String,
    fade: _d(command[2]),
    inStep: command[3]! as bool,
    fromPose: command[4]! as bool,
    shape: Easing.values.byName(command[5]! as String),
  );
  return {'place': tree(player.place.toJson())};
}

Object _command(BlendPlayer player, List<Object?> command) {
  switch (command[0]) {
    case 'advance':
      return _stepDump(player.advance(_d(command[1])));
    case 'enter':
      return _entered(player, command);
    case 'travel':
      player.travel(command[1]! as String);
      return {'place': tree(player.place.toJson())};
    case 'set':
      player.inputs[command[1]! as String] = _d(command[2]);
      return {'inputs': _numbered(player.inputs)};
    case 'sample':
      return {'frame': _framed(player.sample())};
  }
  throw StateError('no command ${command[0]}');
}

Case _playerCase(
  String blend,
  Object? place,
  Map<String, double> inputs,
  List<List<Object?>> script,
) => caseOf('player', [blend, place, _numbered(inputs), script], (input) {
  final player = BlendPlayer(
    _blend(input[0]),
    clips: _clips,
    place: input[1] == null ? null : _placed(input[0], input[1]),
    inputs: _values(input[2]),
  );
  return [
    for (final command in input[3]! as List<Object?>)
      attempt(() => _command(player, command! as List<Object?>)),
  ];
});

List<Object?> _set(String input, double value) => ['set', input, number(value)];

List<List<Object?>> _advance(int count, double seconds) => [
  for (var i = 0; i < count; i++) ['advance', number(seconds)],
];

List<Object?> _enter(
  String state, {
  double fade = 0,
  bool inStep = false,
  bool fromPose = false,
  Easing shape = Easing.smooth,
}) => ['enter', state, number(fade), inStep, fromPose, shape.name];

const double _frame = 1 / 60;

/// Playing the way a game loop does: an input set, a frame, another.
List<Case> playerCases() {
  final snapshot = _movesPlaces().last.toJson();
  return [
    _playerCase('moves', null, const {}, [
      ..._advance(10, _frame),
      _set('speed', 2.5),
      ..._advance(40, _frame),
      _set('speed', 3.5),
      ..._advance(30, _frame),
      _set('speed', 0),
      ..._advance(60, _frame),
    ]),
    _playerCase('moves', null, const {}, [
      _set('speed', 2.5),
      ..._advance(20, _frame),
      _set('hit', 1),
      ..._advance(10, _frame),
      _set('hit', 0),
      _set('speed', 1.2),
      ..._advance(40, _frame),
      _set('speed', 2.8),
      ..._advance(40, _frame),
    ]),
    _playerCase('moves', null, const {}, [
      _set('air', 1),
      ..._advance(6, _frame),
      _set('air', 0),
      ..._advance(70, _frame),
      ['sample'],
    ]),
    _playerCase('moves', null, const {}, _advance(48, 0.5)),
    _playerCase('moves', null, const {'speed': 2}, [
      ['advance', number(0)],
      ['advance', number(double.nan)],
      ['advance', number(-1)],
      ['advance', number(double.infinity)],
      ..._advance(5, 0.4),
      ['sample'],
    ]),
    _playerCase('moves', null, const {}, [
      _enter('jump', fade: 0.2),
      ..._advance(20, _frame),
      _enter('swim'),
      _enter('pace', fade: 0.3, fromPose: true, shape: Easing.back),
      ..._advance(30, _frame),
      _enter('move', fade: 0.4, inStep: true),
      ..._advance(30, _frame),
      _enter('idle'),
      ['sample'],
    ]),
    _playerCase('moves', null, const {'speed': 2}, [
      _enter('walk'),
      _enter('pace', fade: 0.2),
      ..._advance(20, _frame),
      _enter('move', fade: 0.2),
      ..._advance(20, _frame),
      ['sample'],
    ]),
    _playerCase('moves', null, const {}, [
      ['travel', 'pace'],
      ..._advance(120, 0.05),
      ['travel', 'swim'],
      ['travel', 'ghost'],
      ..._advance(60, 0.2),
    ]),
    _playerCase('moves', null, const {}, [
      ['travel', 'strafe'],
      _enter('jump'),
      ['travel', 'idle'],
      ..._advance(40, 0.1),
    ]),
    _playerCase('moves', null, const {'dir': 0.6}, [
      ..._advance(30, _frame),
      _set('dir', -0.5),
      ..._advance(30, _frame),
      _set('dir', 0),
      ..._advance(30, _frame),
    ]),
    _playerCase('moves', snapshot, const {'speed': 1}, [
      ['sample'],
      ..._advance(40, _frame),
      _set('speed', 0),
      ..._advance(40, _frame),
    ]),
    _playerCase('moves', {'state': 'swim'}, const {}, [
      ['sample'],
    ]),
    _playerCase('moves', {'state': 'still'}, const {}, [
      ..._advance(3, 0.1),
      ['sample'],
    ]),
    _playerCase('moves', {'state': 'pace'}, const {'speed': 2}, [
      ['advance', number(0)],
    ]),
    _playerCase('flat', {'state': 'hold'}, const {}, _advance(3, 0.1)),
    _playerCase('flat', {'state': 'half'}, const {'x': 0}, [
      ..._advance(3, 0.1),
      ['sample'],
    ]),
    // A fade so slow that the new state's say is nought, or too small for a
    // product with a clip's weight to hold.
    _playerCase('flat', {'state': 'gone'}, const {'x': 1e-23}, [
      _enter('go', fade: 1e300),
      ..._advance(2, 0.1),
    ]),
    _playerCase('flat', {'state': 'gone'}, const {'x': 1e-23}, [
      _enter('half', fade: 1e300, shape: Easing.linear),
      ..._advance(2, 0.1),
    ]),
    _playerCase('nested', null, const {}, [
      ..._advance(10, _frame),
      _set('speed', 2),
      ..._advance(40, _frame),
      _set('jump', 1),
      ..._advance(20, _frame),
      _set('jump', 0),
      _set('land', 1),
      ..._advance(40, _frame),
    ]),
    _playerCase('nested', null, const {}, [
      ['travel', 'air'],
      ..._advance(30, 0.1),
      ['travel', 'ground'],
      ..._advance(30, 0.1),
      _enter('ground/walk', fade: 0.2),
      ..._advance(10, _frame),
    ]),
  ];
}
