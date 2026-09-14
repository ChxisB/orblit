# orblit_codegen

Turns annotated Dart classes into component registration and a manifest other
front ends can read without compiling the package that declared them.

Part of [Orblit](https://github.com/Orblit-Engine/orblit), a Dart-first 3D game
engine. The documentation is at [orblit-site.vercel.app](https://orblit-site.vercel.app).

## Using it

```yaml
dependencies:
  orblit_codegen:
    git:
      url: https://github.com/Orblit-Engine/orblit.git
      path: packages/orblit_codegen
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
