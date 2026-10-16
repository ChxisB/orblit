import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'grid.dart';
import 'heap.dart';

/// Ground as a few large rectangles, for finding a way across it.
///
/// Built from a [NavGrid]: open cells that cost the same to cross are merged
/// into the biggest rectangles that fit, and two rectangles are joined where
/// they share an edge. A route is found over those joins and then pulled
/// straight through them, so it turns only at the corners of walls. A big
/// open field is one rectangle and costs one step to search however many
/// cells it was, which is what a grid cannot do.
///
/// The mesh is a snapshot. Change the grid and build another.
final class NavMesh {
  factory NavMesh.fromGrid(NavGrid grid, {double radius = 0}) {
    final snapshot = grid.inflated(radius);
    final blocks = _merge(snapshot);
    final owner = Int32List(snapshot.width * snapshot.depth)
      ..fillRange(0, snapshot.width * snapshot.depth, -1);
    for (var b = 0; b < blocks.length; b++) {
      blocks[b].forEachCell((i, j) => owner[j * snapshot.width + i] = b);
    }
    final portals = _connect(snapshot, blocks, owner);
    final byBlock = List.generate(blocks.length, (_) => <int>[]);
    for (var p = 0; p < portals.length; p++) {
      byBlock[portals[p].a].add(p);
      byBlock[portals[p].b].add(p);
    }
    return NavMesh._(snapshot, blocks, owner, portals, byBlock);
  }

  NavMesh._(
    this._grid,
    this._blocks,
    this._owner,
    this._portals,
    this._portalsOf,
  );

  final NavGrid _grid;
  final List<_Block> _blocks;
  final Int32List _owner;
  final List<_Portal> _portals;
  final List<List<int>> _portalsOf;

  /// How many rectangles the ground was merged into.
  int get count => _blocks.length;

  /// A route from [from] to [to] as the points to walk through, or null
  /// when there is none.
  ///
  /// A start or a goal on closed ground is taken to the nearest open cell.
  /// The route is the shortest the mesh allows, with every point that a
  /// straight line could skip dropped.
  List<Vector3>? findPath(Vector3 from, Vector3 to) {
    final start = _standing(from);
    final goal = _standing(to);
    if (start == null || goal == null) return null;
    final route = _route(start, goal);
    if (route == null) return null;
    final gates = [
      for (final step in route) _gate(_portals[step.portal], step.from),
    ];
    return [
      for (final at in _funnel(start.at, goal.at, gates))
        Vector3(at.x, _heightNear(at.x, at.y), at.y),
    ];
  }

  /// Where [at] stands on the mesh: which rectangle, and the point in it,
  /// which is [at] unless it was taken to the nearest open cell.
  _Standing? _standing(Vector3 at) {
    final inside = _grid.cellAt(at.x, at.z);
    if (inside != null && _grid.isOpen(inside.i, inside.j)) {
      return _Standing(
        _owner[inside.j * _grid.width + inside.i],
        Vector2(at.x, at.z),
      );
    }
    final near = _grid.nearestOpen(at.x, at.z);
    if (near == null) return null;
    final middle = _grid.centreOf(near.i, near.j);
    return _Standing(
      _owner[near.j * _grid.width + near.i],
      Vector2(middle.x, middle.z),
    );
  }

  double _heightNear(double x, double z) {
    final half = _grid.cell / 2;
    var total = 0.0;
    var found = 0;
    for (final (dx, dz) in const [(-1, -1), (1, -1), (-1, 1), (1, 1)]) {
      final cell = _grid.cellAt(x + dx * half * 0.5, z + dz * half * 0.5);
      if (cell == null || !_grid.isOpen(cell.i, cell.j)) continue;
      total += _grid.heightOf(cell.i, cell.j);
      found++;
    }
    return found == 0 ? 0 : total / found;
  }

  /// The portals to cross from [start] to [goal], in order, each with the
  /// rectangle it is crossed from.
  List<_Crossing>? _route(_Standing start, _Standing goal) {
    if (start.block == goal.block) return const [];
    return _Search(
      portals: _portals,
      portalsOf: _portalsOf,
      blocks: _blocks,
      start: start,
      goal: goal,
    ).run();
  }

  /// The two ends of [portal] as [from] crosses it, left and right as seen
  /// looking the way it goes.
  ({Vector2 left, Vector2 right}) _gate(_Portal portal, int from) {
    final forward = from == portal.a;
    final lower = portal.alongZ
        ? Vector2(portal.at, portal.low)
        : Vector2(portal.low, portal.at);
    final upper = portal.alongZ
        ? Vector2(portal.at, portal.high)
        : Vector2(portal.high, portal.at);
    // Crossing along +x, left is +z. Crossing along +z, left is -x.
    final leftIsUpper = portal.alongZ ? forward : !forward;
    return leftIsUpper
        ? (left: upper, right: lower)
        : (left: lower, right: upper);
  }
}

/// A* over portals. A node is a portal's middle, with the start and the
/// goal as the two nodes after the last portal.
final class _Search {
  _Search({
    required this.portals,
    required this.portalsOf,
    required this.blocks,
    required this.start,
    required this.goal,
  }) : startNode = portals.length,
       goalNode = portals.length + 1,
       _spent = Float64List(portals.length + 2),
       _came = Int32List(portals.length + 2),
       _through = Int32List(portals.length + 2),
       _done = Uint8List(portals.length + 2) {
    _spent.fillRange(0, _spent.length, double.infinity);
    _came.fillRange(0, _came.length, -1);
    _through.fillRange(0, _through.length, -1);
  }

  final List<_Portal> portals;
  final List<List<int>> portalsOf;
  final List<_Block> blocks;
  final _Standing start;
  final _Standing goal;
  final int startNode;
  final int goalNode;

  final Float64List _spent;
  final Int32List _came;
  final Int32List _through;
  final Uint8List _done;
  final MinHeap _queue = MinHeap();

  Vector2 _where(int node) {
    if (node == startNode) return start.at;
    if (node == goalNode) return goal.at;
    return portals[node].middle;
  }

  List<_Crossing>? run() {
    _spent[startNode] = 0;
    _queue.add(startNode, _where(startNode).distanceTo(goal.at));
    while (!_queue.isEmpty) {
      final node = _queue.removeFirst();
      if (_done[node] == 1) continue;
      _done[node] = 1;
      if (node == goalNode) return _crossings();
      _expand(node);
    }
    return null;
  }

  void _expand(int node) {
    final inside = node == startNode
        ? [start.block]
        : [portals[node].a, portals[node].b];
    for (final block in inside) {
      for (final next in _leaving(node, block)) {
        _relax(node, next, block);
      }
    }
  }

  Iterable<int> _leaving(int node, int block) sync* {
    for (final other in portalsOf[block]) {
      if (other != node) yield other;
    }
    if (block == goal.block) yield goalNode;
  }

  void _relax(int node, int next, int block) {
    if (_done[next] == 1) return;
    final cost =
        _spent[node] +
        _where(node).distanceTo(_where(next)) * blocks[block].weight;
    if (cost >= _spent[next]) return;
    _spent[next] = cost;
    _came[next] = node;
    _through[next] = block;
    _queue.add(next, cost + _where(next).distanceTo(goal.at));
  }

  List<_Crossing> _crossings() {
    final out = <_Crossing>[];
    for (var node = _came[goalNode]; node != startNode; node = _came[node]) {
      out.add(_Crossing(node, _through[node]));
    }
    return out.reversed.toList();
  }
}

final class _Standing {
  const _Standing(this.block, this.at);

  final int block;
  final Vector2 at;
}

final class _Crossing {
  const _Crossing(this.portal, this.from);

  final int portal;
  final int from;
}

/// A rectangle of cells, ends included.
final class _Block {
  const _Block(this.i0, this.j0, this.i1, this.j1, this.weight);

  final int i0;
  final int j0;
  final int i1;
  final int j1;
  final double weight;

  void forEachCell(void Function(int i, int j) visit) {
    for (var j = j0; j <= j1; j++) {
      for (var i = i0; i <= i1; i++) {
        visit(i, j);
      }
    }
  }
}

/// The edge two blocks share. [a] is the one with the smaller coordinate
/// across it. An edge [alongZ] sits at x = [at] and runs from [low] to
/// [high] in z, and the blocks lie either side of it in x.
final class _Portal {
  const _Portal({
    required this.a,
    required this.b,
    required this.alongZ,
    required this.at,
    required this.low,
    required this.high,
  });

  final int a;
  final int b;
  final bool alongZ;
  final double at;
  final double low;
  final double high;

  Vector2 get middle =>
      alongZ ? Vector2(at, (low + high) / 2) : Vector2((low + high) / 2, at);
}

/// Greedy rectangles over the open cells, across then down, each holding
/// cells that cost the same.
List<_Block> _merge(NavGrid grid) {
  final taken = Uint8List(grid.width * grid.depth);
  final blocks = <_Block>[];
  bool free(int i, int j, double weight) =>
      grid.isOpen(i, j) &&
      taken[j * grid.width + i] == 0 &&
      grid.weightOf(i, j) == weight;
  for (var j = 0; j < grid.depth; j++) {
    for (var i = 0; i < grid.width; i++) {
      if (taken[j * grid.width + i] == 1 || !grid.isOpen(i, j)) continue;
      final weight = grid.weightOf(i, j);
      var right = i;
      while (free(right + 1, j, weight)) {
        right++;
      }
      var bottom = j;
      while (_rowFree(i, right, bottom + 1, (c, r) => free(c, r, weight))) {
        bottom++;
      }
      final block = _Block(i, j, right, bottom, weight);
      block.forEachCell((c, r) => taken[r * grid.width + c] = 1);
      blocks.add(block);
    }
  }
  return blocks;
}

bool _rowFree(int from, int to, int row, bool Function(int, int) free) {
  for (var i = from; i <= to; i++) {
    if (!free(i, row)) return false;
  }
  return true;
}

/// Every pair of blocks that touch along an edge, found once from the block
/// on the left or the top.
List<_Portal> _connect(NavGrid grid, List<_Block> blocks, Int32List owner) {
  final portals = <_Portal>[];
  int ownerOf(int i, int j) =>
      i < 0 || j < 0 || i >= grid.width || j >= grid.depth
      ? -1
      : owner[j * grid.width + i];
  for (var b = 0; b < blocks.length; b++) {
    final block = blocks[b];
    _runs(block.j0, block.j1, (j) => ownerOf(block.i1 + 1, j), (
      other,
      from,
      to,
    ) {
      portals.add(
        _Portal(
          a: b,
          b: other,
          alongZ: true,
          at: grid.originX + (block.i1 + 1) * grid.cell,
          low: grid.originZ + from * grid.cell,
          high: grid.originZ + (to + 1) * grid.cell,
        ),
      );
    });
    _runs(block.i0, block.i1, (i) => ownerOf(i, block.j1 + 1), (
      other,
      from,
      to,
    ) {
      portals.add(
        _Portal(
          a: b,
          b: other,
          alongZ: false,
          at: grid.originZ + (block.j1 + 1) * grid.cell,
          low: grid.originX + from * grid.cell,
          high: grid.originX + (to + 1) * grid.cell,
        ),
      );
    });
  }
  return portals;
}

/// Calls [found] for each unbroken run of one owner along [first] to [last].
void _runs(
  int first,
  int last,
  int Function(int at) ownerAt,
  void Function(int owner, int from, int to) found,
) {
  var start = first;
  while (start <= last) {
    final owner = ownerAt(start);
    var end = start;
    while (end + 1 <= last && ownerAt(end + 1) == owner) {
      end++;
    }
    if (owner >= 0) found(owner, start, end);
    start = end + 1;
  }
}

/// Which side of the line from [a] through [b] the point [c] is on: above
/// nought is the counter-clockwise side in x and z.
double _side(Vector2 a, Vector2 b, Vector2 c) =>
    (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

bool _same(Vector2 a, Vector2 b) => a.distanceToSquared(b) < 1e-12;

/// The shortest line from [start] to [goal] that passes through every gate,
/// by the simple stupid funnel: keep the two lines to the ends of the gates
/// so far, and when one crosses the other, its end is a corner.
List<Vector2> _funnel(
  Vector2 start,
  Vector2 goal,
  List<({Vector2 left, Vector2 right})> gates,
) {
  final all = [
    (left: start, right: start),
    ...gates,
    (left: goal, right: goal),
  ];
  final out = [start];
  var apex = start;
  var left = start;
  var right = start;
  var leftAt = 0;
  var rightAt = 0;
  var i = 1;
  while (i < all.length) {
    final gate = all[i];
    if (_side(apex, right, gate.right) >= 0) {
      if (_same(apex, right) || _side(apex, left, gate.right) < 0) {
        right = gate.right;
        rightAt = i;
      } else {
        out.add(left);
        apex = left;
        right = left;
        rightAt = leftAt;
        i = leftAt + 1;
        continue;
      }
    }
    if (_side(apex, left, gate.left) <= 0) {
      if (_same(apex, left) || _side(apex, right, gate.left) > 0) {
        left = gate.left;
        leftAt = i;
      } else {
        out.add(right);
        apex = right;
        left = right;
        leftAt = rightAt;
        i = rightAt + 1;
        continue;
      }
    }
    i++;
  }
  if (!_same(out.last, goal)) out.add(goal);
  return out;
}
