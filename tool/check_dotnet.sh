#!/bin/bash
# Builds, tests and format-checks everything under dotnet/, and runs the
# checks on the native sources.
#
# The analyzer rules from the standard's C# section are build errors, so the
# build is the lint. A build that fails stops the run, because a test over a
# stale binary says nothing. The native libraries are built first, because the
# test projects copy them next to the test assembly.
set -uo pipefail
cd "$(dirname "$0")/../dotnet"

failures=0

step() {
  local label=$1
  shift
  if "$@"; then
    echo "  ok    $label"
  else
    echo "  FAIL  $label"
    failures=$((failures + 1))
    return 1
  fi
}

step "source sizes" ../tool/check_sources.sh
# The check compiles to a Unix path, so a Windows shell skips it.
case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) echo "  skip  core C++ (Windows)" ;;
  *)
    if command -v clang++ > /dev/null; then
      step "core C++" ../tool/check_native.sh
    else
      echo "  skip  core C++ (no clang++)"
    fi
    ;;
esac
step "native build" ../tool/build_native.sh || exit "$failures"
step "native floor" ../tool/check_native_floor.sh
step "dotnet build" dotnet build --nologo || exit "$failures"
step "dotnet test" dotnet test --no-build
step "dotnet format" dotnet format --verify-no-changes --no-restore

exit "$failures"
