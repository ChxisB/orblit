import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

OrblitObject one(
  int key, {
  OrblitAnimation? animation,
  int? variant,
  List<OrblitJointPose>? joints,
}) => OrblitObject(
  key: key,
  transform: Matrix4.identity(),
  colour: Vector3(1, 1, 1),
  mesh: '/models/$key.glb',
  animation: animation,
  variant: variant,
  joints: joints,
);

Map<String, Object?> sent(List<OrblitObject> objects, {double? at}) =>
    OrblitScene(
      objects: objects,
      camera: OrblitCamera(position: Vector3(0, 2, 8), target: Vector3.zero()),
    ).toMessage(1, at: at);

List<int> keys(Map<String, Object?> message) =>
    (message['poseKeys']! as List<int>).toList();
List<int> ints(Map<String, Object?> message) =>
    (message['poseInts']! as Int32List).toList();
List<double> floats(Map<String, Object?> message) =>
    (message['poseFloats']! as Float32List).toList();
List<int> jointCounts(Map<String, Object?> message) =>
    (message['poseJointCounts']! as Int32List).toList();
List<int> joints(Map<String, Object?> message) =>
    (message['poseJoints']! as Int32List).toList();
List<double> jointTransforms(Map<String, Object?> message) =>
    (message['poseJointTransforms']! as Float32List).toList();

void main() {
  group('poses on the wire', () {
    test('a scene with nothing posed sends no pose keys at all', () {
      // Every scene without models out of files sends exactly what it sent
      // before poses existed.
      final message = sent([one(1), one(2)]);
      for (final key in const [
        'poseKeys',
        'poseInts',
        'poseFloats',
        'poseJointCounts',
        'poseJoints',
        'poseJointTransforms',
      ]) {
        expect(message.containsKey(key), isFalse, reason: key);
      }
    });

    test('one playing clip packs its row in the order the renderer reads', () {
      final message = sent([
        one(1),
        one(
          7,
          // A fade without a `from` is ignored: the clip is all there is.
          animation: const OrblitAnimation(
            clip: 2,
            seconds: 1.5,
            speed: 0.5,
            fade: 0.25,
          ),
        ),
      ]);

      expect(keys(message), [7]);
      // Clip, faded-from clip, flags (1: loops), variant.
      expect(ints(message), [2, -1, 1, -1]);
      expect(ints(message).length, OrblitAnimation.intStride);
      // Seconds, speed, from's seconds, from's speed, fade.
      expect(floats(message), [1.5, 0.5, 0, 0, 1]);
      expect(floats(message).length, OrblitAnimation.stride);
      expect(jointCounts(message), [0]);
      expect(joints(message), isEmpty);
      expect(jointTransforms(message), isEmpty);
    });

    test('a fade packs the clip it leaves, and each clip\'s own loop', () {
      final message = sent([
        one(
          3,
          animation: const OrblitAnimation(
            clip: 1,
            seconds: 0.25,
            speed: 2,
            loop: false,
            from: OrblitAnimation(clip: 0, seconds: 3, speed: 1),
            fade: 0.5,
          ),
        ),
      ]);

      // This clip does not loop and the one it leaves does: flag 2 alone.
      expect(ints(message), [1, 0, 2, -1]);
      expect(floats(message), [0.25, 2, 3, 1, 0.5]);
    });

    test('a variant alone and joints alone are each a pose', () {
      final message = sent([
        one(1),
        one(2, variant: 3),
        one(4),
        one(
          5,
          joints: [
            OrblitJointPose(
              skin: 0,
              joint: 6,
              transform: Matrix4.translationValues(1, 2, 3),
            ),
          ],
        ),
        // An empty list of joints has nothing to say.
        one(6, joints: const []),
      ]);

      expect(keys(message), [2, 5]);
      expect(ints(message), [
        -1, -1, 0, 3, //
        -1, -1, 0, -1,
      ]);
      expect(floats(message), [
        0, 0, 0, 0, 1, //
        0, 0, 0, 0, 1,
      ]);
      expect(jointCounts(message), [0, 1]);
    });

    test('joints are end to end, in object order', () {
      final message = sent([
        one(
          10,
          joints: [
            OrblitJointPose(
              skin: 0,
              joint: 1,
              transform: Matrix4.translationValues(1, 0, 0),
            ),
            OrblitJointPose(
              skin: 0,
              joint: 2,
              transform: Matrix4.translationValues(2, 0, 0),
            ),
          ],
        ),
        one(11, variant: 0),
        one(
          12,
          animation: const OrblitAnimation(clip: 0),
          joints: [
            OrblitJointPose(
              skin: 1,
              joint: 3,
              transform: Matrix4.translationValues(0, 0, 3),
            ),
          ],
        ),
      ]);

      expect(keys(message), [10, 11, 12]);
      final counts = jointCounts(message);
      expect(counts, [2, 0, 1]);
      final total = counts.reduce((a, b) => a + b);
      expect(joints(message), [0, 1, 0, 2, 1, 3]);
      expect(joints(message).length, total * 2);

      final transforms = jointTransforms(message);
      expect(transforms.length, total * 16);
      // Each joint's column-major transform, its translation in 12..14.
      expect(transforms.sublist(12, 15), [1, 0, 0]);
      expect(transforms.sublist(16 + 12, 16 + 15), [2, 0, 0]);
      expect(transforms.sublist(32 + 12, 32 + 15), [0, 0, 3]);
    });

    test('the host\'s seconds travel with the poses', () {
      final message = sent([
        one(1, animation: const OrblitAnimation(clip: 0, seconds: 2)),
      ], at: 12.5);
      expect(message['at'], 12.5);
      expect(message.containsKey('poseKeys'), isTrue);
    });
  });
}
