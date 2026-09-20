#include "OrblitImportInternal.h"

// The materials an FBX or OBJ holds, and the textures they name.
//
// ufbx presents every material as its PBR set whatever the file said, so what
// happens here is mostly choosing which of those maps glTF has a place for
// and saying what was dropped. Textures are shared by the image they come
// from rather than by the ufbx texture that named them, and an image is
// embedded or referenced by a URI depending on whether the file carried it.
//
// Part of `orblit::glb::Converter`; see OrblitImportInternal.h.

namespace orblit {
namespace glb {

int Converter::writeMaterial(const ufbx_material *material, bool hasUv,
                             const ufbx_mesh *mesh) {
  const auto key = std::make_pair(material->typed_id, hasUv);
  const auto found = materials_.find(key);
  if (found != materials_.end()) return found->second;

  const ufbx_material_pbr_maps &pbr = material->pbr;
  const std::string name = text(material->name);
  auto lose = [&](const std::string &what) {
    losses_.add(what + " of material '" + name + "'");
  };
  auto enabled = [](const ufbx_material_map &map) {
    return map.texture && map.texture_enabled ? map.texture : nullptr;
  };
  auto clamp01 = [](double value) {
    return std::isfinite(value) ? std::min(std::max(value, 0.0), 1.0) : 0.0;
  };

  // Textures need coordinates; a mesh without them gets this material
  // without its textures rather than a glTF file its validator rejects.
  std::string noUv;
  auto texture = [&](const ufbx_texture *t, const char *slot) {
    if (!t) return -1;
    if (!hasUv) {
      noUv += noUv.empty() ? slot : std::string(", ") + slot;
      return -1;
    }
    return writeTexture(t, slot, name);
  };

  // Base colour. A texture on the colour replaces the colour in FBX and
  // OBJ, as a connection does in Maya and Max, so the factor is white
  // there, scaled only by the separate weight.
  const ufbx_texture *baseTexture = enabled(pbr.base_color);
  double base[4] = {1, 1, 1, 1};
  if (!baseTexture && pbr.base_color.has_value) {
    base[0] = pbr.base_color.value_vec3.x;
    base[1] = pbr.base_color.value_vec3.y;
    base[2] = pbr.base_color.value_vec3.z;
  }
  if (pbr.base_factor.has_value) {
    for (int c = 0; c < 3; c++) base[c] *= pbr.base_factor.value_real;
  }

  // Opacity. Most shading models ufbx maps give it directly; plain FBX
  // Lambert and Phong keep it as transparency, where the exporter's own
  // "Opacity" is the value the FBX SDK itself shows.
  double opacity = 1;
  const ufbx_texture *opacityTexture = enabled(pbr.opacity);
  if (pbr.opacity.has_value) {
    opacity = pbr.opacity.value_real;
  } else if (material->shader_type == UFBX_SHADER_FBX_LAMBERT ||
             material->shader_type == UFBX_SHADER_FBX_PHONG) {
    const ufbx_prop *prop = ufbx_find_prop(&material->props, "Opacity");
    if (prop) {
      opacity = prop->value_real;
    } else if (material->fbx.transparency_color.has_value) {
      const ufbx_vec3 t = material->fbx.transparency_color.value_vec3;
      opacity = 1.0 - material->fbx.transparency_factor.value_real *
                          (t.x + t.y + t.z) / 3.0;
    }
    if (!opacityTexture) {
      opacityTexture = enabled(material->fbx.transparency_color);
    }
    if (!opacityTexture) {
      opacityTexture = enabled(material->fbx.transparency_factor);
    }
  }
  opacity = std::isfinite(opacity) ? clamp01(opacity) : 1.0;
  base[3] = opacity;

  const char *alphaMode = nullptr;
  if (opacity < 1.0 - 1e-6) {
    alphaMode = "BLEND";
  }
  if (opacityTexture) {
    // glTF reads opacity from the base colour's alpha. An opacity map that
    // is that same image — the usual way a cut-out is authored — is a
    // mask; a separate one would need its channel copied across.
    if (baseTexture && sameImage(baseTexture, opacityTexture)) {
      if (!alphaMode) alphaMode = "MASK";
    } else {
      lose("opacity texture dropped (glTF takes opacity from the base "
           "colour's alpha)");
    }
  }

  const double metallic =
      pbr.metalness.has_value ? clamp01(pbr.metalness.value_real) : 0.0;
  const double roughness =
      pbr.roughness.has_value ? clamp01(pbr.roughness.value_real) : 1.0;
  // glTF packs both into one texture's blue and green channels, which
  // takes image processing this importer does not do.
  if (enabled(pbr.metalness)) lose("metalness texture dropped (factor kept)");
  if (enabled(pbr.roughness)) lose("roughness texture dropped (factor kept)");
  if (enabled(pbr.glossiness)) lose("glossiness texture dropped (factor kept)");
  if (enabled(pbr.specular_color) || enabled(pbr.specular_factor)) {
    lose("specular texture dropped");
  }

  double emissive[3] = {0, 0, 0};
  const ufbx_texture *emissiveTexture = enabled(pbr.emission_color);
  if (emissiveTexture) {
    emissive[0] = emissive[1] = emissive[2] = 1;
  } else if (pbr.emission_color.has_value) {
    emissive[0] = pbr.emission_color.value_vec3.x;
    emissive[1] = pbr.emission_color.value_vec3.y;
    emissive[2] = pbr.emission_color.value_vec3.z;
  }
  if (pbr.emission_factor.has_value) {
    for (double &e : emissive) e *= pbr.emission_factor.value_real;
  }
  double strength = 1;
  for (double &e : emissive) {
    if (!std::isfinite(e) || e < 0) e = 0;
    strength = std::max(strength, e);
  }
  if (strength > 1) {
    for (double &e : emissive) e /= strength;
  }

  const ufbx_texture *normalTexture = enabled(pbr.normal_map);
  if (!normalTexture && enabled(material->fbx.bump)) {
    lose("bump (height) map dropped (glTF takes a normal map)");
  }
  const ufbx_texture *occlusionTexture = enabled(pbr.ambient_occlusion);

  std::string json = "{";
  if (!name.empty()) {
    json += "\"name\":";
    putString(json, name);
    json += ',';
  }
  json += "\"pbrMetallicRoughness\":{\"baseColorFactor\":[";
  for (int c = 0; c < 4; c++) {
    if (c > 0) json += ',';
    putFloat(json, clamp01(base[c]));
  }
  json += ']';
  const int baseIndex = texture(baseTexture, "base colour");
  if (baseIndex >= 0) {
    json += ",\"baseColorTexture\":{\"index\":" + std::to_string(baseIndex) + "}";
  }
  json += ",\"metallicFactor\":";
  putFloat(json, metallic);
  json += ",\"roughnessFactor\":";
  putFloat(json, roughness);
  json += '}';

  const int normalIndex = texture(normalTexture, "normal");
  if (normalIndex >= 0) {
    json += ",\"normalTexture\":{\"index\":" + std::to_string(normalIndex) + "}";
  }
  const int occlusionIndex = texture(occlusionTexture, "occlusion");
  if (occlusionIndex >= 0) {
    json += ",\"occlusionTexture\":{\"index\":" +
            std::to_string(occlusionIndex) + "}";
  }
  const int emissiveIndex = texture(emissiveTexture, "emissive");
  if (emissiveIndex >= 0) {
    json += ",\"emissiveTexture\":{\"index\":" +
            std::to_string(emissiveIndex) + "}";
  }
  if (emissive[0] > 0 || emissive[1] > 0 || emissive[2] > 0) {
    json += ",\"emissiveFactor\":";
    putFloats(json, emissive, 3);
  }
  // A MASK drawn from a texture the mesh cannot sample is no mask.
  if (alphaMode && !(std::strcmp(alphaMode, "MASK") == 0 && baseIndex < 0)) {
    json += ",\"alphaMode\":\"";
    json += alphaMode;
    json += '"';
  }
  if (material->features.double_sided.enabled) json += ",\"doubleSided\":true";

  std::string extensions;
  if (strength > 1) {
    extensions += "\"KHR_materials_emissive_strength\":{\"emissiveStrength\":";
    putFloat(extensions, strength);
    extensions += '}';
    doc_.extensionsUsed.insert("KHR_materials_emissive_strength");
  }
  if (material->features.unlit.enabled) {
    if (!extensions.empty()) extensions += ',';
    extensions += "\"KHR_materials_unlit\":{}";
    doc_.extensionsUsed.insert("KHR_materials_unlit");
  }
  if (!extensions.empty()) json += ",\"extensions\":{" + extensions + "}";
  json += '}';

  if (!noUv.empty()) {
    losses_.add("mesh '" + text(mesh->name) +
                "' has no texture coordinates, so the " + noUv +
                " texture(s) of material '" + name + "' are not applied");
  }

  doc_.materials.push_back(std::move(json));
  const int index = int(doc_.materials.size() - 1);
  materials_.emplace(key, index);
  return index;
}

/// The file a texture stands for: itself, or the first file of a layered
/// or shader texture.
const ufbx_texture *Converter::fileOf(const ufbx_texture *t) {
  if (t->type == UFBX_TEXTURE_FILE) return t;
  return t->file_textures.count > 0 ? t->file_textures.data[0] : nullptr;
}

bool Converter::sameImage(const ufbx_texture *a, const ufbx_texture *b) const {
  a = fileOf(a);
  b = fileOf(b);
  if (!a || !b) return false;
  if (a == b) return true;
  if (a->has_file && b->has_file) return a->file_index == b->file_index;
  return text(a->filename) == text(b->filename) &&
         text(a->relative_filename) == text(b->relative_filename);
}

int Converter::writeTexture(const ufbx_texture *given, const char *slot,
                            const std::string &materialName) {
  const ufbx_texture *file = fileOf(given);
  if (!file) {
    losses_.add(std::string(slot) + " texture of material '" + materialName +
                "' is procedural; dropped");
    return -1;
  }
  if (file != given) {
    losses_.add(std::string(slot) + " texture of material '" + materialName +
                "' is layered; only its first layer is kept");
  }
  if (file->has_uv_transform) {
    losses_.add(std::string(slot) + " texture of material '" + materialName +
                "' has a UV transform, which is not carried");
  }

  bool ktx2 = false;
  const int image = writeImage(file, ktx2);
  if (image < 0) return -1;

  // glTF's REPEAT is its default, so only a clamp needs saying.
  const int wrapS = file->wrap_u == UFBX_WRAP_CLAMP ? 33071 : 10497;
  const int wrapT = file->wrap_v == UFBX_WRAP_CLAMP ? 33071 : 10497;
  const auto samplerKey = std::make_pair(wrapS, wrapT);
  auto sampler = samplers_.find(samplerKey);
  if (sampler == samplers_.end()) {
    std::string json = "{";
    if (wrapS != 10497 || wrapT != 10497) {
      json += "\"wrapS\":" + std::to_string(wrapS) +
              ",\"wrapT\":" + std::to_string(wrapT);
    }
    json += '}';
    doc_.samplers.push_back(std::move(json));
    sampler = samplers_.emplace(samplerKey, int(doc_.samplers.size() - 1)).first;
  }

  const auto key = std::make_pair(image, sampler->second);
  const auto found = textures_.find(key);
  if (found != textures_.end()) return found->second;
  std::string json = "{\"sampler\":" + std::to_string(sampler->second);
  if (ktx2) {
    json += ",\"extensions\":{\"KHR_texture_basisu\":{\"source\":" +
            std::to_string(image) + "}}";
    doc_.extensionsUsed.insert("KHR_texture_basisu");
    doc_.extensionsRequired.insert("KHR_texture_basisu");
  } else {
    json += ",\"source\":" + std::to_string(image);
  }
  json += '}';
  doc_.textures.push_back(std::move(json));
  const int index = int(doc_.textures.size() - 1);
  textures_.emplace(key, index);
  return index;
}

int Converter::writeImage(const ufbx_texture *file, bool &ktx2) {
  ufbx_blob content = file->content;
  if (content.size == 0 && file->has_file &&
      file->file_index < scene_->texture_files.count) {
    content = scene_->texture_files.data[file->file_index].content;
  }
  const std::string label =
      file->relative_filename.length > 0 ? text(file->relative_filename)
      : file->filename.length > 0        ? text(file->filename)
                                         : text(file->name);

  std::string key;
  std::string json = "{";
  const std::string shownName = baseName(label);
  if (!shownName.empty()) {
    json += "\"name\":";
    putString(json, shownName);
    json += ',';
  }

  if (content.size > 0 && content.data) {
    const uint8_t *bytes = static_cast<const uint8_t *>(content.data);
    const char *type = sniffImage(bytes, content.size);
    if (!type) {
      losses_.add("embedded texture '" + shownName +
                  "' is not PNG, JPEG or KTX2; dropped");
      return -1;
    }
    // Two textures can share one embedded file; ufbx numbers the files.
    key = file->has_file ? "file:" + std::to_string(file->file_index)
                         : "texture:" + std::to_string(file->typed_id);
    const auto found = images_.find(key);
    if (found != images_.end()) {
      ktx2 = found->second.second;
      return found->second.first;
    }
    const uint32_t view = doc_.view(bytes, content.size, 0);
    json += "\"bufferView\":" + std::to_string(view) + ",\"mimeType\":\"" +
            type + "\"}";
    ktx2 = std::strcmp(type, "image/ktx2") == 0;
    embeddedImages_++;
  } else {
    // Referenced, never read: the path the file gave relative to itself,
    // or failing that just the file's name, beside the source.
    std::string path = text(file->relative_filename);
    std::replace(path.begin(), path.end(), '\\', '/');
    if (path.empty() || looksAbsolute(path)) {
      std::string whole = text(file->filename);
      if (whole.empty()) whole = text(file->absolute_filename);
      if (whole.empty()) whole = path;
      path = baseName(whole);
    }
    while (path.compare(0, 2, "./") == 0) path.erase(0, 2);
    if (path.empty()) {
      losses_.add("a texture of '" + text(file->name) +
                  "' names no file; dropped");
      return -1;
    }
    const char *type = imageTypeOf(path);
    if (!type) {
      losses_.add("texture '" + path +
                  "' is not PNG, JPEG or KTX2, which is all glTF takes; "
                  "dropped");
      return -1;
    }
    key = "uri:" + path;
    const auto found = images_.find(key);
    if (found != images_.end()) {
      ktx2 = found->second.second;
      return found->second.first;
    }
    json += "\"uri\":";
    putString(json, percentEncode(path));
    json += ",\"mimeType\":\"";
    json += type;
    json += "\"}";
    ktx2 = std::strcmp(type, "image/ktx2") == 0;
    referencedImages_++;
  }
  doc_.images.push_back(std::move(json));
  const int index = int(doc_.images.size() - 1);
  images_.emplace(key, std::make_pair(index, ktx2));
  return index;
}

}  // namespace glb
}  // namespace orblit
