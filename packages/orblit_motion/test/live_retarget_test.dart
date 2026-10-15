import 'dart:io';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_rig/orblit_rig.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'people.dart';

/// A stride, an arm swing and a bob of the hips, on the bones of [who].
ClipDocument stride(Person who) {
  final hips = who.slots[HumanoidBone.hips]!;
  final arm = who.slots[HumanoidBone.leftUpperArm]!;
  final thigh = who.slots[HumanoidBone.rightUpperLeg]!;
  final spine = who.slots[HumanoidBone.spine]!;
  final rest = who.locals[who.indexOf(hips)].$1;
  return ClipDocument(
    name: 'Stride',
    duration: 1,
    channels: [
      moves(hips, [
        (0, rest),
        (0.5, rest + Vector3(0.05, -0.08, 0.2)),
        (1, rest),
      ]),
      turns(arm, [
        (0, who.locals[who.indexOf(arm)].$2),
        (0.5, about(1, 0.2, 0, 50)),
        (1, who.locals[who.indexOf(arm)].$2),
      ]),
      turns(thigh, [
        (0, Quaternion.identity()),
        (0.3, about(1, 0, 0.1, -35)),
        (0.7, about(1, 0, 0, 25)),
        (1, Quaternion.identity()),
      ]),
      turns(spine, [
        (0, Quaternion.identity()),
        (0.5, about(0.3, 1, 0, 20)),
        (1, Quaternion.identity()),
      ]),
    ],
  );
}

void main() {
  group('follow', () {
    test('turns each bone the way the baked clip does at its keys', () {
      final from = person(name: capture);
      final to = person(name: mannequin, height: 1.2, armsDown: 0.7);
      final clip = stride(from);
      final live = LiveRetarget(
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      );
      final baked = retargetClip(
        clip,
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      );
      expect(live.problems, baked.problems);
      for (final at in [0.0, 0.3, 0.5, 0.7, 1.0]) {
        final frame = live.follow(clip.sampleAt(at).bones['']!);
        final expected = baked.clip.sampleAt(at).bones[''] ?? {};
        for (final MapEntry(:key, :value) in frame.entries) {
          final turn = expected[key]?.rotation ?? to.locals[to.indexOf(key)].$2;
          expect(
            // An angle from acos, so a rounding error of 1e-16 reads as 1e-8.
            turnBetween(value.rotation!, turn),
            closeTo(0, 1e-6),
            reason: '$key at $at',
          );
          final place = expected[key]?.position;
          if (place != null) {
            expect(
              (value.position! - place).length,
              closeTo(0, 1e-9),
              reason: '$key at $at',
            );
          }
        }
      }
    });

    test('answers a rest pose for a source that says nothing', () {
      final from = person(name: capture);
      final to = person(name: mannequin, height: 1.2);
      final live = LiveRetarget(
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      );
      final frame = live.follow({});
      expect(frame, hasLength(to.slots.length));
      for (final MapEntry(:key, :value) in frame.entries) {
        final rest = to.locals[to.indexOf(key)].$2;
        expect(turnBetween(value.rotation!, rest), closeTo(0, 1e-6));
        expect(value.position, isNull);
      }
    });

    test('puts a limb where a source on its own cannot be moved by name', () {
      // Not a person, so only the names given can pair the two.
      final from = person(name: capture);
      final to = person(name: mannequin);
      final live = LiveRetarget(
        from: from.skeleton,
        to: to.skeleton,
        bones: {'hand_l': 'mixamorig:LeftHand'},
      );
      expect(live.follow({}).keys, contains('hand_l'));
      expect(
        () => LiveRetarget(
          from: from.skeleton,
          to: to.skeleton,
          bones: {'nowhere': 'mixamorig:LeftHand'},
        ),
        throwsArgumentError,
      );
    });
  });

  group('pinned', () {
    test('is the source foot, as large as the target is', () {
      final from = person(name: capture);
      final to = person(name: mannequin, height: 0.9);
      final live = LiveRetarget(
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      );
      final frame = {
        'mixamorig:RightUpLeg': BoneLocal(rotation: about(1, 0, 0, -40)),
        'mixamorig:RightLeg': BoneLocal(rotation: about(1, 0, 0, 30)),
      };
      final foot = from.originOf('mixamorig:RightFoot', frame);
      final size = to.skeleton.reach / from.skeleton.reach;
      final point = live.pointOf('mixamorig:RightFoot', frame);
      expect((point - foot * size).length, closeTo(0, 1e-9));
      expect(point.z, greaterThan(0.05));
    });

    test('reaches a target leg to where the source foot is', () {
      final from = person(name: capture);
      final to = person(name: mannequin, height: 1.2);
      final live = LiveRetarget(
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      );
      final frame = <String, BoneLocal>{
        'mixamorig:LeftUpLeg': BoneLocal(rotation: about(1, 0, 0, -40)),
        'mixamorig:LeftLeg': BoneLocal(rotation: about(1, 0, 0, 30)),
      };
      final world = to.world();
      Vector3 at(String name) => world[to.indexOf(name)].getTranslation();
      final armature = Armature([
        Bone(name: 'thigh_l', head: at('thigh_l'), tail: at('calf_l')),
        Bone(
          name: 'calf_l',
          head: at('calf_l'),
          tail: at('foot_l'),
          parent: 'thigh_l',
          connected: true,
        ),
      ]);
      final pose = Pose(armature)..evaluate();
      Map<String, BoneLocal>? source = frame;
      final ik = TwoBoneIk(
        root: 'thigh_l',
        mid: 'calf_l',
        target: live.pinned('mixamorig:LeftFoot', source: () => source),
      );

      ik.solve(pose, ModifierFrame());
      pose.evaluate();
      final want = live.pointOf('mixamorig:LeftFoot', frame);
      expect((pose.tailOf('calf_l') - want).length, closeTo(0, 1e-6));

      // With no source frame the limb is left as it was.
      source = null;
      final before = pose.tailOf('calf_l');
      ik.solve(pose, ModifierFrame());
      pose.evaluate();
      expect((pose.tailOf('calf_l') - before).length, closeTo(0, 1e-9));
    });

    test('refuses a bone the source does not have', () {
      final from = person(name: capture);
      final to = person(name: mannequin);
      final live = LiveRetarget(from: from.skeleton, to: to.skeleton);
      expect(() => live.pointOf('nowhere', {}), throwsArgumentError);
      expect(
        () => live.pinned('nowhere', source: () => null),
        throwsArgumentError,
      );
    });
  });

  group('a real pair', () {
    final cesium = File('../../assets/samples/CesiumMan.glb');
    final figure = File('../../assets/samples/RiggedFigure.glb');

    test(
      'keeps every joint within a few centimetres of the source',
      () {
        final bytes = cesium.readAsBytesSync();
        final from = restSkeletonsFromGltf(bytes).first;
        final to = restSkeletonsFromGltf(figure.readAsBytesSync()).first;
        final clip = clipsFromGltf(bytes).clips.first;
        final live = LiveRetarget(from: from, to: to, humanoid: true);
        expect(live.problems, isEmpty);

        final size = to.reach / from.reach;
        final pairs = matchHumanoid(from, to);
        var worst = 0.0;
        for (var at = 0.0; at <= clip.duration; at += clip.duration / 8) {
          final frame = clip.sampleAt(at).bones['']!;
          final source = posedWorld(from, frame);
          final moved = posedWorld(to, live.follow(frame));
          final shift = source[0].getTranslation();
          final home = moved[0].getTranslation();
          for (final MapEntry(key: mine, value: theirs) in pairs.entries) {
            final there = source[from.indexOf(theirs)!].getTranslation();
            final here = moved[to.indexOf(mine)!].getTranslation();
            final off = (here - home) - (there - shift) * size;
            if (off.length > worst) worst = off.length;
          }
        }
        // The same few centimetres at every moment: how the two figures'
        // joints differ in proportion, not a mistake in the motion.
        expect(worst, lessThan(0.06));
      },
      skip: cesium.existsSync() && figure.existsSync()
          ? false
          : 'CesiumMan.glb or RiggedFigure.glb is not fetched',
    );
  });
}
