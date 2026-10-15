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

/// A 20 by 20 field with a wall at x 10 to 11 from z 0 up to 15.
NavGrid walled() {
  final grid = NavGrid(width: 20, depth: 20, cell: 1);
  grid.blockRect(minX: 10, minZ: 0, maxX: 10.5, maxZ: 14.5);
  return grid;
}

void main() {
  group('the grid', () {
    test('is open until closed, and closed off the edge', () {
      final grid = NavGrid(width: 4, depth: 4, cell: 2);
      expect(grid.isOpen(0, 0), isTrue);
      expect(grid.isOpen(-1, 0), isFalse);
      expect(grid.isOpen(4, 0), isFalse);
      grid.setOpen(1, 1, open: false);
      expect(grid.isOpen(1, 1), isFalse);
    });

    test('finds the cell at a place and the middle of a cell', () {
      final grid = NavGrid(
        width: 4,
        depth: 4,
        cell: 2,
        originX: 10,
        originZ: -4,
      );
      expect(grid.cellAt(13, -3), (i: 1, j: 0));
      expect(grid.cellAt(9, 0), isNull);
      final middle = grid.centreOf(1, 0);
      expect(middle.x, 13);
      expect(middle.z, -3);
    });

    test('keeps a height and a weight for each cell', () {
      final grid = NavGrid(width: 2, depth: 2, cell: 1);
      grid.setHeight(1, 0, 3.5);
      grid.setWeight(0, 1, 4);
      expect(grid.heightOf(1, 0), 3.5);
      expect(grid.weightOf(0, 1), 4);
      expect(grid.weightOf(0, 0), 1);
      expect(() => grid.setWeight(0, 0, 0.5), throwsArgumentError);
    });

    test('refuses an empty grid or a cell of no size', () {
      expect(() => NavGrid(width: 0, depth: 4, cell: 1), throwsArgumentError);
      expect(() => NavGrid(width: 4, depth: 4, cell: 0), throwsArgumentError);
    });

    test('blocks a circle of cells', () {
      final grid = NavGrid(width: 10, depth: 10, cell: 1);
      grid.blockCircle(5, 5, 1.2);
      expect(grid.isOpen(4, 4), isFalse);
      expect(grid.isOpen(5, 5), isFalse);
      expect(grid.isOpen(7, 5), isTrue);
    });
  });

  group('lines of sight', () {
    test('a line over open cells is clear', () {
      final grid = NavGrid(width: 10, depth: 10, cell: 1);
      expect(grid.isClear(Vector3(0.5, 0, 0.5), Vector3(9.5, 0, 9.5)), isTrue);
    });

    test('a wall in the way is not', () {
      final grid = walled();
      expect(grid.isClear(Vector3(5, 0, 5), Vector3(15, 0, 5)), isFalse);
      expect(grid.isClear(Vector3(5, 0, 17), Vector3(15, 0, 17)), isTrue);
    });

    test('a line cannot squeeze between two walls that touch', () {
      final grid = NavGrid(width: 10, depth: 10, cell: 1);
      grid.setOpen(4, 4, open: false);
      grid.setOpen(5, 5, open: false);
      expect(grid.isClear(Vector3(4.5, 0, 5.5), Vector3(5.5, 0, 4.5)), isFalse);
    });
  });

  group('finding a path', () {
    test('goes straight over open ground', () {
      final grid = NavGrid(width: 20, depth: 20, cell: 1);
      final path = grid.findPath(Vector3(2, 0, 3), Vector3(17, 0, 12))!;
      expect(path, hasLength(2));
      expect(path.first.x, 2);
      expect(path.last.z, 12);
    });

    test('goes round a wall and not through it', () {
      final grid = walled();
      final path = grid.findPath(Vector3(5.5, 0, 5.5), Vector3(15.5, 0, 5.5))!;
      expect(path.length, greaterThan(2));
      expect(
        path.map((p) => p.z).reduce((a, b) => a > b ? a : b),
        greaterThan(14.5),
      );
      for (var i = 1; i < path.length; i++) {
        expect(grid.isClear(path[i - 1], path[i]), isTrue);
      }
      expect(lengthOf(path), greaterThan(10));
    });

    test('says so when there is no way', () {
      final grid = NavGrid(width: 10, depth: 10, cell: 1);
      grid.blockRect(minX: 0, minZ: 4.5, maxX: 10, maxZ: 5.5);
      expect(grid.findPath(Vector3(5, 0, 1), Vector3(5, 0, 8)), isNull);
    });

    test('takes a start in a wall to the nearest open cell', () {
      final grid = walled();
      final path = grid.findPath(Vector3(10.5, 0, 5), Vector3(15.5, 0, 5))!;
      expect(
        grid.isOpen(
          grid.cellAt(path.first.x, path.first.z)!.i,
          grid.cellAt(path.first.x, path.first.z)!.j,
        ),
        isTrue,
      );
    });

    test('is null when nothing is open', () {
      final grid = NavGrid(width: 3, depth: 3, cell: 1);
      grid.blockRect(minX: 0, minZ: 0, maxX: 3, maxZ: 3);
      expect(grid.findPath(Vector3(1, 0, 1), Vector3(2, 0, 2)), isNull);
    });

    test('prefers cheap ground to a shorter way over dear ground', () {
      final grid = NavGrid(width: 21, depth: 9, cell: 1);
      for (var j = 0; j < 6; j++) {
        for (var i = 8; i < 13; i++) {
          grid.setWeight(i, j, 8);
        }
      }
      final path = grid.findPath(Vector3(1.5, 0, 2.5), Vector3(19.5, 0, 2.5))!;
      expect(
        path.map((p) => p.z).reduce((a, b) => a > b ? a : b),
        greaterThan(5.9),
      );
    });

    test('carries the height of the ground along', () {
      final grid = NavGrid(width: 10, depth: 1, cell: 1);
      for (var i = 0; i < 10; i++) {
        grid.setHeight(i, 0, i * 0.5);
      }
      final path = grid.findPath(Vector3(0.5, 0, 0.5), Vector3(8.5, 0, 0.5))!;
      expect(path.last.y, closeTo(4, 1e-6));
    });
  });

  group('for something with a size', () {
    test('an inflated grid closes the cells near a wall', () {
      final grid = walled();
      final fat = grid.inflated(1.5);
      expect(grid.isOpen(9, 5), isTrue);
      expect(fat.isOpen(9, 5), isFalse);
      expect(fat.isOpen(8, 5), isTrue);
      expect(fat.isOpen(10, 5), isFalse);
    });

    test('a route on it stays further from the wall end', () {
      final grid = walled();
      final from = Vector3(5.5, 0, 5.5);
      final to = Vector3(15.5, 0, 5.5);
      double top(NavGrid g) =>
          g.findPath(from, to)!.map((p) => p.z).reduce((a, b) => a > b ? a : b);
      expect(top(grid.inflated(1.5)), greaterThan(top(grid) + 0.5));
    });

    test('does not change the grid it was made from', () {
      final grid = walled();
      grid.inflated(3);
      expect(grid.isOpen(7, 5), isTrue);
    });
  });
}
