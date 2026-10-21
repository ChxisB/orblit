import 'dart:math' as math;

import 'package:orblit_agent/orblit_agent.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_path/orblit_path.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

Map<String, Object?> _case(
  String op,
  List<Object?> input,
  Object? Function() body,
) => {'op': op, 'in': input, 'expect': attempt(body)};

double _double(Object? value) =>
    value is String ? double.parse(value) : (value as num).toDouble();

List<Object> _numbers(Iterable<double> values) => [
  for (final v in values) number(v),
];

List<Object> _vector(Vector3 v) => _numbers(v.storage);

Vector3 _at(Object? spec) {
  final list = spec as List<Object?>;
  return Vector3(_double(list[0]), _double(list[1]), _double(list[2]));
}

List<Vector3> _points(Object? spec) => [
  for (final p in spec as List<Object?>) _at(p),
];

Object? _pathOut(List<Vector3>? path) =>
    path == null ? null : [for (final p in path) _vector(p)];

// A vector as a spec holds it. Numbers are doubles or the strings a fixture
// uses for NaN and infinity, which `_double` reads back.
List<Object> _v(double x, double y, double z) => [
  number(x),
  number(y),
  number(z),
];

// A grid.

// A grid spec is [width, depth, cell, originX, originZ, edits]. An edit is a
// list that starts with what it does.
class _Made {
  _Made(this.grid, this.obstacles);

  final NavGrid grid;
  final List<NavObstacle> obstacles;
}

_Made _grid(List<Object?> spec) {
  final grid = NavGrid(
    width: spec[0] as int,
    depth: spec[1] as int,
    cell: _double(spec[2]),
    originX: _double(spec[3]),
    originZ: _double(spec[4]),
  );
  final made = _Made(grid, []);
  for (final edit in spec[5] as List<Object?>) {
    _edit(made, edit as List<Object?>);
  }
  return made;
}

void _edit(_Made made, List<Object?> e) {
  final grid = made.grid;
  switch (e[0]) {
    case 'rect':
      grid.blockRect(
        minX: _double(e[1]),
        minZ: _double(e[2]),
        maxX: _double(e[3]),
        maxZ: _double(e[4]),
      );
    case 'circle':
      grid.blockCircle(_double(e[1]), _double(e[2]), _double(e[3]));
    case 'open':
      grid.setOpen(e[1] as int, e[2] as int, open: e[3] as bool);
    case 'height':
      grid.setHeight(e[1] as int, e[2] as int, _double(e[3]));
    case 'weight':
      grid.setWeight(e[1] as int, e[2] as int, _double(e[3]));
    case 'obRect':
      made.obstacles.add(
        grid.addObstacleRect(
          minX: _double(e[1]),
          minZ: _double(e[2]),
          maxX: _double(e[3]),
          maxZ: _double(e[4]),
        ),
      );
    case 'obCircle':
      made.obstacles.add(
        grid.addObstacleCircle(_double(e[1]), _double(e[2]), _double(e[3])),
      );
    default:
      made.obstacles[e[1] as int].lift();
  }
}

// Every cell and the ring round the grid, as flags and numbers. A big grid
// gives one sum a row for the numbers, which still shows a wrong cell.
Map<String, Object?> _dump(
  NavGrid grid, {
  List<NavObstacle> obstacles = const [],
  bool rows = false,
}) {
  final open = StringBuffer();
  final heights = <Object>[];
  final weights = <Object>[];
  for (var j = -1; j <= grid.depth; j++) {
    var heightSum = 0.0;
    var weightSum = 0.0;
    for (var i = -1; i <= grid.width; i++) {
      open.write(grid.isOpen(i, j) ? '1' : '0');
      if (rows) {
        heightSum += grid.heightOf(i, j);
        weightSum += grid.weightOf(i, j);
      } else {
        heights.add(number(grid.heightOf(i, j)));
        weights.add(number(grid.weightOf(i, j)));
      }
    }
    if (rows) {
      heights.add(number(heightSum));
      weights.add(number(weightSum));
    }
  }
  return {
    'width': grid.width,
    'depth': grid.depth,
    'cell': number(grid.cell),
    'originX': number(grid.originX),
    'originZ': number(grid.originZ),
    'open': open.toString(),
    'heights': heights,
    'weights': weights,
    'standing': [for (final o in obstacles) o.standing],
  };
}

List<Object?> _cellOut(NavCell? at) => at == null ? [] : [at.i, at.j];

// The edits that build one scenario. Each is a prefix of the next, so a
// refusal part way is seen as well as the end state.
const List<List<Object?>> _scenarios = [
  [],
  [
    ['rect', 2.0, 2.0, 4.5, 4.5],
  ],
  [
    ['circle', 5.0, 5.0, 2.5],
    ['open', 0, 0, false],
    ['open', 1, 1, true],
    ['height', 3, 3, 2.5],
    ['height', 4, 4, -1.25],
    ['weight', 3, 4, 3.5],
    ['weight', 5, 5, 10],
  ],
  [
    ['obRect', 3.0, 0.0, 3.9, 6.0],
    ['obRect', 3.0, 3.0, 4.2, 4.2],
    ['obCircle', 7.0, 7.0, 1.5],
    ['lift', 1],
    ['lift', 0],
  ],
  [
    ['rect', 3.0, -5.0, 3.0, 20.0],
    ['open', 3, 4, true],
    ['weight', 2, 2, 4],
    ['weight', 2, 3, 4],
    ['weight', 2, 4, 4],
    ['circle', 9.5, 2.5, 1.2],
    ['obCircle', 9.5, 8.0, 2.0],
  ],
  [
    ['rect', -100.0, -100.0, 100.0, 100.0],
    ['open', 5, 5, true],
    ['obRect', 4.0, 4.0, 6.0, 6.0],
    ['lift', 0],
  ],
  [
    ['rect', 5.0, 5.0, 2.0, 2.0],
    ['circle', -10.0, 4.0, 3.0],
    ['circle', 4.0, 4.0, 0.0],
    ['circle', 4.5, 4.5, 0.0],
    ['obCircle', 4.5, 4.5, 0.0],
    ['obRect', 9.0, 9.0, 9.0, 9.0],
    ['lift', 0],
    ['lift', 0],
  ],
];

const List<List<Object?>> _shapes = [
  [10, 10, 1.0, 0.0, 0.0],
  [12, 9, 0.5, -3.0, 5.5],
  [8, 8, 2.0, 100.0, -100.0],
  [1, 1, 1.0, 0.0, 0.0],
  [16, 4, 1.5, -12.0, -3.0],
  [9, 9, 0.25, 0.1, 0.1],
];

List<List<Object?>> _gridSpecs() => [
  for (final shape in _shapes)
    for (final edits in _scenarios) [...shape, edits],
];

// Each prefix of each scenario on the first shape, so an edit that refuses
// is seen part way.
List<List<Object?>> _prefixSpecs() => [
  for (final edits in _scenarios)
    for (var k = 1; k < edits.length; k++) [..._shapes[0], edits.sublist(0, k)],
];

List<List<Object?>> _randomGrids(int count, int seed) {
  final random = math.Random(seed);
  const cells = <double>[0.5, 1.0, 2.0, 0.25, 1.5];
  const origins = <double>[0.0, -3.0, 5.5, 100.0];
  final out = <List<Object?>>[];
  for (var n = 0; n < count; n++) {
    final width = 2 + random.nextInt(13);
    final depth = 2 + random.nextInt(13);
    final cell = cells[random.nextInt(cells.length)];
    final ox = origins[random.nextInt(origins.length)];
    final oz = origins[random.nextInt(origins.length)];
    final wide = width * cell;
    final deep = depth * cell;
    double x() => ox + random.nextDouble() * wide;
    double z() => oz + random.nextDouble() * deep;
    var obstacles = 0;
    final edits = <List<Object?>>[];
    for (var k = random.nextInt(9); k > 0; k--) {
      switch (random.nextInt(8)) {
        case 0:
          edits.add(['rect', x(), z(), x(), z()]);
        case 1:
          edits.add(['circle', x(), z(), random.nextDouble() * cell * 3]);
        case 2:
          edits.add([
            'open',
            random.nextInt(width),
            random.nextInt(depth),
            random.nextBool(),
          ]);
        case 3:
          edits.add([
            'height',
            random.nextInt(width),
            random.nextInt(depth),
            random.nextDouble() * 3 - 1,
          ]);
        case 4:
          edits.add([
            'weight',
            random.nextInt(width),
            random.nextInt(depth),
            1 + random.nextDouble() * 6,
          ]);
        case 5:
          edits.add(['obRect', x(), z(), x(), z()]);
          obstacles++;
        case 6:
          edits.add(['obCircle', x(), z(), random.nextDouble() * cell * 2]);
          obstacles++;
        default:
          if (obstacles > 0) edits.add(['lift', random.nextInt(obstacles)]);
      }
    }
    out.add([width, depth, cell, ox, oz, edits]);
  }
  return out;
}

// The places worth asking a grid about: every cell's middle and corner, a
// point either side of each border, off the grid, and the awkward numbers.
List<List<Object>> _probes(NavGrid grid) {
  final out = <List<Object>>[];
  double gx(num i) => grid.originX + i * grid.cell;
  double gz(num j) => grid.originZ + j * grid.cell;
  for (var j = -1; j <= grid.depth; j++) {
    for (var i = -1; i <= grid.width; i++) {
      out.add(_v(gx(i + 0.5), 0.0, gz(j + 0.5)));
      out.add(_v(gx(i), 0.0, gz(j)));
    }
  }
  for (final i in [0, grid.width ~/ 2, grid.width]) {
    out.add(_v(gx(i) - 1e-9, 0.0, gz(1) + 1e-9));
    out.add(_v(gx(i) + 1e-9, 0.0, gz(1) - 1e-9));
  }
  out.add(_v(gx(-40), 0.0, gz(-40)));
  out.add(_v(gx(grid.width + 40), 0.0, gz(grid.depth + 40)));
  out.add(_v(1e15, 0.0, 1e15));
  out.add(_v(-1e15, 0.0, 3.0));
  out.add(_v(1e300, 0.0, 0.0));
  out.add(_v(-1e300, 0.0, -1e300));
  out.add(_v(_nan, 0.0, 0.0));
  out.add(_v(0.0, 0.0, _nan));
  out.add(_v(_inf, 0.0, 0.0));
  out.add(_v(0.0, 0.0, -_inf));
  return out;
}

List<Map<String, Object?>> _gridCases() {
  final cases = <Map<String, Object?>>[];
  for (final spec in _prefixSpecs()) {
    cases.add(_case('cells', [spec], () => _cells(spec)));
  }
  for (final spec in [..._gridSpecs(), ..._randomGrids(40, 5)]) {
    cases.add(_case('cells', [spec], () => _cells(spec)));
    final grid = _grid(spec).grid;
    for (final radius in const [0.0, 0.6, 2.5]) {
      cases.add(
        _case('inflated', [spec, radius], () {
          return _dump(_grid(spec).grid.inflated(radius));
        }),
      );
    }
    final probes = _probes(grid);
    for (var k = 0; k < probes.length; k += 16) {
      final p = probes[k];
      cases.add(
        _case('cell', [spec, p[0], p[2]], () {
          return _cellOut(
            _grid(spec).grid.cellAt(_double(p[0]), _double(p[2])),
          );
        }),
      );
      for (final within in const [8, 0, 2]) {
        if (within != 8 && k % 32 != 0) continue;
        // A far cell saturates to the biggest int, and with no reach the
        // scan then never ends, in Dart and in the port alike.
        if (within == 0 && (_double(p[0]) > 1e18 || _double(p[2]) > 1e18)) {
          continue;
        }
        cases.add(
          _case('nearest', [spec, p[0], p[2], within], () {
            final at = _grid(
              spec,
            ).grid.nearestOpen(_double(p[0]), _double(p[2]), within: within);
            return _cellOut(at);
          }),
        );
      }
    }
    for (var j = -2; j <= grid.depth + 1; j += 4) {
      for (var i = -2; i <= grid.width + 1; i += 4) {
        cases.add(
          _case('centre', [
            spec,
            i,
            j,
          ], () => _vector(_grid(spec).grid.centreOf(i, j))),
        );
      }
    }
  }
  return cases;
}

Map<String, Object?> _cells(List<Object?> spec) {
  final made = _grid(spec);
  return _dump(made.grid, obstacles: made.obstacles);
}

// Lines and routes: pairs from the probes of one grid.
List<Map<String, Object?>> _lineCases() {
  final random = math.Random(11);
  final cases = <Map<String, Object?>>[];
  final specs = [..._gridSpecs(), ..._randomGrids(50, 21)];
  for (final spec in specs) {
    final grid = _grid(spec).grid;
    final probes = _probes(grid);
    final finite = probes
        .where((p) => _double(p[0]).isFinite && _double(p[2]).isFinite)
        .toList();
    final pairs = <List<List<Object>>>[];
    for (var n = 0; n < 14; n++) {
      pairs.add([
        finite[random.nextInt(finite.length)],
        finite[random.nextInt(finite.length)],
      ]);
    }
    final awkward = probes
        .where((p) => !(_double(p[0]).isFinite && _double(p[2]).isFinite))
        .toList();
    for (final bad in awkward.take(spec[0] == 10 ? 8 : 2)) {
      pairs.add([finite[0], bad]);
      pairs.add([bad, finite[1]]);
    }
    final corner = grid.centreOf(0, 0);
    pairs.add([
      _v(corner.x, 0.0, corner.z),
      _v(corner.x + grid.cell, 0.0, corner.z + grid.cell),
    ]);
    for (final pair in pairs) {
      cases.add(
        _case('clear', [spec, pair[0], pair[1]], () {
          return _grid(spec).grid.isClear(_at(pair[0]), _at(pair[1]));
        }),
      );
    }
    for (final pair in pairs.take(8)) {
      cases.add(
        _case('path', [spec, pair[0], pair[1], null], () {
          return _pathOut(
            _grid(spec).grid.findPath(_at(pair[0]), _at(pair[1])),
          );
        }),
      );
    }
    for (final pair in pairs.take(4)) {
      for (final radius in const [0.6, 1.2]) {
        cases.add(
          _case('path', [spec, pair[0], pair[1], radius], () {
            final fat = _grid(spec).grid.inflated(radius);
            return _pathOut(fat.findPath(_at(pair[0]), _at(pair[1])));
          }),
        );
        cases.add(
          _case('mesh', [spec, radius, pair[0], pair[1]], () {
            final mesh = NavMesh.fromGrid(_grid(spec).grid, radius: radius);
            return {
              'count': mesh.count,
              'path': _pathOut(mesh.findPath(_at(pair[0]), _at(pair[1]))),
            };
          }),
        );
      }
      cases.add(
        _case('mesh', [spec, 0.0, pair[0], pair[1]], () {
          final mesh = NavMesh.fromGrid(_grid(spec).grid);
          return {
            'count': mesh.count,
            'path': _pathOut(mesh.findPath(_at(pair[0]), _at(pair[1]))),
          };
        }),
      );
    }
  }
  return cases;
}

// Specials: a weight past what a float holds, and a cell count that wraps.
List<Map<String, Object?>> _gridEdgeCases() {
  return [
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['weight', 1, 1, 1e300],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)
          ..setWeight(1, 1, 1e300);
        return _dump(grid);
      },
    ),
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['weight', 1, 1, 1e39],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)
          ..setWeight(1, 1, 1e39);
        return _dump(grid);
      },
    ),
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['weight', 1, 1, 0.5],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)..setWeight(1, 1, 0.5);
        return _dump(grid);
      },
    ),
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['weight', 1, 1, 1.0],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)..setWeight(1, 1, 1);
        return _dump(grid);
      },
    ),
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['weight', 1, 1, number(_nan)],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)
          ..setWeight(1, 1, _nan);
        return _dump(grid);
      },
    ),
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['height', 1, 1, 1e39],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)
          ..setHeight(1, 1, 1e39);
        return _dump(grid);
      },
    ),
    _case(
      'cells',
      [
        [
          3,
          3,
          1.0,
          0.0,
          0.0,
          [
            ['height', 1, 1, 0.1],
          ],
        ],
      ],
      () {
        final grid = NavGrid(width: 3, depth: 3, cell: 1)..setHeight(1, 1, 0.1);
        return _dump(grid);
      },
    ),
    for (final cell in const [0.0, -1.0, _nan, _inf, 1e-300, 1e300])
      _case(
        'cells',
        [
          [4, 4, number(cell), 0.0, 0.0, <Object?>[]],
        ],
        () {
          return _dump(NavGrid(width: 4, depth: 4, cell: cell));
        },
      ),
    for (final size in const [(0, 4), (4, 0), (-1, 4), (4, -1), (-2, -2)])
      _case(
        'cells',
        [
          [size.$1, size.$2, 1.0, 0.0, 0.0, <Object?>[]],
        ],
        () {
          return _dump(NavGrid(width: size.$1, depth: size.$2, cell: 1));
        },
      ),
  ];
}

// A walker.

// A walker spec is [planner, radius, withClear, lookahead, body]. The planner
// is a grid spec, so a replan sees obstacles added or lifted along the way.
Map<String, Object?> _walkerState(NavWalker w) => {
  'position': _vector(w.body.position),
  'velocity': _vector(w.body.velocity),
  'goal': w.goal == null ? null : _vector(w.goal!),
  'route': _pathOut(w.route),
  'hasRoute': w.hasRoute,
  'arrived': w.isArrived,
};

List<Map<String, Object?>> _walkerCases() {
  final random = math.Random(31);
  final cases = <Map<String, Object?>>[];
  final specs = [
    ..._gridSpecs().where((s) => s[0] == 10 || s[0] == 12).take(8),
    ..._randomGrids(6, 77),
  ];
  for (final spec in specs) {
    final grid = _grid(spec).grid;
    double x() => grid.originX + random.nextDouble() * grid.width * grid.cell;
    double z() => grid.originZ + random.nextDouble() * grid.depth * grid.cell;
    for (final look in const [1, 3, 9]) {
      for (final clear in const [true, false]) {
        final commands = <List<Object?>>[
          ['goto', _v(x(), 0.0, z())],
          for (var n = 0; n < 24; n++) ['update', 1 / 20],
          if (clear) ['obRect', x(), z(), x(), z()],
          if (clear) ['obCircle', x(), z(), grid.cell],
          for (var n = 0; n < 16; n++) ['update', 1 / 20],
          if (clear) ['lift', 0],
          ['goto', _v(x(), 0.0, z())],
          for (var n = 0; n < 24; n++) ['update', 1 / 20],
          ['stop'],
          ['update', 1 / 20],
        ];
        final body = _v(x(), 0.0, z());
        cases.add(
          _case('walker', [spec, clear, look, body, commands], () {
            return _runWalker(spec, clear, look, body, commands);
          }),
        );
      }
    }
  }
  final small = <Object?>[4, 4, 1.0, 0.0, 0.0, <Object?>[]];
  cases.add(
    _case('walker', [small, false, 0, _v(0.5, 0.0, 0.5), <Object?>[]], () {
      return _runWalker(small, false, 0, _v(0.5, 0.0, 0.5), []);
    }),
  );
  return cases;
}

List<Object?> _runWalker(
  List<Object?> spec,
  bool withClear,
  int lookahead,
  List<Object?> body,
  List<List<Object?>> commands,
) {
  final made = _grid(spec);
  final walker = NavWalker(
    body: Steerable(position: _at(body), maxSpeed: 5, maxForce: 40),
    planner: made.grid.findPath,
    isClear: withClear ? made.grid.isClear : null,
    lookahead: lookahead,
  );
  final states = <Object?>[_walkerState(walker)];
  for (var n = 0; n < commands.length; n++) {
    final c = commands[n];
    switch (c[0]) {
      case 'goto':
        walker.goTo(_at(c[1]));
      case 'update':
        walker.update(_double(c[1]));
      case 'stop':
        walker.stop();
      default:
        _edit(made, c);
    }
    if (c[0] != 'update' || n % 4 == 0) states.add(_walkerState(walker));
  }
  return states;
}

// Links.

NavLink _link(List<Object?> spec) => NavLink(
  from: _at(spec[0]),
  to: _at(spec[1]),
  kind: spec[2] as String,
  cost: _double(spec[3]),
  agents: {for (final a in spec[4] as List<Object?>) a as String},
  gate: spec[5] as String?,
  twoWay: spec[6] as bool,
);

Object? _routeOut(List<RouteStep>? route, List<NavLink> links) {
  if (route == null) return null;
  return [
    for (final step in route)
      switch (step) {
        WalkStep(:final points) => ['walk', _pathOut(points)],
        LinkStep(:final link, :final reversed) => [
          'link',
          links.indexOf(link),
          reversed,
        ],
      },
  ];
}

Map<String, Object?> _linksCase(
  List<Object?> ground,
  List<List<Object?>> set,
  String agent,
  List<String> locks,
  bool useMesh,
  List<Object> from,
  List<Object> to,
) => _case('links', [ground, set, agent, locks, useMesh, from, to], () {
  final grid = _grid(ground).grid;
  final links = [for (final l in set) _link(l)];
  final mesh = NavMesh.fromGrid(grid);
  final planner = LinkPlanner(
    ground: useMesh ? mesh.findPath : grid.findPath,
    links: links,
  );
  for (final gate in locks) {
    planner.lock(gate);
  }
  final route = planner.findRoute(_at(from), _at(to), agent: agent);
  return {
    'route': _routeOut(route, links),
    'locked': [
      for (final g in const ['gate', 'other']) planner.isLocked(g),
    ],
  };
});

// Two links that cost the same to reach and to cross, so the first listed is
// the one a route takes.
List<Map<String, Object?>> _linkTieCases() {
  final river = <Object?>[
    12,
    8,
    1.0,
    0.0,
    0.0,
    [
      ['rect', 5.0, 0.0, 5.0, 8.0],
    ],
  ];
  List<Object?> ladder(double z) => [
    _v(4.5, 0.0, z),
    _v(6.5, 0.0, z),
    'ladder',
    1.0,
    <Object?>[],
    null,
    false,
  ];
  final pair = [ladder(1.5), ladder(5.5)];
  return [
    for (final useMesh in const [false, true])
      for (final swap in const [false, true])
        _linksCase(
          river,
          swap ? pair.reversed.toList() : pair,
          '',
          const [],
          useMesh,
          _v(3.5, 0.0, 3.5),
          _v(10.5, 0.0, 3.5),
        ),
  ];
}

List<Map<String, Object?>> _linkCases() {
  final cases = <Map<String, Object?>>[];
  // A river down x = 5 that only a link crosses, and a second one further on.
  final river = <Object?>[
    12,
    8,
    1.0,
    0.0,
    0.0,
    [
      ['rect', 5.0, 0.0, 5.0, 8.0],
    ],
  ];
  final plain = <Object?>[12, 8, 1.0, 0.0, 0.0, <Object?>[]];
  final linkSets = <List<List<Object?>>>[
    [],
    [
      [
        _v(4.5, 0.0, 4.5),
        _v(6.5, 0.0, 4.5),
        'ladder',
        3.0,
        <Object?>[],
        null,
        false,
      ],
    ],
    [
      [
        _v(4.5, 0.0, 4.5),
        _v(6.5, 0.0, 4.5),
        'ladder',
        3.0,
        <Object?>[],
        null,
        true,
      ],
      [
        _v(4.5, 0.0, 1.5),
        _v(6.5, 0.0, 1.5),
        'rope',
        0.0,
        ['goblin'],
        'gate',
        false,
      ],
    ],
    [
      [
        _v(4.5, 0.0, 4.5),
        _v(6.5, 0.0, 4.5),
        'ladder',
        100.0,
        <Object?>[],
        null,
        true,
      ],
      [
        _v(4.5, 0.0, 1.5),
        _v(6.5, 0.0, 1.5),
        'rope',
        0.0,
        ['goblin', 'imp'],
        'gate',
        true,
      ],
      [
        _v(4.5, 0.0, 6.5),
        _v(6.5, 0.0, 6.5),
        'jump',
        1.0,
        <Object?>[],
        'gate',
        false,
      ],
    ],
    [
      [
        _v(1.5, 0.0, 1.5),
        _v(10.5, 0.0, 6.5),
        'zip',
        0.5,
        <Object?>[],
        null,
        false,
      ],
      [
        _v(10.5, 0.0, 6.5),
        _v(1.5, 0.0, 1.5),
        'zip',
        0.5,
        <Object?>[],
        null,
        false,
      ],
    ],
  ];
  const queries = <List<List<double>>>[
    [
      [1.5, 4.5],
      [10.5, 4.5],
    ],
    [
      [10.5, 4.5],
      [1.5, 4.5],
    ],
    [
      [1.5, 1.5],
      [10.5, 1.5],
    ],
    [
      [1.5, 6.5],
      [10.5, 6.5],
    ],
    [
      [2.5, 2.5],
      [4.5, 5.5],
    ],
    [
      [1.5, 1.5],
      [1.5, 6.5],
    ],
  ];
  for (final ground in [river, plain]) {
    for (final set in linkSets) {
      for (final agent in const ['', 'goblin', 'imp', 'dwarf']) {
        for (final locks in const [
          <String>[],
          ['gate'],
        ]) {
          for (final useMesh in const [false, true]) {
            for (final q in queries) {
              final from = _v(q[0][0], 0.0, q[0][1]);
              final to = _v(q[1][0], 0.0, q[1][1]);
              cases.add(
                _linksCase(ground, set, agent, locks, useMesh, from, to),
              );
            }
          }
        }
      }
    }
  }
  // A lock and an unlock, in one planner.
  cases.add(
    _case('linkLock', [river, linkSets[2]], () {
      final grid = _grid(river).grid;
      final links = [for (final l in linkSets[2]) _link(l)];
      final planner = LinkPlanner(ground: grid.findPath, links: links)
        ..lock('gate');
      final shut = planner.findRoute(
        _at(_v(1.5, 0.0, 1.5)),
        _at(_v(10.5, 0.0, 1.5)),
        agent: 'goblin',
      );
      planner.unlock('gate');
      final open = planner.findRoute(
        _at(_v(1.5, 0.0, 1.5)),
        _at(_v(10.5, 0.0, 1.5)),
        agent: 'goblin',
      );
      return {
        'shut': _routeOut(shut, links),
        'open': _routeOut(open, links),
        'locked': planner.isLocked('gate'),
      };
    }),
  );
  // A link that refuses to be made.
  for (final bad in [
    [_v(0.0, 0.0, 0.0), _v(1.0, 0.0, 0.0), '', 0.0, <Object?>[], null, false],
    [_v(0.0, 0.0, 0.0), _v(1.0, 0.0, 0.0), 'a', -1.0, <Object?>[], null, false],
    [
      _v(0.0, 0.0, 0.0),
      _v(1.0, 0.0, 0.0),
      'a',
      number(_nan),
      <Object?>[],
      null,
      false,
    ],
    [
      _v(0.0, 0.0, 0.0),
      _v(1.0, 0.0, 0.0),
      'a',
      number(_inf),
      <Object?>[],
      null,
      false,
    ],
    [_v(0.0, 0.0, 0.0), _v(1.0, 0.0, 0.0), 'a', 0.0, <Object?>[], null, true],
    [_v(0.0, 0.0, 0.0), _v(1.0, 0.0, 0.0), 'a', 1e300, <Object?>[], '', true],
  ]) {
    cases.add(_case('linkMake', [bad], () => _link(bad).kind.length));
  }
  return cases;
}

// Splines.

Spline _spline(List<Object?> spec) {
  final closed = spec[2] as bool;
  switch (spec[0]) {
    case 'bezier':
      return Spline.bezier([
        for (final k in spec[1] as List<Object?>)
          PathKnot(
            _at((k as List<Object?>)[0]),
            tilt: _double(k[1]),
            handleIn: k[2] == null ? null : _at(k[2]),
            handleOut: k[3] == null ? null : _at(k[3]),
          ),
      ], closed: closed);
    case 'through':
      return Spline.through(
        _points(spec[1]),
        tilts: spec[3] == null ? null : _list(spec[3]),
        closed: closed,
      );
    default:
      return Spline.line(
        _points(spec[1]),
        tilts: spec[3] == null ? null : _list(spec[3]),
        closed: closed,
      );
  }
}

List<double> _list(Object? value) => [
  for (final v in value as List<Object?>) _double(v),
];

List<Object?> _knot(
  List<double> at, {
  double tilt = 0,
  List<double>? hin,
  List<double>? hout,
}) => [
  _numbers(at),
  number(tilt),
  hin == null ? null : _numbers(hin),
  hout == null ? null : _numbers(hout),
];

List<List<Object?>> _splineSpecs() {
  final random = math.Random(41);
  List<Object> pt() => _v(
    (random.nextDouble() - 0.5) * 20,
    (random.nextDouble() - 0.5) * 6,
    (random.nextDouble() - 0.5) * 20,
  );
  final specs = <List<Object?>>[
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(10.0, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(10.0, 0.0, 0.0), _v(10.0, 0.0, 10.0)],
      false,
      null,
    ],
    [
      'line',
      [
        _v(0.0, 0.0, 0.0),
        _v(10.0, 0.0, 0.0),
        _v(10.0, 0.0, 10.0),
        _v(0.0, 0.0, 10.0),
      ],
      true,
      null,
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(10.0, 0.0, 0.0)],
      false,
      [0.0, 1.5],
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(0.0, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'line',
      [_v(1.0, 2.0, 3.0), _v(1.0, 2.0, 3.0), _v(1.0, 2.0, 3.0)],
      true,
      null,
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(0.0, 8.0, 0.0)],
      false,
      null,
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(0.0, 8.0, 0.0), _v(0.0, 0.0, 8.0)],
      false,
      [0.0, 3.0, -3.0],
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(10.0, 0.0, 0.0)],
      false,
      [0.0],
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0)],
      false,
      null,
    ],
    ['line', <Object?>[], false, null],
    [
      'line',
      [_v(1.0, 2.0, 3.0), _v(1.0, 2.0, 3.0), _v(1.0, 2.0, 3.0)],
      true,
      [0.0, 0.7, 1.4],
    ],
    [
      'line',
      [_v(0.0, 0.0, 0.0), _v(0.0, 8.0, 1e-5)],
      false,
      null,
    ],
    // A slope and then a chord of exactly the length that counts as none.
    [
      'bezier',
      [
        _knot([0, 0, 0], hout: [1e-6 / 3, 0, 0]),
        _knot([0, 0, 3]),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        _knot([1e-6, 0, 0]),
      ],
      false,
      null,
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0), _v(5.0, 0.0, 0.0), _v(1e-9, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'through',
      [
        _v(0.0, 0.0, 0.0),
        _v(10.0, 0.0, 0.0),
        _v(10.0, 0.0, 10.0),
        _v(0.0, 3.0, 10.0),
      ],
      false,
      null,
    ],
    [
      'through',
      [
        _v(0.0, 0.0, 0.0),
        _v(10.0, 0.0, 0.0),
        _v(10.0, 0.0, 10.0),
        _v(0.0, 3.0, 10.0),
      ],
      true,
      [0.0, 0.5, 1.0, -1.0],
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0), _v(0.0, 0.0, 0.0), _v(5.0, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0), _v(5.0, 0.0, 0.0)],
      true,
      null,
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0), _v(1e-10, 0.0, 0.0), _v(0.0, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0), _v(1e-8, 0.0, 0.0), _v(2e-8, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0)],
      false,
      null,
    ],
    [
      'through',
      [_v(0.0, 0.0, 0.0), _v(1.0, 0.0, 0.0)],
      false,
      [1.0],
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0], hout: [3, 4, 0]),
        _knot([10, 0, 0], tilt: 0.7, hin: [-3, 4, 0]),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0], hout: [0, 0, 5]),
        _knot([10, 0, 0], hin: [0, 0, -5], hout: [0, 0, 5]),
        _knot([10, 0, 10], tilt: -2.0, hin: [5, 0, 0]),
      ],
      true,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        _knot([0, 0, 0]),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0], hout: [0, 9, 0]),
        _knot([0, 0, 0], hin: [0, 9, 0]),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0], tilt: 3.141592653589793),
        _knot([0, 6, 0], tilt: -3.141592653589793),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0], hout: [1e-7, 0, 0]),
        _knot([1e-7, 0, 0], hin: [-1e-7, 0, 0]),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        _knot([1e-5, 0, 1e-5]),
        _knot([0, 0, 0]),
      ],
      true,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        [_v(_nan, 0.0, 0.0), 0.0, null, null],
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        [_v(1.0, 0.0, 0.0), number(_inf), null, null],
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        [_v(1.0, 0.0, 0.0), 0.0, null, _v(0.0, _inf, 0.0)],
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0]),
        [_v(1.0, 0.0, 0.0), 0.0, _v(0.0, 0.0, _nan), null],
      ],
      false,
      null,
    ],
    [
      'bezier',
      [
        _knot([0, 0, 0], hout: [1e150, 0, 0]),
        _knot([1e150, 0, 0], hin: [-1e150, 0, 0]),
      ],
      false,
      null,
    ],
  ];
  for (var n = 0; n < 30; n++) {
    final count = 2 + random.nextInt(6);
    final closed = random.nextBool();
    final tilts = random.nextBool()
        ? [for (var i = 0; i < count; i++) (random.nextDouble() - 0.5) * 6]
        : null;
    final points = [for (var i = 0; i < count; i++) pt()];
    specs.add([n.isEven ? 'through' : 'line', points, closed, tilts]);
    if (n % 3 == 0) {
      specs.add([
        'bezier',
        [
          for (var i = 0; i < count; i++)
            [
              pt(),
              (random.nextDouble() - 0.5) * 4,
              random.nextBool()
                  ? _v(
                      random.nextDouble() * 4 - 2,
                      random.nextDouble() * 4 - 2,
                      random.nextDouble() * 4 - 2,
                    )
                  : null,
              random.nextBool()
                  ? _v(
                      random.nextDouble() * 4 - 2,
                      random.nextDouble() * 4 - 2,
                      random.nextDouble() * 4 - 2,
                    )
                  : null,
            ],
        ],
        closed,
        null,
      ]);
    }
  }
  return specs;
}

List<double> _distances(Spline s) {
  final length = s.length;
  return [
    0,
    -1,
    -1e9,
    1e-9,
    length * 0.25,
    length * 0.5,
    length / 3,
    length - 1e-9,
    length,
    length + 1,
    length * 2.5,
    length * 7,
    -length * 0.3,
    -length * 3.3,
    1e9,
  ];
}

List<Map<String, Object?>> _splineCases() {
  final cases = <Map<String, Object?>>[];
  final random = math.Random(43);
  for (final spec in _splineSpecs()) {
    cases.add(
      _case('spline', [spec], () {
        final s = _spline(spec);
        return {
          'length': number(s.length),
          'closed': s.closed,
          'count': s.knots.length,
          'text': s.encode(),
          'json': tree(s.toJson()),
        };
      }),
    );
    Spline? built;
    try {
      built = _spline(spec);
    } on Object {
      built = null;
    }
    if (built == null) continue;
    final s = built;
    final probes = _distances(s);
    for (final (index, d) in probes.indexed) {
      cases.add(
        _case('position', [
          spec,
          number(d),
        ], () => _vector(_spline(spec).positionAt(d))),
      );
      if (index.isOdd) continue;
      cases.add(
        _case('frame', [spec, number(d)], () {
          final f = _spline(spec).frameAt(d);
          return [
            _vector(f.position),
            _vector(f.forward),
            _vector(f.up),
            _vector(f.right),
          ];
        }),
      );
    }
    for (final d in [_nan, _inf, -_inf]) {
      cases.add(
        _case('position', [
          spec,
          number(d),
        ], () => _vector(_spline(spec).positionAt(d))),
      );
      cases.add(
        _case('forward', [
          spec,
          number(d),
        ], () => _vector(_spline(spec).frameAt(d).forward)),
      );
    }
    final targets = <Object>[
      _v(0.0, 0.0, 0.0),
      _v(5.0, 5.0, 5.0),
      _v(-8.0, 1.0, 3.0),
      _v(100.0, 0.0, -100.0),
      _v(s.positionAt(s.length / 2).x, 0.0, 0.0),
      _vector(s.positionAt(s.length * 0.3)),
      _vector(s.positionAt(0)),
      _vector(s.positionAt(s.length)),
      for (var k = 0; k < 5; k++)
        _v(
          random.nextDouble() * 24 - 12,
          random.nextDouble() * 6 - 3,
          random.nextDouble() * 24 - 12,
        ),
      _v(_nan, 0.0, 0.0),
    ];
    for (final target in targets) {
      cases.add(
        _case('closest', [spec, target], () {
          final n = _spline(spec).closest(_at(target));
          return [number(n.distance), _vector(n.position), number(n.offset)];
        }),
      );
    }
  }
  return cases;
}

// A text is decoded by the port, and what it makes is written out again.

String _valid(Object? knots, {Object? version = 1, Object? closed = false}) =>
    '{"kind": "orblit.path", "formatVersion": ${_j(version)}, "closed": ${_j(closed)}, "knots": ${_j(knots)}}';

String _j(Object? v) => switch (v) {
  null => 'null',
  final String s => '"$s"',
  final List<Object?> l => '[${l.map(_j).join(', ')}]',
  final Map<String, Object?> m =>
    '{${m.entries.map((e) => '"${e.key}": ${_j(e.value)}').join(', ')}}',
  _ => '$v',
};

List<Map<String, Object?>> _decodeCases() {
  Map<String, Object?> k(
    Object? at, {
    Object? tilt,
    Object? hin,
    Object? hout,
  }) => {
    'at': at,
    if (tilt != null) 'tilt': tilt,
    if (hin != null) 'in': hin,
    if (hout != null) 'out': hout,
  };
  final two = [
    k([0, 0, 0]),
    k([10, 0, 0], tilt: 0.5, hin: [-3, 0, 0], hout: [3, 0, 0]),
  ];
  final texts = <String>[
    _valid(two),
    _valid(two, closed: true),
    _valid(two, closed: 'yes'),
    _valid(two, closed: 1),
    _valid(two, version: 1),
    _valid(two, version: 2),
    _valid(two, version: 0),
    _valid(two, version: -1),
    _valid(two, version: 2.0),
    _valid(two, version: 1.5),
    _valid(two, version: null),
    _valid(two, version: '2'),
    _valid(two, version: 9007199254740993),
    _valid([
      k([0, 0, 0]),
    ]),
    _valid(<Object?>[]),
    _valid(null),
    _valid('knots'),
    _valid([
      k([0, 0, 0]),
      5,
    ]),
    _valid([
      k([0, 0, 0]),
      null,
    ]),
    _valid([
      k([0, 0, 0]),
      k(null),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2]),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3, 4]),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, '3']),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, null]),
    ]),
    _valid([
      k([0, 0, 0]),
      k('abc'),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], tilt: 'x'),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], tilt: 0),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], tilt: 1.25),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], tilt: -1e-7),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], tilt: 1e20),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], hin: [1, 2]),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], hout: 'a'),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3], hin: [0, 0, 0], hout: [0, 0, 0]),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1e21, 1e-7, 1.5e300]),
    ]),
    _valid([
      k([0.1, 0.2, 0.30000000000000004]),
      k([1, 2, 3]),
    ]),
    _valid([
      k([-0.0, 0, 0]),
      k([1, 2, 3]),
    ]),
    _valid([
      k([0, 0, 0]),
      k([1, 2, 3]),
      k([5, 5, 5], tilt: 2),
    ], closed: true),
    '{"kind": "other", "formatVersion": 1, "knots": ${_j(two)}}',
    '{"formatVersion": 1, "knots": ${_j(two)}}',
    '{"kind": "orblit.path", "knots": ${_j(two)}}',
    '{"kind": "orblit.path", "formatVersion": 1}',
    '{"kind": "orblit.path", "formatVersion": 1, "closed": true, "knots": ${_j(two)}, "extra": [1, 2]}',
    '[]',
    '"orblit.path"',
    'null',
    '42',
    '',
    '{',
    '{"kind": "orblit.path",',
    '{"kind": "orblit.path", "kind": "other", "knots": ${_j(two)}}',
    '  ${_valid(two)}  \n',
    '${_valid(two)} trailing',
    '﻿${_valid(two)}',
    '{"kind": "orblit.path", "formatVersion": 1, "knots": [{"at": [1e400, 0, 0]}, {"at": [1, 1, 1]}]}',
    '{"kind": "orblit.path", "formatVersion": 1, "knots": [{"at": [-1e400, 0, 0]}, {"at": [1, 1, 1]}]}',
    '{"kind": "orblit.path", "formatVersion": 1, "knots": [{"at": [NaN, 0, 0]}, {"at": [1, 1, 1]}]}',
    '{"kind": "orblit.path", "formatVersion": 1, "knots": [{"at": [0, 0, 0]}, {"at": [1, 1, 1], "tilt": null}]}',
  ];
  return [
    for (final text in texts)
      _case('decode', [text], () {
        final s = Spline.decode(text);
        return {
          'text': s.encode(),
          'length': number(s.length),
          'closed': s.closed,
        };
      }),
  ];
}

// Followers.

List<Map<String, Object?>> _followCases() {
  final cases = <Map<String, Object?>>[];
  final random = math.Random(53);
  final specs = _splineSpecs()
      .where((spec) {
        try {
          _spline(spec);
          return true;
        } on Object {
          return false;
        }
      })
      .take(26);
  const ends = ['stop', 'loop', 'pingPong'];
  for (final spec in specs) {
    final length = _spline(spec).length;
    for (final end in ends) {
      // A bounce folds an overshoot one length at a time, so a tiny spline
      // or an infinite one would never finish.
      if (end == 'pingPong' && !(length >= 0.5 && length.isFinite)) continue;
      final speeds = <double>[4, -2.5, 0, 1e-9, 37.5];
      for (final speed in speeds) {
        final start = random.nextDouble() * _spline(spec).length;
        final dts = <double>[0.1, 0.5, 0, -1, 1, 3.3, 0.016, 100];
        cases.add(_followCase(spec, end, speed, start, dts));
      }
      for (final start in const [-5.0, 1e9, _nan, 0.0]) {
        cases.add(
          _case('followFrom', [spec, end, number(start)], () {
            final rider = PathFollower(
              _spline(spec),
              end: PathEnd.values.byName(end),
              distance: start,
            );
            rider.advance(0.5);
            return [number(rider.distance), rider.isFinished];
          }),
        );
      }
    }
    for (final speed in const [6.0, 2.0, -3.0, 1e-9, 0.0]) {
      final start = random.nextDouble() * _spline(spec).length;
      final spots = [
        for (var k = 0; k < 8; k++)
          _v(
            random.nextDouble() * 24 - 12,
            random.nextDouble() * 6 - 3,
            random.nextDouble() * 24 - 12,
          ),
      ];
      final dts = <double>[0.2, 1, 0, -1, 5, 0.05, 0.3, 2];
      cases.add(_railCase(spec, speed, start, spots, dts));
    }
  }
  return cases;
}

Map<String, Object?> _followCase(
  List<Object?> spec,
  String end,
  double speed,
  double start,
  List<double> dts,
) => _case(
  'follow',
  [spec, end, number(speed), number(start), _numbers(dts)],
  () {
    final rider = PathFollower(
      _spline(spec),
      speed: speed,
      end: PathEnd.values.byName(end),
      distance: start,
    );
    final out = <Object?>[];
    for (final dt in dts) {
      rider.advance(dt);
      final f = rider.frame;
      out.add([
        number(rider.distance),
        rider.isFinished,
        _vector(f.position),
        _vector(f.forward),
      ]);
    }
    return out;
  },
);

Map<String, Object?> _railCase(
  List<Object?> spec,
  double speed,
  double start,
  List<Object> spots,
  List<double> dts,
) => _case(
  'rail',
  [spec, number(speed), number(start), spots, _numbers(dts)],
  () {
    final rail = Rail(_spline(spec), speed: speed, distance: start);
    final out = <Object?>[];
    for (var k = 0; k < spots.length; k++) {
      rail.track(_at(spots[k]), dts[k]);
      out.add([number(rail.distance), _vector(rail.position)]);
    }
    return out;
  },
);

// Followers and rails on a bound: no speed at the two ends, a distance a hair
// off the end, no time at the end of a loop, a bounce that lands on the
// start, and a rail that moves at no speed limit for no time.
List<Map<String, Object?>> _boundaryFollowCases() {
  final line = [
    'line',
    [_v(0.0, 0.0, 0.0), _v(8.0, 0.0, 0.0)],
    false,
    null,
  ];
  final spot = _v(6.0, 0.0, 1.0);
  return [
    _followCase(line, 'stop', 0, 0, [0.5]),
    _followCase(line, 'stop', 0, 8, [0.5]),
    _followCase(line, 'stop', -2.5, 1e-9, [0]),
    _followCase(line, 'stop', 2.5, 8 - 1e-9, [0]),
    _followCase(line, 'loop', 4, 8, [0]),
    _followCase(line, 'pingPong', -2, 6, [3]),
    _railCase(line, 1 / 0, 3, [spot, spot], [0, 1]),
  ];
}

List<Map<String, Object?>> _patrolCases() {
  final cases = <Map<String, Object?>>[];
  final random = math.Random(59);
  final stopSets = <List<List<Object?>>>[
    [
      [_v(0.0, 0.0, 0.0), 0.0],
    ],
    [
      [_v(0.0, 0.0, 0.0), 0.0],
      [_v(10.0, 0.0, 0.0), 2.0],
      [_v(10.0, 0.0, 10.0), 0.0],
    ],
    [
      [_v(0.0, 0.0, 0.0), 1.0],
      [_v(4.0, 0.0, 0.0), 1.0],
      [_v(4.0, 0.0, 4.0), 0.0],
      [_v(0.0, 0.0, 4.0), 3.0],
    ],
    [
      [_v(0.0, 0.0, 0.0), 0.0],
      [_v(1.0, 0.0, 0.0), 0.0],
    ],
    [
      [_v(0.0, 0.0, 0.0), -1.0],
    ],
    [
      [_v(0.0, 0.0, 0.0), number(_nan)],
    ],
    [
      [_v(0.0, 0.0, 0.0), number(_inf)],
    ],
    <List<Object?>>[],
  ];
  for (final stops in stopSets) {
    for (final end in const ['stop', 'loop', 'pingPong']) {
      for (final reached in const [0.5, 0.0, 2.0, 1e-9]) {
        final script = <List<Object?>>[];
        for (var n = 0; n < 40; n++) {
          final stop = stops.isEmpty
              ? _v(0.0, 0.0, 0.0)
              : stops[random.nextInt(stops.length)][0] as List<Object?>;
          final near = random.nextInt(3) != 0;
          final at = near
              ? _v(
                  _double(stop[0]) + (random.nextDouble() - 0.5) * reached,
                  0.0,
                  _double(stop[2]),
                )
              : _v(
                  random.nextDouble() * 12 - 1,
                  0.0,
                  random.nextDouble() * 12 - 1,
                );
          script.add([
            at,
            [0.1, 0.5, 1.0, 2.5, 0.0, -0.5][random.nextInt(6)],
          ]);
        }
        cases.add(
          _case('patrol', [stops, end, number(reached), script], () {
            final patrol = Patrol(
              [
                for (final s in stops)
                  PatrolStop(_at(s[0]), wait: _double(s[1])),
              ],
              end: PathEnd.values.byName(end),
              reached: reached,
            );
            final out = <Object?>[];
            for (final s in script) {
              patrol.update(_at(s[0]), _double(s[1]));
              out.add([
                patrol.target == null ? null : _vector(patrol.target!),
                patrol.isWaiting,
                patrol.isFinished,
              ]);
            }
            return out;
          }),
        );
      }
    }
  }
  return cases;
}

// Baking.

// A terrain spec is {keys, slope, wave, tilt, mark} and, when the texels are
// not a metre apart, spacing. Heights use arithmetic only, so the last digit
// cannot part from .NET's.
Terrain _terrain(Map<String, Object?> spec) {
  final terrain = Terrain(
    regionSize: 16,
    spacing: _double(spec['spacing'] ?? 1.0),
  );
  final slope = _double(spec['slope']);
  final wave = _double(spec['wave']);
  final tilt = _double(spec['tilt']);
  for (final key in spec['keys'] as List<Object?>) {
    final pair = key as List<Object?>;
    final region = terrain.fillHeights(
      RegionKey(pair[0] as int, pair[1] as int),
      (x, z) => slope * x + wave * ((x / 3).floorToDouble() % 2) + tilt * z,
    );
    for (var j = 0; j < 16; j++) {
      for (var i = 0; i < 16; i++) {
        region.cover[j * 16 + i] = _cover(spec['mark'] as String, i, j).word;
      }
    }
  }
  return terrain;
}

Cover _cover(String mark, int i, int j) => switch (mark) {
  'all' => Cover.of(navigation: true),
  'half' => Cover.of(navigation: i < 8),
  'none' => Cover.plain,
  'holes' => Cover.of(navigation: true, hole: (i + j) % 5 == 0),
  'plainHoles' => Cover.of(hole: (i + j) % 5 == 0),
  _ => Cover.of(navigation: (i * 3 + j) % 4 != 0, base: (i + j) % 3),
};

bool Function(Cover) _walkable(String mode) => switch (mode) {
  'any' => (cover) => true,
  'notHole' => (cover) => !cover.hole,
  _ => marked,
};

Map<String, Object?> _terrainSpec(
  List<List<int>> keys, {
  double slope = 0,
  double wave = 0,
  double tilt = 0,
  String mark = 'all',
  double spacing = 1,
}) => {
  'keys': keys,
  'slope': number(slope),
  'wave': number(wave),
  'tilt': number(tilt),
  'mark': mark,
  if (spacing != 1) 'spacing': number(spacing),
};

List<Map<String, Object?>> _bakeTerrainCases() {
  final cases = <Map<String, Object?>>[];
  final terrains = [
    _terrainSpec([
      [0, 0],
    ]),
    _terrainSpec([
      [0, 0],
    ], slope: 0.5),
    _terrainSpec([
      [0, 0],
    ], slope: 1.0),
    _terrainSpec([
      [0, 0],
    ], slope: 0.9999),
    _terrainSpec([
      [0, 0],
    ], slope: 1.0001),
    _terrainSpec([
      [0, 0],
    ], slope: 2.0),
    _terrainSpec(
      [
        [0, 0],
      ],
      slope: 0.1,
      tilt: 0.2,
    ),
    _terrainSpec([
      [0, 0],
    ], wave: 0.8),
    _terrainSpec(
      [
        [0, 0],
      ],
      slope: -0.3,
      wave: -0.5,
      tilt: 1.5,
    ),
    _terrainSpec([
      [0, 0],
    ], mark: 'half'),
    _terrainSpec([
      [0, 0],
    ], mark: 'none'),
    _terrainSpec([
      [0, 0],
    ], mark: 'holes'),
    _terrainSpec([
      [0, 0],
    ], mark: 'plainHoles'),
    _terrainSpec([
      [0, 0],
    ], mark: 'mixed'),
    _terrainSpec([
      [2, -1],
    ]),
    _terrainSpec([
      [0, 0],
      [2, 0],
    ], slope: 0.25),
    _terrainSpec(
      [
        [-1, -1],
        [0, 0],
        [1, 1],
      ],
      mark: 'mixed',
      slope: 0.4,
    ),
    _terrainSpec(<List<int>>[]),
    _terrainSpec(
      [
        [0, 0],
        [1, 0],
      ],
      slope: 0.25,
      spacing: 2,
    ),
    _terrainSpec(
      [
        [-1, 0],
      ],
      wave: 0.4,
      spacing: 0.5,
    ),
  ];
  const cellSizes = [1.0, 2.0, 3.0, 0.5, 0.25, 5.0, 16.0, 100.0];
  const slopes = [math.pi / 4, 0.0, 0.1, math.pi / 2, 1.0, math.pi];
  for (var t = 0; t < terrains.length; t++) {
    final terrain = terrains[t];
    for (final mode in const ['marked', 'any', 'notHole']) {
      for (final cell in cellSizes.take(t < 2 ? cellSizes.length : 3)) {
        for (final slope in slopes.take(t < 6 ? slopes.length : 2)) {
          cases.add(
            _case(
              'bakeTerrain',
              [terrain, number(cell), number(slope), mode, null],
              () {
                final grid = bakeTerrain(
                  _terrain(terrain),
                  cell: cell,
                  maxSlope: slope,
                  walkable: _walkable(mode),
                );
                return _dump(grid, rows: true);
              },
            ),
          );
        }
      }
    }
    final areas = <List<double?>>[
      [4, 4, 12, 12],
      [-5.5, -5.5, 20.5, 20.5],
      [8, null, null, null],
      [null, 2, null, 10],
      [10, 10, 4, 4],
      [0, 0, 0, 0],
      [3, 3, 3.0001, 3.0001],
      [_nan, 0, 4, 4],
      [0, 0, _inf, 4],
    ];
    for (final area in areas) {
      cases.add(
        _case(
          'bakeTerrain',
          [
            terrain,
            1.0,
            math.pi / 4,
            'marked',
            [for (final a in area) a == null ? null : number(a)],
          ],
          () {
            final grid = bakeTerrain(
              _terrain(terrain),
              minX: area[0],
              minZ: area[1],
              maxX: area[2],
              maxZ: area[3],
            );
            return _dump(grid, rows: true);
          },
        ),
      );
    }
  }
  for (final cell in const [0.0, -1.0, _nan, _inf]) {
    cases.add(
      _case(
        'bakeTerrain',
        [terrains[0], number(cell), math.pi / 4, 'marked', null],
        () {
          return _dump(
            bakeTerrain(_terrain(terrains[0]), cell: cell),
            rows: true,
          );
        },
      ),
    );
  }
  return cases;
}

// A mesh spec is [positions, indices, agentHeight, stepHeight, maxSlope].
List<Object?> _meshSpec(
  List<double> positions,
  List<int> indices, {
  double agentHeight = 1.8,
  double stepHeight = 0.3,
  double maxSlope = math.pi / 4,
}) => [
  _numbers(positions),
  indices,
  number(agentHeight),
  number(stepHeight),
  number(maxSlope),
];

MeshWalk _mesh(List<Object?> spec) => (
  positions: _list(spec[0]),
  indices: (spec[1] as List<Object?>).cast<int>(),
  agentHeight: _double(spec[2]),
  stepHeight: _double(spec[3]),
  maxSlope: _double(spec[4]),
);

List<double> _floorAt(double size, double y, [double x = 0, double z = 0]) => [
  x,
  y,
  z,
  x,
  y,
  z + size,
  x + size,
  y,
  z + size,
  x + size,
  y,
  z,
];

List<double> _wallAt(double z, double width, double height) => [
  0,
  0,
  z,
  width,
  0,
  z,
  width,
  height,
  z,
  0,
  height,
  z,
];

List<List<Object?>> _meshSpecs() {
  final random = math.Random(67);
  const quad = [0, 1, 3, 1, 2, 3];
  List<int> second(List<int> i) => [for (final n in i) n + 4];
  final specs = <List<Object?>>[
    _meshSpec(_floorAt(8, 0), quad),
    _meshSpec(_floorAt(8, 1.25), quad),
    _meshSpec(
      [..._floorAt(8, 0), ..._wallAt(4, 8, 2.5)],
      [
        ...quad,
        ...second([0, 1, 2, 0, 2, 3]),
      ],
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._wallAt(4, 8, 0.25)],
      [
        ...quad,
        ...second([0, 1, 2, 0, 2, 3]),
      ],
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._wallAt(4, 8, 0.3)],
      [
        ...quad,
        ...second([0, 1, 2, 0, 2, 3]),
      ],
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._wallAt(4, 8, 1.8)],
      [
        ...quad,
        ...second([0, 1, 2, 0, 2, 3]),
      ],
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._wallAt(4, 8, 2.0)],
      [
        ...quad,
        ...second([0, 1, 2, 0, 2, 3]),
      ],
      agentHeight: 2.0,
      stepHeight: 0.5,
    ),
    // A ceiling over the floor, facing down.
    _meshSpec(
      [..._floorAt(8, 0), ..._floorAt(8, 1.2)],
      [
        ...quad,
        ...second([0, 3, 1, 1, 3, 2]),
      ],
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._floorAt(8, 3.0)],
      [
        ...quad,
        ...second([0, 3, 1, 1, 3, 2]),
      ],
    ),
    // A bridge over a road: two floors, one above the other.
    _meshSpec(
      [..._floorAt(8, 0), ..._floorAt(4, 2.5, 2, 2)],
      [...quad, ...second(quad)],
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._floorAt(4, 1.0, 2, 2)],
      [...quad, ...second(quad)],
    ),
    // A ramp.
    _meshSpec([0, 0, 0, 0, 0, 8, 8, 4, 8, 8, 4, 0], quad),
    _meshSpec([0, 0, 0, 0, 0, 8, 8, 8, 8, 8, 8, 0], quad),
    _meshSpec(
      [0, 0, 0, 0, 0, 8, 8, 8, 8, 8, 8, 0],
      quad,
      maxSlope: math.pi / 3,
    ),
    _meshSpec([0, 0, 0, 0, 0, 8, 8, 6.4, 8, 8, 6.4, 0], quad),
    // Degenerate and empty.
    _meshSpec([], []),
    _meshSpec([0, 0, 0, 1, 0, 0], []),
    _meshSpec([0, 0, 0, 1, 0, 0, 2, 0, 0], [0, 1, 2]),
    _meshSpec(_floorAt(8, 0), [0, 1]),
    _meshSpec(_floorAt(8, 0), [0, 1, 2, 3]),
    _meshSpec(_floorAt(8, 0), [0, 1, 4]),
    _meshSpec(_floorAt(8, 0), [0, 1, -1]),
    _meshSpec(_floorAt(8, 0).sublist(0, 10), [0, 1, 2]),
    _meshSpec(_floorAt(8, 0), quad, agentHeight: 0),
    _meshSpec(_floorAt(8, 0), quad, agentHeight: _nan),
    _meshSpec(_floorAt(8, 0), quad, stepHeight: -1),
    _meshSpec(_floorAt(8, 0), quad, maxSlope: 0),
    _meshSpec(_floorAt(8, 0), quad, maxSlope: math.pi),
    _meshSpec([0, 0, 0, 0, 0, 8, _nan, 0, 8], [0, 1, 2]),
    _meshSpec([0, 0, 0, 0, 0, 8, _inf, 0, 8], [0, 1, 2]),
  ];
  // A few random soups. The vertices stay in a box so the work stays small.
  for (var n = 0; n < 24; n++) {
    final count = 3 + random.nextInt(9);
    final positions = [
      for (var i = 0; i < count; i++) ...[
        random.nextDouble() * 10,
        random.nextDouble() * 3,
        random.nextDouble() * 10,
      ],
    ];
    final indices = [
      for (var i = 0; i < 3 * (1 + random.nextInt(6)); i++)
        random.nextInt(count),
    ];
    specs.add(
      _meshSpec(positions, indices, maxSlope: 0.4 + random.nextDouble()),
    );
  }
  return specs;
}

// A mesh baked at each cell size, over each area, and carved into the first
// three grids ten cells wide.
List<Map<String, Object?>> _meshBakeCases(
  List<Object?> spec,
  List<double> cells,
  List<List<double?>> areas, {
  List<double> areaCells = const [0.5],
}) {
  final cases = <Map<String, Object?>>[];
  for (final cell in cells) {
    cases.add(
      _case('bakeMesh', [
        spec,
        number(cell),
        null,
      ], () => _dump(bakeMesh(_mesh(spec), cell: cell), rows: true)),
    );
  }
  for (final cell in areaCells) {
    for (final area in areas) {
      cases.add(
        _case(
          'bakeMesh',
          [
            spec,
            number(cell),
            [for (final a in area) a == null ? null : number(a)],
          ],
          () {
            final grid = bakeMesh(
              _mesh(spec),
              cell: cell,
              minX: area[0],
              minZ: area[1],
              maxX: area[2],
              maxZ: area[3],
            );
            return _dump(grid, rows: true);
          },
        ),
      );
    }
  }
  // Carving a grid that is already there.
  for (final spec2 in _gridSpecs().where((s) => s[0] == 10).take(3)) {
    cases.add(
      _case('carve', [spec2, spec], () {
        final grid = _grid(spec2).grid;
        carve(grid, _mesh(spec));
        return _dump(grid, rows: true);
      }),
    );
  }
  return cases;
}

List<Map<String, Object?>> _bakeMeshCases() {
  final cases = <Map<String, Object?>>[];
  final specs = _meshSpecs();
  for (var n = 0; n < specs.length; n++) {
    const areas = <List<double?>>[
      [0, 0, 4, 4],
      [-2, -2, 12, 12],
      [3, null, null, 6],
      [4, 4, 4, 4],
      [6, 6, 2, 2],
    ];
    cases.addAll(
      _meshBakeCases(
        specs[n],
        n < 12 ? const [0.5, 1.0, 2.0, 0.25] : const [0.5, 1.0],
        n < 14 ? areas : areas.take(1).toList(),
      ),
    );
  }
  for (final cell in const [0.0, -1.0, _nan, _inf]) {
    cases.add(
      _case('bakeMesh', [
        specs[0],
        number(cell),
        null,
      ], () => _dump(bakeMesh(_mesh(specs[0]), cell: cell), rows: true)),
    );
  }
  return cases;
}

// Meshes that sit on a bound: a ceiling at the height of the agent, a flat
// floor under a slope limit of nothing, a sliver whose cross product is
// exactly as long as the smallest the baker takes as a face, the middle of a
// cell on an edge, and a mesh of one vertex or a vertex short of a triple.
List<Map<String, Object?>> _boundaryMeshCases() {
  const quad = [0, 1, 3, 1, 2, 3];
  List<int> second(List<int> i) => [for (final n in i) n + 4];
  final ceiling = second(const [0, 3, 1, 1, 3, 2]);
  final edge = [0.5, 0.0, 0.5, 3.5, 0.0, 0.5, 0.5, 0.0, 3.5];
  final specs = [
    _meshSpec([..._floorAt(8, 0), ..._floorAt(8, 1.8)], [...quad, ...ceiling]),
    _meshSpec(
      [..._floorAt(8, 0), ..._floorAt(8, 2.0)],
      [...quad, ...ceiling],
      agentHeight: 2.0,
      stepHeight: 0.5,
    ),
    _meshSpec(
      [..._floorAt(8, 0), ..._floorAt(8, 1.0)],
      [...quad, ...second(quad)],
      maxSlope: 0,
    ),
    _meshSpec(
      [..._floorAt(8, 0), 0, 1, 0, 1e-9, 1, 0, 0, 1, 1],
      [...quad, 4, 5, 6],
    ),
    _meshSpec(edge, [0, 2, 1]),
    _meshSpec(edge, [0, 1, 2]),
    _meshSpec([1.5, 0, 2.5], []),
    _meshSpec(_floorAt(8, 0).sublist(0, 11), [0, 1, 2]),
  ];
  final cases = <Map<String, Object?>>[];
  for (final spec in specs) {
    cases.addAll(
      _meshBakeCases(
        spec,
        const [0.5, 1.0],
        const [
          [0, 0, 4, 4],
        ],
        areaCells: const [0.5, 1.0],
      ),
    );
  }
  // A triangle whose normal is exactly as long as the least the bake accepts.
  cases.addAll(
    _meshBakeCases(
      _meshSpec(const [0, 0, 0, 1e-9, 0, 0, 0, 0, 1], const [0, 2, 1]),
      const [1.0],
      const [
        [-0.5, -0.5, 1.5, 1.5],
      ],
      areaCells: const [1.0],
    ),
  );
  return cases;
}

// Heights that do not add exactly, so the order they are summed in shows.
List<Map<String, Object?>> _slopedMeshCases() {
  final spec = <Object?>[
    8,
    8,
    1.0,
    0.0,
    0.0,
    [
      for (var j = 0; j < 8; j++)
        for (var i = 0; i < 8; i++) ['height', i, j, 0.1 * i + 0.07 * j],
    ],
  ];
  final pairs = [
    [_v(0.5, 0.0, 0.5), _v(7.5, 0.0, 7.5)],
    [_v(0.5, 0.0, 7.5), _v(7.5, 0.0, 0.5)],
    [_v(3.5, 0.0, 3.5), _v(6.5, 0.0, 1.5)],
  ];
  return [
    for (final pair in pairs)
      for (final radius in const [0.0, 0.6])
        _case('mesh', [spec, radius, pair[0], pair[1]], () {
          final mesh = NavMesh.fromGrid(_grid(spec).grid, radius: radius);
          return {
            'count': mesh.count,
            'path': _pathOut(mesh.findPath(_at(pair[0]), _at(pair[1]))),
          };
        }),
  ];
}

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_path',
    name: 'path',
    cases: [
      ..._gridCases(),
      ..._gridEdgeCases(),
      ..._lineCases(),
      ..._walkerCases(),
      ..._linkCases(),
      ..._linkTieCases(),
      ..._splineCases(),
      ..._decodeCases(),
      ..._followCases(),
      ..._boundaryFollowCases(),
      ..._patrolCases(),
      ..._bakeTerrainCases(),
      ..._bakeMeshCases(),
      ..._boundaryMeshCases(),
      ..._slopedMeshCases(),
    ],
  );
}
