#pragma once

// What the FBX and OBJ importer is made of, shared by its four sources and
// by nothing else.
//
// The importer reads a file with ufbx and writes a GLB, and the writing is
// three jobs over one `Document`: the meshes, the materials and their
// textures, and the node tree with its skins and animation. Each is a source
// of its own beside this one, and `Converter` is the state they share. Above
// it are the pieces they all reach for — the JSON writing, the path and image
// sniffing, the vertex table — which were file-scope helpers while this was
// one file, and are `inline` now that four sources see them.
//
// Not a public header. OrblitImport.h is the importer's interface; nothing
// outside these four sources includes this one, and `orblit::glb` is where
// everything here lives so that names as short as `text` and `vec3` cannot
// reach the rest of the renderer.

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
namespace glb {

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

inline std::string text(const ufbx_string &s) {
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

inline void putString(std::string &out, const std::string &value) {
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
inline void putFloat(std::string &out, double value) {
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

inline void putFloats(std::string &out, const double *values, size_t count) {
  out += '[';
  for (size_t i = 0; i < count; i++) {
    if (i > 0) out += ',';
    putFloat(out, values[i]);
  }
  out += ']';
}

inline void putList(std::string &out, const char *key,
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

inline std::string indexList(const std::vector<uint32_t> &indices) {
  std::string out = "[";
  for (size_t i = 0; i < indices.size(); i++) {
    if (i > 0) out += ',';
    out += std::to_string(indices[i]);
  }
  return out + "]";
}

// ---- Paths and images ----------------------------------------------------

inline bool looksAbsolute(const std::string &path) {
  if (path.empty()) return false;
  if (path[0] == '/' || path[0] == '\\') return true;
  if (path.size() >= 2 && path[1] == ':') return true;  // C:\ or C:/
  return path.find("://") != std::string::npos;
}

inline std::string baseName(const std::string &path) {
  const size_t slash = path.find_last_of("/\\");
  return slash == std::string::npos ? path : path.substr(slash + 1);
}

inline std::string lowerExtension(const std::string &path) {
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
inline std::string percentEncode(const std::string &path) {
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
inline const char *sniffImage(const uint8_t *data, size_t size) {
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
inline const char *imageTypeOf(const std::string &path) {
  const std::string extension = lowerExtension(path);
  if (extension == "png") return "image/png";
  if (extension == "jpg" || extension == "jpeg") return "image/jpeg";
  if (extension == "ktx2") return "image/ktx2";
  return nullptr;
}

// ---- Math ----------------------------------------------------------------

inline ufbx_vec3 vec3(double x, double y, double z) {
  ufbx_vec3 v;
  v.x = x;
  v.y = y;
  v.z = z;
  return v;
}

inline ufbx_vec3 transformPoint(const ufbx_matrix &m, const ufbx_vec3 &v) {
  return vec3(m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03,
              m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13,
              m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23);
}

inline bool finite3(const ufbx_vec3 &v) {
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

inline void putBytes(uint8_t *into, size_t at, const float *values,
                     size_t count) {
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

  void run(ImportSummary &summary);

  std::vector<uint8_t> glb() const { return doc_.glb(); }

 private:
  // -- What glTF has no place for ------------------------------------------

  void noteUnsupported();

  // -- Meshes ----------------------------------------------------------------

  void collectMeshes();
  void prepareSkin(const ufbx_mesh *mesh, BuiltMesh &built,
                   std::vector<uint32_t> &clusterJoint);
  void prepareTargets(const ufbx_mesh *mesh, BuiltMesh &built);
  void buildMesh(const ufbx_node *node, const ufbx_mesh *mesh,
                 const ufbx_material_list &materials, BuiltMesh &built);
  void packVertex(const ufbx_mesh *mesh, const BuiltMesh &built,
                  const Layout &layout,
                  const std::vector<uint32_t> &clusterJoint,
                  uint32_t nodeJoint, uint32_t index, uint8_t *into,
                  size_t &overInfluenced);
  void packWeights(const BuiltMesh &built,
                   const std::vector<uint32_t> &clusterJoint,
                   uint32_t nodeJoint, uint32_t vertex, uint8_t *into,
                   const Layout &layout, size_t &overInfluenced);
  std::string writePrimitive(const ufbx_mesh *mesh, BuiltMesh &built,
                             const Layout &layout, const VertexSet &vertices,
                             const std::vector<uint32_t> &indices,
                             const ufbx_material *material, bool targetNormals,
                             const ufbx_matrix *restOfUnbound);

  // -- Materials and textures -----------------------------------------------

  int writeMaterial(const ufbx_material *material, bool hasUv,
                    const ufbx_mesh *mesh);
  static const ufbx_texture *fileOf(const ufbx_texture *t);
  bool sameImage(const ufbx_texture *a, const ufbx_texture *b) const;
  int writeTexture(const ufbx_texture *given, const char *slot,
                   const std::string &materialName);
  int writeImage(const ufbx_texture *file, bool &ktx2);

  // -- Nodes and skins ------------------------------------------------------

  static const ufbx_node *topLevel(const ufbx_node *node);
  static bool identity(const ufbx_transform &t);
  void planNodes();
  void measure();
  void writeSkins();
  static void putName(std::string &json, const ufbx_node *node);
  void writeNodes();
  void putTransform(std::string &json, const ufbx_transform &t);
  static bool normalise(double q[4]);

  // -- Animation -------------------------------------------------------------

  /// Channels for one clip, with time accessors shared: a clip baked at
  /// thirty frames a second has hundreds of channels on the same times.
  struct Clip {
    std::vector<std::string> samplers, channels;
    std::map<std::string, uint32_t> inputs;
    float end = 0;
  };

  static std::vector<size_t> keepIncreasing(const std::vector<double> &times,
                                            double start,
                                            std::vector<float> &kept);
  void addChannel(Clip &clip, const std::vector<float> &times,
                  const std::vector<float> &values, size_t components,
                  const char *type, int node, const char *path);
  static bool sameValue(double a, double b);
  void vectorChannel(Clip &clip, const ufbx_baked_vec3_list &keys, bool constant,
                     const ufbx_vec3 &rest, int node, const char *path,
                     double start);
  void rotationChannel(Clip &clip, const ufbx_baked_quat_list &keys, bool constant,
                       const ufbx_quat &rest, int node, double start);
  void weightChannels(Clip &clip, ufbx_baked_anim *baked, double start);
  void writeAnimations();

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

}  // namespace glb
}  // namespace orblit
