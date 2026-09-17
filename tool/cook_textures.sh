#!/bin/bash
# Cooks a folder of textures into GPU-ready KTX2, with mipmaps, beside it.
#
#   cook_textures.sh <folder> [--gltf scene.gltf] [--targets astc,bc,etc2,basis]
#                    [--threads N] [--max-size N] [--into DIR] [--lossless]
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
#             normal map (renormalised mips, all three channels kept: BC7 and
#             ETC2 RGB8, because lit.mat and gltfio read .xyz); the base
#             colour of an alphaMode MASK material is a cut-out at its
#             alphaCutoff (coverage kept per level); base colour, emissive,
#             sheen and specular colour are sRGB; everything else is linear
#             data, cooked with the colour formats.
#   without   the file name says, as these files are usually named: *Normal*
#             is a normal map; *BaseColor*, *Albedo*, *Diffuse* and
#             *Emissive* are sRGB colour; any other PNG or JPEG is linear.
#             A Basis .ktx2 carries sRGB or linear in its own header. No
#             cut-outs: nothing in a name says where the alpha test is.
#
# Two roles are never assigned. Single-channel (BC4, EAC R11) samples 0 for
# green and blue, and glTF's occlusion reads red alone but its
# metallicRoughness, which is usually the same file, reads green and blue: a
# glTF cannot promise every use of an image reads red. Two-channel normals
# (BC5, EAC RG11) need a material that rebuilds Z, and none does. Both are
# orblit_texture_cook flags for whoever knows better.
#
# --lossless cooks every texture in the folder as pixel art: R8G8B8A8 in
# x.ktx2, bit for bit, no mips and no siblings. For a folder of sprites.
#
# Resumable, file by file. A file is current when it is newer than its source,
# its texture was cooked with the same flags (recorded in
# <folder>.cooked/.flags/), and the revision in its own KTXwriter is the one
# this cooker gives that family and content (orblit_texture_cook
# --revisions). Only the families that are not current are cooked again: a
# cooker that changes how normal maps are stored as BC re-cooks those files
# and leaves every other one as it is. Each file is written under another
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
  echo "                        [--threads N] [--max-size N] [--into DIR] [--lossless]"
  exit 2
}

[ $# -ge 1 ] || usage
[ -d "$1" ] || { echo "no such folder: $1"; exit 1; }
FOLDER="$(cd "$1" && pwd)"
shift
GLTF=""
INTO=""
targets="astc,bc,etc2,basis"
# Passed to every cook.
common=()
# What changes the bytes, for the stamp: not the thread count, and not the
# targets, which are decided file by file.
settings=()
while [ $# -gt 0 ]; do
  case "$1" in
    --gltf) [ $# -ge 2 ] || usage; GLTF="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"; shift 2 ;;
    --into) [ $# -ge 2 ] || usage; INTO="$2"; shift 2 ;;
    --lossless) common+=("$1"); settings+=("$1"); shift ;;
    --threads) [ $# -ge 2 ] || usage; common+=("$1" "$2"); shift 2 ;;
    --targets) [ $# -ge 2 ] || usage; targets="$2"; shift 2 ;;
    --max-size) [ $# -ge 2 ] || usage; common+=("$1" "$2"); settings+=("$1" "$2"); shift 2 ;;
    *) usage ;;
  esac
done
[ -z "$GLTF" ] || [ -f "$GLTF" ] || { echo "no such glTF: $GLTF"; exit 1; }

COOKER="${ORBLIT_TEXTURE_COOK:-$COOK_DIR/build/orblit_texture_cook}"
if [ ! -x "$COOKER" ]; then
  echo "building the texture cooker"
  "$COOK_DIR/build.sh" > /dev/null || { echo "could not build $COOKER"; exit 1; }
fi

"$COOKER" --revisions > /dev/null 2>&1 || {
  echo "$COOKER does not run, or is older than this script: rebuild it"
  exit 1
}
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
    # A Basis .ktx2 says sRGB or linear itself, so colour and data from one
    # need no flag. A normal map is linear whatever its header claims: the
    # cooker refuses one marked sRGB rather than guess, and the glTF naming it
    # a normal map is the word that settles it.
    if role[0] == 'normal':
        flags = '--normal --linear'
    elif role[0] == 'cutout':
        flags = '--cutout %g' % role[1]
    elif role[0] == 'colour':
        flags = ''
    else:
        flags = '' if ktx2 else '--linear'
    print(f"{name}\t{flags}")
PYTHON
[ $? -eq 0 ] || { echo "could not read the textures' roles"; exit 1; }

total=$(grep -c . "$plan" || true)
[ "$total" -gt 0 ] || { echo "no .png, .jpg or .ktx2 to cook in $FOLDER"; exit 1; }

# The families asked for, by name.
wanted=()
IFS=',' read -r -a asked <<< "$targets"
for target in "${asked[@]}"; do
  case "$target" in
    all) wanted+=(basis astc bc etc2) ;;
    basis|astc|bc|etc2) wanted+=("$target") ;;
    *) echo "--targets takes astc, bc, etc2 and basis"; exit 2 ;;
  esac
done

# revision_of <family> <the cooker's --revisions line, as words>
revision_of() {
  local family="$1"
  shift
  while [ $# -ge 2 ]; do
    [ "$1" = "$family" ] && { echo "$2"; return; }
    shift 2
  done
}

# The revision a cooked file says it is, from its KTXwriter, which sits in
# the first few hundred bytes.
revision_in() {
  LC_ALL=C head -c 4096 "$1" | LC_ALL=C grep -a -o -m 1 'Orblit texture cook [0-9]*' |
    awk '{print $4}'
}

suffix_of() {
  case "$1" in
    basis) echo ".ktx2" ;;
    *) echo ".$1.ktx2" ;;
  esac
}

echo "cooking $total textures from $FOLDER into $COOKED"
cooked=0
files=0
skipped=0
failed=0
failures=()
started=$(date +%s)

while IFS=$'\t' read -r name flags; do
  [ -n "$name" ] || continue
  source="$FOLDER/$name"
  stem="${name%.*}"
  stamp="$flags${settings[*]:+ ${settings[*]}}"
  # No stamp at all is never current, even for a texture whose flags are
  # empty.
  recorded="$(cat "$COOKED/.flags/$name" 2>/dev/null)" || recorded="(none)"
  # A stamp from before files carried their own revision began with the
  # cooker's version and named the targets; the revision is now read from
  # each file instead, and the targets are decided file by file.
  case "$recorded" in
    "orblit_texture_cook "*)
      recorded="$(printf '%s\n' "$recorded" |
        sed -e 's/^orblit_texture_cook [0-9]* //' -e 's/ *--targets [^ ]*//' -e 's/ *$//')"
      ;;
  esac

  # shellcheck disable=SC2086
  revisions="$("$COOKER" --revisions $flags ${settings[@]+"${settings[@]}"})" || {
    echo "  ! $name: the cooker does not take its flags ($flags)"
    failures+=("$name")
    failed=$((failed + 1))
    continue
  }
  families=("${wanted[@]}")
  case " $flags ${settings[*]:-} " in
    *" --lossless "*) families=(basis) ;;
  esac
  stale=()
  for family in "${families[@]}"; do
    file="$COOKED/$stem$(suffix_of "$family")"
    # shellcheck disable=SC2086
    expected="$(revision_of "$family" $revisions)"
    if [ "$recorded" != "$stamp" ] || [ ! -s "$file" ] || [ ! "$file" -nt "$source" ] ||
       [ "$(revision_in "$file")" != "$expected" ]; then
      stale+=("$family")
    fi
  done
  if [ ${#stale[@]} -eq 0 ]; then
    skipped=$((skipped + 1))
  else
    only="$(IFS=,; echo "${stale[*]}")"
    # $flags is a word list the plan wrote; split it on purpose.
    # shellcheck disable=SC2086
    if output=$("$COOKER" "$source" "$COOKED/$stem" $flags --targets "$only" \
                  ${common[@]+"${common[@]}"} --quiet 2>&1); then
      echo "$stamp" > "$COOKED/.flags/$name"
      cooked=$((cooked + 1))
      files=$((files + ${#stale[@]}))
    else
      echo "  ! $output"
      failures+=("$name")
      failed=$((failed + 1))
    fi
  fi

  done_so_far=$((cooked + skipped + failed))
  if [ $((done_so_far % 25)) -eq 0 ]; then
    echo "  $done_so_far of $total, $(( $(date +%s) - started )) s"
  fi
done < "$plan"

size=$(du -s -k "$COOKED" | cut -f1)
echo "cooked $cooked ($files files), already there $skipped, failed $failed," \
     "in $(( $(date +%s) - started )) s; $COOKED is $((size / 1024)) MB"
for name in ${failures[@]+"${failures[@]}"}; do echo "  failed: $name"; done
[ "$failed" -eq 0 ]
