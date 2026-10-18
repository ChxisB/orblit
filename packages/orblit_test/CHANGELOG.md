# Changelog

## 0.1.0

- First cut of `orblit_test`. Pre-alpha: everything is subject to change.
- `TestStage` steps a game's frame at a fixed rate with no window and no wall
  clock. `step` runs the frames in a span. `stepUntil` runs until a test says
  stop and fails if that takes too long. Each frame updates an `ActionMap`
  from `ScriptedControls`, runs the game, then moves recorded sound on.
- `ScriptedControls` holds keys, buttons, axes and sticks until released.
- `SceneFinders` finds an entity by name or by path and says what is there
  when it cannot.
- Matchers: `hasComponent`, `isPositionedNear`, `hasPlayed` and `isSounding`.
- `describeChange` writes what a frame changed as text. `matchesTextFile`
  compares text with a file and rewrites it with `ORBLIT_UPDATE_GOLDENS=1`.
