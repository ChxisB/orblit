import 'package:orblit_path/orblit_path.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  final line = Spline.line([Vector3(0, 0, 0), Vector3(10, 0, 0)]);

  group('PathFollower', () {
    test('rides at its speed', () {
      final rider = PathFollower(line, speed: 2);
      rider.advance(1.5);
      expect(rider.distance, closeTo(3, 1e-9));
      expect(rider.frame.position.x, closeTo(3, 1e-9));
    });

    test('stops at the end and says so', () {
      final rider = PathFollower(line, speed: 4);
      rider.advance(10);
      expect(rider.distance, 10);
      expect(rider.isFinished, isTrue);
    });

    test('loops back to the start', () {
      final rider = PathFollower(line, speed: 4, end: PathEnd.loop);
      rider.advance(3);
      expect(rider.distance, closeTo(2, 1e-9));
      expect(rider.isFinished, isFalse);
    });

    test('bounces off the ends', () {
      final rider = PathFollower(line, speed: 4, end: PathEnd.pingPong);
      rider.advance(3);
      expect(rider.distance, closeTo(8, 1e-9));
      rider.advance(1);
      expect(rider.distance, closeTo(4, 1e-9));
      rider.advance(2);
      expect(rider.distance, closeTo(4, 1e-9), reason: '4 down then 4 up');
    });

    test('a negative speed runs it backwards', () {
      final rider = PathFollower(line, speed: -2, distance: 6);
      rider.advance(1);
      expect(rider.distance, closeTo(4, 1e-9));
      rider.advance(10);
      expect(rider.distance, 0);
      expect(rider.isFinished, isTrue);
    });

    test('ignores no time and negative time', () {
      final rider = PathFollower(line, speed: 4);
      rider.advance(0);
      rider.advance(-1);
      expect(rider.distance, 0);
    });
  });

  group('Rail', () {
    test('slides to the nearest point of the line', () {
      final rail = Rail(line, speed: 100);
      rail.track(Vector3(7, 5, 2), 1);
      expect(rail.distance, closeTo(7, 1e-6));
      expect(rail.position.x, closeTo(7, 1e-6));
    });

    test('takes its speed to get there', () {
      final rail = Rail(line, speed: 2);
      rail.track(Vector3(9, 0, 0), 1);
      expect(rail.distance, closeTo(2, 1e-9));
      rail.track(Vector3(9, 0, 0), 1);
      expect(rail.distance, closeTo(4, 1e-9));
    });

    test('goes the short way round a closed loop', () {
      final ring = Spline.line([
        Vector3(0, 0, 0),
        Vector3(10, 0, 0),
        Vector3(10, 0, 10),
        Vector3(0, 0, 10),
      ], closed: true);
      final rail = Rail(ring, speed: 3, distance: 1);
      rail.track(Vector3(0, 0, 8), 1);
      // From 1 the target is at 32, which is 9 the other way round.
      expect(rail.position.x, closeTo(0, 1e-6));
      expect(rail.position.z, closeTo(2, 1e-6));
    });
  });

  group('Patrol', () {
    final stops = [
      PatrolStop(Vector3(0, 0, 0)),
      PatrolStop(Vector3(10, 0, 0), wait: 2),
      PatrolStop(Vector3(10, 0, 10)),
    ];

    test('heads for the first stop', () {
      final patrol = Patrol(stops);
      expect(patrol.target!.x, 0);
    });

    test('waits at a stop, then moves on', () {
      final patrol = Patrol(stops, end: PathEnd.stop);
      patrol.update(Vector3(0, 0, 0), 0.1);
      expect(patrol.target!.x, 10);
      patrol.update(Vector3(10, 0, 0), 1);
      expect(patrol.isWaiting, isTrue);
      expect(patrol.target!.z, 0);
      patrol.update(Vector3(10, 0, 0), 1.5);
      expect(patrol.target!.z, 10);
    });

    test('does not count the wait while away', () {
      final patrol = Patrol(stops, end: PathEnd.stop)
        ..update(Vector3(0, 0, 0), 0);
      patrol.update(Vector3(10, 0, 0), 1.5);
      patrol.update(Vector3(5, 0, 0), 1);
      patrol.update(Vector3(10, 0, 0), 1.5);
      expect(patrol.target!.z, 0, reason: 'the wait started over');
    });

    test('ends, loops or bounces after the last stop', () {
      final ends = Patrol(stops, end: PathEnd.stop);
      final loops = Patrol(stops);
      final bounces = Patrol(stops, end: PathEnd.pingPong);
      for (final patrol in [ends, loops, bounces]) {
        for (final stop in [...stops, ...stops.reversed.skip(1)]) {
          patrol.update(stop.at, stop.wait + 0.1);
        }
      }
      expect(ends.isFinished, isTrue);
      expect(ends.target, isNull);
      expect(loops.isFinished, isFalse);
      expect(bounces.isFinished, isFalse);
    });

    test('a loop goes back to the first', () {
      final patrol = Patrol(stops);
      for (final stop in stops) {
        patrol.update(stop.at, stop.wait + 0.1);
      }
      expect(patrol.target!.x, 0);
    });

    test('a bounce comes back through the stops', () {
      final patrol = Patrol(stops, end: PathEnd.pingPong);
      for (final stop in stops) {
        patrol.update(stop.at, stop.wait + 0.1);
      }
      expect(patrol.target!.z, 0);
      expect(patrol.target!.x, 10);
    });

    test('needs a stop', () {
      expect(() => Patrol([]), throwsArgumentError);
    });
  });
}
