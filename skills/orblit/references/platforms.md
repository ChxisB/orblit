# Platforms: setup and what has been seen to work

From the docs at https://orblitengine.com/docs/start/installing-dotnet/ and the
scripts under `packages/orblit_filament/`, as of November 2026. "Draws" means
somebody has looked at a frame, or a check has measured one, on that platform.
Say plainly to a user when their platform is one that hasn't.

## Every machine

- The .NET 10 SDK, with Avalonia 12.1.4 for the window.
- Git, CMake and a C++ compiler (Xcode on a Mac, Visual Studio on Windows,
  clang with libc++ on Linux).
- bash, curl and tar. On Windows that is the bash Git for Windows installs.
- A network for the first build: a pinned Filament release (v1.77.0), about
  74 MB on a Mac, 53 MB on Linux and 810 MB on Windows, plus about 1.4 MB of
  lookup tables.

The packages are not on nuget.org. The engine packs them into a local feed,
`~/.orblit/feed` or `$ORBLIT_FEED`. The steps are in
[dotnet.md](dotnet.md#setting-up).

The first build per platform downloads Filament into `third_party/` and
compiles the materials into the native library. Both are idempotent and
neither is in git.

## Which machine builds what

Filament's material compiler, `matc`, has to run on the build machine.
`tool/build_native.sh` reads the machine and builds for its runtime id:
`osx-arm64`, `linux-x64`, `linux-arm64` or `win-x64`.

| Machine | Can build |
| --- | --- |
| Mac, Apple silicon | macOS |
| Linux x86_64 and arm64 | Linux |
| Windows x64 | Windows |
| Mac, Intel | Nothing that draws |
| Windows on Arm | Nothing that draws |

`build_native.sh` builds only the desktop runtime ids. iOS, Android and the
web have no runtime id yet, because the first targets are macOS, Windows and
Linux. The packages that do not draw (simulation, geometry, agents, 2D, networking)
run anywhere .NET does, Intel Macs included.

## macOS

- Needs an Apple silicon Mac and Xcode with its command line tools.
- Run `packages/orblit_filament/darwin/setup.sh`, then
  `tool/build_native.sh`. It downloads about 74 MB (about 290 MB on disk). Run
  `setup.sh` again after a Filament version bump. Set-up copies are skipped in
  under a second.
- The renderer is built only when `ORBLIT_FILAMENT_SDK` or the default path
  holds a Filament release. Otherwise `build_native.sh` prints `skip
  orblit_filament`, and a package packed then carries no renderer.
- A sandboxed macOS app can't read arbitrary absolute paths, so a model file
  draws as a cube. Hand bytes over with `RenderResources.Provide(name, bytes)`,
  or turn the sandbox off while developing.
- **State:** macOS is the reference. It is the only platform where the .NET
  renderer has drawn, and the only one CI sets the renderer up on.

## Linux

- x86_64 or arm64, glibc 2.38 or newer (Debian 13 and Ubuntu 24.04 are new
  enough, Debian 12 and Ubuntu 22.04 aren't), clang with libc++ (not GCC or
  libstdc++), and a display (Xvfb works).
- `packages/orblit_filament/linux/setup.sh` fetches the release for this
  machine's architecture and compiles the materials for `opengl` and `vulkan`.
  Run `darwin/setup.sh` again before a macOS build, because both write the
  same `generated/` folder.
- **State:** the managed packages build and test in CI on Linux. The renderer
  library is not built there, and no frame has drawn from C# on Linux.

## Windows

- Windows 10 or 11 on x64, Visual Studio 2022 or newer with "Desktop
  development with C++", and Git for Windows with its `bash` on `PATH`. Git's
  installer usually only adds `Git\cmd`. Add `C:\Program Files\Git\bin`, and
  make sure WSL's `bash.exe` isn't first.
- The first setup downloads about 810 MB.
- The engine's `.gitattributes` keeps `dotnet/**` on LF, because the tests
  compare bytes and Windows checks out CRLF by default.
- **State: the managed packages build and test in CI, and no frame has ever
  been drawn on Windows.** Windows on Arm can't build the renderer at all.

## iOS, Android and the web

Not in this build. The native code keeps its iOS, Android and WebGL 2 paths
(`packages/orblit_filament/native/web`, `native/webgpu`), but no .NET package
loads them and no runtime id exists. Say so to a user who asks, and do not
promise a date.

## Newer features, platform by platform

Everything the renderer does has been seen to draw on macOS only: sprites,
Gaussian splats, models (clips, skins, variants, FBX, OBJ), textures (cooked
sets and the queue), and feeds. None has been run on Linux or Windows.
