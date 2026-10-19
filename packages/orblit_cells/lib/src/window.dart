import 'dart:math' as math;

import 'grid.dart';

/// What to bring in and what to let go of, as of one look at the world.
final class CellPlan {
  const CellPlan({this.load = const [], this.drop = const []});

  /// Cells wanted and not yet loaded, the one to load first at the head.
  final List<Cell> load;

  /// Loaded cells nobody needs any more, the farthest at the head.
  final List<Cell> drop;
}

/// The cells worth holding around a point that moves.
///
/// Cells within [loadRadius] steps of the point are wanted. Cells stay until
/// they are more than [dropRadius] steps away. The gap between the two is the
/// margin that keeps a point walking up and down a border from loading and
/// dropping the same cell every few frames.
final class CellWindow {
  CellWindow(
    this.grid, {
    required this.loadRadius,
    int? dropRadius,
    this.behindWeight = 1,
  }) : dropRadius = dropRadius ?? loadRadius + 1 {
    if (loadRadius < 0) {
      throw ArgumentError.value(loadRadius, 'loadRadius', 'negative');
    }
    if (this.dropRadius < loadRadius) {
      throw ArgumentError.value(
        dropRadius,
        'dropRadius',
        'below loadRadius, so a cell would drop the frame it loads',
      );
    }
    if (!behindWeight.isFinite || behindWeight < 0) {
      throw ArgumentError.value(behindWeight, 'behindWeight', 'negative');
    }
  }

  final Grid grid;
  final int loadRadius;
  final int dropRadius;

  /// How much farther a cell behind the point counts than one in front. A cell
  /// straight behind ranks as if it were this much farther again than its real
  /// distance. Zero ranks by distance alone.
  final double behindWeight;

  /// Whether [cell] is close enough to [focus] to stay loaded.
  bool keeps(Cell cell, Cell focus) => grid.apart(cell, focus) <= dropRadius;

  /// What to load and drop for a point at ([x], [z]) facing ([facingX],
  /// [facingZ]), given the cells already [loaded].
  ///
  /// A [pinned] cell is wanted wherever it is and is never dropped. Something
  /// that must not lose its ground, such as a character in contact with it,
  /// pins the cell it stands on.
  ///
  /// A point that is not a finite number plans nothing, since dropping the
  /// world because a position went bad is the worse mistake.
  CellPlan plan(
    double x,
    double z, {
    double facingX = 0,
    double facingZ = 0,
    Set<Cell> loaded = const {},
    Set<Cell> pinned = const {},
  }) {
    final focus = grid.cellOf(x, z);
    if (focus == null) return const CellPlan();

    final ranking = _ranking(x, z, facingX, facingZ);
    // The cell the point stands in comes first, whichever way it faces. A
    // neighbour ahead can otherwise outrank it from near a border.
    double score(Cell cell) => cell == focus ? -1 : ranking(cell);
    final wanted = {...grid.around(focus, loadRadius), ...pinned};
    final load = [
      for (final cell in wanted)
        if (!loaded.contains(cell)) cell,
    ]..sort((a, b) => _byScore(score(a), score(b), a, b));
    final drop = [
      for (final cell in loaded)
        if (!keeps(cell, focus) && !pinned.contains(cell)) cell,
    ]..sort((a, b) => _byScore(score(b), score(a), a, b));
    return CellPlan(load: load, drop: drop);
  }

  /// How far a cell is from the point, counted longer for a cell behind it.
  double Function(Cell) _ranking(
    double x,
    double z,
    double facingX,
    double facingZ,
  ) {
    final length = math.sqrt(facingX * facingX + facingZ * facingZ);
    final facing = length > 0 && length.isFinite;
    return (cell) {
      final middle = grid.centerOf(cell);
      final dx = middle.x - x;
      final dz = middle.z - z;
      final distance = math.sqrt(dx * dx + dz * dz);
      if (!facing || distance == 0) return distance;
      final cosine = (dx * facingX + dz * facingZ) / (distance * length);
      return distance * (1 + behindWeight * (1 - cosine) / 2);
    };
  }

  // The coordinates break a tie, so a plan is the same plan every run.
  int _byScore(double a, double b, Cell first, Cell second) {
    final byScore = a.compareTo(b);
    if (byScore != 0) return byScore;
    return first.x != second.x
        ? first.x.compareTo(second.x)
        : first.z.compareTo(second.z);
  }
}
