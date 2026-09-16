// Converts an FBX or OBJ to the GLB the renderer would make of it at load.
//
//   orblit_import <in.fbx|in.obj> <out.glb>
//
// The same function the renderer calls, orblit::importToGlb, so a model
// converted here ahead of time and one converted when a scene names it are
// the same bytes. Files the model names beside itself — an OBJ's .mtl — are
// read from disk beside the input. Textures are not read: an embedded one
// goes into the GLB, and a referenced one keeps a URI relative to the input,
// so the .glb belongs in the same directory as the original to find them.
//
// What was written is printed, counted, with the time it took and the scene's
// size in metres — a character that comes out 180 metres tall or lying on its
// back is visible here before it is in a renderer — and then everything the
// file had that the GLB does not.
//
// Built by build.sh beside the headless host, out of the importer and ufbx
// alone: no Filament, no renderer.

#include "OrblitImport.h"

#include <chrono>
#include <cstdio>
#include <fstream>
#include <iterator>
#include <memory>
#include <string>
#include <vector>

namespace {

orblit::SharedBytes readFile(const std::string &path) {
  std::ifstream file(path, std::ios::binary);
  if (!file) return nullptr;
  auto bytes = std::make_shared<std::vector<uint8_t>>(
      std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>());
  if (file.bad()) return nullptr;
  return bytes;
}

}  // namespace

int main(int argc, char **argv) {
  if (argc != 3) {
    std::fprintf(stderr, "usage: orblit_import <in.fbx|in.obj> <out.glb>\n");
    return 2;
  }
  const std::string in = argv[1];
  const std::string out = argv[2];
  if (!orblit::importsAsGlb(in)) {
    std::fprintf(stderr, "orblit_import: %s is not an .fbx or .obj\n", in.c_str());
    return 2;
  }

  const orblit::SharedBytes source = readFile(in);
  if (!source) {
    std::fprintf(stderr, "orblit_import: cannot read %s\n", in.c_str());
    return 1;
  }

  const auto from = std::chrono::steady_clock::now();
  const orblit::Imported imported = orblit::importToGlb(
      source->data(), source->size(), in,
      [](const std::string &path) { return readFile(path); });
  const double milliseconds =
      std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - from)
          .count();

  if (imported.glb.empty()) {
    std::fprintf(stderr, "orblit_import: %s\n", imported.note.c_str());
    return 1;
  }

  std::ofstream file(out, std::ios::binary);
  file.write(reinterpret_cast<const char *>(imported.glb.data()),
             std::streamsize(imported.glb.size()));
  file.close();
  if (!file.good()) {
    std::fprintf(stderr, "orblit_import: cannot write %s\n", out.c_str());
    return 1;
  }

  const orblit::ImportSummary &s = imported.summary;
  std::printf("%s: %.2f MB in %.0f ms\n", out.c_str(),
              double(imported.glb.size()) / (1024 * 1024), milliseconds);
  std::printf("  %u nodes, %u meshes, %u primitives, %u materials\n", s.nodes,
              s.meshes, s.primitives, s.materials);
  std::printf("  %u skins, %u joints, %u morph targets\n", s.skins, s.joints,
              s.morphTargets);
  std::printf("  %u textures embedded, %u referenced\n", s.embeddedImages,
              s.referencedImages);
  std::printf("  %zu clips\n", s.clips.size());
  for (const orblit::ImportedClip &clip : s.clips) {
    std::printf("    '%s': %.3f s, %u channels\n", clip.name.c_str(),
                double(clip.seconds), clip.channels);
  }
  if (s.hasBounds) {
    std::printf("  rest-pose bounds, metres: x %.3f..%.3f, y %.3f..%.3f, "
                "z %.3f..%.3f (%.3f x %.3f x %.3f)\n",
                double(s.minimum[0]), double(s.maximum[0]), double(s.minimum[1]),
                double(s.maximum[1]), double(s.minimum[2]), double(s.maximum[2]),
                double(s.maximum[0] - s.minimum[0]),
                double(s.maximum[1] - s.minimum[1]),
                double(s.maximum[2] - s.minimum[2]));
  }
  if (!imported.losses.empty()) {
    std::printf("  not carried:\n");
    for (const std::string &loss : imported.losses) {
      std::printf("    %s\n", loss.c_str());
    }
  }
  return 0;
}
