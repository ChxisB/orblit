import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

RestSkeleton chain(List<String> names) => RestSkeleton(
  names: names,
  parents: [for (var i = 0; i < names.length; i++) i - 1],
  local: [
    for (var i = 0; i < names.length; i++)
      Matrix4.translation(Vector3(0, i == 0 ? 0 : 0.5, 0)),
  ],
);

ClipChannel<Quaternion> turns(String bone, double degrees) =>
    ClipChannel<Quaternion>(
      target: '',
      bone: bone,
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [
        Key(0, Quaternion.identity(), hold: Hold.linear),
        Key(
          1,
          Quaternion.axisAngle(
            Vector3(0, 0, 1),
            degrees * 3.141592653589793 / 180,
          ),
          hold: Hold.linear,
        ),
      ],
    );

ClipDocument swing() => ClipDocument(
  name: 'Swing',
  duration: 1,
  channels: [turns('upper', 30), turns('lower', 50)],
);

Set<String> bonesOf(ClipDocument clip) => {
  for (final channel in clip.channels)
    if (channel.bone != null) channel.bone!,
};

void main() {
  final from = chain(['root', 'upper', 'lower']);
  final to = chain(['root', 'upper', 'lower']);

  group('unpaired', () {
    test('leaves out a bone that a matching name would drive', () {
      final both = retargetClip(swing(), from: from, to: to);
      expect(bonesOf(both.clip), {'upper', 'lower'});

      final left = retargetClip(
        swing(),
        from: from,
        to: to,
        unpaired: {'lower'},
      );
      expect(bonesOf(left.clip), {'upper'});
    });

    test('lets a bone named by hand drive another one', () {
      final paired = retargetClip(
        swing(),
        from: from,
        to: to,
        bones: {'lower': 'upper'},
        unpaired: {'upper'},
      );
      expect(bonesOf(paired.clip), {'lower'});
    });

    test('refuses a bone the target lacks', () {
      expect(
        () => retargetClip(swing(), from: from, to: to, unpaired: {'tail'}),
        throwsArgumentError,
      );
    });

    test('refuses a bone that is also paired by hand', () {
      expect(
        () => retargetClip(
          swing(),
          from: from,
          to: to,
          bones: {'lower': 'upper'},
          unpaired: {'lower'},
        ),
        throwsArgumentError,
      );
    });

    test('is kept off a live retarget too', () {
      final live = LiveRetarget(from: from, to: to, unpaired: {'lower'});
      final pose = live.follow({
        'upper': BoneLocal(rotation: Quaternion.axisAngle(Vector3(0, 0, 1), 1)),
        'lower': BoneLocal(rotation: Quaternion.axisAngle(Vector3(0, 0, 1), 1)),
      });
      expect(pose.keys, isNot(contains('lower')));
      expect(pose.keys, contains('upper'));
    });
  });

  group('a bone map', () {
    test('keeps what it was given, and sorts it on the way out', () {
      final map = BoneMap(
        fromAsset: 'models/hero.glb',
        toAsset: 'models/dwarf.glb',
        bones: {'wrist': 'hand', 'ankle': 'foot'},
        unpaired: {'tail', 'cape'},
        humanoid: true,
        fixRest: true,
      );
      final json = jsonDecode(map.encode()) as Map<String, Object?>;

      expect(json['kind'], 'orblit.bonemap');
      expect((json['bones']! as Map).keys.toList(), ['ankle', 'wrist']);
      expect(json['unpaired'], ['cape', 'tail']);
      expect(map.encode().endsWith('\n'), isTrue);
    });

    test('reads back what it wrote', () {
      final map = BoneMap(
        fromAsset: 'models/hero.glb',
        toAsset: 'models/dwarf.glb',
        bones: {'wrist': 'hand'},
        unpaired: {'tail'},
        humanoid: true,
      );
      final loaded = BoneMap.decode(map.encode());

      expect(loaded.problems, isEmpty);
      expect(loaded.map.fromAsset, 'models/hero.glb');
      expect(loaded.map.toAsset, 'models/dwarf.glb');
      expect(loaded.map.bones, {'wrist': 'hand'});
      expect(loaded.map.unpaired, {'tail'});
      expect(loaded.map.humanoid, isTrue);
      expect(loaded.map.fixRest, isFalse);
      expect(loaded.map.encode(), map.encode());
    });

    test('is the same as spelling the arguments out', () {
      final map = BoneMap(
        bones: {'lower': 'upper'},
        unpaired: {'upper'},
        fixRest: true,
      );
      final byMap = map.apply(swing(), from: from, to: to);
      final byHand = retargetClip(
        swing(),
        from: from,
        to: to,
        bones: {'lower': 'upper'},
        unpaired: {'upper'},
        fixRest: true,
      );
      expect(byMap.clip.encode(), byHand.clip.encode());
      expect(byMap.problems, byHand.problems);
    });

    test('changes one thing with copyWith and keeps the rest', () {
      final map = BoneMap(
        fromAsset: 'a',
        toAsset: 'b',
        bones: {'x': 'y'},
        unpaired: {'z'},
      );
      final other = map.copyWith(humanoid: true);

      expect(other.humanoid, isTrue);
      expect(other.fromAsset, 'a');
      expect(other.bones, {'x': 'y'});
      expect(other.unpaired, {'z'});
    });

    test('pairs a bone by hand and takes it off the unpaired ones', () {
      final map = BoneMap(unpaired: {'lower'}).withPair('lower', 'upper');

      expect(map.bones, {'lower': 'upper'});
      expect(map.unpaired, isEmpty);
    });

    test('leaves a bone alone and takes its pair away', () {
      final map = BoneMap(bones: {'lower': 'upper'}).withUnpaired('lower');

      expect(map.bones, isEmpty);
      expect(map.unpaired, {'lower'});
    });

    test('hands a bone back to names and the body', () {
      final map = BoneMap(
        bones: {'lower': 'upper'},
        unpaired: {'tail'},
      ).withAuto('lower').withAuto('tail');

      expect(map.bones, isEmpty);
      expect(map.unpaired, isEmpty);
    });

    test('keeps the original when a change is made', () {
      final map = BoneMap(bones: {'a': 'b'});
      map.withUnpaired('a');
      expect(map.bones, {'a': 'b'});
    });

    test('names the bones a skeleton lacks, and drops them', () {
      final map = BoneMap(
        bones: {'lower': 'upper', 'tail': 'upper', 'upper': 'wing'},
        unpaired: {'fin'},
      );

      expect(map.stale(from: from, to: to), ['fin', 'tail', 'upper']);
      final fitted = map.fitted(from: from, to: to);
      expect(fitted.bones, {'lower': 'upper'});
      expect(fitted.unpaired, isEmpty);
      expect(fitted.stale(from: from, to: to), isEmpty);
    });

    test('says which bone moves each bone, as a clip would be moved', () {
      final map = BoneMap(bones: {'lower': 'upper'}, unpaired: {'upper'});
      final pairing = map.pairing(from: from, to: to);

      expect(pairing.drivers, {'root': 'root', 'lower': 'upper'});
      expect(pairing.problems, isEmpty);

      final moved = map.apply(swing(), from: from, to: to);
      expect(bonesOf(moved.clip), pairing.drivers.keys.toSet()..remove('root'));
    });

    test('throws for a name no skeleton has, as apply does', () {
      final map = BoneMap(bones: {'tail': 'upper'});
      expect(() => map.pairing(from: from, to: to), throwsArgumentError);
    });

    test('cannot hold the same bone as a pair and as unpaired', () {
      expect(
        () => BoneMap(bones: {'x': 'y'}, unpaired: {'x'}),
        throwsArgumentError,
      );
    });

    test('cannot be edited after it is made', () {
      final map = BoneMap(bones: {'x': 'y'}, unpaired: {'z'});
      expect(() => map.bones['a'] = 'b', throwsUnsupportedError);
      expect(() => map.unpaired.add('a'), throwsUnsupportedError);
    });

    test('refuses a file that is not a bone map', () {
      expect(
        () => BoneMap.decode('not json'),
        throwsA(isA<BoneMapFormatException>()),
      );
      expect(
        () => BoneMap.decode('{"kind":"orblit.clip"}'),
        throwsA(isA<BoneMapFormatException>()),
      );
    });

    test('refuses a file from a newer Orblit', () {
      expect(
        () => BoneMap.decode('{"kind":"orblit.bonemap","formatVersion":2}'),
        throwsA(isA<BoneMapFormatException>()),
      );
    });

    test('leaves out a pair that is not two names, and says so', () {
      final loaded = BoneMap.decode(
        jsonEncode({
          'kind': 'orblit.bonemap',
          'formatVersion': 1,
          'bones': {'wrist': 'hand', 'elbow': 3, 'knee': ''},
          'unpaired': ['tail', 7],
        }),
      );

      expect(loaded.map.bones, {'wrist': 'hand'});
      expect(loaded.map.unpaired, {'tail'});
      expect(loaded.problems, hasLength(3));
      expect(loaded.problems.first, contains('elbow'));
    });

    test('keeps the pair when a file also leaves it unpaired', () {
      final loaded = BoneMap.decode(
        jsonEncode({
          'kind': 'orblit.bonemap',
          'formatVersion': 1,
          'bones': {'wrist': 'hand'},
          'unpaired': ['wrist'],
        }),
      );

      expect(loaded.map.bones, {'wrist': 'hand'});
      expect(loaded.map.unpaired, isEmpty);
      expect(loaded.problems.single, contains('wrist'));
    });

    test('reads a file that says nothing but what it is', () {
      final loaded = BoneMap.decode(
        '{"kind":"orblit.bonemap","formatVersion":1}',
      );
      expect(loaded.map.bones, isEmpty);
      expect(loaded.map.humanoid, isFalse);
      expect(loaded.problems, isEmpty);
    });
  });
}
