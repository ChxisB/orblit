// Usage: `dart run bin/sequence.dart <repository root>`
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

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

double _d(Object? v) => v! as double;

List<Map<String, Object?>> _maps(Object? v) =>
    (v! as List).cast<Map<String, Object?>>();

/// A value kind: how its spec is read into a value and written back out.
final class _Kind<T> {
  const _Kind(this.name, this.mixer, this.read, this.write);

  final String name;
  final Mixer<T> mixer;
  final T Function(Object?) read;
  final Object Function(T) write;
}

double _readDouble(Object? v) => _d(v);

Vector3 _readVec3(Object? v) {
  final l = (v! as List).cast<double>();
  return Vector3(l[0], l[1], l[2]);
}

Object _writeVec3(Vector3 v) => [number(v.x), number(v.y), number(v.z)];

Quaternion _readQuat(Object? v) {
  final l = (v! as List).cast<double>();
  return Quaternion(l[0], l[1], l[2], l[3]);
}

Object _writeQuat(Quaternion q) => [
  number(q.x),
  number(q.y),
  number(q.z),
  number(q.w),
];

bool _readBool(Object? v) => v! as bool;

Object _writeBool(bool v) => v;

const _double = _Kind<double>('double', doubleMixer, _readDouble, number);
const _vec3 = _Kind<Vector3>('vec3', vector3Mixer, _readVec3, _writeVec3);
const _quat = _Kind<Quaternion>('quat', quaternionMixer, _readQuat, _writeQuat);
const _bool = _Kind<bool>('bool', boolMixer, _readBool, _writeBool);

/// Half the quaternions are unit length, as a rotation is. The rest are not,
/// which is what a clamp on the dot product is there for.
List<double> _quatSpec(_Rng r) {
  final q = [for (var i = 0; i < 4; i++) r.between(-1, 1)];
  if (!r.chance(2)) {
    return q;
  }
  final unit = Quaternion(q[0], q[1], q[2], q[3])..normalize();
  return [unit.x, unit.y, unit.z, unit.w];
}

Object _valueSpec(String type, _Rng r) => switch (type) {
  'double' => r.between(-5, 5),
  'vec3' => [for (var i = 0; i < 3; i++) r.between(-5, 5)],
  'quat' => _quatSpec(r),
  _ => r.chance(2),
};

/// A value a hair away from [a], so two rotations are nearly the same one.
Object _near(String type, Object a, _Rng r) => switch (a) {
  double() => a + r.between(-1, 1) * 0.001,
  List<double>() => [for (final x in a) x + r.between(-1, 1) * 0.001],
  _ => _valueSpec(type, r),
};

Easing _shape(Object? name) => Easing.values.byName(name! as String);

Hold _hold(Object? name) => Hold.values.byName(name! as String);

Key<T> _key<T>(_Kind<T> kind, Map<String, Object?> s) => Key<T>(
  _d(s['at']),
  kind.read(s['value']),
  hold: _hold(s['hold']),
  shape: s['shape'] == null ? null : _shape(s['shape']),
  slopeIn: s['slopeIn'] == null ? null : kind.read(s['slopeIn']),
  slopeOut: s['slopeOut'] == null ? null : kind.read(s['slopeOut']),
);

Channel<T> _channel<T>(_Kind<T> kind, List<Map<String, Object?>> keys) =>
    Channel<T>([for (final k in keys) _key(kind, k)], kind.mixer);

/// Keys in ascending order. A step of nothing makes two keys at one moment,
/// which is how a cut is written.
List<Map<String, Object?>> _keys(
  String type,
  _Rng r, {
  required int count,
  required double from,
}) {
  var at = from;
  final keys = <Map<String, Object?>>[];
  for (var i = 0; i < count; i++) {
    if (i > 0) {
      at += r.pick([0.0, 0.25, 0.5, 1.0, 1.5]);
    }
    keys.add(<String, Object?>{
      'at': at,
      'value': _valueSpec(type, r),
      'hold': r.pick(Hold.values).name,
      'shape': r.chance(2) ? r.pick(Easing.values).name : null,
      'slopeIn': type != 'bool' && r.chance(3) ? _valueSpec(type, r) : null,
      'slopeOut': type != 'bool' && r.chance(3) ? _valueSpec(type, r) : null,
    });
  }
  return keys;
}

Map<String, Object?> _clipSpec(_Rng r, {required double start}) =>
    <String, Object?>{
      'start': start,
      'duration': r.between(1, 4),
      'easeIn': r.pick([0.0, 0.5, r.between(0, 3)]),
      'easeOut': r.pick([0.0, 0.5, r.between(0, 3)]),
      'shapeIn': r.pick(Easing.values).name,
      'shapeOut': r.pick(Easing.values).name,
      'clipIn': r.pick([0.0, 0.5, r.between(0, 2)]),
      'speed': r.pick([1.0, 2.0, 0.5, -1.0, 1.5, -0.5]),
      'name': r.chance(2) ? 'clip' : null,
    };

Clip _clip(Map<String, Object?> s) => Clip(
  start: _d(s['start']),
  duration: _d(s['duration']),
  easeIn: _d(s['easeIn']),
  easeOut: _d(s['easeOut']),
  shapeIn: _shape(s['shapeIn']),
  shapeOut: _shape(s['shapeOut']),
  clipIn: _d(s['clipIn']),
  speed: _d(s['speed']),
  name: s['name'] as String?,
);

Map<String, Object?> _common(String kind, _Rng r) => <String, Object?>{
  'kind': kind,
  'name': r.chance(3) ? 'track' : null,
  'muted': r.chance(8),
};

String? _binding(_Rng r) => r.chance(8) ? null : r.pick(['a', 'b', 'c']);

Map<String, Object?> _propertySpec(_Rng r) {
  final type = r.pick(['double', 'vec3', 'quat', 'bool']);
  var start = r.pick([0.0, 0.5, 1.0]);
  final clips = <Map<String, Object?>>[];
  for (var i = 0, n = 1 + r.next(3); i < n; i++) {
    clips.add(<String, Object?>{
      'clip': _clipSpec(r, start: start),
      'keys': _keys(type, r, count: 1 + r.next(4), from: 0.0),
    });
    start += r.pick([0.0, 0.5, 1.0, 2.0]);
  }
  return <String, Object?>{
    ..._common('property', r),
    'binding': _binding(r),
    'type': type,
    'property': r.pick(['position', 'rotation', 'scale', 'visible']),
    'clips': clips,
  };
}

List<Map<String, Object?>> _clipsAt(_Rng r) {
  var start = r.pick([0.0, 0.5, 1.0]);
  final clips = <Map<String, Object?>>[];
  for (var i = 0, n = r.next(4); i < n; i++) {
    clips.add(_clipSpec(r, start: start));
    start += r.pick([0.0, 1.0, 2.5, 4.0]);
  }
  return clips;
}

Map<String, Object?> _activationSpec(_Rng r) => <String, Object?>{
  ..._common('activation', r),
  'binding': _binding(r),
  'clips': _clipsAt(r),
};

Map<String, Object?> _markSpec(_Rng r) {
  final times = [
    for (var i = 0, n = 1 + r.next(5); i < n; i++) r.next(13) * 0.5,
  ]..sort();
  return <String, Object?>{
    ..._common('marks', r),
    'binding': _binding(r),
    'marks': [
      for (final at in times)
        <String, Object?>{
          'at': at,
          'name': 'm${r.next(100)}',
          'payload': switch (r.next(3)) {
            0 => null,
            1 => 'p${r.next(10)}',
            _ => r.between(0, 9),
          },
        },
    ],
  };
}

Map<String, Object?> _soundSpec(_Rng r) => <String, Object?>{
  ..._common('sound', r),
  'binding': _binding(r),
  'gain': r.pick([1.0, 0.5, r.between(0, 1)]),
  'clips': [
    for (final c in _clipsAt(r))
      <String, Object?>{'clip': c, 'sound': 's${r.next(5)}'},
  ],
};

Map<String, Object?> _shotSpec(_Rng r) => <String, Object?>{
  ..._common('shot', r),
  'clips': [
    for (final c in _clipsAt(r))
      <String, Object?>{'clip': c, 'camera': 'c${r.next(3)}'},
  ],
};

Map<String, Object?> _trackSpec(_Rng r) => switch (r.next(10)) {
  0 || 1 || 2 || 3 => _propertySpec(r),
  4 || 5 => _activationSpec(r),
  6 => _markSpec(r),
  7 || 8 => _soundSpec(r),
  _ => _shotSpec(r),
};

Map<String, Object?> _sequenceSpec(_Rng r, {bool withMarks = false}) {
  final tracks = [for (var i = 0, n = 1 + r.next(4); i < n; i++) _trackSpec(r)];
  // Two mark tracks, so marks at one moment come from different tracks and
  // the order they are handed back in is the order the tracks are in. Five
  // each at most, so the lists stay where Dart's sort is still stable.
  if (withMarks) {
    tracks
      ..add(_markSpec(r))
      ..add(_markSpec(r));
  }
  return <String, Object?>{
    'rate': r.pick([24.0, 25.0, 30.0, 60.0]),
    'duration': r.chance(3) ? r.between(1, 8) : null,
    'tracks': tracks,
  };
}

PropertyTrack<T> _property<T>(_Kind<T> kind, Map<String, Object?> s) =>
    PropertyTrack<T>(
      property: s['property']! as String,
      clips: [
        for (final c in _maps(s['clips']))
          Keyed<T>(
            _clip(c['clip']! as Map<String, Object?>),
            _channel(kind, _maps(c['keys'])),
          ),
      ],
      mixer: kind.mixer,
      name: s['name'] as String?,
      binding: s['binding'] as String?,
      muted: s['muted']! as bool,
    );

Track _track(Map<String, Object?> s) {
  final name = s['name'] as String?;
  final binding = s['binding'] as String?;
  final muted = s['muted']! as bool;
  return switch (s['kind']) {
    'property' => switch (s['type']) {
      'double' => _property(_double, s),
      'vec3' => _property(_vec3, s),
      'quat' => _property(_quat, s),
      _ => _property(_bool, s),
    },
    'activation' => ActivationTrack(
      clips: [for (final c in _maps(s['clips'])) _clip(c)],
      name: name,
      binding: binding,
      muted: muted,
    ),
    'marks' => MarkTrack(
      marks: [
        for (final m in _maps(s['marks']))
          Mark(_d(m['at']), m['name']! as String, payload: m['payload']),
      ],
      name: name,
      binding: binding,
      muted: muted,
    ),
    'sound' => SoundTrack(
      clips: [
        for (final c in _maps(s['clips']))
          (_clip(c['clip']! as Map<String, Object?>), c['sound']! as String),
      ],
      gain: _d(s['gain']),
      name: name,
      binding: binding,
      muted: muted,
    ),
    _ => ShotTrack(
      shots: [
        for (final c in _maps(s['clips']))
          (_clip(c['clip']! as Map<String, Object?>), c['camera']! as String),
      ],
      name: name,
      muted: muted,
    ),
  };
}

Sequence _sequence(Map<String, Object?> s) => Sequence(
  tracks: [for (final t in _maps(s['tracks'])) _track(t)],
  rate: _d(s['rate']),
  duration: s['duration'] as double?,
);

Object? _value(Object? v) => switch (v) {
  Vector3() => [v.x, v.y, v.z],
  Quaternion() => [v.x, v.y, v.z, v.w],
  _ => v,
};

Map<String, Object?> _frame(SequenceFrame f) => <String, Object?>{
  'at': f.at,
  'values': {
    for (final MapEntry(:key, :value) in f.values.entries)
      key: {
        for (final MapEntry(key: property, value: v) in value.entries)
          property: _value(v),
      },
  },
  'active': f.active,
  'sounds': [
    for (final s in f.sounds)
      <String, Object?>{
        'sound': s.sound,
        'at': s.at,
        'gain': s.gain,
        'binding': s.binding,
      },
  ],
  'shots': [
    for (final s in f.shots)
      <String, Object?>{'camera': s.camera, 'weight': s.weight},
  ],
  'running': f.running,
};

List<Object?> _marks(List<Mark> marks) => [
  for (final m in marks) <Object?>[m.at, m.name, m.payload],
];

/// What one track says at [at], for the two kinds that can say nothing.
Object? _probe(Track track, double at) => switch (track) {
  PropertyTrack() => _value(track.valueAt(at)),
  ActivationTrack() => track.contributionAt(at),
  _ => null,
};

Map<String, Object?> _easeCase(Easing shape) {
  final ats = <double>[
    -1.0,
    -0.001,
    for (var i = 0; i <= 20; i++) i / 20,
    1.001,
    2.0,
    _nan,
    _inf,
    -_inf,
  ];
  return <String, Object?>{
    'op': 'ease',
    'shape': shape.name,
    'ats': ats,
    'values': [for (final at in ats) ease(shape, at)],
  };
}

Map<String, Object?> _clipCase(_Rng r) {
  final spec = _clipSpec(r, start: r.between(0, 5));
  final clip = _clip(spec);
  final ats = <double>[
    for (final f in [
      -0.5,
      0.0,
      0.05,
      0.1,
      0.25,
      0.5,
      0.75,
      0.9,
      0.99,
      1.0,
      1.1,
    ])
      clip.start + clip.duration * f,
    clip.start + clip.easeIn / 2,
    clip.start + clip.easeIn,
    clip.end - clip.easeOut,
    clip.end - clip.easeOut / 2,
  ];
  return <String, Object?>{
    'op': 'clip',
    'clip': spec,
    'ats': ats,
    'weights': [for (final at in ats) clip.weightAt(at)],
    'locals': [for (final at in ats) clip.localAt(at)],
    'covers': [for (final at in ats) clip.covers(at)],
  };
}

List<double> _probes(List<double> times, _Rng r) => [
  for (final t in times) ...[t - 0.125, t, t + 0.125],
  times.first - 1,
  times.last + 1,
  for (var i = 0; i < 5; i++)
    times.first + r.between(0, 1) * (times.last - times.first),
];

/// A slope for every key, and for one either side of the keys. A channel that
/// cannot curve refuses, and the case records that it did.
Object _slopes<T>(_Kind<T> kind, Channel<T> channel) {
  try {
    return [
      for (var i = -1; i <= channel.keys.length; i++)
        kind.write(channel.slopeAt(i)),
    ];
  } on StateError {
    return 'throws';
  }
}

Map<String, Object?> _channelCase<T>(_Kind<T> kind, _Rng r) {
  final keys = _keys(
    kind.name,
    r,
    count: 1 + r.next(6),
    from: r.pick([0.0, 0.5, 1.0]),
  );
  final channel = _channel(kind, keys);
  final ats = _probes([for (final k in keys) _d(k['at'])], r);
  return <String, Object?>{
    'op': 'channel',
    'type': kind.name,
    'keys': keys,
    'ats': ats,
    'values': [for (final at in ats) kind.write(channel.at(at))],
    'slopes': _slopes(kind, channel),
  };
}

const _ts = [-0.5, 0.0, 0.1, 0.25, 0.5, 0.75, 0.9999, 1.0, 1.5];

Map<String, Object?> _lerpCase<T>(_Kind<T> kind, _Rng r) {
  final a = _valueSpec(kind.name, r);
  final b = r.chance(4) ? _near(kind.name, a, r) : _valueSpec(kind.name, r);
  return <String, Object?>{
    'op': 'lerp',
    'type': kind.name,
    'a': a,
    'b': b,
    'ts': _ts,
    'values': [
      for (final t in _ts)
        kind.write(kind.mixer.lerp(kind.read(a), kind.read(b), t)),
    ],
  };
}

Map<String, Object?> _mixCase<T>(_Kind<T> kind, _Rng r) {
  final n = r.next(5);
  final values = [for (var i = 0; i < n; i++) _valueSpec(kind.name, r)];
  final weights = [
    for (var i = 0; i < n; i++) r.chance(5) ? 0.0 : r.between(0, 1),
  ];
  return <String, Object?>{
    'op': 'mix',
    'type': kind.name,
    'values': values,
    'weights': weights,
    'value': kind.write(
      kind.mixer.mix([for (final v in values) kind.read(v)], weights),
    ),
  };
}

Map<String, Object?> _sequenceCase(_Rng r) {
  final spec = _sequenceSpec(r, withMarks: r.chance(2));
  final sequence = _sequence(spec);
  final ats = <double>[
    -1.0,
    0.0,
    0.25,
    0.5,
    1.0,
    1.5,
    2.0,
    3.0,
    4.0,
    5.0,
    7.5,
    sequence.duration,
    sequence.duration + 1,
    for (var i = 0; i < 6; i++) r.between(-1, 9),
  ];
  final edges = [for (var i = 0; i < 24; i++) r.next(24) * 0.5 - 0.5];
  final windows = [
    for (var i = 0; i < edges.length; i += 2) (edges[i], edges[i + 1]),
  ];
  final snapAts = <double>[
    for (var k = -6; k <= 12; k++) k / 60,
    for (var i = 0; i < 3; i++) r.between(-5, 20),
  ];
  return <String, Object?>{
    'op': 'sequence',
    'spec': spec,
    'duration': sequence.duration,
    'ends': [for (final t in sequence.tracks) t.end],
    'ats': ats,
    'frames': [for (final at in ats) _frame(sequence.sampleAt(at))],
    'probes': [
      for (final t in sequence.tracks) [for (final at in ats) _probe(t, at)],
    ],
    'windows': [
      for (final (from, to) in windows)
        <Object?>[from, to, _marks(sequence.marksBetween(from, to))],
    ],
    'snapAts': snapAts,
    'snaps': [for (final at in snapAts) sequence.snap(at)],
  };
}

List<Object?> _step(_Rng r, {required bool first}) {
  if (first && !r.chance(5)) {
    return ['play'];
  }
  return switch (r.next(14)) {
    0 || 1 || 2 || 3 || 4 || 5 || 6 || 7 => [
      'advance',
      r.pick([0.1, 0.25, 0.5, 1.0, 2.5, 7.0, 0.0, -0.3, 20.0]),
    ],
    8 => ['seek', r.between(-1, 12)],
    9 => ['play'],
    10 => ['pause'],
    11 => ['stop'],
    12 => ['whenDone', r.pick(WhenDone.values).name],
    _ => ['sample'],
  };
}

Map<String, Object?> _run(Director d, List<Object?> step) {
  SequenceFrame? frame;
  List<Mark>? marks;
  switch (step[0]) {
    case 'play':
      d.play();
    case 'pause':
      d.pause();
    case 'stop':
      d.stop();
    case 'seek':
      d.seek(_d(step[1]));
    case 'whenDone':
      d.whenDone = WhenDone.values.byName(step[1]! as String);
    case 'sample':
      frame = d.sample();
    case 'advance':
      final advanced = d.advance(_d(step[1]));
      frame = advanced.frame;
      marks = advanced.marks;
  }
  return <String, Object?>{
    'at': d.at,
    'playing': d.playing,
    'finished': d.finished,
    if (frame != null) 'frame': _frame(frame),
    if (marks != null) 'marks': _marks(marks),
  };
}

Map<String, Object?> _directorCase(_Rng r) {
  final spec = _sequenceSpec(r, withMarks: true);
  final whenDone = r.pick(WhenDone.values);
  final speed = r.pick([1.0, 2.0, 0.5, -1.0, 3.7]);
  final script = [for (var i = 0; i < 14; i++) _step(r, first: i == 0)];
  final director = Director(_sequence(spec), whenDone: whenDone, speed: speed);
  return <String, Object?>{
    'op': 'director',
    'spec': spec,
    'whenDone': whenDone.name,
    'speed': speed,
    'script': script,
    'log': [for (final step in script) _run(director, step)],
  };
}

void main(List<String> args) {
  final r = _Rng(9);
  final cases = <Map<String, Object?>>[
    for (final shape in Easing.values) _easeCase(shape),
    for (var i = 0; i < 40; i++) _clipCase(r),
    for (var i = 0; i < 30; i++) ...[
      _channelCase(_double, r),
      _channelCase(_vec3, r),
      _channelCase(_quat, r),
      _channelCase(_bool, r),
    ],
    for (var i = 0; i < 25; i++) ...[
      _lerpCase(_double, r),
      _lerpCase(_vec3, r),
      _lerpCase(_quat, r),
      _lerpCase(_bool, r),
      _mixCase(_double, r),
      _mixCase(_vec3, r),
      _mixCase(_quat, r),
      _mixCase(_bool, r),
    ],
    for (var i = 0; i < 40; i++) _sequenceCase(r),
    for (var i = 0; i < 40; i++) _directorCase(r),
  ];
  writeFixture(
    root: args[0],
    package: 'orblit_sequence',
    name: 'sequence',
    cases: [for (final c in cases) _encode(c)! as Map<String, Object?>],
  );
}
