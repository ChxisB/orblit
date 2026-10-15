import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

import 'people.dart';

ClipDocument clipOf(List<ClipChannel<Object>> channels) =>
    ClipDocument(name: 'Reduced', duration: 2, channels: channels);

ClipChannel<Object> only(ClipDocument clip) => clip.channels.single;

void main() {
  group('reduceKeys', () {
    test('thins a dense curve to within the distance asked for', () {
      final dense = moves('hips', [
        for (var i = 0; i <= 200; i++)
          (i / 100, Vector3(math.sin(i / 100 * math.pi), 0, 0.2 * i / 100)),
      ]);
      final reduced = only(reduceKeys(clipOf([dense]))) as ClipChannel<Vector3>;
      // Half a millimetre of a sine one metre tall lets about every other
      // key go.
      expect(reduced.keys.length, lessThan(dense.keys.length * 0.7));
      expect(reduced.keys.first.at, 0);
      expect(reduced.keys.last.at, 2);

      for (var i = 0; i < 200; i++) {
        for (final at in [i / 100, (i + 0.5) / 100]) {
          final off = (reduced.valueAt(at) - dense.valueAt(at)).length;
          expect(off, lessThanOrEqualTo(5e-4 + 1e-12), reason: 'at $at');
        }
      }
    });

    test('a looser distance keeps fewer keys than a tighter one', () {
      final dense = moves('hips', [
        for (var i = 0; i <= 100; i++)
          (i / 50, Vector3(math.sin(i / 50 * math.pi), 0, 0)),
      ]);
      int kept(double distance) =>
          only(reduceKeys(clipOf([dense]), distance: distance)).keys.length;
      expect(kept(0.05), lessThan(kept(0.001)));
    });

    test('keeps a rotation inside the angle asked for', () {
      final dense = turns('spine', [
        for (var i = 0; i <= 120; i++)
          (i / 60, about(0.2, 1, 0, 40 * math.sin(i / 60 * math.pi))),
      ]);
      final reduced =
          only(reduceKeys(clipOf([dense]), angle: 1e-3))
              as ClipChannel<Quaternion>;
      expect(reduced.keys.length, lessThan(dense.keys.length));
      for (var i = 0; i < 120; i++) {
        final at = (i + 0.5) / 60;
        expect(
          turnBetween(reduced.valueAt(at), dense.valueAt(at)),
          lessThanOrEqualTo(1e-3 + 1e-9),
          reason: 'at $at',
        );
      }
    });

    test('keeps a straight run as its two ends', () {
      final line = moves('hips', [
        for (var i = 0; i <= 10; i++) (i / 5, Vector3(i * 0.1, 0, 0)),
      ]);
      expect(only(reduceKeys(clipOf([line]))).keys, hasLength(2));
    });

    test('leaves an eased channel as it was', () {
      final eased = ClipChannel<Vector3>(
        target: '',
        bone: 'hips',
        property: 'position',
        kind: ChannelKind.vector,
        keys: [
          Key(0, Vector3.zero(), hold: Hold.smooth),
          Key(1, Vector3(0.5, 0, 0), hold: Hold.smooth),
          Key(2, Vector3(1, 0, 0), hold: Hold.smooth),
        ],
      );
      expect(only(reduceKeys(clipOf([eased]))).keys, hasLength(3));
    });

    test('leaves a channel with a cut as it was', () {
      final cut = moves('hips', [
        (0, Vector3.zero()),
        (1, Vector3(1, 0, 0)),
        (1, Vector3(1, 1, 0)),
        (2, Vector3(1, 2, 0)),
      ]);
      expect(only(reduceKeys(clipOf([cut]))).keys, hasLength(4));
    });

    test('leaves every other part of the clip alone', () {
      const mark = Mark(0.5, 'step');
      final clip = clipOf([
        moves('hips', [
          for (var i = 0; i <= 8; i++) (i / 4, Vector3(i * 0.1, 0, 0)),
        ]),
      ]).copyWith(marks: [mark]);
      final reduced = reduceKeys(clip);
      expect(reduced.marks, hasLength(1));
      expect(reduced.duration, clip.duration);
      expect(reduced.name, clip.name);
    });
  });

  group('after a retarget', () {
    test('takes the keys a bake added back out', () {
      // The target has one spine bone where the source has three, so its
      // neck is turned by all three and gets a key whenever any one has one.
      final from = person(name: capture, spine: 3);
      final to = person(name: capture, spine: 1);
      final chest = from.slots[HumanoidBone.chest]!;
      final clip = clipOf([
        turns(chest, [
          for (var i = 0; i <= 4; i++) (i / 2, about(1, 0, 0, i * 10.0)),
        ]),
        turns(from.slots[HumanoidBone.neck]!, [
          (0, Quaternion.identity()),
          (1.3, Quaternion.identity()),
          (2, Quaternion.identity()),
        ]),
      ]);
      final baked = retargetClip(
        clip,
        from: from.skeleton,
        to: to.skeleton,
        humanoid: true,
      ).clip;
      final neck = to.slots[HumanoidBone.neck]!;
      ClipChannel<Object> neckOf(ClipDocument c) => c.channels.singleWhere(
        (channel) => channel.bone == neck && channel.property == 'rotation',
      );
      final whole = neckOf(baked);
      final thin = neckOf(reduceKeys(baked));
      expect(thin.keys.length, lessThan(whole.keys.length));
      for (var at = 0.0; at <= 2; at += 0.1) {
        final a = baked.sampleAt(at).bones['']![neck]!.rotation!;
        final b = reduceKeys(baked).sampleAt(at).bones['']![neck]!.rotation!;
        expect(turnBetween(a, b), lessThan(1e-3), reason: 'at $at');
      }
    });
  });
}
