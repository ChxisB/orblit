#!/usr/bin/env bash
# Starts a packaged editor on a copy of a small project. It passes when the engine inside the editor has
# started with no .NET error and the editor is still running. The engine runs headless, so no GPU is needed.
# Run it from the repository root: check_packaged_editor.sh <editor program>

set -euo pipefail

editor=$1
work=$(mktemp -d)
cp -R engine/editor/avalonia/Orblit.Editor.Bridge.Checks/Fixtures/view_demo "$work/project"
mkdir "$work/temp"
project=$work/project
temp=$work/temp
# A Windows program needs Windows paths.
if command -v cygpath > /dev/null; then
	project=$(cygpath -w "$project")
	temp=$(cygpath -w "$temp")
fi

# The editor writes the engine log under the temp folder. Windows reads TMP and the others read TMPDIR.
TMPDIR=$temp TMP=$temp "$editor" --project "$project" --headless &
pid=$!
# The editor may have quit already, and a file it still holds open may not delete.
trap 'kill "$pid" 2> /dev/null || true; rm -rf "$work" 2> /dev/null || true' EXIT

engine_log() {
	cat "$work"/temp/orblit-output-*/0.log 2> /dev/null || true
}

fail() {
	echo "::error::$1"
	engine_log
	exit 1
}

for _ in $(seq 120); do
	if engine_log | grep -q "Orblit Engine v"; then
		break
	fi
	kill -0 "$pid" 2> /dev/null || fail "The editor exited before its engine started."
	sleep 1
done
engine_log | grep -q "Orblit Engine v" || fail "The engine did not start within two minutes."

# The engine scans the project and starts .NET after it prints its version.
sleep 30
kill -0 "$pid" 2> /dev/null || fail "The editor exited after its engine started."
if engine_log | grep -q "ERROR: .NET"; then
	fail "The engine could not start .NET."
fi
engine_log
echo "The packaged editor started its engine."
