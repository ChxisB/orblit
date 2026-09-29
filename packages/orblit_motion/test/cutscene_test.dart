import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// The cube slides along x for four seconds, seen first from the front and
/// then from the side, with a second of the two blended between.
CutsceneDocument slide() => CutsceneDocument(
  motion: ClipDocument(
    name: 'Cube slides',
    duration: 4,
    channels: [
      ClipChannel<Vector3>(
        target: 'cube',
        property: 'transform.position',
        kind: ChannelKind.vector,
        keys: [Key(0, Vector3.zero()), Key(4, Vector3(4, 0, 0))],
      ),
      ClipChannel<double>(
        target: 'street1/lamp3/bulb',
        property: 'light.power',
        kind: ChannelKind.number,
        keys: const [Key(0, 10), Key(2, 80)],
      ),
    ],
    marks: const [
      Mark(3, 'door opens', payload: {'door': 'front'}),
    ],
  ),
  shots: [
    CutsceneShot(camera: 'side', start: 2, duration: 2),
    CutsceneShot(camera: 'front', start: 0, duration: 3),
  ],
  sounds: [CutsceneSound(sound: 'sounds/whoosh.ogg', start: 1, duration: 2)],
);

Map<String, double> weights(CutsceneDocument cutscene, double at) => {
  for (final shot in cutscene.shotsAt(at)) shot.camera: shot.weight,
};

String file(Map<String, Object?> json) => jsonEncode({
  'kind': CutsceneDocument.marker,
  'formatVersion': CutsceneDocument.formatVersion,
  ...json,
});

void main() {
  group('the file', () {
    test('reads back as the cutscene it was written from', () {
      final written = slide();
      final load = CutsceneDocument.decode(written.encode());
      expect(load.problems, isEmpty);
      expect(load.cutscene.toJson(), written.toJson());
      expect(load.cutscene.name, 'Cube slides');
      expect(load.cutscene.shots.map((s) => s.camera), ['front', 'side']);
      expect(load.cutscene.sounds.single.sound, 'sounds/whoosh.ogg');
      expect(load.cutscene.motion.marks.single.payload, {'door': 'front'});
    });

    test('writes one key, mark, shot and sound to a line', () {
      final lines = slide().encode().split('\n');
      expect(lines.where((line) => line.contains('"camera"')), hasLength(2));
      expect(
        lines.singleWhere((line) => line.contains('"camera":"front"')),
        contains('"duration":3.0'),
      );
      expect(
        lines.singleWhere((line) => line.contains('"sound"')),
        contains('"start":1.0'),
      );
      expect(
        lines.singleWhere((line) => line.contains('"door opens"')),
        contains('"at":3.0'),
      );
      expect(lines.where((line) => line.contains('"at"')), hasLength(5));
    });

    test('carries its own kind and extension', () {
      expect(slide().toJson()['kind'], 'orblit.cutscene');
      expect(cutsceneExtension, '.ocutscene');
      expect(slide().toJson().containsKey('rootMotion'), isFalse);
    });
  });

  group('reading leniently', () {
    test('drops a shot it cannot read, and says so', () {
      final load = CutsceneDocument.decode(
        file({
          'shots': [
            {'camera': 'front', 'start': 0, 'duration': 2},
            {'camera': '', 'start': 0, 'duration': 2},
            {'camera': 'side', 'start': 1, 'duration': 0},
          ],
        }),
      );
      expect(load.cutscene.shots.single.camera, 'front');
      expect(load.problems, hasLength(2));
    });

    test('drops keys on bones and root motion, and says so', () {
      final load = CutsceneDocument.decode(
        file({
          'rootMotion': {'bone': 'hips'},
          'channels': [
            {
              'target': 'hero',
              'bone': 'hips',
              'property': 'rotation',
              'kind': 'rotation',
              'keys': [
                {
                  'at': 0,
                  'value': [0, 0, 0, 1],
                },
              ],
            },
            {
              'target': 'cube',
              'property': 'mesh.visible',
              'kind': 'flag',
              'keys': [
                {'at': 0, 'value': true},
              ],
            },
          ],
        }),
      );
      final motion = load.cutscene.motion;
      expect(motion.channels.single.target, 'cube');
      expect(motion.rootMotion, isNull);
      expect(load.problems.single, contains('bones'));
    });

    test('calls a cutscene with no name a cutscene', () {
      expect(CutsceneDocument.decode(file({})).cutscene.name, 'Cutscene');
    });

    test('takes the last shot or sound as the length when none is given', () {
      final load = CutsceneDocument.decode(
        file({
          'shots': [
            {'camera': 'front', 'start': 0, 'duration': 2},
          ],
          'sounds': [
            {'sound': 'a.ogg', 'start': 1, 'duration': 4},
          ],
        }),
      );
      expect(load.cutscene.duration, 5);
    });

    test('keeps a stated length shorter than its shots', () {
      final load = CutsceneDocument.decode(
        file({
          'duration': 1,
          'shots': [
            {'camera': 'front', 'start': 0, 'duration': 2},
          ],
        }),
      );
      expect(load.cutscene.duration, 1);
    });
  });

  group('refusing', () {
    test('a file that is not a cutscene', () {
      expect(
        () => CutsceneDocument.decode(slide().motion.encode()),
        throwsA(isA<CutsceneFormatException>()),
      );
      expect(
        () => CutsceneDocument.decode('not json'),
        throwsA(isA<CutsceneFormatException>()),
      );
    });

    test('a cutscene from a newer Orblit', () {
      expect(
        () => CutsceneDocument.decode(
          jsonEncode({
            'kind': CutsceneDocument.marker,
            'formatVersion': CutsceneDocument.formatVersion + 1,
          }),
        ),
        throwsA(
          isA<CutsceneFormatException>().having(
            (error) => error.message,
            'message',
            contains('newer'),
          ),
        ),
      );
    });

    test('keys on a bone', () {
      expect(
        () => CutsceneDocument(
          motion: ClipDocument(
            name: 'Wave',
            duration: 1,
            channels: [
              ClipChannel<double>(
                target: 'hero',
                bone: 'hand',
                property: 'scale',
                kind: ChannelKind.number,
                keys: const [Key(0, 1)],
              ),
            ],
          ),
        ),
        throwsArgumentError,
      );
    });

    test('a shot with no camera, no length, or a start before the start', () {
      expect(
        () => CutsceneShot(camera: '', start: 0, duration: 1),
        throwsArgumentError,
      );
      expect(
        () => CutsceneShot(camera: 'front', start: 0, duration: 0),
        throwsArgumentError,
      );
      expect(
        () => CutsceneShot(camera: 'front', start: -1, duration: 1),
        throwsArgumentError,
      );
      expect(
        () => CutsceneSound(sound: 'a.ogg', start: 0, duration: 0),
        throwsArgumentError,
      );
    });
  });

  group('the shots', () {
    test('are kept in the order they start', () {
      expect(slide().shots.map((s) => s.start), [0, 2]);
    });

    test('look through one camera where only one runs', () {
      expect(weights(slide(), 1), {'front': 1});
      expect(weights(slide(), 3.5), {'side': 1});
    });

    test('blend over the time two overlap, adding up to one', () {
      final cutscene = slide();
      for (final at in [2.1, 2.25, 2.5, 2.75, 2.9]) {
        final both = weights(cutscene, at);
        expect(both.keys, unorderedEquals(['front', 'side']));
        expect(both.values.reduce((a, b) => a + b), closeTo(1, 1e-9));
      }
      expect(weights(cutscene, 2.25)['side'], lessThan(0.5));
      expect(weights(cutscene, 2.5)['side'], closeTo(0.5, 1e-9));
      expect(weights(cutscene, 2.75)['side'], greaterThan(0.5));
    });

    test('that touch cut from one camera to the next', () {
      final cutscene = CutsceneDocument(
        motion: ClipDocument(name: 'Cut', duration: 2),
        shots: [
          CutsceneShot(camera: 'front', start: 0, duration: 1),
          CutsceneShot(camera: 'side', start: 1, duration: 1),
        ],
      );
      expect(weights(cutscene, 0.999), {'front': 1});
      expect(weights(cutscene, 1), {'side': 1});
    });

    test('look through nothing before the first, in a gap, or at the end', () {
      final cutscene = CutsceneDocument(
        motion: ClipDocument(name: 'Gaps', duration: 4),
        shots: [
          CutsceneShot(camera: 'front', start: 1, duration: 1),
          CutsceneShot(camera: 'side', start: 3, duration: 1),
        ],
      );
      expect(cutscene.shotsAt(0.5), isEmpty);
      expect(cutscene.shotsAt(2.5), isEmpty);
      expect(cutscene.shotsAt(4), isEmpty);
    });
  });

  group('playing', () {
    test('holds on its last frame by default, firing its marks once', () {
      final director = Director(slide().sequence)..play();
      final marks = <String>[];
      for (var i = 0; i < 50; i++) {
        marks.addAll(director.advance(0.1).marks.map((m) => m.name));
      }
      expect(marks, ['door opens']);
      expect(director.finished, isTrue);
      expect(director.at, 4);
    });

    test('plays its sounds from where the cutscene has got to', () {
      final frame = slide().sequence.sampleAt(1.5);
      expect(frame.sounds.single.sound, 'sounds/whoosh.ogg');
      expect(frame.sounds.single.at, closeTo(0.5, 1e-9));
    });

    test("keeps its last keys' values at the end", () {
      final frame = slide().motion.sampleAt(4);
      expect(frame.values['cube']!['transform.position'], Vector3(4, 0, 0));
    });
  });

  group('in the scene', () {
    SceneDocument scene() => SceneDocument(
      entities: [
        for (final id in ['cube', 'street1', 'street1/lamp3'])
          SceneEntity(
            id: id,
            name: id,
            parent: id == 'street1/lamp3' ? 'street1' : null,
            components: {SceneComponents.transform: TransformComponent()},
          ),
        SceneEntity(
          id: 'street1/lamp3/bulb',
          name: 'bulb',
          parent: 'street1/lamp3',
          components: {
            SceneComponents.transform: TransformComponent(),
            SceneComponents.light: const LightComponent(),
          },
        ),
      ],
    );

    test("names things by the scene's own ids", () {
      expect(ClipScope.wholeScene.resolve('cube'), 'cube');
      expect(
        ClipScope.wholeScene.resolve('street1/lamp3/bulb'),
        'street1/lamp3/bulb',
      );
      expect(ClipScope.wholeScene.targetOf('street1/lamp3'), 'street1/lamp3');
    });

    test('moves the cube and a part of a placed prefab', () {
      final ops = sceneOpsFor(
        slide().motion.sampleAt(2),
        scene(),
        ClipScope.wholeScene,
      );
      expect(
        ops.map((op) => (op.id, op.type, op.field)),
        unorderedEquals([
          ('cube', 'transform', 'position'),
          ('street1/lamp3/bulb', 'light', 'power'),
        ]),
      );
    });
  });
}
