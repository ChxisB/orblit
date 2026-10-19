import 'package:orblit_cells/orblit_cells.dart';
import 'package:test/test.dart';

void main() {
  final grid = Grid(10);
  // The point is the middle of cell (0, 0) unless a test moves it.
  const middle = 5.0;

  group('what to load', () {
    test('is every cell within the radius, the one under the point first', () {
      final window = CellWindow(grid, loadRadius: 2);

      final plan = window.plan(middle, middle);

      expect(plan.load, hasLength(25));
      expect(plan.load.first, (x: 0, z: 0));
    });

    test('puts a cell in front before an equally near cell behind', () {
      final window = CellWindow(grid, loadRadius: 1);

      final plan = window.plan(middle, middle, facingX: 1);

      final ahead = plan.load.indexOf((x: 1, z: 0));
      final behind = plan.load.indexOf((x: -1, z: 0));
      expect(ahead, lessThan(behind));
    });

    test('ranks by distance alone when behind counts for nothing', () {
      final window = CellWindow(grid, loadRadius: 1, behindWeight: 0);

      final plan = window.plan(middle, middle, facingX: 1);

      // Same distance, so the coordinates decide and the plan is repeatable.
      expect(
        plan.load.indexOf((x: -1, z: 0)),
        lessThan(plan.load.indexOf((x: 1, z: 0))),
      );
    });

    test('leaves out a cell that is already loaded', () {
      final window = CellWindow(grid, loadRadius: 1);

      final plan = window.plan(
        middle,
        middle,
        loaded: {(x: 0, z: 0), (x: 1, z: 0)},
      );

      expect(plan.load, hasLength(7));
      expect(plan.load, isNot(contains((x: 0, z: 0))));
    });

    test('asks for a pinned cell beyond the radius', () {
      final window = CellWindow(grid, loadRadius: 1);

      final plan = window.plan(middle, middle, pinned: {(x: 9, z: 9)});

      expect(plan.load, contains((x: 9, z: 9)));
    });
  });

  group('what to drop', () {
    test('keeps a cell in the margin and drops one past it', () {
      final window = CellWindow(grid, loadRadius: 1);
      final inMargin = (x: 2, z: 0);
      final past = (x: 3, z: 0);

      final plan = window.plan(middle, middle, loaded: {inMargin, past});

      expect(plan.drop, [past]);
      expect(plan.load, isNot(contains(inMargin)));
    });

    test('does not flicker for a point walking across a border', () {
      final window = CellWindow(grid, loadRadius: 1);
      final loaded = <Cell>{...grid.around((x: 0, z: 0), 1)};

      // Over the border into cell 1 and back.
      for (final x in [9.0, 11.0, 9.0, 11.0]) {
        final plan = window.plan(x, middle, loaded: loaded);
        expect(plan.drop, isEmpty, reason: 'at x = $x');
        loaded.addAll(plan.load);
      }
    });

    test('drops the farthest first', () {
      final window = CellWindow(grid, loadRadius: 0, dropRadius: 0);

      final plan = window.plan(
        middle,
        middle,
        loaded: {(x: 2, z: 0), (x: 5, z: 0), (x: 3, z: 0)},
      );

      expect(plan.drop, [(x: 5, z: 0), (x: 3, z: 0), (x: 2, z: 0)]);
    });

    test('never drops a pinned cell', () {
      final window = CellWindow(grid, loadRadius: 0);
      final far = (x: 8, z: 8);

      final plan = window.plan(middle, middle, loaded: {far}, pinned: {far});

      expect(plan.drop, isEmpty);
    });
  });

  test('a point that is not a number plans nothing', () {
    final window = CellWindow(grid, loadRadius: 1);

    final plan = window.plan(double.nan, 0, loaded: {(x: 0, z: 0)});

    expect(plan.load, isEmpty);
    expect(plan.drop, isEmpty);
  });

  test('a point far past the world keeps the world', () {
    final window = CellWindow(grid, loadRadius: 1);

    final plan = window.plan(1e300, 0, loaded: {(x: 0, z: 0)});

    expect(plan.load, isEmpty);
    expect(plan.drop, isEmpty);
  });

  test('the cell the point stands in loads before one ahead of it', () {
    final window = CellWindow(grid, loadRadius: 1);

    // Near the border, facing the neighbour. Its middle is straight ahead and
    // the cell underfoot has its middle behind.
    final plan = window.plan(0.1, 5, facingX: -1);

    expect(plan.load.first, (x: 0, z: 0));
    expect(plan.load[1], (x: -1, z: 0));
  });

  test('a drop radius below the load radius is refused', () {
    expect(
      () => CellWindow(grid, loadRadius: 2, dropRadius: 1),
      throwsArgumentError,
    );
    expect(() => CellWindow(grid, loadRadius: -1), throwsArgumentError);
    expect(
      () => CellWindow(grid, loadRadius: 1, behindWeight: -1),
      throwsArgumentError,
    );
  });
}
