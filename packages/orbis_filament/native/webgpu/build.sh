#!/bin/bash
# Builds the portable renderer against the fork's native WebGPU runtime.
#
# This is deliberately separate from native/web/: WebGL 2 uses the released
# runtime and OpenGL materials, while this path uses the matching fork build,
# Dawn's Metal backend, and WebGPU materials produced by that fork's matc.
# Nothing here changes the default backend or any release material set.
#
#   ORBIS_FILAMENT_WEBGPU_SRC=/path/to/orbis-filament ./build.sh test
#   ORBIS_MATC_BACKENDS=all compiles Metal, Vulkan, OpenGL and WebGPU into
#   generated/webgpu-all, for comparing APIs on the same fork runtime.
#
set -euo pipefail
CALLER="$PWD"
cd "$(dirname "$0")"

if [ "$(uname -s)" != Darwin ] || [ "$(uname -m)" != arm64 ]; then
  echo "native/webgpu/build.sh: this host build currently requires Apple silicon macOS" >&2
  exit 1
fi

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
case "$FORK" in
  /*) ;;
  *) FORK="$CALLER/$FORK" ;;
esac
FORK="$(cd "$FORK" && pwd)"

FILAMENT="$FORK/out/webgpu-release/filament"
MATC="${ORBIS_FILAMENT_WEBGPU_MATC:-$FORK/out/cmake-webgpu-release/tools/matc/matc}"
if [ -z "${ORBIS_FILAMENT_WEBGPU_MATC:-}" ] && [ ! -x "$MATC" ]; then
  MATC="$FILAMENT/bin/matc"
fi
case "$MATC" in
  /*) ;;
  *) MATC="$CALLER/$MATC" ;;
esac
if [ ! -x "$MATC" ]; then
  echo "native/webgpu/build.sh: material compiler is not executable: $MATC" >&2
  exit 1
fi
if [ ! -f "$FILAMENT/include/filament/Engine.h" ] ||
   [ ! -f "$FILAMENT/lib/arm64/libwebgpu_dawn.a" ]; then
  echo "native/webgpu/build.sh: no installed WebGPU runtime at $FILAMENT" >&2
  echo "  build the fork with WebGPU support and install webgpu-release first" >&2
  exit 1
fi

API="${ORBIS_MATC_BACKENDS:-webgpu}"
case "$API" in
  webgpu) GENERATED_SET=webgpu ;;
  all) GENERATED_SET=webgpu-all ;;
  *)
    echo "native/webgpu/build.sh: ORBIS_MATC_BACKENDS must be webgpu or all" >&2
    exit 1
    ;;
esac
GENERATED="$SRC/generated/$GENERATED_SET"
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
MATC_FLAGS=(-a "$API" -p all)
# A checkout revision alone cannot identify built binaries: matc can be
# rebuilt without committing, and the installed runtime can be older.
compiler_id="$(shasum -a 256 "$MATC" | cut -d ' ' -f 1)"
runtime_id="$(shasum -a 256 "$FILAMENT"/lib/arm64/*.a | shasum -a 256 | cut -d ' ' -f 1)"
MATC_WANT="set=$GENERATED_SET runtime=webgpu fork=$fork_id compiler=$compiler_id archives=$runtime_id flags=${MATC_FLAGS[*]}"
STAMP="$GENERATED/.matc"
STALE=""
if [ "$(cat "$STAMP" 2>/dev/null || true)" != "$MATC_WANT" ]; then
  STALE=1
  # An interrupted rebuild must not leave an old stamp blessing a mixture.
  rm -f "$STAMP"
fi

MATERIAL_TMP="$(mktemp -d "${TMPDIR:-/tmp}/orbis-webgpu.XXXXXX")"
cleanup() {
  rm -f "$MATERIAL_TMP/input.mat" "$MATERIAL_TMP/material.filamat" "$MATERIAL_TMP/header.h"
  rmdir "$MATERIAL_TMP"
}
trap cleanup EXIT

BLENDS="opaque transparent fade masked add"
VARIANTS="lit lit_slim unlit video"

compile() {
  local source="$1" name="$2" blend="${3:-}"
  local header="$GENERATED/${name}_material.h"
  if [ -z "$STALE" ] && [ -s "$header" ] && [ ! "$source" -nt "$header" ]; then
    return
  fi
  echo "native/webgpu: compiling $name"
  local input="$source"
  if [ -n "$blend" ]; then
    input="$MATERIAL_TMP/input.mat"
    sed "s/^\( *blending *: *\)[a-z]*,/\1$blend,/" "$source" > "$input"
  fi
  "$MATC" "${MATC_FLAGS[@]}" -o "$MATERIAL_TMP/material.filamat" "$input"
  (cd "$MATERIAL_TMP" && xxd -i material.filamat) \
    | sed "s/material_filamat/k${name}Material/g" > "$MATERIAL_TMP/header.h"
  mv "$MATERIAL_TMP/header.h" "$header"
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
ORBIS_GENERATED_SET="$GENERATED_SET" \
ORBIS_FILAMENT_BACKEND=webgpu \
ORBIS_BUILD_DIR="$OUT" \
  bash ../headless/build.sh

if [ "${1:-}" = "test" ]; then
  "$OUT/orbis_headless" "${2:-$OUT/webgpu.png}" webgpu
fi
