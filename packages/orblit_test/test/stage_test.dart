import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_input/orblit_input.dart';
import 'package:orblit_test/orblit_test.dart';
import 'package:test/test.dart';

void main() {
  group('time', () {
    test('a second is sixty frames, and now says so exactly', () {
      final seen = <Frame>[];
      final stage = TestStage(seen.add);

      stage.step(const Duration(seconds: 1));

      expect(stage.frames, 60);
      expect(stage.now, const Duration(seconds: 1));
      expect(seen, hasLength(60));
      expect(seen.every((frame) => frame.dt == 1 / 60), isTrue);
      expect(seen.last.now, const Duration(seconds: 1));
    });

    test('a different rate changes the frame', () {
      final stage = TestStage((_) {}, fps: 30)
        ..step(const Duration(seconds: 1));

      expect(stage.frames, 30);
      expect(stage.now, const Duration(seconds: 1));
    });

    test('a span between frames waits for the next step', () {
      final stage = TestStage((_) {})..step(const Duration(milliseconds: 20));

      expect(stage.frames, 1);
    });

    test('small steps add up to the time they say', () {
      final stage = TestStage((_) {});

      for (var i = 0; i < 100; i++) {
        stage.step(const Duration(milliseconds: 8));
      }

      expect(stage.frames, 48);
    });

    test('a step back in time is refused', () {
      final stage = TestStage((_) {});

      expect(
        () => stage.step(const Duration(milliseconds: -16)),
        throwsArgumentError,
      );
    });

    test('a rate of nothing is refused', () {
      expect(() => TestStage((_) {}, fps: 0), throwsArgumentError);
    });
  });

  group('stepUntil', () {
    test('stops on the frame the game gets there', () {
      var count = 0;
      final stage = TestStage((_) => count++);

      stage.stepUntil(() => count == 7, within: const Duration(seconds: 1));

      expect(stage.frames, 7);
    });

    test('runs nothing when it is already true', () {
      final stage = TestStage((_) {})
        ..stepUntil(() => true, within: const Duration(seconds: 1));

      expect(stage.frames, 0);
    });

    test('fails rather than hang when the game never gets there', () {
      final stage = TestStage((_) {});

      expect(
        () => stage.stepUntil(
          () => false,
          within: const Duration(milliseconds: 500),
        ),
        throwsStateError,
      );
      expect(stage.frames, 30);
    });
  });

  group('input', () {
    final actions = [const InputAction.button('jump')];
    final contexts = [
      InputContext(
        'play',
        bindings: const {
          'jump': [Binding(KeyControl('Space'))],
        },
      ),
    ];

    test('is read into the actions before the game takes its frame', () {
      final map = ActionMap(
        actions: actions,
        contexts: contexts,
        active: const ['play'],
      );
      final pressed = <bool>[];
      final stage = TestStage(
        (_) => pressed.add(map.wasPressed('jump')),
        actions: map,
      );

      stage.step(const Duration(milliseconds: 50));
      stage.controls.press(const KeyControl('Space'));
      stage.step(const Duration(milliseconds: 50));
      stage.controls.release(const KeyControl('Space'));
      stage.step(const Duration(milliseconds: 17));

      expect(pressed, [
        false, false, false, // Before the key.
        true, false, false, // Pressed on the first frame only.
        false,
      ]);
    });
  });

  group('sound', () {
    test('moves on with the frames, so a fade ends', () {
      final stage = TestStage((_) {});
      stage.audio.play('door').stop(fadeSeconds: 0.5);

      stage.step(const Duration(milliseconds: 250));
      expect(stage.recording.calls.whereType<AudioStopped>(), isEmpty);

      stage.step(const Duration(milliseconds: 500));
      expect(stage.recording.calls.whereType<AudioStopped>(), hasLength(1));
    });
  });
}
