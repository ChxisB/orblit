// Cooks a splat capture into the `.osplat` a launch can read without parsing.
//
//   orblit_splat_cook <in> <out.osplat> [--harmonics N] [--limit N]
//
// Reads whatever loadSplatFile reads — `.ply`, `.spz`, `.splat` or an
// `.osplat` cooked at a higher degree — and writes the cloud as the renderer
// holds it: centres, covariances, colours and quantised harmonics, laid out
// so that opening it is a read and four copies.
//
// What that is worth is printed rather than asserted: both files are read
// afterwards and timed, because "this is faster" is a claim and a pair of
// numbers is not. A capture's `.ply` spends its time on an exponential, a
// quaternion and a covariance a splat, and on two passes over its harmonics;
// none of that happens again once it is cooked.
//
// --harmonics is how many bands to keep, 0 to 3. --limit is the most splats
// to keep, ranked by opacity and size — the same ranking the renderer applies
// for a device's budget — so a cooked file can be the small one a phone
// loads.
//
// This is the by-hand version of what Phase 6's cook step will do for a whole
// project. It lives beside the headless host because it is built the same
// way, out of the same objects.

#include "OrblitSplats.h"

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <string>
#include <vector>

namespace {

double secondsSince(std::chrono::steady_clock::time_point from) {
  return std::chrono::duration<double>(std::chrono::steady_clock::now() - from)
      .count();
}

bool readCloud(const std::string &path, uint32_t degree,
               orblit::SplatCloud &into, double &seconds) {
  const auto from = std::chrono::steady_clock::now();
  std::string error;
  if (!orblit::loadSplatFile(path, degree, into, error)) {
    std::fprintf(stderr, "orblit_splat_cook: %s: %s\n", path.c_str(),
                 error.c_str());
    return false;
  }
  seconds = secondsSince(from);
  return true;
}

}  // namespace

int main(int argc, char **argv) {
  if (argc < 3) {
    std::fprintf(stderr,
                 "usage: orblit_splat_cook <in> <out.osplat> "
                 "[--harmonics N] [--limit N]\n");
    return 2;
  }
  const std::string in = argv[1];
  const std::string out = argv[2];
  uint32_t degree = orblit::kSplatMaxHarmonicDegree;
  uint32_t limit = 0;
  for (int i = 3; i + 1 < argc; i += 2) {
    const std::string name = argv[i];
    const long value = std::strtol(argv[i + 1], nullptr, 10);
    if (name == "--harmonics" && value >= 0 &&
        value <= long(orblit::kSplatMaxHarmonicDegree)) {
      degree = uint32_t(value);
    } else if (name == "--limit" && value > 0) {
      limit = uint32_t(value);
    } else {
      std::fprintf(stderr, "orblit_splat_cook: %s %s?\n", name.c_str(),
                   argv[i + 1]);
      return 2;
    }
  }

  orblit::SplatCloud cloud;
  double readSeconds = 0;
  if (!readCloud(in, degree, cloud, readSeconds)) return 1;
  const uint32_t had = orblit::keepMostVisibleSplats(cloud, limit);

  const std::vector<uint8_t> cooked = orblit::writeSplatCooked(cloud);
  std::ofstream file(out, std::ios::binary);
  file.write(reinterpret_cast<const char *>(cooked.data()),
             std::streamsize(cooked.size()));
  file.close();
  if (!file.good()) {
    std::fprintf(stderr, "orblit_splat_cook: cannot write %s\n", out.c_str());
    return 1;
  }

  orblit::SplatCloud again;
  double cookedSeconds = 0;
  if (!readCloud(out, degree, again, cookedSeconds)) return 1;

  if (had > cloud.count) {
    std::printf("kept the %u largest and most opaque of %u splats\n",
                cloud.count, had);
  }
  std::printf(
      "%s: %u splats, degree %u, read in %.0f ms\n"
      "%s: %.1f MB, read in %.0f ms — %.1f times faster\n",
      in.c_str(), cloud.count, cloud.harmonicDegree, readSeconds * 1000,
      out.c_str(), double(cooked.size()) / (1024 * 1024), cookedSeconds * 1000,
      cookedSeconds > 0 ? readSeconds / cookedSeconds : 0.0);
  return 0;
}
