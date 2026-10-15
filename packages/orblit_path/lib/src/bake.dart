import 'dart:math' as math;

import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:vector_math/vector_math_64.dart';

import 'grid.dart';

/// Whether [cover] marks ground an agent may walk on: the cover's
/// navigation bit.
bool marked(Cover cover) => cover.navigation;

/// A [NavGrid] over a terrain, one cell for each [cell] metres.
///
/// A cell is open where the ground exists, is not a hole, is no steeper
/// than [maxSlope] (radians) and [walkable] says its cover is ground to
/// walk on. By default that is the cover's navigation bit, so ground nobody
/// marked is closed; pass `(cover) => true` to walk anywhere. Each open
/// cell's weight is how much longer a metre of slope is than a metre of
/// map: one over the cosine of the slope.
///
/// The area is the whole terrain unless [minX], [minZ], [maxX] and [maxZ]
/// narrow it. A terrain with no regions gives a grid of one closed cell.
NavGrid bakeTerrain(
  Terrain terrain, {
  double cell = 1,
  double maxSlope = math.pi / 4,
  bool Function(Cover cover) walkable = marked,
  double? minX,
  double? minZ,
  double? maxX,
  double? maxZ,
}) {
  final area = _areaOf(terrain, minX, minZ, maxX, maxZ);
  final grid = NavGrid(
    width: math.max(1, ((area.maxX - area.minX) / cell).ceil()),
    depth: math.max(1, ((area.maxZ - area.minZ) / cell).ceil()),
    cell: cell,
    originX: area.minX,
    originZ: area.minZ,
  );
  final steepest = math.cos(maxSlope);
  for (var j = 0; j < grid.depth; j++) {
    for (var i = 0; i < grid.width; i++) {
      final at = grid.centreOf(i, j);
      final ground = terrain.heightAt(at.x, at.z);
      final cover = terrain.coverAt(at.x, at.z);
      final normal = terrain.normalAt(at.x, at.z);
      final open =
          ground != null &&
          cover != null &&
          normal != null &&
          walkable(cover) &&
          normal.y >= steepest;
      grid.setOpen(i, j, open: open);
      if (open) {
        grid.setHeight(i, j, ground);
        grid.setWeight(i, j, math.min(10, math.max(1, 1 / normal.y)));
      }
    }
  }
  return grid;
}

({double minX, double minZ, double maxX, double maxZ}) _areaOf(
  Terrain terrain,
  double? minX,
  double? minZ,
  double? maxX,
  double? maxZ,
) {
  final metres = terrain.regionSize * terrain.spacing;
  final keys = terrain.regions.map((region) => region.key).toList();
  if (keys.isEmpty) {
    return (minX: minX ?? 0, minZ: minZ ?? 0, maxX: maxX ?? 1, maxZ: maxZ ?? 1);
  }
  return (
    minX: minX ?? keys.map((k) => k.x).reduce(math.min) * metres,
    minZ: minZ ?? keys.map((k) => k.z).reduce(math.min) * metres,
    maxX: maxX ?? (keys.map((k) => k.x).reduce(math.max) + 1) * metres,
    maxZ: maxZ ?? (keys.map((k) => k.z).reduce(math.max) + 1) * metres,
  );
}

/// How a mesh is read for walking: what it is made of, and who walks on it.
///
/// [positions] is x, y, z for each vertex, one after another, and [indices]
/// three vertices for each triangle, already in world space.
typedef MeshWalk = ({
  List<double> positions,
  List<int> indices,
  double agentHeight,
  double stepHeight,
  double maxSlope,
});

/// Closes every cell of [grid] that [mesh] has a wall or a ceiling in
/// where an agent would stand.
///
/// A triangle that is steeper than [MeshWalk.maxSlope], or faces down, is
/// solid. A cell is closed when solid lies above its floor by more than
/// [MeshWalk.stepHeight] and below [MeshWalk.agentHeight]: a step is walked
/// over, a wall is not. A surface that faces up never closes anything, so
/// a mesh that is one plane seen from above has nothing under it blocked.
void carve(NavGrid grid, MeshWalk mesh) {
  _solids(grid, mesh, (i, j, y) {
    if (!grid.isOpen(i, j)) return;
    final floor = grid.heightOf(i, j);
    if (y > floor + mesh.stepHeight && y < floor + mesh.agentHeight) {
      grid.setOpen(i, j, open: false);
    }
  });
}

/// A [NavGrid] over a mesh: open where an up-facing triangle lies no
/// steeper than [MeshWalk.maxSlope], with room for the agent above it.
///
/// One floor to a cell, the lowest that has room. A bridge over a road is
/// not walkable; the road under it is. The area is the mesh's footprint
/// unless it is given.
NavGrid bakeMesh(
  MeshWalk mesh, {
  double cell = 0.5,
  double? minX,
  double? minZ,
  double? maxX,
  double? maxZ,
}) {
  final footprint = _footprintOf(mesh.positions);
  final left = minX ?? footprint.minX;
  final top = minZ ?? footprint.minZ;
  final grid = NavGrid(
    width: math.max(1, (((maxX ?? footprint.maxX) - left) / cell).ceil()),
    depth: math.max(1, (((maxZ ?? footprint.maxZ) - top) / cell).ceil()),
    cell: cell,
    originX: left,
    originZ: top,
  );
  final floors = _floorsOf(grid, mesh);
  final solids = <int, List<double>>{};
  _solids(grid, mesh, (i, j, y) {
    (solids[j * grid.width + i] ??= []).add(y);
  });
  for (var j = 0; j < grid.depth; j++) {
    for (var i = 0; i < grid.width; i++) {
      final key = j * grid.width + i;
      final floor = _lowestWithRoom(floors[key], solids[key], mesh);
      grid.setOpen(i, j, open: floor != null);
      if (floor != null) grid.setHeight(i, j, floor);
    }
  }
  return grid;
}

double? _lowestWithRoom(
  List<double>? floors,
  List<double>? solids,
  MeshWalk mesh,
) {
  if (floors == null) return null;
  final ordered = [...floors]..sort();
  for (final floor in ordered) {
    final blocked =
        solids?.any(
          (y) => y > floor + mesh.stepHeight && y < floor + mesh.agentHeight,
        ) ??
        false;
    if (!blocked) return floor;
  }
  return null;
}

({double minX, double minZ, double maxX, double maxZ}) _footprintOf(
  List<double> positions,
) {
  if (positions.length < 3) return (minX: 0, minZ: 0, maxX: 1, maxZ: 1);
  var minX = double.infinity;
  var minZ = double.infinity;
  var maxX = -double.infinity;
  var maxZ = -double.infinity;
  for (var v = 0; v + 2 < positions.length; v += 3) {
    minX = math.min(minX, positions[v]);
    maxX = math.max(maxX, positions[v]);
    minZ = math.min(minZ, positions[v + 2]);
    maxZ = math.max(maxZ, positions[v + 2]);
  }
  return (minX: minX, minZ: minZ, maxX: maxX, maxZ: maxZ);
}

Vector3 _vertex(List<double> positions, int index) => Vector3(
  positions[index * 3],
  positions[index * 3 + 1],
  positions[index * 3 + 2],
);

/// The height of each up-facing triangle where it covers the middle of a
/// cell, by cell number.
Map<int, List<double>> _floorsOf(NavGrid grid, MeshWalk mesh) {
  final floors = <int, List<double>>{};
  final steepest = math.cos(mesh.maxSlope);
  for (var t = 0; t + 2 < mesh.indices.length; t += 3) {
    final a = _vertex(mesh.positions, mesh.indices[t]);
    final b = _vertex(mesh.positions, mesh.indices[t + 1]);
    final c = _vertex(mesh.positions, mesh.indices[t + 2]);
    final normal = (b - a).cross(c - a);
    if (normal.length2 < 1e-18 || normal.normalized().y < steepest) continue;
    _cover(grid, a, b, c, (i, j, x, z) {
      final y = _planeHeight(a, normal, x, z);
      (floors[j * grid.width + i] ??= []).add(y);
    });
  }
  return floors;
}

/// Calls [hit] for every cell whose middle lies inside the triangle seen
/// from above.
void _cover(
  NavGrid grid,
  Vector3 a,
  Vector3 b,
  Vector3 c,
  void Function(int i, int j, double x, double z) hit,
) {
  final first = math.max(
    0,
    ((math.min(a.x, math.min(b.x, c.x)) - grid.originX) / grid.cell).floor(),
  );
  final last = math.min(
    grid.width - 1,
    ((math.max(a.x, math.max(b.x, c.x)) - grid.originX) / grid.cell).floor(),
  );
  final top = math.max(
    0,
    ((math.min(a.z, math.min(b.z, c.z)) - grid.originZ) / grid.cell).floor(),
  );
  final bottom = math.min(
    grid.depth - 1,
    ((math.max(a.z, math.max(b.z, c.z)) - grid.originZ) / grid.cell).floor(),
  );
  for (var j = top; j <= bottom; j++) {
    for (var i = first; i <= last; i++) {
      final x = grid.originX + (i + 0.5) * grid.cell;
      final z = grid.originZ + (j + 0.5) * grid.cell;
      if (_inTriangle(a, b, c, x, z)) hit(i, j, x, z);
    }
  }
}

bool _inTriangle(Vector3 a, Vector3 b, Vector3 c, double x, double z) {
  double side(Vector3 p, Vector3 q) =>
      (q.x - p.x) * (z - p.z) - (q.z - p.z) * (x - p.x);
  final d1 = side(a, b);
  final d2 = side(b, c);
  final d3 = side(c, a);
  final negative = d1 < 0 || d2 < 0 || d3 < 0;
  final positive = d1 > 0 || d2 > 0 || d3 > 0;
  return !(negative && positive);
}

double _planeHeight(Vector3 a, Vector3 normal, double x, double z) =>
    a.y - (normal.x * (x - a.x) + normal.z * (z - a.z)) / normal.y;

/// Calls [hit] with the cell and height of samples spread over every
/// triangle that is solid for walking, closely enough that no cell under
/// one is skipped.
void _solids(
  NavGrid grid,
  MeshWalk mesh,
  void Function(int i, int j, double y) hit,
) {
  final steepest = math.cos(mesh.maxSlope);
  final step = grid.cell / 3;
  for (var t = 0; t + 2 < mesh.indices.length; t += 3) {
    final a = _vertex(mesh.positions, mesh.indices[t]);
    final b = _vertex(mesh.positions, mesh.indices[t + 1]);
    final c = _vertex(mesh.positions, mesh.indices[t + 2]);
    final normal = (b - a).cross(c - a);
    if (normal.length2 < 1e-18 || normal.normalized().y >= steepest) continue;
    final reach = math.max(
      a.distanceTo(b),
      math.max(b.distanceTo(c), c.distanceTo(a)),
    );
    final n = math.max(1, (reach / step).ceil());
    for (var u = 0; u <= n; u++) {
      for (var v = 0; v <= n - u; v++) {
        final p = a + (b - a) * (u / n) + (c - a) * (v / n);
        final cell = grid.cellAt(p.x, p.z);
        if (cell != null) hit(cell.i, cell.j, p.y);
      }
    }
  }
}
