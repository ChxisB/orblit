# Changelog

## 0.1.0

- `GameRunner.Run` plays an `IGame` in a window of its own. It draws the
  scene, feeds the game the keys and the pointer, and shows a throw from the
  game on the window.
- `IInterfaceGame` is a game with a control of its own. The runner puts the
  control over the view.
- `GameInputSource` reads one control as a `GameInput`. Keys are named by
  their place on the keyboard, such as `KeyW`. A click, the wheel and a right
  drag are sums that `EndFrame` clears. The editor uses it too.
