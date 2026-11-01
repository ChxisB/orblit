#!/bin/bash
# Runs one spike configuration with a watchdog. Throwaway.
#   run.sh <path A|B|C> <WxH> <views> [extra args]
set -uo pipefail
cd "$(dirname "$0")/app"
export SPIKE_NATIVE="$PWD/../native/build"
PATH_KIND="$1"; PIXELS="$2"; VIEWS="$3"; shift 3
mkdir -p ../results
dotnet bin/Release/net10.0/SurfaceSpike.dll --path "$PATH_KIND" --pixels "$PIXELS" \
  --views "$VIEWS" --out ../results/runs.jsonl "$@" &
PID=$!
( sleep "${SPIKE_WATCHDOG:-90}"; kill -9 "$PID" 2>/dev/null ) &
DOG=$!
wait "$PID"; STATUS=$?
kill "$DOG" 2>/dev/null
echo "exit $STATUS (path $PATH_KIND, $PIXELS, $VIEWS view(s))"
