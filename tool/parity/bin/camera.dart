// Usage: `dart run bin/camera.dart <repository root>`
import 'package:orblit_camera/orblit_camera.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;
const _edges = <double>[_nan, _inf, -_inf, 0.0, -0.0, 1e-12, 1e300, -1e300];

/// Numbers that sit on a rounding edge of `toStringAsFixed`, or past where
/// Dart switches to an exponent.
const _printEdges = <double>[
  0.05,
  0.25,
  0.35,
  0.0005,
  1.0005,
  2.5,
  0.95,
  1e20,
  1e21,
  1e22,
  -1e21,
  -0.0001,
  123456789.987654321,
];

const _names = <String?>['c0', 'c1', 'c2', null, 'null', '*'];

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

  /// A value with two decimals, so a fixture line reads as a number.
  double between(int low, int high) => low + next((high - low) * 100 + 1) / 100;

  /// Like [between], now and then replaced by one of the awkward doubles.
  double wild(int low, int high) => chance(90) ? pick(_edges) : between(low, high);
}

/// Every double becomes what JSON can hold, however deep it sits.
Object? _encode(Object? v) => switch (v) {
  double() => number(v),
  List() => [for (final item in v) _encode(item)],
  Map() => <String, Object?>{
    for (final MapEntry(:key, :value) in v.entries)
      key as String: _encode(value),
  },
  _ => v,
};

Map<String, Object?> _map(Object? v) => (v! as Map).cast<String, Object?>();

double _n(Map<String, Object?> s, String key) => s[key]! as double;

List<double> _numbers(Object? v) => (v! as List).cast<double>();

Vector3 _readVec3(Object? v) {
  final l = _numbers(v);
  return Vector3(l[0], l[1], l[2]);
}

Vector3? _vOrNull(Map<String, Object?> s, String key) =>
    s[key] == null ? null : _readVec3(s[key]);

List<double> _writeVec3(Vector3 v) => [v.x, v.y, v.z];

Quaternion _readQuat(Object? v) {
  final l = _numbers(v);
  return Quaternion(l[0], l[1], l[2], l[3]);
}

List<double> _writeQuat(Quaternion q) => [q.x, q.y, q.z, q.w];

Map<String, Object?> _writeLens(Lens l) => {
  'fov': l.fieldOfView,
  'near': l.near,
  'far': l.far,
  'dutch': l.dutch,
  'ortho': l.orthographic,
  'height': l.height,
};

Lens _readLens(Map<String, Object?> s) => Lens(
  fieldOfView: _n(s, 'fov'),
  near: _n(s, 'near'),
  far: _n(s, 'far'),
  dutch: _n(s, 'dutch'),
  orthographic: s['ortho']! as bool,
  height: _n(s, 'height'),
);

List<double> _writeRect(ScreenRect r) => [r.left, r.top, r.width, r.height];

Object? _writeGuides(CameraGuides? g) => g == null
    ? null
    : {
        'screenX': g.screenX,
        'screenY': g.screenY,
        'dead': _writeRect(g.dead),
        'soft': _writeRect(g.soft),
      };

Object? _writeTarget(CameraTarget? t) => t == null
    ? null
    : {'position': _writeVec3(t.position), 'rotation': _writeQuat(t.rotation)};

FixedTarget? _readTarget(Object? v) {
  if (v == null) {
    return null;
  }
  final s = _map(v);
  return FixedTarget(_readVec3(s['position']), _readQuat(s['rotation']));
}

Map<String, Object?> _writeBlend(Blend b) => {
  'style': b.style.name,
  'duration': b.duration,
};

Blend _readBlend(Map<String, Object?> s) =>
    Blend(BlendStyle.values.byName(s['style']! as String), _n(s, 'duration'));

/// Half the quaternions are unit length, as a rotation is. The rest are not,
/// which is what a normalise is there for.
List<double> _quatSpec(_Rng r) {
  final q = [for (var i = 0; i < 4; i++) r.wild(-1, 1)];
  if (!r.chance(2)) {
    return q;
  }
  final unit = Quaternion(q[0], q[1], q[2], q[3])..normalize();
  return [unit.x, unit.y, unit.z, unit.w];
}

List<double> _vec(_Rng r, int low, int high) => [
  for (var i = 0; i < 3; i++) r.wild(low, high),
];

List<double> _tame(_Rng r, int low, int high) => [
  for (var i = 0; i < 3; i++) r.between(low, high),
];

double _delta(_Rng r) => r.chance(4)
    ? 1 / 60
    : r.chance(12)
    ? r.pick([0.0, -0.5, 1e6])
    : r.wild(0, 1);

double _aspect(_Rng r) => r.chance(4)
    ? 16 / 9
    : r.chance(10)
    ? r.pick([0.0, 1.0, _nan, -2.0])
    : r.between(1, 3);

Map<String, Object?> _lensSpec(_Rng r) => {
  'fov': r.chance(15) ? r.pick(_edges) : r.between(20, 120),
  'near': r.between(0, 1),
  'far': r.between(100, 2000),
  'dutch': r.between(-30, 30),
  'ortho': r.chance(4),
  'height': r.chance(15) ? r.pick(_edges) : r.between(1, 30),
};

Map<String, Object?> _targetSpec(_Rng r) => {
  'position': _tame(r, -10, 10),
  'rotation': _quatSpec(r),
};

/// A subject that sits in front of a camera at the origin looking down the
/// negative Z axis, which is where a framing rule has something to do.
Map<String, Object?> _framedTarget(_Rng r) => {
  'position': [r.between(-3, 3), r.between(-2, 2), -r.between(2, 20)],
  'rotation': _quatSpec(r),
};

const _identity = [0.0, 0.0, 0.0, 1.0];

Map<String, Object?> _case(
  String op,
  Map<String, Object?> spec,
  Object? Function() run,
) {
  Object? result;
  try {
    result = run();
  } on ArgumentError {
    result = {'error': 'argument'};
  }
  return {'op': op, 'spec': spec, 'result': result};
}

Map<String, Object?> _dampingCase(_Rng r) {
  final spec = {
    'current': r.wild(-9, 9),
    'target': r.wild(-9, 9),
    'damping': r.chance(5) ? r.pick([0.0, -1.0, 1e-12]) : r.wild(0, 3),
    'delta': _delta(r),
  };
  return _case(
    'damping',
    spec,
    () => {
      'factor': dampingFactor(_n(spec, 'damping'), _n(spec, 'delta')),
      'damped': damp(
        _n(spec, 'current'),
        _n(spec, 'target'),
        _n(spec, 'damping'),
        _n(spec, 'delta'),
      ),
    },
  );
}

Map<String, Object?> _lensCase(_Rng r) {
  final spec = {
    'a': _lensSpec(r),
    'b': _lensSpec(r),
    't': r.chance(5) ? r.pick([0.0, 1.0, -0.5, 1.5]) : r.wild(0, 1),
  };
  return _case('lens', spec, () {
    final a = _readLens(_map(spec['a']));
    final b = _readLens(_map(spec['b']));
    return {
      'lerp': _writeLens(Lens.lerp(a, b, _n(spec, 't'))),
      'a': a.toString(),
      'b': b.toString(),
    };
  });
}

Map<String, Object?> _printedLensCase(_Rng r) {
  final spec = {
    'a': {
      ..._lensSpec(r),
      'fov': r.pick(_printEdges),
      'ortho': false,
    },
    'b': {
      ..._lensSpec(r),
      'height': r.pick(_printEdges),
      'ortho': true,
    },
    't': 0.5,
  };
  return _case('lens', spec, () {
    final a = _readLens(_map(spec['a']));
    final b = _readLens(_map(spec['b']));
    return {
      'lerp': _writeLens(Lens.lerp(a, b, 0.5)),
      'a': a.toString(),
      'b': b.toString(),
    };
  });
}

Map<String, Object?> _flatCase(_Rng r) {
  final spec = {
    'height': r.wild(1, 30),
    'near': r.chance(3) ? null : r.between(-200, 0),
    'far': r.chance(3) ? null : r.between(1, 300),
    'dutch': r.chance(3) ? null : r.between(-30, 30),
  };
  return _case('flat', spec, () {
    final height = _n(spec, 'height');
    final lens = spec['near'] == null
        ? Lens.flat(height: height)
        : Lens.flat(
            height: height,
            near: _n(spec, 'near'),
            far: spec['far'] == null ? 100 : _n(spec, 'far'),
            dutch: spec['dutch'] == null ? 0 : _n(spec, 'dutch'),
          );
    return {'lens': _writeLens(lens), 'text': lens.toString()};
  });
}

Map<String, Object?> _defaultLensCase() => _case('defaultLens', {}, () {
  const lens = Lens();
  return {'lens': _writeLens(lens), 'text': lens.toString()};
});

Map<String, Object?> _rectCase(_Rng r) {
  final printed = r.chance(2);
  final spec = {
    'left': printed ? r.pick(_printEdges) : r.wild(-2, 2),
    'top': printed ? r.pick(_printEdges) : r.wild(-2, 2),
    'width': printed ? r.pick(_printEdges) : r.wild(0, 3),
    'height': printed ? r.pick(_printEdges) : r.wild(0, 3),
    'x': r.wild(-2, 3),
    'y': r.wild(-2, 3),
  };
  return _case('rect', spec, () {
    final rect = ScreenRect(
      _n(spec, 'left'),
      _n(spec, 'top'),
      _n(spec, 'width'),
      _n(spec, 'height'),
    );
    return {
      'right': rect.right,
      'bottom': rect.bottom,
      'contains': rect.contains(_n(spec, 'x'), _n(spec, 'y')),
      'text': rect.toString(),
    };
  });
}

Map<String, Object?> _blendCase(_Rng r) {
  final cut = r.chance(6);
  final spec = {
    'style': cut ? 'cut' : r.pick(BlendStyle.values).name,
    'duration': cut ? 0.0 : r.wild(0, 3),
    'ctor': cut && r.chance(2),
    't': r.chance(5) ? r.pick([0.0, 1.0, -0.5, 1.5, 0.5]) : r.wild(-1, 2),
  };
  return _case('blend', spec, () {
    final blend = spec['ctor']! as bool
        ? const Blend.cut()
        : _readBlend(spec);
    return {'blend': _writeBlend(blend), 'ease': blend.ease(_n(spec, 't'))};
  });
}

Map<String, Object?> _tableCase(_Rng r) {
  Map<String, Object?> pair() => {'from': r.pick(_names), 'to': r.pick(_names)};
  Map<String, Object?> blend() => {
    'style': r.pick(BlendStyle.values).name,
    'duration': r.between(0, 3),
  };
  final spec = {
    'default': r.chance(3) ? blend() : null,
    'rules': [
      for (var i = 0; i < r.next(5); i++) {...pair(), 'blend': blend()},
    ],
    'queries': [for (var i = 0; i < 6; i++) pair()],
  };
  return _case('table', spec, () {
    final fallback = spec['default'];
    final table = fallback == null
        ? BlendTable()
        : BlendTable(defaultBlend: _readBlend(_map(fallback)));
    for (final rule in (spec['rules']! as List).map(_map)) {
      table.set(
        rule['from'] as String?,
        rule['to'] as String?,
        _readBlend(_map(rule['blend'])),
      );
    }
    return {
      'default': _writeBlend(table.defaultBlend),
      'found': [
        for (final q in (spec['queries']! as List).map(_map))
          _writeBlend(table.between(q['from'] as String?, q['to'] as String?)),
      ],
    };
  });
}

Map<String, Object?> _rotateCase(_Rng r) {
  final spec = {'q': _quatSpec(r), 'v': _vec(r, -9, 9)};
  return _case(
    'rotate',
    spec,
    () => _writeVec3(rotateVector(_readQuat(spec['q']), _readVec3(spec['v']))),
  );
}

Map<String, Object?> _slerpCase(_Rng r) {
  final a = _quatSpec(r);
  final b = switch (r.next(5)) {
    0 => [for (final x in a) x + r.between(-1, 1) * 0.001],
    1 => [for (final x in a) -x],
    2 => a,
    _ => _quatSpec(r),
  };
  final spec = {
    'a': a,
    'b': b,
    't': r.chance(5) ? r.pick([0.0, 1.0, -0.5, 1.5]) : r.wild(0, 1),
  };
  return _case(
    'slerp',
    spec,
    () => _writeQuat(
      slerpShortest(_readQuat(spec['a']), _readQuat(spec['b']), _n(spec, 't')),
    ),
  );
}

Map<String, Object?> _lookCase(_Rng r) {
  final up = r.chance(3) ? null : _vec(r, -1, 1);
  final forward = switch (r.next(6)) {
    0 => [0.0, 0.0, 0.0],
    1 => [0.0, 1.0, 0.0],
    2 => [0.0, -3.0, 0.0],
    3 when up != null => [for (final x in up) x * 2],
    _ => _vec(r, -9, 9),
  };
  final spec = {'forward': forward, 'up': up};
  return _case(
    'look',
    spec,
    () => _writeQuat(
      lookRotation(_readVec3(spec['forward']), _vOrNull(spec, 'up')),
    ),
  );
}

Map<String, Object?> _projectCase(_Rng r) {
  final position = _tame(r, -5, 5);
  final spec = {
    'position': position,
    'rotation': r.chance(2) ? _identity : _quatSpec(r),
    'point': r.chance(8) ? position : _tame(r, -9, 9),
    'lens': _lensSpec(r),
    'aspect': _aspect(r),
  };
  return _case('project', spec, () {
    final seen = project(
      _readVec3(spec['position']),
      _readQuat(spec['rotation']),
      _readVec3(spec['point']),
      lens: _readLens(_map(spec['lens'])),
      aspect: _n(spec, 'aspect'),
    );
    return {'x': seen.x, 'y': seen.y, 'inFront': seen.inFront};
  });
}

Map<String, Object?> _placingCase(_Rng r) {
  final position = _tame(r, -5, 5);
  final spec = {
    'position': position,
    'point': r.chance(8) ? position : _tame(r, -9, 9),
    'ndcX': r.wild(-1, 1),
    'ndcY': r.wild(-1, 1),
    'lens': _lensSpec(r),
    'aspect': _aspect(r),
    'up': r.chance(3) ? null : _vec(r, -1, 1),
  };
  return _case(
    'placing',
    spec,
    () => _writeQuat(
      rotationPlacing(
        _readVec3(spec['position']),
        _readVec3(spec['point']),
        ndcX: _n(spec, 'ndcX'),
        ndcY: _n(spec, 'ndcY'),
        lens: _readLens(_map(spec['lens'])),
        aspect: _n(spec, 'aspect'),
        up: _vOrNull(spec, 'up'),
      ),
    ),
  );
}

double _zone(_Rng r) => r.chance(8) ? -r.between(1, 5) / 10 : r.between(0, 8) / 10;

Map<String, Object?> _aimSpec(_Rng r, String kind) {
  final defaults = r.chance(5);
  return switch (kind) {
    'static' => {'kind': kind, 'rotation': _quatSpec(r)},
    'hard' => {'kind': kind, 'up': r.chance(2) ? null : _vec(r, -1, 1)},
    'composer' => {
      'kind': kind,
      'defaults': defaults,
      'screenX': r.between(2, 8) / 10,
      'screenY': r.between(2, 8) / 10,
      'deadZoneWidth': _zone(r),
      'deadZoneHeight': _zone(r),
      'softZoneWidth': _zone(r),
      'softZoneHeight': _zone(r),
      'damping': r.chance(4) ? 0.0 : r.between(0, 2),
      'up': r.chance(3) ? _vec(r, -1, 1) : null,
    },
    'head' => {
      'kind': kind,
      'defaults': defaults,
      'damping': r.chance(2) ? 0.0 : r.between(0, 2),
      'tilt': r.between(-30, 30),
    },
    _ => {
      'kind': kind,
      'defaults': defaults,
      'yaw': r.between(-400, 400),
      'pitch': r.between(-120, 120),
      'minimumPitch': r.chance(10) ? r.between(0, 60) : r.between(-90, 0),
      'maximumPitch': r.chance(10) ? r.between(-90, -10) : r.between(0, 90),
      'damping': r.chance(2) ? 0.0 : r.between(0, 2),
    },
  };
}

CameraAim _aim(Map<String, Object?> s) {
  final defaults = s['defaults'] == true;
  return switch (s['kind']) {
    'static' => StaticAim(_readQuat(s['rotation'])),
    'hard' => HardLookAt(up: _vOrNull(s, 'up')),
    'composer' when defaults => ComposerAim(),
    'composer' => ComposerAim(
      screenX: _n(s, 'screenX'),
      screenY: _n(s, 'screenY'),
      deadZoneWidth: _n(s, 'deadZoneWidth'),
      deadZoneHeight: _n(s, 'deadZoneHeight'),
      softZoneWidth: _n(s, 'softZoneWidth'),
      softZoneHeight: _n(s, 'softZoneHeight'),
      damping: _n(s, 'damping'),
      up: _vOrNull(s, 'up'),
    ),
    'head' when defaults => HeadAim(),
    'head' => HeadAim(damping: _n(s, 'damping'), tilt: _n(s, 'tilt')),
    _ when defaults => PovAim(),
    _ => PovAim(
      yaw: _n(s, 'yaw'),
      pitch: _n(s, 'pitch'),
      minimumPitch: _n(s, 'minimumPitch'),
      maximumPitch: _n(s, 'maximumPitch'),
      damping: _n(s, 'damping'),
    ),
  };
}

const _aimKinds = ['static', 'hard', 'composer', 'head', 'pov'];

Map<String, Object?> _aimCase(_Rng r, String kind) {
  final framed = r.chance(2);
  final spec = {
    'aim': _aimSpec(r, kind),
    'current': framed ? _identity : _quatSpec(r),
    'position': framed ? [0.0, 0.0, 0.0] : _tame(r, -5, 5),
    'lookAt': r.chance(8)
        ? null
        : framed
        ? _framedTarget(r)
        : _targetSpec(r),
    'lens': _lensSpec(r),
    'aspect': _aspect(r),
    'delta': _delta(r),
    'frames': 1 + r.next(4),
  };
  return _case('aim', spec, () {
    final aim = _aim(_map(spec['aim']));
    final lookAt = _readTarget(spec['lookAt']);
    final current = _readQuat(spec['current']);
    final position = _readVec3(spec['position']);
    final lens = _readLens(_map(spec['lens']));
    final rotations = <List<double>>[];
    Object? currentAfter;
    var turn = current;
    for (var i = 0; i < (spec['frames']! as int); i++) {
      turn = aim.solve(
        turn,
        position,
        lookAt,
        lens: lens,
        aspect: _n(spec, 'aspect'),
        delta: _n(spec, 'delta'),
      );
      currentAfter ??= _writeQuat(current);
      rotations.add(_writeQuat(turn));
    }
    return {
      'rotations': rotations,
      'guides': _writeGuides(aim.guides),
      'currentAfter': currentAfter,
      'lookAtAfter': _writeTarget(lookAt),
    };
  });
}

Map<String, Object?> _probeSpec(_Rng r) => switch (r.next(3)) {
  0 => {'kind': 'open'},
  1 => {
    'kind': 'wall',
    'z': r.between(-4, 4),
    'minX': r.between(-4, 0),
    'maxX': r.between(0, 4),
  },
  _ => {
    'kind': 'fixed',
    'distance': r.chance(4) ? null : r.pick([..._edgeDistances, r.between(0, 8)]),
  },
};

const _edgeDistances = <double>[0.0, -1.0, 1e300, 100.0, _nan, -0.0];

double? _wall(
  Vector3 from,
  Vector3 to,
  double radius,
  Map<String, Object?> s,
) {
  final z = _n(s, 'z');
  final stand = z + radius * (from.z > z ? 1 : -1);
  final run = to.z - from.z;
  if ((from.z - z).abs() <= radius) {
    return 0;
  }
  if (run.abs() < 1e-9) {
    return null;
  }
  final share = (stand - from.z) / run;
  if (share < 0 || share > 1) {
    return null;
  }
  final x = from.x + (to.x - from.x) * share;
  if (x < _n(s, 'minX') || x > _n(s, 'maxX')) {
    return null;
  }
  return share * (to - from).length;
}

/// A probe that notes every ask, so a camera that casts a different ball to
/// a different place shows up even when the answer happens to be the same.
ArmProbe _probe(Map<String, Object?> s, List<Object?> log) =>
    (from, to, radius) {
      log.add([_writeVec3(from), _writeVec3(to), radius]);
      return switch (s['kind']) {
        'wall' => _wall(from, to, radius, s),
        'fixed' => s['distance'] as double?,
        _ => null,
      };
    };

const _bodyKinds = ['static', 'follow', 'orbit', 'framing', 'screen', 'arm'];

Map<String, Object?> _bodySpec(_Rng r, String kind, {bool nested = false}) {
  final defaults = r.chance(5);
  return switch (kind) {
    'static' => {'kind': kind, 'position': _tame(r, -5, 5)},
    'follow' => {
      'kind': kind,
      'offset': _tame(r, -6, 6),
      'binding': r.pick(FollowBinding.values).name,
      'damping': r.chance(3) ? null : _tame(r, 0, 2),
    },
    'orbit' => {
      'kind': kind,
      'defaults': defaults,
      'radius': r.wild(1, 12),
      'azimuth': r.between(-400, 400),
      'elevation': r.between(-120, 120),
      'damping': r.chance(3) ? 0.0 : r.between(0, 2),
      'height': r.between(-1, 3),
    },
    'framing' => {
      'kind': kind,
      'defaults': defaults,
      'viewDirection': r.chance(8) ? [0.0, 0.0, 0.0] : _tame(r, -3, 3),
      'distance': r.between(0, 50),
      'minimum': r.chance(10) ? r.between(10, 20) : r.between(1, 5),
      'maximum': r.chance(10) ? r.between(0, 5) : r.between(6, 50),
      'damping': r.chance(3) ? 0.0 : r.between(0, 2),
    },
    'screen' => {
      'kind': kind,
      'defaults': defaults,
      'distance': r.between(1, 20),
      'screenX': r.between(2, 8) / 10,
      'screenY': r.between(2, 8) / 10,
      'deadZoneWidth': _zone(r),
      'deadZoneHeight': _zone(r),
      'softZoneWidth': _zone(r),
      'softZoneHeight': _zone(r),
      'damping': r.chance(3) ? 0.0 : r.between(0, 2),
      'bounds': r.chance(2)
          ? {'minimum': _tame(r, -8, -1), 'maximum': _tame(r, 1, 8)}
          : null,
    },
    _ => {
      'kind': kind,
      'defaults': defaults,
      'inner': _bodySpec(r, nested ? 'static' : r.pick(_bodyKinds.sublist(0, 5))),
      'probe': _probeSpec(r),
      'radius': r.between(0, 1),
      'height': r.between(0, 3),
    },
  };
}

CameraBody _body(Map<String, Object?> s, List<Object?> log) {
  final defaults = s['defaults'] == true;
  return switch (s['kind']) {
    'static' => StaticBody(_readVec3(s['position'])),
    'follow' => FollowBody(
      offset: _readVec3(s['offset']),
      binding: FollowBinding.values.byName(s['binding']! as String),
      damping: _vOrNull(s, 'damping'),
    ),
    'orbit' when defaults => OrbitBody(),
    'orbit' => OrbitBody(
      radius: _n(s, 'radius'),
      azimuth: _n(s, 'azimuth'),
      elevation: _n(s, 'elevation'),
      damping: _n(s, 'damping'),
      height: _n(s, 'height'),
    ),
    'framing' when defaults => FramingBody(
      viewDirection: _readVec3(s['viewDirection']),
    ),
    'framing' => FramingBody(
      viewDirection: _readVec3(s['viewDirection']),
      distance: _n(s, 'distance'),
      minimum: _n(s, 'minimum'),
      maximum: _n(s, 'maximum'),
      damping: _n(s, 'damping'),
    ),
    'screen' when defaults => ScreenFollowBody(),
    'screen' => ScreenFollowBody(
      distance: _n(s, 'distance'),
      screenX: _n(s, 'screenX'),
      screenY: _n(s, 'screenY'),
      deadZoneWidth: _n(s, 'deadZoneWidth'),
      deadZoneHeight: _n(s, 'deadZoneHeight'),
      softZoneWidth: _n(s, 'softZoneWidth'),
      softZoneHeight: _n(s, 'softZoneHeight'),
      damping: _n(s, 'damping'),
      bounds: s['bounds'] == null
          ? null
          : (
              minimum: _readVec3(_map(s['bounds'])['minimum']),
              maximum: _readVec3(_map(s['bounds'])['maximum']),
            ),
    ),
    _ when defaults => ArmBody(
      inner: _body(_map(s['inner']), log),
      probe: _probe(_map(s['probe']), log),
    ),
    _ => ArmBody(
      inner: _body(_map(s['inner']), log),
      probe: _probe(_map(s['probe']), log),
      radius: _n(s, 'radius'),
      height: _n(s, 'height'),
    ),
  };
}

Map<String, Object?> _bodyCase(_Rng r, String kind) {
  final framed = r.chance(2);
  final spec = {
    'body': _bodySpec(r, kind),
    'current': _tame(r, -8, 8),
    'follow': r.chance(8) ? null : framed ? _framedTarget(r) : _targetSpec(r),
    'rotation': framed ? _identity : _quatSpec(r),
    'lens': _lensSpec(r),
    'aspect': _aspect(r),
    'delta': _delta(r),
    'frames': 1 + r.next(4),
  };
  return _case('body', spec, () {
    final log = <Object?>[];
    final body = _body(_map(spec['body']), log);
    final follow = _readTarget(spec['follow']);
    final rotation = _readQuat(spec['rotation']);
    final lens = _readLens(_map(spec['lens']));
    final current = _readVec3(spec['current']);
    final positions = <List<double>>[];
    Object? currentAfter;
    var place = current;
    for (var i = 0; i < (spec['frames']! as int); i++) {
      place = body.solve(
        place,
        follow,
        rotation: rotation,
        lens: lens,
        aspect: _n(spec, 'aspect'),
        delta: _n(spec, 'delta'),
      );
      currentAfter ??= _writeVec3(current);
      positions.add(_writeVec3(place));
    }
    return {
      'positions': positions,
      'guides': _writeGuides(body.guides),
      'probes': log,
      'currentAfter': currentAfter,
      'followAfter': _writeTarget(follow),
    };
  });
}

Map<String, Object?> _noiseSpec(_Rng r) {
  final defaults = r.chance(4);
  List<double>? maybe(int low, int high) =>
      defaults || r.chance(3) ? null : _tame(r, low, high);
  return {
    'positionAmplitude': maybe(0, 2),
    'positionFrequency': maybe(0, 3),
    'rotationAmplitude': maybe(0, 5),
    'rotationFrequency': maybe(0, 3),
    'seed': defaults ? 0 : r.pick([0, 1, -3, 7, 12345, 1 << 40, -(1 << 33)]),
  };
}

CameraNoise _noise(Map<String, Object?> s) => CameraNoise(
  positionAmplitude: _vOrNull(s, 'positionAmplitude'),
  positionFrequency: _vOrNull(s, 'positionFrequency'),
  rotationAmplitude: _vOrNull(s, 'rotationAmplitude'),
  rotationFrequency: _vOrNull(s, 'rotationFrequency'),
  seed: s['seed']! as int,
);

Map<String, Object?> _noiseCase(_Rng r) {
  final spec = {
    'noise': _noiseSpec(r),
    'times': [
      for (var i = 0; i < 6; i++)
        r.chance(15) ? r.pick([..._edgeTimes]) : r.between(-10, 200),
    ],
  };
  return _case('noise', spec, () {
    final noise = _noise(_map(spec['noise']));
    return {
      'fields': {
        'positionAmplitude': _writeVec3(noise.positionAmplitude),
        'positionFrequency': _writeVec3(noise.positionFrequency),
        'rotationAmplitude': _writeVec3(noise.rotationAmplitude),
        'rotationFrequency': _writeVec3(noise.rotationFrequency),
        'seed': noise.seed,
      },
      'samples': [
        for (final time in _numbers(spec['times']))
          {
            'position': _writeVec3(noise.positionAt(time)),
            'rotation': _writeVec3(noise.rotationAt(time)),
          },
      ],
    };
  });
}

const _edgeTimes = <double>[0.0, -0.0, 1e6, _nan, _inf];

Map<String, Object?> _cameraSpec(_Rng r, int index) {
  final targets = <int?>[null, 0, 1];
  return {
    'name': 'c$index',
    'priority': r.next(20),
    'enabled': !r.chance(6),
    'lens': r.chance(2) ? null : _lensSpec(r),
    'follow': r.pick(targets),
    'lookAt': r.pick(targets),
    'body': r.chance(3) ? null : _bodySpec(r, r.pick(_bodyKinds)),
    'aim': r.chance(3) ? null : _aimSpec(r, r.pick(_aimKinds)),
    'noise': r.chance(3) ? _noiseSpec(r) : null,
    'position': r.chance(2) ? null : _tame(r, -5, 5),
    'rotation': r.chance(2) ? null : _quatSpec(r),
  };
}

VirtualCamera _camera(Map<String, Object?> s, List<FixedTarget> targets) {
  final follow = s['follow'] as int?;
  final lookAt = s['lookAt'] as int?;
  final lens = s['lens'];
  final body = s['body'];
  final aim = s['aim'];
  final noise = s['noise'];
  return VirtualCamera(
    name: s['name']! as String,
    priority: s['priority']! as int,
    enabled: s['enabled']! as bool,
    lens: lens == null ? const Lens() : _readLens(_map(lens)),
    follow: follow == null ? null : targets[follow],
    lookAt: lookAt == null ? null : targets[lookAt],
    body: body == null ? null : _body(_map(body), []),
    aim: aim == null ? null : _aim(_map(aim)),
    noise: noise == null ? null : _noise(_map(noise)),
    position: _vOrNull(s, 'position'),
    rotation: s['rotation'] == null ? null : _readQuat(s['rotation']),
  );
}

Map<String, Object?> _stepSpec(_Rng r, int cameras) => switch (r.next(10)) {
  0 => {'do': 'snap'},
  1 => {'do': 'priority', 'camera': r.next(cameras), 'value': r.next(20)},
  2 => {'do': 'enable', 'camera': r.next(cameras), 'value': r.chance(2)},
  3 => {'do': 'remove', 'camera': r.next(cameras)},
  4 => {'do': 'target', 'index': r.next(2), 'position': _tame(r, -10, 10)},
  5 => {'do': 'aspect', 'value': r.chance(2) ? r.pick([0.0, _nan, _inf, -1.0]) : r.between(1, 3)},
  _ => {'do': 'update', 'delta': r.pick([1 / 60, 1 / 60, 0.1, 0.25, 0.5, 0.0])},
};

Map<String, Object?> _snapshot(CameraBrain brain) => {
  'live': brain.live?.name,
  'progress': brain.blendProgress,
  'blending': brain.isBlending,
  'aspect': brain.aspect,
  'position': _writeVec3(brain.state.position),
  'rotation': _writeQuat(brain.state.rotation),
  'lens': _writeLens(brain.state.lens),
  'cameras': [
    for (final camera in brain.cameras)
      {
        'name': camera.name,
        'position': _writeVec3(camera.state.position),
        'rotation': _writeQuat(camera.state.rotation),
        'guides': _writeGuides(camera.guides),
      },
  ],
};

void _step(
  CameraBrain brain,
  Map<String, Object?> s,
  List<VirtualCamera> cameras,
  List<FixedTarget> targets,
) {
  final camera = s['camera'] == null ? null : cameras[s['camera']! as int];
  switch (s['do']) {
    case 'snap':
      brain.snap();
    case 'priority':
      camera!.priority = s['value']! as int;
    case 'enable':
      camera!.enabled = s['value']! as bool;
    case 'remove':
      brain.remove(camera!);
    case 'target':
      targets[s['index']! as int].position = _readVec3(s['position']);
    case 'aspect':
      brain.aspect = _n(s, 'value');
    default:
      brain.update(_n(s, 'delta'));
  }
}

Map<String, Object?> _brainCase(_Rng r) {
  final count = 1 + r.next(3);
  final spec = {
    'aspect': r.chance(8) ? r.pick([0.0, 1.0, 2.35]) : null,
    'default': r.chance(3) ? {'style': 'linear', 'duration': 0.5} : null,
    'targets': [_targetSpec(r), _targetSpec(r)],
    'cameras': [for (var i = 0; i < count; i++) _cameraSpec(r, i)],
    'rules': [
      for (var i = 0; i < r.next(3); i++)
        {
          'from': r.pick(_names),
          'to': r.pick(_names),
          'blend': {
            'style': r.pick(BlendStyle.values).name,
            'duration': r.between(0, 2),
          },
        },
    ],
    'initial': 1 + r.next(count),
    'steps': [for (var i = 0; i < 4 + r.next(12); i++) _stepSpec(r, count)],
  };
  return _case('brain', spec, () {
    final targets = [
      for (final t in (spec['targets']! as List)) _readTarget(t)!,
    ];
    final cameras = [
      for (final c in (spec['cameras']! as List)) _camera(_map(c), targets),
    ];
    final fallback = spec['default'];
    final table = fallback == null
        ? BlendTable()
        : BlendTable(defaultBlend: _readBlend(_map(fallback)));
    for (final rule in (spec['rules']! as List).map(_map)) {
      table.set(
        rule['from'] as String?,
        rule['to'] as String?,
        _readBlend(_map(rule['blend'])),
      );
    }
    final brain = spec['aspect'] == null
        ? CameraBrain(blends: table)
        : CameraBrain(aspect: _n(spec, 'aspect'), blends: table);
    for (final camera in cameras.take(spec['initial']! as int)) {
      brain.add(camera);
    }
    return {
      'start': _snapshot(brain),
      'steps': [
        for (final s in (spec['steps']! as List).map(_map))
          () {
            _step(brain, s, cameras, targets);
            return _snapshot(brain);
          }(),
      ],
    };
  });
}

void main(List<String> args) {
  final r = _Rng(11);
  final cases = <Map<String, Object?>>[
    _defaultLensCase(),
    for (var i = 0; i < 120; i++) _dampingCase(r),
    for (var i = 0; i < 60; i++) _lensCase(r),
    for (var i = 0; i < 30; i++) _printedLensCase(r),
    for (var i = 0; i < 40; i++) _flatCase(r),
    for (var i = 0; i < 60; i++) _rectCase(r),
    for (var i = 0; i < 60; i++) _blendCase(r),
    for (var i = 0; i < 50; i++) _tableCase(r),
    for (var i = 0; i < 80; i++) _rotateCase(r),
    for (var i = 0; i < 120; i++) _slerpCase(r),
    for (var i = 0; i < 100; i++) _lookCase(r),
    for (var i = 0; i < 120; i++) _projectCase(r),
    for (var i = 0; i < 120; i++) _placingCase(r),
    for (final kind in _aimKinds)
      for (var i = 0; i < 50; i++) _aimCase(r, kind),
    for (final kind in _bodyKinds)
      for (var i = 0; i < 50; i++) _bodyCase(r, kind),
    for (var i = 0; i < 60; i++) _noiseCase(r),
    for (var i = 0; i < 80; i++) _brainCase(r),
  ];
  writeFixture(
    root: args[0],
    package: 'orblit_camera',
    name: 'camera',
    cases: [for (final c in cases) _encode(c)! as Map<String, Object?>],
  );
}
