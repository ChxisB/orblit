# orblit_filament

Filament rendering for Orblit, composited by Flutter. Renders into IOSurface-
backed pixel buffers the texture registry adopts without a readback.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_filament:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_filament
```

Needs Flutter. Draws on macOS, iOS, Android, Linux and Windows, and on the
web. One renderer serves them all through a C ABI, behind a Swift plugin on
the Apple platforms, a Kotlin/JNI one on Android, a GTK one on Linux and a
Win32 one on Windows; each hands a frame to Flutter the way its embedder can
take one, which is without a copy everywhere but Linux and Windows (see
`linux/orblit_viewport.h` and `windows/orblit_viewport.h` for why).

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
