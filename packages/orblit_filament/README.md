# orblit_filament

Filament rendering for Orblit, drawn by Flutter's compositor. It renders into
IOSurface-backed pixel buffers that the texture registry takes without a
readback.

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

Needs Flutter. Draws on macOS, iOS, Android, Linux, Windows and the web. One
renderer serves them all through a C ABI, behind a Swift plugin on the Apple
platforms, a Kotlin/JNI one on Android, a GTK one on Linux and a Win32 one on
Windows. Each hands a frame to Flutter the way its embedder can take one,
which is without a copy everywhere but Linux and Windows. See
`linux/orblit_viewport.h` and `windows/orblit_viewport.h` for why.

## Choosing which materials get built

A surface multiplies along three axes: its blend mode, its quality tier, and
whether it is a surface or a sprite. Blending is fixed function state baked
into a compiled material, so each blend mode is its own package. The slim tier
is a different shader from the standard one, with nine samplers rather than
sixteen, for devices below Filament's third feature level. And a sprite is one
package whatever it does, because its additive mode is a uniform over
premultiplied alpha rather than a second blend.

All of it is built by default. A project that names fewer builds faster and
ships smaller. The ten lit packages are thirty-two of the thirty-eight
megabytes a generated set holds, and each one is a C array the compiler chews
through on every clean build. A project with no transparency can drop three
blend modes it never draws, which halves the set and takes 2.3 MB off the
binary.

```sh
ORBLIT_BLENDS="opaque,masked" ORBLIT_TIERS="full,slim" \
  bash packages/orblit_filament/darwin/setup.sh
```

`ORBLIT_BLENDS` takes any of `opaque transparent fade masked add`, and
`ORBLIT_TIERS` either or both of `full slim`. `opaque` and `full` are always
built.

Nothing is ever missing. A combination left out is written as a header naming
the package that stands in for it, so the renderer's surface table stays
whole. A project that drops `fade` and then draws something faded draws it
opaque: a visible result it asked for, rather than a link error.

The one exception is `slim`. Dropping it says this project will never run
below feature level 3, and a build cannot check that promise. On a device that
does run below it, Filament refuses the sixteen-sampler surface and lit
objects do not draw. The set records what it holds in `material_set.h`, and
the renderer says so in its surface notes rather than leaving you with a dark
scene.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
