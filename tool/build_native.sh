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

# The Linux renderer links Filament's libc++ archives, which only clang builds
# against. A machine whose default compiler is gcc would fail in the link.
if [ "$os" = linux ] && command -v clang++ > /dev/null; then
  export CC=${CC:-clang} CXX=${CXX:-clang++}
fi

build() {
  local package=$1
  shift
  local dir="$root/dotnet/native/build/$package"
  cmake -S "$root/packages/$package" -B "$dir" -DORBLIT_RID="$rid" -DCMAKE_BUILD_TYPE=Release "$@" > /dev/null
  cmake --build "$dir" --config Release > /dev/null
  cmake --install "$dir" --config Release --prefix "$root/dotnet/native" > /dev/null
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
  build orblit_filament -DORBLIT_FILAMENT_SDK="$sdk" -DORBLIT_GENERATED_DIR="$materials"
else
  echo "  skip  orblit_filament (no Filament release at $sdk, set ORBLIT_FILAMENT_SDK)"
fi
