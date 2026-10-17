import 'package:orblit_agent/orblit_agent.dart';
import 'package:orblit_path/orblit_path.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A 40 by 30 field cut by a wall at x 20, with a four cell door at z 10 to
/// 13 and the long way round over the top, past z 21.
NavGrid walled() {
  final grid = NavGrid(width: 40, depth: 30, cell: 1);
  grid.blockRect(minX: 20, minZ: 0, maxX: 20.5, maxZ: 9.5);
  grid.blockRect(minX: 20, minZ: 14, maxX: 20.5, maxZ: 21.5);
  return grid;
}

NavObstacle shutDoor(NavGrid grid) =>
    grid.addObstacleRect(minX: 19.5, minZ: 10, maxX: 20.5, maxZ: 13.5);

final west = Vector3(3.5, 0, 11.5);
final east = Vector3(36.5, 0, 11.5);

double lengthOf(List<Vector3> path) {
  var total = 0.0;
  for (var i = 1; i < path.length; i++) {
    total += path[i].distanceTo(path[i - 1]);
  }
  return total;
}

NavWalker walkerOn(
  NavGrid grid, {
  List<int>? asked,
  Planner? planner,
  Clear? clear,
  int lookahead = 3,
  Vector3? at,
}) {
  final plan = planner ?? grid.findPath;
  return NavWalker(
    body: Steerable(position: at ?? west, maxSpeed: 5, maxForce: 40),
    planner: (from, to) {
      asked?.add(1);
      return plan(from, to);
    },
    isClear: clear ?? grid.isClear,
    lookahead: lookahead,
  );
}

void main() {
  group('an obstacle', () {
    test('shuts the cells it covers and nothing else', () {
      final grid = walled();

      final door = shutDoor(grid);

      expect(door.standing, isTrue);
      for (var j = 10; j <= 13; j++) {
        expect(grid.isOpen(19, j), isFalse, reason: 'cell 19, $j');
        expect(grid.isOpen(20, j), isFalse, reason: 'cell 20, $j');
      }
      expect(grid.isOpen(18, 11), isTrue);
      expect(grid.isOpen(21, 11), isTrue);
      expect(grid.isOpen(19, 9), isTrue);
      expect(grid.isOpen(19, 14), isTrue);
    });

    test('opens them again when it is lifted', () {
      final grid = walled();
      final door = shutDoor(grid);

      door.lift();

      expect(door.standing, isFalse);
      expect(grid.isOpen(19, 11), isTrue);
      expect(grid.isOpen(20, 11), isTrue);
    });

    test('lifting twice does not open what another one shuts', () {
      final grid = walled();
      final first = shutDoor(grid);
      final second = shutDoor(grid);

      first.lift();
      first.lift();

      expect(grid.isOpen(19, 11), isFalse);
      second.lift();
      expect(grid.isOpen(19, 11), isTrue);
    });

    test('never opens a cell a wall closes', () {
      final grid = walled();
      final over = grid.addObstacleRect(
        minX: 19.5,
        minZ: 8,
        maxX: 20.5,
        maxZ: 11.5,
      );
      expect(grid.isOpen(20, 9), isFalse);

      over.lift();

      expect(grid.isOpen(20, 9), isFalse, reason: 'the wall stays');
      expect(grid.isOpen(20, 10), isTrue);
    });

    test('a circle shuts the cells whose middles are inside it', () {
      final grid = NavGrid(width: 20, depth: 20, cell: 1);

      final crate = grid.addObstacleCircle(10, 10, 1.6);

      expect(grid.isOpen(9, 9), isFalse);
      expect(grid.isOpen(10, 10), isFalse);
      expect(grid.isOpen(8, 9), isFalse, reason: '1.58 from the middle');
      expect(grid.isOpen(11, 10), isFalse);
      expect(grid.isOpen(8, 8), isTrue, reason: 'the corner of a box');
      expect(grid.isOpen(7, 9), isTrue);
      expect(grid.isOpen(11, 12), isTrue);
      crate.lift();
      expect(grid.isOpen(9, 9), isTrue);
    });

    test('moves by lifting it and putting another down', () {
      final grid = NavGrid(width: 20, depth: 20, cell: 1);
      final crate = grid.addObstacleCircle(5, 5, 1);

      crate.lift();
      grid.addObstacleCircle(14, 14, 1);

      expect(grid.isOpen(4, 4), isTrue);
      expect(grid.isOpen(13, 13), isFalse);
    });

    test('sends a route round it, and back through once lifted', () {
      final grid = walled();
      final direct = lengthOf(grid.findPath(west, east)!);

      final door = shutDoor(grid);
      final round = lengthOf(grid.findPath(west, east)!);
      door.lift();
      final again = lengthOf(grid.findPath(west, east)!);

      expect(round, greaterThan(direct + 5), reason: 'over the top');
      expect(again, closeTo(direct, 1e-9));
    });

    test('is closed in an inflated copy, which does not follow a lift', () {
      final grid = walled();
      final door = shutDoor(grid);

      final copy = grid.inflated(1);
      door.lift();

      expect(copy.isOpen(19, 11), isFalse);
      expect(grid.isOpen(19, 11), isTrue);
      expect(copy.isOpen(18, 11), isFalse, reason: 'a cell off the door');
    });
  });

  group('a walker that checks its way', () {
    void run(NavWalker walker, double seconds, {void Function()? each}) {
      for (var t = 0.0; t < seconds && !walker.isArrived; t += 1 / 60) {
        each?.call();
        walker.update(1 / 60);
      }
    }

    test('goes round a door that shuts in front of it with no goTo', () {
      final grid = walled();
      final asked = <int>[];
      final walker = walkerOn(grid, asked: asked)..goTo(east);
      var top = 0.0;
      NavObstacle? door;

      run(
        walker,
        60,
        each: () {
          if (door == null && walker.body.position.x > 12) {
            door = shutDoor(grid);
          }
          if (walker.body.position.z > top) top = walker.body.position.z;
        },
      );

      expect(door, isNotNull);
      expect(walker.isArrived, isTrue);
      expect(walker.body.position.distanceTo(east), lessThan(0.6));
      expect(top, greaterThan(21), reason: 'over the top');
      expect(asked, hasLength(2), reason: 'planned once, then healed once');
    });

    test('plans only once when nothing gets in the way', () {
      final grid = walled();
      final asked = <int>[];
      final walker = walkerOn(grid, asked: asked)..goTo(east);

      run(walker, 60);

      expect(walker.isArrived, isTrue);
      expect(asked, hasLength(1));
    });

    test('walks on through a shut door when it is not checking', () {
      final grid = walled();
      final walker = NavWalker(
        body: Steerable(position: west, maxSpeed: 5, maxForce: 40),
        planner: grid.findPath,
      )..goTo(east);
      shutDoor(grid);

      run(walker, 60);

      expect(walker.isArrived, isTrue);
      expect(walker.route, hasLength(2), reason: 'still the straight route');
    });

    test('stands and keeps its goal when no way is left', () {
      // A wall the whole depth of the field, with a door that then shuts.
      final open = NavGrid(width: 40, depth: 30, cell: 1);
      open.blockRect(minX: 20, minZ: 0, maxX: 20.5, maxZ: 9.5);
      open.blockRect(minX: 20, minZ: 14, maxX: 20.5, maxZ: 29.5);
      final walker = walkerOn(open)..goTo(east);
      expect(walker.hasRoute, isTrue);

      walker.update(1 / 60);
      final door = shutDoor(open);
      walker.update(1 / 60);

      expect(walker.hasRoute, isFalse);
      expect(walker.goal, east);
      final stood = walker.body.position.clone();
      walker.update(1 / 60);
      expect(walker.body.position, stood);

      door.lift();
      walker.goTo(east);
      run(walker, 60);
      expect(walker.isArrived, isTrue);
    });

    test('stands when the planner does not see what the check sees', () {
      final grid = walled();
      final stale = grid.inflated(0.1);
      final asked = <int>[];
      final walker = walkerOn(grid, asked: asked, planner: stale.findPath)
        ..goTo(east);
      shutDoor(grid);

      walker.update(1 / 60);
      walker.update(1 / 60);

      expect(walker.hasRoute, isFalse);
      expect(asked, hasLength(2), reason: 'not planned again every frame');
    });

    test('looks only as many legs ahead as it is told to', () {
      final route = [
        Vector3(0, 0, 0),
        Vector3(10, 0, 0),
        Vector3(10, 0, 10),
        Vector3(0, 0, 10),
      ];
      // The third leg from the body is the one from route[1] to route[2].
      bool clear(Vector3 from, Vector3 to) =>
          !(from.distanceTo(route[1]) < 1e-9 && to.distanceTo(route[2]) < 1e-9);
      NavWalker make(int lookahead, List<int> asked) => NavWalker(
        body: Steerable(position: route[0].clone(), maxSpeed: 5, maxForce: 40),
        planner: (_, _) {
          asked.add(1);
          return route;
        },
        isClear: clear,
        lookahead: lookahead,
      );
      final nearAsked = <int>[];
      final farAsked = <int>[];
      final near = make(2, nearAsked)..goTo(route[3]);
      final far = make(3, farAsked)..goTo(route[3]);

      near.update(1 / 60);
      far.update(1 / 60);

      expect(near.hasRoute, isTrue);
      expect(nearAsked, hasLength(1));
      expect(far.hasRoute, isFalse, reason: 'shut again after planning anew');
      expect(farAsked, hasLength(2));
    });

    test('refuses a lookahead of nothing', () {
      NavWalker make(int lookahead) => NavWalker(
        body: Steerable(),
        planner: (_, _) => null,
        lookahead: lookahead,
      );

      expect(() => make(0), throwsArgumentError);
      expect(() => make(-1), throwsArgumentError);
      expect(make(1), isA<NavWalker>());
    });
  });
}
