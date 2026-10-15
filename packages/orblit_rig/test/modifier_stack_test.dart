import 'dart:math' as math;

import 'package:orblit_rig/orblit_rig.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// Two bones along +Y, a metre each.
Armature twoBones() => Armature([
  Bone(name: 'a', head: Vector3(0, 0, 0), tail: Vector3(0, 1, 0)),
  Bone(
    name: 'b',
    head: Vector3(0, 1, 0),
    tail: Vector3(0, 2, 0),
    parent: 'a',
    connected: true,
  ),
]);

/// Sets a bone's pose location, and counts how often it was reset.
class Shove extends Modifier {
  Shove(this.bone, this.by);

  final String bone;
  final Vector3 by;
  int resets = 0;
  int solves = 0;

  @override
  void solve(Pose pose, ModifierFrame frame) {
    solves++;
    pose[bone].location.setFrom(by);
  }

  @override
  void reset() => resets++;
}

/// Adds to what the bone already holds, so a second run shows the order.
class Add extends Modifier {
  Add(this.bone, this.by);

  final String bone;
  final double by;

  @override
  void solve(Pose pose, ModifierFrame frame) {
    pose[bone].location.x += by;
  }
}

class Turn extends Modifier {
  Turn(this.bone, this.angle);

  final String bone;
  final double angle;

  @override
  void solve(Pose pose, ModifierFrame frame) {
    pose[bone].rotation = Quaternion.axisAngle(Vector3(0, 0, 1), angle);
  }
}

Pose evaluated(Armature armature) => Pose(armature)..evaluate();

void main() {
  group('the frame', () {
    test('carries a point between the model and the world', () {
      final frame = ModifierFrame(
        modelToWorld: Matrix4.translationValues(5, 0, 0),
      );
      expect(frame.toWorld(Vector3(1, 2, 3)), Vector3(6, 2, 3));
      expect(frame.toModel(Vector3(6, 2, 3)), Vector3(1, 2, 3));
    });

    test('does not move a direction when the model moves', () {
      final frame = ModifierFrame(
        modelToWorld: Matrix4.translationValues(5, 0, 0),
      );
      expect(frame.directionToWorld(Vector3(0, 1, 0)), Vector3(0, 1, 0));
    });

    test('turns a direction with the model', () {
      final frame = ModifierFrame(modelToWorld: Matrix4.rotationZ(math.pi / 2));
      final turned = frame.directionToWorld(Vector3(1, 0, 0));
      expect(turned.x, closeTo(0, 1e-9));
      expect(turned.y, closeTo(1, 1e-9));
    });

    test('reports how much a model is scaled', () {
      final frame = ModifierFrame(
        modelToWorld: Matrix4.diagonal3Values(2, 2, 2),
      );
      expect(frame.scale, closeTo(2, 1e-9));
    });

    test('refuses a negative time', () {
      expect(() => ModifierFrame(dt: -0.1), throwsArgumentError);
    });

    test('refuses a time that never ends', () {
      expect(() => ModifierFrame(dt: double.infinity), throwsArgumentError);
    });

    test('refuses a matrix that squashes space flat', () {
      expect(
        () => ModifierFrame(modelToWorld: Matrix4.diagonal3Values(0, 1, 1)),
        throwsArgumentError,
      );
    });
  });

  group('the stack', () {
    test('runs modifiers in the order they were added', () {
      final pose = evaluated(twoBones());
      final stack = ModifierStack()
        ..add('set', Shove('a', Vector3(1, 0, 0)))
        ..add('add', Add('a', 2));
      stack.apply(pose);
      expect(pose['a'].location.x, 3);
    });

    test('runs them the other way when added the other way', () {
      final pose = evaluated(twoBones());
      final stack = ModifierStack()
        ..add('add', Add('a', 2))
        ..add('set', Shove('a', Vector3(1, 0, 0)));
      stack.apply(pose);
      expect(pose['a'].location.x, 1);
    });

    test('leaves the pose up to date', () {
      final pose = evaluated(twoBones());
      ModifierStack()
        ..add('set', Shove('a', Vector3(0, 0, 4)))
        ..apply(pose);
      expect(pose.headOf('b').z, closeTo(4, 1e-9));
    });

    test('keeps a share of the result for a partial influence', () {
      final pose = evaluated(twoBones());
      final stack = ModifierStack()
        ..add('set', Shove('a', Vector3(4, 0, 0)), influence: 0.25);
      stack.apply(pose);
      expect(pose['a'].location.x, closeTo(1, 1e-9));
    });

    test('mixes a rotation by the shorter arc', () {
      final pose = evaluated(twoBones());
      ModifierStack()
        ..add('turn', Turn('a', math.pi / 2), influence: 0.5)
        ..apply(pose);
      final turn = pose['a'].rotation;
      expect(2 * math.acos(turn.w), closeTo(math.pi / 4, 1e-9));
    });

    test('skips a modifier at zero influence and resets it', () {
      final shove = Shove('a', Vector3(1, 0, 0));
      final pose = evaluated(twoBones());
      ModifierStack()
        ..add('set', shove, influence: 0)
        ..apply(pose);
      expect(shove.solves, 0);
      expect(shove.resets, 1);
      expect(pose['a'].location.x, 0);
    });

    test('skips a modifier that is switched off', () {
      final shove = Shove('a', Vector3(1, 0, 0));
      final pose = evaluated(twoBones());
      final stack = ModifierStack();
      stack.add('set', shove).enabled = false;
      stack.apply(pose);
      expect(shove.solves, 0);
      expect(shove.resets, 1);
    });

    test('takes an influence from a pose property', () {
      final pose = evaluated(twoBones())..setProperty('look', 0.5);
      ModifierStack()
        ..add('set', Shove('a', Vector3(2, 0, 0)), influenceProperty: 'look')
        ..apply(pose);
      expect(pose['a'].location.x, closeTo(1, 1e-9));
    });

    test('falls back to its own influence when the property is not set', () {
      final pose = evaluated(twoBones());
      ModifierStack()
        ..add(
          'set',
          Shove('a', Vector3(2, 0, 0)),
          influence: 0.5,
          influenceProperty: 'missing',
        )
        ..apply(pose);
      expect(pose['a'].location.x, closeTo(1, 1e-9));
    });

    test('clamps an influence to zero and one', () {
      final stack = ModifierStack();
      final slot = stack.add('set', Shove('a', Vector3.zero()), influence: 3);
      expect(slot.influence, 1);
      slot.influence = -2;
      expect(slot.influence, 0);
    });

    test('refuses two modifiers with one name', () {
      final stack = ModifierStack()..add('x', Shove('a', Vector3.zero()));
      expect(
        () => stack.add('x', Shove('b', Vector3.zero())),
        throwsArgumentError,
      );
    });

    test('finds a slot by name, and takes it out', () {
      final shove = Shove('a', Vector3.zero());
      final stack = ModifierStack()..add('x', shove);
      expect(stack['x']?.modifier, same(shove));
      expect(stack.remove('x'), isTrue);
      expect(stack.remove('x'), isFalse);
      expect(stack.length, 0);
      expect(shove.resets, 1);
    });

    test('names the bone a modifier is missing before it solves', () {
      final pose = evaluated(twoBones());
      final stack = ModifierStack()
        ..add('look', LookAt(bone: 'head', target: (_, _) => Vector3(1, 1, 1)));
      expect(
        () => stack.apply(pose),
        throwsA(
          isA<ArmatureError>().having(
            (e) => e.message,
            'message',
            allOf(contains('"head"'), contains('nothing to turn')),
          ),
        ),
      );
    });

    test('resets every modifier on request', () {
      final shove = Shove('a', Vector3.zero());
      ModifierStack()
        ..add('x', shove)
        ..reset();
      expect(shove.resets, 1);
    });
  });

  group('refreshing part of a pose', () {
    test('moves the bone and what hangs below it, and not the rest', () {
      final armature = Armature([
        Bone(name: 'a', head: Vector3(0, 0, 0), tail: Vector3(0, 1, 0)),
        Bone(
          name: 'b',
          head: Vector3(0, 1, 0),
          tail: Vector3(0, 2, 0),
          parent: 'a',
        ),
        Bone(name: 'c', head: Vector3(1, 0, 0), tail: Vector3(1, 1, 0)),
      ]);
      final pose = Pose(armature)..evaluate();
      pose['a'].location.setValues(0, 0, 3);
      pose['c'].location.setValues(0, 0, 3);

      pose.refresh(from: 'a');

      expect(pose.headOf('a').z, 3);
      expect(pose.headOf('b').z, 3);
      expect(pose.headOf('c').z, 0);
    });

    test('names a bone that is not there', () {
      final pose = evaluated(twoBones());
      expect(() => pose.refresh(from: 'nope'), throwsA(isA<ArmatureError>()));
    });

    test('gives the same answer however often it runs', () {
      final armature = twoBones();
      final pose = Pose(armature)
        ..addIkChain(
          const IkChain(root: 'a', mid: 'b', target: 'a', influence: 0.5),
        )
        ..evaluate();
      final before = pose.headOf('b').clone();
      pose.refresh();
      pose.refresh();
      expect(pose.headOf('b').distanceTo(before), lessThan(1e-12));
    });
  });
}
