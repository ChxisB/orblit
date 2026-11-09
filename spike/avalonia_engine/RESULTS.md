# Avalonia hosts the engine: results

Go for decision 181 (a), route A, with three conditions. A C# host loads `libgodot`, drives the loop, reads editor services with no window and shows the engine's frames in an Avalonia control. The conditions are below.

1. Patch the macOS NSApplication takeover. Without it the host must delay engine creation until Avalonia has finished launching.
2. Build a zero-copy frame path before Retina-sized viewports at 120 Hz.
3. Test input forwarding by hand. This machine could not drive it.

All numbers are macOS arm64, Apple M4 Pro, Metal 3.2, one run each, a display that ticks at 8.33 ms. The windowed runs used `libgodot.macos.editor.arm64.dylib`, the editor target, started as a game with no `--editor`. Windows and Linux were not tried.

## What was built

| Path | State |
| --- | --- |
| 1. Shared IOSurface imported by the Avalonia compositor | Not built. Needs an engine patch. |
| 2. Embedded display server with a native layer | Not built. Known to hide Avalonia overlays. |
| 3. CPU readback into a `WriteableBitmap` | Built and measured. This is the floor. |

Path 3 reads the root viewport texture through typed `ptrcall`, copies the pixels to host memory and uploads them. It needs no engine patch.

## Frame cost per path

One scene, `spike/scenes/cube_field`. Every run changed pixels on all 280 timed frames.

| Viewport | Iterate ms | Readback ms | Upload ms | Total ms | Frame interval ms | Working set MB |
| --- | --- | --- | --- | --- | --- | --- |
| Default window, size not logged | 0.42 | 1.42 | 0.18 | 2.02 | 8.33 | 292 |
| 960x540 | 0.39 | 2.58 | 0.15 | 3.12 | 8.33 | 291 |
| 1920x1080 | 1.47 | 4.69 | 0.35 | 6.51 | 8.36 | 337 |
| 2880x1640 | 0.45 | 9.94 | 0.88 | 11.27 | 16.67 | 334 |

Figures are averages. The 960x540 row is the quiet run. A second 960x540 run read back in 1.95 ms, so the spread at small sizes is about 1 ms. I did not repeat runs to tighten it.

Reading the table:

- Readback is the cost. It grows with pixel count, from about 1.4 ms to 10 ms.
- 1080p fits the 8.33 ms budget at 120 Hz with about 1.8 ms to spare.
- 2880x1640 does not. The frame interval fell to 16.67 ms, so the host ran at 60 Hz.
- The asked size was 2880x1800. The window came back 1640 tall.
- Iterate time does not follow size. I did not look into why.
- Working set is the whole process, Avalonia and the .NET runtime included. No per-path memory was isolated.

## Resize

The host calls `Window.set_size` on the engine. The new width reached the readback in 1 frame, 10 to 12 ms from the call. I checked the saved frames for tearing by eye. I did not film a drag.

## Editor services with no window

They run. The check starts the editor library with `--headless --editor`, waits for the filesystem scan, opens `res://main.oscn`, reads a node's class, moves a `Node3D`, saves, and then reads the saved `.oscn` to find the new transform.

```bash
cd orblit/spike/avalonia_engine
dotnet build -c Release
dotnet bin/Release/net10.0/AvaloniaEngine.dll --editor-check \
  ../../bin/libgodot.macos.editor.arm64.dylib ../scenes/cube_field
```

It exits 0. Start took 1053 ms. The working set went from 41 MB before the engine to 803 MB with the editor and one small scene open. That is about 760 MB for the C++ editor nodes in memory. It is the first number for task 17.

A second engine in the same process is refused. `libgodot_create_godot_instance` returns null with "Only one Godot Instance may be created." and the first instance keeps running. The editor and a game preview need two processes.

## The macOS NSApplication takeover

This is the main finding. The engine's `OS_MacOS_NSApp` constructor replaces NSApp's delegate, sets the activation policy and sets a main menu. The delegate runs `Main::setup` a second time when the host's launch finishes. If the host creates the engine before launch ends, setup runs twice and the process hangs.

The spike works around it. `SpikeWindow` creates the engine one second after `Opened`. That fixes the hang. It does not remove the takeover. The engine still owns Avalonia's delegate, and four accessibility getters in `display_server_macos_base.mm` cast it to `GodotApplicationDelegate`.

## Background testing

The engine opens its own Metal window and activates the app, so pixel runs come to the front. These did not stop it:

- `ShowActivated = false` on the Avalonia window.
- `display/window/size/no_focus=true` in the project.
- Parking the engine window off screen. macOS clamps it, so a sliver stays visible.

With all three set, `lsappinfo front` showed the spike in front for 19 of 22 samples. The headless checks never open a window. Only pixel runs do. The fix is the offscreen patch below.

## Engine patches needed

None were made. The sizes are estimates from reading the code.

| File | Change | Size |
| --- | --- | --- |
| `platform/macos/os_macos.mm` | When hosted, keep the host's delegate, skip the menu and skip the activation policy. Keep the engine's own delegate in a member. | small |
| `platform/macos/display_server_macos_base.mm` | The four accessibility getters read the engine's own delegate, not `[NSApp delegate]`. | small |
| `platform/macos/libgodot_macos.mm` | Take a hosted flag from the create call. | small |
| `platform/macos/display_server_macos.mm` or `display_server_macos_embedded.mm` | An offscreen mode with no `NSWindow` and no `activateApplication`. | medium |
| `drivers/metal/` | Export an IOSurface-backed render target for path 1. | medium to large |

The first three are needed for any host. The fourth fixes background testing. The fifth removes the readback cost.

## Build facts

| Library | Time | Size | Notes |
| --- | --- | --- | --- |
| `libgodot.macos.template_debug.arm64.dylib` | 226 s | 52.7 MB | Refuses `--path` unless built with `disable_path_overrides=no`. |
| `libgodot.macos.editor.arm64.dylib` | 299 s | 110 MB | Accepts `--path`. |

Both need `vulkan=no` here, or the build stops on a missing MoltenVK SDK. Do not run two scons jobs in the tree at once.

## Not tested

- Mouse and key input into the viewport. No Accessibility grant here. Chris checks by hand.
- Windows and Linux. The old renderer has no shared texture path there.
- Paths 1 and 2.
- Memory per path.
- A drag resize under load.
- A release export template. Every windowed number is from the editor library, which carries editor code a shipped game would not.

## Shape of the bridge for task 10

- A C# host owns the engine through an `Engine` class. It creates, starts, iterates and destroys `libgodot`, one instance per process.
- Typed calls are generated from `extension_api.json`. The spike hand-wrote about twenty hashes. They will drift on every engine upgrade.
- An `IFrameSource` interface sits next to the Avalonia control that calls it. The readback source ships first. A shared-texture source replaces it later with no control changes.
- `RequestAnimationFrame` on the UI thread pumps `iteration()` and then asks the frame source for pixels.
- Input goes through the engine's `Input` singleton, called from the control's pointer and key handlers.
- The editor runs as its own process in `--editor` mode. A game preview is a second process. The panels talk to the editor through the typed calls above.
