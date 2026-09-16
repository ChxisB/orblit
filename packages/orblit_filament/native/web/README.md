# Orblit renderer core — web

Stage one of taking Orblit's real renderer to the web: the portable C++ core
(`orblit::Renderer`, the C ABI, the portable platform layer) compiled to
WebAssembly with Emscripten, linked against a Filament built for the web,
drawing a frame into a `<canvas>` through nothing but `orblit_renderer.h` —
the same ABI `packages/orblit_filament/native/headless/` drives offscreen on
macOS. No Flutter anywhere in this directory.

This is the web counterpart of `native/headless/`, and reuses its shape
deliberately: the same portable `*.cpp` glob, the same "compile the core,
link Filament's own archives, done" structure, the same proof-by-screenshot
standard. What differs is only what a browser needs that a console host does
not — a WebGL 2 context and a canvas to draw into — and that turned out to
be more than expected (see "What did not work at first" below).

## Building it

### 1. Emscripten

```sh
git clone https://github.com/emscripten-core/emsdk "$ORBLIT_CACHE/emsdk"
cd "$ORBLIT_CACHE/emsdk"
./emsdk install 5.0.4
./emsdk activate 5.0.4
source ./emsdk_env.sh   # every shell that builds this
```

**5.0.4**, not "latest": `orblit-filament`'s own `build/common/versions` pins
`GITHUB_EMSDK_VERSION=5.0.4`, which is what its CI installs and what
`build.sh -p wasm` is proven against. `build/common/get-emscripten.sh` asks
that pinned emsdk tool for whatever it calls "latest" itself, which is a
moving target across a fresh emsdk checkout; installing 5.0.4 directly names
the same version without that drift.

### 2. Filament, built for wasm

In a worktree of the fork (never the main `orblit-filament` checkout):

```sh
git -C orblit-filament worktree add -b build/web \
  "$ROOT/.worktrees/filament-web" orblit-main
cd "$ROOT/.worktrees/filament-web"
source "$ORBLIT_CACHE/emsdk/emsdk_env.sh"
export EMSDK="$ORBLIT_CACHE/emsdk"
./build.sh -p wasm release
```

Took **12 minutes 55 seconds** on this machine (a build tools pass for
desktop first — `matc` and friends, needed by the build itself, not by this
package — then the actual wasm cross-compile: 903 ninja steps, mostly
Filament's own dependencies: abseil, draco, basisu, spirv-tools, zstd).
`out/cmake-wasm-release/` came to **53 MiB**; the ~120 `.a` archives this
package's `build.sh` actually links come to **14 MiB** of that.

One target failed and does not matter here: `web/filament-js/filament.js`
(Filament's own JS-bindings sample) would not link —
`em++: error: '--extern-post-js': file not found: '/Users/.../Orblit'` — a
pre-existing bug in `web/filament-js/CMakeLists.txt`, upstream of this work:
it builds `--extern-post-js` as one space-joined CMake string rather than a
list, and this checkout's path (`.../Personal/Orblit Project/...`) has a
space in it, so the linker sees the path split in two. Everything before
that link step — every static library this package needs, `libfilament.a`
through `libbasis_transcoder.a` — had already built successfully; this
build never uses `filament.js` (it has its own JS host, `host/main.js`, over
the C ABI instead), so the failure was left as found rather than patched.
One archive, `libfilament-iblprefilter.a`, had compiled its objects but not
yet been archived when ninja stopped on that unrelated failure; built
directly afterwards with `ninja libfilament-iblprefilter.a` in
`out/cmake-wasm-release`.

### 3. The core, for the web

```sh
cd "$ROOT/.worktrees/orblit-web-core"   # made by .worktrees/new_worktree.sh web-core
export ORBLIT_FILAMENT_WASM_SRC="$ROOT/.worktrees/filament-web"
ORBLIT_GENERATED_SET=webgl2 ORBLIT_MATC_BACKENDS=opengl \
  ORBLIT_MATC="$ORBLIT_FILAMENT_WASM_SRC/out/cmake-release/tools/matc/matc" \
  bash packages/orblit_filament/darwin/setup.sh
source "$ORBLIT_CACHE/emsdk/emsdk_env.sh"
export EMSDK="$ORBLIT_CACHE/emsdk"
bash packages/orblit_filament/native/web/build.sh
```

`build.sh` does not compile the materials — it refuses to start without
`generated/webgl2` — so the `setup.sh` line above comes first, and it names
its `matc` explicitly. (An earlier note here said `build.sh` compiled them
itself; it never did, and a fresh checkout found out on 2026-09-15.) Two
things that line has to get right, and only the first was ever written down.

The written one: the renderer's compiled materials (`generated/*_material.h`)
carry shaders only for the backends `ORBLIT_MATC_BACKENDS` named, and the
default is `metal` alone. WebGL 2 is Filament's OpenGL backend, so this build
needs `opengl` compiled in.

The one that cost a fortnight of believing this build was lit: run by hand,
that line used the **release tarball's** matc, while everything below links
the **fork's** wasm archives. matc bakes a variant table into the blob and
the engine picks shaders out of it by variant key — one interface, with no
version between the two halves to catch a mismatch, since `MATERIAL_VERSION`
stayed at 76 across the change that broke it. The fork has since moved
directional lighting out of the variant key into a specialization constant
(upstream #10390): it asks for the variant with the `DIR` bit cleared, which
in a tarball-matc blob is exactly the shader compiled *without* the sun in
it. Every directional light contributed nothing, in silence, and the frame
still looked lit because the image-based half is outside that guard. Turning
the ambient off turned the scene black.

So the matc comes out of `$ORBLIT_FILAMENT_WASM_SRC` beside the archives
(`out/cmake-release/tools/matc/matc`, which `./build.sh -p wasm release`
builds on its way through), handed to `setup.sh` as `ORBLIT_MATC`.

### 4. A frame in the browser

```sh
cd packages/orblit_filament/native/web/host
python3 -m http.server 8899 --bind 127.0.0.1 &
# open http://127.0.0.1:8899/, or screenshot it headlessly:
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new \
  --use-angle=swiftshader --enable-unsafe-swiftshader \
  --user-data-dir="$(mktemp -d)" --virtual-time-budget=4000 \
  --screenshot=frame.png http://127.0.0.1:8899/
kill %1   # stop the server
```

Both `--use-angle=swiftshader` and `--enable-unsafe-swiftshader` are needed
together for WebGL 2 to exist at all in headless Chrome; a fresh
`--user-data-dir` keeps it clear of any Chrome already running; and Chrome's
`--screenshot` mode is not reliable about exiting on its own once its
virtual-time budget elapses, so an unattended script needs a watchdog around
it (`tool/capture.sh` in the merged web spike, `spike/web_canvas/`, has a
worked example — poll for the PNG, give it a couple of seconds to exit,
kill it past a hard ceiling).

## Sizes

| Artefact | Size |
|---|---|
| `host/orblit_renderer.wasm` | 14.7 MiB (unstripped; `-O2`, not `-O3`; every backend `matc` was told to compile, not opengl alone — see "What's left" below) |
| `host/orblit_renderer.js` | 117 KiB (Emscripten's glue: memory setup, `ccall`/`cwrap`, the GL emulation shim — no Embind, since this ABI is plain C) |
| `generated/webgl2/` (compiled materials, this worktree) | WebGL 2's `opengl` backend, kept separate from release and WebGPU material sets |
| Filament wasm archives actually linked | 14 MiB of `.a`, out of 53 MiB the full wasm build tree comes to |
| `native/web/build/` (this package's own `.o` files) | 13 MiB, 13 objects |
| emsdk 5.0.4 install | 1.9 GiB (toolchain + Node + Python it brings its own copies of) |

None of the above is committed: `build/`, `host/orblit_renderer.{js,wasm}`
and `captures/` are gitignored, same as `native/headless/build/`.

## What the browser drew

`captures/orblit_web_frame.png` — headless Chrome, the recipe above, a fresh
run against this build. Shows the same scene
`native/headless/orblit_headless.c` draws — a pale ground plane and five
cubes in the same five colours and relative sizes (the large green one is
the 0.9-scale block, the small purple one the 0.25-scale) — composited under
an on-page status panel built from the same ABI calls a Dart or Kotlin host
would use (`orblit_renderer_backend`, `orblit_renderer_notes`,
`orblit_renderer_note`), not a side channel into Filament:

```
backend: OpenGL (orblit_renderer_backend)
surface: ORBLIT_SURFACE_WINDOW onto canvas selector "#canvas" (OrblitSurfaceWeb.cpp)
notes: 1
  [surface] This device supports Filament feature level 1, below the
  standard lit surface's third, so the slim surface is used instead. Base
  colour, normal, metallic/roughness, occlusion and emissive maps, ground
  blending and decals all draw as usual; rectangular area lights are not
  shadowed and the irradiance field does not light this scene.
```

The console (`captures/orblit_web_frame.log`) has Filament's and the
engine's own report of what it is running on, matching PORTING.md's
prediction exactly — this was proven, not assumed:

```
[WebKit], [WebKit WebGL], [OpenGL ES 3.0 (WebGL 2.0 (OpenGL ES 3.0 Chromium))], ...
Feature level: 1
Backend feature level: 1
FEngine feature level: 1
[orblit] engine ready in 0 ms, slim surface (feature level 1)
```

**Feature level 1, the slim surface, confirmed three ways that all agree**:
Filament's own two log lines, `orblit::Renderer`'s own startup log, and the
ABI's own `notes()` mechanism a host is meant to read this through — the
same "surface"/"areaShadows"/"field" note key `PORTING.md` documents for
Metal at feature level 2 on the iOS simulator, now also seen for real on
WebGL 2. Nothing this scene asked for was refused outright: no missing
texture, no unplayable video, no note beyond the expected surface one.

A bonus check, not load-bearing for the screenshot above but worth
recording: `orblit_renderer_request_capture` / `orblit_renderer_read_capture`
— the PNG-writing path `orblit_headless.c` uses — **also works** on this
`ORBLIT_SURFACE_WINDOW` canvas chain (`read_capture: 2073600 bytes came back`
— exactly 960×540 RGBA8). `ORBLIT_SURFACE_HEADLESS` cannot: see
`OrblitSurfaceWeb.cpp`, which documents why (Filament's `PlatformWebGL` never
implemented a windowless swap chain — "TODO: implement headless SwapChain"
in the fork's own source) and lets that surface allocate cleanly and then
fail, the ABI's ordinary "would not start" path, rather than crashing.

## What did not work at first

Two things `native/headless/build.sh`'s recipe did not need, both found by
testing rather than by reading documentation:

**No WebGL context existed until one was created explicitly.**
`Engine::create`'s first real GL call — `glGetString`, querying the version
— crashed in Emscripten's own `library_webgl.js` with `Cannot read
properties of undefined (reading 'getParameter')`. Confirmed directly
against the live page with `evaluate_script` (Chrome DevTools MCP) before
guessing at a fix: `Module.GLctx` was `undefined` at that point. Neither
Filament's `PlatformWebGL.cpp` (its `createSwapChain` just casts whatever
pointer it is given straight to `SwapChain*`, never touching WebGL) nor
Emscripten itself creates a context merely because `-sUSE_WEBGL2=1` was
linked and `Module.canvas` was set — Filament's own `filament.js` gets away
with this because its generated Embind glue creates the context through the
`html5.h` API before `Engine::create` ever runs, and this build has no
equivalent JS glue. Fixed in `orblit_web_host.cpp`:
`emscripten_webgl_create_context` + `emscripten_webgl_make_context_current`
on the given canvas selector, before the call through to the unchanged
`orblit_renderer_create`. Web-only bootstrapping, so it lives in this
web-only wrapper, not in the shared core.

**A thrown `utils::Panic` needs `-fwasm-exceptions` on the whole link, even
though Filament's own wasm archives are compiled with no exception flag at
all.** `Renderer::initWithWidth`'s `try`/`catch` around `startWithWidth()`
depends on catching exactly that. Proved with a two-file repro before
trusting it with the real renderer: a `throw` in an object file built with
zero exception flags, linked against a `catch` site and a final link both
built with `-fwasm-exceptions` — caught correctly. Without the flag anywhere
in the build, the same throw is an unhandled promise rejection and the
process is gone. `build.sh` passes `-fwasm-exceptions` to every translation
unit it compiles and to the final link.

A smaller, one-off gotcha while wiring up the build itself: a from-source
Filament build has no single merged `include/` tree the way the packaged
macOS/iOS SDK release does, so two headers needed their own `-I` beyond what
`darwin/setup.sh`'s SDK-based convention expects —
`out/cmake-wasm-release/filament` (generated headers alongside
`filament/include`) and `out/cmake-wasm-release/libs`
(`gltfio/materials/uberarchive.h`, generated at build time under
`libs/gltfio/materials/`, not nested inside an `include/` directory).
`build.sh`'s `INCLUDES` array has both, with a comment at each explaining
why it is not where the plain source headers are.

## What compiled unchanged, and what did not

Every plain C++ file `native/headless/build.sh` compiles — the core
(`OrblitRendererCore.cpp`, 258 KB, ~5,800 lines), the C ABI
(`OrblitRendererC.cpp`), `OrblitPlatform.cpp`, `OrblitBackend.cpp`,
`OrblitDecals.cpp`, `OrblitMotionBlur.cpp`, `OrblitOutline.cpp`,
`OrblitShadows.cpp`, `OrblitSplatSet.cpp`, `OrblitSplats.cpp`,
`ScreenEffects.cpp` — compiled for `emcc` **with no source change**, same as
`PORTING.md` found for the portable build generally. `OrblitBackend.cpp`
already had an `__EMSCRIPTEN__` branch choosing `ORBLIT_BACKEND_OPENGL` (it
was written for this before this package existed), so backend selection
needed nothing new either. `OrblitPlatform.cpp`'s `parallelFor` uses
`std::thread`, untested for Emscripten until now: confirmed by testing that
it compiles and links with no `-pthread` at all, and that
`hardware_concurrency()` reports 1 under Emscripten without it — so the
function's own `workers <= 1` fallback takes the plain sequential loop, and
no `std::thread` is ever actually constructed at runtime.

**Not compiled**: `OrblitSurfaceHeadless.cpp`. Its `OrblitCreateHeadlessSurface`
and `OrblitCreateWindowSurface` are exactly the two functions
`OrblitSurfaceWeb.cpp` (this directory) provides instead — both defining
the same two names would not link. Everything Apple-specific
(`OrblitPlatformApple.mm`, `OrblitSurfaceApple.mm`, the Objective-C wrapper)
was already excluded by the `*.cpp` glob, as on every portable build.

**No hunk was needed in any shared source file.** The two new files this
work added — `OrblitSurfaceWeb.cpp`, `orblit_web_host.cpp` — live in this
directory, not beside the renderer, and nothing under
`Sources/orblit_filament_native/` was edited.

## The web surface

`OrblitSurfaceWeb.cpp` implements `OrblitCreateHeadlessSurface` and
`OrblitCreateWindowSurface`, worked out from Filament's own web pieces rather
than guessed:

- `filament/backend/src/opengl/platforms/PlatformWebGL.cpp` (the fork):
  `createSwapChain(void *nativeWindow, uint64_t)` is
  `static_cast<SwapChain*>(nativeWindow)` and nothing else — the pointer is
  never dereferenced, only carried as an opaque identity. Its sized overload
  — the one a headless surface needs — is unimplemented and always returns
  null.
- `web/filament-js/jsbindings.cpp`'s `_createSwapChainForCanvas`:
  `engine->createSwapChain((void*)persistentCanvasId->c_str())` — the
  canvas's CSS selector, handed across as that pointer, kept alive for the
  swap chain's life (`persistentCanvasId` is deliberately leaked there for
  exactly that reason).

So `OrblitSurfaceWeb.cpp`'s `WebCanvasSurface` does the same: `"window"` in
`orblit_surface_desc` is a canvas selector string (`orblit_web_host.cpp`'s
`orblit_web_create_on_canvas` builds one from a plain `const char *`, so a
JavaScript host never has to). `ORBLIT_SURFACE_HEADLESS` allocates the
surface object cleanly and then fails at `allocate()` with a null chain —
there being nowhere else for it to go on this backend — which is the ABI's
ordinary "the renderer would not start" path, not a crash.

## What remains for stage two

A Flutter web implementation of the `orblit_filament` plugin:

1. **`dart:ffi` is unconditional today.** `packages/orblit_core/lib/src/
   world.dart`, `bindings.dart`, `packages/orblit_native/lib/src/host.dart`
   and `script.dart` all `import 'dart:ffi'` with no conditional import —
   `dart:ffi` does not exist for the web compiler. Each needs its FFI-backed
   half split out behind `dart.library.ffi` vs. `dart.library.js_interop`
   (or a `dart.library.io` default with a web override). `bindings.dart`'s
   `OrblitTransformsStruct` and `host.dart`'s `OrblitScriptHost` `extends
   Struct`: there is no memory to lay a `Struct` over on the web, so the web
   side re-expresses the same rows as encoded bytes, the way
   `host/main.js`'s `allocF32`/`allocI32`/`allocI64` do here in JavaScript
   — a Dart equivalent is `dart:js_interop`'s typed-array views over the
   module's `HEAPU8`.
2. **An `HtmlElementView` over a real `<canvas>`**, registered with
   `ui_web.platformViewRegistry.registerViewFactory` — the merged web spike
   (`spike/web_canvas/lib/filament_canvas.dart`) already proved this shape
   and its two gotchas: a platform view's element is detached when its
   factory callback runs (poll with `requestAnimationFrame` until it is
   actually laid out before creating anything on it), and Flutter resizes
   the view's CSS box but never the canvas element's backing-store
   `width`/`height` — something has to match those to
   `canvas.clientWidth/Height * devicePixelRatio` every frame.
3. **The scene crossing by `dart:js_interop`, not `dart:ffi`**, into this
   module's exported `orblit_renderer_*` functions via `ccall`/`cwrap` —
   `host/main.js`'s `publishScene` is that shape already, minus the Dart
   side. `orblit_web_create_on_canvas` (`orblit_web_host.cpp`) is there
   specifically so the Dart side never has to build an `orblit_surface_desc`
   by hand either.
4. **Loading `orblit_renderer.js`/`.wasm` from Flutter web's build output.**
   Untested here: whether they belong under `web/` (copied verbatim into
   `build/web/` the way the spike copies `filament.js`/`.wasm`) or need
   something more — bundler interaction, CORS/MIME on whatever serves
   `build/web` in production, and multi-instance behaviour if a page ever
   wants more than one renderer (this build assumes one `Module.canvas` per
   loaded module instance; untested whether loading the module twice for
   two canvases works or needs a second `<script>` load).
5. **Trimming `orblit_renderer.wasm`.** 14.7 MiB unstripped, materials
   compiled for both `metal` and `opengl` in this worktree's local cache (a
   real web build only needs `opengl`, which alone should noticeably
   shrink it — `PORTING.md`: 3.32 MiB embedded for OpenGL alone against
   4.70 for Metal, for comparison, though that is `-a all -p all` across
   every material this package has, not this specific link's subset).
   `-O3`, `wasm-opt`, and Closure Compiler on `orblit_renderer.js` (currently
   plain `-O2`, no `--closure`) are all unexplored.

## Stage two, as built

The five items above, answered. The plugin is `lib/src/web/` in this
package; the gallery runs it at `examples/gallery` with
`tool/capture_web.sh` for a screenshot.

1. **`dart:ffi` splits — done where they actually blocked, and narrower
   than expected.** `orblit_filament/lib` turned out to import neither
   `dart:ffi` nor `dart:io` and to depend on no other Orblit package, and
   `orblit_core`/`orblit_native` are not reachable from the gallery at all
   (`orblit_examples` depends on `orblit_filament`, `orblit_camera`,
   `orblit_weather`, `orblit_noise`; the gallery's `pubspec.lock` has no
   `orblit_core` entry). So those two packages were left alone: splitting
   them is still worth doing for their own sake, but nothing on the way to
   a drawing web app needs it. What did block, and is done: `Int64List`
   (`lib/src/key_list.dart`), the seven examples that read a file
   (`orblit_examples/lib/src/platform/io.dart`) and the gallery's
   environment reading (`examples/gallery/lib/orblit_env.dart`).
2. **`HtmlElementView` over a real `<canvas>` — done**, with both gotchas
   the spike predicted: `OrblitWebViewport._whenLaidOut` polls with
   `requestAnimationFrame` until the element is attached and measured
   before creating anything, and `_fitBackingStore` matches the canvas's
   `width`/`height` to `clientWidth/Height * devicePixelRatio` every frame.
   The viewport is found through the view's creation params, because a
   platform view's own id is minted by Flutter and never reaches a plugin.
3. **The scene crossing by `dart:js_interop` — done.**
   `orblit_scene_web.dart` makes all twenty-two scene calls in the order
   `Viewport.write(scene:)` and `OrblitScene.applyTo` use; `OrblitHeap`
   copies each array into the module's heap, int64 keys as little-endian
   low/high pairs. No web-specific entry point was added to the core: the
   only non-`orblit_renderer.h` call is stage one's own
   `orblit_web_create_on_canvas`.
4. **Loading from Flutter web's build output — done, and it is just
   `web/`.** `orblit_renderer.js` and `.wasm` copied beside `index.html` are
   copied verbatim into `build/web`, with a plain synchronous `<script>`
   tag so the global exists before any Dart runs. Both are gitignored,
   like every other build artefact here. No bundler interaction, no MIME
   or CORS trouble from `python3 -m http.server`. **Untested:** more than
   one renderer on a page. The plugin loads one module instance per
   canvas, which is what `Module.canvas` requires, but two at once has
   never been run.
5. **Trimming — partly, and not by tuning.** 7.79 MiB (8,164,872 bytes)
   against stage one's 14.7, purely because these materials carry only the
   `opengl` backend rather than `metal` and `opengl` both. `-O3`,
   `wasm-opt` and Closure are still unexplored.

### The one thing that does not look right

Lit surfaces draw black. Geometry, camera, sky, and the whole message
arrive correctly — every one of the twenty-two calls returns `ORBLIT_OK`,
and the values were read back at the boundary and checked against the
scene that produced them (the floor's colour arrives as its exact
linearised `0xFF3B424C`, its flags as receive|visible, the sun as 82000 lux
in the right direction, exposure as 16 / 1/125 / 100). Skipping the render
graph, pipeline, post, environment, field or sky changes nothing about it,
and raising a light to 200000 lumens changes nothing either.

So this is not the Dart side mis-marshalling anything, and it is not this
plugin at all: it reproduces in stage one's own JavaScript host, which has
no Flutter and no Dart anywhere in it.

**Direct lights contribute nothing on this build. Only ambient does.**
Measured by serving `host/main.js` unchanged but for its ambient, against
this same `orblit_renderer.wasm`:

| `host/main.js` ambient | centre pixel |
|---|---|
| 24000, as committed | (210, 213, 171) |
| 0 | (0, 0, 0) |

That scene's sun is 100000 lux and did not move between the two runs, so if
analytic lighting reached a surface at all the second row could not be
black. The frame this directory's README calls proof of stage one is lit
entirely by its ambient — which is why it looked right and the gallery does
not: 24000 against bright albedos, where the Surface example has 9000
against a 0.05 floor, about a thirtieth of correct exposure.

The gap is therefore in the renderer or in the slim surface at feature
level 1, upstream of everything here; nothing in this directory or in
`lib/src/web/` can close it. What it wants next is the same scene on
Android's OpenGL ES — the other feature-level-1 host, and the one place the
same question can be asked without a browser in the way.

## Gaussian splats, and the sort that has no thread

A cloud is sorted back to front every time the camera moves, and this build
has no threads: `-pthread` is not on the link line, and `std::thread`'s
constructor throws "Not supported" the moment one is constructed. The sorter
built one per cloud, so the first scene carrying splats did not merely fail to
draw them — the C ABI caught the exception, marked the renderer failed, and
every later call was refused. A browser drew a black canvas.

The sort now goes to a **Web Worker**:

- `OrblitSplats.cpp`'s `makeSplatSorter` chooses where a sort runs: a thread
  natively, the asking thread for a cloud small enough that handing the work
  over costs more than doing it, and here `makeWorkerSplatSorter`.
- `OrblitSplatSorterWeb.cpp` (this directory) is that sorter. Its `EM_JS`
  calls reach `orblit_splat_worker.js`, which `build.sh` passes to `emcc` as
  `--pre-js` so that it lands inside the module's factory as
  `Module.orblitSplatWorkers`.
- The worker's own program is a function in that file turned back into source
  text and started from a `Blob`, so a page still serves
  `orblit_renderer.js` and `.wasm` and nothing else. The positions cross once,
  when the cloud is made; each sort sends 35 floats of camera across and the
  finished order comes back as a transferred buffer.
- A page that will not start a worker at all — no `Worker`, or a content
  security policy that refuses a `blob:` script — sorts on the page's own
  thread and says so in the console. It draws; it stutters.

**Not pthreads**, deliberately: threads in a browser need the page served
cross-origin isolated *and* every object in the link — Filament's own archives
included — rebuilt with `-pthread`, which is a second Filament build and a
second renderer to ship beside this one. A sort shares nothing but the
positions, so a worker gets the whole benefit at none of that cost, on every
page, isolated or not.

Checked in headless Chrome, from `examples/gallery`:

```sh
tool/capture_web.sh 'Gaussian%20splats'
```

The ring draws, its near side blended over its far side, with the pillar in
front of what it stands in.

## Textures and environment pictures, decoded on workers

Natively the texture queue decodes on threads of its own and an environment
picture is prepared on one. Here all of it ran on the page. Measured in a
real-time Chrome on an M4 Pro, from the gallery's Textures example:

| Load | Page thread before | Page thread after |
|---|---|---|
| twelve 2048² PNGs and JPEGs | 841–854 ms of decoding; tasks up to 130–135 ms | 8–12 ms of handing over, at most 3 ms a frame; no task over 50 ms |
| twelve 2048² Basis UASTC files | 641–652 ms, and 7 ms of pushing; tasks up to 120–124 ms | 8–12 ms, at most 3 ms a frame; no task over 50 ms |
| twelve cooked BC7 sets | 30 ms, 3 ms at most | 7–11 ms, at most 4 ms a frame |
| one 2K `.hdr`, prepared for lighting | one task of 200–201 ms | 5–6 ms; no task over 50 ms |

So the decoding goes to **Web Workers**, each running the *decoder module*:

- `orblit_decoder_module.cpp` is a second WebAssembly module holding the pure
  readers and nothing of Filament — `OrblitKtx2`, `OrblitHdrImage`,
  `OrblitTinyExr`, `OrblitEnvironmentBake` and `OrblitDecode.h`, with the zstd,
  stb and Basis Universal archives the renderer itself links. `build.sh`
  links it and embeds it in `orblit_renderer.js` as its JavaScript and its
  `.wasm` gzipped in base64, so a page still serves two files. A worker
  gunzips it with the browser's own `DecompressionStream`.
- `OrblitDecodeJobs.cpp` is the one function a worker runs: a job kind, bytes
  and a few numbers in; parts and numbers out. It is compiled into the
  renderer too, and the page runs the same job on the same bytes when no
  worker will, so what the page draws then is what a worker would have drawn.
- `orblit_decoder_workers.js`, passed to `emcc` as `--pre-js`, is the pool —
  half the machine's threads, at most four — as `Module.orblitDecoders`;
  `OrblitDecodersWeb.cpp` reaches it through `EM_JS`. The texture queue and
  the environment hand a job over only when a worker is idle, so a job not
  picked up within half a second is a worker that is not running. The page
  decodes that job, and every job after it until some worker answers. A job
  picked up and not finished in ten seconds — forty times the slowest job
  measured, 250 ms — has its worker stopped and is decoded on the page the same way.
  No `Worker`, no `DecompressionStream`, a refused `blob:` script or a module
  that will not start: the page decodes everything, and says so once.
- Basis is chosen and transcoded exactly as Filament's `Ktx2Reader` does it,
  from the header and with the same arguments, but without `asyncCreate`,
  which copies the whole file and starts the transcoder on the page first.

Checked, with `ORBLIT_POST=0 ORBLIT_CIRCLING=0 ORBLIT_SECONDS=1.5` so frames
are exact: each load decoded on workers, on the page with no `Worker`, and by
the build before this, is the same frame to the pixel — pictures, Basis,
cooked BC7, and a picture lighting the wall on the GPU route and the CPU one
(float render targets hidden from WebGL). The page draws the same frame again
with a worker that never starts, one that never finishes, and a decoder module
that fails to load.

The decoder module is 988 KB of `.wasm` (392 KB gzipped) and 15 KB of
JavaScript. Embedded, `orblit_renderer.js` grows from 126 KB to 679 KB — 31 KB
to 440 KB gzipped — and `orblit_renderer.wasm` by 15 KB. Served beside the
renderer instead it would cost about the same gzipped, 306 KB with brotli
against the embedded 427 KB, and would only be fetched once something is
decoded.

Everything above is from a real-time Chrome driven over the DevTools
protocol. Under `tool/capture_web.sh`'s virtual clock the Textures example
does not finish arriving before the shot — before this change or after it,
at a 12 or a 30 second budget, and about one shot in three is blank either
way — so that script is no check of this.

## What this proves, in one line

The same `OrblitRendererCore.cpp` that draws on macOS and the iOS simulator
drew a frame in a browser tab through its own C ABI, unmodified, at the
feature level `PORTING.md` predicted, reporting that through the same
mechanism every other host reads it through — with the whole gap between
"the core is portable" and "a browser draws it" turning out to be two small,
web-specific, testable fixes (a WebGL context, an exception-handling flag),
not a rewrite.
