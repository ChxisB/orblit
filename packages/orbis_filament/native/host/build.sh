#!/bin/bash
# Builds the windowed host: the portable renderer core, the C ABI, and three
# C files on top of it.
#
# The same argument ../headless/build.sh makes, one step further on. That one
# proves the renderer runs with nothing but a C program in front of it; this
# one gives that C program a window, a clock and a pad, which is what a
# console front end is. What is left out is still the point: no Objective-C,
# no Swift, no Dart, no Flutter.
#
# One script for macOS and Linux rather than two, which ../headless has,
# because SDL3 is what made the difference between them small: a different
# SDK layout, frameworks against -lpthread, and --start-group. The three
# differences are in three `case` blocks below and nowhere else.
#
#   build.sh          builds ./build/orbis_host
#   build.sh run      and runs it
#
#   ORBIS_FILAMENT_SDK   a Filament release (the directory holding include/
#                        and lib/). Defaults to the one darwin/setup.sh
#                        staged; required on Linux.
#   ORBIS_FILAMENT_ARCH  which slice of lib/ to link. Defaults to arm64 on
#                        macOS and `uname -m` on Linux.
#   ORBIS_BUILD_DIR      where the objects and the program go (default: build)
#   ORBIS_SDL_CFLAGS     )  how to find SDL3, when pkg-config cannot. Both
#   ORBIS_SDL_LIBS       )  are asked of pkg-config otherwise.
#
# The materials must have been compiled for the backend this will run:
# darwin/setup.sh does Metal by default, and ORBIS_MATC_BACKENDS names others
# ("vulkan opengl" for Linux, "webgpu" for Dawn). Without that Filament
# starts, refuses every material as built for another backend, and draws
# nothing. A missing generated/<name>_material.h means setup.sh has not been
# run at all.
set -euo pipefail
cd "$(dirname "$0")"

DARWIN=../../darwin
SRC="$DARWIN/orbis_filament/Sources/orbis_filament_native"
OUT="${ORBIS_BUILD_DIR:-build}"
SYSTEM="$(uname -s)"

case "$SYSTEM" in
  Darwin)
    SDK="${ORBIS_FILAMENT_SDK:-$DARWIN/third_party/filament-mac/filament}"
    ARCH="${ORBIS_FILAMENT_ARCH:-arm64}"
    # Filament's own: its Metal and OpenGL backends are what need these on a
    # Mac. SDL brings Cocoa in as well and asking twice costs nothing.
    PLATFORM_LINK=(-framework Cocoa -framework Metal -framework QuartzCore
                   -framework CoreVideo -framework IOSurface
                   -framework OpenGL)
    STDLIB=()
    GROUP_OPEN=()
    GROUP_CLOSE=()
    ;;
  Linux)
    SDK="${ORBIS_FILAMENT_SDK:?ORBIS_FILAMENT_SDK must point at a Linux Filament release}"
    ARCH="${ORBIS_FILAMENT_ARCH:-$(uname -m)}"
    PLATFORM_LINK=(-lpthread -ldl -lm)
    # libc++, not libstdc++, on the compile line as well as the link one:
    # Filament's Linux release is built against LLVM's standard library and
    # its archives name symbols in namespace std::__1 that libstdc++ has none
    # of. ../headless/build_linux.sh found this; linux/CMakeLists.txt carries
    # the same two flags for the same reason.
    STDLIB=(-stdlib=libc++)
    # Filament's archives refer to each other both ways round and ld reads an
    # archive once unless told otherwise.
    GROUP_OPEN=(-Wl,--start-group)
    GROUP_CLOSE=(-Wl,--end-group)
    ;;
  *)
    echo "build.sh does not know $SYSTEM. macOS and Linux are what it has"
    echo "been run on; Windows wants its own, or MSYS."
    exit 1
    ;;
esac

if [ ! -d "$SDK/lib/$ARCH" ]; then
  echo "no $ARCH slice in $SDK/lib; it has: $(ls "$SDK/lib" 2>/dev/null)"
  exit 1
fi
if [ ! -f "$SRC/generated/lit_opaque_material.h" ]; then
  echo "no compiled materials in $SRC/generated."
  echo "run packages/orbis_filament/darwin/setup.sh first."
  exit 1
fi

SDL_CFLAGS="${ORBIS_SDL_CFLAGS:-$(pkg-config --cflags sdl3)}"
SDL_LIBS="${ORBIS_SDL_LIBS:-$(pkg-config --libs sdl3)}"

mkdir -p "$OUT"

# Every plain C++ file beside the renderer, whatever it is called, exactly as
# ../headless/build.sh takes them: a helper added later is picked up rather
# than forgotten. ORBIS_PLATFORM_PORTABLE is the set of files a Linux,
# Android, Windows or console build takes -- OrbisPlatformApple.mm,
# OrbisSurfaceApple.mm and the Objective-C wrapper are not compiled at all.
objects=()
for source in "$SRC"/*.cpp; do
  name="$(basename "$source" .cpp)"
  clang++ -std=c++17 -O2 -DORBIS_PLATFORM_PORTABLE "${STDLIB[@]+"${STDLIB[@]}"}" \
    -Wall -Wno-deprecated-declarations -Wno-unused-private-field \
    -I "$SDK/include" -I "$SRC" -I "$SRC/include" \
    -c "$source" -o "$OUT/$name.o"
  objects+=("$OUT/$name.o")
done

# ../headless/build.sh's list. bluegl and bluevk are the runtime loaders for
# GL and Vulkan; webgpu_dawn is only in a fork built with
# -DFILAMENT_SUPPORTS_WEBGPU=ON and is linked when it is there.
LIBS=(filament backend filabridge filaflat utils geometry smol-v ibl image
      abseil zstd filament-iblprefilter gltfio_core uberarchive uberzlib
      dracodec meshoptimizer ktxreader stb basis_transcoder mikktspace
      bluegl bluevk)
archives=()
for lib in "${LIBS[@]}"; do archives+=("$SDK/lib/$ARCH/lib$lib.a"); done
if [ -f "$SDK/lib/$ARCH/libwebgpu_dawn.a" ]; then
  archives+=("$SDK/lib/$ARCH/libwebgpu_dawn.a")
  # Dawn's own, on a Mac: its Metal back end reads the GPU's registry entry
  # to name the adapter, which is IOKit and nothing Filament asks for.
  if [ "$SYSTEM" = "Darwin" ]; then PLATFORM_LINK+=(-framework IOKit); fi
fi

# C99 and pedantic, so anything C++ that creeps into orbis_renderer.h or
# orbis_pad.h is an error here rather than a surprise in a console
# toolchain's C compiler later. SDL's own headers are C.
# shellcheck disable=SC2086 -- pkg-config's flags are meant to split.
for source in orbis_host orbis_host_sdl orbis_pad; do
  clang -std=c99 -Wall -Wextra -Werror -pedantic \
    -I "$SRC/include" -I . $SDL_CFLAGS \
    -c "$source.c" -o "$OUT/$source.o"
done

# shellcheck disable=SC2086
clang++ "$OUT/orbis_host.o" "$OUT/orbis_host_sdl.o" "$OUT/orbis_pad.o" \
  "${objects[@]}" "${STDLIB[@]+"${STDLIB[@]}"}" \
  "${GROUP_OPEN[@]+"${GROUP_OPEN[@]}"}" "${archives[@]}" \
  "${GROUP_CLOSE[@]+"${GROUP_CLOSE[@]}"}" \
  $SDL_LIBS "${PLATFORM_LINK[@]}" -o "$OUT/orbis_host"

echo "built $OUT/orbis_host"

if [ "${1:-}" = "run" ]; then
  shift
  "$OUT/orbis_host" "$@"
fi
