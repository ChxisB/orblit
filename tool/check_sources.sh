#!/bin/bash
# The one thing about the sources that a passing build does not tell you.
#
# No new very long file. There was a list of files already over, kept as
# debt; it is empty now, and the point of the limit is that a new one cannot
# appear quietly the way OrblitRendererCore.cpp reached 7,675.
set -uo pipefail
cd "$(dirname "$0")/.."

LIMIT=1500
over=$(
  find . \( -name '*.cs' -o -name '*.cpp' -o -name '*.cc' -o -name '*.mm' \
            -o -name '*.h' \) -print |
    grep -v '/build/\|/generated/\|third_party\|/obj/\|/bin/\|node_modules' |
    xargs wc -l | awk -v l="$LIMIT" '$1 > l && $2 != "total" {print $1, $2}' |
    sed 's| \./| |'
)
if [ -n "$over" ]; then
  echo "$over" | while read -r count path; do
    echo "  FAIL  $path is $count lines, over $LIMIT"
  done
  exit 1
fi
echo "  ok    no source over $LIMIT lines"
