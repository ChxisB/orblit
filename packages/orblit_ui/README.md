# orblit_ui

The interface a game draws: a tree of elements, each with a utility class list
or CSS on it, built into real Flutter widgets. One document, whether it was
laid out by hand in the editor or described by a script.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_ui:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_ui
```

Needs Flutter. Runs anywhere Flutter runs.

## Touch

Three elements for a screen with fingers. `stick` reports `[x, y]` through
the handler named in `onMove`. `touch-button` reports `true` and `false`
through `onHold`. `swipe-pad` reports `[dx, dy]` through `onSwipe`. Up is
positive, and each element follows the finger that landed on it first, so
several can be used at once.

```dart
UiNode(
  type: 'stick',
  css: 'width: 160px; height: 160px; background: #33333388; '
      'border-radius: 80px',
  props: {'onMove': 'move'},
)
```

`orblit_input` has the other half: `TouchControls` turns these events into
controls an action can be bound to.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
