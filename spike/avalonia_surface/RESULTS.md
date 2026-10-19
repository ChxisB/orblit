# Render surface spike: results

**Throwaway.** Nothing here ships. Delete it in migrate task 20, or keep it as
documentation, by Chris's word.

Run on 2026-10-19. Task: `.claude/.planning/migrate/tasks/01-render-surface-spike.md`.

## Answer

On macOS, path C (shared texture) works and wins. A frame reaches Avalonia with
no pixel copied by the CPU. The Avalonia overlay and popups draw over it. It
held 100 fps for 3000 frames with no drop.

Path C needs a recipe that is not in Avalonia's docs. See "The macOS recipe".

Windows and Linux were not run. This Mac cannot run them. Today the portable
renderer build supports only the headless and window surfaces, so those two OSes
have paths A and B and no path C until a platform surface is written for them.

## Machine

| | |
| --- | --- |
| Hardware | Apple M4 Pro |
| OS | macOS, Darwin 27.0.0 |
| Displays | 3440x1440 at 100 Hz (main), built-in 3024x1964 |
| Avalonia | 12.1.4 (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`) |
| .NET | SDK 10.0.302, `net10.0` |
| Renderer | the Filament-backed C ABI, built from `packages/orblit_filament` |
| Scene | the headless sample: a sun, a ground and five blocks, camera orbiting |

The renderer was built twice. The apple variant has the IOSurface surface. The
portable variant is headless and window only.

## The three paths

| | A. Readback | B. Native child | C. Shared texture |
| --- | --- | --- | --- |
| Surface kind | headless, `read_capture` | window, `CAMetalLayer` in an `NSView` | platform, IOSurface pixel buffers |
| Avalonia side | `WriteableBitmap` | `NativeControlHost` | `ICompositionGpuInterop` |
| Copies per frame | 3 by design: GPU to CPU in the renderer, into the bitmap, bitmap to GPU | 0 | 0 |
| Frame lag | 1 to 2 frames late | none | none |

## Numbers

macOS only. Each row is 300 frames after a warm-up, with a small Avalonia
element redrawn every tick (`SPIKE_DIRTY=1`). The display runs at 100 Hz, so
100 fps is the ceiling. Callback is the time the draw call took on the UI
thread. CPU is process CPU as a percentage of one core.

| Case | Path | Callback p50 (p99) ms | fps | CPU % | Dropped |
| --- | --- | --- | --- | --- | --- |
| 1080p, one view | A | 6.1 (6.7) | 100 | 68 | 0 |
| | B | 7.0 (9.3) | 99.8 | 37 | 0 |
| | C | 3.3 (4.1) | 99.5 | 47 | 0 |
| 3840x1254, one view | A | 11.1 (11.8) | 52 | 57 | 0 |
| | B | 5.5 (6.4) | 100 | 42 | 0 |
| | C | 5.5 (6.8) | 100 | 53 | 0 |
| Two views, 1600x900 each | A | 8.6 (9.4) | 95 | 75 | 0 |
| | B | 5.0 (7.1) | 99.9 | 51 | 0 |
| | C | 5.1 (7.2) | 99.9 | 61 | 0 |
| Soak, 3000 frames, 1080p | C | 3.6 (4.2) | 99.9 | 49 | 0 |

- **4K is not run.** The 3840x2160 request drew at 3840x1254, because the window
  was clamped to the screen height. No true 4K cell exists on this display.
- **Resize** from 1920x1080 to 1440x810 settled in one tick on all three paths
  (A 16.9 ms, B 6.3 ms, C 19.5 ms to the first frame at the new size).
- **Two viewports** worked on all three. Each had its own renderer.
- Path A at 3840x1254 misses the 100 Hz tick, so its 52 fps is a real limit.

## Overlay and popup

| | A | B | C |
| --- | --- | --- | --- |
| Avalonia control drawn over the viewport | shows | **hidden** | shows |
| Popup over the viewport | shows | shows | shows |

A native child sits above everything Avalonia draws in the window. A popup is a
window of its own on macOS, so it still shows. Any in-tree overlay is lost on
path B: a toolbar, a gizmo, a drop shadow, a selection rectangle. For an editor
viewport that is the deciding fact against B.

Screenshots are in `results/shots/`.

## The macOS recipe for path C

Avalonia 12.1.4 on macOS offers image type `IOSurfaceRef` and semaphore type
`MetalSharedEvent`. The plan's note that macOS has only shared OpenGL is wrong
for this version.

1. Get `ICompositionGpuInterop` from `compositor.TryGetCompositionGpuInterop()`.
2. Do not use `UpdateAsync`. Automatic sync throws `NotSupportedException` on
   Metal. The viewport stayed black until the fault was read.
3. Make two `MTLSharedEvent` objects. Import each with `ImportSemaphore` and a
   `PlatformHandle` of type `MetalSharedEvent`. One says "rendered". One says
   "released".
4. Per frame: draw, take `copy_presented`, signal "rendered" with the frame
   number, then call `UpdateWithTimelineSemaphoresAsync(image, rendered, n,
   released, n)`.
5. Set `TopLeftOrigin = true` in the image properties, or the frame is upside
   down.
6. Import each IOSurface once. Cache by `IOSurfaceGetID`.
7. Allow two frames in flight. Gate on the released event's value.

### Traps

- **One frame in flight is unstable.** The frame is released just after the next
  tick, so the tick is dropped. Avalonia then has nothing new to draw and falls
  from the 100 Hz display rate to its 60 Hz idle rate. It stays there, with over
  half of all ticks dropped. The first matrix landed in the good state by luck.
  Two or three frames in flight were stable in every run.
- **The renderer rings four buffers on Apple.** Up to three in flight cannot
  reuse a buffer the compositor still holds. The limit is checked in one place,
  and no pixel test for tearing was made.
- **`RequestAnimationFrame` follows demand.** With nothing dirty in Avalonia it
  ticks at about 61 Hz. Path B ticked at 50 Hz until a small element was
  redrawn each tick. The 45 to 50 fps first seen for B was this, not its cost.
  A real editor always has something live, but a frame loop must not assume the
  display rate.
- **The colours look washed out.** All three paths show the same image. It is
  the renderer's own output with this exposure, and not a surface fault.

## What did not run

- **Windows:** not run, no machine. Path C there would use a D3D11 shared
  handle. The portable renderer has no platform surface, so it would need new
  native code first.
- **Linux:** not run, no machine. Path C would use Vulkan or dma-buf, with the
  same missing surface.
- **True 4K:** not run, the display is not tall enough.
- **A display other than 100 Hz:** not run. The in-flight finding says the
  behaviour depends on the tick rate, so 60 Hz and 144 Hz need a run.
- **A render scale other than 1.0:** not run.
- **Tearing:** not checked by pixel test or by eye over a long run.

## Per OS

| OS | Use | Why |
| --- | --- | --- |
| macOS | C, shared texture | Fastest callback, no copy, overlays show. Proven here. |
| Windows | not decided | A runs today. B hides overlays. C needs a D3D11 surface and a run. |
| Linux | not decided | A runs today. B hides overlays. C needs a Vulkan or dma-buf surface and a run. |

Path A works on every OS the renderer builds on, so it is the fallback.

## Reproduce

`native/build.sh apple` builds the renderer and the harness's view helper.
`native/build.sh portable` builds the portable renderer. Then:

```
cd app && dotnet build -c Release
../matrix.sh
```

`run.sh <A|B|C> <WxH> <views> [--frames n --resize --popup --shot file]` runs one
case. Rows append to `results/runs.jsonl`. `results/runs-first-matrix-superseded.jsonl`
is the first matrix, run with one frame in flight and without `SPIKE_DIRTY`. It
is kept only as the record of the trap above.
