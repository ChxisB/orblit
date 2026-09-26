import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

ClipFrame frame(void Function(ClipFrame frame) fill) {
  final frame = ClipFrame(0);
  fill(frame);
  return frame;
}

void main() {
  group('mixing frames', () {
    test('averages numbers and vectors by weight', () {
      final mixed = ClipFrame.mix(
        [
          frame((f) {
            f.values['lamp'] = {'light.power': 0.0};
            f.values[''] = {'transform.position': Vector3(0, 0, 0)};
          }),
          frame((f) {
            f.values['lamp'] = {'light.power': 4.0};
            f.values[''] = {'transform.position': Vector3(2, 0, 4)};
          }),
        ],
        [3, 1],
      );
      expect(mixed.valueOf('lamp', 'light.power'), closeTo(1, 1e-12));
      final position = mixed.valueOf('', 'transform.position')! as Vector3;
      expect((position - Vector3(0.5, 0, 1)).length, lessThan(1e-12));
    });

    test('turns rotations the short way, by weight', () {
      final quarter = Quaternion.axisAngle(Vector3(0, 1, 0), math.pi / 2);
      final mixed = ClipFrame.mix(
        [
          frame((f) {
            f.bones[''] = {'hips': BoneLocal(rotation: Quaternion.identity())};
          }),
          frame((f) {
            f.bones[''] = {'hips': BoneLocal(rotation: quarter)};
          }),
        ],
        [1, 1],
      );
      final eighth = Quaternion.axisAngle(Vector3(0, 1, 0), math.pi / 4);
      final turned = mixed.boneOf('', 'hips')!.rotation!;
      final dot =
          turned.x * eighth.x +
          turned.y * eighth.y +
          turned.z * eighth.z +
          turned.w * eighth.w;
      expect(dot.abs(), closeTo(1, 1e-9));
    });

    test('lets the frame with most say decide a flag', () {
      final mixed = ClipFrame.mix(
        [
          frame((f) => f.values['door'] = {'door.open': true}),
          frame((f) => f.values['door'] = {'door.open': false}),
        ],
        [0.4, 0.6],
      );
      expect(mixed.valueOf('door', 'door.open'), isFalse);
    });

    test('leaves a value to the frames that say something about it', () {
      final mixed = ClipFrame.mix(
        [
          frame((f) {
            f.bones[''] = {'arm': BoneLocal(rotation: Quaternion.identity())};
          }),
          frame((f) {
            f.bones[''] = {
              'arm': BoneLocal(position: Vector3(0, 2, 0)),
              'hand': BoneLocal(scale: Vector3.all(2)),
            };
          }),
        ],
        [0.9, 0.1],
      );
      final arm = mixed.boneOf('', 'arm')!;
      expect(arm.rotation, isNotNull);
      expect((arm.position! - Vector3(0, 2, 0)).length, lessThan(1e-12));
      expect(arm.scale, isNull);
      expect(mixed.boneOf('', 'hand')!.scale, Vector3.all(2));
    });

    test('gives a frame with no weight no say', () {
      final mixed = ClipFrame.mix(
        [
          frame((f) => f.values['lamp'] = {'light.power': 9.0}),
          frame((f) => f.values['lamp'] = {'light.power': 1.0}),
        ],
        [0, 1],
      );
      expect(mixed.valueOf('lamp', 'light.power'), 1);
    });

    test('copies rather than shares a value only one frame has', () {
      final kept = Vector3(1, 2, 3);
      final mixed = ClipFrame.mix(
        [
          frame((f) => f.values[''] = {'transform.scale': kept}),
        ],
        [1],
      );
      final out = mixed.valueOf('', 'transform.scale')! as Vector3;
      expect(out, kept);
      expect(identical(out, kept), isFalse);
    });

    test('keeps the time it is given', () {
      expect(ClipFrame.mix(const [], const [], at: 0.25).at, 0.25);
    });

    test('wants a weight for every frame', () {
      expect(
        () => ClipFrame.mix([ClipFrame(0)], const []),
        throwsArgumentError,
      );
    });
  });
}
