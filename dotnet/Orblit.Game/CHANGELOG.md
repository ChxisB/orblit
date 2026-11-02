# Changelog

## 0.1.0

- `IGame` is a game: a start, an update and a stop, with a `GameContext`.
- `GameScene` holds the scene the game plays in. The game changes it with
  `Apply(SceneDiff)` and reads it from `Document`.
- `GameInput` holds the frame's `FrameControls` and a `GamePointer`. The
  pointer is a ray into the world, whether the primary button went down, a
  secondary-button drag and the wheel.
- `GameHost` runs one game once. It catches a throw from `Start` or `Update`,
  logs it, stops the game and keeps the exception in `Fault`. A throw from
  `Stop` is logged. A diff that arrives after the stop is dropped.
- `GameHost.Guard` runs other code of the game's, such as asking for its
  interface, with the same rule: a throw ends the game.
- `GameHost.ApplyEdit` puts a change made outside the game into its scene,
  such as an edit in the editor while the game plays. The game reads it on
  its next call. It is not raised as `SceneChanged`.
