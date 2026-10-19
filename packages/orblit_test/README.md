# orblit_test

Tests for a game that run with no window.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dev_dependencies:
  orblit_test:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_test
```

Needs Dart alone. A test steps time, holds a control and asserts on what the
game did.

```dart
test('jumping puts the player in the air', () {
  final game = Game();
  final stage = TestStage(
    (frame) => game.update(frame.dt),
    actions: game.input,
  );

  stage.controls.press(const KeyControl('Space'));
  stage.step(const Duration(milliseconds: 300));

  expect(game.player, isPositionedNear(Vector3(0, 1, 0), within: 0.5));
  expect(stage.recording, hasPlayed('jump'));
});
```

- `TestStage` is the frame loop. Time moves only when `step` is called.
- `ScriptedControls` is the keyboard, pad and touch screen of the test.
- `SceneFinders` and the matchers read a scene document and recorded sound.
- `describeChange` and `matchesTextFile` compare what a frame changed with a
  file, with no pixels.

## What it does not do

It does not draw, so a picture of a game is not here. It does not step
physics. The solver is in `orblit_physics`, which a test calls itself.
