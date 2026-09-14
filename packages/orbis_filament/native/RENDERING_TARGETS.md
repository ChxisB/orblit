# Rendering coverage and performance

The goal is a working, measured presentation path for each device family.
Filament already implements Metal, Vulkan, OpenGL/GLES and WebGPU. Dawn
implements the APIs below WebGPU, including D3D12 on Windows. GPU adapters
are discovered from installed drivers at runtime; they are not binaries we
can compile once for every device.

## Current scope

Target Switch 1, Switch 2, Xbox, macOS, Linux/SteamOS (including Steam Deck),
iOS, Android and Windows. Switch 1 and Switch 2 require separate builds and
validation; Xbox is listed once as a family, with generation coverage to
confirm before implementation. This list defines scope, not a delivery order.

PlayStation is explicitly out of scope for now. Browsers are not in the
current list, so browser/WebGL/Wasm development is deferred. Preserve the
existing web implementation and material sets; do not remove them or break
their compiler/runtime pairing. Native WebGPU/Dawn is independent of browser
support and remains an experiment for reaching D3D12.

These are proposed defaults to validate on representative hardware, not a
claim that any API is universally fastest. Support in Filament, an Orbis
plugin that builds, a rendered frame, and measured performance are separate
milestones.

| Device family | Preferred path to measure | Compatibility / additional path | Current evidence and next gate |
| --- | --- | --- | --- |
| macOS | Native Metal | Native WebGPU over Dawn/Metal and OpenGL for backend testing | Apple plugin uses IOSurface buffers. Native Metal, WebGPU and OpenGL headless tests run on the M4 Pro; measure the actual Flutter presentation path. |
| iOS (iPhone / iPad) | Native Metal | Capability-based quality fallback within Metal | Existing Apple plugin and simulator shadow work; validate on physical devices, background/resume and sustained thermal load. Mac GPU proof is not iOS device proof. |
| Android | Native Vulkan, compared with GLES on each GPU family | GLES fallback | Existing Kotlin/JNI plugin and Android build path. Measure physical Adreno/Mali devices, surface replacement and sustained thermal load before selecting defaults. |
| Linux / SteamOS / Steam Deck | Native Vulkan | OpenGL where material linking is validated | `feat/linux-plugin` renders through software Vulkan. Flutter presentation uses CPU readback. Validate on Deck and real desktop GPUs; address presentation copies and the OpenGL linker failure. |
| Windows | Native Vulkan | OpenGL fallback when validated; native WebGPU/Dawn/D3D12 experiment | `feat/windows-plugin` builds in CI; its Flutter path uses CPU readback. Build Dawn with D3D12 on Windows and set `forceBackendType`, then validate on Intel/AMD/NVIDIA hardware; adapter selection ignores backend type otherwise. There is no direct Filament D3D12 backend. |
| Switch 1 | Vendor-supported native graphics path; evaluate Vulkan against the licensed SDK | SDK-specific adapter/backend if required | Console host groundwork exists on `feat/console-host`; there is no console build here. Confirm device/swapchain, shader and synchronization requirements before promising a Vulkan port. |
| Switch 2 | Independently validate its SDK's native graphics path | Reuse Switch 1 integration only where verified compatible | Requires Switch 2 SDK access and hardware. Neither a Switch 1 build nor generic Vulkan support establishes a native Switch 2 port. |
| Xbox | D3D12.x through the Xbox GDK | Dawn bridge and a native Filament D3D12.x backend are now both open options, neither cheap | Requires the GDK with Xbox Extensions, development hardware and shader compilation/cache validation. Source read 14 Sep 2026: vendored Dawn has no Xbox support and four structurally desktop-only subsystems; upstream Dawn lists Xbox as "Not supported". The D3D12.x reference itself is NDA-gated. See the bridge section below. Desktop Dawn/D3D12 success is not Xbox support. |

Linux, Windows, iOS shadow and console host changes are still on separate
branches. Integrate the in-scope core changes, generated material selection,
package versions and CI gates before treating this table as coverage on one
release branch. Retain shared fixes from the web work where needed by native
targets without adding browser delivery back into this scope.

Microsoft's [GDK introduction](https://learn.microsoft.com/en-us/xbox/gdk/docs/gdk-dev/intro/introduction)
requires D3D12.x for Xbox console games. [Dawn](https://dawn.googlesource.com/dawn)
provides a native desktop D3D12 implementation, not a documented Xbox port;
using it for Xbox is an engineering hypothesis to test. Nintendo's
[developer portal](https://developer.nintendo.com/home/development-for-nintendo-platforms)
gates platform development information behind approval. Public desktop API
support is insufficient to choose either Nintendo console's implementation.

Each platform adapter must own native device/surface creation, matching
shader artifacts, GPU completion and buffer handoff, display scheduling,
resize/background/resume and device-loss recovery where applicable. The
shared renderer should not duplicate those platform lifecycles. Reusing an
API alone does not deliver a usable low-latency adapter.

### The Dawn D3D12 bridge, read against the vendored source

Read on 14 September 2026 against the fork's vendored Dawn, revision
`a117f96e09e88e76c0a9e3b553b7316cda50633d` (the `chromium/8021` roll),
which already carries four Filament patches in `third_party/dawn/tnt/`.

**The Filament half is small.** `WebGPUPlatform` declares three pure
virtuals — `getSurfaceExtent`, `createSurface` and `getAdapterOptions` —
with `requestAdapter`, `requestDevice` and `getConfiguration` overridable.
`WebGPUPlatformWindows.cpp` implements the whole Windows platform in about
a hundred lines, and **already lists `wgpu::BackendType::D3D12`** among the
adapters it asks for. Reaching D3D12 needs no Filament backend work.

**The Dawn half is the entire cost, and it is not a shim.** Searching
`src`, `include` and `CMakeLists.txt` for `xbox`, `_GAMING_XBOX`, `GDK`,
`d3d12_x`, `scarlett` or `durango` returns nothing. Upstream Dawn's
[support matrix](https://github.com/google/dawn/blob/main/docs/support.md)
states the position directly: Win32 "Supported", UWP "Supported, best
effort", Xbox **"Not supported, contributions welcome."** The desktop
assumptions are structural, not incidental:

| Dawn dependency | Evidence | Why Xbox differs |
| --- | --- | --- |
| Loads OS graphics DLLs at runtime | `d3d12/PlatformFunctionsD3D12.cpp:65,89,220,225` (`d3d12.dll`, `d3d11.dll`, `dxil.dll`, `dxcompiler.dll`); `d3d/PlatformFunctions.cpp:85,113` (`dxgi.dll`, `d3dcompiler_47.dll`) | The GDK links its D3D12.x runtime; there are no such system DLLs to open |
| Discovers a GPU through DXGI | `d3d/BackendD3D.cpp:46` (`IDXGIFactory4`), `:111` `EnumAdapterByLuid`, `:157-168` `EnumAdapterByGpuPreference`/`EnumAdapters1` | One fixed GPU, created through `D3D12XboxCreateDevice` with `D3D12XBOX_CREATE_DEVICE_PARAMETERS`; there is nothing to enumerate |
| Presents through a DXGI swap chain | `d3d/SwapChainD3D.cpp:232-249` — `CreateSwapChainForHwnd`, `CreateSwapChainForCoreWindow`, `CreateSwapChainForComposition` | The GDK presents with `PresentX` and paces frames with `WaitForOrigin`; a different model, not a swap-chain variant |
| Compiles shaders at pipeline-creation time | `d3d/ShaderUtils.cpp:145` `CompileShaderDXC`, `:188` `CompileShaderFXC` | Console shaders are compiled offline by the GDK shader compiler. This is WebGPU's runtime WGSL→HLSL→DXIL model meeting a platform that does not allow it — the deepest mismatch of the four |
| Desktop-only headers | `d3d/d3d_platform.h` includes `<dxgi1_6.h>`, `<dxcapi.h>`, `<DXProgrammableCapture.h>`, `<dxgidebug.h>` | Not part of the Xbox header set |
| Build gate admits only Windows | Dawn `CMakeLists.txt:96-101` enables D3D12 solely under `elseif (WIN32)`; `:124` excludes `WINDOWS_STORE` | No console branch exists to extend |

Scale: `src/dawn/native/d3d12/` is 82 files and roughly 19,100 lines, over
a shared `d3d/` layer of 30 files and roughly 3,565 lines. A bridge means
forking that backend and carrying the fork across Dawn rolls, against an
SDK under NDA. Microsoft's own D3D12.x reference is gated: fetching the
[D3D12 on GDK overview](https://learn.microsoft.com/en-us/gaming/gdk/docs/features/graphics/d3d12x/d3d12x-overview)
returns "Access to this topic requires membership in a non-disclosure
agreement (NDA) Xbox developer program." The API contract cannot be read,
let alone implemented, before that membership exists.

**Conclusion for the matrix:** the Dawn bridge is not a cheaper route to
Xbox than a native backend — it is a fork of a 19k-line backend whose four
load-bearing subsystems are each wrong for the target. Because Filament's
backend interface is what any native path would target anyway, a direct
Filament D3D12.x backend is no longer obviously the more expensive option.
Decide between them once the GDK is readable, not before.

### What this unblocks now, without the GDK

Dawn's D3D12 backend on **Windows** is supported, buildable and reachable
through the platform class we already have. Building it exercises the whole
Filament → WebGPU → Dawn → D3D12 chain on hardware and CI we can run today,
which is the most Xbox risk that can be retired without a licence — and is
worth having for the Windows target on its own terms.

One correction before any such measurement is reported. `requestAdapter`
collects candidates into a `std::unordered_set` and `selectPreferredAdapter`
breaks ties on power preference and optional-feature count — **backend type
is never a selection criterion unless `Configuration::forceBackendType` is
set**. The order of the `backendTypes` array in `WebGPUPlatformWindows.cpp`
does not choose anything. So a Windows WebGPU run today may resolve to
Vulkan, OpenGL or D3D12 by hash iteration order, and could not be attributed
to D3D12 afterwards. Set `forceBackendType` and record the resolved adapter
before benchmarking, per the standing rule against benchmarking a fallback
adapter as the requested hardware.

## Build boundaries

Use one compiler/runtime revision per target and record the built artifacts.
Generate API-specific materials beside their runtime; the material format's
version number alone does not prove compatible shader variants.

The existing fork's `build.sh` supports desktop, Android, iOS and Wasm
toolchains; Wasm remains deferred. Build on the corresponding supported host with its toolchain;
do not enable every platform's API blindly in one build. In particular,
Dawn's D3D12 backend requires a Windows toolchain, and Apple frameworks
require Apple's SDKs. The target matrix to bring into CI is:

| Build target | Filament APIs | Additional requirements |
| --- | --- | --- |
| macOS arm64 / x86_64 | Metal; other APIs for diagnostic comparisons only | Xcode; Dawn/Metal for native WebGPU tests |
| iOS device / simulator | Metal | Xcode and separate device/simulator slices; no desktop Dawn archive in an iOS link |
| Android arm64 / x86_64 | Vulkan, GLES | Android NDK, host `matc`, matching ABI libraries; add 32-bit ABIs only where supported devices need them |
| Linux / SteamOS x86_64; Linux aarch64 | Vulkan, OpenGL | Matching C++ runtime and window-system libraries; real GPU runtime runs separate from software-renderer CI |
| Windows x64 / arm64 | Vulkan, OpenGL; optional WebGPU/D3D12 | Matching MSVC CRT; source build for architectures without a published SDK |
| Switch 1 | SDK-validated backend, not selected yet | Licensed platform toolchain, matching shaders and development hardware; separate gated CI job |
| Switch 2 | SDK-validated backend, not selected yet | Licensed Switch 2 toolchain and development hardware; separate gated CI job |
| Xbox (generation coverage to confirm) | D3D12.x; Dawn bridge feasibility gate first | GDK with Xbox Extensions, matching shader tools and development hardware; separate gated CI job |

Filament's backend support and build switches are documented in its
[README](https://github.com/google/filament/blob/main/README.md) and
[build instructions](https://github.com/google/filament/blob/main/BUILDING.md).
Our pinned fork and actual runtime tests remain the authority for what we
ship.

## Local API comparison

On Apple silicon, the existing [WebGPU build](webgpu/build.sh) can generate
all four APIs into `generated/webgpu-all` using the same fork compiler and
link them into one headless binary. The ordinary `webgpu` set remains
separate. The stamp records compiler and archive hashes as well as the
checkout revision; it does not claim old installed archives were rebuilt
merely because the checkout advanced.

From the repository root:

```sh
ORBIS_FILAMENT_WEBGPU_SRC=/absolute/path/to/orbis-filament \
ORBIS_MATC_BACKENDS=all \
ORBIS_BUILD_DIR=/tmp/orbis-all-api-build \
bash packages/orbis_filament/native/webgpu/build.sh

/tmp/orbis-all-api-build/orbis_headless /tmp/metal.png metal --benchmark 600 120 1280 720
/tmp/orbis-all-api-build/orbis_headless /tmp/webgpu.png webgpu --benchmark 600 120 1280 720
/tmp/orbis-all-api-build/orbis_headless /tmp/opengl.png opengl --benchmark 600 120 1280 720
```

Run GPU measurements serially with GPU access. The execution sandbox can
hide the Metal device: `Could not obtain Metal device` and `No WebGPU
adapters found` both occurred inside it, while the same WebGPU binary
rendered successfully outside it. This is not evidence that the host has no
GPU. The Vulkan-loader warning in Dawn's macOS enumeration is nonfatal when
it finds a Metal adapter.

The [headless host](headless/orbis_headless.c) also accepts `vulkan` and
`opengl` when the linked runtime, materials and installed driver support
them. The all-API build does not install drivers, build Windows D3D12 on a
Mac, or enable browser WebGPU. The existing
[release headless build](headless/build.sh) remains a separate control.

The final stdout line is JSON with resolution, rendered frame count,
skipped draws, startup/first-frame time, warmup count, elapsed time,
offscreen FPS and p50/p95/p99 draw time. A skipped call never counts as a
rendered frame. A final capture must succeed before a result is emitted.
Readback/PNG encoding are outside the timed interval. Zero/unreported GPU
timing becomes `null`; this fork's WebGPU timer queries are unimplemented.

The scene is six cubes with a directional light. These results are a small
regression baseline, not a workload capacity claim. Offscreen throughput
includes the renderer's current per-frame GPU wait, but excludes Flutter,
display scanout and input handling. It is not display FPS or input latency.
The first-frame measurement uses the machine's existing shader cache;
label a run cold only when the cache state is actually controlled. WebGPU
currently chooses the slim feature-level-2 surface; report that difference
when comparing with Metal's feature-level-3 surface.

### Local baseline — 14 September 2026

Apple M4 Pro, macOS 26.6.2 (25G83), arm64. Each sample renders 600 frames
after 120 warmup frames at 1280×720. Three serial samples per backend;
Metal/WebGPU alternate first, then OpenGL runs. All nine report zero skipped
draws. The machine was not thermally or shader-cache controlled: notably,
Metal's throughput varied substantially. These are observations, not a
speedup claim or a fair equal-feature API ranking.

| Backend / chosen feature level | Offscreen FPS, runs 1 / 2 / 3 | p95 draw ms, runs 1 / 2 / 3 | p99 draw ms, runs 1 / 2 / 3 |
| --- | --- | --- | --- |
| Metal / 3 | 592.05 / 835.48 / 1012.58 | 2.377 / 2.185 / 1.851 | 3.361 / 2.926 / 2.216 |
| WebGPU over Metal / 2 | 364.67 / 362.00 / 389.77 | 3.803 / 3.739 / 3.674 | 4.633 / 4.498 / 4.482 |
| OpenGL / 1 | 449.94 / 391.08 / 412.87 | 4.451 / 4.696 / 4.530 | 5.149 / 5.605 / 5.054 |

Compiler checkout: `6641c1e4fc53b34069db72a1d2ee36e9962d27e1`.
Build stamp (full binary identities, not a claim that the installed SDK
was rebuilt from that checkout):

```text
compiler=fbcb6f9d88be2d5ce0a3d8b26fa6bf2b35f21877ddb3f9684d5c8812722ebd18
archives=0990814be4debcfa5292d89484e59ce8a23bc3bb9501a1023b68b8a9f83a850a
flags=-a all -p all
```

The archive hash combines the `shasum` output of installed arm64 archives
(including paths); it identifies this local artifact set, not a relocatable
release manifest. All 42 material headers compiled with those flags. Vulkan
is material/build coverage only on this host: no Vulkan loader is installed.
OpenGL on Apple's driver does not resolve the separate Mesa linker failure.

Capture inspection exposed a double OpenGL row flip. Filament already
returns top-first rows; Orbis now preserves that ordering. The C ABI test's
red-cube-below-blue-sky fixture failed both orientation checks on OpenGL
before the fix, while Metal and WebGPU passed. After the fix, the complete
C ABI tests pass on all three fork backends and the release Metal control.
Metal and WebGPU 720p captures are byte-for-byte unchanged. A further
600-frame benchmark/capture per backend passed, as did the one-frame
percentile edge case; all 12 full-size JSON/PNG pairs passed schema/value
and dimension checks. The timing interval above
excludes capture, so this capture correction does not change the benchmarked
draw path. Runtime tests require a renderer to start; build-only success is
no longer reported as a runtime pass.

## Performance work, in order

1. Integrate and verify the existing platform branches with matching
   compiler/runtime/material artifacts for the current native target list.
   Keep browser delivery and PlayStation out of this pass. Run console SDK
   and bridge feasibility checks as soon as access is available; do not
   claim a console build while those gates are closed. Capture the selected API, physical
   adapter, driver and feature level in each test report. Never silently
   benchmark a fallback adapter as the requested hardware.
2. Measure the full presentation path. Linux and Windows currently read
   pixels back to CPU buffers and use approximately 60 Hz timers. Measure
   their copy and scheduling costs, then implement GPU texture sharing or
   native presentation where the embedder permits it. Replace fixed timers
   with the display's frame scheduling before claiming 90/120/144 Hz.
3. Audit the per-frame `flushAndWait()` in `Renderer::renderAtTime`.
   It currently ensures Flutter can sample shared Apple buffers safely.
   Replace it only with an explicit completion/ownership contract per
   surface, bounded frames in flight and resize/teardown tests. Removing
   the wait alone can produce stale buffers or overwrite one still sampled.
4. Measure shader startup and hitching, then add warmup/caching compatible
   with the target API. Keep startup results separate from warm-frame
   results. The historical Dawn cold-start cost makes this a release gate.
5. Run representative batching, shadows, transparency, weather, splats and
   post-processing scenes. Record frame-time tails and memory under sustained
   load. Tune resolution, quality and optional passes from those results.

Use 16.67 ms as the total frame interval at 60 Hz, 11.11 ms at 90 Hz,
8.33 ms at 120 Hz and 6.94 ms at 144 Hz, reserving time for simulation and
composition. Measure input-to-display latency separately with an
instrumented presentation path or external measurement. An average FPS
number alone does not establish smoothness or low latency.

Filament describes readback's performance cost and its frame pacing APIs in
[Renderer.h](https://github.com/google/filament/blob/main/filament/include/filament/Renderer.h).
