#!/bin/bash
# Builds the renderer as plain C++ with no Objective-C in it, and two C
# programs on top of it: the C ABI's test and a headless host.
#
# The point is what is left out. The core, the C ABI, the portable half of
# the platform layer and the plain C++ helpers are compiled with
# ORBLIT_PLATFORM_PORTABLE — the set of files a Linux, Android or Windows
# build takes. OrblitPlatformApple.mm, OrblitSurfaceApple.mm and the
# Objective-C wrapper are not compiled at all, and the two programs include
# one header, orblit_renderer.h. The frameworks on the link line are
# Filament's: its Metal and OpenGL backends are what need them on a Mac. A
# WebGPU build adds Dawn's native archive through ORBLIT_FILAMENT_BACKEND;
# that archive belongs to the same fork/runtime as the headers and materials.
#
#   build.sh            builds both into ./build
#   build.sh test       and runs the ABI's test
set -euo pipefail
cd "$(dirname "$0")"

DARWIN=../../darwin
SRC="$DARWIN/orblit_filament/Sources/orblit_filament_native"
SDK="${ORBLIT_FILAMENT_SDK:-$DARWIN/third_party/filament-mac/filament}"
OUT="${ORBLIT_BUILD_DIR:-build}"
if [ -n "${ORBLIT_GENERATED_SET:-}" ]; then
  GENERATED_SET="$ORBLIT_GENERATED_SET"
elif [ -n "${ORBLIT_FILAMENT_SRC:-}" ]; then
  GENERATED_SET="darwin-source"
else
  GENERATED_SET="darwin-release"
fi
case "$GENERATED_SET" in
  ''|.|..|*/*)
    echo "native/headless/build.sh: ORBLIT_GENERATED_SET must be a simple directory name" >&2
    exit 1
    ;;
esac
GENERATED="$SRC/generated/$GENERATED_SET"
mkdir -p "$OUT"

if [ ! -f "$GENERATED/lit_opaque_material.h" ]; then
  echo "native/headless/build.sh: no compiled materials at $GENERATED" >&2
  echo "  run darwin/setup.sh or set ORBLIT_GENERATED_SET to an existing set" >&2
  exit 1
fi

# Every plain C++ file beside the renderer, whatever it is called: a helper
# added later (a new post effect, say) is picked up rather than forgotten.
objects=()
for source in "$SRC"/*.cpp; do
  name="$(basename "$source" .cpp)"
  clang++ -std=c++17 -O2 -DORBLIT_PLATFORM_PORTABLE \
    -Wall -Wno-deprecated-declarations -Wno-unused-private-field \
    -I "$SDK/include" -I "$SRC" -I "$SRC/include" -I "$GENERATED" \
    -c "$source" -o "$OUT/$name.o"
  objects+=("$OUT/$name.o")
done

LIBS=(filament backend filabridge filaflat utils geometry smol-v ibl image
      abseil zstd filament-iblprefilter gltfio_core uberarchive uberzlib
      dracodec meshoptimizer ktxreader stb basis_transcoder mikktspace
      bluegl bluevk)
if [ "${ORBLIT_FILAMENT_BACKEND:-}" = "webgpu" ]; then
  if [ ! -f "$SDK/lib/arm64/libwebgpu_dawn.a" ]; then
    echo "native/headless/build.sh: WebGPU runtime has no libwebgpu_dawn.a" >&2
    echo "  use the matching fork's installed webgpu-release/filament SDK" >&2
    exit 1
  fi
  LIBS+=(webgpu_dawn)
fi
archives=()
for lib in "${LIBS[@]}"; do archives+=("$SDK/lib/arm64/lib$lib.a"); done
# Dawn, which is only in an ORBLIT_FILAMENT_SDK built from the fork with
# -DFILAMENT_SUPPORTS_WEBGPU=ON. Filament's own release does not carry it and
# does not refer to it, so this is a no-op there and the two need no flag to
# tell them apart.
if [ -f "$SDK/lib/arm64/libwebgpu_dawn.a" ] &&
   [ "${ORBLIT_FILAMENT_BACKEND:-}" != "webgpu" ]; then
  archives+=("$SDK/lib/arm64/libwebgpu_dawn.a")
fi
FRAMEWORKS=(-framework Cocoa -framework Metal -framework QuartzCore
            -framework CoreVideo -framework IOSurface -framework OpenGL)
# Dawn's own: its Metal back end reads the GPU's registry entry to name the
# adapter, which is IOKit and nothing Filament itself ever asks for.
if [ -f "$SDK/lib/arm64/libwebgpu_dawn.a" ]; then
  FRAMEWORKS+=(-framework IOKit)
fi

for program in orblit_renderer_test orblit_headless; do
  # C99 and pedantic, so anything C++ in the header is an error here.
  clang -std=c99 -Wall -Wextra -Werror -pedantic -I "$SRC/include" \
    -c "$program.c" -o "$OUT/$program.o"
  clang++ "$OUT/$program.o" "${objects[@]}" "${archives[@]}" \
    "${FRAMEWORKS[@]}" -o "$OUT/$program"
done

# The splat sort's, cull's and limit's own checks. C++ rather than C, because
# what they check is behind the ABI rather than in it: see
# orblit_splats_check.cpp.
clang++ -std=c++17 -O2 -Wall -Wextra -I "$SRC" \
  -c orblit_splats_check.cpp -o "$OUT/orblit_splats_check.o"
clang++ "$OUT/orblit_splats_check.o" "${objects[@]}" "${archives[@]}" \
  "${FRAMEWORKS[@]}" -o "$OUT/orblit_splats_check"

# The cook step for splat captures: a .ply or .spz in, the .osplat a launch
# reads without parsing out. See orblit_splat_cook.cpp.
clang++ -std=c++17 -O2 -Wall -Wextra -I "$SRC" \
  -c orblit_splat_cook.cpp -o "$OUT/orblit_splat_cook.o"
clang++ "$OUT/orblit_splat_cook.o" "${objects[@]}" "${archives[@]}" \
  "${FRAMEWORKS[@]}" -o "$OUT/orblit_splat_cook"
echo "built $OUT/orblit_renderer_test, $OUT/orblit_headless," \
     "$OUT/orblit_splats_check and $OUT/orblit_splat_cook"

if [ "${1:-}" = "test" ]; then
  "$OUT/orblit_splats_check"
  "$OUT/orblit_renderer_test"
fi
