#include "OrblitImportInternal.h"

// Where an FBX or OBJ's things are, and how they move.
//
// The node tree first: which ufbx nodes are worth writing, what number each
// gets, and the skins that bind them to meshes. Then the animation, baked by
// ufbx and written as glTF channels and samplers — a clip's channels share
// their time accessor, because a clip baked at thirty frames a second has
// hundreds of channels on the same times.
//
// Part of `orblit::glb::Converter`; see OrblitImportInternal.h.

namespace orblit {
namespace glb {

  // -- Nodes and skins ------------------------------------------------------

const ufbx_node *Converter::topLevel(const ufbx_node *node) {
  for (uint32_t guard = 0; node && node->parent && !node->parent->is_root &&
                           guard <= kNodeDepthLimit + 2;
       guard++) {
    node = node->parent;
  }
  return node;
}

bool Converter::identity(const ufbx_transform &t) {
  return t.translation.x == 0 && t.translation.y == 0 && t.translation.z == 0 &&
         t.rotation.x == 0 && t.rotation.y == 0 && t.rotation.z == 0 &&
         t.rotation.w == 1 && t.scale.x == 1 && t.scale.y == 1 && t.scale.z == 1;
}

void Converter::planNodes() {
  const size_t count = scene_->nodes.count;
  std::vector<bool> isJoint(count, false);
  for (const Instance &instance : instances_) {
    for (const ufbx_node *joint : instance.mesh->joints) {
      if (joint->typed_id < count) isJoint[joint->typed_id] = true;
    }
  }

  // The root ufbx adds above the file's own nodes is left out — it is the
  // scene — unless it moves something, or a skeleton spans two of its
  // children: glTF's joints need a common root to be one skin.
  const ufbx_node *root = scene_->root_node;
  bool emitRoot = root && !identity(root->local_transform);
  for (const Instance &instance : instances_) {
    const ufbx_node *top = nullptr;
    for (const ufbx_node *joint : instance.mesh->joints) {
      const ufbx_node *jointTop = topLevel(joint);
      if (joint->is_root) emitRoot = true;
      if (top && jointTop != top) emitRoot = true;
      top = jointTop;
    }
    if (instance.mesh->needsNodeJoint && top && topLevel(instance.node) != top) {
      emitRoot = true;
    }
  }

  for (Instance &instance : instances_) {
    if (!instance.mesh->skinned) continue;
    const ufbx_node *node = instance.node;
    instance.movedToRoot = node->children.count == 0 && !node->is_root &&
                           !isJoint[node->typed_id] &&
                           !instance.mesh->needsNodeJoint;
  }
  moved_.assign(count, false);
  for (const Instance &instance : instances_) {
    if (instance.movedToRoot) moved_[instance.node->typed_id] = true;
  }

  hierarchy_.assign(count, -1);
  int next = 0;
  for (size_t n = 0; n < count; n++) {
    const ufbx_node *node = scene_->nodes.data[n];
    if (!node || moved_[n]) continue;
    if (node->is_root && !emitRoot) continue;
    hierarchy_[n] = next++;
  }
  for (Instance &instance : instances_) {
    instance.meshNode = instance.mesh->skinned
                            ? next++
                            : hierarchy_[instance.node->typed_id];
  }
  emitRoot_ = emitRoot;
}

/// The scene's rest-pose extent: a static mesh's box carried by its node,
/// corner by corner, and a skinned mesh's box as its bones placed it.
void Converter::measure() {
  for (const Instance &instance : instances_) {
    const Box &box = instance.mesh->box;
    if (instance.mesh->skinned || box.empty) {
      sceneBox_.add(box);
      continue;
    }
    const ufbx_matrix &world = instance.node->geometry_to_world;
    for (int corner = 0; corner < 8; corner++) {
      sceneBox_.add(transformPoint(
          world, vec3(corner & 1 ? box.maximum[0] : box.minimum[0],
                      corner & 2 ? box.maximum[1] : box.minimum[1],
                      corner & 4 ? box.maximum[2] : box.minimum[2])));
    }
  }
}

void Converter::writeSkins() {
  std::map<std::pair<const BuiltMesh *, const ufbx_node *>, uint32_t> matrices;
  for (Instance &instance : instances_) {
    BuiltMesh &built = *instance.mesh;
    if (!built.skinned) continue;

    std::vector<uint32_t> joints;
    for (const ufbx_node *joint : built.joints) {
      joints.push_back(uint32_t(hierarchy_[joint->typed_id]));
    }
    // The node joint's matrices are the node's own, so an inverse-bind
    // accessor is shared between instances only when there is none.
    const ufbx_node *owner = built.needsNodeJoint ? instance.node : nullptr;
    if (owner) joints.push_back(uint32_t(hierarchy_[owner->typed_id]));

    const auto key = std::make_pair(&built, owner);
    auto found = matrices.find(key);
    if (found == matrices.end()) {
      std::vector<float> values;
      values.reserve((built.inverseBinds.size() + 1) * 16);
      auto put = [&](const ufbx_matrix &m) {
        const double column[16] = {m.m00, m.m10, m.m20, 0, m.m01, m.m11, m.m21, 0,
                                   m.m02, m.m12, m.m22, 0, m.m03, m.m13, m.m23, 1};
        for (double value : column) {
          values.push_back(std::isfinite(value) ? float(value) : 0.0f);
        }
      };
      for (const ufbx_matrix &m : built.inverseBinds) put(m);
      // Unbound vertices are where their node puts them, and the node's
      // world matrix times this is exactly that.
      if (owner) put(owner->geometry_to_node);
      found = matrices.emplace(key, doc_.floats(values, 16, "MAT4", 0, false)).first;
    }

    doc_.skins.push_back("{\"inverseBindMatrices\":" +
                         std::to_string(found->second) +
                         ",\"joints\":" + indexList(joints) + "}");
    skinOf_.push_back(uint32_t(doc_.skins.size() - 1));
    joints_ += uint32_t(joints.size());
  }
}

void Converter::putName(std::string &json, const ufbx_node *node) {
  if (node->name.length == 0) return;
  json += "\"name\":";
  putString(json, text(node->name));
  json += ',';
}

void Converter::writeNodes() {
  std::vector<int> meshOf(scene_->nodes.count, -1);
  for (const Instance &instance : instances_) {
    if (!instance.mesh->skinned) {
      meshOf[instance.node->typed_id] = instance.mesh->index;
    }
  }

  for (size_t n = 0; n < scene_->nodes.count; n++) {
    if (hierarchy_[n] < 0) continue;
    const ufbx_node *node = scene_->nodes.data[n];
    std::string json = "{";
    putName(json, node);

    std::vector<uint32_t> children;
    for (size_t c = 0; c < node->children.count; c++) {
      const ufbx_node *child = node->children.data[c];
      if (child && child->typed_id < hierarchy_.size() &&
          hierarchy_[child->typed_id] >= 0) {
        children.push_back(uint32_t(hierarchy_[child->typed_id]));
      }
    }
    if (!children.empty()) json += "\"children\":" + indexList(children) + ",";
    putTransform(json, node->local_transform);
    if (meshOf[n] >= 0) json += "\"mesh\":" + std::to_string(meshOf[n]) + ",";
    if (json.back() == ',') json.pop_back();
    json += '}';
    doc_.nodes.push_back(std::move(json));
  }

  size_t skin = 0;
  for (const Instance &instance : instances_) {
    if (!instance.mesh->skinned) continue;
    std::string json = "{";
    putName(json, instance.node);
    json += "\"mesh\":" + std::to_string(instance.mesh->index) +
            ",\"skin\":" + std::to_string(skinOf_[skin++]) + "}";
    doc_.nodes.push_back(std::move(json));
  }

  // The scene: the root, or the root's own children, then the skinned
  // meshes, which are roots of their own.
  const ufbx_node *root = scene_->root_node;
  if (emitRoot_ && root) {
    doc_.sceneNodes.push_back(uint32_t(hierarchy_[root->typed_id]));
  } else if (root) {
    for (size_t c = 0; c < root->children.count; c++) {
      const ufbx_node *child = root->children.data[c];
      if (child && hierarchy_[child->typed_id] >= 0) {
        doc_.sceneNodes.push_back(uint32_t(hierarchy_[child->typed_id]));
      }
    }
  }
  for (const Instance &instance : instances_) {
    if (instance.mesh->skinned) {
      doc_.sceneNodes.push_back(uint32_t(instance.meshNode));
    }
  }
}

void Converter::putTransform(std::string &json, const ufbx_transform &t) {
  const ufbx_vec3 &p = t.translation;
  if (finite3(p) && (p.x != 0 || p.y != 0 || p.z != 0)) {
    const double v[3] = {p.x, p.y, p.z};
    json += "\"translation\":";
    putFloats(json, v, 3);
    json += ',';
  }
  double q[4] = {t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w};
  if (normalise(q) && (q[0] != 0 || q[1] != 0 || q[2] != 0)) {
    json += "\"rotation\":";
    putFloats(json, q, 4);
    json += ',';
  }
  const ufbx_vec3 &s = t.scale;
  if (finite3(s) && (s.x != 1 || s.y != 1 || s.z != 1)) {
    const double v[3] = {s.x, s.y, s.z};
    json += "\"scale\":";
    putFloats(json, v, 3);
    json += ',';
  }
}

bool Converter::normalise(double q[4]) {
  const double length = std::sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
  if (!std::isfinite(length) || length < 1e-12) {
    q[0] = q[1] = q[2] = 0;
    q[3] = 1;
    return false;
  }
  for (int i = 0; i < 4; i++) q[i] /= length;
  return true;
}

  // -- Animation -------------------------------------------------------------

/// Key times as glTF needs them: seconds from the clip's start as floats,
/// strictly increasing, none negative. A key before the start stands in
/// for the start only if it is the last one before it. Returns which keys
/// survived, by position.
std::vector<size_t> Converter::keepIncreasing(const std::vector<double> &times,
                                              double start,
                                              std::vector<float> &kept) {
  std::vector<size_t> which;
  kept.clear();
  for (size_t i = 0; i < times.size(); i++) {
    float t = float(times[i] - start);
    if (!std::isfinite(t)) continue;
    if (t < 0) {
      if (i + 1 < times.size() && float(times[i + 1] - start) <= 0) continue;
      t = 0;
    }
    if (!kept.empty() && !(t > kept.back())) continue;
    kept.push_back(t);
    which.push_back(i);
  }
  return which;
}

void Converter::addChannel(Clip &clip, const std::vector<float> &times,
                           const std::vector<float> &values, size_t components,
                           const char *type, int node, const char *path) {
  const std::string bytes(reinterpret_cast<const char *>(times.data()),
                          times.size() * sizeof(float));
  auto input = clip.inputs.find(bytes);
  if (input == clip.inputs.end()) {
    input = clip.inputs.emplace(bytes, doc_.floats(times, 1, "SCALAR", 0, true)).first;
  }
  const uint32_t output = doc_.floats(values, components, type, 0, false);
  clip.samplers.push_back("{\"input\":" + std::to_string(input->second) +
                          ",\"output\":" + std::to_string(output) +
                          ",\"interpolation\":\"LINEAR\"}");
  clip.channels.push_back("{\"sampler\":" + std::to_string(clip.samplers.size() - 1) +
                          ",\"target\":{\"node\":" + std::to_string(node) +
                          ",\"path\":\"" + path + "\"}}");
  clip.end = std::max(clip.end, times.back());
}

bool Converter::sameValue(double a, double b) {
  return std::fabs(a - b) <= 1e-6 * std::max(1.0, std::max(std::fabs(a), std::fabs(b)));
}

void Converter::vectorChannel(Clip &clip, const ufbx_baked_vec3_list &keys,
                              bool constant, const ufbx_vec3 &rest, int node,
                              const char *path, double start) {
  if (keys.count == 0) return;
  // A channel that holds the node where it already rests says nothing.
  // One that holds it somewhere else is a pose, and is kept as one key.
  if (constant && sameValue(keys.data[0].value.x, rest.x) &&
      sameValue(keys.data[0].value.y, rest.y) &&
      sameValue(keys.data[0].value.z, rest.z)) {
    return;
  }
  const size_t used = constant ? 1 : keys.count;
  std::vector<double> times(used);
  for (size_t i = 0; i < used; i++) times[i] = keys.data[i].time;
  std::vector<float> kept;
  const std::vector<size_t> which = keepIncreasing(times, start, kept);
  if (kept.empty()) return;
  std::vector<float> values;
  values.reserve(which.size() * 3);
  for (size_t i : which) {
    const ufbx_vec3 &v = keys.data[i].value;
    values.push_back(std::isfinite(v.x) ? float(v.x) : 0.0f);
    values.push_back(std::isfinite(v.y) ? float(v.y) : 0.0f);
    values.push_back(std::isfinite(v.z) ? float(v.z) : 0.0f);
  }
  addChannel(clip, kept, values, 3, "VEC3", node, path);
}

void Converter::rotationChannel(Clip &clip, const ufbx_baked_quat_list &keys,
                                bool constant, const ufbx_quat &rest, int node,
                                double start) {
  if (keys.count == 0) return;
  if (constant) {
    const ufbx_quat &q = keys.data[0].value;
    const double dot = q.x * rest.x + q.y * rest.y + q.z * rest.z + q.w * rest.w;
    if (std::fabs(dot) > 1.0 - 1e-9) return;
  }
  const size_t used = constant ? 1 : keys.count;
  std::vector<double> times(used);
  for (size_t i = 0; i < used; i++) times[i] = keys.data[i].time;
  std::vector<float> kept;
  const std::vector<size_t> which = keepIncreasing(times, start, kept);
  if (kept.empty()) return;
  std::vector<float> values;
  values.reserve(which.size() * 4);
  double previous[4] = {0, 0, 0, 1};
  for (size_t i : which) {
    const ufbx_quat &source = keys.data[i].value;
    double q[4] = {source.x, source.y, source.z, source.w};
    normalise(q);
    // The shorter way round from the key before, as linear interpolation
    // of quaternions needs.
    if (!values.empty() &&
        q[0] * previous[0] + q[1] * previous[1] + q[2] * previous[2] +
                q[3] * previous[3] < 0) {
      for (double &c : q) c = -c;
    }
    for (int c = 0; c < 4; c++) {
      values.push_back(float(q[c]));
      previous[c] = q[c];
    }
  }
  addChannel(clip, kept, values, 4, "VEC4", node, "rotation");
}

void Converter::weightChannels(Clip &clip, ufbx_baked_anim *baked,
                               double start) {
  for (const Instance &instance : instances_) {
    const std::vector<const ufbx_blend_channel *> &targets = instance.mesh->targets;
    if (targets.empty() || instance.meshNode < 0) continue;
    std::vector<const ufbx_baked_vec3_list *> animated(targets.size(), nullptr);
    std::vector<double> times;
    for (size_t t = 0; t < targets.size(); t++) {
      const ufbx_baked_element *element =
          ufbx_find_baked_element_by_element_id(baked, targets[t]->element_id);
      if (!element) continue;
      for (size_t p = 0; p < element->props.count; p++) {
        const ufbx_baked_prop &prop = element->props.data[p];
        if (text(prop.name) != "DeformPercent" || prop.keys.count == 0) continue;
        animated[t] = &prop.keys;
        for (size_t k = 0; k < prop.keys.count; k++) times.push_back(prop.keys.data[k].time);
      }
    }
    if (times.empty()) continue;
    std::sort(times.begin(), times.end());
    std::vector<float> kept;
    const std::vector<size_t> which = keepIncreasing(times, start, kept);
    if (kept.empty()) continue;

    std::vector<float> values;
    values.reserve(which.size() * targets.size());
    for (size_t i : which) {
      for (size_t t = 0; t < targets.size(); t++) {
        double weight = targets[t]->weight;
        if (animated[t]) {
          weight = ufbx_evaluate_baked_vec3(*animated[t], times[i]).x / 100.0;
        }
        values.push_back(std::isfinite(weight) ? float(weight) : 0.0f);
      }
    }
    // An output of one value per target per key, which glTF reads as
    // SCALAR with a count of keys times targets.
    addChannel(clip, kept, values, 1, "SCALAR", instance.meshNode, "weights");
  }
}

void Converter::writeAnimations() {
  for (size_t s = 0; s < scene_->anim_stacks.count; s++) {
    const ufbx_anim_stack *stack = scene_->anim_stacks.data[s];
    if (!stack || !stack->anim) continue;
    const std::string name = text(stack->name);

    ufbx_bake_opts opts = {};
    opts.temp_allocator.memory_limit = kUfbxMemoryLimit;
    opts.result_allocator.memory_limit = kUfbxMemoryLimit;
    // A clip whose range starts at frame 30 is written to start at nought,
    // where a glTF player starts every clip. The times are moved here, not
    // by ufbx's trim_start_time: in v0.23.0 that shifts each node's keys
    // as the node is finished, so a bone under an animated scale helper
    // multiplies its translation by the helper's scale read at the wrong
    // time — a Maya joint with segment scale compensation came out
    // visibly wrong. Untrimmed, the bake matches ufbx's own evaluation.
    const double start = stack->anim->time_begin > 0 ? stack->anim->time_begin : 0;
    // Keys a straight line between their neighbours would reproduce are
    // dropped; rotations as well, because glTF interpolates them
    // spherically, which is what the reduction assumes.
    opts.key_reduction_enabled = true;
    opts.key_reduction_rotation = true;
    ufbx_error error;
    std::unique_ptr<ufbx_baked_anim, void (*)(ufbx_baked_anim *)> baked(
        ufbx_bake_anim(scene_, stack->anim, &opts, &error), ufbx_free_baked_anim);
    if (!baked) {
      losses_.add("animation '" + name + "' could not be baked: " +
                  text(error.description));
      continue;
    }

    Clip clip;
    for (size_t b = 0; b < baked->nodes.count; b++) {
      const ufbx_baked_node &bn = baked->nodes.data[b];
      if (bn.typed_id >= scene_->nodes.count) continue;
      const int node = hierarchy_[bn.typed_id];
      // Nodes not written, and skinned meshes moved to the root, whose
      // own transform glTF would ignore, have nothing to animate.
      if (node < 0) continue;
      const ufbx_transform &rest = scene_->nodes.data[bn.typed_id]->local_transform;
      vectorChannel(clip, bn.translation_keys, bn.constant_translation,
                    rest.translation, node, "translation", start);
      rotationChannel(clip, bn.rotation_keys, bn.constant_rotation, rest.rotation,
                      node, start);
      vectorChannel(clip, bn.scale_keys, bn.constant_scale, rest.scale, node,
                    "scale", start);
    }
    weightChannels(clip, baked.get(), start);

    if (clip.channels.empty()) {
      losses_.add("animation '" + name +
                  "' moves nothing that was imported; not written");
      continue;
    }
    std::string json = "{";
    if (!name.empty()) {
      json += "\"name\":";
      putString(json, name);
      json += ',';
    }
    json += "\"channels\":[";
    for (size_t i = 0; i < clip.channels.size(); i++) {
      if (i > 0) json += ',';
      json += clip.channels[i];
    }
    json += "],\"samplers\":[";
    for (size_t i = 0; i < clip.samplers.size(); i++) {
      if (i > 0) json += ',';
      json += clip.samplers[i];
    }
    json += "]}";
    doc_.animations.push_back(std::move(json));

    ImportedClip summary;
    summary.name = name;
    summary.seconds = clip.end;
    summary.channels = uint32_t(clip.channels.size());
    clips_.push_back(std::move(summary));
  }
}

}  // namespace glb
}  // namespace orblit
