#!/bin/bash
# Fails when a built native library needs a newer system than the packages
# promise.
#
#   tool/check_native_floor.sh [folder of runtime ids]
#
# A library built on a new runner image loads on that image and nothing older,
# and the package that carries it still restores. macOS names its floor in the
# library's build version. Linux names it in the newest glibc symbol the
# library asks for. Windows has no floor to read.
set -euo pipefail
cd "$(dirname "$0")/.."

runtimes=${1:-dotnet/native/runtimes}

# Ubuntu 24.04 ships glibc 2.39, and macOS 14 is the oldest system the
# packages target.
glibc_floor=2.39
macos_floor=14.0

# True when $1 is no newer than $2.
within() {
  [ "$(printf '%s\n%s\n' "$1" "$2" | sort -V | tail -n 1)" = "$2" ]
}

checked=0
failures=0
report() {
  local lib=$1 needs=$2 floor=$3 what=$4
  checked=$((checked + 1))
  if within "$needs" "$floor"; then
    echo "  ok    $(basename "$lib") needs $what $needs"
  else
    echo "  FAIL  $(basename "$lib") needs $what $needs, and the floor is $floor"
    failures=$((failures + 1))
  fi
}

for lib in "$runtimes"/linux-*/native/*.so; do
  [ -e "$lib" ] || continue
  needs=$(readelf --dyn-syms --wide "$lib" | grep -o 'GLIBC_[0-9.]*' | sed 's/GLIBC_//' | sort -V | tail -n 1)
  report "$lib" "${needs:-0}" "$glibc_floor" "glibc"
done

for lib in "$runtimes"/osx-*/native/*.dylib; do
  [ -e "$lib" ] || continue
  needs=$(otool -l "$lib" | awk '/LC_BUILD_VERSION/ { found = 1 } found && $1 == "minos" { print $2; exit }')
  report "$lib" "${needs:-0}" "$macos_floor" "macOS"
done

[ "$checked" -gt 0 ] || echo "  skip  no library here has a floor to read"
exit "$failures"
