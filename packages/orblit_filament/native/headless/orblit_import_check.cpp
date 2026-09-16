// The FBX and OBJ importer's own checks, in C++ against OrblitImport.h.
//
// No GPU and no renderer: what is checked is the GLB itself. Two kinds of
// input. Small files written here, in memory, every run — an OBJ and its
// material library, and bytes that are not a model at all — where the
// answer is known exactly: which primitive gets which material, what the
// texture's URI is, that a library named by absolute path is never asked
// for. And real files, when there are some, where the answer is a range:
// a Mixamo character is a person's height and stands up the Y axis.
//
// Real files are not committed. Point this at a directory of them, as the
// first argument or ORBLIT_IMPORT_SAMPLES, laid out as the table in
// checkSamples() names them; any that are missing are skipped, and say so.
// Every file found is also converted twice and compared byte for byte, and
// then cut short and scribbled on, to show a broken file comes back as a
// note or a well-formed GLB and never as a crash.
//
// These check what this importer promises, not the whole of glTF; that is
// the Khronos glTF-Validator's job, and the GLBs it makes should be run
// through it whenever the importer changes.
//
// Built and run by build.sh beside the splat checks.

#include "OrblitImport.h"

#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iterator>
#include <random>
#include <string>
#include <vector>

namespace {

int failures = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    std::fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

std::vector<uint8_t> bytesOf(const std::string &text) {
  return std::vector<uint8_t>(text.begin(), text.end());
}

uint32_t u32(const std::vector<uint8_t> &b, size_t at) {
  return uint32_t(b[at]) | uint32_t(b[at + 1]) << 8 | uint32_t(b[at + 2]) << 16 |
         uint32_t(b[at + 3]) << 24;
}

/// Whether `glb` is a GLB as the spec lays one out — header, a JSON chunk
/// and an optional BIN chunk, each four-byte aligned, lengths that add up —
/// and its JSON text if so.
bool wellFormed(const std::vector<uint8_t> &glb, std::string &json) {
  if (glb.size() < 20 || u32(glb, 0) != 0x46546C67 || u32(glb, 4) != 2 ||
      u32(glb, 8) != glb.size()) {
    return false;
  }
  const uint32_t jsonLength = u32(glb, 12);
  if (jsonLength % 4 != 0 || u32(glb, 16) != 0x4E4F534A ||
      20 + uint64_t(jsonLength) > glb.size()) {
    return false;
  }
  json.assign(glb.begin() + 20, glb.begin() + 20 + jsonLength);
  const size_t binAt = 20 + jsonLength;
  if (binAt == glb.size()) return true;
  if (binAt + 8 > glb.size()) return false;
  const uint32_t binLength = u32(glb, binAt);
  return binLength % 4 == 0 && u32(glb, binAt + 4) == 0x004E4942 &&
         binAt + 8 + uint64_t(binLength) == glb.size();
}

bool contains(const std::string &text, const std::string &part) {
  return text.find(part) != std::string::npos;
}

bool anyLoss(const orblit::Imported &imported, const std::string &part) {
  for (const std::string &loss : imported.losses) {
    if (contains(loss, part)) return true;
  }
  return false;
}

/// Files by name from a table, recording every name asked for.
struct Files {
  std::vector<std::pair<std::string, std::string>> named;
  std::vector<std::string> asked;

  orblit::ImportReader reader() {
    return [this](const std::string &path) -> orblit::SharedBytes {
      asked.push_back(path);
      for (const auto &file : named) {
        if (file.first == path) {
          return std::make_shared<std::vector<uint8_t>>(bytesOf(file.second));
        }
      }
      return nullptr;
    };
  }
};

orblit::Imported importText(const std::string &text, const std::string &name,
                            Files &files) {
  const std::vector<uint8_t> bytes = bytesOf(text);
  return orblit::importToGlb(bytes.data(), bytes.size(), name, files.reader());
}

const char *kQuad =
    "mtllib quad.mtl\n"
    "o Quad\n"
    "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n"
    "vt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\n"
    "vn 0 0 1\n"
    "usemtl red\n"
    "f 1/1/1 2/2/1 3/3/1 4/4/1\n"
    "usemtl cutout\n"
    "f 1/1/1 3/3/1 4/4/1\n";

const char *kQuadLibrary =
    "newmtl red\nKd 1 0 0\nd 0.5\n"
    "newmtl cutout\nKd 1 1 1\nmap_Kd tex ture.png\nmap_d tex ture.png\n";

void checkNames() {
  expect(orblit::importsAsGlb("robot.fbx") && orblit::importsAsGlb("dir/Robot.FBX") &&
             orblit::importsAsGlb("a b/c.Obj"),
         ".fbx and .obj in any case import as GLB");
  expect(!orblit::importsAsGlb("robot.gltf") && !orblit::importsAsGlb("robot.glb") &&
             !orblit::importsAsGlb("fbx") && !orblit::importsAsGlb(".obj") &&
             !orblit::importsAsGlb("robot.fbx.png"),
         "other names do not");
}

void checkObj() {
  Files files;
  files.named = {{"models/quad.mtl", kQuadLibrary}};
  const orblit::Imported imported = importText(kQuad, "models/quad.obj", files);
  expect(imported.note.empty(), "a small OBJ imports: " + imported.note);

  std::string json;
  expect(wellFormed(imported.glb, json), "the OBJ becomes a well-formed GLB");
  expect(contains(json, "\"generator\":\"Orblit import\""), "the GLB says who wrote it");
  expect(files.asked.size() == 1 && files.asked[0] == "models/quad.mtl",
         "the material library is asked for beside the OBJ, once");

  const orblit::ImportSummary &s = imported.summary;
  expect(s.nodes == 1 && s.meshes == 1 && s.primitives == 2 && s.materials == 2,
         "one mesh, a primitive for each of its two materials");
  expect(s.referencedImages == 1 && s.embeddedImages == 0,
         "the texture is referenced, not read");
  expect(contains(json, "\"uri\":\"tex%20ture.png\",\"mimeType\":\"image/png\""),
         "its URI is relative and percent-encoded, with its type: " + json);
  expect(contains(json, "\"baseColorFactor\":[1,0,0,0.5]") &&
             contains(json, "\"alphaMode\":\"BLEND\""),
         "a half-opaque red material blends");
  expect(contains(json, "\"alphaMode\":\"MASK\""),
         "an opacity map that is the colour map makes a mask");
  expect(contains(json, "\"metallicFactor\":0"),
         "metallic is written as nought, not left to glTF's default of one");
  expect(contains(json, "\"min\":[0,0,0],\"max\":[1,1,0]"), "positions carry their bounds");
  expect(s.hasBounds && s.minimum[0] == 0 && s.maximum[0] == 1 && s.maximum[1] == 1 &&
             s.maximum[2] == 0,
         "the summary's bounds are the quad's, in metres, unscaled");
  expect(imported.losses.empty(), "nothing is lost from a plain OBJ");

  Files again;
  again.named = files.named;
  const orblit::Imported second = importText(kQuad, "models/quad.obj", again);
  expect(second.glb == imported.glb, "the same OBJ converts to the same bytes");
}

void checkObjCompanions() {
  {
    Files none;
    const orblit::Imported imported = importText(kQuad, "models/quad.obj", none);
    std::string json;
    expect(wellFormed(imported.glb, json), "an OBJ without its library still imports");
    expect(anyLoss(imported, "quad.mtl"), "and says the library was missing");
  }
  {
    Files files;
    files.named = {{"/etc/passwd", "newmtl stolen\n"}};
    std::string text = kQuad;
    text.replace(text.find("quad.mtl"), 8, "/etc/passwd");
    const orblit::Imported imported = importText(text, "models/quad.obj", files);
    bool askedForIt = false;
    for (const std::string &path : files.asked) {
      askedForIt = askedForIt || contains(path, "passwd");
    }
    expect(!askedForIt, "a library named by absolute path is never read");
    expect(anyLoss(imported, "absolute"), "and that is said");
    expect(!contains(std::string(imported.glb.begin(), imported.glb.end()), "stolen"),
           "nothing from it reaches the GLB");
  }
  {
    Files files;
    files.named = {{"quad.mtl", kQuadLibrary}};
    const std::string bare = "mtllib quad.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\n"
                             "usemtl cutout\nf 1 2 3\n";
    const orblit::Imported imported = importText(bare, "quad.obj", files);
    std::string json;
    expect(wellFormed(imported.glb, json), "an OBJ without texture coordinates imports");
    expect(!contains(json, "\"textures\"") && !contains(json, "\"MASK\""),
           "its material is written without the texture it cannot sample");
    expect(anyLoss(imported, "no texture coordinates"), "and that is said");
    expect(contains(json, "\"NORMAL\""), "normals are made where the file has none");
  }
}

void checkRefused() {
  Files files;
  auto refused = [&](const orblit::Imported &imported, const std::string &what) {
    expect(imported.glb.empty() && !imported.note.empty(), what + " is refused with a note");
  };
  const uint8_t one = 0;
  refused(orblit::importToGlb(nullptr, 0, "empty.fbx", files.reader()), "an empty file");
  refused(orblit::importToGlb(&one, 1, "model.gltf", files.reader()), "a name that is not FBX or OBJ");
  // The size is refused before a byte is read, so one byte of real memory
  // stands in for half a gigabyte.
  refused(orblit::importToGlb(&one, orblit::kImportMaxInputBytes + 1, "huge.fbx",
                              files.reader()),
          "a file over the size limit");

  std::mt19937 random(7);
  std::vector<uint8_t> noise(4096);
  for (uint8_t &b : noise) b = uint8_t(random());
  refused(orblit::importToGlb(noise.data(), noise.size(), "noise.fbx", files.reader()),
          "noise named .fbx");
  refused(orblit::importToGlb(noise.data(), noise.size(), "noise.obj", files.reader()),
          "noise named .obj");

  std::vector<uint8_t> header = bytesOf(std::string("Kaydara FBX Binary  \0\x1a\0", 23));
  const uint32_t version = 7400;
  for (int i = 0; i < 4; i++) header.push_back(uint8_t(version >> (8 * i)));
  header.insert(header.end(), noise.begin(), noise.begin() + 256);
  refused(orblit::importToGlb(header.data(), header.size(), "broken.fbx", files.reader()),
          "a binary FBX header followed by noise");
  refused(importText("; FBX 7.4.0 project file\nObjects: {\n  Model: 1, \"Model::",
                     "cut.fbx", files),
          "an ASCII FBX cut off mid-object");
  refused(importText("mtllib nothing.mtl\n# just a comment\n", "empty.obj", files),
          "an OBJ with nothing in it");
}

/// Whatever comes back from a damaged file must be a note or a GLB that is
/// well formed. Returns true for a note.
bool survives(const std::vector<uint8_t> &bytes, const std::string &name,
              const orblit::ImportReader &read, const std::string &what) {
  const orblit::Imported imported = orblit::importToGlb(bytes.data(), bytes.size(), name, read);
  std::string json;
  const bool note = imported.glb.empty();
  expect(note ? !imported.note.empty() : wellFormed(imported.glb, json),
         what + " comes back as a note or a well-formed GLB");
  return note;
}

void checkDamagedObj() {
  Files files;
  files.named = {{"quad.mtl", kQuadLibrary}};
  const std::vector<uint8_t> original = bytesOf(kQuad);
  std::mt19937 random(11);
  for (int round = 0; round < 300; round++) {
    std::vector<uint8_t> damaged = original;
    const int edits = 1 + int(random() % 8);
    for (int e = 0; e < edits; e++) {
      damaged[random() % damaged.size()] = uint8_t(random());
    }
    damaged.resize(1 + random() % damaged.size());
    survives(damaged, "quad.obj", files.reader(), "a scribbled-on OBJ");
  }
}

orblit::SharedBytes readDisk(const std::string &path) {
  std::ifstream file(path, std::ios::binary);
  if (!file) return nullptr;
  return std::make_shared<std::vector<uint8_t>>(std::istreambuf_iterator<char>(file),
                                                std::istreambuf_iterator<char>());
}

/// A real file, and what must be true of it.
struct Sample {
  const char *path;
  void (*check)(const std::string &path, const orblit::Imported &imported);
};

void checkMixamo(const std::string &path, const orblit::Imported &imported) {
  const orblit::ImportSummary &s = imported.summary;
  expect(s.skins >= 1 && s.joints >= 20, path + ": a skinned character");
  bool mixamo = false;
  for (const orblit::ImportedClip &clip : s.clips) {
    mixamo = mixamo || (clip.name == "mixamo.com" && clip.seconds > 1);
  }
  expect(mixamo, path + ": its Mixamo clip, with a duration");
  const float height = s.maximum[1] - s.minimum[1];
  expect(s.hasBounds && height > 1.7f && height < 1.9f,
         path + ": a person's height in metres, " + std::to_string(height));
  expect(std::fabs(s.minimum[1]) < 0.05f, path + ": standing on the ground");
  expect(height > s.maximum[2] - s.minimum[2], path + ": standing up Y, not lying along Z");
}

void checkMale(const std::string &path, const orblit::Imported &imported) {
  const orblit::ImportSummary &s = imported.summary;
  expect(s.materials >= 3 && s.referencedImages == 3 && s.embeddedImages == 0,
         path + ": its materials and three referenced textures");
  std::string json;
  wellFormed(imported.glb, json);
  expect(contains(json, "\"uri\":\"male-02-1noCulling.JPG\",\"mimeType\":\"image/jpeg\""),
         path + ": an upper-case .JPG is still a JPEG");
}

void checkStatic(const std::string &path, const orblit::Imported &imported) {
  expect(imported.summary.meshes >= 1 && imported.summary.primitives >= 1,
         path + ": geometry");
}

void checkSausage(const std::string &path, const orblit::Imported &imported) {
  const orblit::ImportSummary &s = imported.summary;
  expect(s.skins == 1 && s.joints >= 3 && s.clips.size() == 3, path + ": a skin and three clips");
}

void checkMorph(const std::string &path, const orblit::Imported &imported) {
  const orblit::ImportSummary &s = imported.summary;
  expect(s.morphTargets >= 2 && !s.clips.empty(), path + ": animated morph targets");
  std::string json;
  wellFormed(imported.glb, json);
  expect(contains(json, "\"targetNames\"") && contains(json, "\"path\":\"weights\""),
         path + ": target names and a weights channel");
}

void checkEmbedded(const std::string &path, const orblit::Imported &imported) {
  expect(imported.summary.embeddedImages >= 1, path + ": textures carried inside the GLB");
}

void checkSamples(const std::string &directory) {
  static const Sample kSamples[] = {
      {"threejs/Samba Dancing.fbx", checkMixamo},
      {"male02/male02.obj", checkMale},
      {"ufbx/blender_279_default_7400_binary.fbx", checkStatic},
      {"ufbx/max2009_blob_6100_ascii.fbx", checkStatic},
      {"ufbx/maya_game_sausage_7500_binary_combined.fbx", checkSausage},
      {"ufbx/blender440_shape_weight_anim_7400_binary.fbx", checkMorph},
      {"ufbx/maya_blend_shape_cube_7700_binary.fbx", checkMorph},
      {"ufbx/blender_293_embedded_textures_7400_binary.fbx", checkEmbedded},
      {"material_sphere/material_sphere.fbx", checkStatic},
  };
  if (directory.empty()) {
    std::printf("import samples: none given (argument or ORBLIT_IMPORT_SAMPLES); "
                "real files skipped\n");
    return;
  }
  for (const Sample &sample : kSamples) {
    const std::string path = directory + "/" + sample.path;
    const orblit::SharedBytes bytes = readDisk(path);
    if (!bytes) {
      std::printf("import samples: skipped %s (not found)\n", sample.path);
      continue;
    }
    const orblit::ImportReader read = [](const std::string &p) { return readDisk(p); };
    const orblit::Imported imported =
        orblit::importToGlb(bytes->data(), bytes->size(), path, read);
    std::string json;
    expect(imported.note.empty() && wellFormed(imported.glb, json),
           std::string(sample.path) + " imports: " + imported.note);
    if (imported.glb.empty()) continue;
    sample.check(sample.path, imported);

    const orblit::Imported again =
        orblit::importToGlb(bytes->data(), bytes->size(), path, read);
    expect(again.glb == imported.glb, std::string(sample.path) + " converts to the same bytes twice");

    // Cut short at every tenth, then scribbled on in a few places.
    int notes = 0, total = 0;
    for (int tenth = 1; tenth < 10; tenth++) {
      std::vector<uint8_t> cut(bytes->begin(), bytes->begin() + bytes->size() * tenth / 10);
      notes += survives(cut, path, read, std::string(sample.path) + " cut short");
      total++;
    }
    std::mt19937 random(uint32_t(bytes->size()));
    for (int round = 0; round < 12; round++) {
      std::vector<uint8_t> damaged = *bytes;
      for (int e = 0; e < 16; e++) damaged[random() % damaged.size()] = uint8_t(random());
      notes += survives(damaged, path, read, std::string(sample.path) + " scribbled on");
      total++;
    }
    std::printf("import samples: %s, %zu bytes of GLB; %d of %d damaged copies "
                "refused with a note, the rest still well formed\n",
                sample.path, imported.glb.size(), notes, total);
  }
}

}  // namespace

int main(int argc, char **argv) {
  checkNames();
  checkObj();
  checkObjCompanions();
  checkRefused();
  checkDamagedObj();
  const char *fromEnvironment = std::getenv("ORBLIT_IMPORT_SAMPLES");
  checkSamples(argc > 1 ? argv[1] : fromEnvironment ? fromEnvironment : "");
  if (failures > 0) {
    std::fprintf(stderr, "%d import check(s) failed\n", failures);
    return 1;
  }
  std::printf("the FBX and OBJ import holds\n");
  return 0;
}
