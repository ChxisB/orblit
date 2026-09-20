#!/bin/bash
# Builds the renderer core for the web: the same portable sources
# native/headless/build.sh compiles, plus this directory's web surface, as
# WebAssembly with Emscripten, linked against a Filament built for wasm.
#
# The point is the same as native/headless/build.sh's: what is left out.
# OrblitPlatformApple.mm, OrblitSurfaceApple.mm and the Objective-C wrapper are
# never compiled — the *.cpp glob already skips them — and now
# OrblitSurfaceHeadless.cpp is skipped too, deliberately: OrblitSurfaceWeb.cpp
# beside this script provides the same two factory functions
# (OrblitCreateHeadlessSurface, OrblitCreateWindowSurface) for what "a window"
# means in a browser, and both files defining them would not link.
#
# Two things this build needed that native/headless/build.sh did not, found
# by testing rather than guessed — see native/web/README.md's "What did not
# work at first":
#   - -fwasm-exceptions on every translation unit AND the final link.
#     Filament's own wasm archives throw utils::Panic without it (Renderer::
#     initWithWidth's try/catch depends on catching that), compiled with no
#     exception flag at all; proven by linking a throw from an unflagged
#     object file against a -fwasm-exceptions catch site before trusting it
#     with the real renderer.
#   - Every generated material header needs an opengl variant: WebGL 2 is
#     Filament's OpenGL backend, and matc's blob carries only the backends
#     it was told to (packages/orblit_filament/darwin/setup.sh,
#     ORBLIT_MATC_BACKENDS). This script does not compile them: run setup.sh
#     first with ORBLIT_GENERATED_SET=webgl2, ORBLIT_MATC_BACKENDS=opengl and
#     ORBLIT_MATC pointing at the matc of the Filament this links
#     ($ORBLIT_FILAMENT_WASM_SRC/out/cmake-release/tools/matc/matc) — see
#     README.md, step 3, for why a different matc draws without its sun.
#
#   build.sh                 builds ./build and host/orblit_renderer.{js,wasm}
#
# Needs, both required:
#   EMSDK                    an activated Emscripten SDK (see README.md)
#   ORBLIT_FILAMENT_WASM_SRC  a checkout of ChxisB/orblit-filament built
#                            for wasm: ./build.sh -p wasm release there first
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v emcc >/dev/null 2>&1; then
  echo "native/web/build.sh: no emcc on PATH. Activate emsdk first:" >&2
  echo "  source \"\$EMSDK_DIR/emsdk_env.sh\"" >&2
  exit 1
fi
if [ -z "${ORBLIT_FILAMENT_WASM_SRC:-}" ]; then
  echo "native/web/build.sh: ORBLIT_FILAMENT_WASM_SRC is not set. Point it at" >&2
  echo "  a checkout of ChxisB/orblit-filament built with" >&2
  echo "  ./build.sh -p wasm release (see README.md)." >&2
  exit 1
fi
FIL_SRC="$ORBLIT_FILAMENT_WASM_SRC"
FIL_OUT="$FIL_SRC/out/cmake-wasm-release"
if [ ! -d "$FIL_OUT" ]; then
  echo "native/web/build.sh: no $FIL_OUT; build Filament for wasm first." >&2
  exit 1
fi

DARWIN=../../darwin
SRC="$DARWIN/orblit_filament/Sources/orblit_filament_native"
OUT="${ORBLIT_BUILD_DIR:-build}"
GENERATED_SET="${ORBLIT_GENERATED_SET:-webgl2}"
GENERATED="$SRC/generated/$GENERATED_SET"
mkdir -p "$OUT" host

if [ ! -f "$GENERATED/lit_opaque_material.h" ]; then
  echo "native/web/build.sh: no compiled materials at $GENERATED" >&2
  echo "  run ORBLIT_GENERATED_SET=$GENERATED_SET bash packages/orblit_filament/darwin/setup.sh first" >&2
  exit 1
fi

# ---- Headers ----
#
# Not one merged include/ tree: unlike the packaged macOS/iOS releases
# darwin/setup.sh fetches, a source build's headers stay where CMake found
# them. This is the union of every -I flag Filament's own wasm build used
# (out/cmake-wasm-release/compile_commands.json, after `./build.sh -p wasm
# release` there), so anything Filament's public headers themselves reach
# for is covered, not only what the renderer's sources name directly.
INCLUDES=(
  -I "$FIL_SRC/filament/include"
  -I "$FIL_SRC/filament/backend/include"
  -I "$FIL_OUT/filament"
  -I "$FIL_OUT/filament/backend"
  -I "$FIL_SRC/libs/utils/include"
  -I "$FIL_SRC/libs/math/include"
  -I "$FIL_SRC/libs/filabridge/include"
  -I "$FIL_SRC/libs/filaflat/include"
  -I "$FIL_SRC/libs/geometry/include"
  -I "$FIL_SRC/libs/ibl/include"
  -I "$FIL_SRC/libs/image/include"
  -I "$FIL_SRC/libs/iblprefilter/include"
  -I "$FIL_SRC/libs/gltfio/include"
  # gltfio/materials/uberarchive.h is generated at build time (the packed
  # ubershader archive) straight under libs/gltfio/materials, not nested
  # inside an include/ directory the way the plain source headers are.
  -I "$FIL_OUT/libs"
  -I "$FIL_SRC/libs/ktxreader/include"
  -I "$FIL_SRC/third_party/robin-map/tnt/../include"
  -I "$FIL_SRC/third_party/abseil"
)

# ---- The core, the ABI, the portable platform layer and the plain C++
# helpers ----
#
# Every *.cpp beside the renderer, as native/headless/build.sh takes it,
# minus OrblitSurfaceHeadless.cpp (see the header comment above). ORBLIT_
# PLATFORM_PORTABLE is what selects OrblitPlatform.cpp's answers over
# OrblitPlatformApple.mm's — the same macro, the same effect, on any non-
# Apple build.
objects=()
for source in "$SRC"/*.cpp; do
  name="$(basename "$source" .cpp)"
  if [ "$name" = "OrblitSurfaceHeadless" ]; then continue; fi
  echo "native/web/build.sh: compiling $name"
  em++ -std=c++17 -O2 -DORBLIT_PLATFORM_PORTABLE -fwasm-exceptions \
    -Wall -Wno-deprecated-declarations -Wno-unused-private-field \
    "${INCLUDES[@]}" -I "$SRC" -I "$SRC/include" -I "$GENERATED" \
    -c "$source" -o "$OUT/$name.o"
  objects+=("$OUT/$name.o")
done

# This directory's own sources: the web surface, the JS-friendly wrapper, the
# splat sorter that runs on a Web Worker because there are no threads, and the
# decoding that does too — the jobs themselves (OrblitDecodeJobs, which the
# page runs when no worker will) and the renderer's side of the decoder
# workers (OrblitDecodersWeb).
BASISU_INCLUDE="$FIL_SRC/third_party/basisu/transcoder"
for name in OrblitSurfaceWeb orblit_web_host OrblitSplatSorterWeb \
            OrblitDecodeJobs OrblitDecodersWeb; do
  echo "native/web/build.sh: compiling $name"
  em++ -std=c++17 -O2 -DORBLIT_PLATFORM_PORTABLE -fwasm-exceptions \
    -Wall "${INCLUDES[@]}" -I "$SRC" -I "$SRC/include" -I "$GENERATED" \
    -I "$BASISU_INCLUDE" -c "$name.cpp" -o "$OUT/$name.o"
  objects+=("$OUT/$name.o")
done

# ---- Filament's own libraries, built for wasm ----
#
# Named the same as darwin/setup.sh's LIBS, minus bluegl/bluevk (the desktop
# OpenGL/Vulkan loaders PlatformWebGL never needs — Emscripten's own GL
# emulation is the loader here) and smol-v (SPIR-V compression for Metal/
# Vulkan shader variants; WebGL 2 takes GLSL source text, no SPIR-V in this
# build at all). Found by name rather than hand-written paths, because a
# from-source build's layout mirrors the CMake source tree exactly, one
# directory per library, rather than one flat lib/<arch>/ the way a packaged
# release is: finding "lib$name.a" is what stays true if that nesting shifts.
LIB_NAMES=(
  filament backend filabridge filaflat utils geometry ibl image
  filament-iblprefilter gltfio_core uberarchive uberzlib dracodec
  meshoptimizer ktxreader stb basis_transcoder mikktspace math zstd
)
archives=()
for lib in "${LIB_NAMES[@]}"; do
  found="$(find "$FIL_OUT" -name "lib$lib.a" -print -quit)"
  if [ -z "$found" ]; then
    echo "native/web/build.sh: no lib$lib.a under $FIL_OUT" >&2
    exit 1
  fi
  archives+=("$found")
done
# Abseil ships as one archive per component in a from-source build (a
# packaged release merges them; this does not), so every libabsl_*.a rather
# than one name.
while IFS= read -r absl; do archives+=("$absl"); done \
  < <(find "$FIL_OUT" -name "libabsl_*.a" | sort)

# ---- The decoder module ----
#
# What a decoder worker runs (orblit_decoder_module.cpp): the jobs and the
# pure readers under them, linked against the same zstd, stb and Basis
# Universal archives the renderer links, so a texture a worker decodes and one
# the page decodes are the same bytes. Nothing of Filament, which is what
# keeps it small. Its objects are built apart from the renderer's, without
# -fwasm-exceptions: nothing in them throws, and linking the exception runtime
# into a second module would only make it larger.
DECODER_OUT="$OUT/decoder"
mkdir -p "$DECODER_OUT"
decoder_objects=()
for source in "$SRC/OrblitKtx2.cpp" "$SRC/OrblitHdrImage.cpp" \
              "$SRC/OrblitTinyExr.cpp" "$SRC/OrblitEnvironmentBake.cpp" \
              OrblitDecodeJobs.cpp orblit_decoder_module.cpp; do
  name="$(basename "$source" .cpp)"
  echo "native/web/build.sh: compiling $name for the decoder module"
  em++ -std=c++17 -O2 -Wall -I "$SRC" -I "$BASISU_INCLUDE" \
    -c "$source" -o "$DECODER_OUT/$name.o"
  decoder_objects+=("$DECODER_OUT/$name.o")
done
decoder_archives=()
for lib in basis_transcoder zstd stb; do
  found="$(find "$FIL_OUT" -name "lib$lib.a" -print -quit)"
  if [ -z "$found" ]; then
    echo "native/web/build.sh: no lib$lib.a under $FIL_OUT" >&2
    exit 1
  fi
  decoder_archives+=("$found")
done
# ENVIRONMENT=worker, because a worker is the only place it runs; FILESYSTEM=0,
# because it is handed bytes. Its .wasm is given to it as wasmBinary, so
# nothing is fetched.
em++ -O2 "${decoder_objects[@]}" "${decoder_archives[@]}" \
  -s MODULARIZE=1 -s EXPORT_NAME=OrblitDecoderModule \
  -s ENVIRONMENT=worker -s ALLOW_MEMORY_GROWTH=1 -s FILESYSTEM=0 \
  -s EXPORTED_FUNCTIONS="['_malloc','_free']" \
  -s EXPORTED_RUNTIME_METHODS="['HEAPU8','HEAPF64','UTF8ToString','stringToUTF8','lengthBytesUTF8']" \
  -o "$DECODER_OUT/orblit_decoder.js"

# Embedded in the renderer rather than served beside it: the module's
# JavaScript as text, and its .wasm gzipped and in base64, which a worker
# gunzips with the browser's own DecompressionStream. Gzipped because base64
# of the raw module is a third larger than the module and compresses poorly
# on the wire; base64 of the gzipped one costs about what serving the .wasm
# would. Written with the node emsdk brings, so this needs nothing new.
"${EMSDK_NODE:-node}" - "$DECODER_OUT/orblit_decoder.js" \
  "$DECODER_OUT/orblit_decoder.wasm" "$OUT/orblit_decoder_embed.js" <<'NODE'
const fs = require('fs');
const zlib = require('zlib');
const [program, wasm, out] = process.argv.slice(2);
const gzipped = zlib.gzipSync(fs.readFileSync(wasm), { level: 9 });
fs.writeFileSync(out,
  '// Generated by native/web/build.sh from the decoder module; see\n' +
  '// orblit_decoder_workers.js.\n' +
  "Module['orblitDecoderSource'] = {\n" +
  '  program: ' + JSON.stringify(fs.readFileSync(program, 'utf8')) + ',\n' +
  "  wasm: '" + gzipped.toString('base64') + "',\n" +
  '};\n');
NODE

# ---- The ABI's own functions, exported by name rather than hand-listed ----
#
# Every orblit_renderer_* identifier orblit_renderer.h mentions, deduplicated:
# picks up a call added to the ABI later without this script needing to know
# its name, the same idea as the *.cpp glob above.
abi_funcs="$(grep -oE 'orblit_renderer_[a-zA-Z_]+' "$SRC/include/orblit_renderer.h" | sort -u)"
exported="_malloc,_free,_orblit_web_create_on_canvas"
for fn in $abi_funcs; do exported="$exported,_$fn"; done

# ---- Link ----
#
# --bind and its Embind runtime are Filament's own filament-js's, for calling
# C++ through generated JS classes; this ABI is plain C, so ccall/cwrap onto
# EXPORTED_FUNCTIONS is enough and the Embind weight is not carried. USE_
# WEBGL2/FULL_ES3/MIN_WEBGL_VERSION/MAX_WEBGL_VERSION are copied from
# web/filament-js/CMakeLists.txt's own LOPTS — the flags Filament's own web
# target links with — so this build's GL entry points match what its
# archives were built expecting.
#
# --pre-js puts orblit_splat_worker.js inside the module's factory, where
# OrblitSplatSorterWeb.cpp's EM_JS calls find it as Module.orblitSplatWorkers;
# and the decoder module, then orblit_decoder_workers.js, which
# OrblitDecodersWeb.cpp finds as Module.orblitDecoders.
em++ -fwasm-exceptions -O2 \
  "${objects[@]}" "${archives[@]}" \
  --pre-js orblit_splat_worker.js \
  --pre-js "$OUT/orblit_decoder_embed.js" \
  --pre-js orblit_decoder_workers.js \
  -s ALLOW_MEMORY_GROWTH=1 \
  -s USE_WEBGL2=1 -s FULL_ES3 -s MIN_WEBGL_VERSION=2 -s MAX_WEBGL_VERSION=2 \
  -s ENVIRONMENT=web \
  -s MODULARIZE=1 -s EXPORT_NAME=OrblitRendererModule \
  -s EXPORTED_FUNCTIONS="[$(echo "$exported" | sed "s/\([^,]*\)/'\1'/g")]" \
  -s EXPORTED_RUNTIME_METHODS="['ccall','cwrap','getValue','setValue','UTF8ToString','stringToUTF8','lengthBytesUTF8','HEAPU8','HEAP32','HEAPU32','HEAPF32','HEAPF64']" \
  -o host/orblit_renderer.js

ls -la host/orblit_renderer.js host/orblit_renderer.wasm \
  "$DECODER_OUT/orblit_decoder.js" "$DECODER_OUT/orblit_decoder.wasm"
echo "native/web/build.sh: built host/orblit_renderer.js and host/orblit_renderer.wasm"
