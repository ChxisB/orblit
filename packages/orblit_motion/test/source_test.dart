import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';

/// Reads inputs out of [values], and nought for anything else.
double Function(String) reading(Map<String, double> values) =>
    (input) => values[input] ?? 0;

void expectWeights(Map<String, double> actual, Map<String, double> expected) {
  expect(actual.keys.toSet(), expected.keys.toSet(), reason: '$actual');
  for (final MapEntry(:key, :value) in expected.entries) {
    expect(actual[key], closeTo(value, 1e-9), reason: '$key in $actual');
  }
}

BlendLine walkToRun() => BlendLine('speed', const [
  LinePoint(0, BlendClip('idle')),
  LinePoint(1.5, BlendClip('walk')),
  LinePoint(4, BlendClip('run')),
]);

/// Idle in the middle and a walk each way, over a velocity's two parts.
BlendPlane directions() => BlendPlane('sideways', 'forwards', const [
  PlanePoint(0, 0, BlendClip('idle')),
  PlanePoint(0, 1, BlendClip('forwards')),
  PlanePoint(0, -1, BlendClip('backwards')),
  PlanePoint(1, 0, BlendClip('right')),
  PlanePoint(-1, 0, BlendClip('left')),
]);

void main() {
  group('a clip', () {
    test('has the whole say', () {
      expectWeights(const BlendClip('idle').weigh(reading({})), {'idle': 1});
    });
  });

  group('a line', () {
    test('is the nearer clip past either end', () {
      final line = walkToRun();
      expectWeights(line.weigh(reading({'speed': -1})), {'idle': 1});
      expectWeights(line.weigh(reading({'speed': 0})), {'idle': 1});
      expectWeights(line.weigh(reading({'speed': 9})), {'run': 1});
    });

    test('is exactly a clip at its point', () {
      expectWeights(walkToRun().weigh(reading({'speed': 1.5})), {'walk': 1});
    });

    test('mixes the two either side in proportion', () {
      expectWeights(walkToRun().weigh(reading({'speed': 2})), {
        'walk': 0.8,
        'run': 0.2,
      });
    });

    test('is put in order, keeping the order of two at one place', () {
      final line = BlendLine('speed', const [
        LinePoint(4, BlendClip('run')),
        LinePoint(1, BlendClip('walk')),
        LinePoint(1, BlendClip('stride')),
      ]);
      expect([for (final point in line.points) point.at], [1, 1, 4]);
      // Two at one place are a step: the first below, the second from
      // there up.
      expectWeights(line.weigh(reading({'speed': 1})), {'stride': 1});
      expectWeights(line.weigh(reading({'speed': 0.5})), {'walk': 1});
      expectWeights(line.weigh(reading({'speed': 2.5})), {
        'stride': 0.5,
        'run': 0.5,
      });
    });

    test('of one point is that point anywhere', () {
      final line = BlendLine('speed', const [LinePoint(3, BlendClip('only'))]);
      expectWeights(line.weigh(reading({'speed': -10})), {'only': 1});
      expectWeights(line.weigh(reading({'speed': 10})), {'only': 1});
    });

    test('needs a point, somewhere', () {
      expect(() => BlendLine('speed', const []), throwsArgumentError);
      expect(
        () => BlendLine('speed', const [LinePoint(double.nan, BlendClip('x'))]),
        throwsArgumentError,
      );
    });
  });

  group('a plane', () {
    test('is exactly a clip at its point', () {
      final plane = directions();
      for (final point in plane.points) {
        final clip = (point.plays as BlendClip).clip;
        expectWeights(
          plane.weigh(reading({'sideways': point.x, 'forwards': point.y})),
          {clip: 1},
        );
      }
    });

    test('shares between the two points a place is between', () {
      expectWeights(directions().weigh(reading({'forwards': 0.5})), {
        'idle': 0.5,
        'forwards': 0.5,
      });
    });

    test('shares a square evenly at its middle', () {
      final plane = BlendPlane('x', 'y', const [
        PlanePoint(0, 0, BlendClip('a')),
        PlanePoint(1, 0, BlendClip('b')),
        PlanePoint(0, 1, BlendClip('c')),
        PlanePoint(1, 1, BlendClip('d')),
      ]);
      expectWeights(plane.weigh(reading({'x': 0.5, 'y': 0.5})), {
        'a': 0.25,
        'b': 0.25,
        'c': 0.25,
        'd': 0.25,
      });
    });

    test('settles on the nearest outside them all', () {
      expectWeights(
        directions().weigh(reading({'sideways': 5, 'forwards': 0})),
        {'right': 1},
      );
    });

    test('always adds to one', () {
      final plane = directions();
      final random = math.Random(7);
      for (var i = 0; i < 500; i++) {
        final x = random.nextDouble() * 4 - 2;
        final y = random.nextDouble() * 4 - 2;
        final weights = plane.weightsAt(x, y);
        expect(
          weights.fold(0.0, (sum, one) => sum + one),
          closeTo(1, 1e-9),
          reason: 'at ($x, $y)',
        );
        expect(weights.every((one) => one >= 0), isTrue);
      }
    });

    test('changes smoothly, with no jump anywhere', () {
      final plane = directions();
      for (var i = 0; i <= 400; i++) {
        final angle = i / 400 * 2 * math.pi;
        final here = plane.weightsAt(
          0.7 * math.cos(angle),
          0.7 * math.sin(angle),
        );
        final next = plane.weightsAt(
          0.7 * math.cos(angle + 1e-4),
          0.7 * math.sin(angle + 1e-4),
        );
        for (var j = 0; j < here.length; j++) {
          expect((here[j] - next[j]).abs(), lessThan(1e-3));
        }
      }
    });

    test('shares one place between two points at it', () {
      final plane = BlendPlane('x', 'y', const [
        PlanePoint(0, 0, BlendClip('a')),
        PlanePoint(0, 0, BlendClip('b')),
        PlanePoint(1, 0, BlendClip('c')),
      ]);
      expectWeights(plane.weigh(reading({})), {'a': 0.5, 'b': 0.5});
    });

    test('needs a point', () {
      expect(() => BlendPlane('x', 'y', const []), throwsArgumentError);
    });
  });

  group('nesting', () {
    test('multiplies a say through each level', () {
      final line = BlendLine('speed', [
        const LinePoint(0, BlendClip('idle')),
        LinePoint(2, directions()),
      ]);
      expectWeights(
        line.weigh(reading({'speed': 1, 'sideways': 0, 'forwards': 1})),
        {'idle': 0.5, 'forwards': 0.5},
      );
      expectWeights(
        line.weigh(reading({'speed': 1, 'sideways': 0, 'forwards': 0.5})),
        {'idle': 0.75, 'forwards': 0.25},
      );
    });

    test('adds together a clip named twice', () {
      final line = BlendLine('speed', const [
        LinePoint(0, BlendClip('walk')),
        LinePoint(1, BlendClip('walk')),
      ]);
      expectWeights(line.weigh(reading({'speed': 0.3})), {'walk': 1});
    });

    test('lists every clip and every input', () {
      final line = BlendLine('speed', [
        const LinePoint(0, BlendClip('idle')),
        LinePoint(2, directions()),
      ]);
      expect(line.clips, [
        'idle',
        'idle',
        'forwards',
        'backwards',
        'right',
        'left',
      ]);
      expect(line.inputs.toSet(), {'speed', 'sideways', 'forwards'});
    });
  });

  group('in a file', () {
    test('reads back what it writes', () {
      final line = BlendLine('speed', [
        const LinePoint(0, BlendClip('idle')),
        LinePoint(2, directions()),
      ]);
      final problems = <String>[];
      final back = BlendSource.fromJson(line.toJson(), 'here', problems);
      expect(problems, isEmpty);
      expect(back!.toJson(), line.toJson());
    });

    test('notes what it cannot read and keeps the rest', () {
      final problems = <String>[];
      final back = BlendSource.fromJson(
        {
          'line': {
            'input': 'speed',
            'points': [
              {'at': 0, 'clip': 'idle'},
              {'at': 'fast', 'clip': 'run'},
              {'at': 2},
            ],
          },
        },
        'the state "move"',
        problems,
      );
      expect(back, isA<BlendLine>());
      expect((back! as BlendLine).points, hasLength(1));
      expect(problems, hasLength(2));
      expect(problems.first, contains('the line in the state "move"'));
    });

    test('is nothing when it names nothing', () {
      final problems = <String>[];
      expect(BlendSource.fromJson(const {}, 'the state "x"', problems), isNull);
      expect(problems.single, startsWith('The state "x" plays nothing'));
      expect(
        BlendSource.fromJson(
          const {
            'plane': {'x': 'a'},
          },
          'the state "y"',
          problems,
        ),
        isNull,
      );
    });
  });
}
