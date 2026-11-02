#!/bin/bash
# Fails when a package that carries native libraries lacks one of the three
# runtime ids, or holds a library in a folder below native/.
#
#   tool/check_pack_layout.sh <folder of .nupkg files>
#
# A package packed on one machine holds that machine's natives only. A reader
# on another system restores it and finds no library to load.
set -euo pipefail

folder=${1:?Usage: tool/check_pack_layout.sh <folder of .nupkg files>}
runtime_ids=(osx-arm64 linux-x64 win-x64)

checked=0
failures=0
for package in "$folder"/*.nupkg; do
  [ -e "$package" ] || continue
  entries=$(unzip -Z1 "$package")
  grep -q '^runtimes/' <<< "$entries" || continue
  checked=$((checked + 1))

  missing=()
  for rid in "${runtime_ids[@]}"; do
    grep -q "^runtimes/$rid/native/[^/]" <<< "$entries" || missing+=("$rid")
  done
  if [ "${#missing[@]}" -gt 0 ]; then
    echo "  FAIL  $(basename "$package") has no natives for ${missing[*]}"
    failures=$((failures + 1))
  fi

  # A project file that adds a folder level twice puts the library at
  # runtimes/<rid>/native/<rid>/native/, where the loader does not look.
  if grep -q '^runtimes/[^/]*/native/[^/]*/' <<< "$entries"; then
    echo "  FAIL  $(basename "$package") has a native library below runtimes/<rid>/native/"
    failures=$((failures + 1))
  fi
done

if [ "$checked" -eq 0 ]; then
  echo "  FAIL  no package in $folder carries natives"
  exit 1
fi
[ "$failures" -eq 0 ] && echo "  ok    $checked packages carry ${runtime_ids[*]}"
exit "$failures"
