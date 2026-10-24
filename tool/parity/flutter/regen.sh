#!/bin/bash
# Writes the fixtures that need a Flutter package to read. The exporter is
# copied into the package's test folder because that is where its imports
# resolve, run, and removed again.
set -euo pipefail
cd "$(dirname "$0")/../../.."
root=$PWD

export PATH="$HOME/development/flutter/bin:$PATH"

copies=(
  packages/orblit_filament/test/zz_filament_scene_test.dart
  packages/orblit_ui/test/zz_ui_test.dart
)
trap 'rm -f "${copies[@]/#/$root/}"' EXIT
cp tool/parity/flutter/filament_scene_test.dart "${copies[0]}"
cp tool/parity/flutter/ui_test.dart "${copies[1]}"
(cd packages/orblit_filament && flutter test test/zz_filament_scene_test.dart)
(cd packages/orblit_ui && flutter test test/zz_ui_test.dart)

git diff --stat -- dotnet/fixtures/orblit_filament dotnet/fixtures/orblit_ui
