#include "OrblitImportInternal.h"

// The meshes an FBX or OBJ holds, as glTF primitives.
//
// One ufbx mesh under one assignment of materials becomes one glTF mesh, so
// the same mesh under two nodes with different materials is two of them and
// under the same materials is one. Vertices are made unique by their packed
// bytes, which is what turns a file's per-corner attributes into an indexed
// primitive. Skinning weights and morph targets are prepared here too,
// because both are attributes of the vertex.
//
// Part of `orblit::glb::Converter`; see OrblitImportInternal.h.

namespace orblit {
namespace glb {

  // -- What glTF has no place for ------------------------------------------

void Converter::noteUnsupported() {
  auto count = [&](size_t n, const char *what) {
    if (n > 0) losses_.add(std::to_string(n) + " " + what);
  };
  count(scene_->cameras.count, "camera(s) not carried: the importer writes no glTF cameras");
  count(scene_->lights.count, "light(s) not carried: the importer writes no glTF lights");
  count(scene_->nurbs_curves.count + scene_->nurbs_surfaces.count +
            scene_->line_curves.count,
        "curve(s) or NURBS surface(s) not carried: only polygon meshes are");
  count(scene_->constraints.count,
        "constraint(s) not carried: animation is baked without them");
  count(scene_->cache_deformers.count,
        "vertex cache(s) not carried: point-cache animation has no glTF form");
}

  // -- Meshes ----------------------------------------------------------------

void Converter::collectMeshes() {
  for (size_t n = 0; n < scene_->nodes.count; n++) {
    const ufbx_node *node = scene_->nodes.data[n];
    if (!node || !node->mesh) continue;
    const ufbx_mesh *mesh = node->mesh;
    if (mesh->num_triangles == 0 || !mesh->vertex_position.exists) continue;

    const ufbx_material_list &list =
        node->materials.count > 0 ? node->materials : mesh->materials;
    std::vector<uint32_t> key = {mesh->typed_id};
    for (size_t i = 0; i < list.count; i++) {
      key.push_back(list.data[i] ? list.data[i]->typed_id : UINT32_MAX);
    }
    auto found = meshes_.find(key);
    if (found == meshes_.end()) {
      std::unique_ptr<BuiltMesh> built(new BuiltMesh());
      buildMesh(node, mesh, list, *built);
      found = meshes_.emplace(key, std::move(built)).first;
    }
    if (found->second->index < 0) continue;

    Instance instance;
    instance.node = node;
    instance.mesh = found->second.get();
    instances_.push_back(instance);
  }
}

/// The skin a mesh is drawn with, and its joints: one per distinct bone,
/// in cluster order, each with the matrix that takes the mesh's own
/// vertices into that bone's space at bind time.
void Converter::prepareSkin(const ufbx_mesh *mesh, BuiltMesh &built,
                            std::vector<uint32_t> &clusterJoint) {
  if (mesh->skin_deformers.count == 0) return;
  if (mesh->skin_deformers.count > 1) {
    losses_.add("mesh '" + text(mesh->name) +
                "' has more than one skin; only the first is kept");
  }
  const ufbx_skin_deformer *skin = mesh->skin_deformers.data[0];
  if (!skin || skin->clusters.count == 0) return;
  if (skin->skinning_method == UFBX_SKINNING_METHOD_DUAL_QUATERNION ||
      skin->skinning_method == UFBX_SKINNING_METHOD_BLENDED_DQ_LINEAR) {
    losses_.add("mesh '" + text(mesh->name) +
                "' uses dual-quaternion skinning; glTF skins linearly");
  }
  clusterJoint.assign(skin->clusters.count, UINT32_MAX);
  std::map<uint32_t, uint32_t> byBone;
  for (size_t c = 0; c < skin->clusters.count; c++) {
    const ufbx_skin_cluster *cluster = skin->clusters.data[c];
    if (!cluster || !cluster->bone_node) continue;
    const auto found = byBone.find(cluster->bone_node->typed_id);
    if (found != byBone.end()) {
      clusterJoint[c] = found->second;
      continue;
    }
    const uint32_t joint = uint32_t(built.joints.size());
    byBone.emplace(cluster->bone_node->typed_id, joint);
    clusterJoint[c] = joint;
    built.joints.push_back(cluster->bone_node);
    built.clusters.push_back(cluster);
    built.inverseBinds.push_back(cluster->geometry_to_bone);
  }
  // Room for the node joint as well, in sixteen bits.
  if (built.joints.empty() || built.joints.size() >= 65535) {
    if (!built.joints.empty()) {
      losses_.add("mesh '" + text(mesh->name) +
                  "' has more bones than a glTF skin here can index; "
                  "left unskinned");
    }
    built.joints.clear();
    built.clusters.clear();
    built.inverseBinds.clear();
    clusterJoint.clear();
    return;
  }
  built.skinned = true;
  built.skin = skin;
}

void Converter::prepareTargets(const ufbx_mesh *mesh, BuiltMesh &built) {
  for (size_t d = 0; d < mesh->blend_deformers.count; d++) {
    const ufbx_blend_deformer *deformer = mesh->blend_deformers.data[d];
    if (!deformer) continue;
    for (size_t c = 0; c < deformer->channels.count; c++) {
      const ufbx_blend_channel *channel = deformer->channels.data[c];
      if (!channel || !channel->target_shape) continue;
      if (channel->keyframes.count > 1) {
        losses_.add("blend shape '" + text(channel->name) +
                    "' has in-between shapes; only its full target is kept");
      }
      built.targets.push_back(channel);
    }
  }
}

void Converter::buildMesh(const ufbx_node *node, const ufbx_mesh *mesh,
                          const ufbx_material_list &materials,
                          BuiltMesh &built) {
  std::vector<uint32_t> clusterJoint;
  prepareSkin(mesh, built, clusterJoint);
  prepareTargets(mesh, built);

  const Layout layout(mesh->vertex_uv.exists, mesh->vertex_color.exists,
                      built.skinned, !built.targets.empty());
  const uint32_t nodeJoint = uint32_t(built.joints.size());

  // Every target in a primitive carries the same attributes, and normals
  // only when every shape of the mesh has them.
  bool targetNormals = !built.targets.empty();
  for (const ufbx_blend_channel *channel : built.targets) {
    const ufbx_blend_shape *shape = channel->target_shape;
    if (shape->normal_offsets.count < shape->num_offsets ||
        shape->num_offsets == 0) {
      targetNormals = false;
    }
  }

  // The skinned rest pose uses each bone's matrix as ufbx evaluated it;
  // a vertex no bone holds stays where its node puts it.
  const ufbx_matrix *restOfUnbound = &node->geometry_to_world;

  std::vector<std::string> primitives;
  std::vector<uint32_t> triangle(mesh->max_face_triangles * 3 + 3);
  std::vector<uint8_t> packed(layout.stride);
  size_t overInfluenced = 0;

  for (size_t p = 0; p < mesh->material_parts.count; p++) {
    const ufbx_mesh_part &part = mesh->material_parts.data[p];
    if (part.num_triangles == 0) continue;
    if (uint64_t(part.num_triangles) * 3 * (layout.stride + 4) >
        kMaxBinaryBytes) {
      throw Failure{"mesh '" + text(mesh->name) + "' is too large to convert"};
    }

    VertexSet vertices(layout.stride);
    std::vector<uint32_t> indices;
    indices.reserve(part.num_triangles * 3);

    for (size_t f = 0; f < part.face_indices.count; f++) {
      const uint32_t faceIndex = part.face_indices.data[f];
      if (faceIndex >= mesh->faces.count) continue;
      const ufbx_face face = mesh->faces.data[faceIndex];
      if (face.num_indices < 3) continue;
      if ((size_t(face.num_indices) - 2) * 3 > triangle.size()) continue;
      ufbx_panic panic;
      panic.did_panic = false;
      const uint32_t count = ufbx_catch_triangulate_face(
          &panic, triangle.data(), triangle.size(), mesh, face);
      if (panic.did_panic) continue;

      for (uint32_t t = 0; t < count; t++) {
        uint32_t corner[3];
        for (int k = 0; k < 3; k++) {
          packVertex(mesh, built, layout, clusterJoint, nodeJoint,
                     triangle[t * 3 + k], packed.data(), overInfluenced);
          corner[k] = vertices.add(packed.data());
        }
        // Two corners made one by the de-duplication draw nothing.
        if (corner[0] == corner[1] || corner[1] == corner[2] ||
            corner[0] == corner[2]) {
          continue;
        }
        indices.insert(indices.end(), corner, corner + 3);
      }
    }
    if (indices.empty()) continue;

    const ufbx_material *material =
        part.index < materials.count ? materials.data[part.index] : nullptr;
    primitives.push_back(writePrimitive(mesh, built, layout, vertices,
                                        indices, material, targetNormals,
                                        restOfUnbound));
  }

  if (overInfluenced > 0) {
    losses_.add(std::to_string(overInfluenced) + " vertices of mesh '" +
                text(mesh->name) +
                "' had more than four bone influences; kept the four "
                "largest, renormalised");
  }
  if (primitives.empty()) return;

  std::string json = "{";
  const std::string name = mesh->name.length > 0 ? text(mesh->name)
                                                 : text(node->name);
  if (!name.empty()) {
    json += "\"name\":";
    putString(json, name);
    json += ',';
  }
  json += "\"primitives\":[";
  for (size_t i = 0; i < primitives.size(); i++) {
    if (i > 0) json += ',';
    json += primitives[i];
  }
  json += ']';
  if (!built.targets.empty()) {
    json += ",\"weights\":[";
    for (size_t t = 0; t < built.targets.size(); t++) {
      if (t > 0) json += ',';
      putFloat(json, built.targets[t]->weight);
    }
    // Target names where three.js, Blender and gltfio all look for them.
    json += "],\"extras\":{\"targetNames\":[";
    for (size_t t = 0; t < built.targets.size(); t++) {
      if (t > 0) json += ',';
      putString(json, text(built.targets[t]->name));
    }
    json += "]}";
    morphTargets_ += uint32_t(built.targets.size());
  }
  json += '}';
  doc_.meshes.push_back(std::move(json));
  built.index = int(doc_.meshes.size() - 1);
  primitives_ += uint32_t(primitives.size());
}

/// One corner of a triangle, by its index into the ufbx mesh, packed.
void Converter::packVertex(const ufbx_mesh *mesh, const BuiltMesh &built,
                           const Layout &layout,
                           const std::vector<uint32_t> &clusterJoint,
                           uint32_t nodeJoint, uint32_t index, uint8_t *into,
                           size_t &overInfluenced) {
  std::memset(into, 0, layout.stride);

  ufbx_vec3 position = attributeAt<ufbx_vec3>(mesh->vertex_position, index);
  if (!finite3(position)) position = vec3(0, 0, 0);
  const float p[3] = {float(position.x), float(position.y), float(position.z)};
  putBytes(into, 0, p, 3);

  // glTF requires unit normals; one ufbx could not make is pointed up
  // rather than left at zero length.
  ufbx_vec3 normal = attributeAt<ufbx_vec3>(mesh->vertex_normal, index);
  const double length =
      std::sqrt(normal.x * normal.x + normal.y * normal.y + normal.z * normal.z);
  float n[3] = {0, 1, 0};
  if (std::isfinite(length) && length > 1e-12) {
    n[0] = float(normal.x / length);
    n[1] = float(normal.y / length);
    n[2] = float(normal.z / length);
  }
  putBytes(into, 12, n, 3);

  if (layout.uv) {
    const ufbx_vec2 uv = attributeAt<ufbx_vec2>(mesh->vertex_uv, index);
    // FBX and OBJ put v = 0 at the bottom of the image, glTF at the top.
    float t[2] = {float(uv.x), float(1.0 - uv.y)};
    for (float &value : t) {
      if (!std::isfinite(value)) value = 0;
    }
    putBytes(into, layout.uvAt, t, 2);
  }
  if (layout.colour) {
    const ufbx_vec4 colour = attributeAt<ufbx_vec4>(mesh->vertex_color, index);
    float c[4] = {float(colour.x), float(colour.y), float(colour.z),
                  float(colour.w)};
    for (float &value : c) {
      value = std::isfinite(value) ? std::min(std::max(value, 0.0f), 1.0f) : 1.0f;
    }
    putBytes(into, layout.colourAt, c, 4);
  }

  const uint32_t vertex = index < mesh->vertex_indices.count
                              ? mesh->vertex_indices.data[index]
                              : UINT32_MAX;
  if (layout.skin) {
    packWeights(built, clusterJoint, nodeJoint, vertex, into, layout,
                overInfluenced);
  }
  if (layout.morph) {
    std::memcpy(into + layout.vertexAt, &vertex, sizeof vertex);
  }
}

void Converter::packWeights(const BuiltMesh &built,
                            const std::vector<uint32_t> &clusterJoint,
                            uint32_t nodeJoint, uint32_t vertex, uint8_t *into,
                            const Layout &layout, size_t &overInfluenced) {
  const ufbx_skin_deformer *skin = built.skin;
  std::vector<std::pair<uint32_t, double>> &influences = influences_;
  influences.clear();
  if (vertex < skin->vertices.count) {
    const ufbx_skin_vertex &sv = skin->vertices.data[vertex];
    for (uint32_t w = 0; w < sv.num_weights; w++) {
      const size_t at = size_t(sv.weight_begin) + w;
      if (at >= skin->weights.count) break;
      const ufbx_skin_weight &weight = skin->weights.data[at];
      if (weight.cluster_index >= clusterJoint.size()) continue;
      const uint32_t joint = clusterJoint[weight.cluster_index];
      if (joint == UINT32_MAX || !(weight.weight > 0) ||
          !std::isfinite(weight.weight)) {
        continue;
      }
      bool merged = false;
      for (auto &existing : influences) {
        if (existing.first == joint) {
          existing.second += weight.weight;
          merged = true;
        }
      }
      if (!merged) influences.emplace_back(joint, weight.weight);
    }
  }
  // Largest first, and the lower joint first between equals, so the four
  // kept are the same four on every run.
  std::sort(influences.begin(), influences.end(),
            [](const std::pair<uint32_t, double> &a,
               const std::pair<uint32_t, double> &b) {
              if (a.second != b.second) return a.second > b.second;
              return a.first < b.first;
            });
  if (influences.size() > 4) {
    overInfluenced++;
    influences.resize(4);
  }
  double total = 0;
  for (const auto &influence : influences) total += influence.second;

  uint16_t joints[4] = {0, 0, 0, 0};
  float weights[4] = {0, 0, 0, 0};
  if (!(total > 1e-12)) {
    joints[0] = uint16_t(nodeJoint);
    weights[0] = 1;
  } else {
    float sum = 0;
    for (size_t i = 0; i < influences.size(); i++) {
      joints[i] = uint16_t(influences[i].first);
      weights[i] = float(influences[i].second / total);
      sum += weights[i];
    }
    // Whatever float rounding left over goes on the largest, so the four
    // sum to one as glTF's validator measures it.
    weights[0] += 1.0f - sum;
  }
  std::memcpy(into + layout.jointsAt, joints, sizeof joints);
  putBytes(into, layout.weightsAt, weights, 4);
}

std::string Converter::writePrimitive(const ufbx_mesh *mesh, BuiltMesh &built,
                                      const Layout &layout,
                                      const VertexSet &vertices,
                                      const std::vector<uint32_t> &indices,
                                      const ufbx_material *material,
                                      bool targetNormals,
                                      const ufbx_matrix *restOfUnbound) {
  const uint32_t count = vertices.count();
  std::vector<float> positions(size_t(count) * 3), normals(size_t(count) * 3);
  std::vector<float> uvs, colours, weights;
  std::vector<uint16_t> joints;
  if (layout.uv) uvs.resize(size_t(count) * 2);
  if (layout.colour) colours.resize(size_t(count) * 4);
  if (layout.skin) {
    joints.resize(size_t(count) * 4);
    weights.resize(size_t(count) * 4);
  }
  bool unbound = false;
  for (uint32_t i = 0; i < count; i++) {
    const uint8_t *v = vertices.at(i);
    std::memcpy(&positions[size_t(i) * 3], v, 12);
    std::memcpy(&normals[size_t(i) * 3], v + 12, 12);
    if (layout.uv) std::memcpy(&uvs[size_t(i) * 2], v + layout.uvAt, 8);
    if (layout.colour) std::memcpy(&colours[size_t(i) * 4], v + layout.colourAt, 16);
    const ufbx_vec3 local = vec3(positions[size_t(i) * 3],
                                 positions[size_t(i) * 3 + 1],
                                 positions[size_t(i) * 3 + 2]);
    if (layout.skin) {
      std::memcpy(&joints[size_t(i) * 4], v + layout.jointsAt, 8);
      std::memcpy(&weights[size_t(i) * 4], v + layout.weightsAt, 16);
      ufbx_vec3 world = vec3(0, 0, 0);
      for (int k = 0; k < 4; k++) {
        const float w = weights[size_t(i) * 4 + k];
        const uint16_t joint = joints[size_t(i) * 4 + k];
        if (w <= 0) continue;
        ufbx_vec3 moved;
        if (joint < built.clusters.size()) {
          moved = transformPoint(built.clusters[joint]->geometry_to_world, local);
        } else {
          unbound = true;
          moved = transformPoint(*restOfUnbound, local);
        }
        world.x += w * moved.x;
        world.y += w * moved.y;
        world.z += w * moved.z;
      }
      built.box.add(world);
    } else {
      built.box.add(local);
    }
  }
  if (unbound) built.needsNodeJoint = true;

  std::string json = "{\"attributes\":{\"POSITION\":" +
                     std::to_string(doc_.floats(positions, 3, "VEC3", kArrayBuffer, true)) +
                     ",\"NORMAL\":" +
                     std::to_string(doc_.floats(normals, 3, "VEC3", kArrayBuffer, false));
  if (layout.uv) {
    json += ",\"TEXCOORD_0\":" +
            std::to_string(doc_.floats(uvs, 2, "VEC2", kArrayBuffer, false));
  }
  if (layout.colour) {
    json += ",\"COLOR_0\":" +
            std::to_string(doc_.floats(colours, 4, "VEC4", kArrayBuffer, false));
  }
  if (layout.skin) {
    // A byte a joint while the skin's joints (and the node joint) fit.
    const bool bytes = built.joints.size() + 1 <= 256;
    uint32_t accessor;
    if (bytes) {
      std::vector<uint8_t> narrow(joints.size());
      for (size_t i = 0; i < joints.size(); i++) narrow[i] = uint8_t(joints[i]);
      accessor = doc_.integers(narrow.data(), count, 1, kUnsignedByte, "VEC4", 4,
                               kArrayBuffer);
    } else {
      accessor = doc_.integers(joints.data(), count, 2, kUnsignedShort, "VEC4", 4,
                               kArrayBuffer);
    }
    json += ",\"JOINTS_0\":" + std::to_string(accessor) + ",\"WEIGHTS_0\":" +
            std::to_string(doc_.floats(weights, 4, "VEC4", kArrayBuffer, false));
  }
  json += '}';

  uint32_t indexAccessor;
  if (count <= 65535) {
    std::vector<uint16_t> narrow(indices.begin(), indices.end());
    indexAccessor = doc_.integers(narrow.data(), narrow.size(), 2, kUnsignedShort,
                                  "SCALAR", 1, kElementArrayBuffer);
  } else {
    indexAccessor = doc_.integers(indices.data(), indices.size(), 4, kUnsignedInt,
                                  "SCALAR", 1, kElementArrayBuffer);
  }
  json += ",\"indices\":" + std::to_string(indexAccessor);

  if (material) {
    json += ",\"material\":" +
            std::to_string(writeMaterial(material, layout.uv, mesh));
  }

  if (layout.morph) {
    json += ",\"targets\":[";
    std::vector<float> deltas(size_t(count) * 3), normalDeltas;
    if (targetNormals) normalDeltas.resize(size_t(count) * 3);
    for (size_t t = 0; t < built.targets.size(); t++) {
      const ufbx_blend_shape *shape = built.targets[t]->target_shape;
      for (uint32_t i = 0; i < count; i++) {
        uint32_t vertex;
        std::memcpy(&vertex, vertices.at(i) + layout.vertexAt, sizeof vertex);
        ufbx_vec3 offset = vec3(0, 0, 0), normalOffset = vec3(0, 0, 0);
        const uint32_t at = vertex == UINT32_MAX
                                ? UFBX_NO_INDEX
                                : ufbx_get_blend_shape_offset_index(shape, vertex);
        if (at != UFBX_NO_INDEX && at < shape->position_offsets.count) {
          double scale = 1;
          if (at < shape->offset_weights.count) scale = shape->offset_weights.data[at];
          offset = shape->position_offsets.data[at];
          offset = vec3(offset.x * scale, offset.y * scale, offset.z * scale);
          if (targetNormals && at < shape->normal_offsets.count) {
            normalOffset = shape->normal_offsets.data[at];
            normalOffset = vec3(normalOffset.x * scale, normalOffset.y * scale,
                                normalOffset.z * scale);
          }
        }
        if (!finite3(offset)) offset = vec3(0, 0, 0);
        if (!finite3(normalOffset)) normalOffset = vec3(0, 0, 0);
        deltas[size_t(i) * 3] = float(offset.x);
        deltas[size_t(i) * 3 + 1] = float(offset.y);
        deltas[size_t(i) * 3 + 2] = float(offset.z);
        if (targetNormals) {
          normalDeltas[size_t(i) * 3] = float(normalOffset.x);
          normalDeltas[size_t(i) * 3 + 1] = float(normalOffset.y);
          normalDeltas[size_t(i) * 3 + 2] = float(normalOffset.z);
        }
      }
      if (t > 0) json += ',';
      json += "{\"POSITION\":" +
              std::to_string(doc_.floats(deltas, 3, "VEC3", kArrayBuffer, true));
      if (targetNormals) {
        json += ",\"NORMAL\":" +
                std::to_string(doc_.floats(normalDeltas, 3, "VEC3", kArrayBuffer, false));
      }
      json += '}';
    }
    json += ']';
  }
  json += '}';
  return json;
}

}  // namespace glb
}  // namespace orblit
