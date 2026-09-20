#!/bin/bash
# Writes out everything the engine can export as glTF and validates the lot.
#
# The exporters are checked by unit tests, and unit tests check what somebody
# thought to assert. The format's own rules are longer than that — a buffer
# view that has to start on a four-byte boundary, an accessor whose count may
# not be nought, a minimum and maximum that POSITION has to carry — and the
# reference validator knows all of them.
#
# So: export real files, hand them to the validator, and fail on a warning as
# well as an error. A file only Orblit can load is not an export.
set -euo pipefail
cd "$(dirname "$0")/.."

OUT="${1:-$(mktemp -d)}"
mkdir -p "$OUT"
echo "orblit: writing exports to $OUT"

(cd packages/orblit_mesh && dart run tool/dump_glb.dart "$OUT/mesh")

if ! [ -d node_modules/gltf-validator ]; then
  echo "orblit: fetching the Khronos validator"
  npm install --no-save --no-audit --no-fund gltf-validator
fi

failed=0
for one in "$OUT"/*/; do
  echo
  echo "orblit: validating $(basename "$one")"
  node tool/validate_gltf.mjs "$one" || failed=1
done
exit "$failed"
