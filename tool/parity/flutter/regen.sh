#!/bin/bash
# Writes the fixtures that need a Flutter package to read. The exporter is
# copied into the package's test folder because that is where its imports
# resolve, run, and removed again.
set -euo pipefail
cd "$(dirname "$0")/../../.."
root=$PWD

export PATH="$HOME/development/flutter/bin:$PATH"

copy=packages/orblit_filament/test/zz_filament_scene_test.dart
trap 'rm -f "$root/$copy"' EXIT
cp tool/parity/flutter/filament_scene_test.dart "$copy"
(cd packages/orblit_filament && flutter test test/zz_filament_scene_test.dart)

git diff --stat -- dotnet/fixtures/orblit_filament
