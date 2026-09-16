#include "OrblitImport.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <map>
#include <memory>
#include <new>
#include <set>
#include <utility>

#include "third_party/ufbx/ufbx.h"

namespace orblit {

bool importsAsGlb(const std::string &name) {
  auto endsWith = [&](const char *suffix) {
    const size_t length = std::strlen(suffix);
    if (name.size() <= length) return false;
    for (size_t i = 0; i < length; i++) {
      const char c = name[name.size() - length + i];
      const char lower = (c >= 'A' && c <= 'Z') ? char(c - 'A' + 'a') : c;
      if (lower != suffix[i]) return false;
    }
    return true;
  };
  return endsWith(".fbx") || endsWith(".obj");
}

namespace {

/// An OBJ's material library is a few kilobytes of text. Anything past this
/// is not one, and is not copied into ufbx to find out.
constexpr size_t kMaxCompanionBytes = size_t(64) * 1024 * 1024;

/// ufbx's own ceiling, for the scene it builds and the scratch it builds it
/// with, each. Well past what a real file needs — ufbx holds a 100 MB
/// character in a few hundred — and under what a 32-bit browser heap has,
/// so a file crafted to claim a billion vertices fails with a note rather
/// than taking the process's memory.
constexpr size_t kUfbxMemoryLimit = size_t(1024) * 1024 * 1024;

/// The GLB's binary chunk. glTF's lengths are 32-bit, and a model this large
/// would not load on the devices the renderer runs on anyway.
constexpr uint64_t kMaxBinaryBytes = uint64_t(1024) * 1024 * 1024;

/// Deeper hierarchies are refused by ufbx. gltfio walks a node tree by
/// recursion, and a file of ten thousand nested nodes would take the
/// renderer's stack with it long after this function had returned.
constexpr uint32_t kNodeDepthLimit = 256;

/// Losses kept word for word. A malformed file can have a thousand broken
/// textures, and a thousand lines saying so help nobody.
constexpr size_t kMaxLosses = 64;

constexpr int kFloat = 5126;
constexpr int kUnsignedByte = 5121;
constexpr int kUnsignedShort = 5123;
constexpr int kUnsignedInt = 5125;
constexpr int kArrayBuffer = 34962;
constexpr int kElementArrayBuffer = 34963;

/// How a conversion ends early: the note a person reads. Thrown inside this
/// file and caught at its one public entry, never further.
struct Failure {
  std::string note;
};

std::string text(const ufbx_string &s) {
  return s.data ? std::string(s.data, s.length) : std::string();
}

/// What was in the file and did not survive, each line once, in the order
/// it was first met.
class Losses {
 public:
  void add(const std::string &line) {
    if (seen_.count(line) > 0) return;
    if (lines_.size() >= kMaxLosses) {
      more_++;
      return;
    }
    seen_.insert(line);
    lines_.push_back(line);
  }

  std::vector<std::string> take() {
    if (more_ > 0) {
      lines_.push_back("and " + std::to_string(more_) + " more");
    }
    return std::move(lines_);
  }

 private:
  std::set<std::string> seen_;
  std::vector<std::string> lines_;
  size_t more_ = 0;
};

// ---- JSON ----------------------------------------------------------------

void putString(std::string &out, const std::string &value) {
  out += '"';
  for (const unsigned char c : value) {
    switch (c) {
      case '"': out += "\\\""; break;
      case '\\': out += "\\\\"; break;
      case '\n': out += "\\n"; break;
      case '\r': out += "\\r"; break;
      case '\t': out += "\\t"; break;
      default:
        if (c < 0x20) {
          char escaped[8];
          std::snprintf(escaped, sizeof escaped, "\\u%04x", unsigned(c));
          out += escaped;
        } else {
          out += char(c);
        }
    }
  }
  out += '"';
}

/// A number as glTF reads it: a 32-bit float, written with the nine
/// significant digits that bring the same float back. Accessor bounds are
/// compared with the data exactly, so the text has to round-trip; and a
/// decimal comma from a C locale set elsewhere in the process would be a
/// different file, so the one character that can differ is put right.
void putFloat(std::string &out, double value) {
  const float f = float(value);
  if (!std::isfinite(f) || f == 0.0f) {
    out += '0';
    return;
  }
  char buffer[32];
  const int length = std::snprintf(buffer, sizeof buffer, "%.9g", double(f));
  for (int i = 0; i < length && i < int(sizeof buffer); i++) {
    out += buffer[i] == ',' ? '.' : buffer[i];
  }
}

void putFloats(std::string &out, const double *values, size_t count) {
  out += '[';
  for (size_t i = 0; i < count; i++) {
    if (i > 0) out += ',';
    putFloat(out, values[i]);
  }
  out += ']';
}

void putList(std::string &out, const char *key,
             const std::vector<std::string> &items) {
  if (items.empty()) return;
  out += ",\"";
  out += key;
  out += "\":[";
  for (size_t i = 0; i < items.size(); i++) {
    if (i > 0) out += ',';
    out += items[i];
  }
  out += ']';
}

std::string indexList(const std::vector<uint32_t> &indices) {
  std::string out = "[";
  for (size_t i = 0; i < indices.size(); i++) {
    if (i > 0) out += ',';
    out += std::to_string(indices[i]);
  }
  return out + "]";
}

// ---- Paths and images ----------------------------------------------------

bool looksAbsolute(const std::string &path) {
  if (path.empty()) return false;
  if (path[0] == '/' || path[0] == '\\') return true;
  if (path.size() >= 2 && path[1] == ':') return true;  // C:\ or C:/
  return path.find("://") != std::string::npos;
}

std::string baseName(const std::string &path) {
  const size_t slash = path.find_last_of("/\\");
  return slash == std::string::npos ? path : path.substr(slash + 1);
}

std::string lowerExtension(const std::string &path) {
  const std::string base = baseName(path);
  const size_t dot = base.find_last_of('.');
  if (dot == std::string::npos) return std::string();
  std::string extension = base.substr(dot + 1);
  for (char &c : extension) {
    if (c >= 'A' && c <= 'Z') c = char(c - 'A' + 'a');
  }
  return extension;
}

/// A relative path as a URI: every byte but the unreserved characters and
/// the separator percent-encoded, so a space is %20 and a name in any script
/// survives as UTF-8. The renderer decodes it back to the same path.
std::string percentEncode(const std::string &path) {
  static const char kHex[] = "0123456789ABCDEF";
  std::string out;
  for (const unsigned char c : path) {
    const bool unreserved = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                            (c >= '0' && c <= '9') || c == '-' || c == '.' ||
                            c == '_' || c == '~' || c == '/';
    if (unreserved) {
      out += char(c);
    } else {
      out += '%';
      out += kHex[c >> 4];
      out += kHex[c & 15];
    }
  }
  return out;
}

/// What an image's first bytes say it is, for the three kinds glTF and
/// gltfio take. Null for anything else.
const char *sniffImage(const uint8_t *data, size_t size) {
  static const uint8_t kPng[] = {0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A};
  static const uint8_t kJpeg[] = {0xFF, 0xD8, 0xFF};
  static const uint8_t kKtx2[] = {0xAB, 'K', 'T', 'X', ' ', '2', '0',
                                  0xBB, 0x0D, 0x0A, 0x1A, 0x0A};
  if (size >= sizeof kPng && std::memcmp(data, kPng, sizeof kPng) == 0) {
    return "image/png";
  }
  if (size >= sizeof kJpeg && std::memcmp(data, kJpeg, sizeof kJpeg) == 0) {
    return "image/jpeg";
  }
  if (size >= sizeof kKtx2 && std::memcmp(data, kKtx2, sizeof kKtx2) == 0) {
    return "image/ktx2";
  }
  return nullptr;
}

/// The media type for a referenced file, from its name. Written into the
/// GLB rather than left to the reader: gltfio guesses from the extension
/// case-sensitively, and "male-02.JPG" would otherwise be "image/JPG", which
/// nothing decodes.
const char *imageTypeOf(const std::string &path) {
  const std::string extension = lowerExtension(path);
  if (extension == "png") return "image/png";
  if (extension == "jpg" || extension == "jpeg") return "image/jpeg";
  if (extension == "ktx2") return "image/ktx2";
  return nullptr;
}

// ---- Math ----------------------------------------------------------------

ufbx_vec3 vec3(double x, double y, double z) {
  ufbx_vec3 v;
  v.x = x;
  v.y = y;
  v.z = z;
  return v;
}

ufbx_vec3 transformPoint(const ufbx_matrix &m, const ufbx_vec3 &v) {
  return vec3(m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03,
              m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13,
              m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23);
}

bool finite3(const ufbx_vec3 &v) {
  return std::isfinite(v.x) && std::isfinite(v.y) && std::isfinite(v.z);
}

/// An element of one of ufbx's indexed attributes, or zero when the index
/// is out of range. ufbx clamps indices while loading, so this never fires
/// on a file it accepted; it is here so a mistake in that promise is a wrong
/// vertex rather than a read past the end.
template <typename Value, typename Attribute>
Value attributeAt(const Attribute &attribute, size_t index) {
  Value zero{};
  if (!attribute.exists || index >= attribute.indices.count) return zero;
  const uint32_t at = attribute.indices.data[index];
  return at < attribute.values.count ? attribute.values.data[at] : zero;
}

/// The box a point set grows, kept as doubles until it is written.
struct Box {
  bool empty = true;
  double minimum[3] = {0, 0, 0};
  double maximum[3] = {0, 0, 0};

  void add(const ufbx_vec3 &p) {
    if (!finite3(p)) return;
    const double v[3] = {p.x, p.y, p.z};
    for (int a = 0; a < 3; a++) {
      if (empty || v[a] < minimum[a]) minimum[a] = v[a];
      if (empty || v[a] > maximum[a]) maximum[a] = v[a];
    }
    empty = false;
  }

  void add(const Box &other) {
    if (other.empty) return;
    add(vec3(other.minimum[0], other.minimum[1], other.minimum[2]));
    add(vec3(other.maximum[0], other.maximum[1], other.maximum[2]));
  }
};

// ---- The GLB being written -----------------------------------------------

class Document {
 public:
  std::vector<uint8_t> bin;
  std::vector<std::string> bufferViews, accessors, images, samplers, textures,
      materials, meshes, nodes, skins, animations;
  std::vector<uint32_t> sceneNodes;
  // Ordered sets, so the lists come out the same on every run.
  std::set<std::string> extensionsUsed, extensionsRequired;

  /// `size` bytes as a buffer view, started on a four-byte boundary so
  /// every accessor in the file is aligned whatever its component type.
  uint32_t view(const void *data, size_t size, int target) {
    while (bin.size() % 4 != 0) bin.push_back(0);
    if (uint64_t(bin.size()) + size > kMaxBinaryBytes) {
      throw Failure{"the converted model would be larger than " +
                    std::to_string(kMaxBinaryBytes / (1024 * 1024)) +
                    " MB, which is more than a GLB here may hold"};
    }
    const size_t offset = bin.size();
    const uint8_t *bytes = static_cast<const uint8_t *>(data);
    bin.insert(bin.end(), bytes, bytes + size);
    std::string json = "{\"buffer\":0,\"byteOffset\":" +
                       std::to_string(offset) +
                       ",\"byteLength\":" + std::to_string(size);
    if (target != 0) json += ",\"target\":" + std::to_string(target);
    json += '}';
    bufferViews.push_back(std::move(json));
    return uint32_t(bufferViews.size() - 1);
  }

  /// Float components, with the per-component bounds glTF asks for on
  /// positions and animation times when `bounds` is set.
  uint32_t floats(const std::vector<float> &values, size_t components,
                  const char *type, int target, bool bounds) {
    const size_t count = values.size() / components;
    const uint32_t v =
        view(values.data(), values.size() * sizeof(float), target);
    std::string json = "{\"bufferView\":" + std::to_string(v) +
                       ",\"componentType\":" + std::to_string(kFloat) +
                       ",\"count\":" + std::to_string(count) +
                       ",\"type\":\"" + type + "\"";
    if (bounds && count > 0) {
      std::vector<double> low(components), high(components);
      for (size_t c = 0; c < components; c++) low[c] = high[c] = values[c];
      for (size_t i = 0; i < count; i++) {
        for (size_t c = 0; c < components; c++) {
          const double value = values[i * components + c];
          low[c] = std::min(low[c], value);
          high[c] = std::max(high[c], value);
        }
      }
      json += ",\"min\":";
      putFloats(json, low.data(), components);
      json += ",\"max\":";
      putFloats(json, high.data(), components);
    }
    json += '}';
    accessors.push_back(std::move(json));
    return uint32_t(accessors.size() - 1);
  }

  uint32_t integers(const void *data, size_t count, size_t componentBytes,
                    int componentType, const char *type, size_t components,
                    int target) {
    const uint32_t v = view(data, count * components * componentBytes, target);
    accessors.push_back("{\"bufferView\":" + std::to_string(v) +
                        ",\"componentType\":" + std::to_string(componentType) +
                        ",\"count\":" + std::to_string(count) +
                        ",\"type\":\"" + type + "\"}");
    return uint32_t(accessors.size() - 1);
  }

  std::vector<uint8_t> glb() const {
    std::string json =
        "{\"asset\":{\"generator\":\"Orblit import\",\"version\":\"2.0\"}";
    auto names = [](const std::set<std::string> &set) {
      std::vector<std::string> quoted;
      for (const std::string &name : set) {
        std::string item;
        putString(item, name);
        quoted.push_back(std::move(item));
      }
      return quoted;
    };
    putList(json, "extensionsUsed", names(extensionsUsed));
    putList(json, "extensionsRequired", names(extensionsRequired));
    json += ",\"scene\":0,\"scenes\":[{";
    if (!sceneNodes.empty()) json += "\"nodes\":" + indexList(sceneNodes);
    json += "}]";
    putList(json, "nodes", nodes);
    putList(json, "meshes", meshes);
    putList(json, "skins", skins);
    putList(json, "animations", animations);
    putList(json, "materials", materials);
    putList(json, "textures", textures);
    putList(json, "images", images);
    putList(json, "samplers", samplers);
    putList(json, "accessors", accessors);
    putList(json, "bufferViews", bufferViews);
    if (!bin.empty()) {
      json += ",\"buffers\":[{\"byteLength\":" + std::to_string(bin.size()) +
              "}]";
    }
    json += '}';
    while (json.size() % 4 != 0) json += ' ';

    const size_t binLength = (bin.size() + 3) & ~size_t(3);
    const uint64_t total = 12 + 8 + uint64_t(json.size()) +
                           (bin.empty() ? 0 : 8 + uint64_t(binLength));
    if (total > std::numeric_limits<uint32_t>::max()) {
      throw Failure{"the converted model is too large for a GLB"};
    }

    std::vector<uint8_t> out;
    out.reserve(size_t(total));
    auto u32 = [&](uint32_t value) {
      for (int i = 0; i < 4; i++) out.push_back(uint8_t(value >> (8 * i)));
    };
    u32(0x46546C67);  // glTF
    u32(2);
    u32(uint32_t(total));
    u32(uint32_t(json.size()));
    u32(0x4E4F534A);  // JSON
    out.insert(out.end(), json.begin(), json.end());
    if (!bin.empty()) {
      u32(uint32_t(binLength));
      u32(0x004E4942);  // BIN
      out.insert(out.end(), bin.begin(), bin.end());
      out.resize(size_t(total), 0);
    }
    return out;
  }
};

// ---- Vertices --------------------------------------------------------------

/// Vertices made unique by their bytes, numbered in the order they are
/// first met. Open addressing over the packed bytes rather than a map keyed
/// on them: a character's worth of triangles is a million insertions, and
/// the numbering depends only on the input order, never on the table.
class VertexSet {
 public:
  explicit VertexSet(size_t stride) : stride_(stride) {
    slots_.assign(1024, kEmpty);
  }

  uint32_t add(const uint8_t *vertex) {
    if ((size_t(count_) + 1) * 2 > slots_.size()) grow();
    const uint32_t found = place(vertex, slots_);
    if (found != kEmpty) return found;
    data_.insert(data_.end(), vertex, vertex + stride_);
    return count_++;
  }

  uint32_t count() const { return count_; }
  const uint8_t *at(uint32_t i) const { return &data_[size_t(i) * stride_]; }

 private:
  static constexpr uint32_t kEmpty = std::numeric_limits<uint32_t>::max();

  uint64_t hash(const uint8_t *vertex) const {
    uint64_t h = 1469598103934665603ull;
    for (size_t i = 0; i < stride_; i++) {
      h = (h ^ vertex[i]) * 1099511628211ull;
    }
    return h;
  }

  /// The existing number for `vertex`, or kEmpty after claiming a slot for
  /// the next one.
  uint32_t place(const uint8_t *vertex, std::vector<uint32_t> &slots) {
    const size_t mask = slots.size() - 1;
    size_t at = size_t(hash(vertex)) & mask;
    for (;;) {
      const uint32_t slot = slots[at];
      if (slot == kEmpty) {
        slots[at] = count_;
        return kEmpty;
      }
      if (std::memcmp(&data_[size_t(slot) * stride_], vertex, stride_) == 0) {
        return slot;
      }
      at = (at + 1) & mask;
    }
  }

  void grow() {
    std::vector<uint32_t> bigger(slots_.size() * 2, kEmpty);
    const size_t mask = bigger.size() - 1;
    for (uint32_t i = 0; i < count_; i++) {
      size_t at = size_t(hash(this->at(i))) & mask;
      while (bigger[at] != kEmpty) at = (at + 1) & mask;
      bigger[at] = i;
    }
    slots_.swap(bigger);
  }

  size_t stride_;
  uint32_t count_ = 0;
  std::vector<uint8_t> data_;
  std::vector<uint32_t> slots_;
};

/// Where each attribute sits in a packed vertex. Only what the mesh has is
/// packed, so two corners that differ in nothing glTF keeps become one.
struct Layout {
  bool uv = false;
  bool colour = false;
  bool skin = false;
  bool morph = false;
  size_t uvAt = 0, colourAt = 0, jointsAt = 0, weightsAt = 0, vertexAt = 0;
  size_t stride = 24;  // position and normal, always

  Layout(bool hasUv, bool hasColour, bool hasSkin, bool hasMorph)
      : uv(hasUv), colour(hasColour), skin(hasSkin), morph(hasMorph) {
    if (uv) { uvAt = stride; stride += 8; }
    if (colour) { colourAt = stride; stride += 16; }
    if (skin) { jointsAt = stride; stride += 8; weightsAt = stride; stride += 16; }
    // The source vertex, for meshes with morph targets: two corners in the
    // same place can still move differently under a target.
    if (morph) { vertexAt = stride; stride += 4; }
  }
};

void putBytes(uint8_t *into, size_t at, const float *values, size_t count) {
  std::memcpy(into + at, values, count * sizeof(float));
}

// ---- The conversion --------------------------------------------------------

/// A glTF mesh made from one ufbx mesh with one assignment of materials. The
/// same mesh under two nodes with different materials is two of these; with
/// the same materials, one.
struct BuiltMesh {
  int index = -1;
  bool skinned = false;
  /// A vertex no bone moves. glTF has no "not skinned" for one vertex of a
  /// skinned mesh, so those are bound, fully, to one more joint: the node
  /// the mesh hangs from, which is what moves them in the original.
  bool needsNodeJoint = false;
  const ufbx_skin_deformer *skin = nullptr;
  std::vector<const ufbx_node *> joints;
  /// The first cluster of each joint, whose matrices are the joint's.
  std::vector<const ufbx_skin_cluster *> clusters;
  std::vector<ufbx_matrix> inverseBinds;
  std::vector<const ufbx_blend_channel *> targets;
  /// Bounds: local for a static mesh, world for a skinned one (its rest
  /// pose is where the skeleton puts it, whatever node it hangs from).
  Box box;
};

struct Instance {
  const ufbx_node *node = nullptr;
  BuiltMesh *mesh = nullptr;
  /// The glTF node that carries the mesh (and skin).
  int meshNode = -1;
  /// A skinned mesh on a node of its own at the top of the scene, because
  /// glTF ignores the transform of a node with a skin and its validator
  /// asks for such nodes to be roots. When the original node has nothing
  /// else to do — no children, not a joint — it is that node, moved.
  bool movedToRoot = false;
};

class Converter {
 public:
  Converter(const ufbx_scene *scene, Losses &losses)
      : scene_(scene), losses_(losses) {}

  void run(ImportSummary &summary) {
    noteUnsupported();
    collectMeshes();
    planNodes();
    measure();
    writeSkins();
    writeNodes();
    writeAnimations();

    if (doc_.nodes.empty()) {
      throw Failure{"the file has no nodes or geometry to import"};
    }
    summary.nodes = uint32_t(doc_.nodes.size());
    summary.meshes = uint32_t(doc_.meshes.size());
    summary.materials = uint32_t(doc_.materials.size());
    summary.skins = uint32_t(doc_.skins.size());
    summary.primitives = primitives_;
    summary.joints = joints_;
    summary.morphTargets = morphTargets_;
    summary.embeddedImages = embeddedImages_;
    summary.referencedImages = referencedImages_;
    summary.clips = clips_;
    if (!sceneBox_.empty) {
      summary.hasBounds = true;
      for (int a = 0; a < 3; a++) {
        summary.minimum[a] = float(sceneBox_.minimum[a]);
        summary.maximum[a] = float(sceneBox_.maximum[a]);
      }
    }
  }

  std::vector<uint8_t> glb() const { return doc_.glb(); }

 private:
  // -- What glTF has no place for ------------------------------------------

  void noteUnsupported() {
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

  void collectMeshes() {
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
  void prepareSkin(const ufbx_mesh *mesh, BuiltMesh &built,
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

  void prepareTargets(const ufbx_mesh *mesh, BuiltMesh &built) {
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

  void buildMesh(const ufbx_node *node, const ufbx_mesh *mesh,
                 const ufbx_material_list &materials, BuiltMesh &built) {
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
  void packVertex(const ufbx_mesh *mesh, const BuiltMesh &built,
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

  void packWeights(const BuiltMesh &built,
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

  std::string writePrimitive(const ufbx_mesh *mesh, BuiltMesh &built,
                             const Layout &layout, const VertexSet &vertices,
                             const std::vector<uint32_t> &indices,
                             const ufbx_material *material, bool targetNormals,
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

  // -- Materials and textures -----------------------------------------------

  int writeMaterial(const ufbx_material *material, bool hasUv,
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
  static const ufbx_texture *fileOf(const ufbx_texture *t) {
    if (t->type == UFBX_TEXTURE_FILE) return t;
    return t->file_textures.count > 0 ? t->file_textures.data[0] : nullptr;
  }

  bool sameImage(const ufbx_texture *a, const ufbx_texture *b) const {
    a = fileOf(a);
    b = fileOf(b);
    if (!a || !b) return false;
    if (a == b) return true;
    if (a->has_file && b->has_file) return a->file_index == b->file_index;
    return text(a->filename) == text(b->filename) &&
           text(a->relative_filename) == text(b->relative_filename);
  }

  int writeTexture(const ufbx_texture *given, const char *slot,
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

  int writeImage(const ufbx_texture *file, bool &ktx2) {
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

  // -- Nodes and skins ------------------------------------------------------

  static const ufbx_node *topLevel(const ufbx_node *node) {
    for (uint32_t guard = 0; node && node->parent && !node->parent->is_root &&
                             guard <= kNodeDepthLimit + 2;
         guard++) {
      node = node->parent;
    }
    return node;
  }

  static bool identity(const ufbx_transform &t) {
    return t.translation.x == 0 && t.translation.y == 0 && t.translation.z == 0 &&
           t.rotation.x == 0 && t.rotation.y == 0 && t.rotation.z == 0 &&
           t.rotation.w == 1 && t.scale.x == 1 && t.scale.y == 1 && t.scale.z == 1;
  }

  void planNodes() {
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
  void measure() {
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

  void writeSkins() {
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

  static void putName(std::string &json, const ufbx_node *node) {
    if (node->name.length == 0) return;
    json += "\"name\":";
    putString(json, text(node->name));
    json += ',';
  }

  void writeNodes() {
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

  void putTransform(std::string &json, const ufbx_transform &t) {
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

  static bool normalise(double q[4]) {
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

  /// Channels for one clip, with time accessors shared: a clip baked at
  /// thirty frames a second has hundreds of channels on the same times.
  struct Clip {
    std::vector<std::string> samplers, channels;
    std::map<std::string, uint32_t> inputs;
    float end = 0;
  };

  /// Key times as glTF needs them: seconds from the clip's start as floats,
  /// strictly increasing, none negative. A key before the start stands in
  /// for the start only if it is the last one before it. Returns which keys
  /// survived, by position.
  static std::vector<size_t> keepIncreasing(const std::vector<double> &times,
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

  void addChannel(Clip &clip, const std::vector<float> &times,
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

  static bool sameValue(double a, double b) {
    return std::fabs(a - b) <= 1e-6 * std::max(1.0, std::max(std::fabs(a), std::fabs(b)));
  }

  void vectorChannel(Clip &clip, const ufbx_baked_vec3_list &keys, bool constant,
                     const ufbx_vec3 &rest, int node, const char *path,
                     double start) {
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

  void rotationChannel(Clip &clip, const ufbx_baked_quat_list &keys, bool constant,
                       const ufbx_quat &rest, int node, double start) {
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

  void weightChannels(Clip &clip, ufbx_baked_anim *baked, double start) {
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

  void writeAnimations() {
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

  const ufbx_scene *scene_;
  Losses &losses_;
  Document doc_;

  std::map<std::vector<uint32_t>, std::unique_ptr<BuiltMesh>> meshes_;
  std::vector<Instance> instances_;
  std::map<std::pair<uint32_t, bool>, int> materials_;
  std::map<std::pair<int, int>, int> textures_;
  std::map<std::pair<int, int>, int> samplers_;
  std::map<std::string, std::pair<int, bool>> images_;
  std::vector<int> hierarchy_;
  std::vector<bool> moved_;
  std::vector<uint32_t> skinOf_;
  bool emitRoot_ = false;
  std::vector<std::pair<uint32_t, double>> influences_;

  uint32_t primitives_ = 0, joints_ = 0, morphTargets_ = 0;
  uint32_t embeddedImages_ = 0, referencedImages_ = 0;
  std::vector<ImportedClip> clips_;
  Box sceneBox_;
};

// ---- Loading ---------------------------------------------------------------

struct Companions {
  const ImportReader *read = nullptr;
  Losses *losses = nullptr;
};

/// ufbx asks for the files a model names beside itself through this, and
/// only an OBJ's material library is ever answered. Geometry caches are
/// refused, and so is any path the file gives as absolute: a model from
/// somewhere else has no business reading this machine's files by name.
bool openCompanion(void *user, ufbx_stream *stream, const char *path,
                   size_t pathLength, const ufbx_open_file_info *info) {
  // Called from inside ufbx, which cannot unwind; nothing thrown gets past.
  try {
    const Companions *companions = static_cast<const Companions *>(user);
    if (!info || info->type != UFBX_OPEN_FILE_OBJ_MTL) return false;
    if (!companions->read || !*companions->read) return false;
    const std::string original(
        static_cast<const char *>(info->original_filename.data),
        info->original_filename.data ? info->original_filename.size : 0);
    if (looksAbsolute(original)) {
      companions->losses->add("material library '" + original +
                              "' is an absolute path; not read");
      return false;
    }
    const SharedBytes bytes = (*companions->read)(std::string(path, pathLength));
    if (!bytes) return false;
    if (bytes->size() > kMaxCompanionBytes) {
      companions->losses->add("material library '" + original +
                              "' is too large to be one; not read");
      return false;
    }
    static const uint8_t kNothing = 0;
    ufbx_open_memory_opts opts = {};
    ufbx_error error;
    return ufbx_open_memory_ctx(stream, info->context,
                                bytes->empty() ? &kNothing : bytes->data(),
                                bytes->size(), &opts, &error);
  } catch (...) {
    return false;
  }
}

Imported convert(const uint8_t *bytes, size_t size, const std::string &name,
                 const ImportReader &read) {
  const std::string shown = baseName(name);
  if (!importsAsGlb(name)) {
    throw Failure{"'" + shown + "' is not an .fbx or .obj file"};
  }
  if (!bytes || size == 0) throw Failure{"'" + shown + "' is empty"};
  if (size > kImportMaxInputBytes) {
    throw Failure{"'" + shown + "' is larger than the " +
                  std::to_string(kImportMaxInputBytes / (1024 * 1024)) +
                  " MB an import reads"};
  }
  const bool obj = lowerExtension(name) == "obj";

  Losses losses;
  Companions companions;
  companions.read = &read;
  companions.losses = &losses;

  ufbx_load_opts opts = {};
  opts.temp_allocator.memory_limit = kUfbxMemoryLimit;
  opts.result_allocator.memory_limit = kUfbxMemoryLimit;
  // The extension decides, not the content: a file named .fbx is only ever
  // read as FBX, whatever its first bytes claim.
  opts.file_format = obj ? UFBX_FILE_FORMAT_OBJ : UFBX_FILE_FORMAT_FBX;
  opts.no_format_from_content = true;
  opts.no_format_from_extension = true;

  // glTF's space: right-handed, +Y up, +Z the front, one unit a metre.
  opts.target_axes = ufbx_axes_right_handed_y_up;
  opts.target_unit_meters = 1;
  // An OBJ carries neither, and the usual exporters write Y-up metres, so
  // that is what it is taken to be: nothing is rotated or scaled.
  opts.obj_axes = ufbx_axes_right_handed_y_up;
  opts.obj_unit_meters = 1;
  // How the conversion is done matters to everything after it. Scaling the
  // root would leave 0.01 on a node every centimetre file has, and a skinned
  // mesh's joints under it. Adjusting transforms alone would leave the
  // vertices in centimetres under a node scaled to metres. Modifying the
  // geometry puts vertices, translations, blend shape offsets and every
  // skin's bind matrices in metres together, with node scales left as the
  // artist set them — so bounds, skinning and baked animation all agree.
  opts.space_conversion = UFBX_SPACE_CONVERSION_MODIFY_GEOMETRY;
  // A left-handed file is mirrored across X, the axis glTF exporters use,
  // and its faces rewound so they still face out.
  opts.handedness_conversion_axis = UFBX_MIRROR_AXIS_X;

  // FBX transforms have three things glTF nodes do not. A geometric
  // transform moves a node's mesh but not its children: a helper node
  // between the two carries it, which works for instanced meshes, where
  // changing the vertices would not.
  opts.geometry_transform_handling = UFBX_GEOMETRY_TRANSFORM_HANDLING_HELPER_NODES;
  // A node may ignore its parent's scale, or apply it per axis. glTF
  // inherits one way only: ufbx scales the children back where that is
  // exact (uniform, unanimated) and inserts a helper node where it is not.
  opts.inherit_mode_handling = UFBX_INHERIT_MODE_HANDLING_COMPENSATE;
  // Rotation and scaling pivots: the node is moved onto its rotation pivot
  // and its geometry and children moved back, so an animated rotation is a
  // rotation channel rather than a translation that follows it — which
  // baked keys interpolate exactly. Empties keep their authored origin, the
  // place a user would attach something to by name.
  opts.pivot_handling = UFBX_PIVOT_HANDLING_ADJUST_TO_ROTATION_PIVOT;
  opts.pivot_handling_retain_empties = true;

  opts.generate_missing_normals = true;
  opts.normalize_normals = true;
  opts.clean_skin_weights = true;
  // Blender writes its principled material into FBX's Phong slots in a way
  // ufbx can read back as metallic and roughness.
  opts.use_blender_pbr_material = true;
  opts.node_depth_limit = kNodeDepthLimit;

  // Paths inside the file resolve against this one's directory, using
  // whichever separator it was named with.
  opts.filename.data = name.c_str();
  opts.filename.length = name.size();
  opts.path_separator =
      name.find('\\') != std::string::npos && name.find('/') == std::string::npos
          ? '\\'
          : '/';
  opts.load_external_files = true;
  opts.ignore_missing_external_files = true;
  opts.obj_search_mtl_by_filename = true;
  opts.open_file_cb.fn = &openCompanion;
  opts.open_file_cb.user = &companions;

  ufbx_error error;
  std::unique_ptr<ufbx_scene, void (*)(ufbx_scene *)> scene(
      ufbx_load_memory(bytes, size, &opts, &error), ufbx_free_scene);
  if (!scene) {
    // ufbx's description of its commonest failure is "Failed to load", which
    // tells a person nothing they did not know; say what it means instead.
    const std::string why = error.type == UFBX_ERROR_UNKNOWN
                                ? std::string("the file is damaged, or not a valid ") +
                                      (obj ? "OBJ" : "FBX")
                            : error.type == UFBX_ERROR_MEMORY_LIMIT
                                ? "it needs more memory than an import may use"
                                : text(error.description);
    std::string note = "could not read '" + shown + "': " + why;
    if (error.info_length > 0 && error.info_length < sizeof error.info) {
      note += " (" + std::string(error.info, error.info_length) + ")";
    }
    throw Failure{note};
  }

  for (size_t w = 0; w < scene->metadata.warnings.count; w++) {
    const ufbx_warning &warning = scene->metadata.warnings.data[w];
    // Finding model.mtl for model.obj when the file named none is ufbx
    // doing what was asked, not something lost.
    if (warning.type == UFBX_WARNING_IMPLICIT_MTL) continue;
    std::string line = text(warning.description);
    if (warning.count > 1) line += " (" + std::to_string(warning.count) + " times)";
    losses.add(line);
  }

  Imported result;
  Converter converter(scene.get(), losses);
  converter.run(result.summary);
  result.glb = converter.glb();
  result.losses = losses.take();
  return result;
}

}  // namespace

Imported importToGlb(const uint8_t *bytes, size_t size, const std::string &name,
                     const ImportReader &read) {
  try {
    return convert(bytes, size, name, read);
  } catch (const Failure &failure) {
    Imported failed;
    failed.note = failure.note;
    return failed;
  } catch (const std::bad_alloc &) {
    Imported failed;
    failed.note = "ran out of memory converting '" + baseName(name) + "'";
    return failed;
  } catch (...) {
    Imported failed;
    failed.note = "could not convert '" + baseName(name) + "'";
    return failed;
  }
}

}  // namespace orblit
