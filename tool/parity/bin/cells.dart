import 'package:orblit_cells/orblit_cells.dart';
import 'package:orblit_parity_tool/fixture.dart';

List<int> _pair(Cell cell) => [cell.x, cell.z];

/// Sizes that divide evenly and that do not, one so small the division
/// overflows and one so large that the reach check is what refuses.
const _sizes = [10.0, 0.3, 1e-320, 1e300];

/// Borders, a place that is not one, and a walk across both signs.
final _points = <List<double>>[
  [0, 0],
  [9.99, 10],
  [-0.01, -10],
  [1e300, 0],
  [0, -1e12],
  for (var i = 0; i < 40; i++) [i * 3.7 - 60, i * 2.3 - 40],
];

const _windows = <({int loadRadius, int? dropRadius, double behindWeight})>[
  (loadRadius: 2, dropRadius: null, behindWeight: 1),
  (loadRadius: 1, dropRadius: null, behindWeight: 0),
  (loadRadius: 3, dropRadius: 4, behindWeight: 2.5),
  (loadRadius: 0, dropRadius: 0, behindWeight: 1),
];

const _facings = <List<double>>[
  [0, 0],
  [1, 0],
  [-0.3, 2],
];

Map<String, Object?> _planCase(
  double size,
  ({int loadRadius, int? dropRadius, double behindWeight}) shape,
  List<double> at,
  List<double> facing,
  Set<Cell> loaded,
  Set<Cell> pinned,
) {
  final window = CellWindow(
    Grid(size),
    loadRadius: shape.loadRadius,
    dropRadius: shape.dropRadius,
    behindWeight: shape.behindWeight,
  );
  final plan = window.plan(
    at[0],
    at[1],
    facingX: facing[0],
    facingZ: facing[1],
    loaded: loaded,
    pinned: pinned,
  );
  return {
    'op': 'plan',
    'size': size,
    'loadRadius': shape.loadRadius,
    'dropRadius': shape.dropRadius,
    'behindWeight': shape.behindWeight,
    'at': at,
    'facing': facing,
    'loaded': loaded.map(_pair).toList(),
    'pinned': pinned.map(_pair).toList(),
    'expect': {
      'load': plan.load.map(_pair).toList(),
      'drop': plan.drop.map(_pair).toList(),
    },
  };
}

/// Every other cell of the window's own square is loaded, with one cell well
/// outside it. A third of the cases pin the far cell and one loaded cell.
List<Map<String, Object?>> _plans() {
  const size = 10.0;
  final grid = Grid(size);
  final cases = <Map<String, Object?>>[];
  var index = 0;
  for (final shape in _windows) {
    for (var i = 0; i < 10; i++) {
      final at = [i * 7.3 - 30, i * 4.1 - 20];
      final focus = grid.cellOf(at[0], at[1])!;
      final square = grid.around(focus, shape.loadRadius + 1).toList();
      for (final facing in _facings) {
        final far = (x: focus.x + 8, z: focus.z - 8);
        final loaded = {
          for (var k = 0; k < square.length; k++)
            if (k.isOdd) square[k],
          far,
        };
        final pinned = index % 3 == 0 ? {far, square.first} : <Cell>{};
        cases.add(_planCase(size, shape, at, facing, loaded, pinned));
        index++;
      }
    }
  }
  // A cell so far out that its middle overflows to infinity, which makes the
  // rank not a number when the point faces somewhere.
  cases.add(
    _planCase(
      1e300,
      (loadRadius: 1, dropRadius: null, behindWeight: 1),
      [1.7976931348623157e308, 0],
      [1, 0],
      {},
      {},
    ),
  );
  return cases;
}

/// Usage: `dart run bin/cells.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_cells',
    name: 'cells',
    cases: [
      for (final size in _sizes)
        for (final p in _points)
          {
            'op': 'cellOf',
            'size': size,
            'at': p,
            'expect': switch (Grid(size).cellOf(p[0], p[1])) {
              final cell? => _pair(cell),
              null => null,
            },
          },
      ..._plans(),
    ],
  );
}
