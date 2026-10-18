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

  /// The cell holding ([x], [z]), or null when either is not a finite number.
  Cell? cellOf(double x, double z) {
    if (!x.isFinite || !z.isFinite) return null;
    return (x: (x / cellSize).floor(), z: (z / cellSize).floor());
  }

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
