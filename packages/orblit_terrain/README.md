# orblit_terrain

Ground as data. Heights, cover and colour kept in square regions that exist
only where there is ground; the `.oterrain` and `.oregion` files they are
saved in; and the height and slope of the ground at any point, read the way
the renderer draws it, so a character stands on what is seen without asking
physics.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_terrain:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_terrain
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
