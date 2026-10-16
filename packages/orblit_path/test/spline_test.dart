import 'dart:math' as math;

import 'package:orblit_path/orblit_path.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

void main() {
  group('length and position', () {
    test('a line between two points is as long as the gap', () {
      final line = Spline.line([Vector3(0, 0, 0), Vector3(10, 0, 0)]);
      expect(line.length, closeTo(10, 1e-9));
      expect(line.positionAt(5).x, closeTo(5, 1e-9));
    });

    test('a closed square is four sides long and wraps', () {
      final square = Spline.line([
        Vector3(0, 0, 0),
        Vector3(2, 0, 0),
        Vector3(2, 0, 2),
        Vector3(0, 0, 2),
      ], closed: true);
      expect(square.length, closeTo(8, 1e-9));
      expect(
        (square.positionAt(9) - square.positionAt(1)).length,
        lessThan(1e-9),
      );
      expect(
        (square.positionAt(-1) - square.positionAt(7)).length,
        lessThan(1e-9),
      );
    });

    test('an open curve holds at its ends', () {
      final line = Spline.line([Vector3(0, 0, 0), Vector3(4, 0, 0)]);
      expect(line.positionAt(-3).x, 0);
      expect(line.positionAt(40).x, closeTo(4, 1e-9));
    });

    test('a curve through points passes through them', () {
      final points = [
        Vector3(0, 0, 0),
        Vector3(3, 1, 0),
        Vector3(5, 0, 4),
        Vector3(1, 2, 6),
      ];
      final curve = Spline.through(points);
      for (final point in points) {
        expect(curve.closest(point).offset, lessThan(0.02));
      }
    });

    test('distance is metres however the points are spaced', () {
      final curve = Spline.through([
        Vector3(0, 0, 0),
        Vector3(1, 0, 0),
        Vector3(2, 0, 1),
        Vector3(20, 0, 1),
        Vector3(21, 0, 5),
      ]);
      const steps = 120;
      final step = curve.length / steps;
      final gaps = [
        for (var i = 0; i < steps; i++)
          (curve.positionAt((i + 1) * step) - curve.positionAt(i * step))
              .length,
      ];
      for (final gap in gaps) {
        expect(gap, closeTo(step, step * 0.05));
      }
    });

    test('a bezier with handles is longer than its chord', () {
      final bent = Spline.bezier([
        PathKnot(Vector3(0, 0, 0), handleOut: Vector3(0, 0, 4)),
        PathKnot(Vector3(10, 0, 0), handleIn: Vector3(0, 0, 4)),
      ]);
      expect(bent.length, greaterThan(10.5));
      expect(bent.positionAt(bent.length / 2).z, greaterThan(2));
    });

    test('needs two knots', () {
      expect(() => Spline.line([Vector3.zero()]), throwsArgumentError);
    });

    test('refuses a knot that is not finite', () {
      expect(() => PathKnot(Vector3(double.nan, 0, 0)), throwsArgumentError);
      expect(
        () => PathKnot(Vector3.zero(), tilt: double.infinity),
        throwsArgumentError,
      );
    });

    test('needs a tilt for each point or none', () {
      expect(
        () => Spline.line([Vector3.zero(), Vector3(1, 0, 0)], tilts: [0]),
        throwsArgumentError,
      );
    });
  });

  group('frames', () {
    test('forward follows the line and up is world up', () {
      final line = Spline.line([Vector3(0, 0, 0), Vector3(0, 0, -10)]);
      final frame = line.frameAt(4);
      expect(frame.forward.z, closeTo(-1, 1e-9));
      expect(frame.up.y, closeTo(1, 1e-9));
      expect(frame.right.x, closeTo(1, 1e-9));
    });

    test('up stays square to forward on a climb', () {
      final climb = Spline.line([Vector3(0, 0, 0), Vector3(10, 10, 0)]);
      final frame = climb.frameAt(3);
      expect(frame.forward.dot(frame.up), closeTo(0, 1e-9));
      expect(frame.up.y, greaterThan(0.7));
    });

    test('tilt rolls up towards the right and eases between knots', () {
      final bank = Spline.line(
        [Vector3(0, 0, 0), Vector3(0, 0, -10)],
        tilts: [0, math.pi / 2],
      );
      expect(bank.frameAt(0).up.y, closeTo(1, 1e-9));
      final end = bank.frameAt(10);
      expect(end.up.y, closeTo(0, 1e-9));
      expect(end.up.x, closeTo(1, 1e-9));
      final half = bank.frameAt(5);
      expect(half.up.y, closeTo(math.cos(math.pi / 4), 1e-3));
      expect(half.up.length, closeTo(1, 1e-9));
    });

    test('a path straight up still has an up', () {
      final rise = Spline.line([Vector3(0, 0, 0), Vector3(0, 8, 0)]);
      final frame = rise.frameAt(4);
      expect(frame.up.length, closeTo(1, 1e-9));
      expect(frame.forward.dot(frame.up), closeTo(0, 1e-9));
    });
  });

  group('closest', () {
    test('finds the foot of the perpendicular and the way off', () {
      final line = Spline.line([Vector3(0, 0, 0), Vector3(10, 0, 0)]);
      final near = line.closest(Vector3(6, 3, 0));
      expect(near.distance, closeTo(6, 1e-6));
      expect(near.offset, closeTo(3, 1e-6));
      expect(near.position.x, closeTo(6, 1e-6));
    });

    test('clamps to the end of an open path', () {
      final line = Spline.line([Vector3(0, 0, 0), Vector3(10, 0, 0)]);
      expect(line.closest(Vector3(40, 0, 0)).distance, closeTo(10, 1e-9));
      expect(line.closest(Vector3(-4, 0, 0)).distance, closeTo(0, 1e-9));
    });
  });

  group('the file', () {
    final path = Spline.bezier([
      PathKnot(Vector3(0, 0, 0), tilt: 0.25, handleOut: Vector3(1, 0, 0)),
      PathKnot(Vector3(5, 1, 2), handleIn: Vector3(-1, 0, 0)),
      PathKnot(Vector3(9, 0, 0)),
    ], closed: true);

    test('round trips', () {
      final back = Spline.decode(path.encode());
      expect(back.closed, isTrue);
      expect(back.knots, hasLength(3));
      expect(back.knots[0].tilt, 0.25);
      expect(back.knots[0].handleOut.x, 1);
      expect(back.knots[1].handleIn.x, -1);
      expect(back.length, closeTo(path.length, 1e-9));
    });

    test('puts a knot on each line', () {
      final lines = path.encode().split('\n');
      expect(lines.where((line) => line.contains('"at"')), hasLength(3));
    });

    test('names its kind and version', () {
      expect(path.toJson()['kind'], Spline.marker);
      expect(path.toJson()['formatVersion'], Spline.formatVersion);
    });

    test('refuses what is not a path', () {
      expect(
        () => Spline.decode('{"kind":"orblit.terrain"}'),
        throwsA(isA<PathFormatException>()),
      );
      expect(() => Spline.decode('nope'), throwsA(isA<PathFormatException>()));
    });

    test('refuses a newer version', () {
      expect(
        () => Spline.decode(
          '{"kind":"orblit.path","formatVersion":99,"knots":[]}',
        ),
        throwsA(isA<PathFormatException>()),
      );
    });

    test('refuses a knot that is not numbers', () {
      expect(
        () => Spline.decode(
          '{"kind":"orblit.path","formatVersion":1,'
          '"knots":[{"at":[0,0,0]},{"at":[1,"x",0]}]}',
        ),
        throwsA(isA<PathFormatException>()),
      );
    });

    test('uses the .opath extension', () {
      expect(pathExtension, '.opath');
    });
  });
}
