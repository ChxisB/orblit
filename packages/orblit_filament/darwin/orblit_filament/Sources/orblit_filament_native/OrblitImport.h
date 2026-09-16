#pragma once

// FBX and OBJ, turned into GLB bytes in memory, so there is one loader after.
//
// The renderer reads glTF through Filament's gltfio and nothing else. An
// FBX or an OBJ is handed to this first: the file is read with ufbx, every
// part of it glTF has a place for is written into a GLB, and the GLB goes to
// gltfio exactly as a .glb on disk would. A second loader beside gltfio would
// be a second set of bugs in skinning, materials and animation, and a model
// that looked one way as a .glb and another as the .fbx it was made from.
//
// The same function is the offline tool — native/headless/orblit_import.cpp
// writes what it returns to a file — so a model converted ahead of time and
// one converted at load are the same bytes. They are the same bytes on every
// run, too: nothing in the output depends on a clock, an address or the
// order a hash table happened to keep, because a cache keyed on a hash of
// the output is what makes converting at load affordable the second time.
//
// Plain C++17, nothing platform-specific, no exceptions out of it. Input is
// treated as untrusted: a malformed file comes back as a note, never a crash.

#include <cstddef>
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

#include "OrblitResources.h"

namespace orblit {

/// The largest file importToGlb will look at, in bytes. Anything larger is
/// refused before a byte of it is read: a real FBX character is tens of
/// megabytes, and a gigabyte handed over by mistake should fail quickly
/// rather than take a phone's memory with it.
constexpr size_t kImportMaxInputBytes = size_t(512) * 1024 * 1024;

/// Whether `name` is a file this importer turns into GLB: a name ending in
/// .fbx or .obj, in any case.
bool importsAsGlb(const std::string &name);

/// One animation as it was written: the name it had in the source file and
/// how long glTF will play it for, which is the time of its last key.
struct ImportedClip {
  std::string name;
  float seconds = 0;
  uint32_t channels = 0;
};

/// What went into the GLB, counted, for a tool to print and a test to check
/// without parsing the JSON back.
struct ImportSummary {
  uint32_t nodes = 0;
  uint32_t meshes = 0;
  uint32_t primitives = 0;
  uint32_t materials = 0;
  uint32_t skins = 0;
  /// Joints across every skin, a joint shared by two skins counted twice.
  uint32_t joints = 0;
  uint32_t morphTargets = 0;
  uint32_t embeddedImages = 0;
  uint32_t referencedImages = 0;
  std::vector<ImportedClip> clips;
  /// The scene's extent in metres, in its rest pose, with skinned meshes
  /// placed where their skeleton puts them. False when nothing has a vertex.
  bool hasBounds = false;
  float minimum[3] = {0, 0, 0};
  float maximum[3] = {0, 0, 0};
};

struct Imported {
  /// A complete GLB, or empty when the file could not be imported.
  std::vector<uint8_t> glb;
  /// Why the file could not be imported, for a person to read. Empty on
  /// success.
  std::string note;
  /// What was in the file and is not in the GLB, one line each: a roughness
  /// texture glTF has no separate slot for, a material library that was not
  /// found. A GLB can come back with losses; they are why it may not look
  /// exactly like the original.
  std::vector<std::string> losses;
  ImportSummary summary;
};

/// Reads the bytes of a file an import names beside itself — an OBJ's .mtl —
/// by path, or null when there is no such file.
using ImportReader = std::function<SharedBytes(const std::string &path)>;

/// Converts the FBX or OBJ in `bytes` to GLB.
///
/// `name` is the file's path or resource name, the thing a scene named it
/// by. Its extension says which format to read, and its directory is what
/// companion files and textures are found relative to: an OBJ's material
/// library is asked of `read` beside it, and a texture the GLB refers to
/// rather than carries has a URI relative to that same directory, so the GLB
/// resolves it exactly as the original file would have. `read` may be empty,
/// in which case nothing beside the file is looked for.
///
/// The result is Y-up, right-handed and in metres whatever the file was
/// written in. An OBJ says nothing about its units and is taken as metres.
/// Any thread; nothing is shared between calls.
Imported importToGlb(const uint8_t *bytes, size_t size,
                     const std::string &name, const ImportReader &read);

}  // namespace orblit
