import 'dart:math' as math;

/// One square of a [Grid], by how many cells it is from the origin.
typedef Cell = ({int x, int z});

/// Square cells on the ground plane. The cell size lives here and nowhere
/// else, so everything that cuts the world up agrees on where the cuts are.
final class Grid {
  Grid(this.cellSize) {
    if (!cellSize.isFinite || cellSize <= 0) {
      throw ArgumentError.value(cellSize, 'cellSize', 'not above zero');
    }
  }

  final double cellSize;

  /// The cell holding ([x], [z]), or null when either is not a finite number
  /// or lies more than [reach] cells from the origin.
  ///
  /// A position that far is a physics blow-up and not a place. Cell numbers
  /// that large overflow when a window is drawn round them.
  Cell? cellOf(double x, double z) {
    final cx = x / cellSize;
    final cz = z / cellSize;
    if (!cx.isFinite || !cz.isFinite) return null;
    if (cx.abs() > reach || cz.abs() > reach) return null;
    return (x: cx.floor(), z: cz.floor());
  }

  /// The farthest a cell number may be from zero.
  static const double reach = 1e9;

  /// The middle of [cell].
  ({double x, double z}) centerOf(Cell cell) =>
      (x: (cell.x + 0.5) * cellSize, z: (cell.z + 0.5) * cellSize);

  /// How many cells apart [a] and [b] are, counting a diagonal step as one.
  int apart(Cell a, Cell b) => math.max((a.x - b.x).abs(), (a.z - b.z).abs());

  /// [from] and every cell within [radius] steps of it, as a square.
  Iterable<Cell> around(Cell from, int radius) sync* {
    if (radius < 0) throw ArgumentError.value(radius, 'radius', 'negative');
    for (var x = from.x - radius; x <= from.x + radius; x++) {
      for (var z = from.z - radius; z <= from.z + radius; z++) {
        yield (x: x, z: z);
      }
    }
  }
}
