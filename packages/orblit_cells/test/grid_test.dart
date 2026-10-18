import 'package:orblit_cells/orblit_cells.dart';
import 'package:test/test.dart';

void main() {
  group('a grid', () {
    final grid = Grid(10);

    test('rounds down, so the cell before the origin is minus one', () {
      expect(grid.cellOf(0, 0), (x: 0, z: 0));
      expect(grid.cellOf(9.99, 10), (x: 0, z: 1));
      expect(grid.cellOf(-0.01, -10), (x: -1, z: -1));
    });

    test('has no cell for a point that is not a number', () {
      expect(grid.cellOf(double.nan, 0), isNull);
      expect(grid.cellOf(0, double.infinity), isNull);
    });

    test('puts the centre in the middle of the cell', () {
      expect(grid.centerOf((x: 2, z: -1)), (x: 25.0, z: -5.0));
    });

    test('counts a diagonal step as one', () {
      expect(grid.apart((x: 0, z: 0), (x: 3, z: -2)), 3);
      expect(grid.apart((x: 1, z: 1), (x: 1, z: 1)), 0);
    });

    test('lists the square around a cell', () {
      final cells = grid.around((x: 5, z: 5), 2).toSet();
      expect(cells, hasLength(25));
      expect(cells, contains((x: 3, z: 7)));
      expect(grid.around((x: 5, z: 5), 0), [(x: 5, z: 5)]);
    });

    test('refuses a size that is not above zero', () {
      for (final size in [0.0, -1.0, double.nan, double.infinity]) {
        expect(() => Grid(size), throwsArgumentError);
      }
    });

    test('refuses a negative radius', () {
      expect(() => grid.around((x: 0, z: 0), -1).toList(), throwsArgumentError);
    });
  });
}
