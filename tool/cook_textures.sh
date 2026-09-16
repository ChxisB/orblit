#!/bin/bash
# Cooks a folder of textures into GPU-ready KTX2, with mipmaps, beside it.
#
#   cook_textures.sh <folder> [--gltf scene.gltf] [--targets astc,bc,etc2,basis]
#                    [--threads N] [--max-size N] [--into DIR]
#
# Every .png, .jpg and Basis .ktx2 in <folder> goes through
# orblit_texture_cook (packages/orblit_filament/native/texture_cook) into
# <folder>.cooked/: x.ktx2 plus x.astc.ktx2, x.bc.ktx2 and x.etc2.ktx2, each a
# full mip chain in a format a GPU samples directly. The loader, asked for
# x.ktx2, takes the first sibling the device supports. Point a scene at the
# cooked folder in place of the original. --into writes somewhere else: a
# folder named for what the scene calls its textures, say, beside a copy of
# the scene.
#
# What each texture is decides how it is cooked, and a file does not say:
#
#   --gltf    the scene that uses the textures says. normalTexture means a
#             normal map (BC5, EAC RG11, renormalised mips); the base colour
#             of an alphaMode MASK material is a cut-out at its alphaCutoff
#             (coverage kept per level); base colour, emissive, sheen and
#             specular colour are sRGB; everything else is linear data.
#   without   the file name says, as these files are usually named: *Normal*
#             is a normal map; *BaseColor*, *Albedo*, *Diffuse* and
#             *Emissive* are sRGB colour; any other PNG or JPEG is linear.
#             A Basis .ktx2 carries sRGB or linear in its own header. No
#             cut-outs: nothing in a name says where the alpha test is.
#
# Resumable, and settings-aware: a texture is skipped when every file it
# should make is newer than its source and was made with the same flags,
# recorded in <folder>.cooked/.flags/. Each file is written under another
# name and renamed into place, so an interrupted run leaves nothing half
# written for the next one to skip.
#
# History. This script used to re-encode the Bistro's Basis textures to Basis
# again, smaller and with mips, through basisu -uastc -mipmap -resample. That
# is not what it does now, and what was measured then is still true of that
# approach. Four hundred and five textures came out at 1024 with eleven mip
# levels, 348 MB down to 181 MB, no failures — and the scene loaded no faster:
#
#     original, 2048, no mips     2112 ms, 3270 ms
#     cooked, 1024, eleven mips   3161 ms
#
# Basis is transcoded to a GPU format on every load (29 ms for one 2048
# square here), and pre-computed mips in a format that has to be transcoded
# move mip generation off the GPU, where it is nearly free, onto the CPU
# transcoder, where it is not: four hundred and five transcodes became four
# and a half thousand. It cost quality too: the foliage is alpha-tested, and
# a binary alpha through a plain halving and a block encoder came back as
# blocky leaves that thinned out with distance.
#
# Hence the cooker: GPU-native siblings, where a mip level is a memcpy and an
# upload and the transcode goes away, and mips made with the alpha test in
# mind (see OrblitTextureCook.h). Whether the scene loads faster from them is
# for the renderer's loader to measure, against the numbers above.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COOK_DIR="$ROOT/packages/orblit_filament/native/texture_cook"

usage() {
  echo "usage: cook_textures.sh <folder> [--gltf scene.gltf] [--targets astc,bc,etc2,basis]"
  echo "                        [--threads N] [--max-size N] [--into DIR]"
  exit 2
}

[ $# -ge 1 ] || usage
[ -d "$1" ] || { echo "no such folder: $1"; exit 1; }
FOLDER="$(cd "$1" && pwd)"
shift
GLTF=""
INTO=""
common=()
# What changes the bytes, for the stamp: everything but the thread count.
settings=()
while [ $# -gt 0 ]; do
  case "$1" in
    --gltf) [ $# -ge 2 ] || usage; GLTF="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"; shift 2 ;;
    --into) [ $# -ge 2 ] || usage; INTO="$2"; shift 2 ;;
    --threads) [ $# -ge 2 ] || usage; common+=("$1" "$2"); shift 2 ;;
    --targets|--max-size) [ $# -ge 2 ] || usage; common+=("$1" "$2"); settings+=("$1" "$2"); shift 2 ;;
    *) usage ;;
  esac
done
[ -z "$GLTF" ] || [ -f "$GLTF" ] || { echo "no such glTF: $GLTF"; exit 1; }

COOKER="${ORBLIT_TEXTURE_COOK:-$COOK_DIR/build/orblit_texture_cook}"
if [ ! -x "$COOKER" ]; then
  echo "building the texture cooker"
  "$COOK_DIR/build.sh" > /dev/null || { echo "could not build $COOKER"; exit 1; }
fi

VERSION="$("$COOKER" --version)" || { echo "$COOKER does not run"; exit 1; }
if [ -n "$INTO" ]; then
  mkdir -p "$INTO" || exit 1
  COOKED="$(cd "$INTO" && pwd)"
else
  COOKED="$(dirname "$FOLDER")/$(basename "$FOLDER").cooked"
fi
[ "$COOKED" != "$FOLDER" ] || { echo "cooking into the folder being cooked would overwrite it"; exit 1; }
mkdir -p "$COOKED/.flags"

# The plan: one line per texture, its file name and the flags it cooks with.
# In Python because it reads JSON, which fetch_bistro.sh already relies on.
# Into a file rather than a $(...): the bash macOS ships (3.2) misparses a
# quoted heredoc inside a command substitution.
plan="$(mktemp "${TMPDIR:-/tmp}/orblit-cook-plan.XXXXXX")"
trap 'rm -f "$plan"' EXIT
python3 - "$FOLDER" "$GLTF" > "$plan" <<'PYTHON'
import json, os, re, sys, urllib.parse

folder, gltf = sys.argv[1], sys.argv[2]
names = sorted(n for n in os.listdir(folder)
               if re.search(r'\.(png|jpe?g|ktx2)$', n, re.I)
               and not re.search(r'\.(astc|bc|etc2)\.ktx2$', n, re.I))

roles = {}
if gltf:
    doc = json.load(open(gltf))
    base = os.path.dirname(gltf)
    images = doc.get('images', [])

    def image_of(ref):
        if not ref:
            return None
        texture = doc['textures'][ref['index']]
        source = texture.get('source')
        for ext in texture.get('extensions', {}).values():
            source = ext.get('source', source)
        if source is None or 'uri' not in images[source]:
            return None
        path = os.path.normpath(os.path.join(base, urllib.parse.unquote(images[source]['uri'])))
        return os.path.basename(path) if os.path.dirname(path) == folder else None

    def mark(ref, role):
        name = image_of(ref)
        if name:
            roles.setdefault(name, []).append(role)

    for m in doc.get('materials', []):
        pbr = m.get('pbrMetallicRoughness', {})
        if m.get('alphaMode') == 'MASK':
            mark(pbr.get('baseColorTexture'), ('cutout', m.get('alphaCutoff', 0.5)))
        else:
            mark(pbr.get('baseColorTexture'), ('colour',))
        mark(pbr.get('metallicRoughnessTexture'), ('linear',))
        mark(m.get('normalTexture'), ('normal',))
        mark(m.get('occlusionTexture'), ('linear',))
        mark(m.get('emissiveTexture'), ('colour',))
        for name, ext in m.get('extensions', {}).items():
            for key, ref in ext.items():
                if not (key.endswith('Texture') and isinstance(ref, dict)):
                    continue
                if 'Normal' in key:
                    mark(ref, ('normal',))
                elif 'Color' in key:
                    mark(ref, ('colour',))
                else:
                    mark(ref, ('linear',))

for name in names:
    ktx2 = name.lower().endswith('.ktx2')
    found = roles.get(name)
    if found:
        # A texture two materials use differently is cooked for the first
        # of: normal map, cut-out, colour, linear — and said so.
        order = {'normal': 0, 'cutout': 1, 'colour': 2, 'linear': 3}
        kinds = sorted(set(r[0] for r in found), key=order.get)
        role = next(r for r in sorted(found, key=lambda r: order[r[0]]))
        if len(kinds) > 1:
            print(f"  note: {name} is used as {' and '.join(kinds)}; cooking as {kinds[0]}",
                  file=sys.stderr)
    elif gltf:
        role = ('unused',)
    elif re.search(r'normal', name, re.I):
        role = ('normal',)
    elif re.search(r'basecolou?r|albedo|diffuse|emissive', name, re.I):
        role = ('colour',)
    else:
        role = ('linear',)
    if role[0] == 'unused':
        print(f"  note: {name} is not used by the glTF; skipped", file=sys.stderr)
        continue
    if role[0] == 'normal':
        flags = '--normal' + ('' if ktx2 else ' --linear')
    elif role[0] == 'cutout':
        flags = '--cutout %g' % role[1]
    elif role[0] == 'colour':
        flags = ''
    else:
        flags = '' if ktx2 else '--linear'

    # A Basis .ktx2 says sRGB or linear itself, and a normal map that says
    # sRGB is refused rather than guessed at; the glTF's word is enough here.
    if role[0] == 'normal' and ktx2:
        flags += ' --linear'
    print(f"{name}\t{flags.strip()}")
PYTHON
[ $? -eq 0 ] || { echo "could not read the textures' roles"; exit 1; }

total=$(grep -c . "$plan" || true)
[ "$total" -gt 0 ] || { echo "no .png, .jpg or .ktx2 to cook in $FOLDER"; exit 1; }

# Which siblings a cook writes, for deciding what is already there.
targets="astc,bc,etc2,basis"
for ((i = 0; i < ${#common[@]}; i++)); do
  [ "${common[$i]}" = "--targets" ] && targets="${common[$((i + 1))]}"
done

echo "cooking $total textures from $FOLDER into $COOKED"
cooked=0
skipped=0
failed=0
failures=()
started=$(date +%s)

while IFS=$'\t' read -r name flags; do
  [ -n "$name" ] || continue
  source="$FOLDER/$name"
  stem="${name%.*}"
  # One flag set per texture, with the cooker's version, so a changed setting
  # or a cooker that cooks differently cooks it again.
  stamp="$VERSION $flags ${settings[*]:-}"

  expected=()
  case "$flags" in
    *--lossless*) expected=("$COOKED/$stem.ktx2") ;;
    *)
      IFS=',' read -r -a wanted <<< "$targets"
      for target in "${wanted[@]}"; do
        case "$target" in
          basis) expected+=("$COOKED/$stem.ktx2") ;;
          all) expected+=("$COOKED/$stem.ktx2" "$COOKED/$stem.astc.ktx2" "$COOKED/$stem.bc.ktx2" "$COOKED/$stem.etc2.ktx2") ;;
          *) expected+=("$COOKED/$stem.$target.ktx2") ;;
        esac
      done
      ;;
  esac
  whole=1
  for file in "${expected[@]}"; do
    if [ ! -s "$file" ] || [ ! "$file" -nt "$source" ]; then whole=0; fi
  done
  if [ "$whole" -eq 1 ] && [ "$(cat "$COOKED/.flags/$name" 2>/dev/null)" = "$stamp" ]; then
    skipped=$((skipped + 1))
    continue
  fi

  # $flags is a word list the plan wrote; split it on purpose.
  # shellcheck disable=SC2086
  if output=$("$COOKER" "$source" "$COOKED/$stem" $flags ${common[@]+"${common[@]}"} --quiet 2>&1); then
    echo "$stamp" > "$COOKED/.flags/$name"
    cooked=$((cooked + 1))
  else
    echo "  ! $output"
    failures+=("$name")
    failed=$((failed + 1))
  fi

  done_so_far=$((cooked + skipped + failed))
  if [ $((done_so_far % 25)) -eq 0 ]; then
    echo "  $done_so_far of $total, $(( $(date +%s) - started )) s"
  fi
done < "$plan"

size=$(du -s -k "$COOKED" | cut -f1)
echo "cooked $cooked, already there $skipped, failed $failed, in $(( $(date +%s) - started )) s;" \
     "$COOKED is $((size / 1024)) MB"
for name in ${failures[@]+"${failures[@]}"}; do echo "  failed: $name"; done
[ "$failed" -eq 0 ]
