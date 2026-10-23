import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/motion_clips.dart';
import 'package:orblit_parity_tool/motion_retarget_names.dart';
import 'package:vector_math/vector_math_64.dart';

typedef _Json = Map<String, Object?>;

const _core = ['plain', 'mannequin', 'capture', 'blender', 'bip'];

/// Pairs that are not each other's twin: other spines, poses, units,
/// armatures above, and bones missing.
const _shaped = [
  ('capture', 'capture.a60'),
  ('capture.a60', 'mannequin'),
  ('plain', 'plain.s3.rf'),
  ('plain.s1', 'capture.s3'),
  ('blender.s1.rf.end', 'def.s3'),
  ('joint', 'capture'),
  ('plain', 'v.plain.zUpAbove'),
  ('v.plain.zUpAbove', 'capture'),
  ('capture', 'v.capture.centimetres'),
  ('v.capture.centimetres', 'v.capture.zUpAbove'),
  ('capture', 'v.capture.oneLeg'),
  ('v.capture.noLegs', 'capture'),
  ('v.capture.rooted', 'mannequin'),
  ('v.mannequin.halfNamed', 'mannequin'),
  ('n.a', 'n.b'),
  ('v.single', 'plain'),
  ('v.single', 'v.single'),
  ('v.empty', 'plain'),
  ('plain', 'v.empty'),
];

_Json _options({
  bool humanoid = false,
  bool fixRest = false,
  _Json bones = const {},
  List<String> unpaired = const [],
}) => {
  'humanoid': humanoid,
  'fixRest': fixRest,
  'bones': bones,
  'unpaired': unpaired,
};

Quaternion _turn(int bone, double amount) => Quaternion.axisAngle(
  Vector3(1, 0.5 + (bone % 3) * 0.25, 0.3 - (bone % 2) * 0.6).normalized(),
  0.2 + 0.1 * bone + amount,
);

/// What a source says at one moment: nothing, a turn for every bone, or some
/// bones turned, some moved, some sized and one the source does not have.
_Json _frame(String flavour, RestSkeleton from) {
  if (flavour == 'none') return {};
  final out = <String, Object?>{};
  for (var i = 0; i < from.length; i++) {
    final name = from.names[i];
    if (flavour == 'turns') {
      out[name] = [null, quatDump(_turn(i, 0)), null];
      continue;
    }
    out[name] = switch (i % 5) {
      0 => [null, quatDump(_turn(i, 0.3)), null],
      1 => [
        vecDump(from.positionOf(i) + Vector3(0.1 * i, 0.05, -0.2)),
        quatDump(_turn(i, 0.1)),
        null,
      ],
      2 => [null, null, vecDump(Vector3(1.2, 1, 1.5))],
      3 => [vecDump(Vector3(0.5, -0.5, 0.25)), null, null],
      _ => [null, null, null],
    };
  }
  out['ghost'] = [null, quatDump(_turn(1, 0)), null];
  return out;
}

Vector3? _vector(Object? parts) {
  final p = parts as List<Object?>?;
  return p == null
      ? null
      : Vector3(p[0]! as double, p[1]! as double, p[2]! as double);
}

Quaternion? _turned(Object? parts) {
  final p = parts as List<Object?>?;
  return p == null
      ? null
      : Quaternion(
          p[0]! as double,
          p[1]! as double,
          p[2]! as double,
          p[3]! as double,
        );
}

Map<String, BoneLocal> _sourceOf(Object? raw) => {
  for (final MapEntry(:key, :value) in (raw! as _Json).entries)
    key: () {
      final parts = value! as List<Object?>;
      return BoneLocal(
        position: _vector(parts[0]),
        rotation: _turned(parts[1]),
        scale: _vector(parts[2]),
      );
    }(),
};

LiveRetarget _made(List<Object?> input) {
  final options = input[2]! as _Json;
  return LiveRetarget(
    from: skeletonNamed(input[0]! as String),
    to: skeletonNamed(input[1]! as String),
    bones: {
      for (final MapEntry(:key, :value) in (options['bones']! as _Json).entries)
        key: value! as String,
    },
    unpaired: {
      for (final one in options['unpaired']! as List<Object?>) one! as String,
    },
    humanoid: options['humanoid']! as bool,
    fixRest: options['fixRest']! as bool,
  );
}

Object? _followed(List<Object?> input) {
  final live = _made(input);
  final frame = live.follow(_sourceOf(input[3]));
  return {
    'problems': live.problems,
    'frame': {
      for (final MapEntry(:key, :value) in frame.entries) key: _localDump(value),
    },
  };
}

Object _localDump(BoneLocal local) => {
  'position': local.position == null ? null : vecDump(local.position!),
  'rotation': local.rotation == null ? null : quatDump(local.rotation!),
  'scale': local.scale == null ? null : vecDump(local.scale!),
};

Object? _pointed(List<Object?> input) =>
    vecDump(_made(input).pointOf(input[4]! as String, _sourceOf(input[3])));

Object? _pinned(List<Object?> input) {
  _made(input).pinned(input[4]! as String, source: () => null);
  return true;
}

Case _case(
  String op,
  List<Object?> input,
  Object? Function(List<Object?>) run,
) {
  for (final name in [input[0]! as String, input[1]! as String]) {
    if (!hasSkeleton(name)) throw StateError('no skeleton "$name"');
  }
  return caseOf(op, input, run);
}

List<(String, String)> _pairs() => [
  for (final from in _core)
    for (final to in _core)
      if (from != to) (from, to),
  ..._shaped,
];

List<Case> followCases() => [
  for (final (from, to) in _pairs())
    for (final options in [
      _options(),
      _options(humanoid: true),
      _options(humanoid: true, fixRest: true),
    ])
      for (final flavour in ['none', 'turns', 'full'])
        _case('follow', [
          from,
          to,
          options,
          _frame(flavour, skeletonNamed(from)),
        ], _followed),
  ..._handCases(),
];

List<Case> _handCases() {
  final cases = <Case>[];
  for (final (from, to) in [
    ('capture', 'mannequin'),
    ('plain', 'plain.s3.rf'),
    ('capture', 'v.capture.oneLeg'),
  ]) {
    final source = skeletonNamed(from).names;
    final target = skeletonNamed(to).names;
    final frame = _frame('full', skeletonNamed(from));
    final one = _options(bones: {target[2 % target.length]: source[0]});
    final two = _options(
      bones: {
        target[4 % target.length]: source[3 % source.length],
        target[1 % target.length]: source[3 % source.length],
      },
    );
    for (final options in [
      one,
      two,
      _options(unpaired: [target[3 % target.length]]),
      _options(
        humanoid: true,
        fixRest: true,
        bones: {target[2 % target.length]: source[0]},
        unpaired: [target[3 % target.length]],
      ),
      _options(unpaired: target.take(6).toList()),
      _options(bones: {'nowhere': source[0]}),
      _options(bones: {target[0]: 'nowhere'}),
      _options(unpaired: ['nowhere']),
      _options(
        bones: {target[2 % target.length]: source[0]},
        unpaired: [target[2 % target.length]],
      ),
    ]) {
      cases.add(_case('follow', [from, to, options, frame], _followed));
    }
  }
  return cases;
}

List<Case> pointCases() {
  final cases = <Case>[];
  for (final (from, to) in _pairs()) {
    final skeleton = skeletonNamed(from);
    final names = skeleton.names;
    final bones = {
      if (names.isNotEmpty) ...{
        names.first,
        names[names.length ~/ 2],
        names.last,
      },
      'nowhere',
    };
    for (final bone in bones) {
      for (final flavour in ['none', 'full']) {
        for (final options in [_options(), _options(humanoid: true)]) {
          cases.add(
            _case('pointOf', [
              from,
              to,
              options,
              _frame(flavour, skeleton),
              bone,
            ], _pointed),
          );
        }
      }
    }
    cases.add(
      _case('pinned', [
        from,
        to,
        _options(),
        <String, Object?>{},
        names.firstOrNull ?? 'nowhere',
      ], _pinned),
    );
    cases.add(
      _case('pinned', [
        from,
        to,
        _options(),
        <String, Object?>{},
        'nowhere',
      ], _pinned),
    );
  }
  return cases;
}
