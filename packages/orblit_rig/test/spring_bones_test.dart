import 'dart:math' as math;

import 'package:orblit_rig/orblit_rig.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// An anchor bone and two hair bones hung from it, a metre each, along +Y.
Armature hair() => Armature([
  Bone(name: 'anchor', head: Vector3(0, 0, 0), tail: Vector3(0, 1, 0)),
  Bone(
    name: 'h0',
    head: Vector3(0, 1, 0),
    tail: Vector3(0, 2, 0),
    parent: 'anchor',
    connected: true,
  ),
  Bone(
    name: 'h1',
    head: Vector3(0, 2, 0),
    tail: Vector3(0, 3, 0),
    parent: 'h0',
    connected: true,
  ),
]);

/// The same chain laid out along +X from the origin, so gravity can swing it.
Armature fringe() => Armature([
  Bone(name: 'anchor', head: Vector3(0, 0, 0), tail: Vector3(1, 0, 0)),
  Bone(
    name: 'h0',
    head: Vector3(1, 0, 0),
    tail: Vector3(2, 0, 0),
    parent: 'anchor',
    connected: true,
  ),
  Bone(
    name: 'h1',
    head: Vector3(2, 0, 0),
    tail: Vector3(3, 0, 0),
    parent: 'h0',
    connected: true,
  ),
]);

/// A chain that gravity alone pulls, and that has to settle in 10 seconds.
SpringBones loose({List<SpringCollider> colliders = const []}) => SpringBones(
  bones: const ['h0', 'h1'],
  stiffness: 0,
  drag: 4,
  colliders: colliders,
);

Pose settle(List<SpringCollider> colliders, {Vector3? anchor}) {
  final pose = Pose(fringe());
  final stack = ModifierStack()..add('hair', loose(colliders: colliders));
  frame(pose, stack, anchor: anchor);
  run(pose, stack, frames: 600, anchor: anchor);
  return pose;
}

/// One frame the way a game runs it: the clip writes the anchor, the pose is
/// evaluated, then the stack runs.
void frame(
  Pose pose,
  ModifierStack stack, {
  Vector3? anchor,
  double dt = 1 / 60,
}) {
  pose.reset();
  if (anchor != null) pose['anchor'].location.setFrom(anchor);
  pose.evaluate();
  stack.apply(pose, frame: ModifierFrame(dt: dt));
}

void run(
  Pose pose,
  ModifierStack stack, {
  required int frames,
  Vector3? anchor,
}) {
  for (var i = 0; i < frames; i++) {
    frame(pose, stack, anchor: anchor);
  }
}

SpringBones calm({List<SpringCollider> colliders = const []}) => SpringBones(
  bones: const ['h0', 'h1'],
  gravity: Vector3.zero(),
  colliders: colliders,
);

void main() {
  group('spring bones', () {
    test('pass the first frame as animated', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      frame(pose, stack);
      expect(pose['h0'].isRest, isTrue);
      expect(pose['h1'].isRest, isTrue);
    });

    test('stay at rest when nothing moves and nothing pulls', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      run(pose, stack, frames: 120);
      expect(pose.tailOf('h1').distanceTo(Vector3(0, 3, 0)), lessThan(1e-6));
    });

    test('lag behind a head that moves', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      frame(pose, stack, anchor: Vector3.zero());
      frame(pose, stack, anchor: Vector3(0.5, 0, 0));

      // The animation has the tail at x = 0.5. Inertia leaves it behind.
      expect(pose.tailOf('h1').x, lessThan(0.25));
      expect(pose.tailOf('h1').x, greaterThan(-0.05));
    });

    test('catch up once the head stops', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      frame(pose, stack, anchor: Vector3.zero());
      run(pose, stack, frames: 300, anchor: Vector3(0.5, 0, 0));
      expect(pose.tailOf('h1').x, closeTo(0.5, 0.02));
    });

    test('swing on after a model that was carried away stops', () {
      // The model is moved by the frame, not by a bone. The spring runs in the
      // world, so the model's own motion shows as inertia.
      final pose = Pose(hair())..evaluate();
      final stack = ModifierStack()..add('hair', calm());
      stack.apply(pose, frame: ModifierFrame(dt: 1 / 60));

      pose.evaluate();
      stack.apply(
        pose,
        frame: ModifierFrame(
          dt: 1 / 60,
          modelToWorld: Matrix4.translationValues(0.5, 0, 0),
        ),
      );
      expect(pose.tailOf('h1').x, lessThan(-0.01));
    });

    test('hang down under gravity when nothing holds them', () {
      final pose = settle(const []);
      expect(pose.tailOf('h0').x, closeTo(1, 0.05));
      expect(pose.tailOf('h0').y, closeTo(-1, 0.05));
      expect(pose.tailOf('h1').y, closeTo(-2, 0.1));
    });

    test('keep bone lengths while they swing', () {
      final pose = settle(const []);
      expect(pose.tailOf('h0').distanceTo(pose.headOf('h0')), closeTo(1, 1e-6));
      expect(pose.tailOf('h1').distanceTo(pose.headOf('h1')), closeTo(1, 1e-6));
    });

    test('lie on a floor that cuts across the swing', () {
      // The floor is half way down. A bone hanging straight onto it has to
      // fan out across it, and cannot pass through.
      final floor = PlaneCollider(
        point: Vector3(0, -0.5, 0),
        normal: Vector3(0, 1, 0),
      );
      final pose = settle([floor]);
      expect(pose.tailOf('h0').y, closeTo(-0.5, 1e-3));
      expect(pose.tailOf('h1').y, greaterThan(-0.5 - 1e-3));
      expect(pose.tailOf('h0').distanceTo(pose.headOf('h0')), closeTo(1, 1e-6));
    });

    test('rest on a ball the swing runs into', () {
      final ball = SphereCollider(centre: Vector3(1.3, -0.9, 0), radius: 0.6);
      final pose = settle([ball]);
      final away = pose.tailOf('h0').distanceTo(Vector3(1.3, -0.9, 0));
      expect(away, closeTo(0.6, 1e-3));
    });

    test('rest on a rod the swing runs into', () {
      final rod = CapsuleCollider(
        start: Vector3(1.3, -0.9, -1),
        end: Vector3(1.3, -0.9, 1),
        radius: 0.6,
      );
      final pose = settle([rod]);
      final tail = pose.tailOf('h0');
      final away = math.sqrt(
        math.pow(tail.x - 1.3, 2) + math.pow(tail.y + 0.9, 2),
      );
      expect(away, closeTo(0.6, 1e-3));
    });

    test('keep clear by the margin', () {
      final floor = PlaneCollider(
        point: Vector3(0, -0.5, 0),
        normal: Vector3(0, 1, 0),
      );
      final pose = Pose(fringe());
      final stack = ModifierStack()
        ..add(
          'hair',
          SpringBones(
            bones: const ['h0', 'h1'],
            stiffness: 0,
            drag: 4,
            margin: 0.1,
            colliders: [floor],
          ),
        );
      frame(pose, stack);
      run(pose, stack, frames: 600);
      expect(pose.tailOf('h0').y, closeTo(-0.4, 1e-3));
    });

    test('carry a collider along with its bone', () {
      // A bone's space is turned to its own axis, so the ball is put at the
      // same place in the model as the one above by going through the bone.
      final rest = Pose(fringe())..evaluate();
      final inverse = Matrix4.inverted(rest.worldOf('anchor'));
      final ball = SphereCollider(
        bone: 'anchor',
        centre: inverse.transformed3(Vector3(1.3, -0.9, 0)),
        radius: 0.6,
      );

      final pose = settle([ball], anchor: Vector3(0, 0, 4));
      final away = pose.tailOf('h0').distanceTo(Vector3(1.3, -0.9, 4));
      expect(away, closeTo(0.6, 1e-3));
    });

    test('leave a collider where it was given', () {
      final ball = SphereCollider(centre: Vector3(1.3, -0.9, 0), radius: 0.6);
      final pose = Pose(fringe());
      final stack = ModifierStack()..add('hair', loose(colliders: [ball]));
      frame(pose, stack);
      stack.apply(
        pose,
        frame: ModifierFrame(
          dt: 1 / 60,
          modelToWorld: Matrix4.translationValues(0, 0, 3),
        ),
      );
      expect(ball.centre.z, 0);
    });

    test('hold in a frame of no time', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      frame(pose, stack, anchor: Vector3.zero());
      frame(pose, stack, anchor: Vector3(0.5, 0, 0));
      final held = pose.tailOf('h1').clone();

      frame(pose, stack, anchor: Vector3(0.5, 0, 0), dt: 0);
      expect(pose.tailOf('h1').distanceTo(held), lessThan(1e-9));
    });

    test('start again from the animation after a teleport', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      frame(pose, stack, anchor: Vector3.zero());
      frame(pose, stack, anchor: Vector3(100, 0, 0));
      expect(pose['h0'].isRest, isTrue);
      expect(pose['h1'].isRest, isTrue);
    });

    test('start again from the animation after a reset', () {
      final pose = Pose(hair());
      final stack = ModifierStack()..add('hair', calm());
      frame(pose, stack, anchor: Vector3.zero());
      frame(pose, stack, anchor: Vector3(0.5, 0, 0));
      stack.reset();
      frame(pose, stack, anchor: Vector3(0.5, 0, 0));
      expect(pose['h0'].isRest, isTrue);
    });

    test('refuse a bone listed before its parent', () {
      final pose = Pose(hair())..evaluate();
      final stack = ModifierStack()
        ..add('hair', SpringBones(bones: const ['h1', 'h0']));
      expect(() => stack.apply(pose), throwsA(isA<ArmatureError>()));
    });

    test('refuse a collider on a bone that is not there', () {
      final pose = Pose(hair())..evaluate();
      final stack = ModifierStack()
        ..add(
          'hair',
          SpringBones(
            bones: const ['h0'],
            colliders: [
              SphereCollider(bone: 'skull', centre: Vector3.zero(), radius: 1),
            ],
          ),
        );
      expect(() => stack.apply(pose), throwsA(isA<ArmatureError>()));
    });

    test('refuse a collider without a size', () {
      expect(
        () => SphereCollider(centre: Vector3.zero(), radius: -1),
        throwsArgumentError,
      );
    });
  });
}
