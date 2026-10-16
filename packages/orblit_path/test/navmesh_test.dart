import 'package:orblit_path/orblit_path.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

double lengthOf(List<Vector3> path) {
  var total = 0.0;
  for (var i = 1; i < path.length; i++) {
    total += path[i].distanceTo(path[i - 1]);
  }
  return total;
}

double top(List<Vector3> path) =>
    path.map((p) => p.z).reduce((a, b) => a > b ? a : b);

/// A 20 by 20 field with a wall at x 10 to 11 from z 0 up to 15.
NavGrid walled() {
  final grid = NavGrid(width: 20, depth: 20, cell: 1);
  grid.blockRect(minX: 10, minZ: 0, maxX: 10.5, maxZ: 14.5);
  return grid;
}

/// Two corridors three cells wide that meet at a right angle. Open ground
/// is the bottom rows and the right columns, turned by [flipX] and [flipZ].
NavGrid elbow({required bool flipX, required bool flipZ}) {
  final grid = NavGrid(width: 20, depth: 20, cell: 1);
  for (var j = 0; j < 20; j++) {
    for (var i = 0; i < 20; i++) {
      final x = flipX ? 19 - i : i;
      final z = flipZ ? 19 - j : j;
      grid.setOpen(x, z, open: j < 3 || i >= 17);
    }
  }
  return grid;
}

void main() {
  group('building', () {
    test('merges open ground into a few rectangles', () {
      final mesh = NavMesh.fromGrid(NavGrid(width: 50, depth: 50, cell: 1));
      expect(mesh.count, 1);
    });

    test('keeps a walled field to a handful', () {
      final mesh = NavMesh.fromGrid(walled());
      expect(mesh.count, lessThan(10));
      expect(mesh.count, greaterThan(1));
    });

    test('has nothing in a grid with nothing open', () {
      final grid = NavGrid(width: 4, depth: 4, cell: 1)
        ..blockRect(minX: 0, minZ: 0, maxX: 4, maxZ: 4);
      final mesh = NavMesh.fromGrid(grid);
      expect(mesh.count, 0);
      expect(mesh.findPath(Vector3(1, 0, 1), Vector3(3, 0, 3)), isNull);
    });

    test('does not follow the grid when it changes afterwards', () {
      final grid = NavGrid(width: 20, depth: 20, cell: 1);
      final mesh = NavMesh.fromGrid(grid);
      grid.blockRect(minX: 0, minZ: 9, maxX: 20, maxZ: 10);
      expect(mesh.findPath(Vector3(5, 0, 2), Vector3(5, 0, 18)), isNotNull);
    });
  });

  group('finding a path', () {
    test('is a straight line over open ground', () {
      final mesh = NavMesh.fromGrid(NavGrid(width: 30, depth: 30, cell: 1));
      final path = mesh.findPath(Vector3(2, 0, 3), Vector3(25, 0, 20))!;
      expect(path, hasLength(2));
      expect(path.first.x, 2);
      expect(path.last.z, 20);
    });

    test('turns at the two corners of a wall end and nowhere else', () {
      final mesh = NavMesh.fromGrid(walled());
      final path = mesh.findPath(Vector3(5.5, 0, 5.5), Vector3(15.5, 0, 5.5))!;
      expect(path, hasLength(4));
      expect(path[1].x, closeTo(10, 1e-9));
      expect(path[1].z, closeTo(15, 1e-9));
      expect(path[2].x, closeTo(11, 1e-9));
      expect(path[2].z, closeTo(15, 1e-9));
    });

    test('is no longer than the grid path and often shorter', () {
      final grid = walled();
      final mesh = NavMesh.fromGrid(grid);
      final from = Vector3(2.5, 0, 3.5);
      final to = Vector3(17.5, 0, 8.5);
      final onGrid = grid.findPath(from, to)!;
      final onMesh = mesh.findPath(from, to)!;
      expect(lengthOf(onMesh), lessThanOrEqualTo(lengthOf(onGrid) + 1e-9));
      expect(lengthOf(onMesh), greaterThan(from.distanceTo(to)));
    });

    test('never crosses a closed cell', () {
      final grid = walled();
      grid.blockRect(minX: 3, minZ: 16, maxX: 15, maxZ: 17);
      final mesh = NavMesh.fromGrid(grid);
      final path = mesh.findPath(Vector3(2.5, 0, 2.5), Vector3(17.5, 0, 18.5))!;
      for (var i = 1; i < path.length; i++) {
        expect(grid.isClear(path[i - 1], path[i]), isTrue, reason: '$i');
      }
    });

    test('says so when there is no way', () {
      final grid = NavGrid(width: 10, depth: 10, cell: 1)
        ..blockRect(minX: 0, minZ: 4.5, maxX: 10, maxZ: 5.5);
      final mesh = NavMesh.fromGrid(grid);
      expect(mesh.findPath(Vector3(5, 0, 1), Vector3(5, 0, 8)), isNull);
    });

    test('takes a goal in a wall to the nearest open cell', () {
      final grid = walled();
      final mesh = NavMesh.fromGrid(grid);
      final path = mesh.findPath(Vector3(5.5, 0, 5.5), Vector3(10.5, 0, 5.5))!;
      final end = grid.cellAt(path.last.x, path.last.z)!;
      expect(grid.isOpen(end.i, end.j), isTrue);
    });

    test('carries the height of the ground along', () {
      final grid = NavGrid(width: 10, depth: 4, cell: 1);
      for (var j = 0; j < 4; j++) {
        for (var i = 0; i < 10; i++) {
          grid.setHeight(i, j, 2);
        }
      }
      final path = NavMesh.fromGrid(
        grid,
      ).findPath(Vector3(0.5, 0, 1.5), Vector3(8.5, 0, 1.5))!;
      for (final point in path) {
        expect(point.y, closeTo(2, 1e-6));
      }
    });

    test('keeps a route off dear ground when there is a cheap way', () {
      final grid = NavGrid(width: 21, depth: 9, cell: 1);
      for (var j = 0; j < 6; j++) {
        for (var i = 8; i < 13; i++) {
          grid.setWeight(i, j, 8);
        }
      }
      final path = NavMesh.fromGrid(
        grid,
      ).findPath(Vector3(1.5, 0, 2.5), Vector3(19.5, 0, 2.5))!;
      expect(top(path), greaterThan(5.9));
    });
  });

  group('the funnel', () {
    // Open corridors meet at an inner corner (17, 3) before the flip. A
    // funnel with its sides swapped goes round the outer corner instead.
    for (final flipX in [false, true]) {
      for (final flipZ in [false, true]) {
        test('turns the inner corner with flipX $flipX and flipZ $flipZ', () {
          final mesh = NavMesh.fromGrid(elbow(flipX: flipX, flipZ: flipZ));
          double x(double v) => flipX ? 20 - v : v;
          double z(double v) => flipZ ? 20 - v : v;
          final from = Vector3(x(1.5), 0, z(1.5));
          final to = Vector3(x(18.5), 0, z(18.5));
          final there = mesh.findPath(from, to)!;
          expect(there, hasLength(3));
          expect(there[1].x, closeTo(x(17), 1e-9));
          expect(there[1].z, closeTo(z(3), 1e-9));
          final back = mesh.findPath(to, from)!;
          expect(back, hasLength(3));
          expect(back[1].x, closeTo(x(17), 1e-9));
          expect(back[1].z, closeTo(z(3), 1e-9));
        });
      }
    }
  });

  group('for something with a size', () {
    test('a route stays one cell further out round a wall end', () {
      final grid = walled();
      final from = Vector3(5.5, 0, 5.5);
      final to = Vector3(15.5, 0, 5.5);
      final thin = NavMesh.fromGrid(grid).findPath(from, to)!;
      final fat = NavMesh.fromGrid(grid, radius: 1).findPath(from, to)!;
      expect(top(fat), closeTo(top(thin) + 1, 1e-9));
    });
  });
}
