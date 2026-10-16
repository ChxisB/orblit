import 'dart:math' as math;
import 'dart:io';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'people.dart';

ClipDocument clipOf(List<ClipChannel<Object>> channels) =>
    ClipDocument(name: 'Move', duration: 1, channels: channels);

/// Which way [bone] points in the world: from its origin to its child's.
Vector3 pointing(Person who, String bone, String child, ClipDocument clip) {
  final frame = clip.sampleAt(0.5).bones[''] ?? {};
  final world = who.world(frame);
  final from = world[who.indexOf(bone)].getTranslation();
  final to = world[who.indexOf(child)].getTranslation();
  return (to - from).normalized();
}

void main() {
  const arm = 'mixamorig:LeftArm';
  const forearm = 'mixamorig:LeftForeArm';

  group('fixRest', () {
    late Person tpose;
    late Person apose;
    late ClipDocument raise;

    setUp(() {
      tpose = person(name: capture);
      apose = person(name: capture, armsDown: 0.7);
      raise = clipOf([
        turns(arm, [
          (0, Quaternion.identity()),
          (0.5, about(0, 0, 1, 40)),
          (1, Quaternion.identity()),
        ]),
      ]);
    });

    ClipDocument retarget({required bool fix}) => retargetClip(
      raise,
      from: tpose.skeleton,
      to: apose.skeleton,
      fixRest: fix,
    ).clip;

    test('points the target arm where the source arm points', () {
      final want = pointing(tpose, arm, forearm, raise);
      final got = pointing(apose, arm, forearm, retarget(fix: true));
      expect(got.dot(want), closeTo(1, 1e-9));
    });

    test('leaves the arm short of that without it', () {
      final want = pointing(tpose, arm, forearm, raise);
      final got = pointing(apose, arm, forearm, retarget(fix: false));
      // The arm carries 40 degrees of motion but starts 0.7 radians low.
      expect(got.dot(want), closeTo(math.cos(0.7), 1e-6));
    });

    test('gives the arm a key even when the source never moves it', () {
      final still = clipOf([
        moves('mixamorig:Hips', [
          (0, tpose.locals[tpose.indexOf('mixamorig:Hips')].$1),
          (
            1,
            tpose.locals[tpose.indexOf('mixamorig:Hips')].$1 +
                Vector3(0, 0.1, 0),
          ),
        ]),
      ]);
      final result = retargetClip(
        still,
        from: tpose.skeleton,
        to: apose.skeleton,
        fixRest: true,
      ).clip;
      final want = Vector3(1, 0, 0);
      final got = pointing(apose, arm, forearm, result);
      expect(got.dot(want), closeTo(1, 1e-9));
      final armChannel = result.channels.singleWhere(
        (channel) => channel.bone == arm && channel.property == 'rotation',
      );
      expect(armChannel.keys, hasLength(1));
    });

    test('adds nothing between two skeletons that rest the same', () {
      final twin = person(name: capture);
      final plain = retargetClip(
        raise,
        from: tpose.skeleton,
        to: twin.skeleton,
      ).clip;
      final fixed = retargetClip(
        raise,
        from: tpose.skeleton,
        to: twin.skeleton,
        fixRest: true,
      ).clip;
      expect(fixed.channels, hasLength(plain.channels.length));
      for (var i = 0; i < plain.channels.length; i++) {
        expect(fixed.channels[i].bone, plain.channels[i].bone);
        expect(
          fixed.channels[i].keys,
          hasLength(plain.channels[i].keys.length),
        );
      }
    });

    test('says so when a bone points the opposite way to its driver', () {
      final backwards = person(name: capture, armsDown: 3.0);
      final result = retargetClip(
        raise,
        from: tpose.skeleton,
        to: backwards.skeleton,
        fixRest: true,
      );
      expect(
        result.problems.where((text) => text.contains('LeftArm')),
        isNotEmpty,
      );
    });
  });

  group('humanoid', () {
    test('carries a clip between rigs that share no names', () {
      final from = person(spine: 3);
      final to = person(height: 1.2, rightFirst: true, spine: 1);
      final clip = clipOf([
        turns(from.slots[HumanoidBone.leftUpperArm]!, [
          (0, Quaternion.identity()),
          (0.5, about(0, 0, 1, 50)),
          (1, Quaternion.identity()),
        ]),
      ]);
      final result = retargetClip(
        clip,
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      );
      expect(result.problems, isEmpty);
      final upper = to.slots[HumanoidBone.leftUpperArm]!;
      final lower = to.slots[HumanoidBone.leftLowerArm]!;
      final got = pointing(to, upper, lower, result.clip);
      final want = pointing(
        from,
        from.slots[HumanoidBone.leftUpperArm]!,
        from.slots[HumanoidBone.leftLowerArm]!,
        clip,
      );
      expect(got.dot(want), closeTo(1, 1e-9));
    });

    test('says what was not a person, in the source or the target', () {
      final chain = RestSkeleton(
        names: ['a', 'b', 'c'],
        parents: [-1, 0, 1],
        local: [
          for (var i = 0; i < 3; i++)
            Matrix4.translation(Vector3(0, i * 0.3, 0)),
        ],
      );
      final result = retargetClip(
        clipOf([]),
        from: chain,
        to: person(name: capture).skeleton,
        humanoid: true,
      );
      expect(result.problems, contains(startsWith('In the source skeleton: ')));
      expect(
        result.problems.where((text) => text.startsWith('In the target')),
        isEmpty,
      );
    });
  });

  group('a real pair', () {
    final cesium = File('../../assets/samples/CesiumMan.glb');
    final figure = File('../../assets/samples/RiggedFigure.glb');

    test(
      'baked, keeps every joint within a few centimetres of the source',
      () {
        final bytes = cesium.readAsBytesSync();
        final from = restSkeletonsFromGltf(bytes).first;
        final to = restSkeletonsFromGltf(figure.readAsBytesSync()).first;
        final clip = clipsFromGltf(bytes).clips.first;
        final result = retargetClip(clip, from: from, to: to, humanoid: true);
        expect(result.problems, isEmpty);

        final size = to.reach / from.reach;
        final pairs = matchHumanoid(from, to);
        var worst = 0.0;
        for (var at = 0.0; at <= clip.duration; at += clip.duration / 8) {
          final source = posedWorld(from, clip.sampleAt(at).bones['']!);
          final moved = posedWorld(to, result.clip.sampleAt(at).bones['']!);
          final shift = source[0].getTranslation();
          final home = moved[0].getTranslation();
          for (final MapEntry(key: mine, value: theirs) in pairs.entries) {
            final there = source[from.indexOf(theirs)!].getTranslation();
            final here = moved[to.indexOf(mine)!].getTranslation();
            final off = (here - home) - (there - shift) * size;
            if (off.length > worst) worst = off.length;
          }
        }
        expect(worst, lessThan(0.06));
      },
      skip: cesium.existsSync() && figure.existsSync()
          ? false
          : 'CesiumMan.glb or RiggedFigure.glb is not fetched',
    );
  });
}
