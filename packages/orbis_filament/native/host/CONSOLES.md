# Porting Orbis to a console

There is no Nintendo SDK here and no Microsoft GDK. Nothing in this repository
is a console port, nothing here has run on a console, and this document does
not claim otherwise. What it is is a brief: what the work actually is, what
of it can be done without an SDK, what an SDK would unlock on day one, and —
said out loud, because it is the honest half — where the answer is behind an
NDA and therefore not written here.

Everything below is supported by code in this repository or in the fork at
`../orbis-filament`. Where it is not, it says so.

## A port is three pieces

**The graphics back end** lives inside Filament, in the fork. Filament
abstracts the graphics API, so the renderer above it — 10,500 lines of plain
C++ in `darwin/.../orbis_filament_native/*.cpp` — does not change for a new
platform. What changes is one class.

**The front end** owns a window, a clock and a pad, and drives the renderer
through the C ABI. That is this directory: 1,300 lines, of which 453 —
`orbis_host_sdl.c` — are the only ones that know what machine they are on.

**Game logic** has to reach the C ABI. Dart does not run on a console;
QuickJS and linked C++ already do.

The three are independent. The first needs an SDK. The second needs an SDK
for one file. The third needs none at all, and is the largest of the three.

## Switch

### The graphics port is a `VulkanPlatform` subclass

`filament/backend/include/backend/platforms/VulkanPlatform.h` has exactly
**two pure virtual functions**:

```cpp
virtual ExtensionSet getSwapchainInstanceExtensions() const = 0;
virtual SurfaceBundle createVkSurfaceKHR(void* nativeWindow, VkInstance instance,
        uint64_t flags) const noexcept = 0;
```

— name the instance extensions your surface needs, and turn a native window
into a `VkSurfaceKHR` plus its extent. Everything else the class needs is
already written and overridable where a platform disagrees:
`createVkInstance`, `selectVkPhysicalDevice`, `createVkDevice`,
`getCustomization`, `getSwapChainBundle`, `acquire`, `present`, `recreate`,
`hasResized`, `isProtected`, `destroy`, `terminate`.

The existing subclasses say how big that is in practice:

| | lines |
|---|---|
| `VulkanPlatformWindows.cpp` | 73 |
| `VulkanPlatformApple.mm` | 65 |
| `VulkanPlatformLinux.cpp` | 184 (most of it the X11-or-Wayland dance) |
| `VulkanPlatformAndroid.cpp` | 745 (external images and protected content) |

So the floor for a new platform that is only presenting to a window is
**under a hundred lines**, and the ceiling is what the platform asks for
beyond that. A console that wants its own allocator, its own device
selection, or its own present pacing overrides more; none of that changes the
renderer.

This is a real seam and not a hoped-for one: it is where `VulkanPlatformLinux`
puts the X11 `Window` cast that `orbis_host_sdl.c` feeds it, and that path is
running here today under lavapipe.

### What needs the SDK, and what does not

| Needs it | Does not |
|---|---|
| `createVkSurfaceKHR` — the surface extension and the native window type | The whole of `VulkanPlatform`'s other twelve methods, unless the platform disagrees with one |
| The toolchain: which clang, which libc, which C++ runtime | The 10,500-line renderer core, already proven to compile with no Apple, POSIX-only or Objective-C header (`PORTING.md`) |
| Swapchain acquire/present semantics and the frame pacing they imply | `orbis_host.c` and `orbis_pad.c` — 580 lines that are the same everywhere |
| The pad API, the clock, the window: `orbis_host_platform.h`'s eight calls | `orbis_pad.h`'s vocabulary, shaping, slots and hot-plug |
| The file system, the memory budget, the shader precompile format | The materials: `ORBIS_MATC_BACKENDS=vulkan darwin/setup.sh` compiles SPIR-V today |
| Certification: what a title must do on suspend, resume, and controller loss | `orbis_renderer_detach_surface` / `attach_surface`, written for exactly that lifecycle on Android |

### Day one with an SDK

1. Compile the portable set with the SDK's toolchain. `native/headless` is
   the smallest thing that links it, and its first failure is the real
   answer about the C++ runtime.
2. Write `VulkanPlatformNX` against the two pure virtuals, shaped like
   `VulkanPlatformWindows.cpp`.
3. Rewrite `orbis_host_sdl.c` against the SDK's window, clock and pad. The
   eight calls are the whole list; `orbis_pad_raw` is the whole of what a pad
   back end fills in.
4. `ORBIS_MATC_BACKENDS=vulkan darwin/setup.sh`, and run the headless host
   for a PNG before ever opening a window.

What you would learn first is whether Filament's Vulkan backend's memory and
descriptor-set model fits the console's budget, and whether the device
reaches Filament's third feature level — twelve fragment samplers. If it does
not, the renderer already degrades to `lit_slim` on its own and says so
through `notes()`; it does not fail.

### Where the NDA bites

The surface extension's name, the native window type, the swapchain and
present semantics, the pad API, the memory budget, the shader precompile
format and every certification requirement are all under NDA, and none of
them are guessed at here. The consequence is that the estimate above is a
*shape* — "like `VulkanPlatformWindows.cpp`" — and not a line count, and that
the frame pacing, which is where console ports usually actually go wrong, is
not something this document can say anything useful about.

Also not public, and worth asking first: whether Vulkan or the platform's own
API is the supported path for a shipping title. If it is not Vulkan, the
Switch port becomes the Xbox problem — a backend Filament does not have —
and the honest answer changes completely.

## Xbox

### There is no Vulkan, and Filament has no D3D12 backend

That is the whole difficulty, and it is a different kind of difficulty from
the Switch's. Three routes, and this branch measured one of them.

**(a) WebGPU over Dawn onto D3D12.** Filament's WebGPU backend talks to Dawn,
Dawn has a D3D12 backend (`third_party/dawn/src/dawn/native/d3d12`, vendored
in the fork), and `WebGPUPlatformWindows.cpp` — 95 lines — already turns an
HWND into a `wgpu::Surface`. Nothing has to be written inside Filament at
all. This is the plausible route and the one the project's own documents
called unmeasured.

**(b) Write a D3D12 backend for Filament.** `filament/backend/src/vulkan` is
the comparison; it is not a few hundred lines and it would have to be kept
working against upstream forever.

**(c) A Vulkan-to-D3D12 translation layer.** Not ours to write, and whether
one is licensable and supportable on the console is not public.

### The measurement (a) was missing

`build.sh -W`'s help text in the fork says "NOT functional atm". As of this
branch, on this Mac, **that is out of date**.

The fork built with `-DFILAMENT_SUPPORTS_WEBGPU=ON` compiles and installs
clean, and Orbis's headless scene **draws through Dawn onto Metal** —
identical geometry, shading and shadows to the Metal control, and the
windowed host in this directory runs on it at 120 fps. The measurement is
Metal rather than D3D12, because there is no Windows machine here; what it
establishes is that the whole chain above the Dawn backend — Filament's
WebGPU driver, Tint, the WGSL Orbis's materials compile to, the descriptor
sets, the render graph, the post-process stack — works on a real scene. The
part it does not establish is D3D12 itself, which is Dawn's own backend and
the piece with the most industry mileage on it.

Three caveats, each of which is a real finding:

1. **One material will not compile.** `lit.mat` — the standard surface —
   fails for `-a webgpu` on every variant:

   ```
   Tint error: spirv error: SPIR-V failed validation.
   spirv:1:1 error: All OpSampledImage instructions must be in the same block
   in which their Result <id> are consumed.
   ```

   glslang hoists an `OpSampledImage` out of the block that consumes it —
   the area-shadow search, which reads `materialParams_areaShadow` with
   `textureLod` inside a loop, is the likely site — and Tint refuses the
   SPIR-V that Metal, Vulkan and OpenGL all accept. The other **25 of 26**
   materials compile, `lit_slim.mat` among them.

2. **WebGPU reports feature level 2**, because `WebGPUDriver::getFeatureLevel`
   derives it from `maxSamplersPerShaderStage` and WebGPU's limit is below
   Filament's third level. So the renderer picks the slim surface by itself,
   which is why the first caveat did not stop the measurement: `lit` is never
   loaded. What a scene loses is what the slim surface always loses —
   unshadowed area lights, no irradiance field — and `notes()` says so. The
   two effects cancel, but they cancel by luck: fix the feature level and the
   material becomes blocking again.

3. **The first frame costs minutes.** The cold run took about five minutes,
   almost all of it `MTLCompilerService` at 99% CPU compiling the MSL that
   Dawn generates from Tint's WGSL. The same binary run again takes **0.95
   seconds** — the OS's shader cache. On a console, where shaders are
   precompiled offline and there is no such cache to fall back on, this is
   the number to worry about, and it is not knowable without a GDK.

Two smaller ones: `WebGPUDriver`'s timer queries are stubs
(`getTimerQueryValue` returns `ERROR`, `beginTimerQuery` and `endTimerQuery`
are empty), so `orbis_stats.gpu_milliseconds` reads 0.00 on WebGPU and no
GPU-side performance claim can be made through it at all; and there are 48
`TODO`s across `filament/backend/src/webgpu/*.cpp`.

### What needs the GDK, and what does not

| Needs it | Does not |
|---|---|
| Building Dawn for the console — its ~2,000 source files, its D3D12 variant (`d3d12_x`), its threading | Filament's WebGPU backend, which is behind Dawn and platform-independent |
| `WebGPUPlatformXbox::createSurface` — 95 lines by the shape of the Windows one, if the console's swapchain is HWND-shaped | The materials: `ORBIS_MATC_BACKENDS=webgpu` compiles 25 of 26 today |
| Whether the console permits Dawn's runtime shader compilation at all | `orbis_host.c`, `orbis_pad.c`, the renderer, the C ABI |
| The pad, the window, the clock: the same eight calls | `orbis_pad.h`'s vocabulary — Xbox's pad is the one SDL's positional names were written for |

### Day one with a GDK

Try to build Dawn. That single step is the whole risk in route (a), and it is
not reducible by anything that can be done here: the console's D3D12 is a
variant of the desktop one, the toolchain is not the desktop one, and Dawn is
a large Chromium-derived tree with its own build system. If Dawn builds, the
rest of the port is this host rewritten against the SDK and the materials
recompiled, and the measurement above says the engine will draw. If Dawn does
not build, route (a) is dead and the choice is (b) — a D3D12 backend, which
is a project rather than a port.

The second thing worth doing on day one is fixing caveat 1 properly, because
sooner or later the feature level will be right and `lit` will be needed.

### Where the NDA bites

Everything about the console's D3D12 variant, its shader pipeline and whether
runtime compilation is permitted at all; the window, pad and clock APIs; the
memory budget; certification. The measurement above is therefore about
*Filament and Tint and Orbis's materials*, and deliberately makes no claim
about D3D12 or about the console.

## Game logic, without Dart

Dart is how Orbis is written and it is not going to a console: there is no
Dart AOT target for either platform, and a JIT is not permitted on either.

What already runs on a console-shaped machine, and is already wired to the C
ABI in this repository:

- **QuickJS.** `orbis-script` puts game logic in TypeScript on QuickJS,
  calling the engine's C ABI directly — "the shim is C, the boundary is
  handles and buffers". QuickJS is C99 with no OS dependency beyond malloc
  and reading a file, which is the property that makes it portable to a
  platform whose libc is not glibc.
- **Linked C++.** `orbis_native`'s `include/orbis_script.h` already defines
  the contract: a script answers four questions — what ABI it was built for,
  start, step, stop — and is handed *a table of function pointers*, not
  symbols to link against. On a desktop that table is resolved at load from a
  shared library; on a console, where loading code at runtime is restricted,
  the same table is filled and the same four functions are called with the
  script statically linked in. The design already anticipated this: "a script
  is a file the engine loads, not a file the engine has to have been linked
  into" — and the table is what makes the second case work too.

So the console binary is: this host, plus the renderer, plus the core, plus
either a QuickJS interpreter or statically linked C++ scripts. No Dart, no
Flutter, no dynamic loading.

**The real gap, and it needs no SDK.** Today the *scene* is described from
Dart: `packages/orbis_core`, `orbis_mesh`, `orbis_light` and the rest build
the arrays that `orbis_renderer_apply_*` takes, and the C ABI is deliberately
"describe the whole of your part of the scene every time" so that whoever is
describing it can be anyone. On a console someone else has to do that
describing, and nobody does yet. Two answers, neither written:

1. Move the scene layer into the C++ core, or into TypeScript over the C ABI,
   so a game is authored once and runs in both places.
2. Bake a scene from Dart into data at build time, and have the C host replay
   it.

`orbis_host.c` is an existence proof of the second at its smallest: 324 lines
of C describing a scene through the C ABI with no Dart anywhere. It is also
an existence proof of how much work the real version is, because those 324
lines describe eleven boxes.

This is the largest piece of the three, it is the one with no NDA anywhere
near it, and it is the one that could be started tomorrow.

## The short version

|  | Switch | Xbox |
|---|---|---|
| Graphics port | `VulkanPlatform` subclass in the fork; two pure virtuals; shaped like `VulkanPlatformWindows.cpp`'s 73 lines | Dawn onto D3D12 behind Filament's WebGPU backend; nothing written inside Filament; `WebGPUPlatformWindows.cpp`'s 95 lines is the shape |
| Front end | this host, `orbis_host_sdl.c` rewritten | the same |
| Game logic | QuickJS or linked C++; the scene layer is the real gap | the same |
| Measured here | the Vulkan path runs on lavapipe in a container | Dawn draws Orbis's scene on Metal; 25 of 26 materials compile for WebGPU |
| Biggest unknown | frame pacing and the memory budget | whether Dawn builds for the console at all |
| Needs no SDK | the scene layer; the shape of both back ends; `lit.mat` vs Tint | the same |
