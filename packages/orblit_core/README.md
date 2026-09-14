# orblit_core

The Orblit engine core. An archetype entity-component store in C++, reached
over a C ABI, with component data exposed to Dart as views rather than copies.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblit-site.vercel.app](https://orblit-site.vercel.app).

## Using it

```yaml
dependencies:
  orblit_core:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_core
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
