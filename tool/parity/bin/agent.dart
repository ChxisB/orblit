// Usage: `dart run bin/agent.dart <repository root>`
import 'package:orblit_agent/orblit_agent.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

/// The generator the audio exporter uses, so a case list is the same on every
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

List<double> _list(Vector3 v) => [v.x, v.y, v.z];

Vector3 _vec(Object? spec) {
  final v = (spec! as List).cast<double>();
  return Vector3(v[0], v[1], v[2]);
}

Steerable _steerable(Object? spec) {
  final s = spec! as Map<String, Object?>;
  return Steerable(
    position: _vec(s['p']),
    velocity: _vec(s['v']),
    maxSpeed: s['maxSpeed']! as double,
    maxForce: s['maxForce']! as double,
    radius: s['radius']! as double,
    priority: s['priority']! as int,
  );
}

List<Steerable> _neighbours(Object? specs, Steerable self) => [
  for (final s in specs! as List)
    if ((s as Map<String, Object?>)['self'] == true) self else _steerable(s),
];

Map<String, Object?> _steerableSpec(_Rng r, {bool still = false}) =>
    <String, Object?>{
      'p': [
        r.between(-8, 8),
        r.chance(4) ? r.between(-2, 2) : 0.0,
        r.between(-8, 8),
      ],
      'v': still ? [0.0, 0.0, 0.0] : [r.between(-4, 4), 0.0, r.between(-4, 4)],
      'maxSpeed': r.pick([0.0, 1.0, 2.0, 4.0, 6.0]),
      'maxForce': r.pick([0.0, 4.0, 8.0, 20.0]),
      'radius': r.pick([0.0, 0.4, 0.5, 1.0]),
      'priority': r.next(3),
    };

/// A neighbour put close to [self], and often heading for it, so the rules
/// that care about a hit are the ones that get exercised.
Map<String, Object?> _nearSpec(_Rng r, Map<String, Object?> self) {
  final spec = _steerableSpec(r);
  final at = (self['p']! as List).cast<double>();
  final reach = r.pick([1, 3, 8]);
  final placed = r.chance(12)
      ? [at[0], at[1], at[2]]
      : [
          at[0] + r.between(-reach, reach),
          at[1],
          at[2] + r.between(-reach, reach),
        ];
  spec['p'] = placed;
  if (r.chance(2)) {
    final toSelf = Vector3(at[0] - placed[0], 0, at[2] - placed[2]);
    if (toSelf.length > 1e-9) {
      spec['v'] = _list(toSelf.normalized() * r.between(0, 4));
    }
  }
  return spec;
}

List<Object?> _crowd(_Rng r, Map<String, Object?> self) => [
  for (var i = r.next(7); i > 0; i--)
    r.chance(6) ? <String, Object?>{'self': true} : _nearSpec(r, self),
];

List<double> _point(_Rng r) => [
  r.between(-8, 8),
  r.chance(6) ? r.between(-2, 2) : 0.0,
  r.between(-8, 8),
];

Map<String, Object?> _behaviourSpec(
  _Rng r,
  Map<String, Object?> self,
  int depth,
) {
  final kinds = [
    'seek',
    'flee',
    'arrive',
    'pursue',
    'evade',
    'wander',
    'separate',
    'align',
    'cohere',
    'avoid',
    'avoid',
    'path',
    if (depth == 0) ...['blend', 'blend'],
  ];
  final at = (self['p']! as List).cast<double>();
  List<double> target() => r.chance(10) ? [at[0], at[1], at[2]] : _point(r);
  return switch (r.pick(kinds)) {
    'seek' => {'t': 'seek', 'target': target()},
    'flee' => {
      't': 'flee',
      'from': target(),
      'within': r.pick([0.0, 3.0, 8.0, _inf]),
    },
    'arrive' => {
      't': 'arrive',
      'target': target(),
      'slowingRadius': r.pick([-1.0, 0.0, 1.0, 3.0, 6.0]),
    },
    'pursue' => {
      't': 'pursue',
      'quarry': _nearSpec(r, self),
      'lookAhead': r.pick([0.0, 0.5, 1.0, 3.0]),
    },
    'evade' => {
      't': 'evade',
      'hunter': _nearSpec(r, self),
      'lookAhead': r.pick([0.0, 0.5, 1.0, 3.0]),
      'within': r.pick([0.0, 3.0, 12.0, _inf]),
    },
    'wander' => _wanderSpec(r),
    'separate' => {
      't': 'separate',
      'neighbours': _crowd(r, self),
      'radius': r.pick([0.0, 1.0, 2.0, 4.0, 10.0]),
    },
    'align' => {
      't': 'align',
      'neighbours': _crowd(r, self),
      'radius': r.pick([0.0, 1.0, 4.0, 10.0]),
    },
    'cohere' => {
      't': 'cohere',
      'neighbours': _crowd(r, self),
      'radius': r.pick([0.0, 1.0, 5.0, 10.0]),
    },
    'avoid' => {
      't': 'avoid',
      'preferred': [r.between(-4, 4), 0.0, r.between(-4, 4)],
      'neighbours': _crowd(r, self),
      'horizon': r.pick([0.5, 2.0, 5.0]),
      'turns': r.pick([1, 2, 8, 12]),
      'weight': r.pick([0.0, 1.0, 2.0, 10.0]),
    },
    'path' => _pathSpec(r, self),
    _ => {
      't': 'blend',
      'parts': [
        for (var i = 1 + r.next(3); i > 0; i--)
          {
            'b': _behaviourSpec(r, self, depth + 1),
            'w': r.pick([0.0, 0.25, 0.5, 1.0, 2.0]),
          },
      ],
    },
  };
}

Map<String, Object?> _wanderSpec(_Rng r) => {
  't': 'wander',
  'seed': r.pick([
    0,
    1,
    7,
    8191,
    12345,
    -5,
    2147483647,
    4611686018427387904,
    -9223372036854775808,
  ]),
  'at': r.chance(8)
      ? r.pick([_nan, _inf, -_inf, 1e300, -1e300, 0.0, -0.0])
      : r.between(-20, 100),
  'strength': r.pick([0.0, 0.5, 1.0, 3.0]),
  'rate': r.pick([0.0, 0.35, 1.0, 5.0]),
};

Map<String, Object?> _pathSpec(_Rng r, Map<String, Object?> self) {
  final points = [for (var i = r.next(6); i > 0; i--) _point(r)];
  final reached = r.pick([0.0, 0.5, 1.0, 3.0]);
  // Standing on a point, so the index has a reason to move on.
  if (points.isNotEmpty && r.chance(2)) {
    self['p'] = List<double>.of(points[r.next(points.length)]);
  }
  return {
    't': 'path',
    'points': points,
    'index': r.next(points.length + 4) - 2,
    'reached': reached,
    'loop': r.chance(3),
  };
}

Steering _build(Map<String, Object?> b, Steerable self) => switch (b['t']) {
  'seek' => Seek(_vec(b['target'])),
  'flee' => Flee(_vec(b['from']), within: b['within']! as double),
  'arrive' => Arrive(
    _vec(b['target']),
    slowingRadius: b['slowingRadius']! as double,
  ),
  'pursue' => Pursue(
    _steerable(b['quarry']),
    lookAhead: b['lookAhead']! as double,
  ),
  'evade' => Evade(
    _steerable(b['hunter']),
    lookAhead: b['lookAhead']! as double,
    within: b['within']! as double,
  ),
  'wander' => Wander(
    seed: b['seed']! as int,
    at: b['at']! as double,
    strength: b['strength']! as double,
    rate: b['rate']! as double,
  ),
  'separate' => Separate(
    _neighbours(b['neighbours'], self),
    radius: b['radius']! as double,
  ),
  'align' => Align(
    _neighbours(b['neighbours'], self),
    radius: b['radius']! as double,
  ),
  'cohere' => Cohere(
    _neighbours(b['neighbours'], self),
    radius: b['radius']! as double,
  ),
  'avoid' => Avoid(
    _vec(b['preferred']),
    _neighbours(b['neighbours'], self),
    horizon: b['horizon']! as double,
    turns: b['turns']! as int,
    weight: b['weight']! as double,
  ),
  'path' => FollowPath(
    [for (final p in b['points']! as List) _vec(p)],
    index: b['index']! as int,
    reached: b['reached']! as double,
    loop: b['loop']! as bool,
  ),
  'blend' => Blend([
    for (final p in b['parts']! as List)
      (
        behaviour: _build(
          (p as Map<String, Object?>)['b']! as Map<String, Object?>,
          self,
        ),
        weight: p['w']! as double,
      ),
  ]),
  _ => throw StateError('unknown behaviour ${b['t']}'),
};

Map<String, Object?> _forceCase(_Rng r) {
  final self = _steerableSpec(r, still: r.chance(4));
  // Built before the steerable, because a path may move self onto a point.
  final spec = _behaviourSpec(r, self, 0);
  final agent = _steerable(self);
  final behaviour = _build(spec, agent);
  final result = <String, Object?>{'op': 'force', 'self': self, 'b': spec};
  try {
    result['force'] = _list(behaviour.force(agent));
    if (behaviour is FollowPath) {
      result['next'] = behaviour.nextIndex(agent);
      result['done'] = behaviour.isDone(agent);
    }
  } on Error {
    // Wander floors a time that is not a number, which Dart refuses.
    result['throws'] = true;
  }
  return result;
}

List<Map<String, Object?>> _avoidRefusals() => [
  for (final horizon in [0.0, _inf, _nan, -1.0, 2.0])
    for (final turns in [0, -1, 1, 8])
      for (final weight in [-1.0, _nan, _inf, 0.0, 2.0])
        () {
          var ok = true;
          try {
            Avoid(
              Vector3.zero(),
              const [],
              horizon: horizon,
              turns: turns,
              weight: weight,
            );
          } on ArgumentError {
            ok = false;
          }
          return <String, Object?>{
            'op': 'avoidNew',
            'horizon': horizon,
            'turns': turns,
            'weight': weight,
            'ok': ok,
          };
        }(),
];

Map<String, Object?> _integrateCase(_Rng r) {
  final self = _steerableSpec(r, still: r.chance(4));
  final agent = _steerable(self);
  final steps = <Map<String, Object?>>[];
  for (var i = 0; i < 6; i++) {
    final force = [
      r.between(-30, 30),
      r.chance(5) ? r.between(-3, 3) : 0.0,
      r.between(-30, 30),
    ];
    final seconds = r.chance(20)
        ? _nan
        : r.pick([-1.0, 0.0, 1 / 60, 0.1, 0.5, 2.0]);
    agent.integrate(_vec(force), seconds);
    steps.add({
      'force': force,
      'seconds': seconds,
      'p': _list(agent.position),
      'v': _list(agent.velocity),
      'speed': agent.speed,
    });
  }
  return {'op': 'integrate', 'self': self, 'steps': steps};
}

Map<String, Object?> _headingCase(_Rng r) {
  final velocity = r.pick([
    [0.0, 0.0, 0.0],
    [1e-10, 0.0, 0.0],
    [1e-9, 0.0, 0.0],
    [2e-9, 0.0, 0.0],
    [r.between(-4, 4), 0.0, r.between(-4, 4)],
  ]);
  final agent = Steerable(velocity: _vec(velocity));
  return {
    'op': 'heading',
    'v': velocity,
    'heading': agent.heading == null ? null : _list(agent.heading!),
  };
}

/// Builds a tree from a spec, and records what each leaf is asked.
final class _Tree {
  _Tree(this.calls);

  final List<int> calls;
  final _counts = <int, int>{};

  /// Every label, children before their parent.
  final labels = <String>[];

  Object? _next(int id, List<Object?> script) {
    final n = _counts[id] ?? 0;
    _counts[id] = n + 1;
    return script[n % script.length];
  }

  Node build(Map<String, Object?> s) {
    final node = _make(s);
    labels.add(node.label);
    return node;
  }

  Node _make(Map<String, Object?> s) {
    final id = s['id'] as int?;
    List<Node> children() => [
      for (final c in s['c']! as List) build(c as Map<String, Object?>),
    ];
    Node child() => build(s['child']! as Map<String, Object?>);
    final seconds = s['seconds'] as double?;
    return switch (s['t']) {
      'seq' => Sequence(children()),
      'sel' => Selector(children()),
      'par' => Parallel(
        children(),
        succeedAfter: s['succeedAfter'] as int?,
        failAfter: s['failAfter']! as int,
      ),
      'invert' => Invert(child()),
      'succeed' => Succeed(child()),
      'repeat' => Repeat(child(), times: s['times'] as int?),
      'wait' => Wait(seconds!),
      'cooldown' => Cooldown(child(), seconds!),
      'deadline' => Deadline(child(), seconds!),
      'do' => Do('do$id', (tick) {
        calls.add(id!);
        return Status.values[_next(id, s['script']! as List<Object?>)! as int];
      }),
      'check' => Check('check$id', (tick) {
        calls.add(id!);
        return _next(id, s['script']! as List<Object?>)! as bool;
      }),
      'act' => Act('act$id', (tick) {
        calls.add(id!);
        tick.write('acts', (tick.read<int>('acts') ?? 0) + 1);
      }),
      _ => throw StateError('unknown node ${s['t']}'),
    };
  }
}

Map<String, Object?> _nodeSpec(_Rng r, List<int> ids, int depth) {
  final id = ids[0]++;
  final kinds = depth == 0
      ? ['do', 'check', 'act', 'wait']
      : [
          'seq',
          'sel',
          'par',
          'seq',
          'sel',
          'invert',
          'succeed',
          'repeat',
          'wait',
          'cooldown',
          'deadline',
          'do',
          'do',
          'check',
          'act',
        ];
  final kind = r.pick(kinds);
  final spec = <String, Object?>{'t': kind, 'id': id};
  switch (kind) {
    case 'seq' || 'sel' || 'par':
      spec['c'] = [
        for (var i = r.chance(12) ? 0 : 1 + r.next(3); i > 0; i--)
          _nodeSpec(r, ids, depth - 1),
      ];
      if (kind == 'par') {
        spec['succeedAfter'] = r.pick([null, 1, 2]);
        spec['failAfter'] = r.pick([1, 2]);
      }
    case 'invert' || 'succeed' || 'repeat' || 'cooldown' || 'deadline':
      spec['child'] = _nodeSpec(r, ids, depth - 1);
      if (kind == 'repeat') spec['times'] = r.pick([null, 1, 2, 3]);
    case 'do':
      spec['script'] = [for (var i = 1 + r.next(4); i > 0; i--) r.next(3)];
    case 'check':
      spec['script'] = [for (var i = 1 + r.next(3); i > 0; i--) r.chance(2)];
  }
  if (kind == 'wait' || kind == 'cooldown' || kind == 'deadline') {
    spec['seconds'] = r.pick([0.0, 0.1, 0.25, 0.5, 1.0]);
  }
  return spec;
}

Map<String, Object?> _treeCase(_Rng r) {
  final spec = _nodeSpec(r, [0], 3);
  final calls = <int>[];
  final tree = _Tree(calls);
  final brain = Brain(tree.build(spec));
  final blackboard = <String, Object?>{};
  final ticks = <Map<String, Object?>>[];
  for (var i = 0; i < 14; i++) {
    final seconds = r.pick([0.0, 0.05, 0.1, 0.25, 0.5]);
    final interrupt = r.chance(10);
    calls.clear();
    if (interrupt) brain.interrupt();
    final status = brain.tick(seconds, blackboard: blackboard);
    ticks.add({
      'seconds': seconds,
      'interrupt': interrupt,
      'status': status.index,
      'calls': List<int>.of(calls),
      'memory': brain.memory.length,
      'acts': blackboard['acts'],
    });
  }
  return {'op': 'tree', 'spec': spec, 'labels': tree.labels, 'ticks': ticks};
}

/// A label shows the seconds as Dart writes them, so a number with an
/// awkward text is a case of its own.
List<Map<String, Object?>> _labelCases() => [
  for (final seconds in [
    0.25,
    1.0,
    100.0,
    0.0,
    -0.0,
    -1.0,
    0.0001,
    0.00012345,
    123456.789,
    999999999999999.0,
    0.1 + 0.2,
    3.0,
    _nan,
    _inf,
    -_inf,
  ])
    {
      'op': 'label',
      'seconds': seconds,
      'wait': Wait(seconds).label,
      'cooldown': Cooldown(const Wait(1), seconds).label,
      'deadline': Deadline(const Wait(1), seconds).label,
    },
];

/// Fifty walkers a side crossing a plaza in opposite directions, each avoiding
/// the others, for [seconds]. Where they end is what is compared.
Map<String, Object?> _plazaCase(double seconds) {
  final crowd = <(Steerable, Vector3)>[];
  for (var i = 0; i < 50; i++) {
    final row = i % 10;
    final column = i ~/ 10;
    final z = row * 2.0 - 9;
    crowd.add((
      Steerable(
        position: Vector3(-12 - column * 1.1, 0, z),
        maxSpeed: 2,
        maxForce: 20,
        radius: 0.4,
      ),
      Vector3(24, 0, z),
    ));
    crowd.add((
      Steerable(
        position: Vector3(12 + column * 1.1, 0, z + 1.0),
        maxSpeed: 2,
        maxForce: 20,
        radius: 0.4,
      ),
      Vector3(-24, 0, z + 1.0),
    ));
  }
  final agents = [for (final (agent, _) in crowd) agent];
  const step = 1.0 / 20;
  for (var t = 0.0; t < seconds; t += step) {
    for (final (agent, goal) in crowd) {
      final toGoal = goal - agent.position;
      final wanted = toGoal.length < 0.1
          ? Vector3.zero()
          : toGoal.normalized() * agent.maxSpeed;
      agent.integrate(Avoid(wanted, agents).force(agent), step);
    }
  }
  return {
    'op': 'plaza',
    'seconds': seconds,
    'end': [for (final agent in agents) _list(agent.position)],
  };
}

void main(List<String> args) {
  if (args.length != 1) {
    throw ArgumentError('Usage: dart run bin/agent.dart <repository root>');
  }
  final r = _Rng(2718);
  final cases = <Map<String, Object?>>[
    for (var i = 0; i < 1200; i++) _forceCase(r),
    ..._avoidRefusals(),
    for (var i = 0; i < 100; i++) _integrateCase(r),
    for (var i = 0; i < 40; i++) _headingCase(r),
    for (var i = 0; i < 250; i++) _treeCase(r),
    ..._labelCases(),
    _plazaCase(4),
  ];
  writeFixture(
    root: args[0],
    package: 'orblit_agent',
    name: 'agent',
    cases: [for (final c in cases) _encode(c)! as Map<String, Object?>],
  );
}
