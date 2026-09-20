#!/bin/bash
# The two things about the sources that a passing build does not tell you.
#
# Both are the same failure the package list in check.sh guards against: work
# that looks fine here and is broken, or absent, somewhere nobody is looking.
set -uo pipefail
cd "$(dirname "$0")/.."

failures=0

# 1. Every shared source reaches every build.
#
# darwin (SwiftPM), native/headless and native/web all glob the core
# directory, so a new file joins them by existing. linux, windows and android
# name each source by hand, and a file missing from one of those three does
# not fail here, on a Mac — it fails on a platform with no CI, at link time,
# for whoever next builds it. The same applies to the two desktop plugins and
# the scene code they share, so both directories are checked the same way.
plugins=packages/orblit_filament

# Every source in $1 with extension $2, named through CMake variable $3 (they
# are $4) by each of the CMakeLists that follow.
listed_in() {
  local dir=$1 ext=$2 var=$3 what=$4 list on_disk listed absent phantom n
  shift 4
  on_disk=$(cd "$dir" && ls ./*."$ext" | sed 's|.*/||' | sort)
  for list in "$@"; do
    listed=$(grep -oE "\\$\{$var\}/[A-Za-z0-9_]+\.$ext" "$list" |
               sed 's|.*/||' | sort -u)
    absent=$(comm -23 <(echo "$on_disk") <(echo "$listed"))
    phantom=$(comm -13 <(echo "$on_disk") <(echo "$listed"))
    if [ -n "$absent$phantom" ]; then
      echo "  FAIL  $list"
      [ -n "$absent" ] && echo "$absent" | sed 's/^/          not built: /'
      [ -n "$phantom" ] && echo "$phantom" | sed 's/^/          no such file: /'
      failures=$((failures+1))
    else
      n=$(echo "$on_disk" | wc -l | tr -d ' ')
      echo "  ok    $list lists all $n $what$([ "$n" -eq 1 ] || echo s)"
    fi
  done
}

listed_in "$plugins/darwin/orblit_filament/Sources/orblit_filament_native" \
          cpp ORBLIT_CORE_DIR "core source" \
          "$plugins/linux/CMakeLists.txt" \
          "$plugins/windows/CMakeLists.txt" \
          "$plugins/android/src/main/cpp/CMakeLists.txt"

listed_in "$plugins/common" cc ORBLIT_COMMON_DIR "shared plugin source" \
          "$plugins/linux/CMakeLists.txt" \
          "$plugins/windows/CMakeLists.txt"

# 2. No new very long file.
#
# A limit with nothing exempted from it. There was a list of files already
# over, kept as debt; it is empty now, and the point of the limit is that a
# new one cannot appear quietly the way OrblitRendererCore.cpp reached 7,675.
LIMIT=1500
over=$(
  find . \( -name '*.dart' -o -name '*.cpp' -o -name '*.cc' -o -name '*.mm' \
            -o -name '*.h' \) -print |
    grep -v '/build/\|/generated/\|third_party\|\.dart_tool\|/example/' |
    xargs wc -l | awk -v l="$LIMIT" '$1 > l && $2 != "total" {print $1, $2}' |
    sed 's| \./| |'
)
if [ -n "$over" ]; then
  echo "$over" | while read -r count path; do
    echo "  FAIL  $path is $count lines, over $LIMIT"
  done
  failures=$((failures+1))
else
  echo "  ok    no source over $LIMIT lines"
fi

exit "$failures"
