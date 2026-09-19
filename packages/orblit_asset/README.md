# orblit_asset

What an asset is called and where its bytes are: project-relative asset ids,
content hashes, the sources assets are read from and a store that files bytes
by their hash.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_asset:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_asset
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner. The
two directory-backed classes need `dart:io`: the package still compiles for the
web, and constructing one of them there throws.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

FSL-1.1-MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
