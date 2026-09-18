# A console front end, running on a desktop

`native/headless` proves the renderer draws with nothing but a C program in
front of it. This is what that C program needs next to be a console front
end: **a window, a clock and a pad**, and a frame loop that drives the
renderer through the C ABI.

There is no Nintendo SDK here and no Microsoft GDK, so nothing in this
directory is a console port and none of it has run on a console. What it is
is the part of a console port that can be written, and tested, without one.

```
orblit_host_platform.h   the machine, behind eight calls
orblit_host_sdl.c        those eight, answered with SDL3 (macOS, Linux, Windows)
orblit_pad.h / .c        package:orblit_input's vocabulary and decisions, in C
orblit_pad_test.c        that they are still the same decisions
orblit_host.c            the frame loop, the scene, the camera and the walking
build.sh                macOS and Linux
CONSOLES.md             what a Switch port and an Xbox port actually are
```

`orblit_host_sdl.c` is the only file that knows what machine it is on. A port
to a console is that one file written again against an SDK that cannot be
discussed in public; `orblit_host.c`, `orblit_pad.c` and the whole renderer go
across untouched. That is the claim the directory exists to make, and the
reason the seam is drawn where `OrblitSurface.h` draws the renderer's own.

## Running it

The materials have to be compiled for the backend it will run, or Filament
starts, refuses every material as built for another backend and draws
nothing. `darwin/setup.sh` does Metal by default.

```sh
# macOS, Metal
packages/orblit_filament/darwin/setup.sh
packages/orblit_filament/native/host/build.sh run
```

A pad walks it: left stick to move, right stick to turn the camera, triggers
to pull the camera in and out, the bottom face button to hop, start to quit.
With no pad plugged in, WASD or the arrows, IJKL, Q and E, space and return
stand in — a desktop convenience a console back end has no counterpart to,
routed through `orblit_pad_raw` like everything else so it exercises the same
shaping, slots and edges rather than a second path that could rot.
`ORBLIT_HOST_VIRTUAL_PAD=1` attaches one of SDL's virtual gamepads and walks
its stick in a circle, which is how the pad path is tested on a machine with
no controller at all. `package:orblit_input` answers the same problem the same
way, with `tool/virtual_pad.dart`.

`build.sh test` builds and runs `orblit_pad_test.c` on its own — no Filament
SDK, no compiled materials, no SDL, no window, under a second. It is
`package:orblit_input`'s own `shaping_test.dart` and `pads_test.dart` written
against the C, and it exists because "the two drifting apart is a bug in
whichever moved" is not a claim anybody can check by reading. Writing it
found two places they already had. Every build runs it.

One thing worth knowing before blaming the frame loop: SDL ignores pad input
while the window is not focused, silently, with the pad still enumerated and
still reading whatever it held when focus went. That is the right default for
a desktop application and the wrong one for a host shaped like a console
front end, which has no unfocused window at all, so
`SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS` is set before `SDL_Init`.

Linux, in the container `tool/linux_container/Dockerfile` builds, with
`libsdl3-dev` added — Debian trixie has SDL 3.2.10, which is new enough:

```sh
docker run --rm -v "$PWD:/work" -v "<project>/.cache:/cache:ro" \
  orblit-linux:trixie bash -c '
    apt-get update && apt-get install -y libsdl3-dev
    cd /work/packages/orblit_filament/native/host
    ORBLIT_FILAMENT_SDK=/cache/filament-1.77.0/arm-linux/filament ./build.sh
    Xvfb :99 -screen 0 1280x800x24 & sleep 2
    DISPLAY=:99 ./build/orblit_host'
```

with the materials compiled for what it will run first —
`ORBLIT_MATC_BACKENDS="vulkan opengl" darwin/setup.sh`.

## Why SDL3, and what it costs a console later

SDL3 is a **system package** here: `brew install sdl3` on macOS,
`libsdl3-dev` on Debian, found with `pkg-config`. Not vendored and not a
submodule. Three reasons, and the third is the one that matters:

- **It is not on the link line of anything a console ships.** SDL answers
  `orblit_host_platform.h`'s eight calls on a desktop. A console answers the
  same eight with its own SDK. Vendoring a dependency is worth it when the
  dependency travels with the product; this one is scaffolding that gets
  taken down at exactly the platform it would have been vendored for.
- **A system package is the cheapest thing to be rid of.** There is no
  submodule to update, no third-party source in the tree to audit against a
  console vendor's redistribution rules, and no build of SDL to port — the
  cost of dropping it is deleting one file and one `pkg-config` line.
- **SDL3 names buttons by position.** `SDL_GAMEPAD_BUTTON_SOUTH`, not
  `_A`. That is `package:orblit_input`'s decision, arrived at independently,
  which is why `orblit_host_sdl.c`'s mapping table is sixteen entries and has
  no table of per-vendor lies in it.

What a system package costs: a machine without it cannot build the host, and
the two desktops are on different SDL versions (3.4.16 on this Mac, 3.2.10 in
the container), so anything newer than 3.2.0 is off limits. Nothing here
needs anything newer. If the host ever ships to players on a desktop rather
than serving as a test rig, that calculation changes and vendoring becomes
the right answer — but it is the wrong answer *for a console port*, which is
what this exists for.

The one genuine alternative is no dependency at all: Cocoa, X11 and Win32
directly, plus IOKit HID, evdev and XInput for pads. That is roughly the same
amount of code as `orblit_host_sdl.c` **per platform**, and three of them would
have to be kept working to test what a console back end will replace anyway.
GLFW was the other candidate and is out on pads: it has no rumble, no
hot-plug events worth the name, and its gamepad mapping is a bundled copy of
SDL's database.

## What runs, and on what

| | macOS 15, M4 Pro | Linux container, arm64 |
|---|---|---|
| Backend | Metal | Vulkan (lavapipe) |
| Size | 2560x1440 | 1280x720 |
| Rate | 60 fps (vsync), gpu 7.4 ms | 40–60 fps, gpu 13–26 ms |
| Pad | SDL virtual gamepad, slot 0 | the same |

Both draw the same scene: a ground, a ring of nine pillars rising and falling
on their own, and a block that walks.

## WebGPU over Dawn

`build.sh -W` in the fork says "NOT functional atm". On this Mac, as of
this branch, that is **out of date**: a fork built with
`-DFILAMENT_SUPPORTS_WEBGPU=ON` draws Orblit's scene through Dawn onto Metal,
headless and in a window, at 120 fps. `CONSOLES.md` has the measurement and
its three caveats — one material that will not compile, a feature level that
drops the scene to the slim surface, and a cold shader compile measured in
minutes.
