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

/// An ASCII FBX with a triangle, a camera, a spot light and a sun, in the
/// file's own units of centimetres. `@PROJECTION@` is the camera's projection
/// type, 0 for perspective, and `@SUN@` the sun's light type, 1 for
/// directional.
const char *kRig = R"fbx(; FBX 7.4.0 project file
FBXHeaderExtension:  {
	FBXHeaderVersion: 1003
	FBXVersion: 7400
}
GlobalSettings:  {
	Version: 1000
	Properties70:  {
		P: "UpAxis", "int", "Integer", "",1
		P: "UpAxisSign", "int", "Integer", "",1
		P: "FrontAxis", "int", "Integer", "",2
		P: "FrontAxisSign", "int", "Integer", "",1
		P: "CoordAxis", "int", "Integer", "",0
		P: "CoordAxisSign", "int", "Integer", "",1
		P: "UnitScaleFactor", "double", "Number", "",1
	}
}
Definitions:  {
	Version: 100
	Count: 0
}
Objects:  {
	Geometry: 3001, "Geometry::Tri", "Mesh" {
		Vertices: *9 {
			a: 0,0,0,1,0,0,0,1,0
		}
		PolygonVertexIndex: *3 {
			a: 0,1,-3
		}
		GeometryVersion: 124
	}
	NodeAttribute: 1001, "NodeAttribute::Cam", "Camera" {
		TypeFlags: "Camera"
		Properties70:  {
			P: "CameraProjectionType", "enum", "", "",@PROJECTION@
			P: "FieldOfView", "FieldOfView", "", "A",60
			P: "NearPlane", "double", "Number", "",0.5
			P: "FarPlane", "double", "Number", "",200
		}
	}
	NodeAttribute: 1002, "NodeAttribute::Lamp", "Light" {
		TypeFlags: "Light"
		Properties70:  {
			P: "LightType", "enum", "", "",2
			P: "Color", "Color", "", "A",1,0.5,0.25
			P: "Intensity", "Number", "", "A",20000
			P: "InnerAngle", "Number", "", "A",30
			P: "OuterAngle", "Number", "", "A",60
		}
	}
	NodeAttribute: 1003, "NodeAttribute::Sun", "Light" {
		TypeFlags: "Light"
		Properties70:  {
			P: "LightType", "enum", "", "",@SUN@
			P: "Intensity", "Number", "", "A",300
		}
	}
	Model: 2000, "Model::Tri", "Mesh" {
		Version: 232
		Properties70:  {
		}
	}
	Model: 2001, "Model::Cam", "Camera" {
		Version: 232
		Properties70:  {
			P: "Lcl Translation", "Lcl Translation", "", "A",0,2,5
		}
	}
	Model: 2002, "Model::Lamp", "Light" {
		Version: 232
		Properties70:  {
		}
	}
	Model: 2003, "Model::Sun", "Light" {
		Version: 232
		Properties70:  {
			P: "Lcl Rotation", "Lcl Rotation", "", "A",0,0,90
		}
	}
}
Connections:  {
	C: "OO",2000,0
	C: "OO",2001,0
	C: "OO",2002,0
	C: "OO",2003,0
	C: "OO",3001,2000
	C: "OO",1001,2001
	C: "OO",1002,2002
	C: "OO",1003,2003
}
)fbx";

std::string rig(int projection, int sun) {
  std::string text = kRig;
  auto put = [&](const char *marker, int value) {
    text.replace(text.find(marker), std::strlen(marker), std::to_string(value));
  };
  put("@PROJECTION@", projection);
  put("@SUN@", sun);
  return text;
}

/// The numbers of the first array after `key`, searched from `from`.
std::vector<double> numbersAfter(const std::string &json, const std::string &key,
                                 size_t from) {
  std::vector<double> numbers;
  const size_t at = json.find("\"" + key + "\":[", from);
  if (at == std::string::npos) return numbers;
  const char *cursor = json.c_str() + at + key.size() + 4;
  char *end = nullptr;
  for (double value = std::strtod(cursor, &end); end != cursor;
       value = std::strtod(cursor, &end)) {
    numbers.push_back(value);
    cursor = *end == ',' ? end + 1 : end;
  }
  return numbers;
}

/// Where the node called `name` points, which is down -Z for a glTF camera or
/// light, turned by the node's rotation.
std::vector<double> pointingOf(const std::string &json, const std::string &name) {
  const size_t node = json.find("\"name\":\"" + name + "\"");
  const std::vector<double> q = numbersAfter(json, "rotation", node);
  if (node == std::string::npos || q.size() != 4) return {};
  return {-2 * (q[0] * q[2] + q[3] * q[1]), -2 * (q[1] * q[2] - q[3] * q[0]),
          -(1 - 2 * (q[0] * q[0] + q[1] * q[1]))};
}

bool near(const std::vector<double> &got, const std::vector<double> &want) {
  if (got.size() != want.size()) return false;
  for (size_t i = 0; i < got.size(); i++) {
    if (std::fabs(got[i] - want[i]) > 1e-5) return false;
  }
  return true;
}

void checkCamerasAndLights() {
  Files files;
  const orblit::Imported imported = importText(rig(0, 1), "rig.fbx", files);
  std::string json;
  expect(wellFormed(imported.glb, json), "an FBX with a camera and lights imports");
  expect(contains(json, "\"cameras\":[{\"type\":\"perspective\""),
         "its camera is a glTF perspective camera");
  expect(contains(json, "\"extensionsUsed\":[\"KHR_lights_punctual\"]") &&
             contains(json, "\"type\":\"spot\"") &&
             contains(json, "\"type\":\"directional\""),
         "its spot light and sun are punctual lights");
  expect(!anyLoss(imported, "camera") && !anyLoss(imported, "light"),
         "and nothing is said to be lost");

  // An FBX camera looks down +X and a light down -Y, with no rotation. The
  // sun is turned 90 degrees about Z, which takes -Y round to +X.
  expect(near(pointingOf(json, "Cam"), {1, 0, 0}), "the camera looks the way it did");
  expect(near(pointingOf(json, "Lamp"), {0, -1, 0}), "the spot light points the way it did");
  expect(near(pointingOf(json, "Sun"), {1, 0, 0}), "the sun points the way it did");

  // The file is in centimetres, and the glTF in metres.
  const std::vector<double> at = numbersAfter(json, "translation", json.find("\"name\":\"Cam\""));
  expect(at.size() == 3 && std::fabs(at[1] - 0.02) < 1e-6 && std::fabs(at[2] - 0.05) < 1e-6,
         "the camera is where it was, in metres");
  expect(contains(json, "\"yfov\":1.04719758") && contains(json, "\"zfar\":2}"),
         "its angle is the vertical one in radians and its far plane is in metres");

  // 200 watts over a sphere, and 3 watts a square metre, at 683 lumens a watt.
  expect(contains(json, "\"intensity\":10870.28") && contains(json, "\"intensity\":2049"),
         "strengths are in candela for a spot light and lux for a sun");
  expect(contains(json, "\"innerConeAngle\":0.26179") &&
             contains(json, "\"outerConeAngle\":0.52359"),
         "a cone is stated as half its angle");

  // The two kinds with no glTF form are left out and named.
  const orblit::Imported partial = importText(rig(1, 4), "rig.fbx", files);
  expect(anyLoss(partial, "camera(s) not carried") && anyLoss(partial, "light(s) not carried"),
         "an orthographic camera and a volume light are said to be left out");
  const std::string rest(partial.glb.begin(), partial.glb.end());
  expect(!partial.glb.empty() && !contains(rest, "\"cameras\""),
         "and the rest of the file arrives");
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
  checkCamerasAndLights();
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
