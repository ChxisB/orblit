import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/src/examples/day_and_night.dart';
import 'package:orblit_examples/src/examples/runner.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_input/orblit_input.dart';
import 'package:orblit_test/orblit_test.dart';
import 'package:vector_math/vector_math_64.dart';

// Examples played through orblit_test: a stage that steps whole frames and
// a scripted keyboard. No window, no wall clock.

void main() {
  const escape = KeyControl('Escape');

  ActionMap pauseKey() => ActionMap(
    actions: const [InputAction.button('pause')],
    contexts: [
      InputContext(
        'play',
        bindings: const {
          'pause': [Binding(escape)],
        },
      ),
    ],
    active: const ['play'],
  );

  test('the runner waits at the line, then the autopilot sets off', () {
    final game = RunnerExample()..autopilot = true;
    final stage = TestStage((frame) => game.update(frame.dt));

    stage.step(const Duration(milliseconds: 500));
    expect(game.phase, RunnerPhase.ready);

    stage.stepUntil(
      () => game.phase == RunnerPhase.running,
      within: const Duration(seconds: 2),
    );
    expect(game.travelled, 0);

    stage.step(const Duration(seconds: 5));
    expect(game.travelled, greaterThan(20));
  });

  test('a key pauses the runner and the same key lets it go again', () {
    final map = pauseKey();
    final game = RunnerExample()
      ..autopilot = true
      ..restart();
    final stage = TestStage((frame) {
      if (map.wasPressed('pause')) {
        game.phase == RunnerPhase.paused ? game.resume() : game.pause();
      }
      game.update(frame.dt);
    }, actions: map);

    stage.step(const Duration(seconds: 2));
    stage.controls.press(escape);
    stage.step(const Duration(milliseconds: 50));
    stage.controls.release(escape);
    final frozen = game.travelled;
    expect(game.phase, RunnerPhase.paused);
    expect(frozen, greaterThan(0));

    stage.step(const Duration(seconds: 2));
    expect(game.travelled, frozen);

    stage.controls.press(escape);
    stage.step(const Duration(milliseconds: 50));
    stage.controls.release(escape);
    stage.step(const Duration(seconds: 1));
    expect(game.phase, RunnerPhase.running);
    expect(game.travelled, greaterThan(frozen));
  });

  test('the sun peaks at noon and the lux falls to the moon after dark', () {
    final example = DayAndNightExample();
    final camera = OrblitCamera(
      position: Vector3(0, 2, 10),
      target: Vector3.zero(),
    );
    var lux = 0.0;
    final stage = TestStage((frame) {
      final seconds = frame.now.inMicroseconds / 1000000;
      lux = example.scene(camera, seconds).lights.single.intensity;
    });

    // Ten in the morning, at 0.6 hours a second: noon is a little over
    // three seconds in.
    stage.step(const Duration(milliseconds: 3340));
    expect(lux, greaterThan(70000));

    stage.stepUntil(() => lux <= 1, within: const Duration(seconds: 20));
    expect(stage.now, greaterThan(const Duration(seconds: 6)));
  });
}
