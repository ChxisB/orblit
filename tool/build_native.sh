#!/bin/bash
# Builds the native libraries the .NET packages load and puts them in
# dotnet/native/runtimes/<rid>/native, where the project files pick them up.
#
# Only the desktop runtime ids are known. iOS, Android and web come later.
set -euo pipefail
cd "$(dirname "$0")/.."
root=$PWD

case "$(uname -s)" in
  Darwin) os=osx ;;
  Linux) os=linux ;;
  MINGW* | MSYS* | CYGWIN*) os=win ;;
  *) echo "Unknown system $(uname -s)." >&2; exit 1 ;;
esac
case "$(uname -m)" in
  arm64 | aarch64) arch=arm64 ;;
  x86_64 | AMD64) arch=x64 ;;
  *) echo "Unknown machine $(uname -m)." >&2; exit 1 ;;
esac
rid=$os-$arch

# A dylib built on a new macOS refuses to load on an older one. CMake takes its
# deployment target from this variable. tool/check_native_floor.sh holds the
# same number.
[ "$os" = osx ] && export MACOSX_DEPLOYMENT_TARGET=${MACOSX_DEPLOYMENT_TARGET:-14.0}

# Silent when the command works. When it fails, prints the lines that name the
# error, once each, because MSVC writes them to stdout and repeats a header's
# error for every file that includes it.
quietly() {
  local log
  log=$(mktemp)
  if "$@" > "$log" 2>&1; then
    rm -f "$log"
    return 0
  fi
  grep -E 'error|Error|FAILED' "$log" | awk '!seen[$0]++' | head -n 60 >&2 || true
  tail -n 5 "$log" >&2
  rm -f "$log"
  return 1
}

build() {
  local package=$1
  shift
  local dir="$root/dotnet/native/build/$package"
  quietly cmake -S "$root/packages/$package" -B "$dir" -DORBLIT_RID="$rid" -DCMAKE_BUILD_TYPE=Release "$@"
  quietly cmake --build "$dir" --config Release
  quietly cmake --install "$dir" --config Release --prefix "$root/dotnet/native"
  echo "  ok    $package -> runtimes/$rid/native"
}

build orblit_core
build orblit_audio_device

# The renderer needs a Filament release and its compiled materials, which are
# not in the repository. Each system's setup.sh stages its own pair. Linux and
# Windows write the materials to the flat generated/ folder.
filament=$root/packages/orblit_filament
generated=$filament/darwin/orblit_filament/Sources/orblit_filament_native/generated
case "$os" in
  osx)
    default_sdk=$filament/darwin/third_party/filament-mac/filament
    materials=$generated/darwin-release ;;
  linux)
    default_sdk=$filament/linux/third_party/filament
    materials=$generated ;;
  win)
    default_sdk=$filament/windows/third_party/filament
    materials=$generated ;;
esac
sdk=${ORBLIT_FILAMENT_SDK:-$default_sdk}
if [ -f "$sdk/include/filament/Engine.h" ]; then
  # Filament's Linux archives are built against libc++, which only clang links.
  # Core and the audio device keep the system compiler, because the parity
  # tests compare core's floats with a reference built by it.
  if [ "$os" = linux ] && command -v clang++ > /dev/null; then
    export CC=${CC:-clang} CXX=${CXX:-clang++}
  fi
  build orblit_filament -DORBLIT_FILAMENT_SDK="$sdk" -DORBLIT_GENERATED_DIR="$materials"
else
  echo "  skip  orblit_filament (no Filament release at $sdk, set ORBLIT_FILAMENT_SDK)"
fi
