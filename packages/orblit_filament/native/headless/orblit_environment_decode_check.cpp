// The .hdr and .exr decoders on their own, built by build.sh with the address
// and undefined-behaviour sanitisers.
//
// A scene can name any file as its environment, so these read bytes nobody
// vouched for. What is checked: a picture comes back exactly as the bytes say
// (worked out here, from the bytes, not by the decoder); every damaged,
// truncated or oversized file is refused with a sentence rather than read;
// nothing a header claims is allocated before it is checked; and a few
// thousand mutated files neither crash, read out of bounds, nor hang. Plus
// the half-float packing and the CPU filter's arithmetic, which are pure too.
//
//   build/orblit_environment_decode_check            3000 mutations a kind
//   ORBLIT_FUZZ_COUNT=20000 build/orblit_environment_decode_check
//
// No Filament and no GPU: this links the three pure files and stb_image.

#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <functional>
#include <random>
#include <string>
#include <thread>
#include <vector>

#include "OrblitEnvironmentBake.h"
#include "OrblitHdrImage.h"
#include "environment_pictures.h"

namespace {

int failures = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    std::fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

orblit::HdrDecoded decode(const std::vector<uint8_t> &bytes,
                          const orblit::HdrLimits &limits = {}) {
  return orblit::decodeHdrImage(bytes.data(), bytes.size(), limits);
}

bool refused(const std::vector<uint8_t> &bytes, const std::string &needle = "",
             const orblit::HdrLimits &limits = {}) {
  const orblit::HdrDecoded result = decode(bytes, limits);
  return result.image.empty() && !result.note.empty() &&
         (needle.empty() || result.note.find(needle) != std::string::npos);
}

void radianceDecodesAsItsBytesSay() {
  for (bool flat : {false, true}) {
    const pictures::Picture sky = pictures::sky(256);
    const std::vector<uint8_t> file = pictures::radianceFile(sky, flat);
    const orblit::HdrDecoded result = decode(file);
    expect(result.note.empty(), "a Radiance file decodes: " + result.note);
    expect(result.image.width == 256 && result.image.height == 128,
           "at the size its header says");
    if (result.image.empty()) continue;
    size_t wrong = 0;
    for (size_t p = 0; p < size_t(256) * 128; p++) {
      uint8_t bytes[4];
      pictures::rgbe(&sky.rgb[p * 3], bytes);
      float expected[3];
      pictures::fromRgbe(bytes, expected);
      for (int c = 0; c < 3; c++) {
        if (result.image.rgb[p * 3 + c] != expected[c]) wrong++;
      }
    }
    expect(wrong == 0, std::string(flat ? "flat" : "run-length") +
                           " pixels are (v + ½)·2^(e−136) exactly, as cmgen "
                           "reads them; " + std::to_string(wrong) + " differ");
  }

  // Narrower than eight is always flat, whatever the writer wanted.
  pictures::Picture narrow;
  narrow.width = 4;
  narrow.height = 2;
  narrow.rgb.assign(24, 0.5f);
  narrow.rgb[0] = 3.0f;
  const orblit::HdrDecoded small = decode(pictures::radianceFile(narrow));
  expect(small.note.empty() && small.image.width == 4 && small.image.height == 2,
         "a picture four pixels wide decodes: " + small.note);
}

void openExrDecodesAsWritten() {
  const pictures::Picture sky = pictures::sky(128);
  const int kinds[] = {TINYEXR_COMPRESSIONTYPE_NONE, TINYEXR_COMPRESSIONTYPE_RLE,
                       TINYEXR_COMPRESSIONTYPE_ZIPS, TINYEXR_COMPRESSIONTYPE_ZIP,
                       TINYEXR_COMPRESSIONTYPE_PIZ};
  for (int kind : kinds) {
    const std::vector<uint8_t> file =
        pictures::openExrFile(sky, TINYEXR_PIXELTYPE_FLOAT, kind);
    expect(!file.empty(), "the check can write an EXR");
    const orblit::HdrDecoded result = decode(file);
    expect(result.note.empty(), "an EXR decodes: " + result.note);
    if (result.image.empty()) continue;
    size_t wrong = 0;
    for (size_t i = 0; i < sky.rgb.size(); i++) {
      if (result.image.rgb[i] != sky.rgb[i]) wrong++;
    }
    expect(wrong == 0, "a float EXR (compression " + std::to_string(kind) +
                           ") is every value it was written with; " +
                           std::to_string(wrong) + " differ");
  }
  const std::vector<uint8_t> half = pictures::openExrFile(
      sky, TINYEXR_PIXELTYPE_HALF, TINYEXR_COMPRESSIONTYPE_PIZ);
  const orblit::HdrDecoded halves = decode(half);
  expect(halves.note.empty(), "a half-float EXR decodes: " + halves.note);
  if (!halves.image.empty()) {
    double worst = 0;
    for (size_t i = 0; i < sky.rgb.size(); i++) {
      worst = std::max(worst, std::abs(double(halves.image.rgb[i]) - sky.rgb[i]) /
                                  std::max(1e-3, double(sky.rgb[i])));
    }
    expect(worst < 1e-3, "a half-float EXR is within half-float precision (" +
                             std::to_string(worst) + ")");
  }
}

void damagedFilesAreRefused() {
  const std::vector<uint8_t> none;
  expect(refused(none, "neither"), "no bytes are refused");
  expect(refused({'#', '?', 'R'}, "neither"), "three bytes are refused");

  const auto text = [](const std::string &words) {
    return std::vector<uint8_t>(words.begin(), words.end());
  };
  expect(refused(text("#?RADIANCE\n"), "32-bit RGBE"),
         "a header with no format line is refused");
  expect(refused(text("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n+Y 8 +X 16\n"),
                 "top to bottom"),
         "rows bottom to top are refused, not read upside down");
  expect(refused(text("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 0 +X 16\n"),
                 "no pixels"),
         "a picture of no pixels is refused");
  // A decompression bomb: a header claiming fifty thousand by a hundred
  // thousand pixels over no pixels at all.
  expect(refused(text("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 50000 +X 100000\n"),
                 "larger than this device decodes"),
         "a header claiming more than the limits is refused before allocating");
  orblit::HdrLimits tight;
  tight.longestSide = 64;
  tight.mostPixels = 64 * 32;
  expect(refused(pictures::radianceFile(pictures::sky(256)),
                 "larger than this device decodes", tight),
         "the limits are the device's, not constants");

  // A run of nought: stb_image 2.20 loops on it for ever.
  pictures::Picture wide = pictures::sky(16);
  std::vector<uint8_t> zero = pictures::radianceFile(wide);
  size_t pixelsStart = 0;
  for (size_t i = 0; i + 3 < zero.size(); i++) {
    if (zero[i] == 2 && zero[i + 1] == 2 && zero[i + 2] == 0 && zero[i + 3] == 16) {
      pixelsStart = i + 4;
      break;
    }
  }
  expect(pixelsStart > 0, "the check found its own scanline marker");
  if (pixelsStart > 0) {
    zero[pixelsStart] = 0;
    expect(refused(zero, "run of no pixels"), "a run of no pixels is refused");
    std::vector<uint8_t> past = pictures::radianceFile(wide);
    past[pixelsStart] = 128 + 40;
    expect(refused(past, "past the end"), "a run past its scanline is refused");
  }

  // Every truncation of a small file, run-length and flat, is refused — and
  // returns, which for stb_image 2.20 it would not.
  for (bool flat : {false, true}) {
    const std::vector<uint8_t> whole = pictures::radianceFile(pictures::sky(32), flat);
    size_t decodedEarly = 0;
    for (size_t length = 0; length < whole.size(); length++) {
      const std::vector<uint8_t> cut(whole.begin(), whole.begin() + long(length));
      if (!refused(cut)) decodedEarly++;
    }
    expect(decodedEarly == 0, std::string("every truncated ") +
                                  (flat ? "flat" : "run-length") +
                                  " Radiance file is refused (" +
                                  std::to_string(decodedEarly) + " were read)");
  }

  const std::vector<uint8_t> exr = pictures::openExrFile(
      pictures::sky(32), TINYEXR_PIXELTYPE_FLOAT, TINYEXR_COMPRESSIONTYPE_ZIP);
  size_t exrEarly = 0;
  for (size_t length = 0; length < exr.size(); length++) {
    const std::vector<uint8_t> cut(exr.begin(), exr.begin() + long(length));
    if (!refused(cut)) exrEarly++;
  }
  expect(exrEarly == 0, "every truncated EXR is refused (" +
                            std::to_string(exrEarly) + " were read)");

  // An EXR whose data window claims a hundred thousand pixels a side.
  std::vector<uint8_t> bomb = exr;
  const std::string attribute = "dataWindow";
  bool patched = false;
  for (size_t i = 0; i + attribute.size() + 30 < bomb.size(); i++) {
    if (std::memcmp(&bomb[i], attribute.data(), attribute.size()) == 0 &&
        bomb[i + attribute.size()] == 0) {
      // name, NUL, "box2i", NUL, a four-byte size, then four ints.
      const size_t values = i + attribute.size() + 1 + 6 + 4;
      const int32_t corner = 99999;
      std::memcpy(&bomb[values + 8], &corner, 4);
      std::memcpy(&bomb[values + 12], &corner, 4);
      patched = true;
      break;
    }
  }
  expect(patched, "the check found the EXR's data window");
  expect(refused(bomb, "larger than this device decodes"),
         "an EXR claiming 100000 by 100000 is refused before allocating");
}

/// Mutated files: flipped bytes, cut short, bytes inserted, runs overwritten.
void mutationsNeitherCrashNorHang() {
  const char *countText = std::getenv("ORBLIT_FUZZ_COUNT");
  const int count = countText != nullptr ? std::max(1, std::atoi(countText)) : 3000;
  const pictures::Picture sky = pictures::sky(64);
  struct Seed {
    const char *name;
    std::vector<uint8_t> bytes;
  };
  const Seed seeds[] = {
      {"run-length .hdr", pictures::radianceFile(sky)},
      {"flat .hdr", pictures::radianceFile(sky, true)},
      {"ZIP .exr", pictures::openExrFile(sky, TINYEXR_PIXELTYPE_FLOAT, TINYEXR_COMPRESSIONTYPE_ZIP)},
      {"PIZ .exr", pictures::openExrFile(sky, TINYEXR_PIXELTYPE_HALF, TINYEXR_COMPRESSIONTYPE_PIZ)},
      {"RLE .exr", pictures::openExrFile(sky, TINYEXR_PIXELTYPE_FLOAT, TINYEXR_COMPRESSIONTYPE_RLE)},
  };
  std::mt19937 random(20260916);
  orblit::HdrLimits limits;
  limits.longestSide = 4096;
  limits.mostPixels = 4096 * 2048;
  for (const Seed &seed : seeds) {
    int decoded = 0;
    int notes = 0;
    int silent = 0;
    double slowest = 0;
    for (int i = 0; i < count; i++) {
      std::vector<uint8_t> bytes = seed.bytes;
      const int kind = int(random() % 4);
      if (kind == 0) {
        const int flips = 1 + int(random() % 8);
        for (int f = 0; f < flips; f++) bytes[random() % bytes.size()] = uint8_t(random());
      } else if (kind == 1) {
        bytes.resize(random() % bytes.size());
      } else if (kind == 2) {
        const size_t at = random() % bytes.size();
        const size_t length = 1 + random() % 16;
        for (size_t b = 0; b < length; b++) {
          bytes.insert(bytes.begin() + long(at), uint8_t(random()));
        }
      } else {
        const size_t at = random() % bytes.size();
        const size_t length = std::min(bytes.size() - at, size_t(1 + random() % 64));
        const uint8_t value = random() % 3 == 0 ? 0 : random() % 2 ? 0xFF : uint8_t(random());
        for (size_t b = 0; b < length; b++) bytes[at + b] = value;
      }
      const auto started = std::chrono::steady_clock::now();
      const orblit::HdrDecoded result = decode(bytes, limits);
      const double seconds =
          std::chrono::duration<double>(std::chrono::steady_clock::now() - started).count();
      slowest = std::max(slowest, seconds);
      if (!result.image.empty()) {
        decoded++;
        if (result.image.width > limits.longestSide ||
            uint64_t(result.image.width) * result.image.height > limits.mostPixels) {
          silent++;
        }
      } else if (!result.note.empty()) {
        notes++;
      } else {
        silent++;
      }
    }
    std::printf("fuzz %s: %d mutations, %d decoded, %d refused with a note, "
                "slowest %.1f ms\n",
                seed.name, count, decoded, notes, slowest * 1000);
    expect(silent == 0, std::string(seed.name) +
                            ": every mutation decodes within the limits or "
                            "says why not (" + std::to_string(silent) + " did neither)");
    expect(slowest < 2.0, std::string(seed.name) + ": no mutation takes seconds");
  }
}

float fromHalf(uint16_t half) {
  const int sign = (half >> 15) & 1;
  const int exponent = (half >> 10) & 0x1F;
  const int mantissa = half & 0x3FF;
  double value = 0;
  if (exponent == 0) {
    value = std::ldexp(double(mantissa), -24);
  } else if (exponent == 31) {
    value = mantissa == 0 ? INFINITY : NAN;
  } else {
    value = std::ldexp(double(mantissa + 1024), exponent - 25);
  }
  return float(sign ? -value : value);
}

void halfFloatsRoundToNearest() {
  std::mt19937 random(7);
  std::uniform_real_distribution<float> exponent(-30.0f, 17.0f);
  size_t wrong = 0;
  for (int i = 0; i < 200000; i++) {
    const float value = (random() % 2 ? -1.0f : 1.0f) * std::pow(2.0f, exponent(random));
    const uint16_t half = orblit::halfFloat(value);
    const float back = fromHalf(half);
    const float clamped = std::max(-65504.0f, std::min(65504.0f, value));
    if (!std::isfinite(back)) {
      wrong++;
      continue;
    }
    // Nearest: neither neighbouring half float is closer.
    const float error = std::abs(back - clamped);
    for (int step : {-1, 1}) {
      const uint16_t magnitude = uint16_t(half & 0x7FFF);
      if (magnitude == 0 && step < 0) continue;
      if (magnitude >= 0x7BFF && step > 0) continue;
      const uint16_t neighbour = uint16_t((half & 0x8000) | (magnitude + step));
      if (std::abs(fromHalf(neighbour) - clamped) < error) {
        wrong++;
        break;
      }
    }
  }
  expect(wrong == 0, "half floats round to the nearest (" +
                         std::to_string(wrong) + " of 200000 did not)");
  expect(orblit::halfFloat(1e9f) == 0x7BFF, "past the largest half float holds at it");
  expect(orblit::halfFloat(NAN) == 0, "not a number packs as nought");
  expect(orblit::halfFloat(1.0f) == 0x3C00, "one is 0x3C00");
}

void harmonicsDoNotDependOnThreads() {
  const pictures::Picture sky = pictures::sky(512);
  orblit::HdrImage image;
  image.width = sky.width;
  image.height = sky.height;
  image.rgb.reset(static_cast<float *>(std::malloc(sky.rgb.size() * sizeof(float))));
  std::memcpy(image.rgb.get(), sky.rgb.data(), sky.rgb.size() * sizeof(float));
  const orblit::ForEach threads = [](size_t n, const std::function<void(size_t)> &body) {
    std::vector<std::thread> running;
    for (size_t i = 0; i < n; i++) running.emplace_back([&body, i] { body(i); });
    for (std::thread &thread : running) thread.join();
  };
  const orblit::Harmonics inOrder = orblit::irradianceHarmonics(
      orblit::mirroredCubemap(
          orblit::cubemapFromEquirectangular(image, 64, orblit::forEachInOrder),
          orblit::forEachInOrder),
      orblit::forEachInOrder);
  const orblit::Harmonics threaded = orblit::irradianceHarmonics(
      orblit::mirroredCubemap(orblit::cubemapFromEquirectangular(image, 64, threads),
                              threads),
      threads);
  expect(inOrder == threaded, "the harmonics are the same bits on one thread or six");

  // The CPU filter on a small cube: level 0 is the mirror, and every level
  // has the size cmgen gives it.
  orblit::CpuCubemap cube = orblit::mirroredCubemap(
      orblit::cubemapFromEquirectangular(image, 32, threads), threads);
  orblit::makeSeamless(cube);
  std::vector<orblit::CpuCubemap> mips;
  mips.push_back(cube);
  while (mips.back().size > 1) mips.push_back(orblit::halvedCubemap(mips.back()));
  const std::vector<orblit::CpuCubemap> levels = orblit::roughnessPrefilter(mips, 64, threads);
  expect(levels.size() == orblit::prefilterLevelCount(32) && levels.size() == 2,
         "a 32 cube filters to cmgen's 32 and 16");
  expect(orblit::prefilterLevelCount(256) == 5 && orblit::prefilterLevelCount(64) == 3 &&
             orblit::prefilterLevelCount(16) == 5,
         "cmgen's level counts: 256 → 5, 64 → 3, 16 → 5");
  bool mirror = !levels.empty();
  for (uint32_t face = 0; face < 6 && mirror; face++) {
    for (int y = 0; y < 32 && mirror; y++) {
      for (int x = 0; x < 32 && mirror; x++) {
        mirror = std::memcmp(levels[0].at(face, x, y), cube.at(face, x, y),
                             3 * sizeof(float)) == 0;
      }
    }
  }
  expect(mirror, "the first level is the cube itself, as a mirror reflects it");
}

}  // namespace

int main() {
  radianceDecodesAsItsBytesSay();
  openExrDecodesAsWritten();
  damagedFilesAreRefused();
  halfFloatsRoundToNearest();
  harmonicsDoNotDependOnThreads();
  mutationsNeitherCrashNorHang();
  if (failures > 0) {
    std::fprintf(stderr, "orblit_environment_decode_check: %d failed\n", failures);
    return 1;
  }
  std::printf("orblit_environment_decode_check: all passed\n");
  return 0;
}
