import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A second of leaning from [from] to [to] and back, looping.
ClipDocument leaning(String name, double from, double to) => ClipDocument(
  name: name,
  duration: 1,
  whenDone: WhenDone.loop,
  channels: [
    ClipChannel<double>(
      target: '',
      property: 'pose.lean',
      kind: ChannelKind.number,
      keys: [
        Key(0, from, hold: Hold.linear),
        Key(0.5, to, hold: Hold.linear),
        Key(1, from),
      ],
    ),
    ClipChannel<Vector3>(
      target: '',
      bone: 'hips',
      property: 'position',
      kind: ChannelKind.vector,
      keys: [
        Key(0, Vector3(0, 1, 0), hold: Hold.linear),
        Key(0.5, Vector3(0, 1.1, 0), hold: Hold.linear),
        Key(1, Vector3(0, 1, 0)),
      ],
    ),
  ],
  marks: const [Mark(0.25, 'step'), Mark(0.75, 'step')],
);

final Map<String, ClipDocument> clips = {
  'idle': leaning('idle', 0, 0.1),
  'walk': leaning('walk', 0, 1),
  'run': leaning('run', 0, 3),
  'jump': leaning('jump', 5, 6),
};

BlendDocument moves() => BlendDocument(
  name: 'Moves',
  inputs: const {'speed': 0},
  states: [
    BlendState('idle', plays: const BlendClip('idle')),
    BlendState(
      'move',
      plays: BlendLine('speed', const [
        LinePoint(1, BlendClip('walk')),
        LinePoint(3, BlendClip('run')),
      ]),
    ),
    BlendState('jump', plays: const BlendClip('jump')),
  ],
  changes: const [
    BlendChange(
      from: 'idle',
      to: 'move',
      when: BlendCondition.above('speed', 0.1),
      fade: 0.25,
      inStep: true,
    ),
    BlendChange(
      from: 'move',
      to: 'idle',
      when: BlendCondition.below('speed', 0.1),
      fade: 0.4,
    ),
    BlendChange(to: 'jump', when: BlendCondition.on('jump'), fade: 0.1),
  ],
);

void expectSameFrame(ClipFrame actual, ClipFrame expected) {
  expect(actual.at, expected.at);
  expect(actual.valueOf('', 'pose.lean'), expected.valueOf('', 'pose.lean'));
  expect(
    actual.boneOf('', 'hips')!.position,
    expected.boneOf('', 'hips')!.position,
  );
}

/// Plays [player] through a few seconds of starting, speeding up and
/// slowing down, the way a frame loop would.
void play(BlendPlayer player, int frames) {
  for (var i = 0; i < frames; i++) {
    player.inputs['speed'] = i < 20 ? 2.5 : 0;
    player.advance(1 / 60);
  }
}

void main() {
  test("starts where the blend says, with the blend's inputs", () {
    final player = BlendPlayer(moves(), clips: clips, inputs: {'jump': 0});
    expect(player.state, 'idle');
    expect(player.place, const BlendPlace('idle'));
    expect(player.inputs, {'speed': 0, 'jump': 0});
  });

  test('moves its place on as it plays', () {
    final player = BlendPlayer(moves(), clips: clips);
    player.advance(0.25);
    expect(player.place, const BlendPlace('idle', lap: 0.25));
    player.inputs['speed'] = 2;
    final step = player.advance(0.25);
    expect(step.change!.to, 'move');
    expect(player.state, 'move');
    expect(player.place.from!.state, 'idle');
    expect(player.sample().valueOf('', 'pose.lean'), isNotNull);
  });

  test('samples where it is without moving', () {
    final player = BlendPlayer(
      moves(),
      clips: clips,
      place: const BlendPlace('move', lap: 0.25),
      inputs: {'speed': 2},
    );
    final frame = player.sample();
    expect(frame.valueOf('', 'pose.lean'), closeTo(1, 1e-12));
    expect(player.place.lap, 0.25);
  });

  test('goes into a state when told, whatever the changes say', () {
    final player = BlendPlayer(moves(), clips: clips);
    player.enter('jump', fade: 0.2);
    expect(player.place.state, 'jump');
    expect(player.place.from, const BlendPlace('idle'));
    expect(() => player.enter('swim'), throwsArgumentError);
  });

  group('a place', () {
    test('asserts a change without playing up to it', () {
      final blend = moves();
      const place = BlendPlace(
        'move',
        lap: 3.4,
        from: BlendPlace('idle', lap: 12.5),
        faded: 0.2,
        fade: 0.25,
      );
      final step = blend.advance(
        place,
        const {'speed': 2, 'jump': 1},
        0,
        clips: clips,
      );
      expect(step.change!.to, 'jump');
      expect(step.place.state, 'jump');
      expect(step.place.from, place);
      expect(step.place.fade, 0.1);
    });

    test('saved mid-fade restores the same pose, and plays on the same', () {
      final blend = moves();
      final player = BlendPlayer(blend, clips: clips);
      play(player, 30);
      expect(player.place.from, isNotNull, reason: 'mid-fade');

      final saved = jsonEncode({
        'place': player.place.toJson(),
        'inputs': player.inputs,
      });
      final back = jsonDecode(saved) as Map<String, Object?>;
      final restored = BlendPlayer(
        blend,
        clips: clips,
        place: BlendPlace.fromJson(back['place'], blend),
        inputs: {
          for (final MapEntry(:key, :value)
              in (back['inputs']! as Map<String, Object?>).entries)
            key: (value! as num).toDouble(),
        },
      );
      expect(restored.place, player.place);
      expectSameFrame(restored.sample(), player.sample());

      for (var i = 0; i < 10; i++) {
        final one = player.advance(1 / 60);
        final other = restored.advance(1 / 60);
        expect(other.place, one.place);
        expect(other.moved.position, one.moved.position);
        expect(other.marks, one.marks);
        expectSameFrame(other.frame, one.frame);
      }
    });

    test('sent as numbers stands the same on the other end', () {
      final blend = moves();
      final here = BlendPlayer(blend, clips: clips);
      play(here, 30);
      final numbers = here.place.toNumbers(blend);
      expect(numbers, hasLength(BlendPlace.width));

      final there = BlendPlayer(
        blend,
        clips: clips,
        place: BlendPlace.fromNumbers(blend, numbers),
        inputs: here.inputs,
      );
      expect(there.place, here.place);
      expectSameFrame(there.sample(), here.sample());
    });
  });
}
