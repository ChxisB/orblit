import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'heap.dart';

/// A cell of a [NavGrid].
typedef NavCell = ({int i, int j});

/// Ground cut into square cells, each walkable or not, for finding a way
/// across it.
///
/// Cell ([i], [j]) covers x from `originX + i * cell` and z from
/// `originZ + j * cell`, and has one height. Everything is on the ground
/// plane: a cell is open or closed, and there is one floor to a cell.
final class NavGrid {
  NavGrid({
    required this.width,
    required this.depth,
    required this.cell,
    this.originX = 0,
    this.originZ = 0,
  }) : _open = Uint8List(width * depth),
       _shut = Uint16List(width * depth),
       _heights = Float32List(width * depth),
       _weights = Float32List(width * depth) {
    if (width <= 0 || depth <= 0) {
      throw ArgumentError('A grid is at least one cell across.');
    }
    if (!(cell > 0) || !cell.isFinite) {
      throw ArgumentError.value(cell, 'cell', 'Must be above zero.');
    }
    _open.fillRange(0, _open.length, 1);
    _weights.fillRange(0, _weights.length, 1);
  }

  final int width;
  final int depth;

  /// Metres a cell is across.
  final double cell;

  /// The world x and z of the corner of cell (0, 0).
  final double originX;
  final double originZ;

  final Uint8List _open;

  /// How many standing obstacles shut each cell. Apart from [_open] so that
  /// lifting one never opens a cell a wall closes or another obstacle shuts.
  final Uint16List _shut;
  final Float32List _heights;
  final Float32List _weights;

  static const double _diagonal = 1.4142135623730951;

  bool _inside(int i, int j) => i >= 0 && j >= 0 && i < width && j < depth;

  /// Whether something can stand in cell ([i], [j]). Off the grid is closed,
  /// and so is a cell a standing [NavObstacle] shuts.
  bool isOpen(int i, int j) =>
      _inside(i, j) && _open[j * width + i] == 1 && _shut[j * width + i] == 0;

  void setOpen(int i, int j, {required bool open}) {
    if (_inside(i, j)) _open[j * width + i] = open ? 1 : 0;
  }

  /// The height of the floor of cell ([i], [j]), or 0 off the grid.
  double heightOf(int i, int j) =>
      _inside(i, j) ? _heights[j * width + i].toDouble() : 0;

  void setHeight(int i, int j, double height) {
    if (_inside(i, j)) _heights[j * width + i] = height;
  }

  /// How many times dearer a metre is in cell ([i], [j]) than on the flat.
  double weightOf(int i, int j) =>
      _inside(i, j) ? _weights[j * width + i].toDouble() : 1;

  /// Sets what a metre costs in a cell. Below 1 is refused, because a search
  /// that can be rewarded for a detour cannot be steered by distance.
  void setWeight(int i, int j, double weight) {
    if (!(weight >= 1) || !weight.isFinite) {
      throw ArgumentError.value(weight, 'weight', 'Must be 1 or more.');
    }
    if (_inside(i, j)) _weights[j * width + i] = weight;
  }

  /// The cell holding world ([x], [z]), or null off the grid.
  NavCell? cellAt(double x, double z) {
    final i = ((x - originX) / cell).floor();
    final j = ((z - originZ) / cell).floor();
    return _inside(i, j) ? (i: i, j: j) : null;
  }

  /// The middle of cell ([i], [j]) at its floor.
  Vector3 centreOf(int i, int j) => Vector3(
    originX + (i + 0.5) * cell,
    heightOf(i, j),
    originZ + (j + 0.5) * cell,
  );

  /// Closes every cell the rectangle touches.
  void blockRect({
    required double minX,
    required double minZ,
    required double maxX,
    required double maxZ,
  }) {
    for (final at in _rectCells(minX, minZ, maxX, maxZ)) {
      _open[at] = 0;
    }
  }

  /// Closes every cell whose middle is within [radius] of ([x], [z]).
  void blockCircle(double x, double z, double radius) {
    for (final at in _circleCells(x, z, radius)) {
      _open[at] = 0;
    }
  }

  /// Shuts every cell the rectangle touches until the obstacle is lifted.
  ///
  /// A door that closes, a crate pushed into a corridor. Unlike [blockRect]
  /// it can be undone, and only the cells it touched change either way.
  NavObstacle addObstacleRect({
    required double minX,
    required double minZ,
    required double maxX,
    required double maxZ,
  }) => _shutting(_rectCells(minX, minZ, maxX, maxZ));

  /// Shuts every cell whose middle is within [radius] of ([x], [z]) until the
  /// obstacle is lifted.
  NavObstacle addObstacleCircle(double x, double z, double radius) =>
      _shutting(_circleCells(x, z, radius));

  NavObstacle _shutting(Iterable<int> cells) {
    final shut = cells.toList(growable: false);
    for (final at in shut) {
      _shut[at]++;
    }
    return NavObstacle._(this, shut);
  }

  Iterable<int> _rectCells(
    double minX,
    double minZ,
    double maxX,
    double maxZ,
  ) sync* {
    final first = math.max(0, ((minX - originX) / cell).floor());
    final last = math.min(width - 1, ((maxX - originX) / cell).floor());
    final top = math.max(0, ((minZ - originZ) / cell).floor());
    final bottom = math.min(depth - 1, ((maxZ - originZ) / cell).floor());
    for (var j = top; j <= bottom; j++) {
      for (var i = first; i <= last; i++) {
        yield j * width + i;
      }
    }
  }

  Iterable<int> _circleCells(double x, double z, double radius) sync* {
    final reach = radius / cell;
    final ci = ((x - originX) / cell - 0.5).round();
    final cj = ((z - originZ) / cell - 0.5).round();
    final span = reach.ceil() + 1;
    for (var j = cj - span; j <= cj + span; j++) {
      for (var i = ci - span; i <= ci + span; i++) {
        if (!_inside(i, j)) continue;
        final dx = originX + (i + 0.5) * cell - x;
        final dz = originZ + (j + 0.5) * cell - z;
        if (dx * dx + dz * dz <= radius * radius) yield j * width + i;
      }
    }
  }

  /// A copy with every cell within [radius] of a closed one closed too, so a
  /// route planned on it stays that far from every wall for something that
  /// size. The edge of the grid is not a wall. A cell a standing obstacle
  /// shuts counts as closed, and the copy does not change when it is lifted.
  NavGrid inflated(double radius) {
    final out = NavGrid(
      width: width,
      depth: depth,
      cell: cell,
      originX: originX,
      originZ: originZ,
    );
    out._heights.setAll(0, _heights);
    out._weights.setAll(0, _weights);
    out._open.setAll(0, _open);
    for (var j = 0; j < depth; j++) {
      for (var i = 0; i < width; i++) {
        if (!isOpen(i, j)) {
          out.blockCircle(
            originX + (i + 0.5) * cell,
            originZ + (j + 0.5) * cell,
            radius,
          );
        }
      }
    }
    return out;
  }

  /// The open cell nearest ([x], [z]) within [within] cells, or null.
  NavCell? nearestOpen(double x, double z, {int within = 8}) {
    final start = cellAt(x, z);
    final ci = start?.i ?? ((x - originX) / cell).floor();
    final cj = start?.j ?? ((z - originZ) / cell).floor();
    NavCell? best;
    var bestGap = double.infinity;
    for (var j = cj - within; j <= cj + within; j++) {
      for (var i = ci - within; i <= ci + within; i++) {
        if (!isOpen(i, j)) continue;
        final dx = originX + (i + 0.5) * cell - x;
        final dz = originZ + (j + 0.5) * cell - z;
        final gap = dx * dx + dz * dz;
        if (gap < bestGap) {
          bestGap = gap;
          best = (i: i, j: j);
        }
      }
    }
    return best;
  }

  /// Whether the straight line between two places crosses only open cells.
  /// A line through a corner where two cells meet needs both of the others
  /// open, so it cannot squeeze between two walls that touch.
  bool isClear(Vector3 from, Vector3 to) => _lineOver(from, to, isOpen);

  /// Whether the line between two places crosses only cells that [allowed]
  /// says yes to, with the corner rule of [isClear].
  bool _lineOver(
    Vector3 from,
    Vector3 to,
    bool Function(int i, int j) allowed,
  ) {
    final ax = (from.x - originX) / cell;
    final az = (from.z - originZ) / cell;
    final dx = (to.x - originX) / cell - ax;
    final dz = (to.z - originZ) / cell - az;
    var i = ax.floor();
    var j = az.floor();
    final endI = ((to.x - originX) / cell).floor();
    final endJ = ((to.z - originZ) / cell).floor();
    if (!allowed(i, j)) return false;
    final stepI = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
    final stepJ = dz > 0 ? 1 : (dz < 0 ? -1 : 0);
    final deltaX = dx == 0 ? double.infinity : 1 / dx.abs();
    final deltaZ = dz == 0 ? double.infinity : 1 / dz.abs();
    var nextX = dx == 0
        ? double.infinity
        : ((dx > 0 ? i + 1 - ax : ax - i) * deltaX);
    var nextZ = dz == 0
        ? double.infinity
        : ((dz > 0 ? j + 1 - az : az - j) * deltaZ);
    var budget = (endI - i).abs() + (endJ - j).abs() + 2;
    while ((i != endI || j != endJ) && budget-- > 0) {
      if (nextX < nextZ) {
        i += stepI;
        nextX += deltaX;
      } else if (nextZ < nextX) {
        j += stepJ;
        nextZ += deltaZ;
      } else {
        if (!allowed(i + stepI, j) || !allowed(i, j + stepJ)) return false;
        i += stepI;
        j += stepJ;
        nextX += deltaX;
        nextZ += deltaZ;
      }
      if (!allowed(i, j)) return false;
    }
    return true;
  }

  /// A route from [from] to [to] over open cells, as the points to walk
  /// through, or null when there is none.
  ///
  /// A start or a goal in a closed cell is taken to the nearest open one,
  /// which is what an agent pushed into a wall or told to go to a rock wants.
  /// The route is the cheapest over the cells and then pulled tight: every
  /// point that a straight line can skip is dropped, so it turns only where
  /// a corner makes it. Heights are the floor of the cell at each point.
  List<Vector3>? findPath(Vector3 from, Vector3 to) {
    final start = _openNear(from);
    final goal = _openNear(to);
    if (start == null || goal == null) return null;
    final cells = _search(start.j * width + start.i, goal.j * width + goal.i);
    if (cells == null) return null;
    final points = [
      _groundAt(from, start),
      for (final at in cells.skip(1).take(math.max(0, cells.length - 2)))
        centreOf(at % width, at ~/ width),
      _groundAt(to, goal),
    ];
    return _pulledTight(points);
  }

  NavCell? _openNear(Vector3 at) {
    final inside = cellAt(at.x, at.z);
    if (inside != null && isOpen(inside.i, inside.j)) return inside;
    return nearestOpen(at.x, at.z);
  }

  /// [at] kept where it is when its cell is open, and the middle of [cell]
  /// otherwise, which is where it was taken to.
  Vector3 _groundAt(Vector3 at, NavCell nearest) {
    final inside = cellAt(at.x, at.z);
    if (inside == nearest) {
      return Vector3(at.x, heightOf(nearest.i, nearest.j), at.z);
    }
    return centreOf(nearest.i, nearest.j);
  }

  /// [points] with every one dropped that a straight line can skip. A line
  /// only skips over cells that cost what the cell it leaves does, so a
  /// detour round dear ground is not cut back through it.
  List<Vector3> _pulledTight(List<Vector3> points) {
    final out = [points.first];
    var anchor = 0;
    while (anchor < points.length - 1) {
      final weight = _weightAt(points[anchor]);
      bool same(int i, int j) => isOpen(i, j) && weightOf(i, j) == weight;
      var far = anchor + 1;
      while (far + 1 < points.length &&
          _lineOver(points[anchor], points[far + 1], same)) {
        far++;
      }
      out.add(points[far]);
      anchor = far;
    }
    return out;
  }

  double _weightAt(Vector3 at) {
    final inside = cellAt(at.x, at.z);
    return inside == null ? 1 : weightOf(inside.i, inside.j);
  }

  static const List<(int, int)> _around = [
    (1, 0),
    (-1, 0),
    (0, 1),
    (0, -1),
    (1, 1),
    (1, -1),
    (-1, 1),
    (-1, -1),
  ];

  /// The cheapest run of cell numbers from [start] to [goal], or null.
  List<int>? _search(int start, int goal) {
    final count = width * depth;
    final spent = Float64List(count)..fillRange(0, count, double.infinity);
    final came = Int32List(count)..fillRange(0, count, -1);
    final done = Uint8List(count);
    final queue = MinHeap();
    final gi = goal % width;
    final gj = goal ~/ width;
    double estimate(int i, int j) {
      final dx = (i - gi).abs();
      final dz = (j - gj).abs();
      return (math.max(dx, dz) + (_diagonal - 1) * math.min(dx, dz)) * cell;
    }

    spent[start] = 0;
    queue.add(start, estimate(start % width, start ~/ width));
    while (!queue.isEmpty) {
      final at = queue.removeFirst();
      if (done[at] == 1) continue;
      done[at] = 1;
      if (at == goal) return _unwind(came, goal);
      final i = at % width;
      final j = at ~/ width;
      for (final (di, dj) in _around) {
        final ni = i + di;
        final nj = j + dj;
        if (!isOpen(ni, nj)) continue;
        if (di != 0 && dj != 0 && !(isOpen(i + di, j) && isOpen(i, j + dj))) {
          continue;
        }
        final next = nj * width + ni;
        if (done[next] == 1) continue;
        final run = (di != 0 && dj != 0 ? _diagonal : 1) * cell;
        final cost = spent[at] + run * (_weights[at] + _weights[next]) / 2;
        if (cost >= spent[next]) continue;
        spent[next] = cost;
        came[next] = at;
        queue.add(next, cost + estimate(ni, nj));
      }
    }
    return null;
  }

  static List<int> _unwind(Int32List came, int goal) {
    final run = <int>[];
    for (var at = goal; at >= 0; at = came[at]) {
      run.add(at);
    }
    return run.reversed.toList();
  }
}

/// Cells of a [NavGrid] shut for as long as it stands.
///
/// Made by [NavGrid.addObstacleRect] or [NavGrid.addObstacleCircle]. To move
/// one, lift it and add another.
final class NavObstacle {
  NavObstacle._(this._grid, this._cells);

  final NavGrid _grid;
  final List<int> _cells;
  bool _standing = true;

  /// Whether the cells are still shut by this obstacle.
  bool get standing => _standing;

  /// Opens the cells it shut, except any a wall or another obstacle also
  /// shuts. Lifting one that is not standing does nothing.
  void lift() {
    if (!_standing) return;
    _standing = false;
    for (final at in _cells) {
      _grid._shut[at]--;
    }
  }
}
