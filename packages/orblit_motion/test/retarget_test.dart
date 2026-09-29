import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

Vector3 v(double x, double y, double z) => Vector3(x, y, z);

/// A turn tipped about all three axes, so that a wrong order of
/// multiplication shows up. A quarter turn about one axis hides it.
Quaternion tip(double x, double y, double z) =>
    (Quaternion.axisAngle(Vector3(1, 0, 0), radians(x)) *
          Quaternion.axisAngle(Vector3(0, 1, 0), radians(y)) *
          Quaternion.axisAngle(Vector3(0, 0, 1), radians(z)))
      ..normalize();

Matrix3 turnOf(Matrix4 matrix) {
  final turn = Quaternion.identity();
  matrix.decompose(Vector3.zero(), turn, Vector3.zero());
  return turn.asRotationMatrix();
}

/// A skeleton written out by hand, and the forward kinematics to pose it, so
/// that the checks never go through the code under test.
final class Rig {
  Rig(this.names, this.parents, this.rest, {Matrix4? above})
    : above = above ?? Matrix4.identity();

  final List<String> names;
  final List<int> parents;
  final List<(Vector3, Quaternion)> rest;
  final Matrix4 above;

  RestSkeleton get skeleton => RestSkeleton(
    names: names,
    parents: parents,
    local: [
      for (final (position, turn) in rest)
        Matrix4.compose(position, turn, Vector3.all(1)),
    ],
    above: above,
  );

  /// Every bone in the world with [clip] at [time], or at rest with none.
  List<Matrix4> pose([ClipDocument? clip, double time = 0]) {
    final frame = clip?.sampleAt(time);
    final out = <Matrix4>[];
    for (var i = 0; i < names.length; i++) {
      final bone = frame?.boneOf('', names[i]);
      final local = Matrix4.compose(
        bone?.position ?? rest[i].$1,
        bone?.rotation ?? rest[i].$2,
        bone?.scale ?? Vector3.all(1),
      );
      out.add((parents[i] < 0 ? above : out[parents[i]]).multiplied(local));
    }
    return out;
  }

  /// How far the farthest bone is from where the roots hang, at rest.
  double get reach {
    final origin = above.getTranslation();
    return pose()
        .map((world) => world.getTranslation().distanceTo(origin))
        .reduce(math.max);
  }
}

Rig source() => Rig(
  ['hips', 'spine', 'chest', 'head', 'armL', 'handL'],
  [-1, 0, 1, 2, 2, 4],
  [
    (v(0, 1, 0), Quaternion.identity()),
    (v(0, 0.2, 0), tip(5, 0, 0)),
    (v(0, 0.3, 0), tip(0, 10, 0)),
    (v(0, 0.25, 0), Quaternion.identity()),
    (v(0.2, 0.2, 0), tip(0, 0, -90)),
    (v(0, 0.3, 0), Quaternion.identity()),
  ],
);

/// The same body half as big again, with every bone rest-turned another way
/// and an armature above it standing a Z-up rig upright. [spine] is the name
/// of its spine bone, or null for a body with none.
Rig target({String? spine = 'spine'}) {
  final bones = [
    ('hips', -1, v(0, 1.5, 0), tip(0, 15, 0)),
    if (spine != null) (spine, 0, v(0, 0.3, 0), tip(-8, 0, 10)),
    ('chest', spine == null ? 0 : 1, v(0, 0.45, 0), tip(0, -20, 0)),
    ('head', spine == null ? 1 : 2, v(0, 0.4, 0), tip(10, 0, 0)),
    ('armL', spine == null ? 1 : 2, v(0.3, 0.3, 0), tip(20, 0, -70)),
    ('handL', spine == null ? 3 : 4, v(0, 0.45, 0), tip(0, 30, 0)),
  ];
  return Rig(
    [for (final bone in bones) bone.$1],
    [for (final bone in bones) bone.$2],
    [for (final bone in bones) (bone.$3, bone.$4)],
    above: Matrix4.compose(
      Vector3.zero(),
      Quaternion.axisAngle(Vector3(1, 0, 0), -math.pi / 2),
      Vector3.all(1),
    ),
  );
}

ClipChannel<Quaternion> turns(
  String bone,
  List<(double, Quaternion)> keys, {
  Hold hold = Hold.linear,
}) => ClipChannel<Quaternion>(
  target: '',
  bone: bone,
  property: 'rotation',
  kind: ChannelKind.rotation,
  keys: [for (final (time, turn) in keys) Key(time, turn, hold: hold)],
);

ClipChannel<Vector3> moves(
  String bone,
  String property,
  List<(double, Vector3)> keys,
) => ClipChannel<Vector3>(
  target: '',
  bone: bone,
  property: property,
  kind: ChannelKind.vector,
  keys: [for (final (time, value) in keys) Key(time, value, hold: Hold.linear)],
);

/// The hips, spine, chest, head and left arm at four moments, and one mark.
ClipDocument walk() => ClipDocument(
  name: 'Walk',
  duration: 1.5,
  channels: [
    turns('hips', [
      (0, tip(0, 0, 0)),
      (0.4, tip(10, 25, -5)),
      (0.9, tip(-15, 40, 20)),
      (1.5, tip(0, 0, 0)),
    ], hold: Hold.smooth),
    turns('spine', [
      (0, tip(0, 0, 0)),
      (0.4, tip(20, 40, 0)),
      (0.9, tip(-10, 15, 30)),
      (1.5, tip(0, 0, 0)),
    ]),
    turns('chest', [
      (0, tip(0, 0, 0)),
      (0.4, tip(30, -20, 10)),
      (0.9, tip(0, 30, -20)),
      (1.5, tip(0, 0, 0)),
    ]),
    turns('head', [(0, tip(0, 0, 0)), (0.4, tip(20, 40, 0))]),
    turns('armL', [
      (0, tip(0, 0, -90)),
      (0.4, tip(40, 20, -60)),
      (0.9, tip(-30, 50, -120)),
      (1.5, tip(0, 0, -90)),
    ]),
  ],
  marks: [const Mark(0.4, 'step')],
);

/// Every bone of [to] that [from] has a bone of the same name for stands in
/// the world where that bone did, measured from where each rests.
void expectSamePose({
  required Rig from,
  required Rig to,
  required ClipDocument clip,
  required ClipDocument retargeted,
  required List<double> times,
  double within = 1e-9,
}) {
  final fromRest = from.pose();
  final toRest = to.pose();
  for (final time in times) {
    final fromPose = from.pose(clip, time);
    final toPose = to.pose(retargeted, time);
    for (var i = 0; i < to.names.length; i++) {
      final s = from.names.indexOf(to.names[i]);
      if (s < 0) continue;
      final offset = turnOf(fromRest[s]).clone()
        ..transpose()
        ..multiply(turnOf(toRest[i]));
      final expected = turnOf(fromPose[s]).multiplied(offset);
      final actual = turnOf(toPose[i]);
      for (var e = 0; e < 9; e++) {
        expect(
          actual.storage[e],
          closeTo(expected.storage[e], within),
          reason: '${to.names[i]} at $time',
        );
      }
    }
  }
}

RestSkeleton chain(List<String> names) => RestSkeleton(
  names: names,
  parents: [for (var i = 0; i < names.length; i++) i - 1],
  local: [for (final _ in names) Matrix4.identity()],
);

Quaternion turnAt(ClipDocument clip, String bone, int key) =>
    clip.channelFor('', 'rotation', bone: bone)!.keys[key].value as Quaternion;

void main() {
  test('multiplies turns in the order matrices do', () {
    final a = tip(20, 40, 0);
    final b = tip(-30, 10, 50);
    final product = (a * b).asRotationMatrix();
    final expected = a.asRotationMatrix().multiplied(b.asRotationMatrix());
    for (var e = 0; e < 9; e++) {
      expect(product.storage[e], closeTo(expected.storage[e], 1e-12));
    }
  });

  group('RestSkeleton', () {
    test('stands bones in the world through their parents', () {
      final rig = target();
      final world = rig.pose();
      final skeleton = rig.skeleton;
      for (var i = 0; i < rig.names.length; i++) {
        final expected = turnOf(world[i]);
        final actual = skeleton.worldRotationOf(i).asRotationMatrix();
        for (var e = 0; e < 9; e++) {
          expect(actual.storage[e], closeTo(expected.storage[e], 1e-9));
        }
      }
    });

    test('knows its own bones by name', () {
      final skeleton = source().skeleton;
      expect(skeleton.length, 6);
      expect(skeleton.indexOf('head'), 3);
      expect(skeleton.indexOf('tail'), isNull);
    });

    test('measures how big it is from where the roots hang', () {
      final short = source();
      final tall = target();
      expect(short.skeleton.reach, closeTo(short.reach, 1e-9));
      expect(tall.skeleton.reach, closeTo(tall.reach, 1e-9));
      expect(tall.skeleton.reach, greaterThan(short.skeleton.reach));
    });

    test('refuses a parent that does not come first', () {
      expect(
        () => RestSkeleton(
          names: ['a', 'b'],
          parents: [1, -1],
          local: [Matrix4.identity(), Matrix4.identity()],
        ),
        throwsArgumentError,
      );
    });

    test('refuses two bones with one name', () {
      expect(
        () => RestSkeleton(
          names: ['a', 'a'],
          parents: [-1, 0],
          local: [Matrix4.identity(), Matrix4.identity()],
        ),
        throwsArgumentError,
      );
    });

    test('refuses lists that are not the same length', () {
      expect(
        () => RestSkeleton(
          names: ['a', 'b'],
          parents: [-1],
          local: [Matrix4.identity(), Matrix4.identity()],
        ),
        throwsArgumentError,
      );
    });
  });

  group('matchBones', () {
    test('matches the same name, then the same name once cleaned', () {
      final from = chain([
        'mixamorig:Hips',
        'mixamorig:Spine',
        'mixamorig:LeftArm',
        'mixamorig:RightArm',
        'mixamorig:LeftHand',
        'Tail',
      ]);
      final to = chain([
        'Hips',
        'spine',
        'arm.L',
        'arm.R',
        'hand_L',
        'Tail',
        'Wing.L',
      ]);
      expect(matchBones(from, to), {
        'Hips': 'mixamorig:Hips',
        'spine': 'mixamorig:Spine',
        'arm.L': 'mixamorig:LeftArm',
        'arm.R': 'mixamorig:RightArm',
        'hand_L': 'mixamorig:LeftHand',
        'Tail': 'Tail',
      });
    });

    test('keeps left and right apart', () {
      final from = chain(['hand.L', 'hand.R']);
      final to = chain(['RightHand', 'LeftHand']);
      expect(matchBones(from, to), {
        'RightHand': 'hand.R',
        'LeftHand': 'hand.L',
      });
    });

    test('leaves out a name that two bones share once cleaned', () {
      final from = chain(['DEF-arm.L', 'ORG-arm.L', 'hips']);
      final to = chain(['arm.L', 'hips']);
      expect(matchBones(from, to), {'hips': 'hips'});
    });
  });

  group('retargetClip', () {
    test('stands each bone where its driver stood, at any moment', () {
      final from = source();
      final to = target();
      final clip = walk();
      final result = retargetClip(clip, from: from.skeleton, to: to.skeleton);
      expect(result.problems, isEmpty);
      expectSamePose(
        from: from,
        to: to,
        clip: clip,
        retargeted: result.clip,
        times: [0, 0.1, 0.4, 0.65, 0.9, 1.2, 1.5, 2],
      );
    });

    test('keeps its keys and its holds when the bones line up', () {
      final result = retargetClip(
        walk(),
        from: source().skeleton,
        to: target().skeleton,
      );
      for (final name in ['hips', 'chest']) {
        final made = result.clip.channelFor('', 'rotation', bone: name)!;
        final original = walk().channelFor('', 'rotation', bone: name)!;
        expect(made.keys.map((key) => key.at), [0, 0.4, 0.9, 1.5]);
        expect(
          made.keys.map((key) => key.hold),
          original.keys.map((key) => key.hold),
        );
      }
    });

    test('carries cubic slopes through as it carries the values', () {
      final from = source();
      final to = target();
      final clip = ClipDocument(
        name: 'Swing',
        duration: 1,
        channels: [
          ClipChannel<Quaternion>(
            target: '',
            bone: 'armL',
            property: 'rotation',
            kind: ChannelKind.rotation,
            keys: [
              Key(
                0,
                tip(0, 0, -90),
                hold: Hold.curve,
                slopeOut: Quaternion(0.3, -0.2, 0.5, 0.1),
              ),
              Key(
                0.5,
                tip(40, 20, -60),
                hold: Hold.curve,
                slopeIn: Quaternion(-0.4, 0.1, 0.2, 0.3),
                slopeOut: Quaternion(0.1, 0.4, -0.3, 0.2),
              ),
              Key(
                1,
                tip(-30, 50, -120),
                hold: Hold.curve,
                slopeIn: Quaternion(0.2, 0.2, -0.1, -0.4),
              ),
            ],
          ),
        ],
      );
      final result = retargetClip(clip, from: from.skeleton, to: to.skeleton);
      expectSamePose(
        from: from,
        to: to,
        clip: clip,
        retargeted: result.clip,
        times: [0, 0.13, 0.37, 0.5, 0.71, 0.98, 1],
      );
    });

    test('works out a bone the target has no parent for at its keys', () {
      final from = source();
      final to = target(spine: null);
      final clip = walk();
      final result = retargetClip(clip, from: from.skeleton, to: to.skeleton);
      expectSamePose(
        from: from,
        to: to,
        clip: clip,
        retargeted: result.clip,
        times: [0, 0.4, 0.9, 1.5],
      );
      final chest = result.clip.channelFor('', 'rotation', bone: 'chest')!;
      expect(chest.keys.map((key) => key.at), [0, 0.4, 0.9, 1.5]);
      expect(result.problems, [contains('"spine"')]);
    });

    test('stays within a few degrees between the keys of a worked-out bone', () {
      final from = source();
      final to = target(spine: null);
      final clip = walk();
      final result = retargetClip(clip, from: from.skeleton, to: to.skeleton);
      expectSamePose(
        from: from,
        to: to,
        clip: clip,
        retargeted: result.clip,
        times: [0.2, 0.65, 1.2],
        // Straight lines between keys, so the middle of a span is a little off.
        within: 0.1,
      );
    });

    test('keeps a bone under a parent nothing drives where it rests', () {
      final from = source();
      final to = target(spine: 'lumbar');
      final clip = walk();
      final result = retargetClip(clip, from: from.skeleton, to: to.skeleton);
      expect(result.clip.channelFor('', 'rotation', bone: 'lumbar'), isNull);
      expectSamePose(
        from: from,
        to: to,
        clip: clip,
        retargeted: result.clip,
        times: [0, 0.4, 0.9, 1.5],
      );
    });

    test('leaves a bone the clip never moves standing as it rests', () {
      final from = source();
      final to = target();
      final result = retargetClip(walk(), from: from.skeleton, to: to.skeleton);
      expect(result.clip.channelFor('', 'rotation', bone: 'handL'), isNull);
      expect(result.clip.channelFor('', 'rotation', bone: 'head'), isNotNull);
    });

    test('moves the hips as much further as the target is bigger', () {
      final from = source();
      final to = target();
      final clip = ClipDocument(
        name: 'Bob',
        duration: 1,
        channels: [
          moves('hips', 'position', [
            (0, v(0, 1, 0)),
            (0.5, v(0.4, 0.9, 1.5)),
            (1, v(0, 1, 3)),
          ]),
        ],
      );
      final result = retargetClip(clip, from: from.skeleton, to: to.skeleton);
      final size = to.reach / from.reach;
      final fromRest = from.pose()[0].getTranslation();
      final toRest = to.pose()[0].getTranslation();
      for (final time in [0.0, 0.25, 0.5, 0.8, 1.0]) {
        final moved = from.pose(clip, time)[0].getTranslation() - fromRest;
        final carried = to.pose(result.clip, time)[0].getTranslation() - toRest;
        expect((carried - moved * size).length, closeTo(0, 1e-9));
      }
    });

    test('leaves out a position that stays at rest', () {
      final clip = ClipDocument(
        name: 'Held',
        duration: 1,
        channels: [
          moves('spine', 'position', [(0, v(0, 0.2, 0)), (1, v(0, 0.2, 0))]),
        ],
      );
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: target().skeleton,
      );
      expect(result.clip.channels, isEmpty);
    });

    test('carries a scale as the change from rest', () {
      final clip = ClipDocument(
        name: 'Squash',
        duration: 1,
        channels: [
          moves('chest', 'scale', [(0, v(1, 1, 1)), (1, v(1.5, 0.5, 1))]),
        ],
      );
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: target().skeleton,
      );
      final made = result.clip.channelFor('', 'scale', bone: 'chest')!;
      final last = made.keys.last.value as Vector3;
      expect(last.x, closeTo(1.5, 1e-12));
      expect(last.y, closeTo(0.5, 1e-12));
      expect(last.z, closeTo(1, 1e-12));
    });

    test('uses the bones it is told to', () {
      final from = source();
      final renamed = Rig(
        ['pelvis', 'back', 'ribs', 'skull', 'shoulder', 'palm'],
        from.parents,
        target().rest,
      );
      final result = retargetClip(
        walk(),
        from: from.skeleton,
        to: renamed.skeleton,
        bones: {
          'pelvis': 'hips',
          'back': 'spine',
          'ribs': 'chest',
          'skull': 'head',
          'shoulder': 'armL',
          'palm': 'handL',
        },
      );
      expect(result.clip.channelFor('', 'rotation', bone: 'skull'), isNotNull);
      expect(result.clip.channelFor('', 'rotation', bone: 'head'), isNull);
      expect(result.problems, isEmpty);
    });

    test('refuses a bone neither skeleton has', () {
      expect(
        () => retargetClip(
          walk(),
          from: source().skeleton,
          to: target().skeleton,
          bones: {'wing': 'hips'},
        ),
        throwsArgumentError,
      );
    });

    test('keeps what is not a bone, and the marks, name and length', () {
      final clip = walk().copyWith(
        channels: [
          ...walk().channels,
          ClipChannel<Vector3>(
            target: 'door',
            property: 'transform.position',
            kind: ChannelKind.vector,
            keys: [Key(0, v(0, 0, 0)), Key(1, v(1, 0, 0))],
          ),
        ],
      );
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: target().skeleton,
      );
      expect(result.clip.channelFor('door', 'transform.position'), isNotNull);
      expect(result.clip.duration, 1.5);
      expect(result.clip.marks.map((mark) => mark.name), ['step']);
      expect(result.clip.name, 'Walk');
    });

    test('does not touch the bones of another target', () {
      final clip = ClipDocument(
        name: 'Two',
        duration: 1,
        channels: [
          ClipChannel<Quaternion>(
            target: 'other',
            bone: 'hips',
            property: 'rotation',
            kind: ChannelKind.rotation,
            keys: [Key(0, tip(0, 0, 0)), Key(1, tip(10, 20, 30))],
          ),
        ],
      );
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: target().skeleton,
      );
      final made = result.clip.channelFor('other', 'rotation', bone: 'hips')!;
      expect((made.keys.last.value as Quaternion).x, tip(10, 20, 30).x);
      expect(result.problems, isEmpty);
    });

    test('says which motion it could not carry', () {
      final clip = walk().copyWith(
        channels: [
          ...walk().channels,
          turns('tail', [(0, tip(0, 0, 0)), (1, tip(10, 0, 0))]),
        ],
      );
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: chain(['hips', 'spine']),
      );
      expect(result.problems, [
        startsWith('The clip moves "tail"'),
        allOf(contains('"chest"'), contains('"armL"')),
      ]);
    });

    test('stays quiet about root motion a clip never had', () {
      final result = retargetClip(
        walk(),
        from: source().skeleton,
        to: chain(['a']),
      );
      expect(result.clip.rootMotion, isNull);
      expect(result.problems.any((line) => line.contains('root')), isFalse);
    });

    test('moves root motion to the bone that carries it', () {
      final clip = walk().copyWith(
        rootMotion: RootMotion(bone: 'hips', turns: true, rises: true),
      );
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: target().skeleton,
      );
      final root = result.clip.rootMotion!;
      expect(root.bone, 'hips');
      expect(root.turns, isTrue);
      expect(root.rises, isTrue);
      // The target's armature stands its Z axis up, so up is Z on the way in.
      expect(root.up.x, closeTo(0, 1e-9));
      expect(root.up.y, closeTo(0, 1e-9));
      expect(root.up.z, closeTo(1, 1e-9));
    });

    test('follows root motion to a renamed bone', () {
      final from = source();
      final renamed = Rig(
        ['pelvis', ...from.names.skip(1)],
        from.parents,
        from.rest,
      );
      final clip = walk().copyWith(rootMotion: RootMotion(bone: 'hips'));
      final result = retargetClip(
        clip,
        from: from.skeleton,
        to: renamed.skeleton,
        bones: {'pelvis': 'hips'},
      );
      expect(result.clip.rootMotion!.bone, 'pelvis');
    });

    test('drops root motion it has no bone to carry', () {
      final clip = walk().copyWith(rootMotion: RootMotion(bone: 'hips'));
      final result = retargetClip(
        clip,
        from: source().skeleton,
        to: chain(['a']),
      );
      expect(result.clip.rootMotion, isNull);
      expect(result.problems.last, contains('root motion'));
    });

    test('turns a clip that stays at rest into one that stays at rest', () {
      final from = source();
      final to = target();
      final still = ClipDocument(
        name: 'Still',
        duration: 1,
        channels: [
          for (var i = 0; i < from.names.length; i++)
            turns(from.names[i], [(0, from.rest[i].$2), (1, from.rest[i].$2)]),
        ],
      );
      final result = retargetClip(still, from: from.skeleton, to: to.skeleton);
      expectSamePose(
        from: from,
        to: to,
        clip: still,
        retargeted: result.clip,
        times: [0, 0.5],
      );
      final rest = to.pose();
      final posed = to.pose(result.clip, 0.5);
      for (var i = 0; i < rest.length; i++) {
        expect(
          posed[i].getTranslation().distanceTo(rest[i].getTranslation()),
          lessThan(1e-9),
        );
      }
      expect(turnAt(result.clip, 'chest', 0).length, closeTo(1, 1e-12));
    });
  });

  group('restSkeletonsFromGltf', () {
    Uint8List file(Map<String, Object?> document) => Uint8List.fromList(
      utf8.encode(
        jsonEncode({
          'asset': {'version': '2.0'},
          ...document,
        }),
      ),
    );

    test('reads a skin as bones, parents first', () {
      final bytes = file({
        'nodes': [
          {
            'name': 'Armature',
            'rotation': [-0.7071068, 0, 0, 0.7071068],
            'scale': [0.01, 0.01, 0.01],
            'children': [1],
          },
          {
            'name': 'Hips',
            'translation': [0, 100, 0],
            'children': [2],
          },
          {
            'name': 'Spine',
            'matrix': [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 20, 0, 1],
          },
        ],
        // The child comes first, which a file is free to do.
        'skins': [
          {
            'joints': [2, 1],
          },
        ],
      });
      final skeleton = restSkeletonsFromGltf(bytes).single;
      expect(skeleton.names, ['Hips', 'Spine']);
      expect(skeleton.parents, [-1, 0]);
      expect(skeleton.positionOf(0).y, closeTo(100, 1e-9));
      expect(skeleton.positionOf(1).y, closeTo(20, 1e-9));
      expect(skeleton.reach, closeTo(1.2, 1e-9));
      final up = skeleton.parentWorldRotationOf(0).asRotationMatrix();
      expect(up.transformed(Vector3(0, 1, 0)).z, closeTo(-1, 1e-6));
    });

    test('names bones as the clips do', () {
      final bytes = file({
        'nodes': [
          {
            'name': 'A',
            'children': [1],
          },
          {'name': 'A'},
        ],
        'skins': [
          {
            'joints': [0, 1],
          },
        ],
      });
      expect(restSkeletonsFromGltf(bytes).single.names, ['A', 'A.001']);
    });

    test('has nothing to say about a file with no skin', () {
      final bytes = file({
        'nodes': [<String, Object?>{}],
      });
      expect(restSkeletonsFromGltf(bytes), isEmpty);
    });

    test('refuses bytes that are not glTF', () {
      expect(
        () => restSkeletonsFromGltf(Uint8List.fromList([1, 2, 3])),
        throwsA(isA<ClipFormatException>()),
      );
    });
  });
}
