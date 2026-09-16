#!/bin/bash
# Fetches the models the import work is checked against: Khronos's own glTF
# samples, one for each thing a file can hold beyond its geometry.
#
#   Fox                        three clips on one skin (CC BY 4.0 and CC0)
#   CesiumMan                  a walk cycle (CC BY 4.0, Cesium)
#   RiggedSimple               the smallest skin there is (CC BY 4.0)
#   MaterialsVariantsShoe      KHR_materials_variants (CC BY 4.0, Shopify)
#   LightsPunctualLamp         KHR_lights_punctual (CC BY 4.0)
#   ClearCoatTest, SheenChair  KHR_materials_clearcoat and _sheen
#   AnisotropyBarnLamp         an extension the renderer does not draw, to
#                              check that it says so (CC BY 4.0)
#
# And files that are not glTF, which the renderer converts on the way in:
#
#   Samba Dancing.fbx          a Mixamo character with its dance, as three.js
#                              carries it in its examples
#   male02/                    an OBJ with its material library and three
#                              JPEGs beside it, also from three.js's examples
#   maya_character, blender_279_sausage, blender_293_embedded_textures
#                              from ufbx's own test data: Maya and Blender
#                              exports, and textures carried inside the FBX
#
# Each model's own licence is in its README in the repository it comes from:
# https://github.com/KhronosGroup/glTF-Sample-Assets — not ours, so kept out
# of git and fetched on demand, like the Bistro.
#
# About 25 MB, into assets/samples. Used by the gallery's Imported models
# example and by native/headless/orblit_models_check:
#
#   ORBLIT_SAMPLES=assets/samples native/headless/build/orblit_models_check
set -euo pipefail
cd "$(dirname "$0")/.."

INTO="assets/samples"
FROM="https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models"
MODELS=(Fox CesiumMan RiggedSimple MaterialsVariantsShoe LightsPunctualLamp
        ClearCoatTest SheenChair AnisotropyBarnLamp)

# get <url> <file under $INTO> — skips what is already there. Into a
# temporary name and then moved, so an interrupted download is not left
# looking like a whole file that the next run skips.
get() {
  local url="$1"
  local out="$INTO/$2"
  if [ -s "$out" ]; then return; fi
  echo "  $2"
  mkdir -p "$(dirname "$out")"
  curl -fsSL -o "$out.part" "$url"
  mv "$out.part" "$out"
}

mkdir -p "$INTO"
for model in "${MODELS[@]}"; do
  get "$FROM/$model/glTF-Binary/$model.glb" "$model.glb"
done

THREE="https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/models"
get "$THREE/fbx/Samba%20Dancing.fbx" "Samba Dancing.fbx"
for file in male02.obj male02.mtl 01_-_Default1noCulling.JPG \
            male-02-1noCulling.JPG orig_02_-_Defaul1noCulling.JPG; do
  get "$THREE/obj/male02/$file" "male02/$file"
done

UFBX="https://raw.githubusercontent.com/ufbx/ufbx/master/data"
for file in maya_character_7500_binary.fbx blender_279_sausage_7400_binary.fbx \
            blender_293_embedded_textures_7400_binary.fbx; do
  get "$UFBX/$file" "$file"
done
echo "fetched into $INTO"
