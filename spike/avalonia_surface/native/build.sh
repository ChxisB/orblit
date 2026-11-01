#!/bin/bash
# Builds the renderer's C ABI as a shared library for the Avalonia surface
# spike. Throwaway.
#
#   build.sh portable   no Objective-C. Headless and window surfaces only.
#   build.sh apple      adds the IOSurface surface, so copy_presented works.
#
# Output: build/<variant>/liborblit_renderer.dylib
set -euo pipefail
cd "$(dirname "$0")"

VARIANT="${1:-portable}"
ROOT="$(cd ../../.. && pwd)"
PKG="$ROOT/packages/orblit_filament"
SRC="$PKG/darwin/orblit_filament/Sources/orblit_filament_native"
SDK="$PKG/darwin/third_party/filament-mac/filament"
GENERATED="$SRC/generated/darwin-release"
OUT="build/$VARIANT"
mkdir -p "$OUT/obj"

DEFS=()
SOURCES=("$SRC"/*.cpp)
if [ "$VARIANT" = "portable" ]; then
  DEFS+=(-DORBLIT_PLATFORM_PORTABLE)
else
  SOURCES+=("$SRC/OrblitSurfaceApple.mm" "$SRC/OrblitPlatformApple.mm")
fi

# xargs cannot carry an array, so the defines ride in a string.
export SDK SRC GENERATED OUT
export DEFS_STRING="${DEFS[*]-}"
compile() {
  local source="$1"
  local name
  name="$(basename "$source")"
  name="${name%.*}"
  local lang=(-std=c++17)
  case "$source" in *.mm) lang=(-std=c++17 -ObjC++ -fobjc-arc) ;; esac
  # shellcheck disable=SC2086
  clang++ "${lang[@]}" -O2 -fPIC $DEFS_STRING \
    -Wno-deprecated-declarations -Wno-unused-private-field \
    -I "$SDK/include" -I "$SRC" -I "$SRC/include" -I "$GENERATED" \
    -c "$source" -o "$OUT/obj/$name.o"
}
export -f compile

printf '%s\n' "${SOURCES[@]}" | xargs -P 8 -I{} bash -c 'compile "$1"' _ {}

LIBS=(filament backend filabridge filaflat utils geometry smol-v ibl image
      abseil zstd filament-iblprefilter gltfio_core uberarchive uberzlib
      dracodec meshoptimizer ktxreader stb basis_transcoder mikktspace
      bluegl bluevk)
archives=()
for lib in "${LIBS[@]}"; do archives+=("$SDK/lib/arm64/lib$lib.a"); done
if [ -f "$SDK/lib/arm64/libwebgpu_dawn.a" ]; then
  archives+=("$SDK/lib/arm64/libwebgpu_dawn.a")
fi
FRAMEWORKS=(-framework Cocoa -framework Metal -framework QuartzCore
            -framework CoreVideo -framework IOSurface -framework OpenGL
            -framework CoreFoundation -framework ImageIO -framework AVFoundation
            -framework CoreMedia -framework CoreGraphics -framework IOKit)

clang++ -dynamiclib -o "$OUT/liborblit_renderer.dylib" \
  -install_name "@rpath/liborblit_renderer.dylib" \
  "$OUT"/obj/*.o "${archives[@]}" "${FRAMEWORKS[@]}"
echo "built $OUT/liborblit_renderer.dylib"

# The harness's own view and shared-event helpers. Apple only.
if [ "$VARIANT" = "apple" ]; then
  clang++ -dynamiclib -std=c++17 -ObjC++ -fobjc-arc -o build/libspike_view.dylib spike_view.mm \
    -install_name "@rpath/libspike_view.dylib" -framework Cocoa -framework Metal -framework QuartzCore
  echo "built build/libspike_view.dylib"
fi
ls -l "$OUT/liborblit_renderer.dylib"
