import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:vector_math/vector_math_64.dart';

CutsceneShot _shot(String camera, double start, double duration) =>
    CutsceneShot(camera: camera, start: start, duration: duration);

CutsceneSound _sound(String sound, double start, double duration) =>
    CutsceneSound(sound: sound, start: start, duration: duration);

CutsceneDocument _cutscene(
  String name,
  double duration, {
  List<CutsceneShot> shots = const [],
  List<CutsceneSound> sounds = const [],
  List<Mark> marks = const [],
  List<ClipChannel<Object>> channels = const [],
  WhenDone whenDone = WhenDone.hold,
  double rate = 30,
}) => CutsceneDocument(
  motion: ClipDocument(
    name: name,
    duration: duration,
    whenDone: whenDone,
    rate: rate,
    channels: channels,
    marks: marks,
  ),
  shots: shots,
  sounds: sounds,
);

ClipChannel<Vector3> _slides(String target) => ClipChannel<Vector3>(
  target: target,
  property: 'transform.position',
  kind: ChannelKind.vector,
  keys: [Key(0, Vector3.zero()), Key(4, Vector3(4, 0, 0))],
);

/// Cutscenes that differ in how their shots lie: overlapping in a chain,
/// one inside another, starting together, touching, apart, out of order and
/// barely overlapping, and one with no shot or sound at all.
Map<String, String> cutsceneTexts() => {
  'slide': CutsceneDocument(
    motion: ClipDocument(
      name: 'Cube slides',
      duration: 4,
      channels: [
        _slides('cube'),
        ClipChannel<double>(
          target: 'street1/lamp3/bulb',
          property: 'light.power',
          kind: ChannelKind.number,
          keys: const [Key(0, 10), Key(2, 80)],
        ),
      ],
      marks: const [
        Mark(3, 'door opens', payload: {'door': 'front'}),
      ],
    ),
    shots: [_shot('side', 2, 2), _shot('front', 0, 3)],
    sounds: [_sound('sounds/whoosh.ogg', 1, 2)],
  ).encode(),
  'chain': _cutscene(
    'Chain',
    5,
    rate: 24,
    whenDone: WhenDone.loop,
    shots: [_shot('a', 0, 2), _shot('b', 1.5, 2), _shot('c', 3, 2)],
    sounds: [
      _sound('one.ogg', 0, 1),
      _sound('two.ogg', 0.5, 2.5),
      _sound('three.ogg', 4, 2),
    ],
    marks: const [Mark(0, 'start'), Mark(2.5, 'middle'), Mark(5, 'end')],
    channels: [
      ClipChannel<bool>(
        target: 'door',
        property: 'mesh.visible',
        kind: ChannelKind.flag,
        keys: const [Key(0, false), Key(2.5, true)],
      ),
    ],
  ).encode(),
  'inside': _cutscene(
    'Inside',
    5,
    shots: [_shot('wide', 0, 5), _shot('close', 1, 1), _shot('late', 4, 3)],
  ).encode(),
  'together': _cutscene(
    'Together',
    3,
    shots: [_shot('x', 0, 2), _shot('y', 0, 3), _shot('z', 0, 1)],
    sounds: [_sound('a.ogg', 1, 1), _sound('b.ogg', 1, 2)],
  ).encode(),
  'touching': _cutscene(
    'Touching',
    3,
    shots: [_shot('front', 0, 1), _shot('side', 1, 1), _shot('top', 2, 1)],
  ).encode(),
  'gaps': _cutscene(
    'Gaps',
    4,
    shots: [_shot('front', 1, 1), _shot('side', 3, 1)],
  ).encode(),
  'bare': _cutscene('Bare', 2).encode(),
  'reversed': _cutscene(
    'Reversed',
    5,
    shots: [_shot('c', 4, 1), _shot('b', 2, 2), _shot('a', 0, 3)],
    sounds: [_sound('late.ogg', 3, 1), _sound('early.ogg', 0, 1)],
  ).encode(),
  'sliver': _cutscene(
    'Sliver',
    2,
    shots: [_shot('a', 0, 1), _shot('b', 0.999999, 1)],
  ).encode(),
};

String _file(Map<String, Object?> json) => jsonEncode({
  'kind': CutsceneDocument.marker,
  'formatVersion': CutsceneDocument.formatVersion,
  ...json,
});

Map<String, Object?> _row(String camera, Object start, Object duration) => {
  'camera': camera,
  'start': start,
  'duration': duration,
};

Map<String, Object?> _noise(String sound, Object start, Object duration) => {
  'sound': sound,
  'start': start,
  'duration': duration,
};

Map<String, Object?> _boneChannel = {
  'target': 'hero',
  'bone': 'hips',
  'property': 'rotation',
  'kind': 'rotation',
  'keys': [
    {
      'at': 0,
      'value': [0, 0, 0, 1],
    },
  ],
};

/// Files a cutscene could be given that are not one Dart wrote.
List<String> _oddTexts() {
  final marker = CutsceneDocument.marker;
  return [
    '',
    '{',
    'null',
    '[]',
    '{}',
    '{"kind": "other"}',
    '{"kind": "$marker"}',
    '{"kind": "$marker", "formatVersion": 2}',
    '{"kind": "$marker", "formatVersion": 0}',
    '{"kind": "$marker", "formatVersion": "1"}',
    '{"kind": "${ClipDocument.marker}", "formatVersion": 1}',
    _file({}),
    _file({'name': ''}),
    _file({'name': 5}),
    _file({'duration': 3}),
    _file({'duration': 0}),
    _file({'duration': -1}),
    _file({'duration': 'long'}),
    _file({'shots': 5}),
    _file({'shots': {}}),
    _file({
      'shots': [1, 'a', null, <Object?>[]],
    }),
    _file({
      'shots': [_row('front', 0, 2)],
      'sounds': [_noise('a.ogg', 1, 4)],
    }),
    _file({
      'duration': 1,
      'shots': [_row('front', 0, 2)],
    }),
    _file({
      'duration': 0,
      'shots': [_row('front', 0, 2)],
      'sounds': [_noise('a.ogg', 1, 4)],
    }),
    _file({
      'shots': [
        _row('front', 0, 2),
        _row('', 0, 2),
        _row('side', 1, 0),
        _row('top', -1, 2),
        _row('low', 0, -2),
        {'camera': 'nowhere', 'duration': 2},
        {'camera': 'startless', 'start': 'x', 'duration': 2},
        {'camera': 'lengthless', 'start': 1},
        {'camera': 7, 'start': 1, 'duration': 2},
        {'start': 1, 'duration': 2},
      ],
    }),
    _file({
      'sounds': [
        _noise('a.ogg', 0, 2),
        _noise('', 0, 2),
        _noise('b.ogg', 1, 0),
        _noise('c.ogg', -1, 2),
        {'sound': 'd.ogg', 'duration': 3},
        {'sound': 'e.ogg', 'start': 1},
        {'sound': 4, 'start': 1, 'duration': 2},
      ],
    }),
    _file({
      'shots': [
        _row('a', 1.5, 1),
        _row('b', 1.5, 2),
        _row('c', 0, 1),
        _row('d', 1.5, 0.5),
      ],
      'sounds': [
        _noise('x.ogg', 2, 1),
        _noise('y.ogg', 2, 1),
        _noise('z.ogg', 1, 1),
      ],
    }),
    _file({
      'rootMotion': {'bone': 'hips'},
    }),
    _file({
      'channels': [_boneChannel],
    }),
    _file({
      'rootMotion': {'bone': 'hips'},
      'channels': [
        _boneChannel,
        {
          'target': 'cube',
          'property': 'mesh.visible',
          'kind': 'flag',
          'keys': [
            {'at': 0, 'value': true},
          ],
        },
      ],
    }),
    _file({
      'rate': 0,
      'whenDone': 'loop',
      'marks': [
        {'at': 2, 'name': 'late'},
        {
          'at': 1,
          'name': 'early',
          'payload': {'n': 1},
        },
        {'at': 3},
      ],
    }),
  ];
}

Case _decodeCase(String text) => {
  'op': 'decode',
  'in': [text],
  'expect': attempt(() {
    final load = CutsceneDocument.decode(text);
    return {'problems': load.problems, 'text': load.cutscene.encode()};
  }),
};

/// Each cutscene Dart wrote, files that are not cutscenes, and each place in
/// one cutscene given a value of the wrong kind or taken away.
List<Case> decodeCases() {
  final texts = cutsceneTexts().values.toList();
  return [
    for (final text in texts) _decodeCase(text),
    for (final text in _oddTexts()) _decodeCase(text),
    for (final text in mutations(jsonDecode(texts.first))) _decodeCase(text),
  ];
}

List<double> _ats() => [
  -1,
  0,
  0.25,
  0.5,
  0.999,
  1,
  1.5,
  1.75,
  2,
  2.1,
  2.25,
  2.5,
  2.75,
  2.9,
  3,
  3.25,
  3.5,
  4,
  4.5,
  5,
  6,
];

CutsceneDocument _read(Object? text) =>
    CutsceneDocument.decode(text as String).cutscene;

double _double(Object? number) => (number as num).toDouble();

/// What a sequence says at each moment: the cameras looked through and the
/// sounds playing.
List<Case> frameCases() => [
  for (final text in cutsceneTexts().values)
    for (final at in _ats())
      caseOf('frame', [text, at], (input) {
        final frame = _read(input[0]).sequence.sampleAt(_double(input[1]));
        return {
          'shots': [
            for (final shot in frame.shots) [shot.camera, number(shot.weight)],
          ],
          'sounds': [
            for (final sound in frame.sounds)
              [sound.sound, number(sound.at), number(sound.gain)],
          ],
        };
      }),
];

/// The marks crossed between two moments, running forwards, standing still
/// and backwards.
List<Case> markCases() => [
  for (final text in [cutsceneTexts()['slide'], cutsceneTexts()['chain']])
    for (final (from, to) in [
      (-1.0, 0.0),
      (-1.0, 6.0),
      (0.0, 0.0),
      (0.0, 5.0),
      (2.5, 2.5),
      (2.49, 2.5),
      (2.5, 3.0),
      (2.99, 3.0),
      (3.0, 3.5),
      (4.0, 0.0),
      (5.0, 5.5),
    ])
      caseOf('marks', [text, from, to], (input) {
        final marks = _read(
          input[0],
        ).sequence.marksBetween(_double(input[1]), _double(input[2]));
        return [
          for (final mark in marks)
            [number(mark.at), mark.name, tree(mark.payload)],
        ];
      }),
];

/// A director stepping a cutscene's sequence in steps of one size, ending each
/// way the director can.
List<Case> playCases() => [
  for (final text in cutsceneTexts().values)
    for (final ending in WhenDone.values)
      for (final (steps, seconds) in [(50, 0.1), (20, 0.5), (3, 1.0), (7, 2.5)])
        caseOf('play', [text, ending.name, steps, seconds], (input) {
          final director = Director(
            _read(input[0]).sequence,
            whenDone: WhenDone.values.byName(input[1] as String),
          )..play();
          final marks = <Object?>[];
          for (var i = 0; i < (input[2] as int); i++) {
            for (final mark in director.advance(_double(input[3])).marks) {
              marks.add([number(mark.at), mark.name]);
            }
          }
          return {
            'marks': marks,
            'at': number(director.at),
            'finished': director.finished,
          };
        }),
];

final List<(String, double, double)> _spans = [
  ('front', 0, 1),
  ('', 0, 1),
  ('front', -1, 1),
  ('front', -0.0, 1),
  ('front', 0, 0),
  ('front', 0, -1),
  ('front', 0, 1e-320),
  ('front', 1e300, 1e300),
  ('front', 2.5, 0.25),
];

/// The three things a shot or a sound must have.
List<Case> spanCases() => [
  for (final (name, start, duration) in _spans) ...[
    caseOf('shot', [name, start, duration], (input) {
      final shot = _shot(
        input[0] as String,
        _double(input[1]),
        _double(input[2]),
      );
      return [
        shot.camera,
        number(shot.start),
        number(shot.duration),
        number(shot.end),
      ];
    }),
    caseOf('sound', [name, start, duration], (input) {
      final sound = _sound(
        input[0] as String,
        _double(input[1]),
        _double(input[2]),
      );
      return [
        sound.sound,
        number(sound.start),
        number(sound.duration),
        number(sound.end),
      ];
    }),
  ],
];
