import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

RestSkeleton skeleton() => RestSkeleton(
  names: ['hips', 'leg', 'spine', 'arm'],
  parents: [-1, 0, 0, 2],
  local: [for (var i = 0; i < 4; i++) Matrix4.identity()],
);

ClipChannel<Vector3> movingBone(String bone, double from, double to) =>
    ClipChannel<Vector3>(
      target: '',
      bone: bone,
      property: 'position',
      kind: ChannelKind.vector,
      keys: [
        Key(0, Vector3(from, 0, 0), hold: Hold.linear),
        Key(1, Vector3(to, 0, 0)),
      ],
    );

ClipFrame pose({double position = 0, double rotation = 0, double scale = 1}) =>
    ClipFrame(0)
      ..bones[''] = {
        'arm': BoneLocal(
          position: Vector3(position, 0, 0),
          rotation: Quaternion.axisAngle(Vector3(0, 0, 1), rotation),
          scale: Vector3.all(scale),
        ),
      };

double boneX(ClipFrame frame, String bone) =>
    frame.boneOf('', bone)!.position!.x;

void main() {
  test('additive entity values use their reference and preserve flags', () {
    final rest = ClipFrame(0)
      ..values[''] = {
        'pose.value': 2.0,
        'pose.point': Vector3(1, 0, 0),
        'pose.flag': false,
      };
    final base = ClipFrame(0)
      ..values[''] = {
        'pose.value': 5.0,
        'pose.point': Vector3(4, 0, 0),
        'pose.flag': false,
      };
    final overlay = ClipFrame(0)
      ..values[''] = {
        'pose.value': 6.0,
        'pose.point': Vector3(3, 0, 0),
        'pose.flag': true,
      };
    final out = addFrame(base, overlay, rest: rest, weight: 0.5);
    expect(out.valueOf('', 'pose.value'), 7);
    expect(out.valueOf('', 'pose.point'), Vector3(5, 0, 0));
    expect(out.valueOf('', 'pose.flag'), isFalse);
    (out.valueOf('', 'pose.point')! as Vector3).x = 20;
    expect(base.valueOf('', 'pose.point'), Vector3(4, 0, 0));
    final masked = addFrame(
      base,
      overlay,
      rest: rest,
      mask: BoneMask({'arm': 1}),
    );
    expect(masked.valueOf('', 'pose.value'), 5);
  });

  test('a zero reference scale adds its offset without division', () {
    final out = addFrame(
      pose(scale: 3),
      pose(scale: 2),
      rest: pose(scale: 0),
      weight: 0.5,
    );
    expect(out.boneOf('', 'arm')!.scale, Vector3.all(4));
  });
  test(
    'a running character attacks with its upper body and keeps its legs',
    () {
      final rest = restFrame({'': skeleton()});
      final run = ClipDocument(
        name: 'Run',
        duration: 1,
        whenDone: WhenDone.loop,
        channels: [movingBone('leg', 0, 4), movingBone('arm', 0, 1)],
      );
      final attack = ClipDocument(
        name: 'Attack',
        duration: 1,
        channels: [movingBone('arm', 8, 10), movingBone('leg', 100, 200)],
        marks: const [Mark(0.5, 'fire')],
      );
      final graph = BlendDocument(
        name: 'Run',
        states: [BlendState('run', plays: const BlendClip('run'))],
      );
      final player = BlendPlayer(graph, clips: {'run': run}, rest: rest);
      final layer = ClipLayer(
        attack,
        mask: BoneMask.below(skeleton(), 'spine'),
        fadeIn: 0.2,
        fadeOut: 0.2,
      );
      var place = LayerPlace();
      final legPositions = <double>[];
      final marks = <String>[];
      for (var i = 0; i < 60; i++) {
        final base = player.advance(1 / 60).frame;
        final step = layer.advance(place, 1 / 60);
        place = step.place;
        marks.addAll(step.marks.map((mark) => mark.name));
        final result = layer.sampleAt(base, place, rest: rest);
        legPositions.add(boneX(result, 'leg'));
        expect(boneX(result, 'leg'), boneX(base, 'leg'));
        if (i == 29) expect(boneX(result, 'arm'), closeTo(9, 1e-9));
        if (i == 59) expect(boneX(result, 'arm'), boneX(base, 'arm'));
      }
      expect(legPositions.toSet().length, 60);
      expect(marks, ['fire']);
    },
  );

  test('subtree masks include descendants and leave other targets alone', () {
    final mask = BoneMask.below(skeleton(), 'spine', weight: 0.5);
    expect(mask.weights, {'spine': 0.5, 'arm': 0.5});
    expect(mask.weightOf('other', 'arm'), 0);
    expect(mask.weightOf('', 'leg'), 0);
    expect(() => BoneMask.below(skeleton(), 'missing'), throwsArgumentError);
    expect(() => BoneMask({'arm': double.nan}), throwsArgumentError);
    expect(() => mask.weights['arm'] = 0, throwsUnsupportedError);
  });

  test('partial overlays preserve every unauthored base channel', () {
    final base = pose(position: 2, rotation: 0.4, scale: 3);
    final overlay = ClipFrame(0)
      ..bones[''] = {'arm': BoneLocal(position: Vector3(10, 0, 0))};
    final out = layerFrame(base, overlay, rest: pose(), weight: 0.5);
    expect(boneX(out, 'arm'), 6);
    expect(out.boneOf('', 'arm')!.rotation!.radians, closeTo(0.4, 1e-9));
    expect(out.boneOf('', 'arm')!.scale, Vector3.all(3));
    expect(boneX(base, 'arm'), 2);
  });

  test('additive translation, rotation and scale are relative to rest', () {
    final out = addFrame(
      pose(position: 5, rotation: 0.4, scale: 3),
      pose(position: 6, rotation: 1.2, scale: 4),
      rest: pose(position: 2, rotation: 0.2, scale: 2),
      weight: 0.5,
    );
    expect(boneX(out, 'arm'), 7);
    expect(out.boneOf('', 'arm')!.rotation!.radians, closeTo(0.9, 1e-9));
    expect(out.boneOf('', 'arm')!.scale, Vector3.all(4.5));
  });

  test('additive rotations compose in local order for noncommuting axes', () {
    final rest = pose();
    rest.boneOf('', 'arm')!.rotation = Quaternion.axisAngle(
      Vector3(1, 0, 0),
      0.5,
    );
    final delta = Quaternion.axisAngle(Vector3(0, 1, 0), 0.8);
    final overlay = pose();
    overlay.boneOf('', 'arm')!.rotation =
        rest.boneOf('', 'arm')!.rotation! * delta;
    final base = pose(rotation: 0.4);
    final out = addFrame(base, overlay, rest: rest);
    final expected = base.boneOf('', 'arm')!.rotation! * delta;
    expect(
      out
          .boneOf('', 'arm')!
          .rotation!
          .rotated(Vector3(1, 0, 0))
          .distanceTo(expected.rotated(Vector3(1, 0, 0))),
      lessThan(1e-9),
    );
  });

  test('an additive reference can differ from the rest pose', () {
    final out = addFrame(
      pose(position: 5),
      pose(position: 6),
      rest: pose(position: 1),
      reference: pose(position: 4),
    );
    expect(boneX(out, 'arm'), 7);
  });

  test('a removed channel returns to rest during a graph fade', () {
    final graph = BlendDocument(
      name: 'Arm',
      states: [
        BlendState('up', plays: const BlendClip('up')),
        BlendState('empty', plays: const BlendClip('empty')),
      ],
    );
    final player = BlendPlayer(
      graph,
      clips: {
        'up': ClipDocument(
          name: 'Up',
          duration: 1,
          channels: [movingBone('arm', 10, 10)],
        ),
        'empty': ClipDocument(name: 'Empty', duration: 1, channels: const []),
      },
      rest: pose(position: 2),
    );
    player.enter('empty', fade: 1, shape: Easing.linear);
    expect(boneX(player.advance(0.5).frame, 'arm'), 6);
    expect(boneX(player.advance(0.5).frame, 'arm'), 2);
  });

  test('one-shot envelope fades in and out and releases at its end', () {
    final layer = ClipLayer(
      ClipDocument(name: 'Shot', duration: 1, channels: const []),
      fadeIn: 0.2,
      fadeOut: 0.4,
    );
    expect(layer.weightAt(LayerPlace()), 0);
    expect(layer.weightAt(LayerPlace(0.1)), closeTo(0.5, 1e-9));
    expect(layer.weightAt(LayerPlace(0.4)), 1);
    expect(layer.weightAt(LayerPlace(0.8)), closeTo(0.5, 1e-9));
    expect(layer.weightAt(LayerPlace(1)), 0);
    expect(layer.advance(LayerPlace(), 5).finished, isTrue);
    expect(layer.advance(LayerPlace(1), 1).marks, isEmpty);
  });

  test('layer clock survives JSON and samples the same after restoring', () {
    final place = LayerPlace(0.4);
    final restored = LayerPlace.fromJson(
      jsonDecode(jsonEncode(place.toJson())),
    )!;
    expect(restored.at, place.at);
    expect(LayerPlace.fromJson({'at': -1}), isNull);
    expect(() => LayerPlace(double.infinity), throwsArgumentError);
  });

  test('zero weight and completed additive shots return the base pose', () {
    final clip = ClipDocument(
      name: 'Recoil',
      duration: 1,
      channels: [movingBone('arm', 8, 10)],
    );
    final layer = ClipLayer(clip, additive: true);
    expect(
      boneX(
        layer.sampleAt(pose(position: 4), LayerPlace(1), rest: pose()),
        'arm',
      ),
      4,
    );
    expect(
      boneX(
        addFrame(
          pose(position: 4),
          pose(position: 10),
          rest: pose(),
          weight: 0,
        ),
        'arm',
      ),
      4,
    );
    expect(() => ClipLayer(clip, fadeIn: -1), throwsArgumentError);
    expect(
      () => layerFrame(pose(), pose(), rest: pose(), weight: 2),
      throwsArgumentError,
    );
  });
}
