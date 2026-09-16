#!/bin/bash
# Builds the offline texture cooker and its check: orblit_texture_cook and
# orblit_texture_cook_check.
#
# Out of Basis Universal, its copy of zstd and stb_image alone — no Filament,
# no renderer, no compiled materials — so it builds on a machine that has
# none of those, which is what a CI cook step is. See OrblitTextureCook.h for
# why none of this may go into Sources/orblit_filament_native.
#
#   build.sh               builds both into $ORBLIT_BUILD_DIR (default: build)
#   build.sh test          and runs the check
#   build.sh determinism   and cooks the check's fixtures with an -O0 build
#                          and an -O2 build, at one thread and at eight, and
#                          compares the files byte for byte
#   build.sh fuzz [N]      builds under AddressSanitizer and
#                          UndefinedBehaviorSanitizer into
#                          $ORBLIT_BUILD_DIR/sanitized, and runs N mutated
#                          inputs through the cooker and the check's reader
#                          (default 3000)
#
#   ORBLIT_COOK_OPT        the optimisation flag (default -O2)
#   CXX, CC                the compilers (default clang++ and clang)
#
# Basis Universal's thirty-odd files are compiled once per build directory
# and flag set, and kept: they are pinned, so they only change when fetch.sh
# fetches another version, and that replaces them wholesale.
#
# -ffp-contract=off, everywhere. Without it clang on arm64 and GCC everywhere
# may fuse a * b + c into one fused-multiply-add instruction at -O2 and not
# at -O0, and the two round differently — so the same cook would give
# different bytes depending on how the cooker was built. -fno-strict-aliasing
# because Basis Universal asks for it.
set -euo pipefail
cd "$(dirname "$0")"
HERE="$PWD"

MODE="${1:-}"
OUT="${ORBLIT_BUILD_DIR:-build}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
CXX="${CXX:-clang++}"
CC="${CC:-clang}"
OPT="${ORBLIT_COOK_OPT:--O2}"

./fetch.sh

TP="$HERE/third_party"
DEFINES=(-DBASISU_SUPPORT_SSE=0 -DBASISU_SUPPORT_OPENCL=0
         -DBASISD_SUPPORT_KTX2_ZSTD=1 -DBASISU_DISABLE_ANDROID_ASTC_DECOMP=0)
COMMON=(-ffp-contract=off -fno-strict-aliasing -pthread)
SANITIZE=()
if [ "$MODE" = "fuzz" ]; then
  OUT="$OUT/sanitized"
  OPT="-O1"
  # Alignment is left out, as Basis Universal's own sanitised build leaves it
  # out: it reads packed blocks through wider types on purpose, which is not
  # what these runs are looking for.
  SANITIZE=(-g -fsanitize=address,undefined -fno-sanitize=alignment
            -fno-sanitize-recover=undefined -fno-omit-frame-pointer)
fi

build_objects() {
  local out="$1"
  local opt="$2"
  shift 2
  local flags=("$opt" "${COMMON[@]}" "${DEFINES[@]}" ${SANITIZE[@]+"${SANITIZE[@]}"})
  mkdir -p "$out/basisu"
  # Objects built with other flags are not these objects.
  local signature="$CXX $CC ${flags[*]}"
  if [ "$(cat "$out/basisu/flags" 2>/dev/null)" != "$signature" ]; then
    rm -f "$out"/basisu/*.o
    echo "$signature" > "$out/basisu/flags"
  fi

  local sources=()
  local source
  for source in "$TP"/basisu/encoder/*.cpp "$TP"/basisu/encoder/3rdparty/android_astc_decomp.cpp \
                "$TP"/basisu/transcoder/basisu_transcoder.cpp; do
    sources+=("$source")
  done

  # In parallel, a few at a time: the transcoder alone is a 1.5 MB file.
  local pids=()
  for source in "${sources[@]}"; do
    local object="$out/basisu/$(basename "$source" .cpp).o"
    if [ ! -f "$object" ] || [ "$source" -nt "$object" ]; then
      "$CXX" -std=c++17 -w "${flags[@]}" -I "$TP/basisu" -c "$source" -o "$object.part" &&
        mv "$object.part" "$object" &
      pids+=($!)
      if [ ${#pids[@]} -ge 6 ]; then
        wait "${pids[0]}"
        pids=("${pids[@]:1}")
      fi
    fi
  done
  local zstd_object="$out/basisu/zstd.o"
  if [ ! -f "$zstd_object" ] || [ "$TP/basisu/zstd/zstd.c" -nt "$zstd_object" ]; then
    "$CC" -w "${flags[@]}" -c "$TP/basisu/zstd/zstd.c" -o "$zstd_object.part" &&
      mv "$zstd_object.part" "$zstd_object" &
    pids+=($!)
  fi
  local pid
  for pid in ${pids[@]+"${pids[@]}"}; do wait "$pid"; done
  for source in "${sources[@]}"; do
    [ -f "$out/basisu/$(basename "$source" .cpp).o" ] || {
      echo "texture_cook/build.sh: $(basename "$source") did not compile" >&2
      exit 1
    }
  done
  [ -f "$zstd_object" ] || { echo "texture_cook/build.sh: zstd.c did not compile" >&2; exit 1; }

  # Ours, always, held to -Wall -Wextra.
  local ours=()
  for source in OrblitTextureCook OrblitStb orblit_texture_cook orblit_texture_cook_check; do
    local warnings=(-Wall -Wextra)
    # stb_image's own warnings are its business.
    [ "$source" = "OrblitStb" ] && warnings=(-w)
    "$CXX" -std=c++17 "${warnings[@]}" "${flags[@]}" -I "$TP/basisu" -I "$TP/stb" -I "$HERE" \
      -c "$HERE/$source.cpp" -o "$out/$source.o"
  done

  local library=("$out/OrblitTextureCook.o" "$out/OrblitStb.o" "$out"/basisu/*.o)
  "$CXX" "${flags[@]}" "$out/orblit_texture_cook.o" "${library[@]}" -o "$out/orblit_texture_cook"
  "$CXX" "${flags[@]}" "$out/orblit_texture_cook_check.o" "${library[@]}" \
    -o "$out/orblit_texture_cook_check"
}

build_objects "$OUT" "$OPT"
echo "built $OUT/orblit_texture_cook and $OUT/orblit_texture_cook_check"

case "$MODE" in
  test)
    # Real textures are measured from ORBLIT_TEXTURE_SAMPLES when it is set;
    # without it the check cooks the fixtures it draws for itself.
    "$OUT/orblit_texture_cook_check"
    ;;
  determinism)
    # The fixtures, then every cook of them from both builds at two thread
    # counts, compared with the first.
    work="$OUT/determinism"
    rm -r -f "$work"
    mkdir -p "$work/fixtures"
    build_objects "$OUT/O0" -O0
    "$OUT/orblit_texture_cook_check" --write-fixtures "$work/fixtures"
    shopt -s nullglob
    fixtures=("$work/fixtures"/*.png "$work/fixtures"/*.jpg "$work/fixtures"/*.ktx2)
    [ ${#fixtures[@]} -gt 0 ] || { echo "no fixtures were written" >&2; exit 1; }
    differ=0
    for fixture in "${fixtures[@]}"; do
      name="$(basename "$fixture")"
      stem="${name%.*}"
      flags=()
      case "$stem" in
        *normal*) flags=(--normal) ;;
        *single*) flags=(--single-channel --linear) ;;
        *cutout*) flags=(--cutout 0.5) ;;
        *pixel*) flags=(--lossless) ;;
      esac
      for build in O2:"$OUT" O0:"$OUT/O0"; do
        for threads in 1 8; do
          into="$work/${build%%:*}-t$threads"
          mkdir -p "$into"
          "${build#*:}/orblit_texture_cook" "$fixture" "$into/$stem" --threads "$threads" \
            --quiet ${flags[@]+"${flags[@]}"}
        done
      done
      for into in "$work"/O2-t8 "$work"/O0-t1 "$work"/O0-t8; do
        for file in "$work/O2-t1/$stem".*; do
          if ! cmp -s "$file" "$into/$(basename "$file")"; then
            echo "differs: $(basename "$file") in $(basename "$into")" >&2
            differ=$((differ + 1))
          fi
        done
      done
    done
    count=$(ls "$work/O2-t1" | wc -l | tr -d ' ')
    if [ "$differ" -ne 0 ]; then
      echo "$differ cooked file(s) differ between builds or thread counts" >&2
      exit 1
    fi
    echo "all $count cooked files identical at -O2 and -O0, one thread and eight"
    ;;
  fuzz)
    "$OUT/orblit_texture_cook_check" --fuzz "${2:-3000}"
    ;;
  "") ;;
  *)
    echo "usage: build.sh [test|determinism|fuzz [N]]" >&2
    exit 2
    ;;
esac
