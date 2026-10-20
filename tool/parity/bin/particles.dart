import 'dart:math';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_particles/orblit_particles.dart';
import 'package:orblit_particles/src/ramp.dart' show rampAt;
import 'package:orblit_particles/src/random.dart' show unitAt;
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

List<Object> _numbers(Iterable<double> values) => [
  for (final v in values) number(v),
];

List<Object> _vector(Vector3 v) => _numbers(v.storage);

// A spec is a map with only the keys a case sets. A missing key leaves the
// constructor's own default, so the defaults are compared too.
Shape _shape(List<Object?> spec) => switch (spec[0]) {
  'point' => const PointShape(),
  'sphere' => SphereShape(radius: _double(spec[1])),
  'cone' => ConeShape(angle: _double(spec[1]), radius: _double(spec[2])),
  _ => BoxShape(Vector3(_double(spec[1]), _double(spec[2]), _double(spec[3]))),
};

double _double(Object? value) => (value as num).toDouble();

Range _range(Object? value) {
  final pair = value as List<Object?>;
  return Range(_double(pair[0]), _double(pair[1]));
}

List<double> _list(Object? value) => [
  for (final v in value as List<Object?>) _double(v),
];

Emitter _emitter(Map<String, Object?> spec) {
  final named = <Symbol, Object?>{
    if (spec.containsKey('seed')) #seed: spec['seed'],
    if (spec.containsKey('shape'))
      #shape: _shape(spec['shape'] as List<Object?>),
    if (spec.containsKey('rate')) #rate: _double(spec['rate']),
    if (spec.containsKey('bursts'))
      #bursts: [
        for (final b in spec['bursts'] as List<Object?>)
          Burst(_double((b as List<Object?>)[0]), b[1] as int),
      ],
    if (spec.containsKey('duration')) #duration: _double(spec['duration']),
    if (spec.containsKey('lifetime')) #lifetime: _range(spec['lifetime']),
    if (spec.containsKey('speed')) #speed: _range(spec['speed']),
    if (spec.containsKey('size')) #size: _list(spec['size']),
    if (spec.containsKey('colour')) #colour: _list(spec['colour']),
    if (spec.containsKey('gravity'))
      #gravity: Vector3.array(_list(spec['gravity'])),
    if (spec.containsKey('drag')) #drag: _double(spec['drag']),
    if (spec.containsKey('noiseStrength'))
      #noiseStrength: _double(spec['noiseStrength']),
    if (spec.containsKey('noiseScale'))
      #noiseScale: _double(spec['noiseScale']),
    if (spec.containsKey('spin')) #spin: _range(spec['spin']),
    if (spec.containsKey('space'))
      #space: spec['space'] == 'local'
          ? ParticleSpace.local
          : ParticleSpace.world,
    if (spec.containsKey('max')) #maxParticles: spec['max'],
  };
  return Function.apply(Emitter.new, const [], named) as Emitter;
}

Placement? _placement(List<Object?>? spec) {
  if (spec == null) return null;
  final speed = _double(spec[1]);
  final roll = spec.length > 2 ? _double(spec[2]) : 0.0;
  return switch (spec[0]) {
    'slide' => (t) => Matrix4.translationValues(t * speed, 1, 2),
    _ => (t) => Matrix4.translationValues(
      t * speed,
      1,
      2,
    )..multiply(Matrix4.rotationZ(roll)),
  };
}

HitTest? _hit(List<Object?>? spec) {
  if (spec == null) return null;
  final at = _double(spec[1]);
  return switch (spec[0]) {
    'floor' =>
      (from, to) =>
          to.y < at ? Hit(Vector3(to.x, at, to.z), Vector3(0, 1, 0)) : null,
    'wall' =>
      (from, to) =>
          to.x > at ? Hit(Vector3(at, to.y, to.z), Vector3(-1, 0, 0)) : null,
    _ => (from, to) {
      final normal = Vector3(0.6, 0.8, 0);
      final depth = to.dot(normal) - at;
      return depth < 0 ? Hit(to - normal * depth, normal) : null;
    },
  };
}

Map<String, Object?> _buffer(ParticleBuffer b) => {
  'count': b.count,
  'positions': _numbers(b.positions.take(b.count * 3)),
  'sizes': _numbers(b.sizes.take(b.count)),
  'colours': _numbers(b.colours.take(b.count * 3)),
  'angles': _numbers(b.angles.take(b.count)),
};

Map<String, Object?> _strip(TrailStrip s) => {
  'count': s.count,
  'positions': _numbers(s.positions.take(s.count * 3)),
  'widths': _numbers(s.widths.take(s.count)),
  'head': _vector(s.head),
};

Trail _trail(Map<String, Object?> spec) {
  final named = <Symbol, Object?>{
    if (spec.containsKey('life')) #life: _double(spec['life']),
    if (spec.containsKey('rate')) #rate: _double(spec['rate']),
    if (spec.containsKey('width')) #width: _list(spec['width']),
  };
  return Function.apply(Trail.new, const [], named) as Trail;
}

PathAt _path(List<Object?> spec) {
  final a = _double(spec[1]);
  final b = _double(spec[2]);
  return switch (spec[0]) {
    'line' => (t) => Vector3(t * a, t * b, 1),
    _ => (t) => Vector3(a * cos(b * t), t, a * sin(b * t)),
  };
}

Map<String, Object?> _case(
  String op,
  List<Object?> input,
  Object? Function() body,
) => {'op': op, 'in': input, 'expect': attempt(body)};

const _seeds = <int>[0, 1, 7, -3, 4294967296, 1 << 40, -(1 << 50), 1 << 62];

List<Map<String, Object?>> _units() => [
  for (final seed in _seeds)
    for (final index in const <int>[0, 1, 2, 63, 64, 1000, 65535, 65536, -1])
      for (final channel in const <int>[0, 1, 2, 3, 4, 5, 7, 8, 9, -1])
        _case('unit', [big(seed), big(index), channel], () {
          return number(unitAt(seed, index, channel));
        }),
  // Past 32 bits, where a wrapped product and a masked one part.
  for (final index in const <int>[
    0x7FFFFFFF,
    0x80000000,
    0xFFFFFFFF,
    0x100000000,
    1 << 62,
    -(1 << 62),
    9223372036854775807,
    -9223372036854775808,
  ])
    _case('unit', [big(index), big(index), 9223372036854775807], () {
      return number(unitAt(index, index, 9223372036854775807));
    }),
  for (var i = 0; i < 300; i++)
    _case('unit', [i, i * 7, i % 10], () => number(unitAt(i, i * 7, i % 10))),
];

List<Map<String, Object?>> _ramps() {
  final lists = <(List<double>, int)>[
    ([0.3], 1),
    ([0, 1], 1),
    ([0, 1, 0], 1),
    ([0, 4, 2, 8, 1], 1),
    ([1, 0, 0], 3),
    ([1, 0, 0, 0, 0, 1], 3),
    ([1, 0, 0, 0, 1, 0, 0, 0, 1, 0.5, 0.5, 0.5], 3),
    ([1, 2, 3, 4], 2),
    ([], 1),
    ([1, 2, 3], 0),
    ([1, 2, 3, 4], 3),
  ];
  final fractions = <double>[
    -1,
    -0.0,
    0,
    1e-12,
    0.1,
    0.25,
    1 / 3,
    0.5,
    0.6,
    0.75,
    0.999,
    1,
    1.5,
    _nan,
    _inf,
    -_inf,
  ];
  return [
    for (final (values, stride) in lists)
      for (final channel in const <int>[0, 1, 2, 5])
        for (final fraction in fractions)
          _case('ramp', [
            _numbers(values),
            stride,
            channel,
            number(fraction),
          ], () => number(rampAt(values, stride, channel, fraction))),
  ];
}

List<List<Object?>> _shapeSpecs(String kind) => switch (kind) {
  'sphere' => [
    ['sphere', 0.0],
    ['sphere', 0.5],
    ['sphere', 1.0],
    ['sphere', 2.0],
    ['sphere', 1e6],
    ['sphere', -1.0],
    ['sphere', _nan],
    ['sphere', _inf],
  ],
  'cone' => [
    ['cone', 0.0, 0.0],
    ['cone', 0.1, 0.0],
    ['cone', 0.4, 0.5],
    ['cone', 0.5, 1.0],
    ['cone', pi / 2, 2.0],
    ['cone', 2.5, 0.3],
    ['cone', pi, 1.0],
    ['cone', pi + 1e-9, 1.0],
    ['cone', -1e-9, 1.0],
    ['cone', 0.5, -1.0],
    ['cone', _nan, 1.0],
    ['cone', 0.5, _nan],
    ['cone', 0.5, _inf],
  ],
  'box' => [
    ['box', 0.0, 0.0, 0.0],
    ['box', 1.0, 2.0, 3.0],
    ['box', 0.1, 10.0, 0.5],
    ['box', -1.0, 2.0, 3.0],
    ['box', 1.0, -2.0, 3.0],
    ['box', 1.0, 2.0, -3.0],
    ['box', 1.0, _nan, 3.0],
    ['box', 1.0, 2.0, _inf],
  ],
  _ => [
    ['point'],
  ],
};

List<Map<String, Object?>> _shapes() => [
  for (final kind in const ['point', 'sphere', 'cone', 'box'])
    for (final spec in _shapeSpecs(kind))
      for (final seed in const <int>[0, 1, -7, 1 << 40])
        for (final index in const <int>[0, 1, 2, 3, 17, 40, 65536, 1 << 33])
          _case('shape', [spec.map(_json).toList(), big(seed), big(index)], () {
            final position = Vector3.zero();
            final direction = Vector3.zero();
            _shape(spec).pick(seed, index, position, direction);
            return {
              'position': _vector(position),
              'direction': _vector(direction),
            };
          }),
];

Object? _json(Object? value) => value is double ? number(value) : value;

Map<String, Object?> _spec(Map<String, Object?> spec) => {
  for (final e in spec.entries)
    e.key: switch (e.value) {
      final double d => number(d),
      final List<Object?> l => _jsonList(l),
      final v => v,
    },
};

List<Object?> _jsonList(List<Object?> list) => [
  for (final v in list)
    switch (v) {
      final double d => number(d),
      final List<Object?> l => _jsonList(l),
      _ => v,
    },
];

/// Every field a spec can set, one refusal each, then the ones that are fine
/// at their edge.
List<Map<String, Object?>> _makes() {
  final specs = <Map<String, Object?>>[
    {},
    {'seed': -5},
    {'rate': 0.0},
    {'rate': -1.0},
    {'rate': _nan},
    {'rate': _inf},
    {'duration': 0.0},
    {'duration': 2.5},
    {'duration': -1.0},
    {'duration': _nan},
    {'duration': _inf},
    {
      'lifetime': [0.5, 0.5],
    },
    {
      'lifetime': [0.0, 1.0],
    },
    {
      'lifetime': [-1.0, 1.0],
    },
    {
      'lifetime': [2.0, 1.0],
    },
    {
      'lifetime': [1.0, _inf],
    },
    {
      'lifetime': [_nan, 1.0],
    },
    {
      'lifetime': [1.0, _nan],
    },
    {
      'lifetime': [1e-300, 1.0],
    },
    {
      'speed': [0.0, 0.0],
    },
    {
      'speed': [-2.0, -1.0],
    },
    {
      'speed': [2.0, 1.0],
    },
    {
      'speed': [1.0, _inf],
    },
    {
      'speed': [_nan, 1.0],
    },
    {
      'spin': [-1.0, 1.0],
    },
    {
      'spin': [1.0, -1.0],
    },
    {
      'spin': [_nan, 1.0],
    },
    {
      'spin': [0.0, _inf],
    },
    {'size': <double>[]},
    {
      'size': [0.0],
    },
    {
      'size': [0.0, 1.0, 0.0],
    },
    {
      'size': [-0.1],
    },
    {
      'size': [_nan],
    },
    {
      'size': [_inf],
    },
    {
      'size': [1.0, -1.0],
    },
    {'colour': <double>[]},
    {
      'colour': [1.0, 1.0],
    },
    {
      'colour': [1.0, 1.0, 1.0, 0.0],
    },
    {
      'colour': [-1.0, 0.5, 2.0],
    },
    {
      'colour': [1.0, 1.0, _nan],
    },
    {
      'colour': [1.0, 1.0, _inf],
    },
    {
      'colour': [1.0, 0.0, 0.0, 0.0, 0.0, 1.0],
    },
    {'drag': 0.0},
    {'drag': 2.0},
    {'drag': -0.1},
    {'drag': _nan},
    {'drag': _inf},
    {'noiseStrength': 0.0},
    {'noiseStrength': 1.0},
    {'noiseStrength': -1.0},
    {'noiseStrength': _nan},
    {'noiseStrength': _inf},
    {'noiseScale': 0.5},
    {'noiseScale': 0.0},
    {'noiseScale': -1.0},
    {'noiseScale': _nan},
    {'noiseScale': _inf},
    {
      'gravity': [0.0, -9.8, 0.0],
    },
    {
      'gravity': [_nan, 0.0, 0.0],
    },
    {
      'gravity': [0.0, _inf, 0.0],
    },
    {
      'gravity': [0.0, 0.0, -_inf],
    },
    {'max': 1},
    {'max': 0},
    {'max': -3},
    {'max': 100000},
    {'bursts': <Object?>[]},
    {
      'bursts': [
        [0.0, 1],
        [1.5, 65535],
      ],
    },
    {
      'bursts': [
        [0.0, 0],
      ],
    },
    {
      'bursts': [
        [0.0, 65536],
      ],
    },
    {
      'bursts': [
        [-0.5, 3],
      ],
    },
    {
      'bursts': [
        [_nan, 3],
      ],
    },
    {
      'bursts': [
        [_inf, 3],
      ],
    },
    {'space': 'local'},
    {'space': 'world'},
  ];
  return [
    for (final spec in specs)
      _case('make', [_spec(spec)], () {
        _emitter(spec);
        return true;
      }),
  ];
}

/// Emitters that between them use every option, each held small enough that
/// a frame is a few particles.
final List<Map<String, Object?>> _emitters = [
  {},
  {
    'rate': 10.0,
    'lifetime': [1.0, 1.0],
  },
  {
    'seed': 4,
    'shape': ['sphere', 2.0],
    'speed': [1.0, 3.0],
    'gravity': [0.0, -9.8, 0.0],
    'drag': 0.3,
    'noiseStrength': 0.2,
  },
  {
    'seed': 9,
    'shape': ['cone', 0.4, 0.3],
    'rate': 6.0,
  },
  {
    'seed': -2,
    'shape': ['cone', 2.0, 0.0],
    'speed': [0.5, 4.0],
    'rate': 8.0,
  },
  {
    'seed': 11,
    'shape': ['box', 1.0, 2.0, 3.0],
    'rate': 7.0,
    'lifetime': [0.5, 3.0],
  },
  {
    'rate': 0.0,
    'bursts': [
      [1.0, 5],
    ],
    'lifetime': [2.0, 2.0],
  },
  {
    'rate': 0.0,
    'bursts': [
      [0.0, 20],
    ],
    'max': 8,
  },
  {
    'rate': 0.0,
    'bursts': [
      [0.0, 4],
      [0.5, 6],
      [0.5, 3],
    ],
    'lifetime': [1.0, 2.0],
    'shape': ['sphere', 1.0],
  },
  {
    'rate': 5.0,
    'bursts': [
      [0.0, 3],
    ],
    'lifetime': [1.0, 1.0],
  },
  {
    'rate': 10.0,
    'duration': 1.0,
    'lifetime': [0.5, 0.5],
  },
  {
    'rate': 12.0,
    'max': 5,
    'lifetime': [1.0, 2.0],
  },
  {
    'rate': 6.0,
    'gravity': [0.0, -10.0, 0.0],
    'speed': [3.0, 3.0],
    'shape': ['cone', 0.0, 0.0],
  },
  {
    'rate': 6.0,
    'gravity': [1.0, -9.8, 0.5],
    'speed': [6.0, 6.0],
    'shape': ['cone', 0.5, 0.0],
    'drag': 0.8,
  },
  {
    'rate': 6.0,
    'gravity': [0.0, -9.8, 0.0],
    'speed': [6.0, 6.0],
    'shape': ['cone', 0.5, 0.0],
    'drag': 1e-12,
  },
  {
    'rate': 6.0,
    'gravity': [0.0, -9.8, 0.0],
    'speed': [6.0, 6.0],
    'shape': ['cone', 0.5, 0.0],
    'drag': 1e-6,
  },
  {
    'rate': 6.0,
    'gravity': [0.0, -9.8, 0.0],
    'speed': [6.0, 6.0],
    'shape': ['cone', 0.5, 0.0],
    'drag': 2e-6,
  },
  {
    'rate': 6.0,
    'drag': 2.0,
    'speed': [6.0, 6.0],
  },
  {
    'rate': 6.0,
    'noiseStrength': 0.5,
    'noiseScale': 0.7,
    'shape': ['sphere', 3.0],
  },
  {
    'rate': 6.0,
    'noiseStrength': 1.0,
    'noiseScale': 3.0,
    'drag': 0.4,
    'gravity': [0.0, -1.0, 0.0],
  },
  {
    'rate': 6.0,
    'size': [0.0, 1.0, 0.0],
    'colour': [1.0, 0.0, 0.0, 0.0, 0.0, 1.0],
    'lifetime': [2.0, 2.0],
  },
  {
    'rate': 6.0,
    'size': [0.3, 0.9, 0.1, 0.5],
    'colour': [1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.5, 0.5, 0.5],
  },
  {
    'rate': 6.0,
    'spin': [-2.0, 3.0],
  },
  {
    'rate': 6.0,
    'space': 'local',
    'speed': [0.0, 1.0],
  },
  {
    'rate': 6.0,
    'space': 'local',
    'shape': ['cone', 0.0, 0.0],
    'speed': [1.0, 1.0],
    'bursts': [
      [0.5, 2],
    ],
  },
  {
    'rate': 100.0,
    'lifetime': [1.0, 1.0],
    'max': 12,
  },
  {
    'rate': 0.0,
    'bursts': [
      [0.0, 65535],
    ],
    'max': 6,
  },
  {
    'rate': 1.0,
    'lifetime': [1e-3, 1e-3],
    'speed': [0.0, 0.0],
  },
  {
    'rate': 7.0,
    'duration': 0.5,
    'lifetime': [1.0, 1.0],
  },
  {
    'seed': 3,
    'rate': 6.0,
    'gravity': [0.0, -9.8, 0.0],
    'drag': 5e-7,
  },
];

const _times = <double>[
  0,
  1e-9,
  0.05,
  0.3,
  0.5,
  0.5000001,
  0.9,
  1,
  1.3,
  2,
  2.9,
  3,
  3.2,
  5.05,
  7.9,
  50,
  1e5,
  -1,
  -0.0,
  _nan,
  _inf,
];

const _placements = <List<Object?>?>[
  null,
  ['slide', 1.5],
  ['turn', 2.0, 1.2],
];

List<Map<String, Object?>> _samples() => [
  for (final spec in _emitters)
    for (final time in _times)
      for (final place in const [0, 1, 2])
        if (place == 0 || time >= 0 && time < 6 && spec.length < 6)
          for (final room in const <(int, int?)>[
            (64, null),
            (3, 4),
            (64, 2),
            (64, 0),
            (64, 1),
            (64, 1000),
            (1, null),
            (3, -1),
          ])
            if (room == (64, null) || place == 0 && time > 0 && time < 4)
              _case(
                'sample',
                [
                  _spec(spec),
                  number(time),
                  _placements[place]?.map(_json).toList(),
                  room.$1,
                  room.$2,
                ],
                () {
                  final out = ParticleBuffer(room.$1);
                  _emitter(spec).sample(
                    time,
                    out,
                    place: _placement(_placements[place]),
                    limit: room.$2,
                  );
                  return _buffer(out);
                },
              ),
];

List<Map<String, Object?>> _births() => [
  for (final spec in _emitters)
    for (final id in <int>[
      0,
      1,
      2,
      9,
      100,
      65535,
      Emitter.burstId(0, 0),
      Emitter.burstId(1, 2),
      Emitter.burstId(3, 65535),
    ])
      _case('birth', [_spec(spec), big(id)], () {
        final emitter = _emitter(spec);
        final position = Vector3.zero();
        final velocity = Vector3.zero();
        emitter.birth(id, position, velocity);
        return {
          'life': number(emitter.lifeOf(id)),
          'position': _vector(position),
          'velocity': _vector(velocity),
        };
      }),
  for (final index in const <int>[0, 1, 5, 65535, 70000])
    for (final n in const <int>[0, 1, 65535, 65536, 70000])
      _case('burstId', [index, n], () => big(Emitter.burstId(index, n))),
];

List<Map<String, Object?>> _looks() => [
  for (final spec in _emitters)
    for (final id in const <int>[0, 3, 70])
      for (final (age, life) in const <(double, double)>[
        (0, 1),
        (0.25, 1),
        (0.5, 1),
        (1, 1),
        (1.5, 1),
        (0.7, 2),
        (3, 2),
        (-0.5, 1),
        (0.5, 0),
        (_nan, 1),
        (_inf, 1),
      ])
        _case('look', [_spec(spec), id, number(age), number(life)], () {
          final out = ParticleBuffer(2);
          _emitter(spec).look(out, 1, id, age, life);
          return {
            'size': number(out.sizes[1]),
            'colours': _numbers(out.colours.skip(3)),
            'angle': number(out.angles[1]),
          };
        }),
];

const _steps = <List<double>>[
  [0.01, 0.01, 0.01, 0.01, 0.01],
  [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1],
  [0.4, 0.2, 0.2],
  [1e5],
  [0.5, 0, 2, 0.0001],
  [1],
  [
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
    0.03,
  ],
  [0.1, -0.1],
  [0.1, _nan],
  [_inf],
];

final List<Map<String, Object?>> _thrown = [
  {
    'rate': 0.0,
    'bursts': [
      [0.0, 1],
    ],
    'lifetime': [20.0, 20.0],
    'speed': [5.0, 5.0],
    'shape': ['cone', 0.0, 0.0],
    'gravity': [0.0, -10.0, 0.0],
  },
  {
    'rate': 0.0,
    'bursts': [
      [0.0, 3],
    ],
    'lifetime': [20.0, 20.0],
    'speed': [4.0, 6.0],
    'shape': ['cone', 0.6, 0.0],
    'gravity': [0.0, -10.0, 0.0],
  },
];

List<Map<String, Object?>> _sims() => [
  for (final spec in [..._emitters, ..._thrown])
    for (final steps in _steps)
      for (final extra
          in const <(List<Object?>?, double?, double, List<Object?>?)>[
            (null, null, 0, null),
            (null, null, 8, ['slide', 2.0]),
            (['floor', 0.0], null, 0, null),
            (['floor', 0.0], 0.5, 0, null),
            (['floor', 0.0], 1.0, 4, ['turn', 0.5, 0.7]),
            (['wall', 1.5], 0.3, 0, null),
            (['slope', 0.5], 0.8, 0, null),
            (['floor', 0.0], 1.5, 0, null),
            (['floor', 0.0], -0.1, 0, null),
            (['floor', 0.0], 0.0, 0, null),
            (null, null, -1, null),
            (null, null, _inf, null),
          ])
        if (extra.$1 == null && extra.$2 == null && extra.$3 == 0 ||
            identical(spec, _thrown[0]) ||
            identical(spec, _thrown[1]) ||
            _emitters.indexOf(spec) % 5 == 0)
          _case(
            'sim',
            [
              _spec(spec),
              _numbers(steps),
              extra.$1?.map(_json).toList(),
              extra.$2 == null ? null : number(extra.$2!),
              number(extra.$3),
              extra.$4?.map(_json).toList(),
              5,
            ],
            () {
              final sim = ParticleSim(
                _emitter(spec),
                hit: _hit(extra.$1),
                bounce: extra.$2,
                turbulence: extra.$3,
              );
              final counts = <int>[];
              final out = ParticleBuffer(5);
              for (final dt in steps) {
                sim.step(dt, place: _placement(extra.$4));
                counts.add(sim.count);
              }
              sim.fill(out, place: _placement(extra.$4));
              return {'counts': counts, 'buffer': _buffer(out)};
            },
          ),
];

final List<Map<String, Object?>> _trails = [
  {},
  {
    'life': 1.0,
    'rate': 4.0,
    'width': [0.4, 0.0],
  },
  {'life': 0.5, 'rate': 60.0},
  {
    'life': 1.0,
    'rate': 2.5,
    'width': [1.0, 0.5, 0.2, 0.0],
  },
  {
    'life': 0.37,
    'rate': 7.0,
    'width': [0.2],
  },
  {
    'life': 2.0,
    'rate': 63.5,
    'width': [1.0, 0.0],
  },
  {'life': 2.0, 'rate': 64.0},
  {'life': 0.0, 'rate': 60.0},
  {'life': -1.0},
  {'rate': 0.0},
  {'rate': -3.0},
  {'life': _nan},
  {'rate': _inf},
  {'width': <double>[]},
  {
    'width': [-0.1],
  },
  {
    'width': [_nan],
  },
  {
    'width': [_inf],
  },
  {'life': 1.0, 'rate': 127.0},
  {'life': 1.0, 'rate': 127.5},
  {'life': 1.0, 'rate': 128.0},
];

List<Map<String, Object?>> _trailMakes() => [
  for (final spec in _trails)
    _case('trail', [_spec(spec)], () {
      final trail = _trail(spec);
      return {
        'life': number(trail.life),
        'rate': number(trail.rate),
        'width': _numbers(trail.width),
        'points': trail.points,
      };
    }),
  for (final capacity in const <int>[1, 2, 0, -1, 3, 200])
    _case('strip', [capacity], () {
      final strip = TrailStrip(capacity);
      return {'capacity': strip.capacity, 'count': strip.count};
    }),
];

const _paths = <List<Object?>>[
  ['line', 3.0, -1.0],
  ['circle', 2.0, 5.0],
];

List<Map<String, Object?>> _alongs() => [
  for (final spec in _trails)
    for (final path in _paths)
      for (final time in const <double>[
        0,
        0.01,
        0.3,
        0.5,
        0.7,
        1,
        1.01,
        2,
        9.5,
        -1,
        _nan,
        _inf,
      ])
        for (final capacity in const <int>[2, 5, 200])
          _case(
            'along',
            [_spec(spec), path.map(_json).toList(), number(time), capacity],
            () {
              final strip = TrailStrip(capacity);
              _trail(spec).sampleAlong(time, _path(path), strip);
              return _strip(strip);
            },
          ),
];

/// A run of calls on one ring. Each fill gives the strip, so what the ring
/// kept shows through.
final _rings = <List<List<Object?>>>[
  [
    ['record', 0.0, 0.0, 0.0, 0.0],
    ['fill'],
    ['record', 0.25, 1.0, 0.0, 0.0],
    ['record', 0.5, 2.0, 0.0, 0.0],
    ['fill'],
    ['record', 1.25, 5.0, 0.0, 0.0],
    ['fill'],
    ['record', 2.5, 9.0, 1.0, 0.0],
    ['fill'],
  ],
  [
    ['record', 1.0, 1.0, 1.0, 1.0],
    ['record', 2.0, 2.0, 2.0, 2.0],
    ['record', 1.5, 3.0, 3.0, 3.0],
    ['fill'],
    ['record', 2.0, 4.0, 4.0, 4.0],
    ['fill'],
  ],
  [
    ['record', 0.0, 0.0, 0.0, 0.0],
    ['record', 0.1, 1.0, 0.0, 0.0],
    ['record', 0.1, 2.0, 0.0, 0.0],
    ['fill'],
    ['clear'],
    ['fill'],
    ['record', 5.0, 1.0, 0.0, 0.0],
    ['fill'],
  ],
  [
    ['record', _nan, 1.0, 0.0, 0.0],
    ['record', -1.0, 1.0, 0.0, 0.0],
    ['record', _inf, 1.0, 0.0, 0.0],
    ['fill'],
    ['record', 0.5, 1.0, 2.0, 3.0],
    ['record', 0.51, 1.5, 2.0, 3.0],
    ['record', 0.6, 2.5, 2.0, 3.0],
    ['fill'],
  ],
  [
    for (var i = 0; i < 40; i++) ['record', i * 0.05, i * 1.0, 0.0, 0.0],
    ['fill'],
    for (var i = 40; i < 60; i++) ['record', i * 0.05, i * 1.0, 0.0, 0.0],
    ['fill'],
  ],
  [
    ['record', 0.0, 0.0, 0.0, 0.0],
    ['record', 0.2, 1.0, 0.0, 0.0],
    ['record', 0.5, 2.0, 0.0, 0.0],
    ['record', 1.0, 3.0, 0.0, 0.0],
    ['record', 1.0000001, 4.0, 0.0, 0.0],
    ['record', 1.2, 5.0, 0.0, 0.0],
    ['record', 2.2, 6.0, 0.0, 0.0],
    ['fill'],
    ['record', 2.3, 7.0, 0.0, 0.0],
    ['fill'],
  ],
];

List<Map<String, Object?>> _ringRuns() => [
  for (final spec in _trails.take(7))
    if (!spec.containsKey('life') || (spec['life'] as double) > 0)
      for (final calls in _rings)
        for (final capacity in const <int>[2, 4, 200])
          _ring(spec, calls, capacity),
];

Map<String, Object?> _ring(
  Map<String, Object?> spec,
  List<List<Object?>> calls,
  int capacity,
) => _case('ring', [_spec(spec), _jsonList(calls), capacity], () {
  final ring = TrailRing(_trail(spec));
  final out = TrailStrip(capacity);
  final fills = <Map<String, Object?>>[];
  for (final call in calls) {
    switch (call[0]) {
      case 'record':
        ring.record(
          _double(call[1]),
          Vector3(_double(call[2]), _double(call[3]), _double(call[4])),
        );
      case 'clear':
        ring.clear();
      default:
        ring.fill(out);
        fills.add(_strip(out));
    }
  }
  return fills;
});

/// A frame clock lands a hair short of one step, and the ring allows 1e-6 of
/// it. The three gaps sit on the bound, just inside it and just outside it.
List<Map<String, Object?>> _stepRuns() => [
  for (final spec in _trails.take(6))
    if (!spec.containsKey('life') || (spec['life'] as double) > 0)
      for (final capacity in const <int>[4, 200])
        () {
          final trail = _trail(spec);
          final step = trail.life / (trail.points - 1);
          final first = step * (1 - 1e-6);
          final calls = <List<Object?>>[
            ['record', 0.0, 0.0, 0.0, 0.0],
            ['record', first, 1.0, 0.0, 0.0],
            ['fill'],
            ['record', first + step * (1 - 5e-6), 2.0, 0.0, 0.0],
            ['fill'],
            ['record', first + step * (1 - 5e-7), 3.0, 0.0, 0.0],
            ['fill'],
          ];
          return _ring(spec, calls, capacity);
        }(),
];

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_particles',
    name: 'particles',
    cases: [
      ..._units(),
      ..._ramps(),
      ..._shapes(),
      ..._makes(),
      ..._samples(),
      ..._births(),
      ..._looks(),
      ..._sims(),
      ..._trailMakes(),
      ..._alongs(),
      ..._ringRuns(),
      ..._stepRuns(),
    ],
  );
}
