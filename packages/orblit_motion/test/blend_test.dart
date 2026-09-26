import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A pose held still: `pose.lean` at [lean] throughout.
ClipDocument still(
  String name,
  double lean, {
  double duration = 1,
  WhenDone whenDone = WhenDone.loop,
  List<Mark> marks = const [],
}) => ClipDocument(
  name: name,
  duration: duration,
  whenDone: whenDone,
  channels: [
    ClipChannel<double>(
      target: '',
      property: 'pose.lean',
      kind: ChannelKind.number,
      keys: [Key(0, lean)],
    ),
  ],
  marks: marks,
);

/// [metres] along Z in [duration] seconds, looping, carrying the character.
ClipDocument walking(
  String name, {
  double metres = 1,
  double duration = 1,
  List<Mark> marks = const [],
}) => ClipDocument(
  name: name,
  duration: duration,
  whenDone: WhenDone.loop,
  rootMotion: RootMotion(target: 'body'),
  channels: [
    ClipChannel<Vector3>(
      target: 'body',
      property: 'transform.position',
      kind: ChannelKind.vector,
      keys: [
        Key(0, Vector3.zero(), hold: Hold.linear),
        Key(duration, Vector3(0, 0, metres)),
      ],
    ),
  ],
  marks: marks,
);

const BlendChange toMove = BlendChange(
  from: 'idle',
  to: 'move',
  when: BlendCondition.above('speed', 0.1),
  fade: 0.2,
  inStep: true,
);
const BlendChange toIdle = BlendChange(
  from: 'move',
  to: 'idle',
  when: BlendCondition.below('speed', 0.1),
  fade: 0.3,
);
const BlendChange toJump = BlendChange(
  to: 'jump',
  when: BlendCondition.on('jump'),
  fade: 0.1,
  shape: Easing.out,
);
const BlendChange toLand = BlendChange(
  from: 'jump',
  to: 'land',
  when: BlendCondition.through(1),
);
const BlendChange landed = BlendChange(
  from: 'land',
  to: 'idle',
  when: BlendCondition.through(1),
  fade: 0.2,
);

/// Standing, moving along a line from a walk to a run, jumping and landing.
BlendDocument moves() => BlendDocument(
  name: 'Moves',
  inputs: const {'speed': 0, 'jump': 0},
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
    BlendState('land', plays: const BlendClip('land'), whenDone: WhenDone.hold),
  ],
  changes: const [toMove, toIdle, toJump, toLand, landed],
);

/// The clips [moves] names. A walk goes a metre a second and a run three.
Map<String, ClipDocument> movesClips() => {
  'idle': still('idle', 0, duration: 2, marks: const [Mark(0.5, 'breath')]),
  'walk': walking(
    'walk',
    marks: const [Mark(0.25, 'walk-step'), Mark(0.75, 'walk-step')],
  ),
  'run': walking(
    'run',
    metres: 3,
    marks: const [Mark(0.25, 'run-step'), Mark(0.75, 'run-step')],
  ),
  'jump': still('jump', 1, duration: 0.5, whenDone: WhenDone.hold),
  'land': still('land', -1, duration: 0.25),
};

List<String> names(BlendStep step) => [
  for (final mark in step.marks) mark.name,
];

double lean(ClipFrame frame) => frame.valueOf('', 'pose.lean')! as double;

void main() {
  group('a blend', () {
    test('starts in its first state unless it says another', () {
      expect(moves().start, 'idle');
      final blend = BlendDocument(
        name: 'Two',
        states: [
          BlendState('a', plays: const BlendClip('a')),
          BlendState('b', plays: const BlendClip('b')),
        ],
        start: 'b',
      );
      expect(blend.start, 'b');
    });

    test('needs a state, each with its own name', () {
      expect(
        () => BlendDocument(name: 'None', states: const []),
        throwsArgumentError,
      );
      expect(
        () => BlendDocument(
          name: 'Twice',
          states: [
            BlendState('a', plays: const BlendClip('a')),
            BlendState('a', plays: const BlendClip('b')),
          ],
        ),
        throwsArgumentError,
      );
      expect(
        () => BlendState('', plays: const BlendClip('a')),
        throwsArgumentError,
      );
    });

    test('starts in, and changes between, states it has', () {
      final states = [BlendState('a', plays: const BlendClip('a'))];
      expect(
        () => BlendDocument(name: 'x', states: states, start: 'b'),
        throwsArgumentError,
      );
      expect(
        () => BlendDocument(
          name: 'x',
          states: states,
          changes: const [BlendChange(to: 'b')],
        ),
        throwsArgumentError,
      );
      expect(
        () => BlendDocument(
          name: 'x',
          states: states,
          changes: const [BlendChange(from: 'b', to: 'a')],
        ),
        throwsArgumentError,
      );
    });

    test('plays forwards or not at all', () {
      expect(
        () => BlendState('a', plays: const BlendClip('a'), speed: -1),
        throwsArgumentError,
      );
      expect(
        () => BlendState('a', plays: const BlendClip('a'), speed: double.nan),
        throwsArgumentError,
      );
    });

    test('lists every clip it names and every input it reads', () {
      final blend = moves();
      expect(blend.clipNames, ['idle', 'walk', 'run', 'jump', 'land']);
      expect(blend.inputsRead.toSet(), {'speed', 'jump'});
      expect(blend.indexOf('jump'), 2);
      expect(blend.indexOf('swim'), -1);
      expect(blend.stateNamed('move')!.plays, isA<BlendLine>());
    });
  });

  group('which change comes next', () {
    test('is none while nothing holds', () {
      expect(moves().changeFor(const BlendPlace('idle'), const {}), isNull);
    });

    test('is one from the state being played', () {
      final blend = moves();
      expect(
        blend.changeFor(const BlendPlace('idle'), const {'speed': 2}),
        same(toMove),
      );
      expect(
        blend.changeFor(const BlendPlace('move'), const {'speed': 0}),
        same(toIdle),
      );
      expect(
        blend.changeFor(const BlendPlace('move'), const {'speed': 2}),
        isNull,
      );
    });

    test('is one from anywhere, but never into the state being played', () {
      final blend = moves();
      expect(
        blend.changeFor(const BlendPlace('move'), const {
          'speed': 2,
          'jump': 1,
        }),
        same(toJump),
      );
      expect(
        blend.changeFor(const BlendPlace('jump'), const {'jump': 1}),
        isNull,
      );
    });

    test('is the first that holds, in the order they are written', () {
      expect(
        moves().changeFor(const BlendPlace('idle'), const {
          'speed': 2,
          'jump': 1,
        }),
        same(toMove),
      );
    });

    test('waits for a state to play through when told to', () {
      final blend = moves();
      expect(blend.changeFor(const BlendPlace('jump', lap: 0.9), {}), isNull);
      expect(
        blend.changeFor(const BlendPlace('jump', lap: 1), {}),
        same(toLand),
      );
    });

    test('reads an input it is not given as the blend says it starts', () {
      final blend = BlendDocument(
        name: 'Grounded',
        inputs: const {'grounded': 1},
        states: [
          BlendState('stand', plays: const BlendClip('stand')),
          BlendState('fall', plays: const BlendClip('fall')),
        ],
        changes: const [
          BlendChange(
            to: 'fall',
            when: BlendCondition.not(BlendCondition.on('grounded')),
          ),
        ],
      );
      expect(blend.changeFor(const BlendPlace('stand'), const {}), isNull);
      expect(
        blend.changeFor(const BlendPlace('stand'), const {
          'grounded': double.nan,
        }),
        isNull,
      );
      expect(
        blend.changeFor(const BlendPlace('stand'), const {'grounded': 0}),
        isNotNull,
      );
    });
  });

  group('which clips have a say', () {
    test('shares a fade between the states in it', () {
      const place = BlendPlace(
        'move',
        lap: 0.5,
        from: BlendPlace('idle', lap: 3.2),
        fade: 0.4,
        faded: 0.2,
        shape: Easing.linear,
      );
      final weights = moves().weightsAt(place, const {'speed': 2});
      expect(
        [for (final one in weights) (one.state, one.clip, one.lap)],
        [('move', 'walk', 0.5), ('move', 'run', 0.5), ('idle', 'idle', 3.2)],
      );
      expect(
        [for (final one in weights) one.weight],
        [closeTo(0.25, 1e-12), closeTo(0.25, 1e-12), closeTo(0.5, 1e-12)],
      );
    });

    test('leaves out a state with no say yet', () {
      final place = const BlendPlace('idle').enter('jump', fade: 0.1);
      expect(
        [for (final one in moves().weightsAt(place, const {})) one.clip],
        ['idle'],
      );
    });
  });

  group('how long a state is', () {
    test('is its clips mixed as they are', () {
      final blend = moves();
      final clips = {
        'walk': walking('walk', duration: 1.2),
        'run': walking('run', duration: 0.8),
      };
      expect(
        blend.lengthOf('move', const {'speed': 2}, clips: clips),
        closeTo(1, 1e-12),
      );
      expect(
        blend.lengthOf('move', const {'speed': 3}, clips: clips),
        closeTo(0.8, 1e-12),
      );
    });

    test('is nought with no clips to measure', () {
      expect(moves().lengthOf('move', const {}, clips: const {}), 0);
      expect(moves().lengthOf('swim', const {}, clips: movesClips()), 0);
    });
  });

  group('the pose', () {
    test('mixes a fade as it has got to', () {
      const place = BlendPlace(
        'jump',
        from: BlendPlace('idle'),
        faded: 0.03,
        fade: 0.1,
        shape: Easing.linear,
      );
      final frame = moves().sampleAt(place, const {}, clips: movesClips());
      expect(lean(frame), closeTo(0.3, 1e-12));
    });

    test('holds root motion in place', () {
      final frame = moves().sampleAt(const BlendPlace('move', lap: 0.5), const {
        'speed': 2,
      }, clips: movesClips());
      final body = frame.valueOf('body', 'transform.position')! as Vector3;
      expect(body.length, lessThan(1e-12));
    });

    test('shares out what a missing clip would have had', () {
      const place = BlendPlace(
        'jump',
        from: BlendPlace('idle'),
        faded: 0.03,
        fade: 0.1,
      );
      final clips = movesClips()..remove('idle');
      final frame = moves().sampleAt(place, const {}, clips: clips);
      expect(lean(frame), 1);
    });

    test('is where the loudest clip is', () {
      final frame = moves().sampleAt(
        const BlendPlace('move', lap: 1.25),
        const {'speed': 2.5},
        clips: movesClips(),
      );
      expect(frame.at, closeTo(0.25, 1e-12));
    });
  });

  group('playing on', () {
    test('goes through a state by the time its clips take', () {
      final step = moves().advance(
        const BlendPlace('idle'),
        const {},
        0.5,
        clips: movesClips(),
      );
      expect(step.place, const BlendPlace('idle', lap: 0.25));
      expect(step.change, isNull);
    });

    test("goes at the state's own speed", () {
      final blend = BlendDocument(
        name: 'Fast',
        states: [
          BlendState('idle', plays: const BlendClip('idle'), speed: 2),
          BlendState('frozen', plays: const BlendClip('idle'), speed: 0),
        ],
      );
      final clips = movesClips();
      expect(
        blend.advance(const BlendPlace('idle'), {}, 0.5, clips: clips).place,
        const BlendPlace('idle', lap: 0.5),
      );
      expect(
        blend
            .advance(
              const BlendPlace('frozen', lap: 0.3),
              {},
              0.5,
              clips: clips,
            )
            .place,
        const BlendPlace('frozen', lap: 0.3),
      );
    });

    test('carries the character as far as the mix of clips goes', () {
      final step = moves().advance(
        const BlendPlace('move'),
        const {'speed': 2},
        0.5,
        clips: movesClips(),
      );
      expect(step.place.lap, closeTo(0.5, 1e-12));
      expect((step.moved.position - Vector3(0, 0, 1)).length, lessThan(1e-9));
    });

    test('carries on through a fade, the state fading out included', () {
      final blend = moves();
      final clips = movesClips();
      var place = blend
          .advance(
            const BlendPlace('idle', lap: 0.5),
            const {'speed': 2},
            0,
            clips: clips,
          )
          .place;
      expect(place.state, 'move');
      // In step with the idle, as the change says.
      expect(place.lap, 0.5);
      expect(place.from, const BlendPlace('idle', lap: 0.5));

      var step = blend.advance(place, const {'speed': 2}, 0.1, clips: clips);
      place = step.place;
      expect(place.faded, closeTo(0.1, 1e-12));
      expect(place.lap, closeTo(0.6, 1e-12));
      expect(place.from!.lap, closeTo(0.55, 1e-12));
      // Half faded, smoothly, so half standing still and half moving at two
      // metres a second.
      expect(step.moved.position.z, closeTo(0.5 * 0.2, 1e-9));

      step = blend.advance(place, const {'speed': 2}, 0.15, clips: clips);
      expect(step.place.from, isNull);
      expect(step.place.lap, closeTo(0.75, 1e-12));
    });

    test('keeps in step when the change says so', () {
      final place = moves()
          .advance(
            const BlendPlace('idle', lap: 1.75),
            const {'speed': 2},
            0,
            clips: movesClips(),
          )
          .place;
      expect(place.lap, 0.75);
    });

    test('takes a change as soon as the state has played through', () {
      final blend = moves();
      final clips = movesClips();
      var step = blend.advance(const BlendPlace('jump'), {}, 0.4, clips: clips);
      expect(step.place, const BlendPlace('jump', lap: 0.8));
      step = blend.advance(step.place, {}, 0.2, clips: clips);
      expect(step.change, same(toLand));
      expect(step.place, const BlendPlace('land'));
      expect(lean(step.frame), -1);
    });

    test('takes one change a step, however they chain', () {
      final blend = BlendDocument(
        name: 'Round',
        states: [
          BlendState('a', plays: const BlendClip('idle')),
          BlendState('b', plays: const BlendClip('idle')),
          BlendState('c', plays: const BlendClip('idle')),
        ],
        changes: const [
          BlendChange(from: 'a', to: 'b'),
          BlendChange(from: 'b', to: 'c'),
          BlendChange(from: 'c', to: 'a'),
        ],
      );
      final clips = movesClips();
      var place = const BlendPlace('a');
      final visited = <String>[];
      for (var i = 0; i < 4; i++) {
        place = blend.advance(place, {}, 1 / 60, clips: clips).place;
        visited.add(place.state);
      }
      expect(visited, ['b', 'c', 'a', 'b']);
    });

    test('keeps no more than the deepest few fades', () {
      final blend = moves();
      final clips = movesClips();
      var place = const BlendPlace('idle');
      for (final values in [
        {'speed': 2.0},
        {'speed': 2.0, 'jump': 1.0},
        {'speed': 0.0},
      ]) {
        place = blend.advance(place, values, 0.01, clips: clips).place;
      }
      place = place.enter('move', fade: 1).enter('idle', fade: 1);
      expect(place.depth, BlendPlace.deepest);
      final step = blend.advance(place, const {}, 0.01, clips: clips);
      expect(step.place.depth, lessThanOrEqualTo(BlendPlace.deepest));
    });
  });

  group('marks', () {
    test('fire every lap of a loop, the first frame included', () {
      final blend = BlendDocument(
        name: 'Walk',
        states: [BlendState('walk', plays: const BlendClip('walk'))],
      );
      final clips = {
        'walk': walking(
          'walk',
          marks: const [Mark(0, 'start'), Mark(0.5, 'middle')],
        ),
      };
      final step = blend.advance(
        const BlendPlace('walk'),
        const {},
        2,
        clips: clips,
      );
      expect(names(step), ['start', 'middle', 'start', 'middle']);
    });

    test('come from the clip with the most say, and only it', () {
      final blend = moves();
      final clips = movesClips();
      expect(
        names(
          blend.advance(
            const BlendPlace('move'),
            const {'speed': 1.5},
            0.5,
            clips: clips,
          ),
        ),
        ['walk-step'],
      );
      expect(
        names(
          blend.advance(
            const BlendPlace('move'),
            const {'speed': 2.5},
            0.5,
            clips: clips,
          ),
        ),
        ['run-step'],
      );
    });

    test('come from the state being played, not one fading out', () {
      const place = BlendPlace(
        'move',
        from: BlendPlace('idle'),
        fade: 5,
        shape: Easing.linear,
      );
      final step = moves().advance(
        place,
        const {'speed': 1.5},
        1,
        clips: movesClips(),
      );
      expect(names(step), ['walk-step', 'walk-step']);
    });

    test('fire once for a clip that holds', () {
      final blend = BlendDocument(
        name: 'Once',
        states: [BlendState('hit', plays: const BlendClip('hit'))],
      );
      final clips = {
        'hit': still(
          'hit',
          0,
          whenDone: WhenDone.hold,
          marks: const [Mark(0, 'swing'), Mark(1, 'land')],
        ),
      };
      var step = blend.advance(const BlendPlace('hit'), {}, 0.5, clips: clips);
      expect(names(step), ['swing']);
      step = blend.advance(step.place, {}, 5, clips: clips);
      expect(names(step), ['land']);
      step = blend.advance(step.place, {}, 5, clips: clips);
      expect(names(step), isEmpty);
    });
  });

  group('odd steps', () {
    test('get through a stall without walking every lap', () {
      final blend = BlendDocument(
        name: 'Walk',
        states: [BlendState('walk', plays: const BlendClip('walk'))],
      );
      final step = blend.advance(
        const BlendPlace('walk'),
        const {},
        1e6,
        clips: {'walk': walking('walk')},
      );
      expect(step.place.lap, closeTo(1e6, 1e-6));
      expect(step.moved.position.z, closeTo(1e6, 1e-3));
    });

    test('come back to where they started, for a bounce there and back', () {
      final blend = BlendDocument(
        name: 'Pace',
        states: [
          BlendState(
            'pace',
            plays: const BlendClip('walk'),
            whenDone: WhenDone.bounce,
          ),
        ],
      );
      final clips = {'walk': walking('walk')};
      final there = blend.advance(
        const BlendPlace('pace'),
        {},
        1.5,
        clips: clips,
      );
      expect(there.moved.position.z, closeTo(0.5, 1e-9));
      final back = blend.advance(there.place, {}, 0.5, clips: clips);
      expect(back.moved.position.z, closeTo(-0.5, 1e-9));
      expect(
        blend
            .advance(const BlendPlace('pace'), {}, 2, clips: clips)
            .moved
            .position
            .z,
        closeTo(0, 1e-9),
      );
    });

    test('play on without a clip that is missing', () {
      final clips = movesClips()..remove('run');
      final step = moves().advance(
        const BlendPlace('move'),
        const {'speed': 2},
        0.5,
        clips: clips,
      );
      expect(step.place.lap, closeTo(0.5, 1e-12));
      expect(step.moved.position.z, closeTo(0.5, 1e-9));
      expect(
        [
          for (final one in moves().weightsAt(step.place, {'speed': 2}))
            one.clip,
        ],
        ['walk', 'run'],
      );
    });

    test('are through at once for a state with nothing to play', () {
      final blend = BlendDocument(
        name: 'Empty',
        states: [
          BlendState('gone', plays: const BlendClip('nothing')),
          BlendState('idle', plays: const BlendClip('idle')),
        ],
        changes: const [
          BlendChange(
            from: 'gone',
            to: 'idle',
            when: BlendCondition.through(1),
          ),
        ],
      );
      final clips = movesClips();
      expect(
        blend.advance(const BlendPlace('gone'), {}, 0, clips: clips).place,
        const BlendPlace('gone'),
      );
      expect(
        blend.advance(const BlendPlace('gone'), {}, 0.1, clips: clips).place,
        const BlendPlace('idle'),
      );
    });

    test('play nothing for a step that is not a length of time', () {
      for (final seconds in [-1.0, double.nan, double.infinity]) {
        final step = moves().advance(
          const BlendPlace('idle', lap: 0.5),
          const {},
          seconds,
          clips: movesClips(),
        );
        expect(step.place, const BlendPlace('idle', lap: 0.5));
      }
    });
  });

  group('in a file', () {
    test('reads back what it writes, with nothing to note', () {
      final blend = BlendDocument(
        name: 'Moves',
        inputs: const {'speed': 0, 'sideways': 0, 'jump': 0},
        start: 'idle',
        states: [
          BlendState(
            'move',
            speed: 1.25,
            plays: BlendLine('speed', [
              const LinePoint(0, BlendClip('idle')),
              LinePoint(
                2,
                BlendPlane('sideways', 'speed', const [
                  PlanePoint(0, 2, BlendClip('walk')),
                  PlanePoint(1, 0, BlendClip('strafe')),
                ]),
              ),
            ]),
          ),
          BlendState('idle', plays: const BlendClip('idle')),
          BlendState(
            'jump',
            plays: const BlendClip('jump'),
            whenDone: WhenDone.hold,
          ),
        ],
        changes: const [
          BlendChange(
            from: 'idle',
            to: 'move',
            when: BlendCondition.above('speed', 0.1),
            fade: 0.2,
            inStep: true,
          ),
          BlendChange(
            to: 'jump',
            when: BlendCondition.all([
              BlendCondition.on('jump'),
              BlendCondition.not(BlendCondition.through(0.5)),
            ]),
            fade: 0.1,
            shape: Easing.out,
          ),
          BlendChange(from: 'jump', to: 'idle'),
        ],
      );
      final text = blend.encode();
      final load = BlendDocument.decode(text);
      expect(load.problems, isEmpty);
      expect(load.blend.encode(), text);
      expect(load.blend.start, 'idle');
      expect(load.blend.states.first.speed, 1.25);
      expect(load.blend.changes[1].shape, Easing.out);
      expect(load.blend.changes.first.inStep, isTrue);
    });

    test('puts anything short on a line of its own', () {
      final text = moves().encode();
      expect(text, contains('\n    {"name":"idle","clip":"idle"},\n'));
      expect(
        text,
        contains('\n    {"from":"jump","to":"land","when":{"through":1.0}},\n'),
      );
      expect(text, contains('\n      "inStep": true\n'));
      expect(text, startsWith('{\n  "kind": "orblit.blend",\n'));
    });

    test('is not a blend unless it says so', () {
      expect(
        () => BlendDocument.decode('not json'),
        throwsA(isA<BlendFormatException>()),
      );
      expect(
        () => BlendDocument.decode('{"kind": "orblit.clip"}'),
        throwsA(isA<BlendFormatException>()),
      );
      expect(
        () => BlendDocument.decode(
          jsonEncode({...moves().toJson(), 'formatVersion': 99}),
        ),
        throwsA(isA<BlendFormatException>()),
      );
      expect(
        () => BlendDocument.decode(
          jsonEncode({...moves().toJson(), 'states': <Object?>[]}),
        ),
        throwsA(isA<BlendFormatException>()),
      );
    });

    test('leaves out what it cannot read, and says so', () {
      final load = BlendDocument.decode(
        jsonEncode({
          'kind': 'orblit.blend',
          'formatVersion': 1,
          'name': 'Rough',
          'inputs': {'speed': 'fast'},
          'start': 'swim',
          'states': [
            {'name': 'idle', 'clip': 'idle', 'speed': -2},
            {'clip': 'nameless'},
            {'name': 'idle', 'clip': 'again'},
            {'name': 'empty'},
            {'name': 'walk', 'clip': 'walk'},
          ],
          'changes': [
            {'from': 'idle', 'to': 'swim'},
            {
              'from': 'idle',
              'to': 'walk',
              'when': {'input': 'speed', 'above': 'slow'},
            },
            {'from': 'walk', 'to': 'idle', 'fade': -1},
            {
              'to': 'walk',
              'when': {'input': 'hurry'},
            },
          ],
        }),
      );
      final blend = load.blend;
      expect([for (final state in blend.states) state.name], ['idle', 'walk']);
      expect(blend.states.first.speed, 1);
      expect(blend.start, 'idle');
      expect(blend.inputs, {'speed': 0});
      expect(blend.changes, hasLength(2));
      expect(blend.changes.first.fade, 0);
      expect(load.problems, [
        'The input "speed" starts at something that is not a number, so it '
            'starts at nought.',
        'The state "idle" has a speed it cannot play at, so it plays at one.',
        'A state with no name was left out.',
        'Two states are called "idle"; the second was left out.',
        'The state "empty" plays nothing: it names no clip, line or plane.',
        'The blend starts in "swim", which is not one of its states, so it '
            'starts in "idle".',
        'The change from "idle" to "swim" is not between two of the states, '
            'so it was left out.',
        'The change from "idle" to "walk" compares "speed" with something '
            'that is not a number.',
        'The change from "idle" to "walk" was left out, since its condition '
            'could not be read.',
        'The change from "walk" to "idle" fades for a time that is not one, '
            'so it cuts.',
        'The blend reads "hurry", which it does not declare, so it is nought '
            'until something sets it.',
      ]);
    });
  });
}
