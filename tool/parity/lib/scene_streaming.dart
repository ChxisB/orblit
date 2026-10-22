import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_cells/orblit_cells.dart';
import 'package:orblit_scene/orblit_scene.dart';

import 'fixture.dart';

/// The cases for streaming: a world of cell files and a list of steps in, and
/// what each step did and what each cell cost out.
///
/// The cells are ten across and the clock is a number the cases set, so every
/// run reads the same.
Future<List<Map<String, Object?>>> streamingCases() async => [
  for (final scenario in _scenarios())
    {
      'op': 'streaming',
      'in': scenario,
      'expect': await _result(scenario),
    },
];

String _tree(String name) => SceneDocument(
  entities: [
    SceneEntity(id: 'root', name: name),
    SceneEntity(id: 'leaf', name: name, parent: 'root'),
  ],
).encode();

String _lone(String name) =>
    SceneDocument(entities: [SceneEntity(id: 'only', name: name)]).encode();

// The key of a file is its cell as "x,z". A value that is null is a cell the
// world has nothing in, and "throws" is a fetch that fails.
Map<String, Object?> _world(
  List<(int, int)> cells, {
  Map<String, Object?> extra = const {},
}) => {
  for (final (x, z) in cells) '$x,$z': _tree('c$x$z'),
  ...extra,
};

Map<String, Object?> _step(
  double x,
  double z, {
  double fx = 0,
  double fz = 0,
  List<List<int>> pinned = const [],
  int advance = 0,
}) => {
  'x': x,
  'z': z,
  'fx': fx,
  'fz': fz,
  'pinned': pinned,
  'advance': advance,
};

List<Map<String, Object?>> _walk(double from, double to, double by) => [
  for (var x = from; x <= to; x += by) _step(x, 5, fx: 1),
];

List<Map<String, Object?>> _scenarios() => [
  {
    'radius': 1,
    'inFlight': 2,
    'budget': 4,
    'cost': 0,
    'files': _world([
      for (var x = -1; x <= 4; x++)
        for (var z = -1; z <= 1; z++) (x, z),
    ]),
    'steps': [..._walk(5, 45, 10), ...List.filled(4, _step(45, 5))],
  },
  {
    'radius': 1,
    'inFlight': 9,
    'budget': 4,
    'cost': 3,
    'files': _world([
      for (var x = -1; x <= 3; x++)
        for (var z = -1; z <= 1; z++) (x, z),
    ]),
    'steps': [
      ...List.filled(6, _step(5, 5)),
      ..._walk(15, 25, 10),
      ...List.filled(6, _step(25, 5)),
    ],
  },
  {
    'radius': 2,
    'inFlight': 6,
    'budget': 4,
    'cost': 2,
    'files': _world([
      for (var x = -2; x <= 5; x++)
        for (var z = -2; z <= 2; z++) (x, z),
    ]),
    'steps': [
      ...List.filled(5, _step(5, 5, fx: 1)),
      ...List.filled(5, _step(5, 5, fz: 1)),
      ...List.filled(5, _step(5, 5, fx: -1)),
    ],
  },
  {
    // Room for every cell at once, so a fetch that fails never frees a place
    // for another in the same step.
    'radius': 1,
    'inFlight': 9,
    'budget': 4,
    'cost': 0,
    'files': _world(
      [(0, 0), (1, 0)],
      extra: {
        '-1,0': 'throws',
        '0,1': 'this is not a scene',
        '1,1': null,
        '0,-1': _lone('alone'),
        '1,-1': '{"formatVersion": 99}',
      },
    ),
    'steps': [
      ...List.filled(4, _step(5, 5)),
      _step(5, 5, advance: 5000),
      _step(5, 5),
      _step(5, 5),
    ],
  },
  {
    'radius': 0,
    'inFlight': 2,
    'budget': 4,
    'cost': 0,
    'files': _world([(0, 0), (3, 3), (-3, 2), (7, 0)]),
    'steps': [
      ...List.filled(3, _step(5, 5, pinned: [[3, 3], [-3, 2]])),
      ...List.filled(3, _step(5, 5, pinned: [[3, 3]])),
      ...List.filled(3, _step(35, 35, pinned: [[7, 0]])),
      ...List.filled(3, _step(5, 5)),
    ],
  },
  {
    'radius': 1,
    'inFlight': 2,
    'budget': 1,
    'cost': 2,
    'files': _world([
      for (var x = -2; x <= 2; x++)
        for (var z = -1; z <= 1; z++) (x, z),
    ]),
    'steps': [
      ...List.filled(6, _step(5, 5)),
      ...List.filled(6, _step(-15, 5)),
      ...List.filled(6, _step(5, 5)),
    ],
  },
  {
    'radius': 1,
    'inFlight': 2,
    'budget': 4,
    'cost': 0,
    'files': _world([(-1, -1), (0, -1), (-1, 0), (0, 0)]),
    'steps': [
      ...List.filled(6, _step(-5, -5)),
      _step(-5, -5, advance: 1),
    ],
  },
];

Future<Map<String, Object?>> _result(Map<String, Object?> scenario) async {
  var now = Duration.zero;
  var scene = SceneDocument();
  final asked = <String>[];
  final loads = <Map<String, Object?>>[];
  final files = scenario['files']! as Map<String, Object?>;
  final cost = Duration(milliseconds: scenario['cost']! as int);
  final grid = Grid(10);
  final streamer = CellStreamer(
    window: CellWindow(grid, loadRadius: scenario['radius']! as int),
    inFlight: scenario['inFlight']! as int,
    budget: Duration(milliseconds: scenario['budget']! as int),
    retryAfter: const Duration(seconds: 5),
    clock: () => now,
    onLoad: (load) => loads.add({
      'cell': '${load.cell.x},${load.cell.z}',
      'fetch': load.fetch.inMicroseconds,
      'apply': load.apply.inMicroseconds,
      'bytes': load.bytes,
      'entities': load.entities,
      'problems': load.problems.length,
      'failed': load.error != null,
    }),
    fetch: (cell) async {
      asked.add('${cell.x},${cell.z}');
      final found = files['${cell.x},${cell.z}'];
      if (found == 'throws') throw Exception('the network');
      return found == null ? null : Uint8List.fromList(utf8.encode(found as String));
    },
    apply: (diff) {
      now += cost;
      scene = diff.applyTo(scene);
    },
  );

  final steps = <Map<String, Object?>>[];
  for (final step in scenario['steps']! as List<Map<String, Object?>>) {
    now += Duration(milliseconds: step['advance']! as int);
    streamer.step(
      step['x']! as double,
      step['z']! as double,
      facingX: step['fx']! as double,
      facingZ: step['fz']! as double,
      pinned: {
        for (final p in step['pinned']! as List<List<int>>) (x: p[0], z: p[1]),
      },
    );
    await Future<void>.delayed(Duration.zero);
    final loaded = [
      for (final cell in streamer.loaded) '${cell.x},${cell.z}',
    ]..sort();
    steps.add({
      'loaded': streamer.lastStep.loaded,
      'dropped': streamer.lastStep.dropped,
      'spent': streamer.lastStep.spent.inMicroseconds,
      'pending': streamer.pending,
      'cells': loaded,
      'ids': [for (final e in scene.entities) e.id],
    });
  }
  return {'steps': steps, 'asked': asked, 'loads': loads};
}
