import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

ClipDocument stride(String name, double duration, double left, double right) =>
    ClipDocument(
      name: name,
      duration: duration,
      whenDone: WhenDone.loop,
      channels: [
        ClipChannel<double>(
          target: '',
          property: 'foot.left',
          kind: ChannelKind.number,
          keys: [
            const Key(0, -1.0, hold: Hold.linear),
            Key(left, 0.0, hold: Hold.linear),
            Key(right, 1.0, hold: Hold.linear),
            Key(duration, -1.0),
          ],
        ),
      ],
      marks: [Mark(left, 'left'), Mark(right, 'right')],
    );

BlendDocument locomotion(
  BlendSource source, {
  List<String> sync = const ['left', 'right'],
}) => BlendDocument(
  name: 'Locomotion',
  states: [BlendState('move', plays: source, sync: sync)],
  inputs: const {'speed': 0.5, 'x': 0.5, 'y': 0},
);

void main() {
  test('root motion follows the same contact clock as the pose', () {
    ClipDocument walk(
      String name,
      double duration,
      double distance,
      double left,
      double right,
    ) => ClipDocument(
      name: name,
      duration: duration,
      whenDone: WhenDone.loop,
      rootMotion: RootMotion(bone: 'hips'),
      marks: [Mark(left, 'left'), Mark(right, 'right')],
      channels: [
        ClipChannel<Vector3>(
          target: '',
          bone: 'hips',
          property: 'position',
          kind: ChannelKind.vector,
          keys: [
            Key(0, Vector3.zero(), hold: Hold.linear),
            Key(duration, Vector3(0, 0, distance)),
          ],
        ),
      ],
    );
    final graph = locomotion(
      BlendLine('speed', const [
        LinePoint(0, BlendClip('walk')),
        LinePoint(1, BlendClip('run')),
      ]),
    );
    final player = BlendPlayer(
      graph,
      clips: {
        'walk': walk('Walk', 2, 2, 0.2, 1.6),
        'run': walk('Run', 1, 4, 0.3, 0.6),
      },
    );
    final step = player.advance(0.75);
    expect(step.moved.position.z, closeTo(1.3, 1e-9));
    expect(step.frame.boneOf('', 'hips')!.position!.z, 0);
    expect(step.marks.map((mark) => mark.name), ['left', 'right']);
  });
  final clips = {
    'walk': stride('Walk', 2, 0.2, 1.6),
    'run': stride('Run', 1, 0.3, 0.6),
  };
  final line = BlendLine('speed', const [
    LinePoint(0, BlendClip('walk')),
    LinePoint(1, BlendClip('run')),
  ]);

  test(
    'walk-run feet match at both contacts despite different marker times',
    () {
      final graph = locomotion(line);
      for (final speed in [0.0, 0.2, 0.5, 0.8, 1.0]) {
        final left = graph.sampleAt(const BlendPlace('move'), {
          'speed': speed,
        }, clips: clips);
        final right = graph.sampleAt(const BlendPlace('move', lap: 0.5), {
          'speed': speed,
        }, clips: clips);
        expect(left.valueOf('', 'foot.left'), closeTo(0, 1e-9));
        expect(right.valueOf('', 'foot.left'), closeTo(1, 1e-9));
      }
    },
  );

  test('changing blend weights mid-stride keeps the shared contact phase', () {
    final player = BlendPlayer(locomotion(line), clips: clips);
    player.advance(0.75);
    expect(player.place.lap, 0.5);
    player.inputs['speed'] = 1;
    expect(player.sample().valueOf('', 'foot.left'), closeTo(1, 1e-9));
    player.inputs['speed'] = 0;
    expect(player.sample().valueOf('', 'foot.left'), closeTo(1, 1e-9));
  });

  test('one contact event per step comes from the dominant cyclic clip', () {
    final player = BlendPlayer(locomotion(line), clips: clips);
    final marks = <String>[];
    for (var i = 0; i < 60; i++) {
      marks.addAll(player.advance(1.5 / 60).marks.map((mark) => mark.name));
    }
    expect(marks, ['left', 'right', 'left']);
  });

  test('sync applies to planes as well as lines', () {
    final graph = locomotion(
      BlendPlane('x', 'y', const [
        PlanePoint(0, 0, BlendClip('walk')),
        PlanePoint(1, 0, BlendClip('run')),
      ]),
    );
    expect(
      graph
          .sampleAt(const BlendPlace('move', lap: 0.5), const {}, clips: clips)
          .valueOf('', 'foot.left'),
      closeTo(1, 1e-9),
    );
  });

  test('missing sync contacts fall back to ordinary laps', () {
    final graph = locomotion(line, sync: const ['left', 'missing']);
    final plain = locomotion(line, sync: const []);
    expect(
      graph
          .sampleAt(const BlendPlace('move', lap: 0.5), const {}, clips: clips)
          .valueOf('', 'foot.left'),
      plain
          .sampleAt(const BlendPlace('move', lap: 0.5), const {}, clips: clips)
          .valueOf('', 'foot.left'),
    );
  });

  test('sync names round-trip in the blend file', () {
    final graph = locomotion(line);
    final loaded = BlendDocument.decode(graph.encode());
    expect(loaded.problems, isEmpty);
    expect(loaded.blend.states.single.sync, ['left', 'right']);
    expect(
      () => BlendState(
        'bad',
        plays: const BlendClip('walk'),
        sync: ['left', 'left'],
      ),
      throwsArgumentError,
    );
  });
}
