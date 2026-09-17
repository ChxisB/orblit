#!/bin/bash
# Fetches the sources the texture cooker is built from, pinned and checked.
#
#   Basis Universal v2.1.0r (BinomialLLC/basis_universal, commit e4f439f)
#     the UASTC encoder, the transcoder that turns UASTC into ASTC, BC7,
#     BC5, BC4, ETC2 and EAC, and the ASTC, BC and ETC block decoders the
#     check measures with. Apache-2.0. It carries its own copy of Zstandard
#     (zstd/zstd.c, 1.5.x, BSD), which is the one the cooker compresses with.
#   stb_image 2.30 and stb_image_write 1.16 (nothings/stb, commit 2c980bb)
#     PNG and JPEG in, and PNG out for the check's own fixtures. MIT or
#     public domain, at the user's choice.
#
# Why fetched rather than taken from the Filament fork beside this repository,
# which has Basis Universal in its third_party/: the fork carries v2.1.0, and
# v2.1.0r is the revision after it with the fixes from fuzzing the transcoder
# (an ETC1 modifier table one entry short, an unchecked allocation size). The
# cooker reads untrusted .ktx2 files through that transcoder, so it wants
# those. And one pinned source is what makes a cook the same bytes on every
# machine: a cooker built from whatever copy happens to be nearby is a cooker
# whose output depends on the neighbourhood.
#
# Why file by file rather than the release archive: the archive is 161 MB of
# test images around 8 MB of source. This needs only curl and a sha256 tool,
# which is what a CI runner has.
#
# Every file is checked against sources.sha256 before anything is moved into
# place, so an interrupted or tampered download never looks like a finished
# one. Idempotent: a complete, matching third_party/ is left alone.
#
# Usage: fetch.sh    (into third_party/ beside this script, git-ignored)
set -euo pipefail
cd "$(dirname "$0")"

BASISU_COMMIT=e4f439fc9545b6a9e1fd26fc7ffd0c682c4b96d4
STB_COMMIT=2c980bb59875b0d32144a71867fbdebb2f77cd20
MANIFEST="$PWD/sources.sha256"
INTO="third_party"

# shasum on macOS, sha256sum on Linux; both read the same two-column format.
if command -v sha256sum > /dev/null; then
  check=(sha256sum --check --quiet --strict)
elif command -v shasum > /dev/null; then
  check=(shasum -a 256 --check --quiet --strict)
else
  echo "fetch.sh: no sha256sum or shasum to check the download with" >&2
  exit 1
fi

if [ -d "$INTO" ] && (cd "$INTO" && "${check[@]}" "$MANIFEST") > /dev/null 2>&1; then
  exit 0
fi

echo "texture_cook: fetching Basis Universal ${BASISU_COMMIT:0:7} and stb ${STB_COMMIT:0:7}"
work="$(mktemp -d "${TMPDIR:-/tmp}/orblit-texture-cook-fetch.XXXXXX")"
trap 'rm -r -f "$work"' EXIT

while read -r _ path; do
  case "$path" in
    basisu/*) url="https://raw.githubusercontent.com/BinomialLLC/basis_universal/$BASISU_COMMIT/${path#basisu/}" ;;
    stb/*) url="https://raw.githubusercontent.com/nothings/stb/$STB_COMMIT/${path#stb/}" ;;
    *) echo "fetch.sh: $path in the manifest belongs to no known source" >&2; exit 1 ;;
  esac
  mkdir -p "$work/$(dirname "$path")"
  curl -fsSL --retry 3 -o "$work/$path" "$url"
done < "$MANIFEST"

if ! (cd "$work" && "${check[@]}" "$MANIFEST"); then
  echo "fetch.sh: the download does not match sources.sha256; nothing was changed" >&2
  exit 1
fi

rm -r -f "$INTO"
mv "$work" "$INTO"
trap - EXIT
echo "texture_cook: sources in $(pwd)/$INTO"
