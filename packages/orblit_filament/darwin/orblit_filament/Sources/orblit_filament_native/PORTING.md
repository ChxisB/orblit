# Porting the renderer off Apple platforms

The renderer is plain C++ now. `orblit::Renderer` is declared in
`OrblitRendererCore.h` and defined across the `OrblitRenderer*.cpp` files, one
a topic, with no Objective-C and no Apple header. The Objective-C class in `OrblitRenderer.mm` is a thin wrapper:
the Swift plugin calls it through `include/OrblitRenderer.h`, which has not
changed, and it forwards every call. Any other host calls the C ABI in
`include/orblit_renderer.h`.

## Where everything is

| Was | Is |
|---|---|
| The structs and constants at the top of `OrblitRenderer.mm` | `OrblitRendererTypes.h`, in the same order, in `namespace orblit`; every file gets them through `OrblitRendererCore.h` |
| The ivar block of `@implementation OrblitRenderer` | The private members at the end of `class Renderer`, same names, same comments |
| Each `- (T)foo:(A)a bar:(B)b` method | `T Renderer::foo(A a, B b)` in the file for its topic, in the same order within it, comments kept |
| `[self foo:x bar:y]` | `foo(x, y)` |
| `NSLog(@"…%@…", s)` | `orblit::log("…%s…", s.c_str())` (`OrblitPlatform.h`) |
| `NSString *`, `NSArray<NSString *> *` | `std::string`, `std::vector<std::string>` |
| The notes dictionaries | `orblit::Notes`, a `std::map` |
| `NSData dataWithContentsOfFile:` | `orblit::readFile` |
| `CFAbsoluteTimeGetCurrent()` | `orblit::now()` (still CoreFoundation's clock on Apple) |
| `dispatch_apply` | `orblit::parallelFor` (still dispatch on Apple) |
| `NSLock` | `std::mutex` |
| `OrblitReadDecalPicture` (ImageIO) | `orblit::readPicture`: ImageIO in `OrblitPlatformApple.mm`, stb_image and Filament's resampler in `OrblitPlatform.cpp` |
| `AVPlayer` and friends in `Movie`, `open:`, `close:`, `pumpVideos` | `orblit::VideoDecoder`: AVFoundation in `OrblitPlatformApple.mm`; none elsewhere yet, which the notes say |
| `builder.backend(Engine::Backend::METAL)` | `orblit::backendCandidates` (`OrblitBackend.cpp`) |
| `initWithWidth:` / `copyPresentedBuffer` / `notes` / `passTimings` | `Renderer::initWithWidth` / `copyPresentedBuffer` / `notes` / `passTimings`, which the wrapper turns back into Foundation types |

## The renderer's own files

One class, fourteen files. Each opens with a comment naming what it holds, so
`head -3 OrblitRenderer*.cpp` is the index. Two names do not say it:
`OrblitRendererCore.cpp` is the renderer's own lifecycle, and
`OrblitRendererGraph.cpp` is the render targets. All of them include
`OrblitRendererInternal.h` — the headers they share, and the two helpers more
than one of them needs.

The compiled materials are the exception to that. They live in the anonymous
namespace of `OrblitMaterialPackages.cpp` and reach the rest through the
lookups in `OrblitMaterialPackages.h`, so the multi-megabyte arrays are in one
translation unit and out of the library's symbol table.

## Porting a change made to the old `OrblitRenderer.mm`

By hand, a hunk goes into the function of the same name, at the same place —
the comments around it are the same, so `grep -rn` for them. `[self …]`
becomes a call and Foundation types become the ones in the table above. A new
ivar becomes a member in `OrblitRendererCore.h` and a new struct goes in
`OrblitRendererTypes.h`; a new `#include` goes at the top of
`OrblitRendererInternal.h` if more than one file needs it, otherwise at the
top of the one that does, except a compiled material's `…_material.h`, which
goes in `OrblitMaterialPackages.cpp` as above; the platform build supplies
the selected `generated/<set>/` directory as an include path; a new method
in `include/OrblitRenderer.h` becomes a public member of `orblit::Renderer`
plus a one-line forwarder in `OrblitRenderer.mm`.

The scripts that did the original conversion — and regenerated the core for a
branch cut before the move, which is how god rays, distortion and motion blur
came across — lived in `packages/orblit_filament/native/port_from_mm`. They
are gone: every such branch has landed, and rerunning them now would throw
away everything written to the C++ since. `git log` has them if one is ever
needed again.

## What is still Apple's, and why

- `OrblitRenderer.mm` and `include/OrblitRenderer.h` — the Swift plugin speaks
  Objective-C.
- `OrblitSurfaceApple.mm` — IOSurface-backed `CVPixelBuffer`s are how Flutter
  on Apple adopts a frame without a copy. Every platform presents
  differently; `OrblitSurface` is the seam, and `OrblitSurfaceHeadless.cpp`
  adds a window surface and an offscreen one for everywhere.
- `OrblitPlatformApple.mm` — the Apple answers to the platform layer, kept
  so an Apple build does what it did.
- `OrblitTexture.m`, `OrblitFilamentPlugin.swift` — Flutter's Apple plugin.
- Video — only AVFoundation is written. MediaCodec, GStreamer or Media
  Foundation are each their own piece of work.

## What was proven, and what was not

Proven on this Mac:

- Every plain C++ file — the core, the C ABI, the platform layer's portable
  half and the helpers — compiles with `ORBLIT_PLATFORM_PORTABLE`, and
  `clang -M` finds no Apple framework, Objective-C or dispatch header among
  the 700 to 900 each includes.
- The aarch64 Linux release's headers are identical to the macOS ones apart
  from `gltfio/materials/uberarchive.h`, so compiling against the macOS
  headers is representative.
- `native/headless` links the portable build into two C programs that
  include only `orblit_renderer.h`: the C ABI's test, which passes, and a
  headless host that draws a scene offscreen and writes a PNG.

Proven on the iOS simulator, which is the second platform the same sources
serve and the only one that runs without a signing identity:

- The whole package builds and links for the simulator with no change to any
  source — core, C ABI, platform layer, wrapper and plugin — against the
  xcframework's `ios-arm64_x86_64-simulator` slice.
- The app launches, the plugin registers, and a Filament Metal engine is
  created on the simulator's GPU. `flutter build ios --simulator` and a run
  are now a CI job beside the macOS one.

Proven on the iOS simulator, added since:

- A frame. The slim surface (see "Materials and feature levels" below) is
  what let this happen at all: below it, the engine aborted before a
  triangle was drawn. `tool/ci_draw_frame_ios.sh` boots, installs, launches
  and waits for the same "written" line the macOS script does, and is a CI
  job on `iPhone 17 Pro`, `iPhone 16` and whatever else `ORBLIT_SIM_DEVICE`
  names.
- Along the way, a latent bug the feature level precondition had always
  masked: decalImages, the one sampler read with `textureGrad` rather than
  `texture`, took Filament's default mobile precision, and the derivatives a
  material hands `textureGrad` compiled to Metal's `half2` there — which
  `metal::gradient2d` has no constructor for, only `float2`. Desktop Metal's
  default is already full precision, which is why nothing had ever shown
  this. Both `lit.mat` and `lit_slim.mat` now mark that sampler and its
  derivatives `highp`. A real device would have hit the same panic, high end
  or not, so this was fixed for the standard surface too, not only the slim
  one.
- That a scene a host publishes reaches the renderer on the simulator — it
  always did. What actually reached the renderer was never in question:
  temporary logging through `applyObjects` showed it running on every single
  call, building the right number of objects and flipping
  `_sceneIsOwnedByHost` correctly. The scene itself was the same one every
  time, which is what looked like the startup placeholder: `dart:io`'s
  `Platform.environment` comes back an empty map on the iOS simulator
  regardless of how the process was launched — proved by writing its length
  to a file mid-run (0) while that launch's own `SIMCTL_CHILD_`-prefixed
  variables were visible to native `getenv` throughout. `examples/gallery`
  reads `ORBLIT_EXAMPLE` (and every other `ORBLIT_*` switch, `ORBLIT_SECONDS`
  and `ORBLIT_CIRCLING` included) through `Platform.environment`, so on iOS it
  always fell back to the gallery's first example, and its clock and camera
  orbit were never pinned either — which is also the likely explanation for
  the exposure finding below: the same rotating cube and orbiting camera,
  caught at whatever unpinned moment frame 60 happened to land on. Fixed in
  `examples/gallery/lib/main.dart`, which now reads the real environment
  through `dart:ffi` when `Platform.environment` has nothing, changing
  nothing on a platform where it already did.
- **A shadow-casting directional light on the simulator, which used to light
  nothing.** The entry that sat here explained the dark frames away — "a few
  parts in 255 where the same content on macOS reads two hundred plus" — as
  an unpinned clock catching a shadowed face, on the grounds that
  multiplying the pixels by twenty recovered the right geometry and colour.
  It recovered the geometry and the colour but not a shading gradient, and
  that was the part that mattered: the faces were flat.

  Measured with the clock pinned (`ORBLIT_SECONDS=1`, `ORBLIT_CIRCLING=0`,
  frame 30, the gallery's first example, one directional light): the cube's
  three visible faces read (6.2, 1.4, 1.3), (6.6, 1.5, 1.5) and (5.8, 1.3,
  1.3) — the same within noise, which no directional light can produce —
  and the floor read (0.3, 0.6, 1.3). Turning off that one cube's
  `castShadows` and changing nothing else gave (198.0, 77.9, 47.5), (149.3,
  42.6, 24.4) and a floor of (34.1, 37.7, 41.4): a properly shaded scene,
  thirty times brighter. So the light was never missing — the directional
  shadow lookup returned nought for every receiver, the direct term was
  multiplied away, and what was left was the ambient.

  Not the slim surface and not shadows in general: the same scene on the
  Android emulator at feature level 1, on the same slim surface with the
  same light still casting, draws correctly with a visible cast shadow, and
  so does macOS on the standard surface.

  The cause is Filament's, and it is not the hardware. Its Metal backend
  rewrites a sampler's comparison function to `MTLCompareFunctionNever`
  whenever an iOS build's device answers no to
  `MTLFeatureSet_iOS_GPUFamily3_v1` — `filament/backend/src/metal/
  MetalState.mm`, which prints "sample comparison not supported by this
  GPU" on every simulator run, and the line is in every log recorded above.
  `Never` fails every comparison, so every `sample_compare` returns nought,
  and PCF, DPCF and PCSS are each made of `sample_compare`. The simulator's
  virtual GPU declines that feature set.

  It performs the comparison perfectly all the same. A Metal probe built for
  the simulator and run on its own device — a depth texture holding 0.5,
  sampled `LessEqual` against 0.25, 0.5, 0.75 and 1.0 — answers 1, 1, 0, 0
  exactly, and the complement of that under `Greater`. Only the
  advertisement is missing. But the decision is taken inside the framework
  this package links as a published binary (see `setup.sh`: a source build
  of Filament serves macOS, and iOS always comes from the release), so the
  renderer cannot talk it out of it.

  So the renderer stops asking for a shadow Filament will not compare.
  `orblit::shadowComparisonAvailable` (`OrblitPlatform.h`) asks Filament's own
  question once at startup, and `applyViewShadows` substitutes a variance
  shadow — which keeps depth moments in an ordinary colour texture and
  compares them with arithmetic in the shader — for whichever comparison
  kind was asked for. The host is told through `notes()` under "shadows",
  the same way the slim surface says what it dropped. The same scene now
  reads (197.9, 77.9, 47.5), (149.0, 42.6, 24.4) and a floor of (34.1,
  37.7, 41.4), within half a level of the `castShadows` control on the same
  device and of macOS, with a cast shadow the control has not got; and
  Filament's warning no longer appears, because no comparison sampler is
  built. Variance shadows have softer edges than PCF and can bleed light
  through a thin occluder, which is the cost and which the note says.

  **A real pre-A13 iPhone is not affected**, and that is worth stating
  because it is the other place the slim surface and Metal meet. The feature
  set Filament tests is the A9's; every device this package admits is an A9
  or newer, because the podspec's floor is iOS 13 (the gallery's is 15) and
  no A8 or older device runs either. An iPhone 6s through an iPhone XS gets
  feature level 2 and the slim surface — the simulator's combination
  exactly — and still passes the gate, so its sampler keeps its comparison
  and its shadows are ordinary PCF. Reasoned from Apple's family-to-silicon
  mapping and the deployment floor, not measured: there is no device here.
  What would settle it outright is one run on an A12 or older iPhone —
  `[device supportsFeatureSet:MTLFeatureSet_iOS_GPUFamily3_v1]` coming back
  YES, and the gallery's first example drawing a shadow with no note under
  "shadows". Until somebody does that, the claim rests on the mapping.

Not proven:

- A frame from a real device. That needs a signing identity there is none of.
- A Linux, Android or Windows build. Docker's daemon did not answer, so the
  core has not been compiled against a Linux sysroot or linked against the
  Linux release.

Found on macOS while chasing the above: Panel shadows and Irradiance field
are not frame-for-frame reproducible even on the standard surface, unmodified,
at the commit this branch started from. Launching the identical `.app` twice
in a row gave three different frames for Irradiance field and two for Panel
shadows across three launches total — both examples carry something that
accumulates over a run (the field's own two atlases; the PCSS search's
blocker average), and whatever it depends on is not fully pinned by
`ORBLIT_SECONDS`/`ORBLIT_CIRCLING`. Lights and Decals, which carry no such
accumulation, were bit-for-bit reproducible on every rebuild this branch's
work involved, source-unchanged or not — that is the pair this branch's own
"no regression" claim rests its evidence on. A precision fix briefly kept in
`lit.mat` (see its git history) looked like it broke Panel shadows' parity
for exactly this reason before this was understood; reverted once the real
cause was found run-to-run on the unmodified commit itself.
- Any backend but Metal drawing a frame. This Mac has no Vulkan driver, and
  its OpenGL is 4.1, feature level 1, below the standard surface; a headless
  OpenGL swap chain there also needs a main-thread run loop.
- Video, and `ORBLIT_SURFACE_WINDOW`, anywhere but Apple.

## Materials and feature levels

`setup.sh` compiles for Metal by default; `ORBLIT_MATC_BACKENDS` names others
(`vulkan opengl`, or `all`). Every material compiles with `-a all -p all`.
Embedded, the thirty-seven packages are 4.70 MiB for Metal, 3.32 MiB for
OpenGL alone, 13.27 MiB for Vulkan and OpenGL, and 18.21 MiB for all.

`lit.mat` declares `featureLevel : 3`: the standard surface binds twelve
samplers (seven maps, the light data, the area shadow, the field atlas and
two for decals), and Filament allows a material nine below the third level.
matc enforces that at build time, so the declaration cannot simply be
lowered — `featureLevel : 2` fails with "has feature level 2 and is using
more than 9 samplers", the second level's sixteen texture units
notwithstanding. Every other material is feature level 1. OpenGL ES 3.0,
WebGL 2 and desktop OpenGL below 4.3 are feature level 1, so on those the
standard surface does not load, and the renderer does not start. Reaching
them means a lit surface with nine samplers or fewer.

Metal is not automatically above that bar. `MetalDriver::getFeatureLevel`
returns the third level for `MTLGPUFamilyApple6` or `MTLGPUFamilyMac2` and
newer, and the second for everything else — so A13 and later (an iPhone 11
onwards) and every Apple silicon Mac, but *not* the iOS simulator, whose
virtual GPU reports `MTLGPUFamilyApple2`. On the simulator the engine used to
start at the second level and abort when the first lit object was built.

There is now a degradation path: `lit_slim.mat` is a second standard surface,
feature level 1, nine samplers. It keeps every picture map and ground
blending, packs the LTC pair, the rectangles' own data and decalData into one
texture (three tenants sharing `lightData`, read by `texelFetch` and
`textureLod` as the standard surface's own LTC tables and rectangles already
were) and keeps decalImages besides it, so textured decals and ground
blending both survive. What does not fit is the area shadow map and the
irradiance field atlas — a rectangle still lights a slim surface, only
unshadowed, and a scene's field does not reach it at all. `Renderer::
startWithWidth` asks `getSupportedFeatureLevel()`, exactly as before, and
now chooses between the two surfaces by what comes back rather than only
clamping the engine to it; `surfaceAt` hands out the slim five packages in
place of the standard five whenever it does. What a scene loses is said once
through `notes()`, under "surface", "areaShadows" and "field" — the same
mechanism that already reports a missing texture or an unplayable video —
so a host is told rather than left to notice a shadowless panel or a dark
field on its own.

## The audit this started from

Taken at `d6447da`, when `OrblitRenderer.mm` was 7,287 lines; line numbers
are into that version.

| Construct | Lines | What it was for |
|---|---|---|
| `@interface` / `@implementation` / `@end`, the ivar block, 125 method definitions | 854–857, 867–1317, then 1319 to 7287 | the class itself |
| `[self …]` message sends (132 lines) | 1330 to 7284 | the class calling its own methods |
| `NSLog` (16) | 1332, 1335, 1348, 1449, 2011, 2023, 2134, 2139, 2154, 2753, 3136, 6802, 6813, 6816, 6964, 7027 | diagnostics; `tool/ci_draw_frame.sh` waits for `[orblit] frame` |
| `NSString`, `NSArray<NSString *>`, `NSData`, `NSURL`, `@"…"`, `stringWithFormat:` (143 lines) | 344; 2007–2155 (mesh loading); 2707, 2764; 3071–3096 (textures); 3439–3563 (cubemaps); 3653; 4632, 4727, 4886; 5191; 5396–5611 (decals); 5899; 6038; 6781–6786; 7247–7280 (notes) | paths in, notes out |
| `NSMutableDictionary` notes | 1170, 1176, 1180–1181, 1205, 4896, 5255, 5585 | what a scene asked for that could not be given |
| `NSLock` | 1255, 1291, 1360–1361, 6454–6457, 6485–6488, 6679–6682, 6826–6835, 6903–6906, 7037–7043 | the camera hand-off and the presented index, between threads |
| `NSUInteger`, `NSInteger`, `BOOL` / `YES` / `NO`, `nil`, `MAX` (61 lines) | e.g. 864, 879, 1053, 1240–1241, 1292, 1355–1356, 4548, 6110 | Foundation's types and macros |
| AVFoundation, CoreMedia, `CACurrentMediaTime`, `NSNotificationCenter` with a block and `__weak` | 3, 429–446, 3344–3425, 4629–4721 | video onto an external texture |
| CoreVideo `CVPixelBufferRef` | 436, 3356–3358, 3420, 4710, 4718, 7036–7044 | video frames; `copyPresentedBuffer` |
| ImageIO and CoreGraphics | 4–5, 5396–5425 | decal pictures |
| `dispatch_apply` | 2105–2107 | reading a model's files in parallel |
| `CFAbsoluteTimeGetCurrent` | 1353, 1450, 2008, 2016, 2029, 2153, 6451, 6531, 6589, 6748, 6772, 6804, 6911, 6927 | load timings, camera clock alignment, pass timings, pacing |
| `Engine::Backend::METAL` | 1371 | the only backend it ever asked for |
| `#import` of Foundation, AVFoundation, CoreGraphics, ImageIO | 1, 3–5, 28 | |

Not Apple but not portable either: POSIX `open`/`read`/`fstat` in
`readWholeFile` (361–394), absent under MSVC and now behind
`orblit::readWholeFile`; `M_PI` (1657, 1661, 1868, 3910), defined by the core
if the platform does not; `getenv` (1362, 5115, 6993), harmless where nobody
sets the environment.
