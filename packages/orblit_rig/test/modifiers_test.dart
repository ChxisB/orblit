import 'dart:math' as math;

import 'package:orblit_rig/orblit_rig.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// [count] bones a metre long along +Y, each hung from the one before.
Armature chain(int count, {String prefix = 'b'}) => Armature([
  for (var i = 0; i < count; i++)
    Bone(
      name: '$prefix$i',
      head: Vector3(0, i.toDouble(), 0),
      tail: Vector3(0, i + 1.0, 0),
      parent: i == 0 ? null : '$prefix${i - 1}',
      connected: i > 0,
    ),
]);

Pose posed(Armature armature) => Pose(armature)..evaluate();

ModelPoint at(double x, double y, double z) =>
    (_, _) => Vector3(x, y, z);

double angleBetween(Vector3 a, Vector3 b) =>
    math.acos(a.normalized().dot(b.normalized()).clamp(-1.0, 1.0));

/// How far [point] is from the nearest part of a line of straight pieces.
double distanceToPolyline(Vector3 point, List<Vector3> line) {
  var nearest = double.infinity;
  for (var i = 0; i + 1 < line.length; i++) {
    final along = line[i + 1] - line[i];
    final t = ((point - line[i]).dot(along) / along.length2).clamp(0.0, 1.0);
    nearest = math.min(nearest, point.distanceTo(line[i] + along * t));
  }
  return nearest;
}

/// A pose axis the way the rest of a bone sees it.
Vector3 yAxisOf(Pose pose, String bone) =>
    pose.worldOf(bone).getColumn(1).xyz.normalized();

void apply(Pose pose, Modifier modifier, {double dt = 0, Matrix4? toWorld}) {
  ModifierStack()
    ..add('m', modifier)
    ..apply(
      pose,
      frame: ModifierFrame(dt: dt, modelToWorld: toWorld),
    );
}

void main() {
  group('two-bone IK', () {
    test('puts the end of the limb on a target it can reach', () {
      final pose = posed(chain(2));
      apply(pose, TwoBoneIk(root: 'b0', mid: 'b1', target: at(1, 1, 0)));
      expect(pose.tailOf('b1').distanceTo(Vector3(1, 1, 0)), lessThan(1e-6));
    });

    test('keeps the length of both bones', () {
      final pose = posed(chain(2));
      apply(pose, TwoBoneIk(root: 'b0', mid: 'b1', target: at(1, 1, 0)));
      expect(pose.headOf('b1').length, closeTo(1, 1e-6));
      expect(pose.tailOf('b1').distanceTo(pose.headOf('b1')), closeTo(1, 1e-6));
    });

    test('reaches the same place the pose does with an IkChain', () {
      final armature = Armature([
        ...chain(2).bones.map((b) => b.copy()),
        Bone(name: 'goal', head: Vector3(1, 1, 0), tail: Vector3(1, 1.2, 0)),
        Bone(name: 'pole', head: Vector3(0, 1, 2), tail: Vector3(0, 1.2, 2)),
      ]);
      final inside = Pose(armature)
        ..addIkChain(
          const IkChain(root: 'b0', mid: 'b1', target: 'goal', pole: 'pole'),
        )
        ..evaluate();

      final outside = posed(armature);
      apply(
        outside,
        TwoBoneIk(
          root: 'b0',
          mid: 'b1',
          target: at(1, 1, 0),
          pole: at(0, 1, 2),
        ),
      );

      expect(
        outside.headOf('b1').distanceTo(inside.headOf('b1')),
        lessThan(1e-6),
      );
      expect(
        outside.tailOf('b1').distanceTo(inside.tailOf('b1')),
        lessThan(1e-6),
      );
    });

    test('is the same when it runs twice on the same pose', () {
      final pose = posed(chain(2));
      final modifier = TwoBoneIk(root: 'b0', mid: 'b1', target: at(1, 1, 0));
      apply(pose, modifier);
      final once = pose.tailOf('b1').clone();
      apply(pose, modifier);
      expect(pose.tailOf('b1').distanceTo(once), lessThan(1e-9));
    });

    test('straightens towards a target out of reach', () {
      final pose = posed(chain(2));
      apply(pose, TwoBoneIk(root: 'b0', mid: 'b1', target: at(0, 0, 5)));
      final end = pose.tailOf('b1');
      expect(end.z, closeTo(2, 1e-2));
      expect(end.y.abs(), lessThan(1e-2));
    });

    test('stretches past full reach when allowed', () {
      final pose = posed(chain(2));
      apply(
        pose,
        TwoBoneIk(root: 'b0', mid: 'b1', target: at(0, 0, 3), stretch: 1),
      );
      expect(pose.tailOf('b1').distanceTo(Vector3(0, 0, 3)), lessThan(1e-6));
    });

    test('drops a stretch once the target is back in reach', () {
      final pose = posed(chain(2));
      var goal = Vector3(0, 0, 3);
      final modifier = TwoBoneIk(
        root: 'b0',
        mid: 'b1',
        target: (_, _) => goal,
        stretch: 1,
      );
      apply(pose, modifier);
      expect(pose['b0'].scale.y, greaterThan(1.2));

      goal = Vector3(0, 0, 1);
      apply(pose, modifier);
      expect(pose['b0'].scale.y, closeTo(1, 1e-9));
    });

    test('reaches for a point given in the world', () {
      final pose = posed(chain(2));
      final lifted = Matrix4.translationValues(10, 0, 0);
      apply(
        pose,
        TwoBoneIk(
          root: 'b0',
          mid: 'b1',
          target: (_, frame) => frame.toModel(Vector3(11, 1, 0)),
        ),
        toWorld: lifted,
      );
      expect(pose.tailOf('b1').distanceTo(Vector3(1, 1, 0)), lessThan(1e-6));
    });

    test('leaves the limb alone with no target', () {
      final pose = posed(chain(2));
      apply(pose, TwoBoneIk(root: 'b0', mid: 'b1', target: (_, _) => null));
      expect(pose['b0'].isRest, isTrue);
    });

    test('refuses two bones that are not one limb', () {
      final pose = posed(chain(3));
      expect(
        () =>
            apply(pose, TwoBoneIk(root: 'b0', mid: 'b2', target: at(1, 1, 0))),
        throwsA(isA<ArmatureError>()),
      );
    });
  });

  group('look at', () {
    test('turns a bone to face a target inside the limit', () {
      final pose = posed(chain(1));
      apply(pose, LookAt(bone: 'b0', target: at(1, 0, 0), limit: math.pi));
      final facing = yAxisOf(pose, 'b0');
      expect(angleBetween(facing, Vector3(1, 0, 0)), lessThan(1e-6));
    });

    test('stops at the limit and no further', () {
      final pose = posed(chain(1));
      apply(pose, LookAt(bone: 'b0', target: at(1, 0, 0), limit: 0.5));
      final facing = yAxisOf(pose, 'b0');
      expect(angleBetween(facing, Vector3(0, 1, 0)), closeTo(0.5, 1e-6));
      // It turned the right way: towards the target and not away from it.
      expect(facing.x, greaterThan(0));
    });

    test('looks as far round as the limit goes for a target behind', () {
      final pose = posed(chain(1));
      apply(pose, LookAt(bone: 'b0', target: at(0, -5, 0.001), limit: 1));
      expect(
        angleBetween(yAxisOf(pose, 'b0'), Vector3(0, 1, 0)),
        closeTo(1, 1e-6),
      );
    });

    test('adds to a turn the animation already made', () {
      final pose = posed(chain(1));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), -0.3);
      pose.evaluate();
      apply(pose, LookAt(bone: 'b0', target: at(0, 5, 0), limit: math.pi));
      expect(
        angleBetween(yAxisOf(pose, 'b0'), Vector3(0, 1, 0)),
        lessThan(1e-6),
      );
    });

    test('eases towards the target by the time that passed', () {
      final pose = posed(chain(1));
      final look = LookAt(
        bone: 'b0',
        target: at(1, 0, 0),
        limit: math.pi,
        ease: 0.1,
      );
      final stack = ModifierStack()..add('look', look);
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      final turned = angleBetween(yAxisOf(pose, 'b0'), Vector3(0, 1, 0));
      // After one time constant it has closed 1 - 1/e of the quarter turn.
      expect(turned, closeTo((math.pi / 2) * (1 - math.exp(-1)), 1e-6));
    });

    test('does not move in a frame of no time while easing', () {
      final pose = posed(chain(1));
      apply(
        pose,
        LookAt(bone: 'b0', target: at(1, 0, 0), limit: math.pi, ease: 0.2),
      );
      expect(pose['b0'].isRest, isTrue);
    });

    test('lets go of a target that goes away', () {
      final pose = posed(chain(1));
      Vector3? goal = Vector3(1, 0, 0);
      final stack = ModifierStack()
        ..add(
          'look',
          LookAt(bone: 'b0', target: (_, _) => goal, limit: math.pi),
        );
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      goal = null;
      pose.reset();
      pose.evaluate();
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      expect(pose['b0'].isRest, isTrue);
    });

    test('does not spin the bone about its own length', () {
      final pose = posed(chain(1));
      apply(pose, LookAt(bone: 'b0', target: at(1, 1, 0), limit: math.pi));
      final side = pose.worldOf('b0').getColumn(0).xyz.normalized();
      // A rest bone's X axis is along +X. Turning about Z keeps it in the XY
      // plane, so it has no component along Z.
      expect(side.z.abs(), lessThan(1e-9));
    });

    test('refuses a limit that is not an angle', () {
      expect(
        () => LookAt(bone: 'b0', target: at(0, 0, 0), limit: -1),
        throwsArgumentError,
      );
    });
  });

  group('constrain', () {
    test('runs a constraint after the modifiers above it', () {
      final armature = Armature([
        ...chain(1).bones.map((b) => b.copy()),
        Bone(name: 'goal', head: Vector3(3, 0, 0), tail: Vector3(3, 1, 0)),
      ]);
      final pose = posed(armature);
      apply(
        pose,
        Constrain(
          bone: 'b0',
          constraint: const DampedTrack(target: 'goal'),
        ),
      );
      final toGoal = pose.headOf('goal') - pose.headOf('b0');
      expect(angleBetween(yAxisOf(pose, 'b0'), toGoal), lessThan(1e-6));
    });

    test('refuses a constraint that has its own influence', () {
      expect(
        () => Constrain(
          bone: 'b0',
          constraint: const DampedTrack(target: 'goal', influence: 0.5),
        ),
        throwsArgumentError,
      );
    });
  });

  group('chain IK', () {
    test('lands the tip on a target it can reach', () {
      final pose = posed(chain(4));
      apply(pose, ChainIk(root: 'b0', tip: 'b3', target: at(1.5, 2, 1)));
      expect(pose.tailOf('b3').distanceTo(Vector3(1.5, 2, 1)), lessThan(1e-2));
    });

    test('keeps the root where it was', () {
      final pose = posed(chain(4));
      apply(pose, ChainIk(root: 'b0', tip: 'b3', target: at(1.5, 2, 1)));
      expect(pose.headOf('b0').length, lessThan(1e-9));
    });

    test('keeps the length of every bone', () {
      final pose = posed(chain(4));
      apply(pose, ChainIk(root: 'b0', tip: 'b3', target: at(1.5, 2, 1)));
      for (var i = 0; i < 4; i++) {
        final length = pose.tailOf('b$i').distanceTo(pose.headOf('b$i'));
        expect(length, closeTo(1, 1e-6));
      }
    });

    test('keeps the chain joined end to end', () {
      final pose = posed(chain(4));
      apply(pose, ChainIk(root: 'b0', tip: 'b3', target: at(1.5, 2, 1)));
      for (var i = 0; i < 3; i++) {
        expect(
          pose.tailOf('b$i').distanceTo(pose.headOf('b${i + 1}')),
          lessThan(1e-6),
        );
      }
    });

    test('straightens towards a target out of reach', () {
      final pose = posed(chain(4));
      apply(pose, ChainIk(root: 'b0', tip: 'b3', target: at(10, 0, 0)));
      final tip = pose.tailOf('b3');
      expect(tip.x, closeTo(4, 1e-6));
      expect(tip.y.abs(), lessThan(1e-6));
    });

    test('refuses a tip that is not below the root', () {
      final armature = Armature([
        ...chain(2, prefix: 'a').bones.map((b) => b.copy()),
        ...chain(2, prefix: 'c').bones.map((b) => b.copy()),
      ]);
      final pose = posed(armature);
      expect(
        () => apply(pose, ChainIk(root: 'a0', tip: 'c1', target: at(0, 0, 0))),
        throwsA(isA<ArmatureError>()),
      );
    });
  });

  group('fabrik', () {
    test('keeps every gap and the first point', () {
      final points = [for (var i = 0; i < 5; i++) Vector3(0, i * 0.5, 0)];
      final solved = solveFabrik(points: points, target: Vector3(1, 1, 0));
      expect(solved.first, Vector3.zero());
      for (var i = 0; i < 4; i++) {
        expect(solved[i].distanceTo(solved[i + 1]), closeTo(0.5, 1e-9));
      }
      expect(solved.last.distanceTo(Vector3(1, 1, 0)), lessThan(1e-3));
    });

    test('leaves the input alone', () {
      final points = [Vector3.zero(), Vector3(0, 1, 0)];
      solveFabrik(points: points, target: Vector3(1, 0, 0));
      expect(points.last, Vector3(0, 1, 0));
    });

    test('has nothing to do with one point', () {
      final solved = solveFabrik(
        points: [Vector3(1, 2, 3)],
        target: Vector3.zero(),
      );
      expect(solved.single, Vector3(1, 2, 3));
    });
  });

  group('spline IK', () {
    final controls = [
      Vector3(0, 0, 0),
      Vector3(2, 1, 0),
      Vector3(4, 0, 0),
      Vector3(6, 1, 0),
    ];

    test('puts the root on the start of the curve', () {
      final pose = posed(chain(3));
      apply(pose, SplineIk(root: 'b0', tip: 'b2', points: (_, _) => controls));
      expect(pose.headOf('b0').distanceTo(controls.first), lessThan(1e-9));
    });

    test('keeps every bone its own length', () {
      final pose = posed(chain(3));
      apply(pose, SplineIk(root: 'b0', tip: 'b2', points: (_, _) => controls));
      for (var i = 0; i < 3; i++) {
        final length = pose.tailOf('b$i').distanceTo(pose.headOf('b$i'));
        expect(length, closeTo(1, 1e-6));
      }
    });

    test('lays the joints on the curve', () {
      final pose = posed(chain(3));
      apply(pose, SplineIk(root: 'b0', tip: 'b2', points: (_, _) => controls));
      final curve = sampleCurve(controls, 12);
      final joints = [pose.headOf('b1'), pose.headOf('b2'), pose.tailOf('b2')];
      for (final joint in joints) {
        expect(distanceToPolyline(joint, curve), lessThan(1e-6));
      }
    });

    test('goes on straight where the curve runs out', () {
      final pose = posed(chain(8));
      final short = [Vector3(0, 0, 0), Vector3(1, 0, 0), Vector3(2, 0, 0)];
      apply(pose, SplineIk(root: 'b0', tip: 'b7', points: (_, _) => short));
      expect(pose.tailOf('b7').x, closeTo(8, 1e-3));
      expect(pose.tailOf('b7').y.abs(), lessThan(1e-3));
    });

    test('passes through its control points', () {
      final curve = sampleCurve(controls, 12);
      for (final point in controls) {
        final nearest = curve.map((p) => p.distanceTo(point)).reduce(math.min);
        expect(nearest, lessThan(1e-9));
      }
    });

    test('leaves the chain alone with fewer than two points', () {
      final pose = posed(chain(3));
      apply(
        pose,
        SplineIk(root: 'b0', tip: 'b2', points: (_, _) => [Vector3.zero()]),
      );
      expect(pose['b0'].isRest, isTrue);
    });
  });

  group('cone limit', () {
    test('leaves a bone inside the cone alone', () {
      final pose = posed(chain(2));
      final turn = Quaternion.axisAngle(Vector3(1, 0, 0), 0.2);
      pose['b0'].rotation = turn;
      apply(pose, ConeLimit(bone: 'b0', swing: 0.5));
      expect(turnAngleOf(pose['b0'].rotation), closeTo(0.2, 1e-9));
    });

    test('holds a swing to the limit', () {
      final pose = posed(chain(2));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), 1.2);
      apply(pose, ConeLimit(bone: 'b0', swing: 0.5));
      expect(turnAngleOf(pose['b0'].rotation), closeTo(0.5, 1e-6));
    });

    test('holds the swing in the direction it was going', () {
      final pose = posed(chain(2));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), 1.2);
      apply(pose, ConeLimit(bone: 'b0', swing: 0.5));
      final facing = yAxisOf(pose, 'b0');
      // A turn about +X by a positive angle swings +Y towards +Z.
      expect(facing.z, closeTo(math.sin(0.5), 1e-6));
    });

    test('holds a roll to the limit', () {
      final pose = posed(chain(2));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 1, 0), 1.5);
      apply(pose, ConeLimit(bone: 'b0', swing: 1, twist: 0.4));
      expect(turnAngleOf(pose['b0'].rotation), closeTo(0.4, 1e-6));
    });

    test('moves what hangs below it', () {
      final pose = posed(chain(2));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), 1.2);
      pose.evaluate();
      apply(pose, ConeLimit(bone: 'b0', swing: 0.5));
      expect(pose.headOf('b1').z, closeTo(math.sin(0.5), 1e-6));
    });

    test('does not clamp a rest pose that is not axis aligned', () {
      final armature = Armature([
        Bone(name: 'a', head: Vector3.zero(), tail: Vector3(1, 1, 0)),
      ]);
      final pose = posed(armature);
      apply(pose, ConeLimit(bone: 'a', swing: 0.1));
      expect(pose['a'].isRest, isTrue);
    });

    test('refuses a swing past a half turn', () {
      expect(() => ConeLimit(bone: 'a', swing: 4), throwsArgumentError);
    });
  });

  group('hinge limit', () {
    test('keeps a bend about the axis', () {
      final pose = posed(chain(2));
      pose['b1'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), 0.8);
      apply(pose, HingeLimit(bone: 'b1', min: -0.1, max: 1.5));
      expect(turnAngleOf(pose['b1'].rotation), closeTo(0.8, 1e-9));
    });

    test('holds a bend to the ends of its range', () {
      final pose = posed(chain(2));
      pose['b1'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), 2.5);
      apply(pose, HingeLimit(bone: 'b1', min: 0, max: 1.5));
      expect(turnAngleOf(pose['b1'].rotation), closeTo(1.5, 1e-9));
    });

    test('will not bend the wrong way', () {
      final pose = posed(chain(2));
      pose['b1'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), -0.7);
      apply(pose, HingeLimit(bone: 'b1', min: 0, max: 1.5));
      expect(pose['b1'].isRest, isTrue);
    });

    test('drops a turn off the axis', () {
      final pose = posed(chain(2));
      pose['b1'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 0.9);
      apply(pose, HingeLimit(bone: 'b1', min: -1, max: 1));
      expect(turnAngleOf(pose['b1'].rotation), closeTo(0, 1e-6));
    });

    test('refuses a range that runs backwards', () {
      expect(() => HingeLimit(bone: 'a', min: 1, max: 0), throwsArgumentError);
    });
  });

  group('twist spread', () {
    Armature forearm() => Armature([
      Bone(name: 'fore', head: Vector3.zero(), tail: Vector3(0, 3, 0)),
      Bone(
        name: 'twist0',
        head: Vector3(0, 1, 0),
        tail: Vector3(0, 2, 0),
        parent: 'fore',
      ),
      Bone(
        name: 'twist1',
        head: Vector3(0, 2, 0),
        tail: Vector3(0, 3, 0),
        parent: 'fore',
      ),
      Bone(
        name: 'hand',
        head: Vector3(0, 3, 0),
        tail: Vector3(0, 4, 0),
        parent: 'fore',
      ),
    ]);

    test('shares a roll out evenly by default', () {
      final pose = posed(forearm());
      pose['hand'].rotation = Quaternion.axisAngle(Vector3(0, 1, 0), 1.2);
      apply(
        pose,
        TwistSpread(source: 'hand', bones: const ['twist0', 'twist1']),
      );
      expect(turnAngleOf(pose['twist0'].rotation), closeTo(0.6, 1e-9));
      expect(turnAngleOf(pose['twist1'].rotation), closeTo(1.2, 1e-9));
    });

    test('takes the shares it is given', () {
      final pose = posed(forearm());
      pose['hand'].rotation = Quaternion.axisAngle(Vector3(0, 1, 0), 1.0);
      apply(
        pose,
        TwistSpread(
          source: 'hand',
          bones: const ['twist0', 'twist1'],
          shares: const [0.25, 0.75],
        ),
      );
      expect(turnAngleOf(pose['twist0'].rotation), closeTo(0.25, 1e-9));
      expect(turnAngleOf(pose['twist1'].rotation), closeTo(0.75, 1e-9));
    });

    test('ignores a bend that is not a roll', () {
      final pose = posed(forearm());
      pose['hand'].rotation = Quaternion.axisAngle(Vector3(1, 0, 0), 1.0);
      apply(pose, TwistSpread(source: 'hand', bones: const ['twist0']));
      expect(turnAngleOf(pose['twist0'].rotation), closeTo(0, 1e-9));
    });

    test('follows a roll the other way', () {
      final pose = posed(forearm());
      pose['hand'].rotation = Quaternion.axisAngle(Vector3(0, 1, 0), -0.8);
      apply(pose, TwistSpread(source: 'hand', bones: const ['twist0']));
      final spread = pose['twist0'].rotation;
      expect(spread.y, lessThan(0));
    });

    test('refuses a share for each bone that is not there', () {
      expect(
        () => TwistSpread(
          source: 'hand',
          bones: const ['twist0', 'twist1'],
          shares: const [1],
        ),
        throwsArgumentError,
      );
    });
  });

  group('speed limit', () {
    test('lets the first frame through', () {
      final pose = posed(chain(1));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 1.5);
      apply(pose, SpeedLimit(bones: const ['b0'], turnSpeed: 1), dt: 0.1);
      expect(turnAngleOf(pose['b0'].rotation), closeTo(1.5, 1e-9));
    });

    test('limits a turn to the speed times the time', () {
      final pose = posed(chain(1));
      final stack = ModifierStack()
        ..add('slow', SpeedLimit(bones: const ['b0'], turnSpeed: 1));
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));

      pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 1.5);
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      expect(turnAngleOf(pose['b0'].rotation), closeTo(0.1, 1e-9));
    });

    test('gets there in time', () {
      final pose = posed(chain(1));
      final stack = ModifierStack()
        ..add('slow', SpeedLimit(bones: const ['b0'], turnSpeed: 1));
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      for (var i = 0; i < 20; i++) {
        pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 1.0);
        stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      }
      expect(turnAngleOf(pose['b0'].rotation), closeTo(1.0, 1e-9));
    });

    test('limits movement as well as turning', () {
      final pose = posed(chain(1));
      final stack = ModifierStack()
        ..add('slow', SpeedLimit(bones: const ['b0'], moveSpeed: 2));
      stack.apply(pose, frame: ModifierFrame(dt: 0.5));

      pose['b0'].location.setValues(10, 0, 0);
      stack.apply(pose, frame: ModifierFrame(dt: 0.5));
      expect(pose['b0'].location.x, closeTo(1, 1e-9));
    });

    test('holds a bone still in a frame of no time', () {
      final pose = posed(chain(1));
      final stack = ModifierStack()
        ..add('slow', SpeedLimit(bones: const ['b0'], turnSpeed: 1));
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 1.5);
      stack.apply(pose);
      expect(pose['b0'].isRest, isTrue);
    });

    test('starts again after a reset', () {
      final pose = posed(chain(1));
      final stack = ModifierStack()
        ..add('slow', SpeedLimit(bones: const ['b0'], turnSpeed: 1));
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      stack.reset();
      pose['b0'].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), 1.5);
      stack.apply(pose, frame: ModifierFrame(dt: 0.1));
      expect(turnAngleOf(pose['b0'].rotation), closeTo(1.5, 1e-9));
    });
  });

  group('bone attachment', () {
    test('reports nothing before the first frame', () {
      expect(BoneAttachment(bone: 'b0').world, isNull);
    });

    test('follows the bone, in the model and in the world', () {
      final pose = posed(chain(2));
      pose['b0'].location.setValues(0, 0, 2);
      pose.evaluate();
      final socket = BoneAttachment(
        bone: 'b1',
        offset: Matrix4.translationValues(0, 1, 0),
      );
      apply(pose, socket, toWorld: Matrix4.translationValues(10, 0, 0));
      expect(socket.model!.getTranslation(), Vector3(0, 2, 2));
      expect(socket.world!.getTranslation(), Vector3(10, 2, 2));
    });

    test('does not change the pose', () {
      final pose = posed(chain(2));
      apply(pose, BoneAttachment(bone: 'b1'));
      expect(pose['b1'].isRest, isTrue);
    });

    test('hands out a copy', () {
      final pose = posed(chain(2));
      final socket = BoneAttachment(bone: 'b1');
      apply(pose, socket);
      socket.world!.setTranslation(Vector3(9, 9, 9));
      expect(socket.world!.getTranslation(), Vector3(0, 1, 0));
    });
  });
}

/// How far a rotation turns, in radians.
double turnAngleOf(Quaternion rotation) =>
    2 * math.acos(rotation.w.abs().clamp(0.0, 1.0));
