import 'dart:convert';
import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

typedef Case = Map<String, Object?>;

Object vecDump(Vector3 v) => [number(v.x), number(v.y), number(v.z)];

Object quatDump(Quaternion q) => [
  number(q.x),
  number(q.y),
  number(q.z),
  number(q.w),
];

Object? _value(Object value) => switch (value) {
  final double d => number(d),
  final Vector3 v => vecDump(v),
  final Quaternion q => quatDump(q),
  final bool b => b,
  _ => throw StateError('no dump for ${value.runtimeType}'),
};

/// A frame the way a fixture holds it: everything it says about an entity, and
/// where it has each bone.
Object frameDump(ClipFrame frame) => {
  'values': {
    for (final target in frame.values.entries)
      target.key: {
        for (final property in target.value.entries)
          property.key: _value(property.value),
      },
  },
  'bones': {
    for (final target in frame.bones.entries)
      target.key: {
        for (final bone in target.value.entries)
          bone.key: {
            'position': bone.value.position.andThen(vecDump),
            'rotation': bone.value.rotation.andThen(quatDump),
            'scale': bone.value.scale.andThen(vecDump),
          },
      },
  },
};

extension on Object? {
  Object? andThen<T extends Object>(Object? Function(T) convert) =>
      this == null ? null : convert(this as T);
}

Object stepDump(RootStep step) => {
  'position': vecDump(step.position),
  'rotation': quatDump(step.rotation),
  'none': step.isNone,
};

ClipDocument _lamp() => ClipDocument(
  name: 'Lamp flicker',
  duration: 2,
  whenDone: WhenDone.loop,
  rate: 24,
  rootMotion: RootMotion(bone: 'hips', turns: true),
  channels: [
    ClipChannel<double>(
      target: 'bulb',
      property: 'light.power',
      kind: ChannelKind.number,
      keys: [
        const Key(0.0, 60.0, hold: Hold.linear),
        const Key(0.5, 20.0, hold: Hold.linear),
        const Key(1.0, 60.0, hold: Hold.shaped, shape: Easing.bounce),
        const Key(2.0, 60.0, hold: Hold.linear),
      ],
    ),
    ClipChannel<Vector3>(
      target: '',
      property: 'transform.position',
      kind: ChannelKind.vector,
      keys: [Key(0.0, Vector3(0, 0, 0)), Key(2.0, Vector3(1, 2, 3))],
    ),
    ClipChannel<bool>(
      target: 'bulb/glow',
      property: 'mesh.visible',
      kind: ChannelKind.flag,
      keys: const [Key(0.0, true, hold: Hold.step), Key(1.5, false)],
    ),
    ClipChannel<Quaternion>(
      target: '',
      bone: 'hips',
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [
        Key(
          0.0,
          Quaternion.identity(),
          hold: Hold.curve,
          slopeOut: Quaternion(0, 0.5, 0, 0),
        ),
        Key(
          1.0,
          Quaternion.axisAngle(Vector3(0, 1, 0), 0.5),
          hold: Hold.curve,
          slopeIn: Quaternion(0, 0.25, 0, -0.1),
        ),
      ],
    ),
  ],
  marks: const [
    Mark(1, 'buzz', payload: {'volume': 0.5}),
    Mark(0.25, 'spark'),
  ],
);

Quaternion _yaw(double turns) =>
    Quaternion.axisAngle(Vector3(0, 1, 0), turns * 2 * math.pi);

/// A walk on a bone: forward and up and to the side, turning a quarter each
/// second, with a second bone and a scale beside the root.
ClipDocument _boneWalk({bool turns = true, bool rises = false}) => ClipDocument(
  name: 'Walk',
  duration: 2,
  rootMotion: RootMotion(bone: 'hips', turns: turns, rises: rises),
  marks: const [Mark(0.5, 'left'), Mark(1.0, 'right'), Mark(1.5, 'left')],
  channels: [
    ClipChannel<Vector3>(
      target: '',
      bone: 'hips',
      property: 'position',
      kind: ChannelKind.vector,
      keys: [
        Key(0.0, Vector3(0, 1, 0), hold: Hold.linear),
        Key(1.0, Vector3(2, 1.25, 1), hold: Hold.linear),
        Key(2.0, Vector3(3, 1, 4), hold: Hold.linear),
      ],
    ),
    ClipChannel<Quaternion>(
      target: '',
      bone: 'hips',
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [
        Key(0.0, _yaw(0)),
        Key(1.0, _yaw(0.25) * Quaternion.axisAngle(Vector3(1, 0, 0), 0.2)),
        Key(2.0, _yaw(0.5)),
      ],
    ),
    ClipChannel<Vector3>(
      target: '',
      bone: 'hips',
      property: 'scale',
      kind: ChannelKind.vector,
      keys: [Key(0.0, Vector3(1, 1, 1)), Key(2.0, Vector3(2, 1, 1))],
    ),
    ClipChannel<Quaternion>(
      target: '',
      bone: 'spine',
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [Key(0.0, _yaw(0)), Key(2.0, _yaw(0.1))],
    ),
  ],
);

/// A root on the entity itself, with its turn as angles.
ClipDocument _entityWalk() => ClipDocument(
  name: 'Drive',
  duration: 2,
  whenDone: WhenDone.bounce,
  rootMotion: RootMotion(turns: true, rises: true),
  channels: [
    ClipChannel<Vector3>(
      target: '',
      property: 'transform.position',
      kind: ChannelKind.vector,
      keys: [
        Key(0.0, Vector3(1, 0, 0), hold: Hold.linear),
        Key(1.0, Vector3(1, 3, 2), hold: Hold.linear),
        Key(2.0, Vector3(-4, 3, 2), hold: Hold.linear),
      ],
    ),
    ClipChannel<Vector3>(
      target: '',
      property: 'transform.rotation',
      kind: ChannelKind.vector,
      keys: [
        Key(0.0, Vector3(0, 0, 0)),
        Key(1.0, Vector3(10, 90, 0)),
        Key(2.0, Vector3(0, 200, 30)),
      ],
    ),
    ClipChannel<double>(
      target: 'wheel',
      property: 'light.power',
      kind: ChannelKind.number,
      keys: const [Key(0.0, 1.0), Key(2.0, 5.0)],
    ),
  ],
);

/// A root on the entity whose turn is a rotation, about an up that is not y.
ClipDocument _tilted() => ClipDocument(
  name: 'Wall',
  duration: 1,
  whenDone: WhenDone.hold,
  rate: 60,
  rootMotion: RootMotion(turns: true, up: Vector3(0, 0, 3)),
  channels: [
    ClipChannel<Vector3>(
      target: '',
      property: 'transform.position',
      kind: ChannelKind.vector,
      keys: [Key(0.0, Vector3(0, 0, 1)), Key(1.0, Vector3(5, 2, 4))],
    ),
    ClipChannel<Quaternion>(
      target: '',
      property: 'transform.rotation',
      kind: ChannelKind.rotation,
      keys: [
        Key(0.0, Quaternion.identity()),
        Key(1.0, Quaternion.axisAngle(Vector3(0, 0, 1), 1.2)),
      ],
    ),
  ],
);

/// One number channel with a key for every hold and every easing, and a
/// vector one with slopes.
ClipDocument _shapes() {
  final keys = <Key<double>>[];
  var at = 0.0;
  for (final hold in Hold.values) {
    if (hold == Hold.shaped) {
      for (final shape in Easing.values) {
        keys.add(Key(at, at * 2, hold: hold, shape: shape));
        at += 0.5;
      }
    } else {
      keys.add(Key(at, at * 2, hold: hold));
      at += 0.5;
    }
  }
  return ClipDocument(
    name: 'Shapes',
    duration: at,
    channels: [
      ClipChannel<double>(
        target: 'a',
        property: 'light.power',
        kind: ChannelKind.number,
        keys: keys,
      ),
      ClipChannel<Vector3>(
        target: 'a',
        property: 'transform.scale',
        kind: ChannelKind.vector,
        keys: [
          Key(
            0.0,
            Vector3(1, 1, 1),
            hold: Hold.curve,
            slopeOut: Vector3(1, 0, -1),
          ),
          Key(1.0, Vector3(2, 3, 4), hold: Hold.curve),
          Key(
            3.0,
            Vector3(0, 0, 0),
            hold: Hold.curve,
            slopeIn: Vector3(0, 2, 0),
            slopeOut: Vector3(0, 0, 3),
          ),
        ],
      ),
    ],
  );
}

final Map<String, ClipDocument> _clips = {
  'lamp': _lamp(),
  'boneWalk': _boneWalk(),
  'boneWalkFlat': _boneWalk(turns: false),
  'boneWalkRising': _boneWalk(rises: true),
  'entityWalk': _entityWalk(),
  'tilted': _tilted(),
  'shapes': _shapes(),
  'empty': ClipDocument(name: 'Empty', duration: 0),
};

/// Clips in the order the cases walk them: name, then the text Dart wrote.
Map<String, String> clipTexts() => {
  for (final e in _clips.entries) e.key: e.value.encode(),
};

// One wrong value of each JSON kind, to put in each place in turn.
final List<Object?> _wrong = [
  'x',
  -3,
  2.5,
  <Object?>[],
  null,
  true,
  <String, Object?>{},
];

const Object _gone = Object();

Object? _copy(Object? tree) => jsonDecode(jsonEncode(tree));

Iterable<List<Object>> _paths(Object? node, [List<Object> at = const []]) sync* {
  switch (node) {
    case final Map<String, Object?> map:
      for (final e in map.entries) {
        yield [...at, e.key];
        yield* _paths(e.value, [...at, e.key]);
      }
    case final List<Object?> list:
      for (var i = 0; i < list.length; i++) {
        yield [...at, i];
        yield* _paths(list[i], [...at, i]);
      }
  }
}

String _with(Object? base, List<Object> path, Object? replacement) {
  final tree = _copy(base);
  Object? holder = tree;
  for (final step in path.take(path.length - 1)) {
    holder = holder is Map<String, Object?> ? holder[step as String] : (holder as List<Object?>)[step as int];
  }
  final last = path.last;
  if (holder is Map<String, Object?>) {
    if (identical(replacement, _gone)) {
      holder.remove(last);
    } else {
      holder[last as String] = replacement;
    }
  } else if (holder is List<Object?>) {
    if (identical(replacement, _gone)) {
      holder.removeAt(last as int);
    } else {
      holder[last as int] = replacement;
    }
  }
  return jsonEncode(tree);
}

/// The text of [base] with each place in it given each of [values] in turn.
Iterable<String> replacements(Object? base, List<Object?> values) sync* {
  for (final path in _paths(base)) {
    for (final value in values) {
      yield _with(base, path, value);
    }
  }
}

/// The text of [base] with each place in it given a value of the wrong kind
/// in turn, and then taken away.
Iterable<String> mutations(Object? base) =>
    replacements(base, [..._wrong, _gone]);

Case _decodeCase(String text) => {
  'op': 'decode',
  'in': [text],
  'expect': attempt(() {
    final load = ClipDocument.decode(text);
    return {'problems': load.problems, 'text': load.clip.encode()};
  }),
};

/// Each clip Dart wrote, text that is not a clip, and each place in a clip
/// given a value of the wrong kind or taken away.
List<Case> decodeCases() {
  final texts = clipTexts().values.toList();
  final base = jsonDecode(texts.first);
  final marker = ClipDocument.marker;
  return [
    for (final text in texts) _decodeCase(text),
    for (final text in [
      '',
      '{',
      'null',
      '[]',
      '"clip"',
      '{}',
      '{"kind": "other"}',
      '{"kind": "$marker"}',
      '{"kind": "$marker", "formatVersion": 2}',
      '{"kind": "$marker", "formatVersion": 0}',
      '{"kind": "$marker", "formatVersion": 1.5}',
      '{"kind": "$marker", "formatVersion": "1"}',
      '{"kind": "$marker", "formatVersion": 1, "duration": 3}',
      '{"kind": "$marker", "formatVersion": 1, "duration": 3, "channels": 5}',
      '{"kind": "$marker", "formatVersion": 1, "rate": 0}',
      '{"kind": "$marker", "formatVersion": 1, "rate": -5, "duration": -1}',
    ])
      _decodeCase(text),
    for (final text in mutations(base)) _decodeCase(text),
  ];
}

List<double> get _ats => [
  -1.0,
  0.0,
  0.1,
  0.25,
  0.5,
  0.75,
  1.0,
  1.25,
  1.5,
  2.0,
  2.5,
  3.0,
  3.5,
];

List<Case> sampleCases() => [
  for (final e in clipTexts().entries)
    for (final inPlace in [false, true])
      for (final at in _ats)
        {
          'op': 'sample',
          'in': [e.value, at, inPlace],
          'expect': attempt(
            () => frameDump(
              ClipDocument.decode(e.value).clip.sampleAt(at, inPlace: inPlace),
            ),
          ),
        },
];

const List<(double, double)> _spans = [
  (0.0, 1.0),
  (0.5, 1.5),
  (1.0, 0.0),
  (0.0, 2.0),
  (2.0, 0.0),
  (0.3, 0.31),
  (-1.0, 3.0),
  (3.0, -1.0),
  (2.0, 2.0),
  (0.0, 0.25),
  (0.25, 1.0),
  (1.5, 0.5),
];

List<Case> rootCases() => [
  for (final e in clipTexts().entries)
    for (final (from, to) in _spans)
      {
        'op': 'root',
        'in': [e.value, from, to],
        'expect': attempt(
          () => stepDump(ClipDocument.decode(e.value).clip.rootStep(from, to)),
        ),
      },
];

List<Case> markCases() => [
  for (final e in clipTexts().entries)
    for (final (from, to) in _spans)
      {
        'op': 'marks',
        'in': [e.value, from, to],
        'expect': attempt(
          () => [
            for (final mark in ClipDocument.decode(
              e.value,
            ).clip.marksBetween(from, to))
              [number(mark.at), mark.name, mark.payload],
          ],
        ),
      },
];

Case _mixCase(List<String> names, List<double> ats, List<double> weights) {
  final texts = clipTexts();
  return {
    'op': 'mix',
    'in': [
      [for (final name in names) texts[name]],
      ats,
      [for (final w in weights) number(w)],
    ],
    'expect': attempt(() {
      final frames = [
        for (var i = 0; i < names.length; i++)
          ClipFrame.sample(ClipDocument.decode(texts[names[i]]!).clip, ats[i]),
      ];
      return frameDump(ClipFrame.mix(frames, weights, at: 0.5));
    }),
  };
}

List<Case> mixCases() => [
  for (final weights in [
    [0.5, 0.5],
    [1.0, 0.0],
    [0.0, 1.0],
    [0.2, 0.8],
    [0.0, 0.0],
    [-1.0, 1.0],
    [double.nan, 1.0],
    [3.0, 1.0],
  ])
    for (final names in [
      ['boneWalk', 'lamp'],
      ['boneWalk', 'boneWalkFlat'],
      ['entityWalk', 'tilted'],
      ['lamp', 'shapes'],
    ])
      _mixCase(names, [0.4, 1.2], weights),
  _mixCase(['boneWalk', 'lamp', 'entityWalk'], [0.4, 1.2, 0.9], [0.5, 0.25, 0.25]),
  _mixCase(['lamp'], [1.0], [1.0]),
  _mixCase(['lamp'], [1.0], [0.5, 0.5]),
  _mixCase(['lamp', 'boneWalk'], [1.0, 0.0], [1.0]),
  _mixCase(const [], const [], const []),
];

final List<(Vector3, Quaternion)> _steps = [
  (Vector3.zero(), Quaternion.identity()),
  (Vector3(1, 0, 0), Quaternion.identity()),
  (Vector3(0, 2, -1), Quaternion.identity()),
  (Vector3.zero(), Quaternion.axisAngle(Vector3(0, 1, 0), math.pi / 2)),
  (Vector3(1, 2, 3), Quaternion.axisAngle(Vector3(0, 1, 0), 0.7)),
  (Vector3(0, 0, 1), Quaternion.axisAngle(Vector3(1, 0, 0), -1.1)),
  (Vector3(1e-10, 0, 0), Quaternion.identity()),
  (Vector3.zero(), Quaternion(0, 0, 0, -1)),
];

List<Case> thenCases() => [
  for (final (pa, ra) in _steps)
    for (final (pb, rb) in _steps)
      {
        'op': 'then',
        'in': [
          [vecDump(pa), quatDump(ra)],
          [vecDump(pb), quatDump(rb)],
        ],
        'expect': attempt(
          () => stepDump(
            RootStep(position: pa, rotation: ra).then(
              RootStep(position: pb, rotation: rb),
            ),
          ),
        ),
      },
];
