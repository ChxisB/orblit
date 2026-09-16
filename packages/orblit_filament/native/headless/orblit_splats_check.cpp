// The splat sorter's and loader's own checks, in C++ against OrblitSplats.h.
//
// The C ABI's test sees splats only as pictures, and a picture that is right
// from one camera says little about an order that is wrong by one splat, a
// cull that keeps too little at the edge of the screen, or a limit that keeps
// the wrong half of a capture. These are checked here, directly, against
// numbers worked out independently of the code under test: the frustum in
// doubles, the ranking from the covariance's own formula.
//
// Built and run by build.sh beside the C ABI's test.

#include "OrblitSplats.h"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <random>
#include <string>
#include <thread>
#include <vector>

namespace {

int failures = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    std::fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

/// `count` splats scattered through a box twenty metres across, each its own
/// size, shape, turn and opacity, as the compact records a `.splat` holds.
std::vector<uint8_t> scatteredRecords(uint32_t count, uint32_t seed) {
  std::mt19937 random(seed);
  std::uniform_real_distribution<float> place(-10.0f, 10.0f);
  std::uniform_real_distribution<float> size(0.01f, 0.5f);
  std::uniform_int_distribution<int> byte(0, 255);
  std::vector<uint8_t> records(size_t(count) * orblit::kSplatRecordBytes);
  for (uint32_t i = 0; i < count; i++) {
    uint8_t *r = &records[size_t(i) * orblit::kSplatRecordBytes];
    const float p[3] = {place(random), place(random), place(random)};
    const float s[3] = {size(random), size(random), size(random)};
    std::memcpy(r, p, sizeof p);
    std::memcpy(r + 12, s, sizeof s);
    for (int b = 24; b < 32; b++) r[b] = uint8_t(byte(random));
  }
  return records;
}

orblit::SplatCloud cloudOf(const std::vector<uint8_t> &records) {
  orblit::SplatCloud cloud;
  std::string error;
  const bool read =
      orblit::readSplatRecords(records.data(), records.size(), cloud, error);
  expect(read, "scattered records read back: " + error);
  return cloud;
}

/// A perspective camera at `eye` looking at the origin, as the direction and
/// the two column-major matrices a sort culls with, worked out here rather
/// than by anything the renderer has.
struct Camera {
  double eye[3];
  double forward[3];
  double side[3];
  double up[3];
  double focal;  // projection[1][1]
  double aspect;
  orblit::SplatSortRequest request;
};

Camera cameraAt(double x, double y, double z, double fovDegrees,
                double aspect) {
  Camera camera{};
  camera.eye[0] = x;
  camera.eye[1] = y;
  camera.eye[2] = z;
  const double length = std::sqrt(x * x + y * y + z * z);
  for (int a = 0; a < 3; a++) camera.forward[a] = -camera.eye[a] / length;
  const double *f = camera.forward;
  // side = forward × world up, up = side × forward.
  double side[3] = {-f[2], 0.0, f[0]};
  const double sideLength = std::sqrt(side[0] * side[0] + side[2] * side[2]);
  for (double &s : side) s /= sideLength;
  std::memcpy(camera.side, side, sizeof side);
  camera.up[0] = side[1] * f[2] - side[2] * f[1];
  camera.up[1] = side[2] * f[0] - side[0] * f[2];
  camera.up[2] = side[0] * f[1] - side[1] * f[0];
  camera.focal = 1.0 / std::tan(fovDegrees * M_PI / 360.0);
  camera.aspect = aspect;

  const double *s = camera.side;
  const double *u = camera.up;
  const double *e = camera.eye;
  const double dotS = s[0] * e[0] + s[1] * e[1] + s[2] * e[2];
  const double dotU = u[0] * e[0] + u[1] * e[1] + u[2] * e[2];
  const double dotF = f[0] * e[0] + f[1] * e[1] + f[2] * e[2];
  const double view[16] = {s[0], u[0], -f[0], 0, s[1], u[1], -f[1], 0,
                           s[2], u[2], -f[2], 0, -dotS, -dotU, dotF, 1};
  const double near = 0.1, far = 1000.0;
  const double projection[16] = {camera.focal / aspect, 0, 0, 0,
                                 0, camera.focal, 0, 0,
                                 0, 0, (far + near) / (near - far), -1,
                                 0, 0, 2 * far * near / (near - far), 0};
  double clip[16] = {};
  for (int column = 0; column < 4; column++) {
    for (int row = 0; row < 4; row++) {
      double sum = 0;
      for (int k = 0; k < 4; k++) {
        sum += projection[k * 4 + row] * view[column * 4 + k];
      }
      clip[column * 4 + row] = sum;
    }
  }

  auto &request = camera.request;
  for (int a = 0; a < 3; a++) request.direction[a] = float(f[a]);
  for (int i = 0; i < 16; i++) {
    request.viewFromModel[i] = float(view[i]);
    request.clipFromModel[i] = float(clip[i]);
  }
  return camera;
}

/// Where a point lands for `camera`, in doubles: its distance in front, and
/// its position on screen as a multiple of the half-screen.
struct Seen {
  double ahead;
  double x;
  double y;
};

Seen seenFrom(const Camera &camera, const float *p) {
  const double d[3] = {p[0] - camera.eye[0], p[1] - camera.eye[1],
                       p[2] - camera.eye[2]};
  const double ahead = d[0] * camera.forward[0] + d[1] * camera.forward[1] +
                       d[2] * camera.forward[2];
  const double across = d[0] * camera.side[0] + d[1] * camera.side[1] +
                        d[2] * camera.side[2];
  const double upward =
      d[0] * camera.up[0] + d[1] * camera.up[1] + d[2] * camera.up[2];
  return {ahead, across * camera.focal / camera.aspect / ahead,
          upward * camera.focal / ahead};
}

double depthAlong(const float *positions, uint32_t id, const float d[3]) {
  const float *p = positions + size_t(id) * 3;
  return double(p[0]) * d[0] + double(p[1]) * d[1] + double(p[2]) * d[2];
}

bool isPermutation(const std::vector<uint32_t> &order, uint32_t count) {
  if (order.size() != count) return false;
  std::vector<bool> seen(count, false);
  for (uint32_t id : order) {
    if (id >= count || seen[id]) return false;
    seen[id] = true;
  }
  return true;
}

/// Whether `part` is `whole` with some ids left out and none moved.
bool isSubsequence(const std::vector<uint32_t> &part,
                   const std::vector<uint32_t> &whole) {
  size_t at = 0;
  for (uint32_t id : whole) {
    if (at < part.size() && part[at] == id) at++;
  }
  return at == part.size();
}

void checkFullSort() {
  const auto cloud = cloudOf(scatteredRecords(20000, 1));
  const Camera camera = cameraAt(3, 4, 30, 50, 1.5);
  std::vector<uint32_t> order, scratch;
  orblit::SplatSortRequest request = camera.request;
  request.cull = false;
  orblit::sortSplats(cloud.positions.data(), cloud.count, request, order,
                     scratch);

  expect(isPermutation(order, cloud.count),
         "an unculled sort holds every splat exactly once");
  bool backToFront = true;
  for (size_t i = 1; i < order.size(); i++) {
    if (depthAlong(cloud.positions.data(), order[i - 1], request.direction) <
        depthAlong(cloud.positions.data(), order[i], request.direction) -
            1e-4) {
      backToFront = false;
      break;
    }
  }
  expect(backToFront, "an unculled sort is farthest first");

  // Splats at the same depth keep the order they came in.
  std::vector<float> same(9 * 3, 0.0f);
  for (int i = 0; i < 9; i++) same[size_t(i) * 3 + 1] = float(i % 3);
  orblit::SplatSortRequest along;
  along.direction[0] = 1;
  along.direction[1] = 0;
  along.direction[2] = 0;
  orblit::sortSplats(same.data(), 9, along, order, scratch);
  expect(order == std::vector<uint32_t>{0, 1, 2, 3, 4, 5, 6, 7, 8},
         "a sort is stable: equal depths stay in the order given");

  orblit::sortSplats(nullptr, 0, along, order, scratch);
  expect(order.empty(), "a cloud of nothing sorts to nothing");
}

void checkCulledSort() {
  const auto cloud = cloudOf(scatteredRecords(50000, 2));
  // Inside the box, looking across it: most of the cloud is behind or beside
  // the camera, which is what culling is for.
  const Camera camera = cameraAt(2, 1, 4, 60, 16.0 / 9.0);
  std::vector<uint32_t> full, culled, scratch;

  orblit::SplatSortRequest request = camera.request;
  request.cull = false;
  orblit::sortSplats(cloud.positions.data(), cloud.count, request, full,
                     scratch);
  request.cull = true;
  orblit::sortSplats(cloud.positions.data(), cloud.count, request, culled,
                     scratch);

  expect(!culled.empty() && culled.size() < full.size(),
         "a camera inside a cloud culls some of it and keeps some (kept " +
             std::to_string(culled.size()) + " of " +
             std::to_string(full.size()) + ")");
  expect(isSubsequence(culled, full),
         "culling only takes splats out of the order a full sort gives");

  std::vector<bool> kept(cloud.count, false);
  for (uint32_t id : culled) kept[id] = true;
  uint32_t missing = 0, extra = 0;
  for (uint32_t id = 0; id < cloud.count; id++) {
    const Seen seen = seenFrom(camera, &cloud.positions[size_t(id) * 3]);
    // Clearly on screen, by the shader's own reckoning: in front by more
    // than its near cut and inside its 1.3 guard with room to spare.
    const bool drawn = seen.ahead > 0.25 && std::abs(seen.x) < 1.25 &&
                       std::abs(seen.y) < 1.25;
    // Clearly nowhere near it: behind, or twice the screen away.
    const bool gone = seen.ahead < -0.05 ||
                      (seen.ahead > 0.05 &&
                       (std::abs(seen.x) > 1.6 || std::abs(seen.y) > 1.6));
    if (drawn && !kept[id]) missing++;
    if (gone && kept[id]) extra++;
  }
  expect(missing == 0, "culling keeps every splat the shader would draw (" +
                           std::to_string(missing) + " dropped)");
  expect(extra == 0, "culling drops every splat far off screen or behind (" +
                         std::to_string(extra) + " kept)");
}

void checkCoarseSort() {
  const auto cloud = cloudOf(scatteredRecords(40000, 3));
  const Camera camera = cameraAt(-5, 2, 25, 45, 1.0);
  std::vector<uint32_t> fine, coarse, scratch;
  orblit::SplatSortRequest request = camera.request;
  request.cull = true;
  orblit::sortSplats(cloud.positions.data(), cloud.count, request, fine,
                     scratch);
  request.coarse = true;
  orblit::sortSplats(cloud.positions.data(), cloud.count, request, coarse,
                     scratch);

  std::vector<uint32_t> a = fine, b = coarse;
  std::sort(a.begin(), a.end());
  std::sort(b.begin(), b.end());
  expect(a == b, "a coarse sort keeps the same splats as a fine one");

  double nearest = 1e30, farthest = -1e30;
  for (uint32_t id : coarse) {
    const double depth =
        depthAlong(cloud.positions.data(), id, request.direction);
    nearest = std::min(nearest, depth);
    farthest = std::max(farthest, depth);
  }
  const double step = (farthest - nearest) / 65535.0;
  bool ordered = true;
  for (size_t i = 1; i < coarse.size(); i++) {
    if (depthAlong(cloud.positions.data(), coarse[i - 1], request.direction) <
        depthAlong(cloud.positions.data(), coarse[i], request.direction) -
            step * 1.01) {
      ordered = false;
      break;
    }
  }
  expect(ordered,
         "a coarse sort is farthest first to within one of its steps");
}

/// What a splat is worth to keepMostVisibleSplats, from the covariance's
/// second invariant as written out in full.
double weightOf(const orblit::SplatCloud &cloud, uint32_t id) {
  const float *c = &cloud.covariances[size_t(id) * 6];
  const double s00 = c[0], s01 = c[1], s02 = c[2], s11 = c[3], s12 = c[4],
               s22 = c[5];
  const double minors = (s00 * s11 - s01 * s01) + (s11 * s22 - s12 * s12) +
                        (s00 * s22 - s02 * s02);
  return double(cloud.colours[id] >> 24) / 255.0 *
         std::sqrt(std::max(minors, 0.0));
}

void checkLimit() {
  const auto records = scatteredRecords(10000, 4);
  const auto original = cloudOf(records);
  auto limited = cloudOf(records);

  expect(orblit::keepMostVisibleSplats(limited, 0) == 10000 &&
             limited.count == 10000,
         "a limit of nought keeps everything");
  expect(orblit::keepMostVisibleSplats(limited, 20000) == 10000 &&
             limited.count == 10000,
         "a limit past the cloud's size keeps everything");

  const uint32_t had = orblit::keepMostVisibleSplats(limited, 2500);
  expect(had == 10000 && limited.count == 2500 &&
             limited.positions.size() == 2500 * 3 &&
             limited.covariances.size() == 2500 * 6 &&
             limited.colours.size() == 2500,
         "a limit keeps exactly that many splats");

  // Which originals were kept, found by position, in the order found.
  std::vector<uint32_t> keptIds;
  size_t search = 0;
  for (uint32_t k = 0; k < limited.count; k++) {
    while (search < original.count &&
           std::memcmp(&original.positions[search * 3],
                       &limited.positions[size_t(k) * 3],
                       sizeof(float) * 3) != 0) {
      search++;
    }
    if (search == original.count) break;
    keptIds.push_back(uint32_t(search));
    search++;
  }
  expect(keptIds.size() == limited.count,
         "the splats kept are the originals, in the order they came");

  std::vector<bool> kept(original.count, false);
  for (uint32_t id : keptIds) kept[id] = true;
  double leastKept = 1e30, mostDropped = -1e30;
  for (uint32_t id = 0; id < original.count; id++) {
    const double weight = weightOf(original, id);
    if (kept[id]) {
      leastKept = std::min(leastKept, weight);
    } else {
      mostDropped = std::max(mostDropped, weight);
    }
  }
  expect(leastKept >= mostDropped * (1 - 1e-5),
         "a limit keeps the most opaque and largest, and drops the rest");

  bool boxed = true;
  for (uint32_t k = 0; k < limited.count && boxed; k++) {
    for (int a = 0; a < 3; a++) {
      const float p = limited.positions[size_t(k) * 3 + a];
      if (p < limited.minimum[a] || p > limited.maximum[a]) boxed = false;
    }
  }
  expect(boxed, "the box is measured again round the splats kept");
}

/// Waits for a sorter's answer, as a frame loop would, for up to five
/// seconds.
bool waitFor(orblit::SplatSorter &sorter, std::vector<uint32_t> &order) {
  double milliseconds = 0;
  const auto until =
      std::chrono::steady_clock::now() + std::chrono::seconds(5);
  while (std::chrono::steady_clock::now() < until) {
    if (sorter.take(order, milliseconds)) return true;
    std::this_thread::sleep_for(std::chrono::milliseconds(1));
  }
  return false;
}

void checkSorters() {
  for (const uint32_t count :
       {uint32_t(1000), orblit::kSplatInlineSortLimit + 1000}) {
    const auto cloud = cloudOf(scatteredRecords(count, 5));
    auto positions = std::make_shared<const std::vector<float>>(cloud.positions);
    const Camera camera = cameraAt(1, 3, 20, 55, 1.3);
    orblit::SplatSortRequest request = camera.request;
    request.cull = true;

    std::vector<uint32_t> expected, scratch, order;
    orblit::sortSplats(positions->data(), count, request, expected, scratch);

    auto sorter = orblit::makeSplatSorter(positions, count);
    sorter->request(request);
    const bool small = count <= orblit::kSplatInlineSortLimit;
    if (small) {
      expect(!sorter->busy(),
             "a small cloud's sort is finished as soon as it is asked for");
    }
    expect(waitFor(*sorter, order), "a sorter answers (" +
                                        std::to_string(count) + " splats)");
    expect(order == expected,
           "a sorter's order is sortSplats' order (" + std::to_string(count) +
               " splats)");
    double milliseconds = 0;
    expect(!sorter->take(order, milliseconds),
           "an answer is handed over once");
  }
}

// ---- The file formats ----
//
// A `.spz` is gzip, and the only inflater this links is stb's, which does not
// compress. It does read a stored deflate block, though — the part of the
// format that is "here are the bytes, uncompressed" — so the check builds its
// own gzip file out of those, and the reader under test sees exactly what it
// would see from a real one.

uint32_t crc32Of(const uint8_t *data, size_t length) {
  uint32_t crc = 0xffffffffu;
  for (size_t i = 0; i < length; i++) {
    crc ^= data[i];
    for (int bit = 0; bit < 8; bit++) {
      // The polynomial where the low bit is set, and nothing where it is not.
      crc = (crc >> 1) ^ (0xedb88320u & (~(crc & 1u) + 1u));
    }
  }
  return ~crc;
}

std::vector<uint8_t> gzipped(const std::vector<uint8_t> &raw) {
  std::vector<uint8_t> out = {0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 0, 3};
  size_t at = 0;
  do {
    const size_t block = std::min<size_t>(raw.size() - at, 65535);
    const bool last = at + block >= raw.size();
    out.push_back(last ? 1 : 0);
    out.push_back(uint8_t(block & 0xff));
    out.push_back(uint8_t(block >> 8));
    out.push_back(uint8_t(~block & 0xff));
    out.push_back(uint8_t((~block >> 8) & 0xff));
    out.insert(out.end(), raw.begin() + long(at),
               raw.begin() + long(at + block));
    at += block;
  } while (at < raw.size());
  const uint32_t crc = crc32Of(raw.data(), raw.size());
  const uint32_t size = uint32_t(raw.size());
  for (int i = 0; i < 4; i++) out.push_back(uint8_t((crc >> (i * 8)) & 0xff));
  for (int i = 0; i < 4; i++) out.push_back(uint8_t((size >> (i * 8)) & 0xff));
  return out;
}

void putWord(std::vector<uint8_t> &into, uint32_t value) {
  for (int i = 0; i < 4; i++) {
    into.push_back(uint8_t((value >> (i * 8)) & 0xff));
  }
}

/// Two splats in the layout a `.spz` of `version` holds them, with values
/// chosen so that what comes back out is obvious: the first at (1.5, 2, -0.5)
/// with a scale of exactly one and no turn, the second at the origin.
///
/// At degree one, every coefficient but the middle one is as positive as a
/// byte goes — and the first is one of those that has to change sign when the
/// frame does, while the third is not.
std::vector<uint8_t> spzFile(uint32_t version, uint32_t degree) {
  const uint32_t points = 2;
  const uint32_t coefficients = degree == 0 ? 0 : 3;
  std::vector<uint8_t> raw;
  putWord(raw, 0x5053474e);  // NGSP
  putWord(raw, version);
  putWord(raw, points);
  raw.push_back(uint8_t(degree));
  raw.push_back(12);  // fractional bits
  raw.push_back(0);   // flags
  raw.push_back(0);   // kept back

  // Positions: three bytes a number, little-endian, in 1/4096ths.
  const int32_t places[2][3] = {{6144, 8192, -2048}, {0, 0, 0}};
  for (uint32_t i = 0; i < points; i++) {
    for (int a = 0; a < 3; a++) {
      const uint32_t bits = uint32_t(places[i][a]) & 0xffffffu;
      raw.push_back(uint8_t(bits & 0xff));
      raw.push_back(uint8_t((bits >> 8) & 0xff));
      raw.push_back(uint8_t((bits >> 16) & 0xff));
    }
  }
  // Alphas, then colours, then scales, then rotations: the order the format
  // writes them in.
  raw.push_back(64);
  raw.push_back(255);
  for (uint32_t i = 0; i < points; i++) {
    raw.push_back(200);
    raw.push_back(128);
    raw.push_back(40);
  }
  for (uint32_t i = 0; i < points; i++) {
    // Ten sixteenths under ten is a scale of exactly one.
    raw.push_back(160);
    raw.push_back(160);
    raw.push_back(160);
  }
  for (uint32_t i = 0; i < points; i++) {
    if (version >= 3) {
      // The smallest three, with w left out and the rest nought: no turn.
      putWord(raw, 3u << 30);
    } else {
      raw.push_back(128);
      raw.push_back(128);
      raw.push_back(128);
    }
  }
  for (uint32_t i = 0; i < points; i++) {
    for (uint32_t k = 0; k < coefficients; k++) {
      for (int c = 0; c < 3; c++) raw.push_back(k == 1 ? 128 : 255);
    }
  }
  return gzipped(raw);
}

void checkSpz() {
  for (const uint32_t version : {uint32_t(2), uint32_t(3)}) {
    const std::string said = "version " + std::to_string(version);
    const std::vector<uint8_t> file = spzFile(version, 1);
    orblit::SplatCloud cloud;
    std::string error;
    if (!orblit::readSplatSpz(file.data(), file.size(), 3, cloud, error)) {
      expect(false, "a .spz reads (" + said + "): " + error);
      continue;
    }
    expect(cloud.count == 2, "a .spz holds its two splats (" + said + ")");

    // Right-up-back to right-down-front: y and z the other way about.
    expect(std::abs(cloud.positions[0] - 1.5f) < 1e-5f &&
               std::abs(cloud.positions[1] + 2.0f) < 1e-5f &&
               std::abs(cloud.positions[2] - 0.5f) < 1e-5f,
           "a .spz's fixed-point positions come back turned the right way "
           "round (" + said + ")");

    // A scale of one and no turn is a covariance of one down the diagonal.
    const float *c = cloud.covariances.data();
    expect(std::abs(c[0] - 1.0f) < 0.02f && std::abs(c[3] - 1.0f) < 0.02f &&
               std::abs(c[5] - 1.0f) < 0.02f && std::abs(c[1]) < 0.02f &&
               std::abs(c[2]) < 0.02f && std::abs(c[4]) < 0.02f,
           "a .spz's scales and rotation make the covariance (" + said + ")");

    expect((cloud.colours[0] >> 24) == 64,
           "a .spz's alpha is the opacity itself (" + said + ")");
    expect((cloud.colours[0] & 0xff) > 200 &&
               ((cloud.colours[0] >> 16) & 0xff) < 60,
           "a .spz's colour is its degree-zero coefficient (" + said + ")");

    expect(cloud.harmonicDegree == 1 && cloud.harmonics.size() == 2 * 9,
           "a .spz's first band is read (" + said + ")");
    // The first coefficient changes sign with the frame and the third does
    // not, so one stored byte comes back at each end.
    expect(cloud.harmonics[0] < 10 && cloud.harmonics[6] > 245,
           "a .spz's harmonics turn with the frame (" + said + ")");
  }

  // What it refuses, and whether it says why.
  orblit::SplatCloud cloud;
  std::string error;
  const std::vector<uint8_t> four = spzFile(4, 0);
  expect(!orblit::readSplatSpz(four.data(), four.size(), 3, cloud, error) &&
             error.find("version 4") != std::string::npos,
         "a version-4 .spz is refused, and says so: " + error);
  const std::vector<uint8_t> nonsense = gzipped(std::vector<uint8_t>(64, 7));
  expect(
      !orblit::readSplatSpz(nonsense.data(), nonsense.size(), 3, cloud, error),
      "bytes that are not a .spz are refused");
  const std::vector<uint8_t> tiny = {0x1f, 0x8b, 8};
  expect(!orblit::readSplatSpz(tiny.data(), tiny.size(), 3, cloud, error),
         "a file too short to be gzip at all is refused");
}

void checkCooked() {
  const std::vector<uint8_t> spz = spzFile(3, 1);
  orblit::SplatCloud cloud;
  std::string error;
  expect(orblit::readSplatSpz(spz.data(), spz.size(), 3, cloud, error),
         "the cloud to cook reads: " + error);

  const std::vector<uint8_t> cooked = orblit::writeSplatCooked(cloud);
  orblit::SplatCloud again;
  expect(
      orblit::readSplatCooked(cooked.data(), cooked.size(), 3, again, error),
      "an .osplat reads back: " + error);
  expect(again.count == cloud.count && again.positions == cloud.positions &&
             again.covariances == cloud.covariances &&
             again.colours == cloud.colours &&
             again.harmonics == cloud.harmonics &&
             again.harmonicDegree == cloud.harmonicDegree &&
             std::memcmp(again.harmonicScale, cloud.harmonicScale,
                         sizeof cloud.harmonicScale) == 0 &&
             std::memcmp(again.minimum, cloud.minimum,
                         sizeof cloud.minimum) == 0 &&
             std::memcmp(again.maximum, cloud.maximum,
                         sizeof cloud.maximum) == 0,
         "an .osplat is the cloud it was written from, to the byte");

  orblit::SplatCloud flat;
  expect(
      orblit::readSplatCooked(cooked.data(), cooked.size(), 0, flat, error) &&
          flat.harmonicDegree == 0 && flat.harmonics.empty() &&
          flat.droppedHigherBands && flat.count == cloud.count,
      "an .osplat read at a lower degree drops the bands and says so");

  std::vector<uint8_t> broken = cooked;
  broken[0] = 'X';
  expect(
      !orblit::readSplatCooked(broken.data(), broken.size(), 3, again, error),
      "an .osplat that does not begin OSPL is refused");
  expect(!orblit::readSplatCooked(cooked.data(), cooked.size() / 2, 3, again,
                                  error),
         "half an .osplat is refused rather than read past");
}

}  // namespace

int main() {
  checkFullSort();
  checkCulledSort();
  checkCoarseSort();
  checkLimit();
  checkSorters();
  checkSpz();
  checkCooked();
  if (failures > 0) {
    std::fprintf(stderr, "%d splat check(s) failed\n", failures);
    return 1;
  }
  std::printf("the splat sort, cull and limit hold\n");
  return 0;
}
