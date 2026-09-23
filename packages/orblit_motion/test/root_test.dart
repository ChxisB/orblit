import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' show TransformComponent;
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

final Vector3 up = Vector3(0, 1, 0);

Quaternion yaw(double degrees) => Quaternion.axisAngle(up, radians(degrees));

Quaternion pitch(double degrees) =>
    Quaternion.axisAngle(Vector3(1, 0, 0), radians(degrees));

void expectVector(Vector3? actual, Vector3 expected) {
  expect(actual, isNotNull);
  expect(
    (actual! - expected).length,
    lessThan(1e-9),
    reason: '$actual is not $expected',
  );
}

/// The same turn: a rotation and its negation turn a thing alike.
void expectTurn(Quaternion? actual, Quaternion expected) {
  expect(actual, isNotNull);
  final dot =
      actual!.x * expected.x +
      actual.y * expected.y +
      actual.z * expected.z +
      actual.w * expected.w;
  expect(dot.abs(), closeTo(1, 1e-9), reason: '$actual is not $expected');
}

/// A second of walking two metres along Z, bobbing up in the middle, with
/// its hips turning through [turn] degrees as it goes and leaning [lean].
ClipDocument walk({
  bool rises = false,
  bool turns = false,
  double turn = 0,
  double lean = 0,
  WhenDone whenDone = WhenDone.loop,
}) => ClipDocument(
  name: 'Walk',
  duration: 1,
  whenDone: whenDone,
  rootMotion: RootMotion(bone: 'hips', rises: rises, turns: turns),
  channels: [
    ClipChannel<Vector3>(
      target: '',
      bone: 'hips',
      property: 'position',
      kind: ChannelKind.vector,
      keys: [
        Key(0, Vector3(0, 1, 0), hold: Hold.linear),
        Key(0.5, Vector3(0, 1.2, 1), hold: Hold.linear),
        Key(1, Vector3(0, 1, 2)),
      ],
    ),
    ClipChannel<Quaternion>(
      target: '',
      bone: 'hips',
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [
        Key(0, pitch(lean), hold: Hold.linear),
        Key(1, yaw(turn) * pitch(lean)),
      ],
    ),
  ],
);

void main() {
  group('across the ground', () {
    test('goes to the character, and the bob stays in the pose', () {
      final clip = walk();
      expectVector(clip.rootStep(0, 1).position, Vector3(0, 0, 2));
      expectVector(clip.rootStep(0, 0.5).position, Vector3(0, 0, 1));
      expectTurn(clip.rootStep(0, 1).rotation, Quaternion.identity());

      final held = clip.sampleAt(0.5, inPlace: true).boneOf('', 'hips')!;
      expectVector(held.position, Vector3(0, 1.2, 0));
      final authored = clip.sampleAt(0.5).boneOf('', 'hips')!;
      expectVector(authored.position, Vector3(0, 1.2, 1));
    });

    test('takes up and down too when the root rises', () {
      final clip = walk(rises: true);
      expectVector(clip.rootStep(0, 0.5).position, Vector3(0, 0.2, 1));
      final held = clip.sampleAt(0.5, inPlace: true).boneOf('', 'hips')!;
      expectVector(held.position, Vector3.zero());
    });

    test('is measured along the up the clip names', () {
      final clip = walk().copyWith(
        rootMotion: RootMotion(bone: 'hips', up: Vector3(0, 0, 2)),
      );
      // With Z up, walking along Z is climbing, and the bob is the walk.
      expectVector(clip.rootStep(0, 0.5).position, Vector3(0, 0.2, 0));
    });

    test('goes backwards when played backwards', () {
      final clip = walk();
      expectVector(clip.rootStep(1, 0.5).position, Vector3(0, 0, -1));
    });
  });

  group('turning', () {
    test('stays in the pose unless the root turns', () {
      final clip = walk(turn: 90);
      expectTurn(clip.rootStep(0, 1).rotation, Quaternion.identity());
      final held = clip.sampleAt(1, inPlace: true).boneOf('', 'hips')!;
      expectTurn(held.rotation, yaw(90));
    });

    test('goes to the character when it does, the lean kept', () {
      final clip = walk(turns: true, turn: 90, lean: 20);
      expectTurn(clip.rootStep(0, 1).rotation, yaw(90));
      expectTurn(clip.rootStep(0, 0.5).rotation, yaw(45));
      for (final at in [0.0, 0.3, 1.0]) {
        final held = clip.sampleAt(at, inPlace: true).boneOf('', 'hips')!;
        expectTurn(held.rotation, pitch(20));
      }
    });

    test('carries each step forward the way the character now faces', () {
      final clip = walk(turns: true, turn: 90);
      // From half way, the hips face 45 degrees round towards X, so what is
      // straight along Z in the clip is forward and to the character's -X.
      final step = clip.rootStep(0.5, 1);
      expectVector(step.position, Vector3(-math.sqrt1_2, 0, math.sqrt1_2));
      expectTurn(step.rotation, yaw(45));
    });

    test('two steps one after the other are the step over both', () {
      final clip = walk(turns: true, turn: 120, lean: 10);
      final whole = clip.rootStep(0.1, 0.9);
      final parts = clip.rootStep(0.1, 0.4).then(clip.rootStep(0.4, 0.9));
      expectVector(parts.position, whole.position);
      expectTurn(parts.rotation, whole.rotation);
    });

    test('a root on its back has no heading to give', () {
      final clip = walk(turns: true).copyWith(
        channels: [
          ClipChannel<Quaternion>(
            target: '',
            bone: 'hips',
            property: 'rotation',
            kind: ChannelKind.rotation,
            keys: [
              Key(0, pitch(180), hold: Hold.linear),
              Key(1, pitch(180)),
            ],
          ),
        ],
      );
      expectTurn(clip.rootStep(0, 1).rotation, Quaternion.identity());
    });
  });

  group('played', () {
    test('a loop that wraps mid-step still takes the whole step', () {
      final player = ClipPlayer(walk())..play();
      player.advance(0.75);
      final wrapped = player.advance(0.5);
      expectVector(wrapped.moved.position, Vector3(0, 0, 1));
      expect(player.at, closeTo(0.25, 1e-12));
      final held = wrapped.frame.boneOf('', 'hips')!;
      expectVector(held.position, Vector3(0, 1.1, 0));
    });

    test('a turning loop goes on from where the last lap left it', () {
      final clip = walk(turns: true, turn: 90);
      final player = ClipPlayer(clip)..play();
      final moved = player.advance(1.5).moved;
      final expected = clip.rootStep(0, 1).then(clip.rootStep(0, 0.5));
      expectVector(moved.position, expected.position);
      expectTurn(moved.rotation, yaw(135));
    });

    test('backwards walks backwards', () {
      final player = ClipPlayer(walk(), speed: -1)
        ..seek(1)
        ..play();
      expectVector(player.advance(0.5).moved.position, Vector3(0, 0, -1));
    });

    test('a bounce comes back the way it went', () {
      final player = ClipPlayer(walk(), whenDone: WhenDone.bounce)..play();
      expectVector(player.advance(1.5).moved.position, Vector3(0, 0, 1));
    });

    test('a step many laps long goes the whole way', () {
      final player = ClipPlayer(walk())..play();
      final moved = player.advance(1000.25).moved;
      expect(moved.position.z, closeTo(2000.5, 1e-6));
      expect(moved.position.x.abs() + moved.position.y.abs(), lessThan(1e-9));
    });

    test('a turning step many laps long turns the whole way', () {
      // A quarter turn a lap: a thousand laps is two hundred and fifty
      // times round, and back where it started facing.
      final clip = walk(turns: true, turn: 90);
      final player = ClipPlayer(clip)..play();
      final moved = player.advance(1000).moved;
      expectTurn(moved.rotation, Quaternion.identity());
    });
  });

  test('a clip with no root motion goes nowhere and holds nothing', () {
    final clip = walk().copyWith(clearRootMotion: true);
    expect(clip.rootStep(0, 1).isNone, isTrue);
    final frame = clip.sampleAt(0.5, inPlace: true);
    expectVector(frame.boneOf('', 'hips')!.position, Vector3(0, 1.2, 1));
  });

  test('an entity root can turn in degrees', () {
    final clip = ClipDocument(
      name: 'Cart',
      duration: 1,
      rootMotion: RootMotion(target: 'cart', turns: true),
      channels: [
        ClipChannel<Vector3>(
          target: 'cart',
          property: 'transform.position',
          kind: ChannelKind.vector,
          keys: [
            Key(0, Vector3.zero(), hold: Hold.linear),
            Key(1, Vector3(4, 1, 0)),
          ],
        ),
        // Pitched, then turned: Z, then Y, then X is the order a transform
        // composes, so the X is applied first.
        ClipChannel<Vector3>(
          target: 'cart',
          property: 'transform.rotation',
          kind: ChannelKind.vector,
          keys: [
            Key(0, Vector3(10, 0, 0), hold: Hold.linear),
            Key(1, Vector3(10, 60, 0)),
          ],
        ),
      ],
    );

    final step = clip.rootStep(0, 1);
    expectVector(step.position, Vector3(4, 0, 0));
    expectTurn(step.rotation, yaw(60));

    final frame = clip.sampleAt(1, inPlace: true);
    expectVector(
      frame.valueOf('cart', 'transform.position')! as Vector3,
      Vector3(0, 1, 0),
    );
    final degrees = frame.valueOf('cart', 'transform.rotation')! as Vector3;
    expectTurn(
      Quaternion.fromRotation(TransformComponent.rotationOf(degrees)),
      pitch(10),
    );
  });

  test('steps compose the way a character takes them', () {
    final first = RootStep(position: Vector3(0, 0, 1), rotation: yaw(90));
    final second = RootStep(position: Vector3(0, 0, 1));
    final both = first.then(second);
    // A quarter turn about Y takes Z to X.
    expectVector(both.position, Vector3(1, 0, 1));
    expectTurn(both.rotation, yaw(90));
    expect(RootStep().isNone, isTrue);
    expect(first.isNone, isFalse);
  });
}
