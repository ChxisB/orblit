# Platforms: setup and what has been seen to work

From the docs at https://orblitengine.com/start/installing/ and the pages under
`/start/setup/`, as of September 2026. "Draws" means somebody has looked at a
frame, or a check has measured one, on that platform. Say plainly to a user
when their platform is one that hasn't.

## Every machine

- Flutter 3.47.0 or newer, stable channel. Dart 3.10.0 or newer.
- Git, and bash, curl and tar (on Windows, the bash Git for Windows installs).
- A network for the first build: a pinned Filament release (v1.77.0) of 51 MB
  to 810 MB depending on the platform, plus about 1.4 MB of lookup tables.
- `vector_math: ^2.1.4` as a direct dependency.

The first build per platform downloads Filament into `third_party/` and
compiles the materials into the binary. Both are idempotent and neither is in
git.

Smoke test: `MaterialApp(home: Scaffold(body: OrblitView()))` draws the
default scene. `flutter run -d macos` (or `linux`, `windows`, `chrome`); a
phone or simulator goes by the id `flutter devices` prints, since `-d ios`
and `-d android` match nothing.

## Which machine builds what

Filament's material compiler, `matc`, has to run on the build machine.

| Machine | Can build |
| --- | --- |
| Mac, Apple silicon | macOS, iOS, Android, the web |
| Linux x86_64 | Linux, Android |
| Linux arm64 | Linux |
| Windows x64 | Windows |
| Mac, Intel | Nothing that draws |
| Windows on Arm | Nothing that draws |

The plain-Dart packages (simulation, geometry, agents, 2D, networking) run
anywhere Dart does, Intel Macs included.

## macOS and iOS

- Needs an Apple silicon Mac, Xcode with its command line tools (and the iOS
  platform and a simulator for iOS). Targets macOS 10.15 and iOS 13, Flutter's
  defaults.
- After `flutter pub get`, run:

  ```sh
  for setup in "${PUB_CACHE:-$HOME/.pub-cache}"/git/orblit-*/packages/orblit_filament/darwin/setup.sh; do
    bash "$setup"
  done
  ```

  Pub keeps each git commit in its own cache directory, which is why the loop
  searches. It downloads about 74 MB (about 290 MB on disk). Run it again after
  every `pub upgrade` that moves the engine; set-up copies are skipped in
  under a second.
- `error: When building for macOS, the expected library
  …/Filament.xcframework/macos-arm64/macos.a was not found` means the loop
  hasn't run for this commit.
- Under CocoaPods the podspec runs the setup itself on `pod install`, but a
  path dependency on a clone that gained new materials after a `git pull`
  fails with `'generated/<name>_material.h' file not found`; run
  `packages/orblit_filament/darwin/setup.sh` in that clone. Switching between
  Swift Package Manager and CocoaPods needs `flutter clean`.
- A sandboxed macOS app (Flutter's default) can't read arbitrary absolute
  paths, so model files draw as cubes. Hand bytes over with
  `OrblitResources.provide`, or turn the sandbox off in
  `macos/Runner/DebugProfile.entitlements` while developing.
- **State:** macOS is the reference; CI draws a frame on every change. iOS
  draws on the simulator, with a slimmer lit surface (Filament feature level
  2: no rectangular-light shadows, no irradiance field). A real iPhone (A13 or
  later reports level 3) hasn't been run.

## Android

- Build on an Apple silicon Mac (run the macOS loop above first; without it
  the build ends `run ../darwin/setup.sh first`) or on Linux x86_64. Not on
  Linux arm64 or Windows.
- Needs the SDK for API 36, NDK 28.2.13676358, CMake 3.22.1, JDK 17 or newer,
  and `xxd`. Apps can target API 24 and up.

  ```sh
  yes | "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" --install "ndk;28.2.13676358" "cmake;3.22.1"
  ```

- Built for `arm64-v8a` only: a phone, or an arm64 emulator image (an Apple
  silicon Mac's emulator is arm64). x86_64 emulator images can't load it.
- Gradle runs the setup; the first build downloads 58 MB (plus 52 MB on Linux
  for its `matc`).
- **State:** draws on a Galaxy S24+ (Android 16, Vulkan) and on the emulator.
  CI builds the APK but doesn't run it.

## Linux

- x86_64 or arm64, glibc 2.38 or newer (Debian 13 and Ubuntu 24.04 are new
  enough; Debian 12 and Ubuntu 22.04 aren't), clang with libc++ (not GCC or
  libstdc++), Flutter's Linux toolchain and GTK 3 headers, and a display (Xvfb
  works).
- CMake runs the setup when Flutter configures the build. `flutter run -d linux`.
- The engine carries a Debian 13 container (`tool/linux_container`); keep the
  pub cache in a Docker volume, and run `flutter pub get` on the host again
  afterwards.
- **State:** has drawn only on arm64, in a Debian 13 container on an Apple
  silicon Mac, with Mesa's software rasterisers. No real GPU and no x86_64
  machine has run it. No Linux job in CI.

## Windows

- Windows 10 or 11 on x64, Visual Studio 2022 or newer with "Desktop
  development with C++", Git for Windows with its `bash` on `PATH` (Git's
  installer usually only adds `Git\cmd`; add `C:\Program Files\Git\bin`, and
  make sure WSL's `bash.exe` isn't first), and Developer Mode. `flutter clean`
  after fixing `PATH` if CMake already found the wrong bash.
- The first configure downloads about 810 MB.
- **State: it builds, and no frame has ever been drawn on Windows.** CI
  compiles and bundles it; its runner can't create a Vulkan instance, so the
  frame check times out. Nobody has run it on a real machine. Windows on Arm
  can't build the renderer at all.

## The web

The most manual platform, and Mac-only for now because one step goes through
the Mac setup.

1. Emscripten exactly 5.0.4.
2. Build Orblit's Filament fork (`https://github.com/ChxisB/orblit-filament`)
   with `./build.sh -p wasm release`, on a path with no spaces.
3. Clone the engine at the `resolved-ref` from the app's `pubspec.lock`.
4. Compile the WebGL 2 materials with the Mac setup, pointing `ORBLIT_MATC` at
   **the fork's `matc`**, never the release's: the release's builds materials
   in which every directional light contributes nothing, with no error.
5. Build the renderer (`packages/orblit_filament/native/web/build.sh` with
   `ORBLIT_FILAMENT_WASM_SRC` set).
6. Copy `orblit_renderer.js` and `orblit_renderer.wasm` into the app's `web/`
   and load the script in `web/index.html` before `flutter_bootstrap.js`.

Repeat steps 3 to 6 whenever the app's engine commit changes. The exact
commands are at https://orblitengine.com/start/setup/web/.

- **State:** Chrome only (headless with SwiftShader, and in real time for
  textures). Safari and Firefox haven't been run. WebGL 2 at feature level 1,
  so the slimmer surface.

## Newer features, platform by platform

| Feature | Seen on |
| --- | --- |
| `OrblitResources` (bytes by name) | macOS, Chrome, iOS simulator, Android emulator |
| Sprites | macOS, Chrome |
| Gaussian splats | macOS, Chrome |
| Models (clips, skins, variants, FBX, OBJ) | macOS, Chrome, iOS simulator, Android emulator |
| Textures (cooked sets, queue, `fromImage`) | macOS, Chrome, iOS simulator, Android emulator |

None of these has been run on Linux, Windows, Safari or Firefox.
