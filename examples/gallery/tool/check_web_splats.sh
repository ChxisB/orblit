#!/bin/bash
# Checks that a browser really draws Gaussian splats, and really sorts them.
#
# A screenshot proves a page did not crash. It does not prove the splats are
# there, and it certainly does not prove the order they were drawn in — which
# is the half of splatting that runs somewhere else entirely (a Web Worker
# here; see native/web/README.md). So this takes two shots of the same scene,
# one sorted and one not, and reads the pixels:
#
#   - both of the ring's colours are on screen in quantity, so the cloud drew
#     rather than a handful of stray splats
#   - the two shots differ, which they only can if the order the worker
#     handed back reached the GPU
#   - nothing in the console says the renderer stopped
#
#   tool/check_web_splats.sh        after flutter build web
#
# Needs what capture_web.sh needs: a built build/web with the renderer beside
# it, and headless Chrome. Prints what it measured either way, because a
# threshold that fails is only useful next to the number that failed it.
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${OUT:-captures}"

echo "check_web_splats: sorted"
OUT="$OUT" LABEL=splats_sorted bash tool/capture_web.sh "Gaussian%20splats" "${BUDGET:-20000}" > /dev/null
echo "check_web_splats: unsorted"
OUT="$OUT" LABEL=splats_unsorted QUERY='&ORBLIT_SPLAT_SORT=0' \
  bash tool/capture_web.sh "Gaussian%20splats" "${BUDGET:-20000}" > /dev/null

for log in "$OUT/splats_sorted.log" "$OUT/splats_unsorted.log"; do
  if grep -q 'has stopped' "$log"; then
    echo "check_web_splats: the renderer stopped — $(grep -m1 'has stopped' "$log")" >&2
    exit 1
  fi
done

# Which of the two ways the order arrived does not matter here — the worker,
# or the renderer's own half-second fallback when a page will not schedule one
# — but that one of them did matters. Headless Chrome runs on a virtual clock,
# and a worker doing real work is often not scheduled before the shot is
# taken, so this is usually the fallback; on a real page it is the worker.
if ! grep -q '\[orblit\] splats: sort' "$OUT/splats_sorted.log"; then
  echo "check_web_splats: no sort ever landed — see $OUT/splats_sorted.log" >&2
  exit 1
fi
grep -h -o '\[orblit\] splats:.*' "$OUT/splats_sorted.log" | head -3 || true

python3 - "$OUT/splats_sorted.png" "$OUT/splats_unsorted.png" <<'PY'
import sys, zlib, struct

def pixels(path):
    """An 8-bit PNG as (width, height, bytes, channels). No PIL anywhere."""
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', f'{path} is not a PNG'
    at, idat, width, height, channels = 8, bytearray(), 0, 0, 0
    while at < len(data):
        length, kind = struct.unpack('>I4s', data[at:at + 8])
        body = data[at + 8:at + 8 + length]
        at += 12 + length
        if kind == b'IHDR':
            width, height, depth, colour, _, _, interlace = struct.unpack('>IIBBBBB', body)
            assert depth == 8 and interlace == 0, 'only plain 8-bit PNGs'
            channels = {0: 1, 2: 3, 4: 2, 6: 4}[colour]
        elif kind == b'IDAT':
            idat += body
        elif kind == b'IEND':
            break
    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    out = bytearray(height * stride)
    previous = bytearray(stride)
    at = 0
    for y in range(height):
        kind = raw[at]
        at += 1
        line = bytearray(raw[at:at + stride])
        at += stride
        if kind == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif kind == 2:
            for i in range(stride):
                line[i] = (line[i] + previous[i]) & 0xFF
        elif kind == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + previous[i]) >> 1)) & 0xFF
        elif kind == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = previous[i]
                c = previous[i - channels] if i >= channels else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                nearest = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + nearest) & 0xFF
        out[y * stride:(y + 1) * stride] = line
        previous = line
    return width, height, bytes(out), channels

def measure(path):
    width, height, data, channels = pixels(path)
    orange = teal = 0
    for i in range(0, len(data), channels):
        r, g, b = data[i], data[i + 1], data[i + 2]
        if r > 150 and r > b + 40 and 60 < g < 200:
            orange += 1
        elif b > 140 and b > r + 40 and g > 100:
            teal += 1
    total = width * height
    return width, height, data, orange / total, teal / total

sorted_path, unsorted_path = sys.argv[1], sys.argv[2]
width, height, first, orange, teal = measure(sorted_path)
_, _, second, _, _ = measure(unsorted_path)

changed = sum(1 for a, b in zip(first, second) if a != b) / len(first)
print(f'check_web_splats: {width}x{height}, orange {orange:.1%}, teal {teal:.1%}, '
      f'sorted against unsorted {changed:.1%} of channels differ')

failed = []
if orange < 0.02 or teal < 0.02:
    failed.append('the ring is missing: each of its colours should cover more than 2%')
if changed < 0.01:
    failed.append('sorted and unsorted are the same picture: no order reached the GPU')
if failed:
    for reason in failed:
        print(f'check_web_splats: {reason}', file=sys.stderr)
    sys.exit(1)
print('check_web_splats: the browser draws splats, and sorts them')
PY
