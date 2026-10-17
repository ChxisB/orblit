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

/// The ground length of a whole route, with a link counting for its own cost.
double costOf(List<RouteStep> route) {
  var total = 0.0;
  for (final step in route) {
    total += switch (step) {
      WalkStep(:final points) => lengthOf(points),
      LinkStep(:final link) => link.cost,
    };
  }
  return total;
}

/// A 20 by 20 field with a wall at x 10 to 11 from z 0 up to 15, so the way
/// round is over the top.
NavGrid walled() {
  final grid = NavGrid(width: 20, depth: 20, cell: 1);
  grid.blockRect(minX: 10, minZ: 0, maxX: 10.5, maxZ: 14.5);
  return grid;
}

/// Three rooms with no way between them over the ground.
NavGrid rooms() {
  final grid = NavGrid(width: 20, depth: 20, cell: 1);
  grid.blockRect(minX: 6, minZ: 0, maxX: 6.5, maxZ: 19.5);
  grid.blockRect(minX: 13, minZ: 0, maxX: 13.5, maxZ: 19.5);
  return grid;
}

final west = Vector3(3.5, 0, 2.5);
final east = Vector3(16.5, 0, 2.5);

/// A jump across the wall of [walled], low down where the way round is long.
NavLink jump({
  double cost = 0,
  Set<String> agents = const {},
  String? gate,
  bool twoWay = false,
}) => NavLink(
  from: Vector3(8.5, 0, 2.5),
  to: Vector3(12.5, 0, 2.5),
  kind: 'jump',
  cost: cost,
  agents: agents,
  gate: gate,
  twoWay: twoWay,
);

void main() {
  group('a link planner', () {
    test('crosses a gap by a link instead of going round', () {
      final grid = walled();
      final planner = LinkPlanner(ground: grid.findPath, links: [jump()]);

      final route = planner.findRoute(west, east)!;

      expect(route, hasLength(3));
      final before = route[0] as WalkStep;
      final cross = route[1] as LinkStep;
      final after = route[2] as WalkStep;
      expect(cross.link.kind, 'jump');
      expect(cross.reversed, isFalse);
      expect(before.points.last.distanceTo(cross.start), lessThan(0.01));
      expect(after.points.first.distanceTo(cross.end), lessThan(0.01));
      final round = lengthOf(grid.findPath(west, east)!);
      expect(costOf(route), lessThan(round - 20));
    });

    test('goes the long way for an agent kind that may not use it', () {
      final grid = walled();
      final planner = LinkPlanner(
        ground: grid.findPath,
        links: [
          jump(agents: {'climber'}),
        ],
      );
      final round = grid.findPath(west, east)!;

      for (final agent in ['walker', '']) {
        final route = planner.findRoute(west, east, agent: agent)!;
        expect(route, hasLength(1), reason: 'agent "$agent"');
        expect((route.single as WalkStep).points, hasLength(round.length));
        expect(costOf(route), closeTo(lengthOf(round), 1e-9));
      }
      final climbs = planner.findRoute(west, east, agent: 'climber')!;
      expect(climbs.whereType<LinkStep>(), hasLength(1));
    });

    test('skips a locked door and uses it once it is unlocked', () {
      final planner = LinkPlanner(
        ground: walled().findPath,
        links: [jump(gate: 'door')],
      );

      planner.lock('door');
      expect(planner.isLocked('door'), isTrue);
      final shut = planner.findRoute(west, east)!;
      expect(shut.whereType<LinkStep>(), isEmpty);

      planner.unlock('door');
      expect(planner.isLocked('door'), isFalse);
      final open = planner.findRoute(west, east)!;
      expect(open.whereType<LinkStep>(), hasLength(1));
      expect(costOf(open), lessThan(costOf(shut)));
    });

    test('a lock on one gate leaves a link on another open', () {
      final planner = LinkPlanner(
        ground: walled().findPath,
        links: [
          jump(gate: 'door'),
          jump(),
        ],
      );

      planner.lock('door');

      final route = planner.findRoute(west, east)!;
      expect(route.whereType<LinkStep>(), hasLength(1));
      expect((route[1] as LinkStep).link.gate, isNull);
    });

    test('uses a one way link only the way it goes', () {
      final grid = walled();
      final back = LinkPlanner(ground: grid.findPath, links: [jump()]);
      final both = LinkPlanner(
        ground: grid.findPath,
        links: [jump(twoWay: true)],
      );

      expect(back.findRoute(east, west)!.whereType<LinkStep>(), isEmpty);

      final route = both.findRoute(east, west)!;
      final cross = route.whereType<LinkStep>().single;
      expect(cross.reversed, isTrue);
      expect(cross.start, cross.link.to);
      expect(cross.end, cross.link.from);
    });

    test('takes a link only while it costs less than walking', () {
      final grid = walled();
      final free = costOf(
        LinkPlanner(
          ground: grid.findPath,
          links: [jump()],
        ).findRoute(west, east)!,
      );
      final round = lengthOf(grid.findPath(west, east)!);
      final margin = round - free;

      LinkPlanner with_(double cost) => LinkPlanner(
        ground: grid.findPath,
        links: [jump(cost: cost)],
      );

      expect(
        with_(margin - 1).findRoute(west, east)!.whereType<LinkStep>(),
        hasLength(1),
      );
      expect(
        with_(margin + 1).findRoute(west, east)!.whereType<LinkStep>(),
        isEmpty,
      );
    });

    test('walks when a link costs exactly what walking does', () {
      // A straight line for ground, so the tie is exact.
      List<Vector3>? line(Vector3 a, Vector3 b) => [a, b];
      final planner = LinkPlanner(
        ground: line,
        links: [
          NavLink(
            from: Vector3.zero(),
            to: Vector3(10, 0, 0),
            kind: 'jump',
            cost: 10,
          ),
        ],
      );

      final route = planner.findRoute(Vector3.zero(), Vector3(10, 0, 0))!;

      expect(route.single, isA<WalkStep>());
    });

    test('chains links between rooms that have no ground between them', () {
      final grid = rooms();
      final first = NavLink(
        from: Vector3(5.5, 0, 10.5),
        to: Vector3(7.5, 0, 10.5),
        kind: 'door',
      );
      final second = NavLink(
        from: Vector3(12.5, 0, 4.5),
        to: Vector3(14.5, 0, 4.5),
        kind: 'ladder',
        cost: 3,
      );
      expect(grid.findPath(west, east), isNull);

      final route = LinkPlanner(
        ground: grid.findPath,
        links: [second, first],
      ).findRoute(west, east)!;

      expect(route.map((step) => step.runtimeType), [
        WalkStep,
        LinkStep,
        WalkStep,
        LinkStep,
        WalkStep,
      ]);
      expect((route[1] as LinkStep).link, same(first));
      expect((route[3] as LinkStep).link, same(second));
    });

    test('has no route when the links do not join the ground up', () {
      final grid = rooms();
      final planner = LinkPlanner(
        ground: grid.findPath,
        links: [
          NavLink(
            from: Vector3(5.5, 0, 10.5),
            to: Vector3(7.5, 0, 10.5),
            kind: 'door',
          ),
        ],
      );

      expect(planner.findRoute(west, east), isNull);
      expect(planner.findRoute(west, Vector3(9.5, 0, 2.5)), isNotNull);
    });

    test('starting at the foot of a link begins with the crossing', () {
      final planner = LinkPlanner(ground: walled().findPath, links: [jump()]);

      final route = planner.findRoute(jump().from, east)!;

      expect(route.first, isA<LinkStep>());
      expect(route, hasLength(2));
    });

    test('a start that is the goal is one walk', () {
      final planner = LinkPlanner(ground: walled().findPath, links: [jump()]);

      final route = planner.findRoute(west, west)!;

      expect(route, hasLength(1));
      expect(route.single, isA<WalkStep>());
    });

    test('is a plain ground route when there are no links', () {
      final grid = walled();
      final route = LinkPlanner(
        ground: grid.findPath,
        links: const [],
      ).findRoute(west, east)!;

      final expected = grid.findPath(west, east)!;
      expect((route.single as WalkStep).points, hasLength(expected.length));
    });

    test('works over a navmesh as well as a grid', () {
      final mesh = NavMesh.fromGrid(walled());
      final planner = LinkPlanner(ground: mesh.findPath, links: [jump()]);

      final route = planner.findRoute(west, east)!;

      expect(route.whereType<LinkStep>(), hasLength(1));
    });

    test('the same ask twice gives the same route', () {
      final planner = LinkPlanner(ground: walled().findPath, links: [jump()]);

      final once = planner.findRoute(west, east)!;
      final twice = planner.findRoute(west, east)!;

      expect(twice.length, once.length);
      expect(costOf(twice), costOf(once));
    });
  });

  group('a link', () {
    test('keeps its own copy of its ends', () {
      final from = Vector3(1, 2, 3);
      final to = Vector3(4, 5, 6);
      final link = NavLink(from: from, to: to, kind: 'jump');

      from.x = 9;
      to.x = 9;

      expect(link.from.x, 1);
      expect(link.to.x, 4);
    });

    test('refuses what cannot work', () {
      NavLink make({String kind = 'jump', double cost = 0}) => NavLink(
        from: Vector3.zero(),
        to: Vector3.zero(),
        kind: kind,
        cost: cost,
      );

      expect(() => make(kind: ''), throwsArgumentError);
      expect(() => make(cost: -1), throwsArgumentError);
      expect(() => make(cost: double.nan), throwsArgumentError);
      expect(() => make(cost: double.infinity), throwsArgumentError);
      expect(make(cost: 0).cost, 0);
    });
  });
}
