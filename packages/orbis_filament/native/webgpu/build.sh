#!/bin/bash
# Builds the portable renderer against the fork's native WebGPU runtime.
#
# This is deliberately separate from native/web/: WebGL 2 uses the released
# runtime and OpenGL materials, while this path uses the matching fork build,
# Dawn's Metal backend, and WebGPU materials produced by that fork's matc.
# Nothing here changes the default backend or any release material set.
#
#   ORBIS_FILAMENT_WEBGPU_SRC=/path/to/orbis-filament ./build.sh test
#
set -euo pipefail
cd "$(dirname "$0")"

PACKAGE="../.."
DARWIN="$PACKAGE/darwin"
SRC="$DARWIN/orbis_filament/Sources/orbis_filament_native"
MATERIALS="$DARWIN/materials"
FORK="${ORBIS_FILAMENT_WEBGPU_SRC:-}"
if [ -z "$FORK" ]; then
  echo "native/webgpu/build.sh: ORBIS_FILAMENT_WEBGPU_SRC is not set" >&2
  echo "  point it at the matching orbis-filament checkout" >&2
  exit 1
fi

FILAMENT="$FORK/out/webgpu-release/filament"
MATC="${ORBIS_FILAMENT_WEBGPU_MATC:-$FORK/out/cmake-webgpu-release/tools/matc/matc}"
if [ ! -x "$MATC" ]; then
  MATC="$FILAMENT/bin/matc"
fi
if [ ! -x "$MATC" ] || [ ! -f "$FILAMENT/lib/arm64/libwebgpu_dawn.a" ]; then
  echo "native/webgpu/build.sh: no installed WebGPU runtime at $FILAMENT" >&2
  echo "  build the fork with WebGPU support and install webgpu-release first" >&2
  exit 1
fi

GENERATED="$SRC/generated/webgpu"
mkdir -p "$GENERATED"

# These are backend-independent lookup data, not compiled materials. Reuse
# the release set when it exists; this avoids fetching two copies and keeps
# the WebGPU set limited to the blobs whose compiler/runtime identity matters.
COMMON="$SRC/generated/darwin-release"
for table in AreaTex.h SearchTex.h LtcTables.h; do
  if [ ! -s "$GENERATED/$table" ]; then
    if [ ! -s "$COMMON/$table" ]; then
      echo "native/webgpu/build.sh: missing common table $table" >&2
      echo "  run packages/orbis_filament/darwin/setup.sh first" >&2
      exit 1
    fi
    cp "$COMMON/$table" "$GENERATED/$table"
  fi
done

fork_id="$(git -C "$FORK" rev-parse HEAD 2>/dev/null || echo unknown)"
MATC_FLAGS="-a webgpu -p all"
MATC_WANT="set=webgpu runtime=webgpu fork=$fork_id flags=$MATC_FLAGS"
STAMP="$GENERATED/.matc"
STALE=""
if [ "$(cat "$STAMP" 2>/dev/null || true)" != "$MATC_WANT" ]; then
  STALE=1
fi

BLENDS="opaque transparent fade masked add"
VARIANTS="lit lit_slim unlit video"

compile() {
  local source="$1" name="$2" blend="${3:-}"
  local header="$GENERATED/${name}_material.h"
  if [ -z "$STALE" ] && [ -f "$header" ] && [ ! "$source" -nt "$header" ]; then
    return
  fi
  echo "native/webgpu: compiling $name"
  local input="$source"
  if [ -n "$blend" ]; then
    input="/tmp/orbis_webgpu_src_$name.mat"
    sed "s/^\( *blending *: *\)[a-z]*,/\1$blend,/" "$source" > "$input"
  fi
  "$MATC" $MATC_FLAGS -o "/tmp/orbis_webgpu_$name.filamat" "$input"
  (cd /tmp && xxd -i "orbis_webgpu_$name.filamat") \
    | sed "s/orbis_webgpu_${name}_filamat/k${name}Material/g" > "$header"
  rm -f "/tmp/orbis_webgpu_$name.filamat" "/tmp/orbis_webgpu_src_$name.mat"
}

for mat in "$MATERIALS"/*.mat; do
  name="$(basename "$mat" .mat)"
  case " $VARIANTS " in
    *" $name "*)
      for blend in $BLENDS; do
        compile "$mat" "${name}_${blend}" "$blend"
      done
      ;;
    *)
      compile "$mat" "$name"
      ;;
  esac
done
echo "$MATC_WANT" > "$STAMP"

OUT="${ORBIS_BUILD_DIR:-$PWD/build}"
case "$OUT" in
  /*) ;;
  *) OUT="$PWD/$OUT" ;;
esac
ORBIS_FILAMENT_SDK="$FILAMENT" \
ORBIS_GENERATED_SET=webgpu \
ORBIS_FILAMENT_BACKEND=webgpu \
ORBIS_BUILD_DIR="$OUT" \
  bash ../headless/build.sh

if [ "${1:-}" = "test" ]; then
  "$OUT/orbis_headless" "${2:-$OUT/webgpu.png}" webgpu
fi
