// Cooks one texture into GPU-ready KTX2 files.
//
//   orblit_texture_cook <in.png|in.jpg|in.ktx2> <out-dir/ | out-stem>
//       [--targets astc,bc,etc2,basis]  which files to write (default: all)
//       [--normal]                      a tangent-space normal map, kept as
//                                       three channels: BC7, ETC2 RGB8
//       [--two-channel-normals]         with --normal: BC5 and EAC RG11, X
//                                       and Y alone, blue sampling as 0.
//                                       Only for a material that rebuilds Z;
//                                       lit.mat and gltfio's read .xyz
//       [--single-channel]              one linear channel, taken from red:
//                                       BC4 and EAC R11, green and blue
//                                       sampling as 0. Only for a texture
//                                       every material reads red alone from
//       [--srgb | --linear]             override what the file says, or the
//                                       default of sRGB for a PNG or JPEG
//       [--cutout T]                    alpha-tested at T: keep its coverage
//       [--lossless]                    R8G8B8A8, x.ktx2 only, no mips
//       [--mips | --no-mips]            a full chain (the default unless
//                                       lossless) or the top level alone
//       [--max-size N]                  drop levels larger than N
//       [--wrap]                        the texture tiles: filter across edges
//       [--astc direct|transcoded]      ASTC from its own encoder, or from
//                                       the UASTC blocks (default: direct
//                                       from a PNG or JPEG, transcoded from
//                                       a .ktx2; see AstcRoute)
//       [--uastc L]                     UASTC effort, 0-4 (default 2)
//       [--zstd L]                      zstd level, 1-22 (default 19)
//       [--threads N]                   default: one per hardware thread
//       [--quiet]
//   orblit_texture_cook --version
//   orblit_texture_cook --revisions [flags]
//       each family's output revision for these flags, as "basis 1 astc 1
//       bc 2 etc2 2": what a resumable cook compares with the revision in a
//       file's KTXwriter to decide whether that file needs cooking again
// An output ending in / or naming a directory gets the input's name without
// its extension: `Textures/Wall.ktx2 cooked/` writes cooked/Wall.ktx2,
// cooked/Wall.astc.ktx2 and the rest. Anything else is the stem itself.
//
// What can be told from the input is: whether it is sRGB, when it is a KTX2
// (its descriptor says), and whether it has alpha, which decides ETC2 RGBA8
// against RGB8. What cannot — that a texture is a normal map, or a cut-out,
// or pixel art — is a flag, because guessing from a file name is the calling
// script's business (tool/cook_textures.sh reads the glTF that uses them).
//
// Every file is written beside its final name and renamed into place, so an
// interrupted cook never leaves a half file that a resumable script would
// take for a whole one. A lossless cook also removes any x.astc.ktx2,
// x.bc.ktx2 or x.etc2.ktx2 an earlier cook left: the loader prefers a sibling
// to x.ktx2, and a stale one would win.
//
// What it did is printed, with the time each stage took, so a claim about
// cook time is a number.
//
// Built by build.sh beside it. The same library Phase 6's cook step will
// call.

#include "OrblitTextureCook.h"

#include <cerrno>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iterator>
#include <string>
#include <vector>

#include <sys/stat.h>
#include <unistd.h>

namespace {

using namespace orblit::texturecook;

bool readFile(const std::string &path, std::vector<uint8_t> &bytes) {
  std::ifstream file(path, std::ios::binary);
  if (!file) return false;
  bytes.assign(std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>());
  return !file.bad();
}

bool isDirectory(const std::string &path) {
  struct stat info;
  return stat(path.c_str(), &info) == 0 && S_ISDIR(info.st_mode);
}

std::string baseName(const std::string &path) {
  const size_t slash = path.find_last_of('/');
  return slash == std::string::npos ? path : path.substr(slash + 1);
}

std::string directoryOf(const std::string &path) {
  const size_t slash = path.find_last_of('/');
  if (slash == std::string::npos) return ".";
  return slash == 0 ? "/" : path.substr(0, slash);
}

bool endsWith(const std::string &text, const std::string &end) {
  return text.size() >= end.size() &&
         text.compare(text.size() - end.size(), end.size(), end) == 0;
}

std::string stemFor(const std::string &in, const std::string &out) {
  if (endsWith(out, "/") || isDirectory(out)) {
    std::string name = baseName(in);
    const size_t dot = name.find_last_of('.');
    if (dot != std::string::npos && dot > 0) name = name.substr(0, dot);
    return (endsWith(out, "/") ? out : out + "/") + name;
  }
  if (endsWith(out, ".ktx2")) return out.substr(0, out.size() - 5);
  return out;
}

/// Whether two paths name the same file, when both exist.
bool sameFile(const std::string &a, const std::string &b) {
  struct stat first, second;
  return stat(a.c_str(), &first) == 0 && stat(b.c_str(), &second) == 0 &&
         first.st_dev == second.st_dev && first.st_ino == second.st_ino;
}

bool writeAtomically(const std::string &path, const std::vector<uint8_t> &bytes,
                     std::string &why) {
  const std::string part = path + ".part" + std::to_string(getpid());
  {
    std::ofstream file(part, std::ios::binary | std::ios::trunc);
    if (!file) {
      why = "cannot write " + part + ": " + std::strerror(errno);
      return false;
    }
    file.write(reinterpret_cast<const char *>(bytes.data()), std::streamsize(bytes.size()));
    file.close();
    if (!file.good()) {
      std::remove(part.c_str());
      why = "cannot write " + part;
      return false;
    }
  }
  if (std::rename(part.c_str(), path.c_str()) != 0) {
    why = "cannot rename " + part + " to " + path + ": " + std::strerror(errno);
    std::remove(part.c_str());
    return false;
  }
  return true;
}

const char *contentText(Content content) {
  switch (content) {
    case Content::kColour: return "colour";
    case Content::kNormal: return "normal map";
    case Content::kSingleChannel: return "single channel";
  }
  return "colour";
}

int usage() {
  std::fprintf(stderr,
               "usage: orblit_texture_cook <in.png|in.jpg|in.ktx2> <out-dir/|out-stem>\n"
               "  [--targets astc,bc,etc2,basis] [--normal] [--single-channel]\n"
               "  [--two-channel-normals: BC5/EAC RG11, only for a material that rebuilds Z]\n"
               "  [--srgb|--linear] [--cutout T] [--lossless] [--mips|--no-mips]\n"
               "  [--max-size N] [--wrap] [--astc direct|transcoded] [--uastc L] [--zstd L]\n"
               "  [--threads N] [--quiet]\n"
               "       orblit_texture_cook --version\n"
               "       orblit_texture_cook --revisions [flags]\n");
  return 2;
}

bool number(const char *text, long low, long high, long &value) {
  char *end = nullptr;
  errno = 0;
  value = std::strtol(text, &end, 10);
  return errno == 0 && end != text && *end == '\0' && value >= low && value <= high;
}

/// Reads the flags from argv[first] on; false, having said why, on one it
/// does not know.
bool parseFlags(int argc, char **argv, int first, Settings &settings, bool &quiet) {
  for (int i = first; i < argc; i++) {
    const std::string name = argv[i];
    const bool hasValue = i + 1 < argc;
    long value = 0;
    if (name == "--targets" && hasValue) {
      if (!parseFamilies(argv[++i], settings.families)) {
        std::fprintf(stderr, "orblit_texture_cook: --targets takes astc, bc, etc2 and basis\n");
        return false;
      }
    } else if (name == "--normal") {
      settings.content = Content::kNormal;
    } else if (name == "--two-channel-normals") {
      settings.twoChannelNormals = true;
    } else if (name == "--single-channel") {
      settings.content = Content::kSingleChannel;
    } else if (name == "--srgb") {
      settings.transfer = Transfer::kSrgb;
    } else if (name == "--linear") {
      settings.transfer = Transfer::kLinear;
    } else if (name == "--cutout" && hasValue) {
      char *end = nullptr;
      const double threshold = std::strtod(argv[++i], &end);
      if (end == argv[i] || *end != '\0' || !(threshold > 0.0 && threshold < 1.0)) {
        std::fprintf(stderr, "orblit_texture_cook: --cutout takes a threshold between 0 and 1\n");
        return false;
      }
      settings.cutout = float(threshold);
    } else if (name == "--lossless") {
      settings.lossless = true;
    } else if (name == "--mips") {
      settings.mips = 1;
    } else if (name == "--no-mips") {
      settings.mips = 0;
    } else if (name == "--max-size" && hasValue && number(argv[i + 1], 1, 1 << 20, value)) {
      settings.maxSize = uint32_t(value);
      i++;
    } else if (name == "--wrap") {
      settings.edge = Edge::kWrap;
    } else if (name == "--astc" && hasValue) {
      const std::string route = argv[++i];
      if (route == "direct") {
        settings.astc = AstcRoute::kDirect;
      } else if (route == "transcoded") {
        settings.astc = AstcRoute::kTranscoded;
      } else if (route == "auto") {
        settings.astc = AstcRoute::kAuto;
      } else {
        std::fprintf(stderr, "orblit_texture_cook: --astc takes direct, transcoded or auto\n");
        return false;
      }
    } else if (name == "--uastc" && hasValue && number(argv[i + 1], 0, 4, value)) {
      settings.uastcLevel = int(value);
      i++;
    } else if (name == "--zstd" && hasValue && number(argv[i + 1], 1, 22, value)) {
      settings.zstdLevel = int(value);
      i++;
    } else if (name == "--threads" && hasValue && number(argv[i + 1], 1, 1024, value)) {
      settings.threads = uint32_t(value);
      i++;
    } else if (name == "--quiet") {
      quiet = true;
    } else {
      std::fprintf(stderr, "orblit_texture_cook: %s?\n", name.c_str());
      usage();
      return false;
    }
  }

  return true;
}

}  // namespace

int main(int argc, char **argv) {
  if (argc == 2 && std::strcmp(argv[1], "--version") == 0) {
    // The highest output revision; see revisionOf for a file's own.
    std::printf("orblit_texture_cook %d\n", kCookVersion);
    return 0;
  }
  if (argc >= 2 && std::strcmp(argv[1], "--revisions") == 0) {
    Settings settings;
    bool quiet = false;
    if (!parseFlags(argc, argv, 2, settings, quiet)) return 2;
    std::printf("basis %d astc %d bc %d etc2 %d\n",
                revisionOf(kFamilyBasis, settings.content, settings.twoChannelNormals),
                revisionOf(kFamilyAstc, settings.content, settings.twoChannelNormals),
                revisionOf(kFamilyBc, settings.content, settings.twoChannelNormals),
                revisionOf(kFamilyEtc2, settings.content, settings.twoChannelNormals));
    return 0;
  }
  if (argc < 3) return usage();
  const std::string in = argv[1];
  const std::string out = argv[2];
  Settings settings;
  bool quiet = false;
  if (!parseFlags(argc, argv, 3, settings, quiet)) return 2;

  std::vector<uint8_t> bytes;
  if (!readFile(in, bytes)) {
    std::fprintf(stderr, "orblit_texture_cook: cannot read %s\n", in.c_str());
    return 1;
  }
  const std::string stem = stemFor(in, out);
  if (sameFile(in, stem + ".ktx2")) {
    std::fprintf(stderr,
                 "orblit_texture_cook: %s.ktx2 is the input itself; cook into another "
                 "directory\n",
                 stem.c_str());
    return 1;
  }
  if (!isDirectory(directoryOf(stem))) {
    std::fprintf(stderr, "orblit_texture_cook: no directory %s to write into\n",
                 directoryOf(stem).c_str());
    return 1;
  }

  const Cooked cooked = cook(bytes.data(), bytes.size(), settings);
  if (cooked.files.empty()) {
    std::fprintf(stderr, "orblit_texture_cook: %s: %s\n", in.c_str(), cooked.note.c_str());
    return 1;
  }

  double megabytes = 0.0;
  for (const File &file : cooked.files) {
    std::string why;
    if (!writeAtomically(stem + file.suffix, file.bytes, why)) {
      std::fprintf(stderr, "orblit_texture_cook: %s\n", why.c_str());
      return 1;
    }
    megabytes += double(file.bytes.size()) / (1024.0 * 1024.0);
  }
  std::vector<std::string> removed;
  if (cooked.report.lossless) {
    for (Family family : {kFamilyAstc, kFamilyBc, kFamilyEtc2}) {
      const std::string stale = stem + suffixOf(family);
      if (std::remove(stale.c_str()) == 0) removed.push_back(stale);
    }
  }
  if (quiet) return 0;

  const Report &r = cooked.report;
  std::printf("%s -> %s\n", in.c_str(), stem.c_str());
  std::printf("  %s %ux%u, %s, %s%s, %s alpha; cooked %ux%u, %u level%s, %u thread%s\n",
              r.source.container.c_str(), r.source.width, r.source.height,
              contentText(r.content), r.srgb ? "sRGB" : "linear",
              settings.transfer == Transfer::kInfer && r.source.transferKnown
                  ? " (as the file says)"
                  : "",
              r.source.hasAlpha ? "with" : "no", r.width, r.height, r.levels,
              r.levels == 1 ? "" : "s", r.threads, r.threads == 1 ? "" : "s");
  if (r.cutout > 0.0f) {
    std::printf("  cut-out at %.3f: %.4f of level 0 passes\n", double(r.cutout),
                r.targetCoverage);
    std::printf("    level:        ");
    for (size_t i = 0; i < r.coverage.size(); i++) std::printf(" %6zu", i);
    std::printf("\n    kept:         ");
    for (double c : r.coverage) std::printf(" %6.4f", c);
    std::printf("\n    plain filter: ");
    for (double c : r.naiveCoverage) std::printf(" %6.4f", c);
    std::printf("\n");
  }
  for (const File &file : cooked.files) {
    std::printf("  %-22s %-10s vkFormat %3u  %8.3f MB%s\n", baseName(stem + file.suffix).c_str(),
                file.format.c_str(), file.vkFormat,
                double(file.bytes.size()) / (1024.0 * 1024.0),
                file.family == kFamilyAstc ? (r.directAstc ? "  (encoded directly)" : "  (transcoded)")
                                           : "");
  }
  for (const std::string &stale : removed) {
    std::printf("  removed %s: a lossless texture has no siblings\n", stale.c_str());
  }
  std::printf("  %.3f MB in all; decode %.2f s, mips %.2f s, UASTC %.2f s, families %.2f s, "
              "zstd %.2f s\n",
              megabytes, r.decodeSeconds, r.mipSeconds, r.uastcSeconds, r.familySeconds,
              r.compressSeconds);
  for (const std::string &warning : r.warnings) {
    std::printf("  note: %s\n", warning.c_str());
  }
  return 0;
}
