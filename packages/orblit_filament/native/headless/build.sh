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

# The FBX and OBJ importer's tool and checks, before anything else, because
# they need none of what the rest of this script goes looking for: no
# Filament, no compiled materials, no renderer core — the importer and ufbx
# alone, so they build and run even while the renderer does not. Held to
# -Wextra as well; ufbx's own warnings are quieted inside OrblitUfbx.cpp. The
# loop below links these same two objects into the renderer rather than
# compiling thirty thousand lines of ufbx a second time.
import_objects=()
for name in OrblitImport OrblitUfbx; do
  clang++ -std=c++17 -O2 -DORBLIT_PLATFORM_PORTABLE -Wall -Wextra \
    -I "$SRC" -I "$SRC/include" -c "$SRC/$name.cpp" -o "$OUT/$name.o"
  import_objects+=("$OUT/$name.o")
done
for program in orblit_import orblit_import_check; do
  clang++ -std=c++17 -O2 -Wall -Wextra -I "$SRC" \
    -c "$program.cpp" -o "$OUT/$program.o"
  clang++ "$OUT/$program.o" "${import_objects[@]}" -o "$OUT/$program"
done

# The KTX 2 reader's checks, likewise before anything that needs Filament:
# the reader is bytes in and bytes out, and needs only zstd.
clang++ -std=c++17 -O2 -DORBLIT_PLATFORM_PORTABLE -Wall -Wextra \
  -I "$SRC" -c "$SRC/OrblitKtx2.cpp" -o "$OUT/OrblitKtx2.o"
clang++ -std=c++17 -O2 -Wall -Wextra -I "$SRC" \
  -c orblit_ktx2_check.cpp -o "$OUT/orblit_ktx2_check.o"
clang++ "$OUT/orblit_ktx2_check.o" "$OUT/OrblitKtx2.o" \
  "$SDK/lib/arm64/libzstd.a" -o "$OUT/orblit_ktx2_check"

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
  case "$name" in
    OrblitImport|OrblitUfbx|OrblitKtx2) objects+=("$OUT/$name.o"); continue ;;
  esac
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

for program in orblit_renderer_test orblit_headless orblit_models_check; do
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

# Textures through the GPU: the upload queue counted, and formats, siblings,
# limits and arrival read back in pixels. See orblit_textures_check.cpp.
clang++ -std=c++17 -O2 -Wall -Wextra -Wno-deprecated-declarations \
  -I "$SDK/include" -I "$SRC" -I "$SRC/include" \
  -c orblit_textures_check.cpp -o "$OUT/orblit_textures_check.o"
clang++ "$OUT/orblit_textures_check.o" "${objects[@]}" "${archives[@]}" \
  "${FRAMEWORKS[@]}" -o "$OUT/orblit_textures_check"

# The cook step for splat captures: a .ply or .spz in, the .osplat a launch
# reads without parsing out. See orblit_splat_cook.cpp.
clang++ -std=c++17 -O2 -Wall -Wextra -I "$SRC" \
  -c orblit_splat_cook.cpp -o "$OUT/orblit_splat_cook.o"
clang++ "$OUT/orblit_splat_cook.o" "${objects[@]}" "${archives[@]}" \
  "${FRAMEWORKS[@]}" -o "$OUT/orblit_splat_cook"
echo "built $OUT/orblit_renderer_test, $OUT/orblit_headless," \
     "$OUT/orblit_models_check, $OUT/orblit_splats_check," \
     "$OUT/orblit_ktx2_check, $OUT/orblit_textures_check," \
     "$OUT/orblit_splat_cook," \
     "$OUT/orblit_import and $OUT/orblit_import_check"

if [ "${1:-}" = "test" ]; then
  # Real FBX and OBJ files are read from ORBLIT_IMPORT_SAMPLES when it is
  # set; without it the check runs its in-memory cases and says it skipped
  # the rest.
  "$OUT/orblit_import_check"
  # Real Basis files from ORBLIT_KTX2_SAMPLES when it is set.
  "$OUT/orblit_ktx2_check"
  "$OUT/orblit_splats_check"
  "$OUT/orblit_textures_check"
  "$OUT/orblit_renderer_test"
  # Khronos's samples and the converted ones, from ORBLIT_SAMPLES when it is
  # set (tool/fetch_import_samples.sh puts them in assets/samples); without it
  # only the ABI is checked.
  "$OUT/orblit_models_check"
fi
