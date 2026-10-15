import 'dart:io';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'people.dart';

Map<HumanoidBone, String> found(Person who, {Vector3? forward}) =>
    humanoidOf(who.skeleton, forward: forward).bones;

void main() {
  group('humanoidOf', () {
    test('finds each part of a person whatever the bones are called', () {
      for (final naming in [plainName, mannequin, capture]) {
        final who = person(name: naming);
        final map = humanoidOf(who.skeleton);
        expect(map.bones, who.slots);
        expect(map.problems, isEmpty);
      }
    });

    test('tells left from right by where they stand, not by their order', () {
      final who = person(rightFirst: true);
      final map = humanoidOf(who.skeleton);
      expect(map.bones, who.slots);
      final hand = who.indexOf(map[HumanoidBone.leftHand]!);
      expect(who.world()[hand].getTranslation().x, greaterThan(0));
    });

    test('believes a name that says a side over the way it is facing', () {
      // Facing -Z, a person's left is at -X, and their names say so.
      final who = person(name: mannequin);
      final names = [
        for (final name in who.names)
          name.endsWith('_l')
              ? '${name.substring(0, name.length - 2)}_r'
              : name.endsWith('_r')
              ? '${name.substring(0, name.length - 2)}_l'
              : name,
      ];
      final turned = RestSkeleton(
        names: names,
        parents: who.parents,
        local: [
          for (final (position, turn) in who.locals)
            Matrix4.compose(position, turn, Vector3.all(1)),
        ],
      );
      final map = humanoidOf(turned);
      expect(map[HumanoidBone.leftHand], 'hand_l');
      expect(map[HumanoidBone.rightHand], 'hand_r');
    });

    test('goes by which way the person faces when no name says', () {
      final who = person();
      final map = humanoidOf(who.skeleton, forward: Vector3(0, 0, -1));
      final left = who.slots[HumanoidBone.leftHand]!;
      expect(map[HumanoidBone.rightHand], left);
    });

    test('puts a skeleton with one, two or three spine bones in order', () {
      for (final count in [1, 2, 3]) {
        final who = person(spine: count);
        expect(found(who), who.slots, reason: '$count spine bones');
      }
    });

    test('has no shoulder where the arm hangs from the spine', () {
      final who = person(shoulders: false);
      final map = humanoidOf(who.skeleton);
      expect(map.bones, who.slots);
      expect(map[HumanoidBone.leftShoulder], isNull);
    });

    test('leaves off an end site that only marks where a bone stops', () {
      final who = person(name: capture, endSites: true);
      expect(found(who), who.slots);
    });

    test('says nothing for a skeleton that is not a person', () {
      // Four legs under a body, and a head: no spine with arms at its top.
      Matrix4 at(double x, double y, double z) =>
          Matrix4.translation(Vector3(x, y, z));
      final names = [
        'body',
        'neck',
        'head',
        for (final leg in ['a', 'b', 'c', 'd']) ...['${leg}1', '${leg}2'],
      ];
      final animal = RestSkeleton(
        names: names,
        parents: [-1, 0, 1, 0, 3, 0, 5, 0, 7, 0, 9],
        local: [
          at(0, 1, 0),
          at(0, 0.2, 0.5),
          at(0, 0.2, 0.2),
          for (final x in [-0.2, 0.2])
            for (final z in [-0.4, 0.4]) ...[at(x, -0.1, z), at(0, -0.9, 0)],
        ],
      );
      final map = humanoidOf(animal);
      expect(map.bones, isEmpty);
      expect(map.problems, isNotEmpty);
    });

    test('says why when it has no height to go by', () {
      final flat = RestSkeleton(
        names: ['a', 'b'],
        parents: [-1, 0],
        local: [Matrix4.identity(), Matrix4.identity()],
      );
      expect(humanoidOf(flat).problems.single, contains('no height'));
    });
  });

  group('matchHumanoid', () {
    test('pairs two people by the part each bone is', () {
      final from = person(name: capture, rightFirst: true);
      final to = person(name: mannequin, height: 1.2);
      final pairs = matchHumanoid(from.skeleton, to.skeleton);
      expect(pairs, hasLength(to.slots.length));
      for (final MapEntry(:key, :value) in to.slots.entries) {
        expect(pairs[value], from.slots[key], reason: '$key');
      }
    });

    test('pairs only the slots both people have', () {
      final from = person(spine: 3, name: capture);
      final to = person(spine: 1, shoulders: false, name: mannequin);
      final pairs = matchHumanoid(from.skeleton, to.skeleton);
      expect(pairs['spine_01'], 'mixamorig:Spine');
      expect(pairs.values, isNot(contains('mixamorig:Spine1')));
      expect(pairs.containsKey('clavicle_l'), isFalse);
    });
  });

  group('two real figures', () {
    final cesium = File('../../assets/samples/CesiumMan.glb');
    final figure = File('../../assets/samples/RiggedFigure.glb');
    final fox = File('../../assets/samples/Fox.glb');
    final have = cesium.existsSync() && figure.existsSync();

    test('are paired bone for bone though they share no names', () {
      final from = restSkeletonsFromGltf(cesium.readAsBytesSync()).first;
      final to = restSkeletonsFromGltf(figure.readAsBytesSync()).first;
      final named = matchBones(from, to);
      final paired = matchHumanoid(from, to);
      expect(named.length, lessThan(to.length));
      expect(paired, hasLength(to.length));
      expect(paired.values.toSet(), hasLength(from.length));
      expect(humanoidOf(from).problems, isEmpty);
      expect(humanoidOf(to).problems, isEmpty);
    }, skip: have ? false : 'CesiumMan.glb or RiggedFigure.glb is not fetched');

    test('a fox is not read as a person', () {
      final skeleton = restSkeletonsFromGltf(fox.readAsBytesSync()).first;
      final map = humanoidOf(skeleton);
      expect(map.bones, isEmpty);
      expect(map.problems, isNotEmpty);
    }, skip: fox.existsSync() ? false : 'Fox.glb is not fetched');
  });
}
