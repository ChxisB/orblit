import 'package:orblit_agent/orblit_agent.dart';
import 'package:orblit_path/orblit_path.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A 40 by 30 field with a wall at x 20 from z 0 up to 22, so the way
/// round is at the top.
NavGrid field() {
  final grid = NavGrid(width: 40, depth: 30, cell: 1);
  grid.blockRect(minX: 20, minZ: 0, maxX: 20.5, maxZ: 21.5);
  return grid;
}

Steerable walkerBody(Vector3 at) =>
    Steerable(position: at, maxSpeed: 5, maxForce: 40);

/// Runs [walker] for up to [seconds] and answers every place it stood.
List<Vector3> walk(NavWalker walker, double seconds) {
  final trail = <Vector3>[walker.body.position.clone()];
  for (var t = 0.0; t < seconds && !walker.isArrived; t += 1 / 60) {
    walker.update(1 / 60);
    trail.add(walker.body.position.clone());
  }
  return trail;
}

void main() {
  final grid = field();

  group('walking a route', () {
    test('an agent walks round a wall to a target', () {
      final safe = grid.inflated(1);
      final walker = NavWalker(
        body: walkerBody(Vector3(3.5, 0, 5.5)),
        planner: safe.findPath,
      )..goTo(Vector3(36.5, 0, 5.5));
      expect(walker.hasRoute, isTrue);
      final trail = walk(walker, 60);
      expect(walker.isArrived, isTrue);
      expect(
        walker.body.position.distanceTo(Vector3(36.5, 0, 5.5)),
        lessThan(0.6),
      );
      expect(
        trail.map((p) => p.z).reduce((a, b) => a > b ? a : b),
        greaterThan(21),
      );
      for (final place in trail) {
        final cell = grid.cellAt(place.x, place.z)!;
        expect(grid.isOpen(cell.i, cell.j), isTrue, reason: '$place');
      }
    });

    test('the same on a navmesh', () {
      final mesh = NavMesh.fromGrid(grid, radius: 1);
      final walker = NavWalker(
        body: walkerBody(Vector3(3.5, 0, 5.5)),
        planner: mesh.findPath,
      )..goTo(Vector3(36.5, 0, 5.5));
      final trail = walk(walker, 60);
      expect(walker.isArrived, isTrue);
      for (final place in trail) {
        final cell = grid.cellAt(place.x, place.z)!;
        expect(grid.isOpen(cell.i, cell.j), isTrue, reason: '$place');
      }
    });

    test('with no route it stays where it is', () {
      final closed = NavGrid(width: 10, depth: 10, cell: 1)
        ..blockRect(minX: 0, minZ: 4.5, maxX: 10, maxZ: 5.5);
      final walker = NavWalker(
        body: walkerBody(Vector3(5, 0, 1)),
        planner: closed.findPath,
      )..goTo(Vector3(5, 0, 8));
      expect(walker.hasRoute, isFalse);
      walker.update(1);
      expect(walker.body.position.z, 1);
      expect(walker.isArrived, isFalse);
    });

    test('is not arrived before it has set out', () {
      final walker = NavWalker(
        body: walkerBody(Vector3(3.5, 0, 5.5)),
        planner: grid.findPath,
      );
      expect(walker.isArrived, isFalse);
      expect(walker.goal, isNull);
    });

    test('stop forgets the route and the goal', () {
      final walker = NavWalker(
        body: walkerBody(Vector3(3.5, 0, 5.5)),
        planner: grid.findPath,
      )..goTo(Vector3(36.5, 0, 5.5));
      expect(walker.route, isNotEmpty);
      expect(walker.goal!.x, 36.5);
      walker.stop();
      expect(walker.hasRoute, isFalse);
      expect(walker.goal, isNull);
    });

    test('plans again from where it is when told a new goal', () {
      final walker = NavWalker(
        body: walkerBody(Vector3(3.5, 0, 5.5)),
        planner: grid.inflated(1).findPath,
      )..goTo(Vector3(36.5, 0, 5.5));
      for (var i = 0; i < 120; i++) {
        walker.update(1 / 60);
      }
      final here = walker.body.position.clone();
      walker.goTo(Vector3(3.5, 0, 25.5));
      expect(walker.route.first.distanceTo(here), lessThan(1e-6));
      expect(walker.route.last.z, closeTo(25.5, 1e-6));
    });
  });
}
