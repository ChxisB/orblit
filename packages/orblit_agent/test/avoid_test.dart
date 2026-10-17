import 'dart:math' as math;

import 'package:orblit_agent/orblit_agent.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

Steerable walker(
  double x,
  double z, {
  double vx = 0,
  int priority = 0,
  double speed = 2,
}) => Steerable(
  position: Vector3(x, 0, z),
  velocity: Vector3(vx, 0, 0),
  maxSpeed: speed,
  maxForce: 20,
  radius: 0.4,
  priority: priority,
);

/// The velocity an [Avoid] settles on for [self] wanting to go [wanted].
Vector3 chosen(Steerable self, Vector3 wanted, List<Steerable> others) =>
    Avoid(wanted, others).force(self) + self.velocity;

void main() {
  group('avoidance', () {
    test('goes where the route says when nothing is near', () {
      final self = walker(0, 0, vx: 1);
      final far = walker(40, 0, vx: -1);

      final force = Avoid(Vector3(2, 0, 0), [far]).force(self);

      expect(force.x, closeTo(1, 1e-9));
      expect(force.z, closeTo(0, 1e-9));
    });

    test('caps what the route wants at the top speed', () {
      final self = walker(0, 0, speed: 2);

      final force = Avoid(Vector3(10, 0, 0), const []).force(self);

      expect(force.length, closeTo(2, 1e-9));
    });

    test('is not troubled by itself', () {
      final self = walker(0, 0);

      final force = Avoid(Vector3(2, 0, 0), [self]).force(self);

      expect(force.x, closeTo(2, 1e-9));
    });

    test('ignores a neighbour that is moving away', () {
      final self = walker(0, 0, vx: 2);
      final ahead = walker(1.5, 0, vx: 2);

      final velocity = chosen(self, Vector3(2, 0, 0), [ahead]);

      expect(velocity.x, closeTo(2, 1e-9));
      expect(velocity.z, closeTo(0, 1e-9));
    });

    test('turns off the line to miss someone coming the other way', () {
      final self = walker(0, 0, vx: 2);
      final oncoming = walker(6, 0, vx: -2);

      final velocity = chosen(self, Vector3(2, 0, 0), [oncoming]);

      expect(velocity.z.abs(), greaterThan(0.1));
      expect(velocity.x, greaterThan(0), reason: 'still making headway');
    });

    test('two meeting head on pass on opposite sides', () {
      final a = walker(0, 0, vx: 2);
      final b = walker(6, 0, vx: -2);

      final va = chosen(a, Vector3(2, 0, 0), [b]);
      final vb = chosen(b, Vector3(-2, 0, 0), [a]);

      expect(va.z, lessThan(0), reason: 'positive turns first');
      expect(vb.z, greaterThan(0));
    });

    test('only looks as far as anyone could get within the horizon', () {
      final self = walker(0, 0, vx: 2);
      // Two a second each way for two seconds is 8, and the radii add 0.8.
      final inReach = walker(8.5, 0)..velocity = Vector3(-10, 0, 0);
      final beyond = walker(9, 0)..velocity = Vector3(-10, 0, 0);

      final near = chosen(self, Vector3(2, 0, 0), [inReach]);
      final far = chosen(self, Vector3(2, 0, 0), [beyond]);

      expect(near.z.abs(), greaterThan(0.1));
      expect(far.z, closeTo(0, 1e-9));
    });

    test('gives the same answer every time it is asked', () {
      final self = walker(0, 0, vx: 2);
      final others = [walker(5, 0.1, vx: -2), walker(4, -1, vx: -1)];

      final first = chosen(self, Vector3(2, 0, 0), others);
      final second = chosen(self, Vector3(2, 0, 0), others);

      expect(second, first);
    });

    test('does not step round someone of lower priority', () {
      final self = walker(0, 0, vx: 2, priority: 1);
      final lesser = walker(6, 0, vx: -2);

      final velocity = chosen(self, Vector3(2, 0, 0), [lesser]);

      expect(velocity.x, closeTo(2, 1e-9));
      expect(velocity.z, closeTo(0, 1e-9));
    });

    test('steps round someone of higher priority', () {
      final self = walker(0, 0, vx: 2);
      final greater = walker(6, 0, vx: -2, priority: 1);

      final velocity = chosen(self, Vector3(2, 0, 0), [greater]);

      expect(velocity.z.abs(), greaterThan(0.1));
    });

    test('backs off from someone it already overlaps and is closing on', () {
      final self = walker(0, 0, vx: 1);
      final inside = walker(0.5, 0);

      final velocity = chosen(self, Vector3(2, 0, 0), [inside]);

      final closing = (inside.position - self.position).dot(velocity);
      expect(closing, lessThanOrEqualTo(0));
    });

    test('stops for a wall of neighbours it cannot turn round', () {
      final self = walker(0, 0, vx: 2);
      final wall = [
        for (var z = -6.0; z <= 6; z += 0.8) walker(1.2, z, speed: 0),
      ];

      final velocity = chosen(self, Vector3(2, 0, 0), wall);

      expect(velocity.length, lessThan(1));
    });

    test('goes back when no turn of it gets clear', () {
      // One turn to try is straight back. Stopping would be run into inside
      // a second, and the oncoming walker is as fast, so going back is the
      // only choice that is not hit.
      final self = walker(0, 0, vx: 2);
      final oncoming = walker(2.5, 0, vx: -2);

      final velocity =
          Avoid(Vector3(2, 0, 0), [oncoming], turns: 1).force(self) +
          self.velocity;

      expect(velocity.x, closeTo(-2, 1e-9));
      expect(velocity.z, closeTo(0, 1e-9));
    });

    test('refuses what cannot work', () {
      Avoid make({double horizon = 2, int turns = 8, double weight = 2}) =>
          Avoid(
            Vector3.zero(),
            const [],
            horizon: horizon,
            turns: turns,
            weight: weight,
          );

      expect(() => make(horizon: 0), throwsArgumentError);
      expect(() => make(horizon: double.infinity), throwsArgumentError);
      expect(() => make(horizon: double.nan), throwsArgumentError);
      expect(() => make(turns: 0), throwsArgumentError);
      expect(() => make(weight: -1), throwsArgumentError);
      expect(() => make(weight: double.nan), throwsArgumentError);
      expect(() => make(weight: double.infinity), throwsArgumentError);
      expect(make(weight: 0), isA<Avoid>());
      expect(make(turns: 1), isA<Avoid>());
    });
  });

  group('a crowd', () {
    /// Fifty walkers a side crossing a plaza in opposite directions, each
    /// starting in its own slot of a grid so none starts touching. The lanes
    /// of the two sides are two metres apart each and interleaved, so a lane
    /// is a metre wide for a walker 0.8 wide. Packed tighter than that, a
    /// head on crowd jams and overlaps by half a metre or more.
    List<({Steerable agent, Vector3 goal})> plaza() {
      final crowd = <({Steerable agent, Vector3 goal})>[];
      for (var i = 0; i < 50; i++) {
        final row = i % 10;
        final column = i ~/ 10;
        final z = row * 2.0 - 9;
        final eastbound = Steerable(
          position: Vector3(-12 - column * 1.1, 0, z),
          maxSpeed: 2,
          maxForce: 20,
          radius: 0.4,
        );
        final westbound = Steerable(
          position: Vector3(12 + column * 1.1, 0, z + 1.0),
          maxSpeed: 2,
          maxForce: 20,
          radius: 0.4,
        );
        crowd.add((agent: eastbound, goal: Vector3(24, 0, z)));
        crowd.add((agent: westbound, goal: Vector3(-24, 0, z + 1.0)));
      }
      return crowd;
    }

    /// Walks the crowd [seconds] in the order it is listed. Returns the longest
    /// time any pair stayed overlapping by more than [depth] metres.
    ({double longest, List<Vector3> end}) cross(
      List<({Steerable agent, Vector3 goal})> crowd, {
      double seconds = 40,
      double depth = 0.1,
    }) {
      const step = 1 / 20;
      final agents = [for (final entry in crowd) entry.agent];
      final together = <int, double>{};
      var longest = 0.0;
      for (var t = 0.0; t < seconds; t += step) {
        for (final entry in crowd) {
          final toGoal = entry.goal - entry.agent.position;
          final wanted = toGoal.length < 0.1
              ? Vector3.zero()
              : toGoal.normalized() * entry.agent.maxSpeed;
          entry.agent.integrate(Avoid(wanted, agents).force(entry.agent), step);
        }
        longest = math.max(longest, _overlaps(agents, together, step, depth));
      }
      return (
        longest: longest,
        end: [for (final a in agents) a.position.clone()],
      );
    }

    test('crosses without walking through itself', () {
      final crowd = plaza();

      final result = cross(crowd);

      expect(result.longest, lessThanOrEqualTo(1));
      // Five to a lane want one point, so the last of them queues behind the
      // first. All that is asked is that each got past the far side's start.
      for (final entry in crowd) {
        expect(
          entry.goal.x.sign * entry.agent.position.x,
          greaterThan(17),
          reason: 'got across the plaza',
        );
      }
    });

    test('repeats exactly', () {
      final first = cross(plaza(), seconds: 12).end;
      final second = cross(plaza(), seconds: 12).end;

      expect(second, first);
    });

    test('walks through itself when nobody avoids', () {
      final crowd = plaza();
      final agents = [for (final entry in crowd) entry.agent];
      final together = <int, double>{};
      var longest = 0.0;
      for (var t = 0.0; t < 40; t += 1 / 20) {
        for (final entry in crowd) {
          final toGoal = entry.goal - entry.agent.position;
          final wanted = toGoal.length < 0.1
              ? Vector3.zero()
              : toGoal.normalized() * 2;
          entry.agent.integrate(wanted - entry.agent.velocity, 1 / 20);
        }
        longest = math.max(longest, _overlaps(agents, together, 1 / 20, 0.1));
      }

      expect(longest, greaterThan(1));
    });
  });
}

/// Adds [step] to each pair overlapping by more than [depth], clears the rest,
/// and returns the longest run of any pair.
double _overlaps(
  List<Steerable> agents,
  Map<int, double> together,
  double step,
  double depth,
) {
  var longest = 0.0;
  for (var i = 0; i < agents.length; i++) {
    for (var j = i + 1; j < agents.length; j++) {
      final gap =
          agents[i].radius +
          agents[j].radius -
          agents[i].position.distanceTo(agents[j].position);
      final key = i * agents.length + j;
      if (gap > depth) {
        together[key] = (together[key] ?? 0) + step;
        longest = math.max(longest, together[key]!);
      } else {
        together.remove(key);
      }
    }
  }
  return longest;
}
