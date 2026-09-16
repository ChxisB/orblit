// Models out of files: what one holds, and what its own animation does to it.
//
// The renderer part of glTF import beyond geometry — clips, skins, material
// variants, the lights and cameras a file carries — kept out of
// OrblitRendererCore.cpp, which is long enough, and in one place because each
// of these is a question about the same loaded file.
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <map>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_set>
#include <utility>
#include <vector>

#include <filament/Camera.h>
#include <filament/LightManager.h>
#include <filament/MaterialInstance.h>
#include <filament/RenderableManager.h>
#include <filament/TransformManager.h>
#include <gltfio/Animator.h>
#include <utils/EntityManager.h>

#include "OrblitImport.h"
#include "OrblitRendererCore.h"

namespace orblit {

using filament::Box;
using filament::Camera;
using filament::LightManager;
using filament::VertexAttribute;

namespace {

/// The glTF extensions gltfio's loader and ubershader materials draw, as of
/// Filament 1.76. Anything else a file says it uses is reported rather than
/// silently ignored — a clear-coat car and an anisotropic one look alike to a
/// loader that skips the extension, and very different to the person who made
/// them. The material side is libs/gltfio/materials/*.spec.in.
const char *const kDrawnExtensions[] = {
    "KHR_draco_mesh_compression",
    "KHR_lights_punctual",
    "KHR_materials_clearcoat",
    "KHR_materials_emissive_strength",
    "KHR_materials_ior",
    "KHR_materials_pbrSpecularGlossiness",
    "KHR_materials_sheen",
    "KHR_materials_specular",
    "KHR_materials_transmission",
    "KHR_materials_unlit",
    "KHR_materials_variants",
    "KHR_materials_volume",
    "KHR_mesh_quantization",
    "KHR_texture_basisu",
    "KHR_texture_transform",
};

bool isDrawn(const std::string &extension) {
  for (const char *known : kDrawnExtensions) {
    if (extension == known) return true;
  }
  return false;
}

/// The JSON of a .gltf, or of a .glb's first chunk; empty if it is neither.
std::string jsonOf(const uint8_t *bytes, size_t size) {
  if (bytes == nullptr || size < 4) return {};
  const auto u32 = [bytes](size_t at) {
    return uint32_t(bytes[at]) | uint32_t(bytes[at + 1]) << 8 |
           uint32_t(bytes[at + 2]) << 16 | uint32_t(bytes[at + 3]) << 24;
  };
  if (u32(0) == 0x46546C67) {  // "glTF"
    if (size < 20 || u32(16) != 0x4E4F534A) return {};  // "JSON"
    const size_t length = u32(12);
    if (length > size - 20) return {};
    return std::string(reinterpret_cast<const char *>(bytes + 20), length);
  }
  return std::string(reinterpret_cast<const char *>(bytes), size);
}

/// The strings in the top level's "extensionsUsed", read with just enough of
/// a JSON reader to find them: strings are skipped whole, escapes and all, so
/// a material named "extensionsUsed" or a brace inside a name cannot fool it.
std::vector<std::string> extensionsUsed(const std::string &json) {
  std::vector<std::string> found;
  int depth = 0;
  size_t i = 0;
  const size_t n = json.size();
  // Reads the string whose opening quote i is on, leaving i past its close.
  const auto readString = [&](std::string *into) {
    for (i++; i < n; i++) {
      const char c = json[i];
      if (c == '\\') {
        i++;
        continue;
      }
      if (c == '"') {
        i++;
        return;
      }
      if (into != nullptr) into->push_back(c);
    }
  };
  while (i < n) {
    const char c = json[i];
    if (c == '"') {
      std::string key;
      readString(depth == 1 ? &key : nullptr);
      if (depth != 1 || key != "extensionsUsed") continue;
      while (i < n && json[i] != '[' && json[i] != '{' && json[i] != '"') i++;
      if (i >= n || json[i] != '[') continue;
      for (i++; i < n && json[i] != ']';) {
        if (json[i] == '"') {
          std::string name;
          readString(&name);
          found.push_back(name);
        } else {
          i++;
        }
      }
      continue;
    }
    if (c == '{' || c == '[') depth++;
    if (c == '}' || c == ']') depth--;
    i++;
  }
  return found;
}

void appendText(std::string &out, const char *text) {
  out.push_back('"');
  for (const char *c = text != nullptr ? text : ""; *c != 0; c++) {
    const unsigned char u = static_cast<unsigned char>(*c);
    switch (u) {
      case '"': out += "\\\""; break;
      case '\\': out += "\\\\"; break;
      case '\n': out += "\\n"; break;
      case '\r': out += "\\r"; break;
      case '\t': out += "\\t"; break;
      default:
        if (u < 0x20) {
          char escaped[8];
          std::snprintf(escaped, sizeof escaped, "\\u%04x", u);
          out += escaped;
        } else {
          out.push_back(char(u));
        }
    }
  }
  out.push_back('"');
}

/// A number JSON can hold. Infinity and NaN are not JSON, and a camera with no
/// far plane is a real thing a file can say, so those become null.
void appendNumber(std::string &out, double value) {
  if (!std::isfinite(value)) {
    out += "null";
    return;
  }
  char text[32];
  std::snprintf(text, sizeof text, "%.9g", value);
  out += text;
}

void appendNumbers(std::string &out, const float *values, size_t count) {
  out.push_back('[');
  for (size_t i = 0; i < count; i++) {
    if (i > 0) out.push_back(',');
    appendNumber(out, values[i]);
  }
  out.push_back(']');
}

void appendMatrix(std::string &out, const mat4f &m) {
  float values[16];
  std::memcpy(values, &m, sizeof values);
  appendNumbers(out, values, 16);
}

/// Where a clip is at `lead` seconds of the host's clock past the moment it
/// was stated, wrapped or held at its ends.
float clipTime(const Played &played, double lead, float duration) {
  if (duration <= 0) return 0;
  double t = double(played.seconds) + double(played.speed) * lead;
  if (played.loops) {
    t = std::fmod(t, double(duration));
    if (t < 0) t += duration;
  } else {
    t = std::clamp(t, 0.0, double(duration));
  }
  return float(t);
}

/// What became of converting one file, by the path a scene named it by.
///
/// For the whole process rather than one renderer, like the bytes the result
/// is kept as: a second view of the same scene should not convert the same
/// character again.
struct Conversion {
  bool finished = false;
  std::string note;
  std::vector<std::string> losses;
};

std::mutex &conversionLock() {
  static std::mutex lock;
  return lock;
}

std::map<std::string, Conversion> &conversions() {
  static std::map<std::string, Conversion> all;
  return all;
}

/// Where a converted file's GLB is kept, beside the bytes hosts provide.
constexpr const char *kConvertedPrefix = "orblit:converted/";

/// Converts one file and keeps what comes of it. Kept even when it failed,
/// as no bytes at all: keeping anything moves the resource generation on, and
/// that is what sends the object waiting for this back to look again.
void convert(const std::string &path, const SharedBytes &source) {
  Imported imported = importToGlb(
      source->data(), source->size(), path,
      [](const std::string &companion) { return readResource(companion); });
  {
    std::lock_guard<std::mutex> hold(conversionLock());
    Conversion &done = conversions()[path];
    done.finished = true;
    done.note = std::move(imported.note);
    done.losses = std::move(imported.losses);
  }
  provideResource(std::string(kConvertedPrefix) + path,
                  std::move(imported.glb));
}

/// How far a frame may sample past, or before, the moment a pose was stated.
/// The same quarter second the camera takes for a pause rather than a rate: a
/// host that has stopped describing a pose has stopped its clip with it.
constexpr double kMostLead = 0.25;

}  // namespace

/// The GLB an FBX or OBJ becomes, or null while it is becoming one or if it
/// could not.
///
/// Converted off the thread that draws, because a character with a few
/// thousand keyframes is a few hundred milliseconds of work and a frame is
/// sixteen: the object stands as the placeholder until the GLB is kept, and
/// keeping it is what makes the next publish build the real thing. In a
/// browser there is no thread to put it on, so it is converted where it is
/// asked for — once, and then kept like everywhere else.
///
/// `carried` is set, on success, to what the conversion could not carry
/// across, for the notes.
SharedBytes Renderer::convertedModel(const std::string &path,
                                     const SharedBytes &source,
                                     std::string &carried) {
  const std::string name = std::string(kConvertedPrefix) + path;
  bool start = false;
  {
    std::lock_guard<std::mutex> hold(conversionLock());
    auto found = conversions().find(path);
    if (found == conversions().end()) {
      conversions()[path];
      start = true;
    } else if (found->second.finished) {
      SharedBytes glb = findResource(name);
      if (glb && !glb->empty()) {
        const auto &losses = found->second.losses;
        if (!losses.empty()) {
          std::string listed;
          for (size_t i = 0; i < losses.size() && i < 3; i++) {
            listed += (i == 0 ? "" : "; ") + losses[i];
          }
          carried = losses.size() > 3
                        ? orblit::format("Converted to glTF without: %s; and "
                                         "%zu more.",
                                         listed.c_str(), losses.size() - 3)
                        : "Converted to glTF without: " + listed + ".";
        }
        return glb;
      }
      _assetNotes[path] =
          "It could not be converted to glTF: " + found->second.note;
      return nullptr;
    }
  }

  if (start) {
#ifdef __EMSCRIPTEN__
    convert(path, source);
#else
    bool threaded = false;
    try {
      std::thread([path, source] { convert(path, source); }).detach();
      threaded = true;
    } catch (const std::exception &) {
      // A thread that will not start is not a reason to stop the renderer.
    }
    if (!threaded) convert(path, source);
#endif
    std::lock_guard<std::mutex> hold(conversionLock());
    if (conversions()[path].finished) {
      // Converted where it stood, so it can be used now.
    } else {
      _assetNotes[path] =
          "It is being converted to glTF, and draws once that has finished.";
      return nullptr;
    }
  } else {
    _assetNotes[path] =
        "It is being converted to glTF, and draws once that has finished.";
    return nullptr;
  }
  return convertedModel(path, source, carried);
}

/// What is kept of a freshly loaded file to put its copies back later: every
/// node's own transform, and every entity's declared box.
void Renderer::readModel(Mesh &mesh, gltfio::FilamentInstance *first) {
  auto &transforms = _engine->getTransformManager();
  auto &renderables = _engine->getRenderableManager();
  const utils::Entity *entities = first->getEntities();
  const size_t count = first->getEntityCount();

  mesh.rest.assign(count, mat4f());
  mesh.boxes.assign(count, Box());
  mesh.skinned.clear();
  for (size_t i = 0; i < count; i++) {
    const auto node = transforms.getInstance(entities[i]);
    if (node) mesh.rest[i] = transforms.getTransform(node);
    const auto renderable = renderables.getInstance(entities[i]);
    if (!renderable) continue;
    mesh.boxes[i] = renderables.getAxisAlignedBoundingBox(renderable);
    if (renderables.getPrimitiveCount(renderable) > 0 &&
        renderables.getEnabledAttributesAt(renderable, 0)
            .test(VertexAttribute::BONE_INDICES)) {
      mesh.skinned.push_back(uint32_t(i));
    }
  }
}

/// Takes the lights a file brought out of one copy of it.
///
/// gltfio makes a Filament light for every KHR_lights_punctual node, and left
/// alone they draw: with no shadows, in candela rather than the units every
/// other light here is stated in, uncounted against the scene's light budget —
/// and a directional one competing with the scene's own sun for the single
/// slot Filament has. So they are taken out, and a host that wants them reads
/// them from the model's description and states them as ordinary lights,
/// where they share one lighting path with everything else.
void Renderer::dropFileLights(gltfio::FilamentInstance *instance) {
  if (instance == nullptr) return;
  auto &lights = _engine->getLightManager();
  const utils::Entity *entities = instance->getEntities();
  const size_t count = instance->getEntityCount();
  for (size_t i = 0; i < count; i++) {
    if (lights.hasComponent(entities[i])) lights.destroy(entities[i]);
  }
}

/// Writes down what a file holds, as the JSON a host reads back.
///
/// From the loaded asset rather than from the file: an FBX has become a glTF
/// by the time it is here, and what a host wants to know is what the renderer
/// actually has — the clips it can play, the joints it can set — not what a
/// parser thinks the file says.
void Renderer::describeModel(Mesh &mesh, gltfio::FilamentInstance *first,
                             const std::string &path, const uint8_t *bytes,
                             size_t size) {
  auto &transforms = _engine->getTransformManager();
  auto &lights = _engine->getLightManager();
  const utils::Entity *entities = first->getEntities();
  const size_t count = first->getEntityCount();

  const auto nameOf = [this](utils::Entity entity) -> const char * {
    if (_names == nullptr) return "";
    const auto instance = _names->getInstance(entity);
    return instance ? _names->getName(instance) : "";
  };
  // Relative to the model's own root, so a host can put it wherever the
  // object stands.
  const auto root = transforms.getInstance(first->getRoot());
  const mat4f fromRoot =
      root ? inverse(transforms.getWorldTransform(root)) : mat4f();
  const auto placementOf = [&](utils::Entity entity) {
    const auto node = transforms.getInstance(entity);
    return node ? fromRoot * transforms.getWorldTransform(node) : mat4f();
  };

  std::string out = "{\"clips\":[";
  if (auto *animator = first->getAnimator()) {
    for (size_t i = 0; i < animator->getAnimationCount(); i++) {
      if (i > 0) out.push_back(',');
      out += "{\"name\":";
      appendText(out, animator->getAnimationName(i));
      out += ",\"seconds\":";
      appendNumber(out, animator->getAnimationDuration(i));
      out.push_back('}');
    }
  }

  out += "],\"skins\":[";
  for (size_t s = 0; s < first->getSkinCount(); s++) {
    if (s > 0) out.push_back(',');
    out += "{\"name\":";
    appendText(out, first->getSkinNameAt(s));
    out += ",\"joints\":[";
    const utils::Entity *joints = first->getJointsAt(s);
    const size_t jointCount = first->getJointCountAt(s);
    for (size_t j = 0; j < jointCount; j++) {
      if (j > 0) out.push_back(',');
      appendText(out, nameOf(joints[j]));
    }
    // Which joint of this skin each hangs from, -1 for one hanging from
    // something that is not: the nearest ancestor that is a joint, because a
    // file may put plain nodes between two joints.
    out += "],\"parents\":[";
    for (size_t j = 0; j < jointCount; j++) {
      if (j > 0) out.push_back(',');
      int32_t parent = -1;
      auto node = transforms.getInstance(joints[j]);
      for (utils::Entity up = node ? transforms.getParent(node) : utils::Entity();
           up && parent < 0;) {
        for (size_t k = 0; k < jointCount; k++) {
          if (joints[k] == up) parent = int32_t(k);
        }
        const auto above = transforms.getInstance(up);
        up = above ? transforms.getParent(above) : utils::Entity();
      }
      appendNumber(out, parent);
    }
    // Every joint's rest, twice: where it stands relative to the model's root,
    // and relative to its own parent node. A rig solved in Dart poses joints
    // in the first, and a joint is set by hand in the second — and a joint
    // whose parent is not a joint has no other way to say where that parent
    // is. As the file left them, from `rest`, not as anything has moved them.
    out += "],\"rest\":[";
    for (size_t j = 0; j < jointCount; j++) {
      if (j > 0) out.push_back(',');
      appendMatrix(out, placementOf(joints[j]));
    }
    out += "],\"local\":[";
    for (size_t j = 0; j < jointCount; j++) {
      if (j > 0) out.push_back(',');
      const auto node = transforms.getInstance(joints[j]);
      appendMatrix(out, node ? transforms.getTransform(node) : mat4f());
    }
    out += "]}";
  }

  out += "],\"variants\":[";
  for (size_t v = 0; v < first->getMaterialVariantCount(); v++) {
    if (v > 0) out.push_back(',');
    appendText(out, first->getMaterialVariantName(v));
  }

  out += "],\"materials\":[";
  {
    // One name per material instance the file made; gltfio shares an
    // instance between primitives that use the same material.
    const auto *materials = first->getMaterialInstances();
    for (size_t m = 0; m < first->getMaterialInstanceCount(); m++) {
      if (m > 0) out.push_back(',');
      appendText(out, materials[m] != nullptr ? materials[m]->getName() : "");
    }
  }

  out += "],\"lights\":[";
  bool firstLight = true;
  for (size_t i = 0; i < count; i++) {
    const auto light = lights.getInstance(entities[i]);
    if (!light) continue;
    if (!firstLight) out.push_back(',');
    firstLight = false;

    // In OrblitLight's own terms: its kinds, and lux for a sun or lumens for
    // everything else. Filament answers in candela for a point or a spot, and
    // for a focused spot the lumens depend on the outer cone — the same
    // conversion Filament makes the other way when a light is built.
    const LightManager::Type type = lights.getType(light);
    const bool directional = type == LightManager::Type::DIRECTIONAL ||
                             type == LightManager::Type::SUN;
    const bool spot = type == LightManager::Type::SPOT ||
                      type == LightManager::Type::FOCUSED_SPOT;
    const float outer = spot ? lights.getSpotLightOuterCone(light) : 0.0f;
    const float inner = spot ? lights.getSpotLightInnerCone(light) : 0.0f;
    const float candela = lights.getIntensity(light);
    constexpr float kTau = 6.28318530718f;
    const float intensity =
        directional ? candela
        : spot      ? candela * kTau * (1.0f - std::cos(outer))
                    : candela * 2.0f * kTau;
    const float3 colour = lights.getColor(light);

    out += "{\"name\":";
    appendText(out, nameOf(entities[i]));
    out += ",\"kind\":";
    appendNumber(out, directional ? 0 : spot ? 2 : 1);
    out += ",\"colour\":";
    const float rgb[3] = {colour.r, colour.g, colour.b};
    appendNumbers(out, rgb, 3);
    out += ",\"intensity\":";
    appendNumber(out, intensity);
    out += ",\"falloff\":";
    appendNumber(out, directional ? 0 : lights.getFalloff(light));
    out += ",\"inner\":";
    appendNumber(out, inner);
    out += ",\"outer\":";
    appendNumber(out, outer);
    out += ",\"transform\":";
    appendMatrix(out, placementOf(entities[i]));
    out.push_back('}');
  }

  out += "],\"cameras\":[";
  bool firstCamera = true;
  for (size_t i = 0; i < count; i++) {
    const Camera *camera = _engine->getCameraComponent(entities[i]);
    if (camera == nullptr) continue;
    if (!firstCamera) out.push_back(',');
    firstCamera = false;
    const auto projection = camera->getProjectionMatrix();
    // A perspective projection has nought in its bottom-right corner; an
    // orthographic one has one there.
    const bool orthographic = projection[3][3] == 1.0;
    out += "{\"name\":";
    appendText(out, nameOf(entities[i]));
    out += ",\"orthographic\":";
    out += orthographic ? "true" : "false";
    out += ",\"fieldOfView\":";
    appendNumber(out, orthographic ? 0.0
                                   : camera->getFieldOfViewInDegrees(
                                         Camera::Fov::VERTICAL));
    out += ",\"viewHeight\":";
    appendNumber(out, orthographic && projection[1][1] != 0.0
                          ? 2.0 / projection[1][1]
                          : 0.0);
    out += ",\"near\":";
    appendNumber(out, camera->getNear());
    out += ",\"far\":";
    appendNumber(out, camera->getCullingFar());
    out += ",\"transform\":";
    appendMatrix(out, placementOf(entities[i]));
    out.push_back('}');
  }

  out += "],\"bounds\":{\"min\":";
  const auto box = mesh.asset->getBoundingBox();
  const float least[3] = {box.min.x, box.min.y, box.min.z};
  const float most[3] = {box.max.x, box.max.y, box.max.z};
  appendNumbers(out, least, 3);
  out += ",\"max\":";
  appendNumbers(out, most, 3);

  out += "},\"unsupported\":[";
  std::vector<std::string> unsupported;
  for (const std::string &extension : extensionsUsed(jsonOf(bytes, size))) {
    if (!isDrawn(extension)) unsupported.push_back(extension);
  }
  for (size_t i = 0; i < unsupported.size(); i++) {
    if (i > 0) out.push_back(',');
    appendText(out, unsupported[i].c_str());
  }
  out += "]}";
  mesh.info = std::move(out);

  // Said as a problem too, because it is one: a host that never reads the
  // description still hears that the file will not look as it was made.
  if (!unsupported.empty()) {
    std::string names;
    for (size_t i = 0; i < unsupported.size(); i++) {
      names += (i == 0 ? "" : ", ") + unsupported[i];
    }
    const std::string said = orblit::format(
        "It uses %s, which this renderer does not draw; those parts are drawn "
        "without it.",
        names.c_str());
    const auto found = _assetNotes.find(path);
    _assetNotes[path] =
        found == _assetNotes.end() ? said : found->second + " " + said;
  }
}

bool Renderer::hasPoses() { return !_posed.empty() || !_varied.empty(); }

void Renderer::applyPoses(const int64_t *keys, const int32_t *ints,
                          const float *floats, const int32_t *jointCounts,
                          const int32_t *joints, const float *jointTransforms,
                          double at, uint32_t count) {
  if (_disposed) return;
  Notes notes;

  std::vector<int64_t> posed;
  posed.reserve(count);
  std::vector<int64_t> varied;
  std::unordered_set<int64_t> mentioned;
  mentioned.reserve(count);

  // Where this pose's joints begin, walked alongside the poses the way morph
  // weights are walked alongside objects.
  size_t jointAt = 0;
  for (uint32_t i = 0; i < count; i++) {
    const int32_t *row = ints + size_t(i) * kPoseInts;
    const float *numbers = floats + size_t(i) * kPoseFloats;
    const size_t jointCount = size_t(std::max(jointCounts[i], 0));
    const size_t firstJoint = jointAt;
    jointAt += jointCount;

    auto found = _drawn.find(keys[i]);
    // A placeholder cube has no clips, and a key the objects did not name is
    // nothing at all. Neither is worth stopping a scene over.
    if (found == _drawn.end() || found->second.instance == nullptr) continue;
    Drawn &drawn = found->second;
    mentioned.insert(keys[i]);
    const std::string about = orblit::format("pose %lld", (long long)keys[i]);

    const int32_t variant = row[3];
    const int32_t variants =
        int32_t(drawn.instance->getMaterialVariantCount());
    if (variant >= variants) {
      notes[about] = orblit::format(
          "Variant %d was asked for, and the file has %d.", variant, variants);
    }
    wearVariant(drawn, variant < variants ? std::max(variant, -1) : -1);
    if (drawn.variant >= 0) varied.push_back(keys[i]);

    Posed next;
    next.now = {row[0], numbers[0], numbers[1], (row[2] & kPoseLoops) != 0};
    next.from = {row[1], numbers[2], numbers[3],
                 (row[2] & kPoseFromLoops) != 0};
    next.fade = std::clamp(numbers[4], 0.0f, 1.0f);
    next.at = at;
    next.moved = drawn.pose.moved;

    auto *animator = drawn.instance->getAnimator();
    const int32_t clips =
        animator != nullptr ? int32_t(animator->getAnimationCount()) : 0;
    for (Played *played : {&next.now, &next.from}) {
      if (played->clip >= clips) {
        notes[about] = orblit::format(
            "Clip %d was asked for, and the file has %d.", played->clip, clips);
        played->clip = -1;
      }
      if (played->clip < -1) played->clip = -1;
    }

    for (size_t j = 0; j < jointCount; j++) {
      const int32_t skin = joints[(firstJoint + j) * 2];
      const int32_t joint = joints[(firstJoint + j) * 2 + 1];
      if (skin < 0 || size_t(skin) >= drawn.instance->getSkinCount() ||
          joint < 0 ||
          size_t(joint) >= drawn.instance->getJointCountAt(size_t(skin))) {
        notes[about] = orblit::format(
            "Joint %d of skin %d was set, and the file has no such joint.",
            joint, skin);
        continue;
      }
      mat4f local;
      std::memcpy(&local, jointTransforms + (firstJoint + j) * 16,
                  sizeof(float) * 16);
      next.joints.emplace_back(
          drawn.instance->getJointsAt(size_t(skin))[joint], local);
    }

    const bool animated =
        next.now.clip >= 0 || next.from.clip >= 0 || !next.joints.empty();
    drawn.pose = std::move(next);
    if (animated) {
      posed.push_back(keys[i]);
    } else {
      restPose(drawn);
    }
  }

  // Posed last time and not now: back to how the file had it. Stating a scene
  // whole is what lets a host stop an animation by no longer mentioning it.
  for (int64_t key : _posed) {
    if (mentioned.count(key) != 0) continue;
    auto found = _drawn.find(key);
    if (found == _drawn.end()) continue;
    const bool moved = found->second.pose.moved;
    found->second.pose = Posed{};
    found->second.pose.moved = moved;
    restPose(found->second);
  }
  // And back in the file's own materials. Walked from last time's list rather
  // than over every object: a publish arrives every frame, and a scene of
  // thousands with three shoes in it should not look at thousands to find
  // them.
  for (int64_t key : _varied) {
    if (mentioned.count(key) != 0) continue;
    auto found = _drawn.find(key);
    if (found != _drawn.end()) wearVariant(found->second, -1);
  }

  _posed = std::move(posed);
  _varied = std::move(varied);
  _poseNotes = std::move(notes);
}

/// Moves every posed object to where its clips have it this frame.
///
/// Once a frame, on the thread that draws, after the camera is placed — its
/// moment on the host's clock is the one the clips are sampled at.
void Renderer::animate() {
  if (_posed.empty()) return;
  auto &transforms = _engine->getTransformManager();

  for (int64_t key : _posed) {
    auto found = _drawn.find(key);
    if (found == _drawn.end() || found->second.instance == nullptr) continue;
    Drawn &drawn = found->second;
    Posed &pose = drawn.pose;
    auto *animator = drawn.instance->getAnimator();

    const double lead =
        _drawnHostSecondsKnown
            ? std::clamp(_drawnHostSeconds - pose.at, -kMostLead, kMostLead)
            : 0.0;

    if (animator != nullptr && pose.now.clip >= 0) {
      animator->applyAnimation(
          size_t(pose.now.clip),
          clipTime(pose.now, lead,
                   animator->getAnimationDuration(size_t(pose.now.clip))));
      // gltfio's order: the clip being faded to first, then the one being
      // left laid over it at one minus the fade.
      if (pose.from.clip >= 0 && pose.fade < 1.0f) {
        animator->applyCrossFade(
            size_t(pose.from.clip),
            clipTime(pose.from, lead,
                     animator->getAnimationDuration(size_t(pose.from.clip))),
            pose.fade);
      }
    }

    // After the clips, so a joint set by hand wins over one the file moves.
    for (const auto &joint : pose.joints) {
      const auto node = transforms.getInstance(joint.first);
      if (node) transforms.setTransform(node, joint.second);
    }

    // A clip that animates shapes writes their weights, and the host's own
    // go back over them — the same precedence a joint set by hand has.
    if (!drawn.morphWeights.empty()) {
      morph(drawn, drawn.morphWeights.data(), drawn.morphWeights.size());
    }

    if (animator != nullptr && drawn.instance->getSkinCount() > 0) {
      animator->updateBoneMatrices();
      fitSkinnedBoxes(drawn);
    }
    pose.moved = true;
  }
}

/// Puts an object's nodes, bones and boxes back as the file had them.
///
/// Not everything: gltfio keeps its own copy of each animated node's
/// translation, rotation and scale, which nothing outside it can reach. A
/// clip started afterwards moves the channels it animates and leaves any
/// others where the last clip put them — which is also what gltfio does on
/// its own when one clip follows another.
void Renderer::restPose(Drawn &drawn) {
  if (!drawn.pose.moved || drawn.instance == nullptr || drawn.mesh == nullptr) {
    return;
  }
  auto &transforms = _engine->getTransformManager();
  auto &renderables = _engine->getRenderableManager();
  const utils::Entity *entities = drawn.instance->getEntities();
  const size_t count =
      std::min(drawn.instance->getEntityCount(), drawn.mesh->rest.size());
  const utils::Entity root = drawn.instance->getRoot();

  for (size_t i = 0; i < count; i++) {
    // The root is where the host put the object, not the file's to restore.
    if (entities[i] == root) continue;
    const auto node = transforms.getInstance(entities[i]);
    if (node) transforms.setTransform(node, drawn.mesh->rest[i]);
  }
  for (uint32_t index : drawn.mesh->skinned) {
    if (index >= count) continue;
    const auto renderable = renderables.getInstance(entities[index]);
    if (renderable) {
      renderables.setAxisAlignedBoundingBox(renderable,
                                            drawn.mesh->boxes[index]);
    }
  }
  if (auto *animator = drawn.instance->getAnimator()) {
    if (drawn.instance->getSkinCount() > 0) animator->updateBoneMatrices();
  }
  drawn.pose.moved = false;
}

/// Gives each skinned part a box that holds it in the pose it is in now.
///
/// Filament culls a renderable by the box it was built with, and a skinned
/// one's is the bind pose's — so a character whose arm swings out of that box
/// loses the arm at the edge of the screen, and one whose clip walks it away
/// from where it was bound vanishes while still in view.
///
/// Every skinned vertex is a weighted average, weights summing to one, of its
/// bind position carried by each joint's skinning matrix. Each of those lies
/// inside the bind box carried by the same matrix, and an average of points
/// lies inside any box holding all of them — so the union of the bind box
/// carried by every joint holds every vertex, whatever the pose. A bound
/// rather than a guess, and a few dozen matrix products a frame rather than a
/// pass over the vertices.
void Renderer::fitSkinnedBoxes(Drawn &drawn) {
  if (drawn.mesh == nullptr || drawn.mesh->skinned.empty()) return;
  auto &transforms = _engine->getTransformManager();
  auto &renderables = _engine->getRenderableManager();
  const utils::Entity *entities = drawn.instance->getEntities();
  const size_t count =
      std::min(drawn.instance->getEntityCount(), drawn.mesh->boxes.size());
  const size_t skins = drawn.instance->getSkinCount();

  for (uint32_t index : drawn.mesh->skinned) {
    if (index >= count) continue;
    const auto renderable = renderables.getInstance(entities[index]);
    const auto node = transforms.getInstance(entities[index]);
    if (!renderable || !node) continue;
    const Box &bind = drawn.mesh->boxes[index];
    const mat4f toLocal = inverse(transforms.getWorldTransform(node));

    float3 least{std::numeric_limits<float>::max()};
    float3 most{-std::numeric_limits<float>::max()};
    // Every joint of every skin: a part names one skin, which gltfio does
    // not say, and more joints make a looser box but never a wrong one.
    for (size_t s = 0; s < skins; s++) {
      const utils::Entity *joints = drawn.instance->getJointsAt(s);
      const mat4f *inverseBinds = drawn.instance->getInverseBindMatricesAt(s);
      for (size_t j = 0; j < drawn.instance->getJointCountAt(s); j++) {
        const auto jointNode = transforms.getInstance(joints[j]);
        if (!jointNode) continue;
        const mat4f m =
            toLocal * transforms.getWorldTransform(jointNode) * inverseBinds[j];
        const float3 centre = (m * float4(bind.center, 1.0f)).xyz;
        const float3 &e = bind.halfExtent;
        const float3 half{
            std::abs(m[0].x) * e.x + std::abs(m[1].x) * e.y +
                std::abs(m[2].x) * e.z,
            std::abs(m[0].y) * e.x + std::abs(m[1].y) * e.y +
                std::abs(m[2].y) * e.z,
            std::abs(m[0].z) * e.x + std::abs(m[1].z) * e.y +
                std::abs(m[2].z) * e.z};
        least = min(least, centre - half);
        most = max(most, centre + half);
      }
    }
    if (least.x <= most.x) {
      renderables.setAxisAlignedBoundingBox(renderable,
                                            Box().set(least, most));
    }
  }
}

/// Dresses an object in one of its file's material variants, or back in the
/// file's own materials for -1.
///
/// A variant names only the primitives it changes, so every primitive goes
/// back to the file's own material first and the variant is laid over that —
/// otherwise a primitive the new variant does not mention keeps whatever the
/// last one gave it. A named Orblit material still wins over both: it is
/// taken off, the variant is worn underneath, and it is put back on top.
void Renderer::wearVariant(Drawn &drawn, int32_t variant) {
  if (drawn.instance == nullptr || variant == drawn.variant) return;
  auto &renderables = _engine->getRenderableManager();
  const utils::Entity *entities = drawn.instance->getEntities();
  const size_t count = drawn.instance->getEntityCount();

  const bool overridden = !drawn.ownMaterials.empty();
  if (overridden) {
    dress(drawn, -1);
    drawn.ownMaterials.clear();
  }

  const auto walk = [&](auto &&visit) {
    for (size_t i = 0; i < count; i++) {
      const auto renderable = renderables.getInstance(entities[i]);
      if (!renderable) continue;
      for (size_t p = 0; p < renderables.getPrimitiveCount(renderable); p++) {
        visit(renderable, p);
      }
    }
  };
  if (drawn.fileMaterials.empty()) {
    walk([&](auto renderable, size_t p) {
      drawn.fileMaterials.push_back(
          renderables.getMaterialInstanceAt(renderable, p));
    });
  }
  size_t slot = 0;
  walk([&](auto renderable, size_t p) {
    if (slot < drawn.fileMaterials.size() && drawn.fileMaterials[slot]) {
      renderables.setMaterialInstanceAt(renderable, p,
                                        drawn.fileMaterials[slot]);
    }
    slot++;
  });
  if (variant >= 0) drawn.instance->applyMaterialVariant(size_t(variant));
  drawn.variant = variant;

  if (overridden) dress(drawn, drawn.surface);
}

}  // namespace orblit
