#!/bin/bash
# Compiles and runs the core's own C++ checks, without Dart in the way.
set -euo pipefail
cd "$(dirname "$0")/../packages/orblit_core"
clang++ -std=c++17 -O2 -Wall -Wextra -Iinclude -Isrc \
  src/world.cpp src/orblit_core.cpp src/transform.cpp src/orblit_native_test.cpp -o /tmp/orblit_core_check
/tmp/orblit_core_check
