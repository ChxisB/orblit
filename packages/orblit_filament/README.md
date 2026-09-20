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

## Choosing which materials get built

A surface multiplies along three axes: its blend mode, its quality tier, and
whether it is a surface or a sprite. Blending is fixed function state baked
into a compiled material, so each blend mode is its own package; the slim tier
is a different shader from the standard one, nine samplers rather than
sixteen, for the devices below Filament's third feature level; and a sprite is
one package whatever it does, because its additive mode is a uniform over
premultiplied alpha rather than a second blend.

All of it is built by default. A project that names less builds faster and
ships smaller — the ten lit packages are thirty-two of the thirty-eight
megabytes a generated set holds, and each one is a C array the compiler chews
through on every clean build. Dropping the three blend modes a project with no
transparency never draws halves the set and takes 2.3 MB off the binary.

```sh
ORBLIT_BLENDS="opaque,masked" ORBLIT_TIERS="full,slim" \
  bash packages/orblit_filament/darwin/setup.sh
```

`ORBLIT_BLENDS` takes any of `opaque transparent fade masked add`, and
`ORBLIT_TIERS` either or both of `full slim`. `opaque` and `full` are always
built.

Nothing is ever missing. A combination left out is written as a header naming
the package that stands in for it, so the renderer's surface table stays whole
and a project that drops `fade` and then draws something faded draws it
opaque — a visible result it asked for rather than a link error.

The one exception to that is `slim`. Dropping it says this project will never
run below feature level 3, and a build cannot check the promise. On a device
that does, Filament refuses the sixteen-sampler surface and lit objects do not
draw; the set records what it holds in `material_set.h` and the renderer says
so in its surface notes rather than leaving you with a dark scene.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

FSL-1.1-MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
