#!/bin/bash
# Bakes an .hdr or .exr environment into the two KTX cubemaps an
# OrblitEnvironment names, with Filament's cmgen, at the sizes the renderer
# filters the same picture at when it is named directly.
#
#   tool/bake_environment.sh [--size N] [--skybox N] <picture.hdr|.exr> <out dir>
#
# Writes, into <out dir>:
#   <name>_ibl.ktx     the reflections, N texels a side with a level for every
#                      size down to 16, and the diffuse harmonics in its
#                      metadata
#   <name>_skybox.ktx  the backdrop, --skybox texels a side
#   <name>_sh.txt      the same harmonics at full precision, for comparing
#
# Then:
#   OrblitEnvironment(radiance: 'out/<name>_ibl.ktx',
#                     skybox: 'out/<name>_skybox.ktx')
#
# **Bake, or name the picture?** OrblitEnvironment.fromImage filters an .hdr
# or .exr at run time: a decode on a worker and two frames with GPU work in
# them, once per picture per session. Bake what ships — the result is fixed
# before anybody runs it, a launch reads two small files and filters nothing,
# and a device that cannot filter (one without half-float render targets gets
# a small CPU-filtered environment instead) still gets the full-size light.
# Name the picture for what is not known in advance: an editor trying
# environments on, a user's own HDR, anything fetched.
#
# **The sizes.** The renderer chooses from the device (see planFor in
# OrblitEnvironment.cpp), and these defaults are its choice on a device at
# Filament feature level 2 or above:
#   --size 256     cmgen's own default; the renderer uses 128 at feature
#                  level 1 (GLES 3.0, WebGL 2) and 64 on its CPU route
#   --skybox       four times --size, held to 1024 and to a quarter of the
#                  picture's width (read from an .hdr's header; an .exr's
#                  width is not read here, so pass --skybox for one smaller
#                  than 4096 wide); the renderer also holds it to 512 on a
#                  device with under 3 GB of memory
# Bake at the size a scene asks for with OrblitEnvironment.fromImage(size:)
# and the two agree. native/headless's environment check bakes with this
# script and measures, in pixels, how far the two routes are apart.
#
# cmgen: ORBLIT_CMGEN if set, else the one in the Filament SDK
# packages/orblit_filament/darwin/setup.sh fetched, else cmgen on PATH.
set -euo pipefail

size=256
skybox=""
while [ $# -gt 0 ]; do
  case "$1" in
    --size) size="$2"; shift 2 ;;
    --skybox) skybox="$2"; shift 2 ;;
    -h|--help) sed -n '2,45p' "$0"; exit 0 ;;
    --*) echo "bake_environment.sh: unknown option $1" >&2; exit 1 ;;
    *) break ;;
  esac
done
if [ $# -ne 2 ]; then
  echo "usage: tool/bake_environment.sh [--size N] [--skybox N] <picture.hdr|.exr> <out dir>" >&2
  exit 1
fi
picture="$1"
out="$2"

power_of_two() { [ "$1" -gt 0 ] && [ $(( $1 & ($1 - 1) )) -eq 0 ]; }
if ! power_of_two "$size"; then
  echo "bake_environment.sh: --size must be a power of two, not $size" >&2
  exit 1
fi
if [ ! -f "$picture" ]; then
  echo "bake_environment.sh: no picture at $picture" >&2
  exit 1
fi

here="$(cd "$(dirname "$0")" && pwd)"
cmgen="${ORBLIT_CMGEN:-}"
if [ -z "$cmgen" ]; then
  sdk_cmgen="$here/../packages/orblit_filament/darwin/third_party/filament-mac/filament/bin/cmgen"
  if [ -x "$sdk_cmgen" ]; then
    cmgen="$sdk_cmgen"
  elif command -v cmgen >/dev/null 2>&1; then
    cmgen="$(command -v cmgen)"
  else
    echo "bake_environment.sh: no cmgen; run packages/orblit_filament/darwin/setup.sh or set ORBLIT_CMGEN" >&2
    exit 1
  fi
fi

if [ -z "$skybox" ]; then
  skybox=$(( size * 4 ))
  if [ "$skybox" -gt 1024 ]; then skybox=1024; fi
  # An .hdr says its width in its header; a backdrop sharper than the picture
  # is only a larger texture.
  resolution="$(LC_ALL=C head -c 4096 "$picture" | LC_ALL=C grep -a -m1 -E '^-Y [0-9]+ \+X [0-9]+' || true)"
  if [ -n "$resolution" ]; then
    width="${resolution##* }"
    quarter=1
    while [ $(( quarter * 2 )) -le $(( width / 4 )) ]; do quarter=$(( quarter * 2 )); done
    if [ "$skybox" -gt "$quarter" ]; then skybox="$quarter"; fi
  fi
fi
if ! power_of_two "$skybox"; then
  echo "bake_environment.sh: --skybox must be a power of two, not $skybox" >&2
  exit 1
fi

name="$(basename "$picture")"
name="${name%.*}"
work="$(mktemp -d "${TMPDIR:-/tmp}/orblit-bake.XXXXXX")"
trap 'rm -rf "$work"' EXIT

# The reflections and harmonics. cmgen names what it writes after the
# directory it is given, so the directory is named after the picture.
"$cmgen" --quiet --format=ktx --size="$size" --deploy="$work/$name" "$picture"
# The backdrop, at its own size: cmgen's deploy writes one the size of the
# reflections, which is too soft to look at.
"$cmgen" --quiet --format=ktx --size="$skybox" --extract="$work/sky/$name" "$picture"

mkdir -p "$out"
cp "$work/$name/${name}_ibl.ktx" "$out/${name}_ibl.ktx"
cp "$work/sky/$name/${name}_skybox.ktx" "$out/${name}_skybox.ktx"
cp "$work/$name/sh.txt" "$out/${name}_sh.txt"
echo "bake_environment.sh: $out/${name}_ibl.ktx ($size) and $out/${name}_skybox.ktx ($skybox)"
