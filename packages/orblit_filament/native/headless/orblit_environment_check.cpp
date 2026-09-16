// Environments from pictures, checked in pixels against cmgen's bake of the
// same picture.
//
// Three spheres — a mirror, rough metal, and matte white — lit twice: once
// by the KTX cubemaps tool/bake_environment.sh makes with cmgen, and once by
// the renderer filtering the .hdr itself. How far apart the frames are is
// printed as numbers (mean, 99th percentile, the share of pixels over a few
// levels), per sphere, because what is left different is different for a
// reason and the reason shows in which sphere it is on. Then: the CPU route
// against a small bake, an EXR against the HDR it was written from, how long
// each frame took while a picture was being filtered, that naming a picture
// again filters nothing, and that damaged pictures are refused with notes.
//
// Dithering and TAA are off, so two frames of the same light are the same
// bytes and any difference is the environment's.
//
// Inputs, from the environment:
//   ORBLIT_BAKE_SCRIPT   tool/bake_environment.sh (build.sh passes it)
//   ORBLIT_CMGEN         cmgen, for the script (build.sh passes the SDK's)
//   ORBLIT_CHECK_WORK    a directory to bake into (build.sh passes one)
//   ORBLIT_ENVIRONMENTS  optional: a directory holding pillars_2k.hdr and
//                        asakusa.exr — Poly Haven's CC0 picture and tinyexr's
//                        sample, both in a Filament checkout's third_party —
//                        checked as well as the check's own synthetic sky
//   ORBLIT_CHECK_DUMP    optional: write every frame measured as a .ppm

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <functional>
#include <string>
#include <thread>
#include <vector>

#include "OrblitEnvironmentBake.h"
#include "OrblitHdrImage.h"
#include "environment_pictures.h"
#include "orblit_renderer.h"

namespace orblit {
uint64_t environmentPicturesFiltered();
}

namespace {

constexpr uint32_t kWidth = 480;
constexpr uint32_t kHeight = 160;

int failures = 0;
int skipped = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    std::fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

std::string env(const char *name) {
  const char *value = std::getenv(name);
  return value != nullptr ? value : "";
}

using Clock = std::chrono::steady_clock;

double millisecondsSince(Clock::time_point start) {
  return std::chrono::duration<double, std::milli>(Clock::now() - start).count();
}

// ---- The scene ----

/// A UV sphere of radius one as a GLB: positions, normals and indices.
std::vector<uint8_t> sphereGlb() {
  constexpr int segments = 96;
  constexpr int rings = 48;
  std::vector<float> positions;
  std::vector<float> normals;
  std::vector<uint16_t> indices;
  const double pi = 3.14159265358979323846;
  for (int r = 0; r <= rings; r++) {
    const double theta = pi * r / rings;
    for (int s = 0; s <= segments; s++) {
      const double phi = 2 * pi * s / segments;
      const float n[3] = {float(std::sin(theta) * std::cos(phi)),
                          float(std::cos(theta)),
                          float(std::sin(theta) * std::sin(phi))};
      positions.insert(positions.end(), n, n + 3);
      normals.insert(normals.end(), n, n + 3);
    }
  }
  for (int r = 0; r < rings; r++) {
    for (int s = 0; s < segments; s++) {
      const uint16_t a = uint16_t(r * (segments + 1) + s);
      const uint16_t b = uint16_t(a + segments + 1);
      const uint16_t tri[6] = {a, uint16_t(a + 1), b, b, uint16_t(a + 1), uint16_t(b + 1)};
      indices.insert(indices.end(), tri, tri + 6);
    }
  }
  const size_t vertexBytes = positions.size() * sizeof(float);
  const size_t indexBytes = indices.size() * sizeof(uint16_t);
  std::vector<uint8_t> bin(vertexBytes * 2 + indexBytes);
  std::memcpy(bin.data(), positions.data(), vertexBytes);
  std::memcpy(bin.data() + vertexBytes, normals.data(), vertexBytes);
  std::memcpy(bin.data() + vertexBytes * 2, indices.data(), indexBytes);
  while (bin.size() % 4 != 0) bin.push_back(0);

  char json[2048];
  std::snprintf(
      json, sizeof json,
      "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],"
      "\"nodes\":[{\"mesh\":0}],\"meshes\":[{\"primitives\":[{\"attributes\":"
      "{\"POSITION\":0,\"NORMAL\":1},\"indices\":2}]}],"
      "\"buffers\":[{\"byteLength\":%zu}],\"bufferViews\":["
      "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":%zu,\"target\":34962},"
      "{\"buffer\":0,\"byteOffset\":%zu,\"byteLength\":%zu,\"target\":34962},"
      "{\"buffer\":0,\"byteOffset\":%zu,\"byteLength\":%zu,\"target\":34963}],"
      "\"accessors\":["
      "{\"bufferView\":0,\"componentType\":5126,\"count\":%zu,\"type\":\"VEC3\","
      "\"min\":[-1,-1,-1],\"max\":[1,1,1]},"
      "{\"bufferView\":1,\"componentType\":5126,\"count\":%zu,\"type\":\"VEC3\"},"
      "{\"bufferView\":2,\"componentType\":5123,\"count\":%zu,\"type\":\"SCALAR\"}]}",
      bin.size(), vertexBytes, vertexBytes, vertexBytes, vertexBytes * 2, indexBytes,
      positions.size() / 3, normals.size() / 3, indices.size());
  std::string text = json;
  while (text.size() % 4 != 0) text.push_back(' ');

  std::vector<uint8_t> glb;
  const auto u32 = [&](uint32_t v) {
    for (int i = 0; i < 4; i++) glb.push_back(uint8_t(v >> (8 * i)));
  };
  u32(0x46546C67);
  u32(2);
  u32(uint32_t(12 + 8 + text.size() + 8 + bin.size()));
  u32(uint32_t(text.size()));
  u32(0x4E4F534A);
  glb.insert(glb.end(), text.begin(), text.end());
  u32(uint32_t(bin.size()));
  u32(0x004E4942);
  glb.insert(glb.end(), bin.begin(), bin.end());
  return glb;
}

const char *kSphere = "orblit:resource/environment-check/sphere.glb";

orblit_renderer *start() {
  orblit_surface_desc headless = {ORBLIT_SURFACE_HEADLESS, nullptr};
  orblit_renderer *renderer =
      orblit_renderer_create(ORBLIT_BACKEND_DEFAULT, &headless, kWidth, kHeight);
  if (renderer == nullptr) return nullptr;
  // Post-processing on for tone mapping and nothing else: no dithering and no
  // TAA, so a frame of the same light is the same bytes.
  float post[49];
  std::memset(post, 0, sizeof post);
  post[0] = 1.0f;
  orblit_renderer_set_post_process(renderer, post, 49);
  orblit_renderer_apply_lights(renderer, 0, nullptr, nullptr, nullptr, nullptr, 0);
  const float black[3] = {0, 0, 0};
  orblit_renderer_set_sky_colour(renderer, black, 0.0f, 0);
  // Sunny sixteen: the exposure a daylight environment of thirty thousand lux
  // is meant to be seen at.
  orblit_renderer_set_exposure(renderer, 16.0f, 1.0f / 125.0f, 100.0f);

  const uint32_t materialFloats = orblit_renderer_stride(ORBLIT_STRIDE_MATERIAL);
  const uint32_t mapCount = orblit_renderer_stride(ORBLIT_STRIDE_MATERIAL_MAPS);
  // A mirror, rough metal, matte white.
  const float surfaces[3][3] = {{0.95f, 1.0f, 0.02f}, {0.95f, 1.0f, 0.45f}, {0.8f, 0.0f, 1.0f}};
  std::vector<float> params(size_t(materialFloats) * 3, 0.0f);
  std::vector<int32_t> maps(size_t(mapCount) * 3, -1);
  const int64_t materialKeys[3] = {101, 102, 103};
  const int32_t materialFlags[3] = {1 << 9, 1 << 9, 1 << 9};
  const int32_t videos[3] = {-1, -1, -1};
  for (int i = 0; i < 3; i++) {
    float *p = &params[size_t(i) * materialFloats];
    p[0] = p[1] = p[2] = surfaces[i][0];
    p[3] = 1.0f;
    p[4] = surfaces[i][1];
    p[5] = surfaces[i][2];
    p[6] = 0.5f;   // reflectance
    p[11] = 1.0f;  // ambient occlusion
    p[12] = 1.0f;  // normal scale
    p[13] = p[14] = 1.0f;
    p[17] = 0.5f;
    p[22] = p[23] = 1.0f;
  }
  orblit_renderer_apply_materials(renderer, 3, materialKeys, materialFlags, params.data(),
                                  params.size(), maps.data(), maps.size(), nullptr, nullptr, 0,
                                  videos);

  const int64_t keys[3] = {1, 2, 3};
  float transforms[48];
  for (int i = 0; i < 3; i++) {
    const float placed[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -2.3f + 2.3f * i, 0, 0, 1};
    std::memcpy(transforms + i * 16, placed, sizeof placed);
  }
  const float colours[9] = {1, 1, 1, 1, 1, 1, 1, 1, 1};
  const int32_t meshes[3] = {0, 0, 0};
  const int32_t flags[3] = {7, 7, 7};
  const int32_t materials[3] = {0, 1, 2};
  const int32_t morphs[3] = {0, 0, 0};
  const float weight = 0;
  const char *paths[1] = {kSphere};
  orblit_renderer_apply_objects(renderer, 3, keys, transforms, 48, colours, 9, meshes, flags,
                                materials, morphs, &weight, 0, paths, 1);
  const float eye[3] = {0.0f, 0.0f, 7.5f};
  const float look[3] = {0.0f, 0.0f, 0.0f};
  orblit_renderer_set_camera(renderer, eye, look, 32.0f, 0, 10.0f, 0.0);
  return renderer;
}

/// The environment's four numbers: thirty thousand lux, unturned, the
/// backdrop drawn or not, and a size (nought for the device's own choice).
bool environment(orblit_renderer *renderer, const std::string &radiance,
                 const std::string &skybox, bool backdrop, float size = 0) {
  const float params[4] = {30000.0f, 0.0f, backdrop ? 1.0f : 0.0f, size};
  return orblit_renderer_set_environment(renderer, radiance.c_str(), skybox.c_str(), params,
                                         4) == ORBLIT_OK;
}

/// Every note, as "about: saying" lines.
std::string notes(orblit_renderer *renderer) {
  std::string all;
  const uint32_t count = orblit_renderer_notes(renderer);
  for (uint32_t i = 0; i < count; i++) {
    const char *about = nullptr;
    const char *saying = nullptr;
    if (orblit_renderer_note(renderer, i, &about, &saying) == ORBLIT_OK) {
      all += std::string(about) + ": " + saying + "\n";
    }
  }
  return all;
}

struct Frame {
  std::vector<uint8_t> rgba;
  bool empty() const { return rgba.empty(); }
};

/// A frame, after enough frames for everything to have settled.
Frame capture(orblit_renderer *renderer, int settle = 8) {
  for (int i = 0; i < settle; i++) orblit_renderer_draw(renderer, 1.0);
  orblit_renderer_request_capture(renderer);
  for (int i = 0; i < 6; i++) orblit_renderer_draw(renderer, 1.0);
  Frame frame;
  uint32_t width = 0;
  uint32_t height = 0;
  const size_t bytes = orblit_renderer_read_capture(renderer, nullptr, 0, &width, &height);
  if (bytes == 0) return frame;
  frame.rgba.resize(bytes);
  orblit_renderer_read_capture(renderer, frame.rgba.data(), bytes, &width, &height);
  return frame;
}

void dump(const Frame &frame, const std::string &name) {
  const std::string dir = env("ORBLIT_CHECK_DUMP");
  if (dir.empty() || frame.empty()) return;
  FILE *out = std::fopen((dir + "/" + name + ".ppm").c_str(), "wb");
  if (out == nullptr) return;
  std::fprintf(out, "P6\n%u %u\n255\n", kWidth, kHeight);
  for (size_t i = 0; i < size_t(kWidth) * kHeight; i++) std::fwrite(&frame.rgba[i * 4], 1, 3, out);
  std::fclose(out);
}

struct Difference {
  double mean = 0;       // levels, averaged over every channel of every pixel
  int percentile99 = 0;  // of each pixel's largest channel difference
  double over2 = 0;      // share of pixels whose largest channel differs by more than 2
  double over8 = 0;
  double over32 = 0;
  int largest = 0;
};

/// How `a` and `b` differ between columns `from` and `to`.
Difference differ(const Frame &a, const Frame &b, uint32_t from = 0, uint32_t to = kWidth) {
  Difference d;
  if (a.empty() || b.empty()) {
    d.largest = 255;
    d.mean = 255;
    return d;
  }
  std::vector<int> each;
  double sum = 0;
  for (uint32_t y = 0; y < kHeight; y++) {
    for (uint32_t x = from; x < to; x++) {
      const size_t at = (size_t(y) * kWidth + x) * 4;
      int most = 0;
      for (int c = 0; c < 3; c++) {
        const int delta = std::abs(int(a.rgba[at + c]) - int(b.rgba[at + c]));
        sum += delta;
        most = std::max(most, delta);
      }
      each.push_back(most);
    }
  }
  std::sort(each.begin(), each.end());
  const double n = double(each.size());
  d.mean = sum / (n * 3);
  d.percentile99 = each[size_t(n * 0.99)];
  d.largest = each.back();
  for (int v : each) {
    d.over2 += v > 2;
    d.over8 += v > 8;
    d.over32 += v > 32;
  }
  d.over2 /= n;
  d.over8 /= n;
  d.over32 /= n;
  return d;
}

void report(const char *what, const Difference &d) {
  std::printf("  %-34s mean %.2f, p99 %d, max %d; over 2: %.1f%%, over 8: %.1f%%, over 32: %.2f%%\n",
              what, d.mean, d.percentile99, d.largest, d.over2 * 100, d.over8 * 100,
              d.over32 * 100);
}

void reportSpheres(const char *label, const Frame &a, const Frame &b) {
  std::printf(" %s\n", label);
  report("whole frame", differ(a, b));
  report("mirror (left third)", differ(a, b, 0, kWidth / 3));
  report("rough metal (middle third)", differ(a, b, kWidth / 3, 2 * kWidth / 3));
  report("matte (right third)", differ(a, b, 2 * kWidth / 3, kWidth));
}

/// Draws until the pictures being filtered have been, timing every frame.
/// Returns false if they never were.
bool waitForFilter(orblit_renderer *renderer, uint64_t filteredBefore,
                   std::vector<double> *frames = nullptr) {
  for (int i = 0; i < 1000; i++) {
    const Clock::time_point started = Clock::now();
    orblit_renderer_draw(renderer, 1.0);
    if (frames != nullptr) frames->push_back(millisecondsSince(started));
    if (orblit::environmentPicturesFiltered() > filteredBefore &&
        notes(renderer).find("being filtered") == std::string::npos) {
      return true;
    }
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
  }
  return false;
}

/// Draws until nothing is being filtered any more (a failure, or a cache hit
/// found by a worker).
void waitForNotes(orblit_renderer *renderer) {
  for (int i = 0; i < 1000; i++) {
    orblit_renderer_draw(renderer, 1.0);
    if (notes(renderer).find("being filtered") == std::string::npos) return;
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
  }
}

bool bake(const std::string &picture, const std::string &out, uint32_t size, uint32_t skybox) {
  const std::string script = env("ORBLIT_BAKE_SCRIPT");
  if (script.empty()) return false;
  const std::string command = "bash '" + script + "' --size " + std::to_string(size) +
                              " --skybox " + std::to_string(skybox) + " '" + picture + "' '" +
                              out + "' > /dev/null";
  return std::system(command.c_str()) == 0;
}

std::string stem(const std::string &path) {
  std::string name = path.substr(path.find_last_of('/') + 1);
  return name.substr(0, name.find_last_of('.'));
}

// ---- The checks ----

/// The harmonics against the ones cmgen wrote beside its bake.
void harmonicsMatchCmgen(const std::string &picture, const std::string &shText) {
  const std::vector<uint8_t> bytes = pictures::readFile(picture);
  const orblit::HdrDecoded decoded = orblit::decodeHdrImage(bytes.data(), bytes.size(), {});
  expect(decoded.note.empty(), "the picture decodes: " + decoded.note);
  if (decoded.image.empty()) return;
  const orblit::ForEach threads = [](size_t n, const std::function<void(size_t)> &body) {
    std::vector<std::thread> running;
    for (size_t i = 0; i < n; i++) running.emplace_back([&body, i] { body(i); });
    for (std::thread &thread : running) thread.join();
  };
  const orblit::Harmonics ours = orblit::irradianceHarmonics(
      orblit::mirroredCubemap(orblit::cubemapFromEquirectangular(decoded.image, 256, threads),
                              threads),
      threads);
  FILE *in = std::fopen(shText.c_str(), "r");
  expect(in != nullptr, "cmgen wrote its harmonics");
  if (in == nullptr) return;
  double worstRelative = 0;
  double worstAbsolute = 0;
  for (int i = 0; i < 9; i++) {
    char line[256];
    double rgb[3] = {0, 0, 0};
    if (std::fgets(line, sizeof line, in) == nullptr ||
        std::sscanf(line, " (%lf, %lf, %lf)", &rgb[0], &rgb[1], &rgb[2]) != 3) {
      expect(false, "cmgen's harmonics parse");
      break;
    }
    for (int c = 0; c < 3; c++) {
      const double difference = std::abs(double(ours[size_t(i) * 3 + c]) - rgb[c]);
      worstAbsolute = std::max(worstAbsolute, difference);
      if (std::abs(rgb[c]) > 1e-3) {
        worstRelative = std::max(worstRelative, difference / std::abs(rgb[c]));
      }
    }
  }
  std::fclose(in);
  std::printf(" harmonics against cmgen's: largest difference %.2g (%.2g of the "
              "coefficient)\n",
              worstAbsolute, worstRelative);
  expect(worstRelative < 1e-3, "the harmonics are cmgen's to a thousandth");
}

void picturesLightLikeTheirBake(const std::string &picture, const std::string &work) {
  const std::string name = stem(picture);
  std::printf("%s\n", name.c_str());
  const std::vector<uint8_t> bytes = pictures::readFile(picture);
  orblit::HdrDecoded decoded = orblit::decodeHdrImage(bytes.data(), bytes.size(), {});
  expect(decoded.note.empty(), name + " decodes");
  if (decoded.image.empty()) return;
  uint32_t skybox = 1;
  while (skybox * 2 <= decoded.image.width / 4) skybox *= 2;
  skybox = std::min<uint32_t>(skybox, 1024);

  const std::string baked = work + "/" + name;
  expect(bake(picture, baked, 256, skybox), "the bake script bakes " + name);
  harmonicsMatchCmgen(picture, baked + "/" + name + "_sh.txt");

  orblit_renderer *renderer = start();
  expect(renderer != nullptr, "a renderer starts");
  if (renderer == nullptr) return;

  const std::string ibl = baked + "/" + name + "_ibl.ktx";
  const std::string sky = baked + "/" + name + "_skybox.ktx";
  expect(environment(renderer, ibl, sky, false), "the bake is named");
  capture(renderer, 20);
  const Frame bakedLight = capture(renderer);
  std::vector<double> steady;
  for (int i = 0; i < 60; i++) {
    const Clock::time_point started = Clock::now();
    orblit_renderer_draw(renderer, 1.0);
    steady.push_back(millisecondsSince(started));
  }
  std::sort(steady.begin(), steady.end());
  environment(renderer, ibl, sky, true);
  const Frame bakedBackdrop = capture(renderer);
  dump(bakedLight, name + "-baked");
  dump(bakedBackdrop, name + "-baked-backdrop");

  // The picture itself.
  const uint64_t before = orblit::environmentPicturesFiltered();
  expect(environment(renderer, picture, picture, false), "the picture is named");
  const std::string pending = notes(renderer);
  expect(pending.find("being filtered") != std::string::npos,
         "while it is filtered, the notes say so: " + pending);
  std::vector<double> frames;
  const Clock::time_point asked = Clock::now();
  const bool filtered = waitForFilter(renderer, before, &frames);
  const double tookMilliseconds = millisecondsSince(asked);
  expect(filtered, "the picture is filtered: " + notes(renderer));
  expect(orblit::environmentPicturesFiltered() == before + 1, "exactly once");
  const Frame runtimeLight = capture(renderer);
  environment(renderer, picture, picture, true);
  const Frame runtimeBackdrop = capture(renderer);
  dump(runtimeLight, name + "-runtime");
  dump(runtimeBackdrop, name + "-runtime-backdrop");

  std::sort(frames.begin(), frames.end());
  const double budget = 1000.0 / 60;
  int over = 0;
  for (double f : frames) over += f > budget;
  std::printf(" filtered in %.0f ms over %zu frames; frames while filtering: median %.1f ms, "
              "longest %.1f and %.1f ms, %d over %.1f ms (steady frames: median %.1f ms)\n",
              tookMilliseconds, frames.size(), frames[frames.size() / 2], frames.back(),
              frames.size() > 1 ? frames[frames.size() - 2] : 0.0, over, budget,
              steady[steady.size() / 2]);

  reportSpheres("GPU filter at 256 against cmgen at 256, backdrop hidden:", bakedLight,
                runtimeLight);
  std::printf(" backdrop at %u against cmgen's at %u:\n", skybox, skybox);
  report("whole frame, spheres included", differ(bakedBackdrop, runtimeBackdrop));
  const Difference light = differ(bakedLight, runtimeLight);
  expect(light.mean < 3.0, name + ": the run-time light is within three levels of the bake on average");
  expect(differ(bakedLight, runtimeLight, 2 * kWidth / 3, kWidth).percentile99 <= 8,
         name + ": matte, which only the harmonics light, is within 8 levels at the 99th percentile");

  // Named again, after something else: lit on the very first frame, with
  // nothing filtered.
  environment(renderer, ibl, sky, false);
  capture(renderer, 2);
  const uint64_t filteredNow = orblit::environmentPicturesFiltered();
  environment(renderer, picture, picture, false);
  orblit_renderer_request_capture(renderer);
  for (int i = 0; i < 6; i++) orblit_renderer_draw(renderer, 1.0);
  Frame firstFrame;
  size_t firstBytes = orblit_renderer_read_capture(renderer, nullptr, 0, nullptr, nullptr);
  firstFrame.rgba.resize(firstBytes);
  orblit_renderer_read_capture(renderer, firstFrame.rgba.data(), firstBytes, nullptr, nullptr);
  const Difference again = differ(runtimeLight, firstFrame);
  std::printf(" named again: %llu filtered, first frame %d levels from the lit one\n",
              static_cast<unsigned long long>(orblit::environmentPicturesFiltered() - filteredNow),
              again.largest);
  expect(orblit::environmentPicturesFiltered() == filteredNow,
         "naming the picture again filters nothing");
  expect(again.largest == 0, "and it lights the first frame drawn");

  // The same bytes under another name: a worker hashes them and finds them.
  const std::string alias = "orblit:resource/environment-check/" + name + "-again.hdr";
  orblit_renderer_provide_resource(alias.c_str(), bytes.data(), bytes.size());
  environment(renderer, alias, alias, false);
  waitForNotes(renderer);
  const Frame aliased = capture(renderer);
  std::printf(" the same bytes under another name: %llu filtered, %d levels from the lit frame\n",
              static_cast<unsigned long long>(orblit::environmentPicturesFiltered() - filteredNow),
              differ(runtimeLight, aliased).largest);
  expect(orblit::environmentPicturesFiltered() == filteredNow,
         "the same bytes under another name filter nothing");
  expect(differ(runtimeLight, aliased).largest == 0, "and light the same frame");

  // An EXR written from the decoded picture, in floats and in halves.
  pictures::Picture floats;
  floats.width = decoded.image.width;
  floats.height = decoded.image.height;
  floats.rgb.assign(decoded.image.rgb.get(),
                    decoded.image.rgb.get() + size_t(floats.width) * floats.height * 3);
  for (int half = 0; half < 2; half++) {
    const std::string exrName = work + "/" + name + (half ? "-half.exr" : "-float.exr");
    const std::vector<uint8_t> exr = pictures::openExrFile(
        floats, half ? TINYEXR_PIXELTYPE_HALF : TINYEXR_PIXELTYPE_FLOAT,
        half ? TINYEXR_COMPRESSIONTYPE_PIZ : TINYEXR_COMPRESSIONTYPE_ZIP);
    expect(pictures::writeFile(exrName, exr), "the check writes an EXR");
    const uint64_t exrBefore = orblit::environmentPicturesFiltered();
    environment(renderer, exrName, exrName, false);
    expect(waitForFilter(renderer, exrBefore), "the EXR is filtered: " + notes(renderer));
    const Frame exrLight = capture(renderer);
    dump(exrLight, name + (half ? "-exr-half" : "-exr-float"));
    reportSpheres(half ? "EXR (half floats, PIZ) against the HDR it was written from:"
                       : "EXR (floats, ZIP) against the HDR it was written from:",
                  runtimeLight, exrLight);
    if (!half) {
      expect(differ(runtimeLight, exrLight).largest == 0,
             "an EXR of the same light draws the same frame as the HDR");
    }
  }

  orblit_renderer_destroy(renderer);

  // The CPU route, against cmgen at the size it filters at.
  setenv("ORBLIT_ENVIRONMENT_FILTER", "cpu", 1);
  const std::string small = work + "/" + name + "-64";
  expect(bake(picture, small, 64, 256), "the bake script bakes at 64");
  renderer = start();
  if (renderer != nullptr) {
    environment(renderer, small + "/" + name + "_ibl.ktx", small + "/" + name + "_skybox.ktx",
                false);
    capture(renderer, 20);
    const Frame bakedSmall = capture(renderer);
    const uint64_t cpuBefore = orblit::environmentPicturesFiltered();
    std::vector<double> cpuFrames;
    const Clock::time_point cpuAsked = Clock::now();
    environment(renderer, picture, picture, false);
    expect(waitForFilter(renderer, cpuBefore, &cpuFrames), "the CPU route filters");
    const double cpuTook = millisecondsSince(cpuAsked);
    const Frame cpuLight = capture(renderer);
    dump(bakedSmall, name + "-baked-64");
    dump(cpuLight, name + "-cpu-64");
    std::sort(cpuFrames.begin(), cpuFrames.end());
    std::printf(" CPU route: lit %.0f ms after it was named; longest frame %.1f ms\n", cpuTook,
                cpuFrames.back());
    reportSpheres("CPU filter at 64 against cmgen at 64, backdrop hidden:", bakedSmall, cpuLight);
    expect(differ(bakedSmall, cpuLight).mean < 3.0,
           name + ": the CPU route is within three levels of its bake on average");
    orblit_renderer_destroy(renderer);
  }
  unsetenv("ORBLIT_ENVIRONMENT_FILTER");
}

void damagedPicturesAreNoted(const std::string &work, const std::string &realExr) {
  orblit_renderer *renderer = start();
  if (renderer == nullptr) return;

  environment(renderer, work + "/no-such-picture.hdr", "", false);
  waitForNotes(renderer);
  std::string said = notes(renderer);
  expect(said.find("environment: no-such-picture.hdr could not be read") != std::string::npos,
         "a missing picture is noted: " + said);

  std::vector<uint8_t> cut = pictures::radianceFile(pictures::sky(256));
  cut.resize(cut.size() * 3 / 5);
  const char *cutName = "orblit:resource/environment-check/cut.hdr";
  orblit_renderer_provide_resource(cutName, cut.data(), cut.size());
  environment(renderer, cutName, cutName, true);
  waitForNotes(renderer);
  said = notes(renderer);
  expect(said.find("environment: cut.hdr: It ends part-way through its pixels") != std::string::npos &&
             said.find("skybox: cut.hdr") != std::string::npos,
         "a truncated picture is noted for the light and the backdrop: " + said);

  pictures::Picture square;
  square.width = 64;
  square.height = 64;
  square.rgb.assign(64 * 64 * 3, 1.0f);
  const std::vector<uint8_t> squareFile = pictures::radianceFile(square);
  const char *squareName = "orblit:resource/environment-check/square.hdr";
  orblit_renderer_provide_resource(squareName, squareFile.data(), squareFile.size());
  environment(renderer, squareName, "", false);
  waitForNotes(renderer);
  said = notes(renderer);
  expect(said.find("twice as wide as it is tall") != std::string::npos,
         "a picture that is not equirectangular is noted: " + said);

  if (!realExr.empty()) {
    environment(renderer, realExr, "", false);
    waitForNotes(renderer);
    said = notes(renderer);
    std::printf(" asakusa.exr: %s", said.c_str());
    expect(said.find("660 by 440") != std::string::npos,
           "a real EXR decodes, and is noted as not equirectangular: " + said);
  }

  // Failures are remembered: naming the same missing picture again reads
  // nothing and says the same.
  environment(renderer, "", "", false);
  orblit_renderer_draw(renderer, 1.0);
  expect(notes(renderer).find("environment:") == std::string::npos,
         "clearing the environment clears its notes");
  environment(renderer, squareName, "", false);
  said = notes(renderer);
  expect(said.find("twice as wide") != std::string::npos,
         "a picture that failed is remembered, and noted at once: " + said);

  orblit_renderer_destroy(renderer);
}

}  // namespace

int main() {
  const std::string work = env("ORBLIT_CHECK_WORK");
  if (work.empty() || env("ORBLIT_BAKE_SCRIPT").empty()) {
    std::printf("orblit_environment_check: skipped — needs ORBLIT_CHECK_WORK and "
                "ORBLIT_BAKE_SCRIPT (build.sh test sets both)\n");
    return 0;
  }
  std::system(("mkdir -p '" + work + "'").c_str());
  const std::vector<uint8_t> sphere = sphereGlb();
  orblit_renderer_provide_resource(kSphere, sphere.data(), sphere.size());

  // The check's own sky, always.
  const std::string sky = work + "/synthetic_sky.hdr";
  expect(pictures::writeFile(sky, pictures::radianceFile(pictures::sky(2048))),
         "the check writes its sky");
  picturesLightLikeTheirBake(sky, work);

  std::string realExr;
  const std::string samples = env("ORBLIT_ENVIRONMENTS");
  if (!samples.empty()) {
    const std::string pillars = samples + "/pillars_2k.hdr";
    if (!pictures::readFile(pillars).empty()) {
      picturesLightLikeTheirBake(pillars, work);
    } else {
      std::printf("skipped pillars_2k.hdr: not in ORBLIT_ENVIRONMENTS\n");
      skipped++;
    }
    if (!pictures::readFile(samples + "/asakusa.exr").empty()) realExr = samples + "/asakusa.exr";
  } else {
    std::printf("skipped real pictures: ORBLIT_ENVIRONMENTS is not set\n");
    skipped++;
  }

  damagedPicturesAreNoted(work, realExr);

  if (failures > 0) {
    std::fprintf(stderr, "orblit_environment_check: %d failed\n", failures);
    return 1;
  }
  std::printf("orblit_environment_check: all passed (%d skipped)\n", skipped);
  return 0;
}
