import 'dart:math' as math;

import 'package:orblit_effect/orblit_effect.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

List<Object> _nums(List<double> values) => [for (final v in values) number(v)];

double _num(Object? value) =>
    value is num ? value.toDouble() : double.parse(value! as String);

List<double> _list(Object? value) => [
  for (final v in value! as List<Object?>) _num(v),
];

Vector3 _vector3(Object? value) {
  final v = _list(value);
  return Vector3(v[0], v[1], v[2]);
}

List<Object> _vector(Vector3 v) => _nums([v.x, v.y, v.z]);

Map<String, Object?> _change(Change c) => {
  'move': _vector(c.move),
  'turn': _nums([c.turn.x, c.turn.y, c.turn.z, c.turn.w]),
  'grow': _vector(c.grow),
  'tint': _nums([c.tint.x, c.tint.y, c.tint.z, c.tint.w]),
  'fade': number(c.fade),
  'isNothing': c.isNothing,
};

final _curves = <String, Ease>{
  'linear': Eases.linear,
  'inQuad': Eases.inQuad,
  'outQuad': Eases.outQuad,
  'inCubic': Eases.inCubic,
  'outCubic': Eases.outCubic,
  'inOutQuad': Eases.inOutQuad,
  'inOutCubic': Eases.inOutCubic,
  'outBack': Eases.outBack,
  'outElastic': Eases.outElastic,
  'outBounce': Eases.outBounce,
};

/// A curve by name, with `reversed:` and `thereAndBack:` wrapping another.
Ease _ease(String name) {
  final colon = name.indexOf(':');
  if (colon < 0) return _curves[name]!;
  final inner = _ease(name.substring(colon + 1));
  return switch (name.substring(0, colon)) {
    'reversed' => Eases.reversed(inner),
    'thereAndBack' => Eases.thereAndBack(inner),
    _ => throw ArgumentError.value(name, 'name'),
  };
}

Ease? _easeOf(Map<String, Object?> spec) {
  final name = spec['ease'];
  return name == null ? null : _ease(name as String);
}

/// Builds the effect a spec describes. A null `ease` leaves the effect's own
/// default in place, so the defaults are part of what the fixture checks.
Effect _build(Map<String, Object?> spec) {
  final ease = _easeOf(spec);
  // Only the leaf kinds carry one, so it is read when a leaf asks.
  final duration = spec.containsKey('duration') ? _num(spec['duration']) : 0.0;
  Effect inner() => _build(spec['inner']! as Map<String, Object?>);
  return switch (spec['k']) {
    'wait' => Wait(duration),
    'move' =>
      ease == null
          ? MoveBy(_vector3(spec['offset']), duration)
          : MoveBy(_vector3(spec['offset']), duration, ease: ease),
    'turn' => _buildTurn(spec, duration, ease),
    'grow' =>
      ease == null
          ? GrowTo(_vector3(spec['factor']), duration)
          : GrowTo(_vector3(spec['factor']), duration, ease: ease),
    'fade' =>
      ease == null
          ? FadeTo(_num(spec['opacity']), duration)
          : FadeTo(_num(spec['opacity']), duration, ease: ease),
    'tint' => _buildTint(spec, duration, ease),
    'shake' => _buildShake(spec, duration, ease),
    'then' => Then(_buildAll(spec['steps'])),
    'both' => Both(_buildAll(spec['parts'])),
    'again' => Again(inner(), times: spec['times'] as int?),
    'outAndBack' => OutAndBack(inner()),
    'after' => After(_num(spec['delay']), inner()),
    _ => throw ArgumentError.value(spec['k'], 'k'),
  };
}

List<Effect> _buildAll(Object? specs) => [
  for (final s in specs! as List<Object?>) _build(s! as Map<String, Object?>),
];

Effect _buildTurn(Map<String, Object?> spec, double duration, Ease? ease) {
  final axis = spec['axis'] == null ? null : _vector3(spec['axis']);
  final angle = _num(spec['angle']);
  return ease == null
      ? TurnBy(angle, duration: duration, axis: axis)
      : TurnBy(angle, duration: duration, axis: axis, ease: ease);
}

Effect _buildTint(Map<String, Object?> spec, double duration, Ease? ease) {
  final c = _list(spec['colour']);
  final colour = Vector4(c[0], c[1], c[2], c[3]);
  return ease == null
      ? TintTo(colour, duration)
      : TintTo(colour, duration, ease: ease);
}

Effect _buildShake(Map<String, Object?> spec, double duration, Ease? ease) {
  final strength = _num(spec['strength']);
  final rate = spec['rate'];
  final seed = spec['seed'];
  if (rate == null && seed == null && ease == null) {
    return Shake(strength, duration);
  }
  return Shake(
    strength,
    duration,
    rate: rate == null ? 24 : _num(rate),
    seed: seed as int? ?? 0,
    ease: ease ?? Eases.linear,
  );
}

Map<String, Object?> _wait(double duration) => {
  'k': 'wait',
  'duration': number(duration),
};

Map<String, Object?> _move(
  List<double> offset,
  double duration, [
  String? ease,
]) => {
  'k': 'move',
  'offset': _nums(offset),
  'duration': number(duration),
  'ease': ease,
};

Map<String, Object?> _turn(
  double angle,
  double duration, {
  List<double>? axis,
  String? ease,
}) => {
  'k': 'turn',
  'angle': number(angle),
  'duration': number(duration),
  'axis': axis == null ? null : _nums(axis),
  'ease': ease,
};

Map<String, Object?> _grow(
  List<double> factor,
  double duration, [
  String? ease,
]) => {
  'k': 'grow',
  'factor': _nums(factor),
  'duration': number(duration),
  'ease': ease,
};

Map<String, Object?> _fade(double opacity, double duration, [String? ease]) => {
  'k': 'fade',
  'opacity': number(opacity),
  'duration': number(duration),
  'ease': ease,
};

Map<String, Object?> _tint(
  List<double> colour,
  double duration, [
  String? ease,
]) => {
  'k': 'tint',
  'colour': _nums(colour),
  'duration': number(duration),
  'ease': ease,
};

Map<String, Object?> _shake(
  double strength,
  double duration, {
  double? rate,
  int? seed,
  String? ease,
}) => {
  'k': 'shake',
  'strength': number(strength),
  'duration': number(duration),
  'rate': rate == null ? null : number(rate),
  'seed': seed,
  'ease': ease,
};

Map<String, Object?> _then(List<Map<String, Object?>> steps) => {
  'k': 'then',
  'steps': steps,
};

Map<String, Object?> _both(List<Map<String, Object?>> parts) => {
  'k': 'both',
  'parts': parts,
};

Map<String, Object?> _again(Map<String, Object?> inner, {int? times}) => {
  'k': 'again',
  'inner': inner,
  'times': times,
};

Map<String, Object?> _outAndBack(Map<String, Object?> inner) => {
  'k': 'outAndBack',
  'inner': inner,
};

Map<String, Object?> _after(double delay, Map<String, Object?> inner) => {
  'k': 'after',
  'delay': number(delay),
  'inner': inner,
};

List<Map<String, Object?>> _eases() {
  const times = <double>[
    -1,
    -0.5,
    0,
    1e-9,
    0.1,
    0.25,
    1 / 3,
    0.4999999,
    0.5,
    0.5000001,
    0.6,
    0.75,
    0.9,
    1,
    1.5,
    2,
    _nan,
    _inf,
    -_inf,
  ];
  final names = _curves.keys.toList();
  return [
    for (final name in [
      ...names,
      for (final n in names) 'reversed:$n',
      for (final n in names) 'thereAndBack:$n',
    ])
      for (final t in times)
        {
          'op': 'ease',
          'name': name,
          't': number(t),
          'expect': number(_ease(name)(t)),
        },
  ];
}

/// One of each kind of effect, with the awkward values mixed in: a duration of
/// nothing or less, a zero axis, a repeat of none, a shake with a huge seed.
List<Map<String, Object?>> _specs() {
  final move = _move([10, -4, 2], 2);
  final turn = _turn(math.pi / 2, 2);
  return [
    _wait(0),
    _wait(1.5),
    _wait(-1),
    for (final name in _curves.keys) _move([10, -4, 2], 2, name),
    _move([10, -4, 2], 2),
    _move([0, 0, 0], 1),
    _move([5, 0, 0], 0),
    _move([5, 0, 0], -1),
    _move([1, 2, 3], _inf, 'linear'),
    _turn(math.pi / 2, 2, ease: 'linear'),
    turn,
    _turn(-3, 1.5, axis: [1, 0, 0]),
    _turn(7, 2, axis: [1, 2, 3], ease: 'outBack'),
    _turn(math.pi, 1, axis: [0, 0, 1], ease: 'linear'),
    _turn(2, 1, axis: [0, 0, 0]),
    _turn(1, 0),
    _grow([3, 3, 3], 2, 'linear'),
    _grow([0.5, 2, 1], 2),
    _grow([0.5, 2, 1], 0),
    _fade(0, 2),
    _fade(0.3, 2, 'outElastic'),
    _fade(2, 1, 'outBounce'),
    _tint([0.2, 0.5, 1, 0.5], 2),
    _tint([0, 0, 0, 0], 1, 'inOutCubic'),
    _shake(0.5, 2),
    _shake(0.5, 2, seed: 4),
    _shake(1, 1, seed: -7, rate: 60),
    _shake(1, 1, seed: 123456789, rate: 0.5, ease: 'outQuad'),
    _shake(2, 1e12, seed: 9223372036854775807),
    _shake(2, 1e12, seed: -9223372036854775808, rate: 1000),
    _shake(1, 0),
    _then([move, turn]),
    _then([_wait(1), _move([10, 0, 0], 1, 'linear')]),
    _then([]),
    _then([_then([_move([1, 0, 0], 1, 'linear')]), _wait(0.5), turn]),
    _both([move, _fade(0, 2), _grow([2, 2, 2], 1)]),
    _both([]),
    _again(_move([10, 0, 0], 1, 'linear'), times: 3),
    _again(_move([10, 0, 0], 1, 'linear')),
    _again(_move([10, 0, 0], 1, 'linear'), times: 0),
    _again(_wait(0), times: 2),
    _again(_shake(1, 0.5, seed: 3)),
    _outAndBack(_grow([2, 2, 2], 1, 'linear')),
    _outAndBack(move),
    _after(2, _move([10, 0, 0], 1, 'linear')),
    _after(-1, move),
    _then([
      _after(1, _both([move, _shake(0.2, 2, seed: 1)])),
      _again(_outAndBack(_fade(0.5, 0.5)), times: 2),
    ]),
  ];
}

Map<String, Object?> _sampled(Map<String, Object?> spec) {
  const times = <double>[
    -5,
    -0.001,
    0,
    0.1,
    0.25,
    0.5,
    0.999,
    1,
    1.0001,
    1.5,
    2,
    2.5,
    3,
    7.3,
    1000.5,
    1e6 + 0.123,
    1e11,
    _inf,
    _nan,
  ];
  final effect = _build(spec);
  final samples = <Map<String, Object?>>[];
  for (final t in times) {
    final at = <String, Object?>{'at': number(t)};
    try {
      at['expect'] = _change(effect.at(t));
      at['done'] = effect.doneBy(t);
    } on UnsupportedError {
      // A shake floors its clock, and a floor of NaN or infinity throws.
      at['throws'] = true;
    }
    samples.add(at);
  }
  return {
    'op': 'effect',
    'spec': spec,
    'duration': number(effect.duration),
    'samples': samples,
  };
}

Map<String, Object?> _step(
  Playing playing,
  Map<Effect, int> ids,
  Map<String, Object?> step,
) {
  final result = <String, Object?>{};
  switch (step['do']) {
    case 'start':
      final effect = _build(step['spec']! as Map<String, Object?>);
      ids[effect] = ids.length;
      playing.start(effect);
    case 'advance':
      final done = playing.advance(_num(step['by']));
      result['finished'] = [for (final e in done) ids[e]];
    case 'bake':
      result['baked'] = _change(playing.bake());
    case 'clear':
      playing.clear();
  }
  return {
    ...result,
    'now': number(playing.now),
    'length': playing.length,
    'settled': _change(playing.settled),
    'change': _change(playing.change),
  };
}

Map<String, Object?> _script(List<Map<String, Object?>> steps) {
  final playing = Playing();
  final ids = Map<Effect, int>.identity();
  return {
    'op': 'playing',
    'steps': steps,
    'expect': [for (final step in steps) _step(playing, ids, step)],
  };
}

Map<String, Object?> _start(Map<String, Object?> spec) => {
  'do': 'start',
  'spec': spec,
};

Map<String, Object?> _advance(double by) => {'do': 'advance', 'by': number(by)};

List<Map<String, Object?>> _scripts() {
  final slide = _move([10, 0, 0], 2, 'linear');
  return [
    _script([
      _start(slide),
      _start(_fade(0, 2)),
      for (var i = 0; i < 6; i++) _advance(0.5),
      {'do': 'bake'},
      _advance(1),
    ]),
    _script([
      _start(slide),
      _advance(1),
      _start(_move([0, 10, 0], 2, 'linear')),
      _advance(1),
      _advance(1),
      {'do': 'bake'},
      {'do': 'bake'},
    ]),
    _script([
      _start(_move([1, 0, 0], 1)),
      _start(_move([2, 0, 0], 1)),
      _start(_move([3, 0, 0], 2)),
      _advance(1),
      _advance(1),
    ]),
    _script([
      _start(_move([5, 0, 0], 0)),
      _start(_wait(0)),
      _advance(0),
      _start(_then([_wait(1), _grow([2, 2, 2], 1, 'linear')])),
      _advance(0.25),
      _advance(1.75),
    ]),
    _script([
      _start(_again(_move([10, 0, 0], 1, 'linear'))),
      _start(_shake(0.5, 3, seed: 8)),
      _advance(0.7),
      _advance(0.7),
      {'do': 'bake'},
      _advance(5),
      {'do': 'clear'},
      _advance(1),
    ]),
    _script([_start(slide), _advance(1.5), _advance(-0.5), _advance(1)]),
    _script([_advance(1), {'do': 'bake'}]),
  ];
}

/// Starting transforms for [_applied]: identity, moved, turned either side of
/// the branch in `Quaternion.fromRotation`, and scaled.
List<Matrix4> _starts() {
  Matrix4 compose(List<double> t, Quaternion q, List<double> s) =>
      Matrix4.compose(
        Vector3(t[0], t[1], t[2]),
        q,
        Vector3(s[0], s[1], s[2]),
      );
  final unit = [1.0, 1.0, 1.0];
  final none = [0.0, 0.0, 0.0];
  Quaternion about(List<double> axis, double angle) =>
      Quaternion.axisAngle(Vector3(axis[0], axis[1], axis[2]), angle);
  return [
    Matrix4.identity(),
    compose([1, 2, 3], Quaternion.identity(), unit),
    compose(none, about([0, 1, 0], 0.7), unit),
    compose(none, about([1, 0, 0], math.pi), unit),
    compose(none, about([0, 1, 0], math.pi), unit),
    compose(none, about([0, 0, 1], math.pi), unit),
    compose([4, 5, 6], about([1, 1, 0], 3), unit),
    compose([4, 5, 6], about([0, 1, 1], 2.9), unit),
    compose(none, Quaternion.identity(), [2, 3, 4]),
    compose([1, 1, 1], about([1, 2, 3], 1.1), [2, 3, 4]),
    compose(none, Quaternion.identity(), none),
  ];
}

List<Map<String, Object?>> _applied() {
  final changes = <Change>[
    Change.none,
    Change(move: Vector3(10, -2, 0.5)),
    Change(turn: Quaternion.axisAngle(Vector3(0, 1, 0), math.pi / 2)),
    Change(turn: Quaternion.axisAngle(Vector3.zero(), 1)),
    Change(grow: Vector3.all(2)),
    Change(grow: Vector3(0.5, 1, 3)),
    Change(
      move: Vector3(1, 2, 3),
      turn: Quaternion.axisAngle(Vector3(1, 1, 1), 2),
      grow: Vector3(2, 0.5, 1),
    ),
  ];
  return [
    for (final start in _starts())
      for (final change in changes)
        {
          'op': 'applied',
          'to': _nums([for (var i = 0; i < 16; i++) start.storage[i]]),
          'move': _vector(change.move),
          'turn': _nums([
            change.turn.x,
            change.turn.y,
            change.turn.z,
            change.turn.w,
          ]),
          'grow': _vector(change.grow),
          'expect': _nums([
            for (var i = 0; i < 16; i++) applied(start, change).storage[i],
          ]),
        },
  ];
}

List<Map<String, Object?>> _helpers() {
  const angles = <double>[
    0,
    0.1,
    1,
    math.pi / 2,
    math.pi,
    -math.pi,
    3 * math.pi,
    6.1,
    -6.1,
    100,
    -100,
    2 * math.pi,
    _nan,
    _inf,
  ];
  return [
    for (final from in angles)
      for (final to in angles)
        {
          'op': 'shortestTurn',
          'from': number(from),
          'to': number(to),
          'expect': number(shortestTurn(from, to)),
        },
    for (final from in const <double>[0, 10, -3.5, 1e9])
      for (final to in const <double>[20, 0, -1])
        for (final t in const <double>[0, 0.5, 1, -1, 2, _nan])
          {
            'op': 'lerp',
            'from': number(from),
            'to': number(to),
            't': number(t),
            'expect': number(lerp(from, to, t)),
          },
  ];
}

/// Usage: `dart run bin/effect.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_effect',
    name: 'effect',
    cases: [
      ..._eases(),
      for (final spec in _specs()) _sampled(spec),
      ..._scripts(),
      ..._applied(),
      ..._helpers(),
    ],
  );
}
