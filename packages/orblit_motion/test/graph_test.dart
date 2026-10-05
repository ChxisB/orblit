import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

ClipDocument moving(String name, double from, double to) => ClipDocument(
  name: name,
  duration: 1,
  whenDone: WhenDone.loop,
  channels: [
    ClipChannel<double>(
      target: '',
      property: 'pose.value',
      kind: ChannelKind.number,
      keys: [
        Key(0, from, hold: Hold.linear),
        Key(1, to),
      ],
    ),
  ],
);

BlendDocument graph({bool fromPose = false}) => BlendDocument(
  name: 'Three',
  inputs: const {'go': 0},
  states: [
    for (final name in ['a', 'b', 'c', 'alone'])
      BlendState(name, plays: BlendClip(name)),
  ],
  changes: [
    BlendChange(
      from: 'a',
      to: 'b',
      when: const BlendCondition.on('go'),
      fade: 0.4,
      shape: Easing.linear,
      fromPose: fromPose,
    ),
    const BlendChange(
      from: 'b',
      to: 'c',
      when: BlendCondition.on('go'),
      fade: 0.2,
      shape: Easing.linear,
    ),
  ],
);

double value(ClipFrame frame) => frame.valueOf('', 'pose.value')! as double;

Map<String, ClipDocument> clips() => {
  'a': moving('A', 0, 10),
  'b': moving('B', 20, 30),
  'c': moving('C', 40, 50),
  'alone': moving('Alone', 60, 70),
};

BlendDocument nested() => BlendDocument(
  name: 'Outer',
  inputs: const {'jump': 0, 'land': 0},
  states: [
    BlendState(
      'ground',
      plays: BlendGraph(
        BlendDocument(
          name: 'Ground',
          inputs: const {'speed': 0.5},
          states: [
            BlendState('a', plays: const BlendClip('a')),
            BlendState('b', plays: const BlendClip('b')),
          ],
          changes: const [
            BlendChange(
              from: 'a',
              to: 'b',
              when: BlendCondition.above('speed', 1),
            ),
          ],
        ),
      ),
    ),
    BlendState('c', plays: const BlendClip('c')),
  ],
  changes: const [
    BlendChange(from: 'ground', to: 'c', when: BlendCondition.on('jump')),
    BlendChange(from: 'c', to: 'ground', when: BlendCondition.on('land')),
  ],
);

void main() {
  test('pair fades choose the dominant clip that is actually available', () {
    final blend = BlendDocument(
      name: 'Missing',
      inputs: const {'speed': 0.2},
      states: [
        BlendState(
          'mixed',
          plays: BlendLine('speed', const [
            LinePoint(0, BlendClip('missing')),
            LinePoint(1, BlendClip('a')),
          ]),
        ),
        BlendState('b', plays: const BlendClip('b')),
      ],
      fades: [BlendFade('a', 'b', 0.75)],
    );
    final player = BlendPlayer(blend, clips: clips());
    player.enter('b', fade: 0.2);
    expect(player.place.fade, 0.75);
  });
  test('snapshot construction and reading reject malformed pose values', () {
    final invalid = ClipFrame(0)
      ..bones[''] = {'arm': BoneLocal(rotation: Quaternion(0, 0, 0, 0))};
    expect(() => PoseSnapshot(invalid), throwsArgumentError);
    expect(
      PoseSnapshot.fromJson({
        'at': 0,
        'values': <String, Object?>{},
        'bones': {
          '': {
            'arm': {
              'position': [1, 2],
            },
          },
        },
      }),
      isNull,
    );
  });

  test('ambiguous parent names cannot shadow expanded leaves', () {
    final inner = BlendDocument(
      name: 'Inner',
      states: [BlendState('a', plays: const BlendClip('a'))],
    );
    expect(
      () => BlendDocument(
        name: 'Ambiguous',
        states: [
          BlendState('root', plays: BlendGraph(inner)),
          BlendState('root/a', plays: BlendGraph(inner)),
        ],
      ),
      throwsArgumentError,
    );
  });
  test('saved routes are canceled when an edge disappears', () {
    final blend = graph();
    final player = BlendPlayer(blend, clips: clips())..travel('c');
    final saved = jsonDecode(jsonEncode(player.place.toJson()));
    final edited = BlendDocument(
      name: 'Edited',
      states: blend.states,
      changes: const [BlendChange(from: 'a', to: 'b')],
    );
    final restored = BlendPlace.fromJson(saved, edited)!;
    expect(restored.route, isEmpty);
    final supplied = ['b', 'c'];
    final place = const BlendPlace('a').withRoute(supplied);
    supplied.clear();
    expect(place.route, ['b', 'c']);
    expect(() => place.route.clear(), throwsUnsupportedError);
  });

  test('nested wildcard changes stay inside their graph', () {
    final inner = BlendDocument(
      name: 'Inner',
      inputs: const {'hit': 0},
      states: [
        BlendState('a', plays: const BlendClip('a')),
        BlendState('b', plays: const BlendClip('b')),
      ],
      changes: const [BlendChange(to: 'b', when: BlendCondition.on('hit'))],
    );
    final outer = BlendDocument(
      name: 'Outer',
      inputs: const {'hit': 1},
      states: [
        BlendState('inside', plays: BlendGraph(inner), speed: 0.5),
        BlendState('c', plays: const BlendClip('c')),
      ],
    );
    final player = BlendPlayer(outer, clips: clips());
    player.advance(0.4);
    expect(player.state, 'inside/b');
    expect(player.place.lap, 0);
    player.advance(0.4);
    expect(player.place.lap, 0.2);
    player.enter('c');
    player.advance(0);
    expect(player.state, 'c');
  });

  test('malformed sync and pair data is reported without losing a state', () {
    final raw = graph().toJson();
    (raw['states']! as List<Map<String, Object?>>).first['sync'] = ['left'];
    raw['fades'] = [
      {'from': 'a', 'to': 'b', 'seconds': -1},
    ];
    final loaded = BlendDocument.decode(jsonEncode(raw));
    expect(loaded.blend.states, hasLength(4));
    expect(loaded.blend.fades, isEmpty);
    expect(loaded.blend.states.first.sync, isEmpty);
    expect(loaded.problems, hasLength(2));
  });

  test(
    'travel finds a directed shortest route and leaves unreachable targets alone',
    () {
      final blend = graph();
      expect(blend.routeTo('a', 'c'), ['b', 'c']);
      expect(blend.routeTo('a', 'a'), isEmpty);
      expect(blend.routeTo('c', 'a'), isNull);
      final player = BlendPlayer(blend, clips: clips());
      final before = player.place;
      expect(() => player.travel('alone'), throwsArgumentError);
      expect(player.place, same(before));
    },
  );

  test('travel overrides conditions and waits for fades between edges', () {
    final player = BlendPlayer(graph(), clips: clips());
    player.travel('c');
    expect(player.place.route, ['b', 'c']);
    player.advance(0);
    expect(player.state, 'b');
    expect(player.place.route, ['c']);
    player.advance(0.2);
    expect(player.state, 'b');
    player.advance(0.2);
    expect(player.state, 'c');
    expect(player.place.fade, 0.2);
    expect(player.place.route, isEmpty);
  });

  test(
    'a current-pose fade holds its source still while the destination moves',
    () {
      final player = BlendPlayer(graph(), clips: clips());
      player.advance(0.2);
      player.enter('b', fade: 1, shape: Easing.linear, fromPose: true);
      expect(value(player.sample()), closeTo(2, 1e-9));
      expect(value(player.advance(0.5).frame), closeTo(13.5, 1e-9));
      expect(player.place.frozen, isNotNull);
    },
  );

  test('interrupting a fade freezes the visible mixed pose without a jump', () {
    final player = BlendPlayer(graph(), clips: clips());
    player.enter('b', fade: 1, shape: Easing.linear);
    player.advance(0.2);
    final before = value(player.sample());
    player.enter('c', fade: 1, shape: Easing.linear, fromPose: true);
    expect(value(player.sample()), closeTo(before, 1e-9));
    expect(value(player.advance(0.5).frame), closeTo((before + 45) / 2, 1e-9));
  });

  test('automatic transitions can freeze the current pose', () {
    final player = BlendPlayer(
      graph(fromPose: true),
      clips: clips(),
      inputs: {'go': 1},
    );
    player.advance(0.2);
    expect(player.state, 'b');
    expect(value(player.sample()), closeTo(2, 1e-9));
    player.inputs['go'] = 0;
    expect(value(player.advance(0.2).frame), closeTo(12, 1e-9));
  });

  test(
    'a clip-pair fade overrides the edge duration in either direction of entry',
    () {
      final blend = BlendDocument(
        name: 'Pair',
        inputs: graph().inputs,
        states: graph().states,
        changes: graph().changes,
        fades: [BlendFade('a', 'b', 0.75)],
      );
      final player = BlendPlayer(blend, clips: clips(), inputs: {'go': 1});
      player.advance(0);
      expect(player.place.fade, 0.75);
      player.enter('a');
      player.enter('b', fade: 2);
      expect(player.place.fade, 0.75);
      final loaded = BlendDocument.decode(blend.encode());
      expect(loaded.problems, isEmpty);
      expect(loaded.blend.fades.single.seconds, 0.75);
    },
  );

  test(
    'nested graphs play children, enter their start, and exit from any child',
    () {
      final player = BlendPlayer(nested(), clips: clips());
      expect(player.state, 'ground/a');
      player.inputs['speed'] = 2;
      player.advance(0);
      expect(player.state, 'ground/b');
      player.inputs['jump'] = 1;
      player.advance(0);
      expect(player.state, 'c');
      player.inputs['land'] = 1;
      player.advance(0);
      expect(player.state, 'ground/a');
      player.enter('ground');
      expect(player.state, 'ground/a');
      expect(player.blend.routeTo('ground/a', 'c'), ['c']);
    },
  );

  test(
    'nested assets round-trip without losing graph boundaries or inputs',
    () {
      final original = nested();
      final loaded = BlendDocument.decode(original.encode());
      expect(loaded.problems, isEmpty);
      expect(loaded.blend.toJson(), original.toJson());
      expect(loaded.blend.inputs['speed'], 0.5);
      expect(loaded.blend.clipNames, containsAll(['a', 'b', 'c']));
      expect(loaded.blend.states.map((state) => state.name), [
        'ground/a',
        'ground/b',
        'c',
      ]);
    },
  );

  test('multiple graph levels keep their starts and numeric places', () {
    final blend = BlendDocument(
      name: 'Root',
      states: [BlendState('outer', plays: BlendGraph(nested()))],
    );
    final player = BlendPlayer(blend, clips: clips());
    expect(player.state, 'outer/ground/a');
    player.enter('outer/ground');
    expect(player.state, 'outer/ground/a');
    expect(
      BlendPlace.fromNumbers(blend, player.place.toNumbers(blend)),
      player.place,
    );
    expect(BlendDocument.decode(blend.encode()).blend.start, player.state);
  });

  test('frozen poses and routes round-trip and advance identically', () {
    final blend = graph();
    final player = BlendPlayer(blend, clips: clips());
    player.advance(0.2);
    player.enter('b', fade: 1, fromPose: true);
    player.travel('c');
    player.advance(0.2);
    final saved = jsonDecode(jsonEncode(player.place.toJson()));
    final numbers = player.place.toNumbers(blend);
    expect(numbers.length, greaterThan(BlendPlace.width));
    for (final restored in [
      BlendPlace.fromJson(saved, blend),
      BlendPlace.fromNumbers(blend, numbers),
    ]) {
      expect(restored, player.place);
      final copy = BlendPlayer(blend, clips: clips(), place: restored);
      expect(value(copy.sample()), value(player.sample()));
      expect(
        value(copy.advance(0.2).frame),
        value(
          blend.advance(player.place, player.inputs, 0.2, clips: clips()).frame,
        ),
      );
    }
    numbers[numbers.length - 1] = 999;
    expect(BlendPlace.fromNumbers(blend, numbers), isNull);
  });

  test('frozen vectors cannot be changed through source or sampled frames', () {
    final frame = ClipFrame(0)
      ..bones[''] = {'arm': BoneLocal(position: Vector3(1, 2, 3))};
    final snapshot = PoseSnapshot(frame);
    frame.boneOf('', 'arm')!.position!.x = 20;
    snapshot.sample().boneOf('', 'arm')!.position!.x = 30;
    expect(snapshot.sample().boneOf('', 'arm')!.position!.x, 1);
    expect(
      PoseSnapshot.fromJson(jsonDecode(jsonEncode(snapshot.toJson()))),
      snapshot,
    );
  });

  test('format one loads with its original playback defaults', () {
    final raw = graph().toJson()..['formatVersion'] = 1;
    final loaded = BlendDocument.decode(jsonEncode(raw));
    expect(loaded.problems, isEmpty);
    expect(loaded.blend.start, 'a');
    expect(loaded.blend.changes.first.fromPose, isFalse);
    expect(loaded.blend.states.first.sync, isEmpty);
  });
}
