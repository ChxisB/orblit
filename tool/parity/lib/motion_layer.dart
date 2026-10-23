import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:vector_math/vector_math_64.dart';

Quaternion _about(double x, double y, double z, double radians) =>
    Quaternion.axisAngle(Vector3(x, y, z).normalized(), radians);

List<Object?> _part(Vector3 position, Quaternion rotation, Vector3 scale) => [
  vecDump(position),
  quatDump(rotation),
  vecDump(scale),
];

List<Object?> _rest(double x, double y, double z) =>
    _part(Vector3(x, y, z), Quaternion.identity(), Vector3.all(1));

/// A bone as a frame holds it: any of position, rotation and scale.
List<Object?> _bone(Vector3? p, Quaternion? q, Vector3? s) => [
  p == null ? null : vecDump(p),
  q == null ? null : quatDump(q),
  s == null ? null : vecDump(s),
];

/// Skeletons as a fixture holds them: names, parents and a rest transform
/// for each bone in its parent.
final Map<String, Map<String, Object?>> _skeletons = {
  'rig': {
    'names': ['hips', 'leg', 'spine', 'arm', 'hand'],
    'parents': [-1, 0, 0, 2, 3],
    'local': [
      _rest(0, 1, 0),
      _rest(0, -1, 0),
      _rest(0, 1, 0),
      _part(Vector3(1, 0, 0), _about(0, 0, 1, 0.2), Vector3.all(1)),
      _part(Vector3(1, 0, 0), Quaternion.identity(), Vector3.all(1.5)),
    ],
  },
  'prop': {
    'names': ['root', 'lid'],
    'parents': [-1, 0],
    'local': [_rest(0, 0, 0), _rest(0, 2, 0)],
  },
};

/// Frames as a fixture holds them. A value is a number, a flag, three
/// numbers for a vector or four for a turn.
final Map<String, Map<String, Object?>> _frames = {
  'empty': {
    'at': 0.0,
    'values': <String, Object?>{},
    'bones': <String, Object?>{},
  },
  'rest': {
    'at': 0.0,
    'values': {
      'lamp': {
        'light.power': 10.0,
        'transform.position': vecDump(Vector3.zero()),
        'mesh.visible': true,
        'turn': quatDump(Quaternion.identity()),
      },
    },
    'bones': {
      '': {
        'hips': _bone(Vector3(0, 1, 0), Quaternion.identity(), Vector3.all(1)),
        'leg': _bone(Vector3(0, -1, 0), Quaternion.identity(), Vector3.all(1)),
        'spine': _bone(Vector3(0, 1, 0), Quaternion.identity(), Vector3.all(1)),
        'arm': _bone(Vector3(1, 0, 0), _about(0, 0, 1, 0.2), Vector3.all(1)),
        'hand': _bone(
          Vector3(1, 0, 0),
          Quaternion.identity(),
          Vector3.all(1.5),
        ),
      },
      'prop': {
        'root': _bone(Vector3.zero(), Quaternion.identity(), Vector3.all(1)),
        'lid': _bone(Vector3(0, 2, 0), Quaternion.identity(), Vector3.all(1)),
      },
    },
  },
  'base': {
    'at': 0.75,
    'values': {
      'lamp': {
        'light.power': 20.0,
        'transform.position': vecDump(Vector3(1, 2, 3)),
        'mesh.visible': false,
        'turn': quatDump(_about(0, 1, 0, 1)),
      },
    },
    'bones': {
      '': {
        'hips': _bone(Vector3(0, 1.5, 0), _about(0, 1, 0, 0.3), null),
        'leg': _bone(
          Vector3(0, -2, 1),
          _about(1, 0, 0, -0.5),
          Vector3.all(1.2),
        ),
        'arm': _bone(Vector3(2, 0, 0), _about(1, 0, 0, 0.4), Vector3.all(2)),
      },
    },
  },
  'overlay': {
    'at': 0.25,
    'values': {
      'lamp': {
        'light.power': 40.0,
        'transform.position': vecDump(Vector3(3, 3, 3)),
        'mesh.visible': true,
        'turn': quatDump(_about(0, 1, 0, 2)),
      },
      'ghost': {'light.power': 5.0, 'mesh.visible': true},
    },
    'bones': {
      '': {
        'arm': _bone(Vector3(4, 0, 0), _about(0, 0, 1, 1), Vector3(3, 1, 1)),
        'hand': _bone(null, null, Vector3.all(2)),
        'spine': _bone(null, _about(1, 0, 0, 0.8), null),
        'tail': _bone(Vector3(0, 0, 9), null, null),
      },
      'prop': {'lid': _bone(Vector3(0, 3, 0), null, null)},
      'elsewhere': {'bone': _bone(Vector3(1, 1, 1), null, null)},
    },
  },
  'hands': {
    'at': 0.0,
    'values': <String, Object?>{},
    'bones': {
      '': {
        'hips': _bone(null, null, null),
        'leg': _bone(Vector3(0, 5, 0), null, null),
        'hand': _bone(
          Vector3(1, 1, 1),
          _about(1, 1, 0, 0.9),
          Vector3(0.5, 0.5, 0.5),
        ),
      },
    },
  },
  'reference': {
    'at': 0.0,
    'values': {
      'lamp': {
        'light.power': 15.0,
        'transform.position': vecDump(Vector3(1, 0, 0)),
        'mesh.visible': true,
        'turn': quatDump(_about(0, 1, 0, 0.5)),
      },
    },
    'bones': {
      '': {
        'arm': _bone(Vector3(1, 0, 0), _about(0, 0, 1, 0.3), Vector3.all(2)),
        'hand': _bone(null, _about(0, 1, 0, 0.1), Vector3(0.5, 1, 2)),
      },
    },
  },
  'extra': {
    'at': 0.0,
    'values': <String, Object?>{},
    'bones': {
      '': {'tail': _bone(Vector3(0, 0, 1), null, null)},
    },
  },
  'noScale': {
    'at': 0.0,
    'values': <String, Object?>{},
    'bones': {
      '': {
        'arm': _bone(null, null, Vector3(0, 2, 1)),
        'hand': _bone(null, null, Vector3.zero()),
      },
    },
  },
};

/// The names a case may use for the frames above.
const List<String> _bases = ['empty', 'base'];
const List<String> _overlays = ['overlay', 'hands', 'empty'];

/// Masks as a fixture holds them: a target and a bone to weight list.
final Map<String, Object?> _masks = {
  'none': null,
  'upper': {
    'target': '',
    'weights': [
      ['spine', 1.0],
      ['arm', 1.0],
      ['hand', 1.0],
    ],
  },
  'half': {
    'target': '',
    'weights': [
      ['arm', 0.5],
      ['leg', 0.25],
    ],
  },
  'elsewhere': {
    'target': 'prop',
    'weights': [
      ['lid', 1.0],
      ['arm', 1.0],
    ],
  },
  'closed': {'target': '', 'weights': <Object?>[]},
};

const List<double> _layerWeights = [0.0, 0.25, 0.49, 0.5, 1.0];
const List<double> _addWeights = [0.0, 0.3, 1.0];
const List<double> _badWeights = [
  -0.1,
  1.5,
  double.nan,
  double.infinity,
  double.negativeInfinity,
];

double _num(Object? raw) =>
    raw is String ? double.parse(raw) : (raw! as num).toDouble();

Vector3 _vector(Object? raw) {
  final list = raw! as List<Object?>;
  return Vector3(_num(list[0]), _num(list[1]), _num(list[2]));
}

Quaternion _turn(Object? raw) {
  final list = raw! as List<Object?>;
  return Quaternion(_num(list[0]), _num(list[1]), _num(list[2]), _num(list[3]));
}

Object _value(Object? raw) => switch (raw) {
  final bool flag => flag,
  final num number => number.toDouble(),
  final List<Object?> list when list.length == 3 => _vector(list),
  final List<Object?> list => _turn(list),
  _ => throw StateError('no value'),
};

ClipFrame _frame(Object? raw) {
  final spec = raw! as Map<String, Object?>;
  final frame = ClipFrame(_num(spec['at']));
  for (final target in (spec['values']! as Map<String, Object?>).entries) {
    frame.values[target.key] = {
      for (final property in (target.value! as Map<String, Object?>).entries)
        property.key: _value(property.value),
    };
  }
  for (final target in (spec['bones']! as Map<String, Object?>).entries) {
    frame.bones[target.key] = {
      for (final bone in (target.value! as Map<String, Object?>).entries)
        bone.key: BoneLocal(
          position: _maybe(bone.value, 0, _vector),
          rotation: _maybe(bone.value, 1, _turn),
          scale: _maybe(bone.value, 2, _vector),
        ),
    };
  }
  return frame;
}

T? _maybe<T>(Object? parts, int at, T Function(Object?) read) {
  final part = (parts! as List<Object?>)[at];
  return part == null ? null : read(part);
}

RestSkeleton _skeleton(Object? raw) {
  final spec = raw! as Map<String, Object?>;
  return RestSkeleton(
    names: [for (final n in spec['names']! as List<Object?>) n! as String],
    parents: [for (final p in spec['parents']! as List<Object?>) p! as int],
    local: [
      for (final l in (spec['local']! as List<Object?>).cast<List<Object?>>())
        Matrix4.compose(_vector(l[0]), _turn(l[1]), _vector(l[2])),
    ],
  );
}

BoneMask? _mask(Object? raw) {
  if (raw == null) return null;
  final spec = raw as Map<String, Object?>;
  return BoneMask({
    for (final pair
        in (spec['weights']! as List<Object?>).cast<List<Object?>>())
      pair[0]! as String: _num(pair[1]),
  }, target: spec['target']! as String);
}

Object _framed(ClipFrame frame) => {
  ...frameDump(frame) as Map<String, Object?>,
  'at': number(frame.at),
};

Object _weights(BoneMask mask) => {
  'target': mask.target,
  'weights': [
    for (final one in mask.weights.entries) [one.key, number(one.value)],
  ],
  'probe': [
    for (final (target, bone) in _probes) number(mask.weightOf(target, bone)),
  ],
};

const List<(String, String)> _probes = [
  ('', 'arm'),
  ('', 'leg'),
  ('prop', 'arm'),
  ('', 'nope'),
  ('', 'hips'),
];

/// Clips every layer case plays, as the text Dart wrote.
Map<String, String> _clips() {
  ClipChannel<Vector3> moves(String bone, double from, double to) =>
      ClipChannel<Vector3>(
        target: '',
        bone: bone,
        property: 'position',
        kind: ChannelKind.vector,
        keys: [
          Key(0.0, Vector3(from, 0, 0), hold: Hold.linear),
          Key(1.0, Vector3(to, from + 1, 0), hold: Hold.linear),
        ],
      );
  return {
    'reach': ClipDocument(
      name: 'Reach',
      duration: 1,
      marks: const [Mark(0, 'start'), Mark(0.5, 'mid'), Mark(1, 'end')],
      channels: [
        moves('arm', 0, 2),
        moves('leg', 5, 4),
        ClipChannel<Quaternion>(
          target: '',
          bone: 'spine',
          property: 'rotation',
          kind: ChannelKind.rotation,
          keys: [
            Key(0.0, Quaternion.identity(), hold: Hold.linear),
            Key(1.0, _about(0, 0, 1, 1.2), hold: Hold.linear),
          ],
        ),
        ClipChannel<Vector3>(
          target: '',
          bone: 'hand',
          property: 'scale',
          kind: ChannelKind.vector,
          keys: [
            Key(0.0, Vector3.all(1), hold: Hold.linear),
            Key(1.0, Vector3.all(2), hold: Hold.linear),
          ],
        ),
      ],
    ).encode(),
    'props': ClipDocument(
      name: 'Props',
      duration: 2,
      marks: const [Mark(1, 'flip')],
      channels: [
        ClipChannel<double>(
          target: 'lamp',
          property: 'light.power',
          kind: ChannelKind.number,
          keys: const [
            Key(0.0, 10.0, hold: Hold.linear),
            Key(2.0, 30.0, hold: Hold.linear),
          ],
        ),
        ClipChannel<Vector3>(
          target: 'lamp',
          property: 'transform.position',
          kind: ChannelKind.vector,
          keys: [
            Key(0.0, Vector3.zero(), hold: Hold.linear),
            Key(2.0, Vector3(2, 4, 6), hold: Hold.linear),
          ],
        ),
        ClipChannel<bool>(
          target: 'lamp',
          property: 'mesh.visible',
          kind: ChannelKind.flag,
          keys: const [
            Key(0.0, false, hold: Hold.step),
            Key(1.0, true),
          ],
        ),
        ClipChannel<Quaternion>(
          target: 'lamp',
          property: 'turn',
          kind: ChannelKind.rotation,
          keys: [
            Key(0.0, Quaternion.identity(), hold: Hold.linear),
            Key(2.0, _about(0, 1, 0, 2), hold: Hold.linear),
          ],
        ),
      ],
    ).encode(),
    'travel': ClipDocument(
      name: 'Travel',
      duration: 1,
      rootMotion: RootMotion(bone: 'hips', turns: true),
      channels: [
        moves('hips', 0, 3),
        moves('arm', 1, 1),
        ClipChannel<Quaternion>(
          target: '',
          bone: 'hips',
          property: 'rotation',
          kind: ChannelKind.rotation,
          keys: [
            Key(0.0, Quaternion.identity(), hold: Hold.linear),
            Key(1.0, _about(0, 1, 0, 0.8), hold: Hold.linear),
          ],
        ),
      ],
    ).encode(),
    'empty': ClipDocument(name: 'Empty', duration: 1).encode(),
    'zero': ClipDocument(
      name: 'Zero',
      duration: 0,
      marks: const [Mark(0, 'now')],
    ).encode(),
  };
}

ClipDocument _clip(Object? name) => ClipDocument.decode(_clips()[name]!).clip;

/// The skeletons, frames and clips the other cases name.
List<Case> libraryCases() => [
  {
    'op': 'library',
    'in': <Object?>[],
    'expect': {
      'skeletons': _skeletons,
      'frames': _frames,
      'masks': _masks,
      'clips': _clips(),
    },
  },
];

Case _case(
  String op,
  List<Object?> input,
  Object? Function(List<Object?> input) run,
) {
  final seen = jsonDecode(jsonEncode(input)) as List<Object?>;
  return {'op': op, 'in': seen, 'expect': attempt(() => run(seen))};
}

Map<String, RestSkeleton> _skeletonsOf(Object? names) => {
  for (final one in names! as List<Object?>)
    one! as String: _skeleton(_skeletons[one]),
};

List<Case> restCases() => [
  for (final names in const [
    <String>[],
    ['rig'],
    ['prop'],
    ['rig', 'prop'],
  ])
    _case('rest', [
      names,
    ], (input) => _framed(restFrame(_skeletonsOf(input[0])))),
];

List<Case> completeCases() => [
  for (final frame in _frames.keys)
    _case(
      'complete',
      [frame, 'rest'],
      (input) => _framed(
        completeFrame(_frame(_frames[input[0]]), _frame(_frames[input[1]])),
      ),
    ),
  for (final frame in const ['base', 'overlay'])
    _case(
      'complete',
      [frame, 'empty'],
      (input) => _framed(
        completeFrame(_frame(_frames[input[0]]), _frame(_frames[input[1]])),
      ),
    ),
];

List<Case> maskCases() => [
  for (final bone in const ['hips', 'leg', 'spine', 'arm', 'hand', 'nope'])
    for (final target in const ['', 'prop'])
      for (final weight in const [1.0, 0.5, 0.0])
        _case(
          'below',
          ['rig', bone, target, weight],
          (input) => _weights(
            BoneMask.below(
              _skeleton(_skeletons[input[0]]),
              input[1]! as String,
              target: input[2]! as String,
              weight: _num(input[3]),
            ),
          ),
        ),
  for (final weight in _badWeights)
    _case(
      'below',
      ['rig', 'arm', '', number(weight)],
      (input) => _weights(
        BoneMask.below(
          _skeleton(_skeletons[input[0]]),
          input[1]! as String,
          weight: _num(input[3]),
        ),
      ),
    ),
  for (final name in _masks.keys)
    if (_masks[name] != null)
      _case('mask', [_masks[name]], (input) => _weights(_mask(input[0])!)),
  for (final weight in [-0.5, 1.5, double.nan, double.infinity])
    _case('mask', [
      {
        'target': '',
        'weights': [
          ['arm', 1.0],
          ['leg', number(weight)],
        ],
      },
    ], (input) => _weights(_mask(input[0])!)),
];

List<Case> layerCases() => [
  for (final base in _bases)
    for (final overlay in _overlays)
      for (final mask in _masks.keys)
        for (final weight in _layerWeights)
          _case(
            'layer',
            [base, overlay, 'rest', _masks[mask], weight],
            (input) => _framed(
              layerFrame(
                _frame(_frames[input[0]]),
                _frame(_frames[input[1]]),
                rest: _frame(_frames[input[2]]),
                mask: _mask(input[3]),
                weight: _num(input[4]),
              ),
            ),
          ),
  for (final weight in _badWeights)
    _case(
      'layer',
      ['base', 'overlay', 'rest', null, number(weight)],
      (input) => _framed(
        layerFrame(
          _frame(_frames[input[0]]),
          _frame(_frames[input[1]]),
          rest: _frame(_frames[input[2]]),
          weight: _num(input[4]),
        ),
      ),
    ),
];

List<Case> addCases() => [
  for (final base in _bases)
    for (final overlay in _overlays)
      for (final reference in const [null, 'reference', 'noScale', 'extra'])
        for (final mask in const ['none', 'upper', 'half'])
          for (final weight in _addWeights)
            _case(
              'add',
              [base, overlay, 'rest', reference, _masks[mask], weight],
              (input) => _framed(
                addFrame(
                  _frame(_frames[input[0]]),
                  _frame(_frames[input[1]]),
                  rest: _frame(_frames[input[2]]),
                  reference: input[3] == null
                      ? null
                      : _frame(_frames[input[3]]),
                  mask: _mask(input[4]),
                  weight: _num(input[5]),
                ),
              ),
            ),
  for (final weight in _badWeights)
    _case(
      'add',
      ['base', 'overlay', 'rest', null, null, number(weight)],
      (input) => _framed(
        addFrame(
          _frame(_frames[input[0]]),
          _frame(_frames[input[1]]),
          rest: _frame(_frames[input[2]]),
          weight: _num(input[5]),
        ),
      ),
    ),
];

/// A layer as a case spells it: the clip, then a mask name, two fades, an
/// influence, whether it adds and a reference frame name.
ClipLayer _layer(List<Object?> input) => ClipLayer(
  _clip(input[0]),
  mask: _mask(input[1]),
  fadeIn: _num(input[2]),
  fadeOut: _num(input[3]),
  influence: _num(input[4]),
  additive: input[5]! as bool,
  reference: input[6] == null ? null : _frame(_frames[input[6]]),
);

const List<(double, double)> _fades = [
  (0.1, 0.1),
  (0.2, 0.4),
  (0.0, 0.0),
  (0.0, 0.5),
  (0.0, 2.0),
  (0.5, 0.0),
  (0.6, 0.6),
];

List<Case> weightCases() => [
  for (final clip in const ['reach', 'zero'])
    for (final (fadeIn, fadeOut) in _fades)
      for (final influence in const [1.0, 0.5])
        for (final at in const [0.0, 0.05, 0.1, 0.3, 0.5, 0.95, 1.0, 1.5])
          _case(
            'weight',
            [clip, number(fadeIn), number(fadeOut), influence, at],
            (input) => number(
              _layer([
                input[0],
                null,
                input[1],
                input[2],
                input[3],
                false,
                null,
              ]).weightAt(LayerPlace(_num(input[4]))),
            ),
          ),
  for (final (fadeIn, fadeOut, influence) in const [
    (-1.0, 0.1, 1.0),
    (double.nan, 0.1, 1.0),
    (double.infinity, 0.1, 1.0),
    (0.1, -0.5, 1.0),
    (0.1, double.nan, 1.0),
    (0.1, double.infinity, 1.0),
    (0.1, 0.1, 2.0),
    (0.1, 0.1, -1.0),
    (0.1, 0.1, double.nan),
  ])
    _case(
      'weight',
      ['reach', number(fadeIn), number(fadeOut), number(influence), 0.5],
      (input) => number(
        _layer([
          input[0],
          null,
          input[1],
          input[2],
          input[3],
          false,
          null,
        ]).weightAt(LayerPlace(_num(input[4]))),
      ),
    ),
];

List<Case> advanceCases() => [
  for (final clip in const ['reach', 'props', 'zero'])
    for (final at in const [0.0, 0.25, 0.5, 0.75, 1.0, 1.5])
      for (final seconds in const [0.0, 0.25, 0.5, 1.0, 5.0, -1.0])
        _case('advance', [clip, at, seconds], (input) => _stepped(input)),
  for (final seconds in [double.nan, double.infinity, double.negativeInfinity])
    _case('advance', [
      'reach',
      0.25,
      number(seconds),
    ], (input) => _stepped(input)),
];

Object _stepped(List<Object?> input) {
  final step = _layer([
    input[0],
    null,
    0.1,
    0.1,
    1.0,
    false,
    null,
  ]).advance(LayerPlace(_num(input[1])), _num(input[2]));
  return {
    'at': number(step.place.at),
    'finished': step.finished,
    'marks': [
      for (final mark in step.marks) [number(mark.at), mark.name],
    ],
  };
}

List<Case> sampleCases() => [
  for (final clip in const ['reach', 'props', 'travel'])
    for (final additive in const [false, true])
      for (final mask in const ['none', 'upper'])
        for (final base in _bases)
          for (final at in const [0.0, 0.05, 0.5, 0.95, 1.0, 1.5])
            _case(
              'sample',
              [
                clip,
                _masks[mask],
                0.1,
                0.2,
                0.75,
                additive,
                additive && clip != 'travel' ? 'reference' : null,
                base,
                'rest',
                at,
              ],
              (input) => _framed(
                _layer(input).sampleAt(
                  _frame(_frames[input[7]]),
                  LayerPlace(_num(input[9])),
                  rest: _frame(_frames[input[8]]),
                ),
              ),
            ),
];

const List<Object?> _places = [
  {'at': 0},
  {'at': 0.5},
  {'at': 1},
  {'at': 2.5},
  {'at': 1e300},
  {'at': -1},
  {'at': -0.5},
  {'at': 'x'},
  {'at': null},
  {'at': true},
  {'at': <Object?>[]},
  {'at': <String, Object?>{}},
  <String, Object?>{},
  {'other': 1},
  <Object?>[],
  <Object?>[0.5],
  3,
  'at',
  null,
  true,
];

List<Case> placeCases() => [
  for (final raw in _places)
    _case('place', [raw], (input) {
      final place = LayerPlace.fromJson(input[0]);
      if (place == null) return null;
      return {
        'json': tree(place.toJson()),
        'at': number(place.at),
        'back': tree(LayerPlace.fromJson(place.toJson())!.toJson()),
      };
    }),
  for (final at in const [0.0, 0.25, 1e300, -0.0, -1.0, double.nan])
    _case('placeAt', [
      number(at),
    ], (input) => number(LayerPlace(_num(input[0])).at)),
  for (final at in const [double.infinity, double.negativeInfinity])
    _case('placeAt', [
      number(at),
    ], (input) => number(LayerPlace(_num(input[0])).at)),
];
