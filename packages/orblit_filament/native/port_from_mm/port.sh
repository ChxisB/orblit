#!/bin/bash
# Regenerates the renderer's C++ core from an OrblitRenderer.mm in the old,
# Objective-C shape.
#
#   port.sh <an OrblitRenderer.mm from before the move>
#
# For a branch cut before the renderer moved into C++ that changed
# OrblitRenderer.mm: merge it, resolve OrblitRenderer.mm by keeping the wrapper
# (`git checkout --ours`), then run this on the branch's own copy of the old
# file (`git show <branch>:<path>/OrblitRenderer.mm > /tmp/old.mm`). It writes
# OrblitRendererCore.h and OrblitRendererCore.cpp with the branch's changes in
# the functions they were made in. This is how god rays, distortion and
# motion blur came across.
#
# It is the same conversion that made the core, run again: convert.py turns
# methods into member functions and message sends into calls, header.py turns
# the ivar block into members, and fix.py applies the hand conversions — the
# Apple calls replaced by the platform layer — which it finds by their exact
# text. So it only works while that text is still the text: a branch that
# edited one of those lines is told so (EXPECTED ... FOUND 0) and that hunk is
# ported by hand. Three things it cannot see, and which need doing by hand:
#
#   - a new #include goes into OrblitRendererCore.h.in, beside the
#     ScreenEffects and motion blur ones — except a compiled material's
#     …_material.h, which goes in the anonymous namespace at the top of
#     core_prologue.cpp, so its arrays stay private to the renderer; the
#     build supplies the selected generated directory as an include path;
#   - a new method in include/OrblitRenderer.h goes into the public section of
#     OrblitRendererCore.h.in and API in header.py, and gets a one-line
#     forwarder in OrblitRenderer.mm;
#   - anything Apple-only in the branch's new code (NSLog, NSString, NSData)
#     is a compile error to replace with orblit::log, std::string and
#     orblit::readFile.
#
# Once the branches cut before the move have landed, the core should be
# edited directly and this directory deleted: regenerating would throw away
# any change made to the C++ since.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
native="$here/../../darwin/orblit_filament/Sources/orblit_filament_native"
old="${1:?usage: port.sh <an OrblitRenderer.mm from before the move>}"
work="$(mktemp -d)"

python3 "$here/convert.py" "$old" "$work/methods.cpp" "$work/decls.txt" \
  "$work/prelude.h"
cp "$here/OrblitRendererCore.h.in" "$work/OrblitRendererCore.h.in"
python3 "$here/header.py" "$old" "$work/prelude.h" "$work/decls.txt" \
  "$work/OrblitRendererCore.h"
{
  cat "$here/core_prologue.cpp" "$work/methods.cpp"
  printf '\n}  // namespace orblit\n'
} > "$work/OrblitRendererCore.cpp"
python3 "$here/fix.py" "$work/OrblitRendererCore.cpp" "$here/replacements.cpp"

cp "$work/OrblitRendererCore.h" "$work/OrblitRendererCore.cpp" "$native/"
echo "wrote OrblitRendererCore.h and OrblitRendererCore.cpp; diff them before"
echo "committing — only the branch's own changes should show."
