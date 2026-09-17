// Pictures for the environment checks, made here rather than read from
// anywhere: a Radiance file written byte by byte, an OpenEXR written through
// tinyexr's own writer, and a sky to put in them.
//
// Shared by orblit_environment_decode_check.cpp and
// orblit_environment_check.cpp. Header-only, because each check is one file.

#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#include "third_party/tinyexr/tinyexr.h"

namespace pictures {

/// A picture as three floats a pixel, top row first.
struct Picture {
  uint32_t width = 0;
  uint32_t height = 0;
  std::vector<float> rgb;
};

/// A sky over ground, with a small sun far brighter than everything else and
/// a red wall and a green one on opposite sides, so a mirror image or a turn
/// the wrong way shows as a different frame rather than the same one.
inline Picture sky(uint32_t width) {
  Picture picture;
  picture.width = width;
  picture.height = width / 2;
  picture.rgb.resize(size_t(picture.width) * picture.height * 3);
  const double pi = 3.14159265358979323846;
  for (uint32_t y = 0; y < picture.height; y++) {
    const double elevation = (0.5 - (y + 0.5) / picture.height) * pi;
    for (uint32_t x = 0; x < picture.width; x++) {
      const double azimuth = ((x + 0.5) / picture.width * 2 - 1) * pi;
      float *p = &picture.rgb[(size_t(y) * picture.width + x) * 3];
      if (elevation > 0) {
        const double t = std::sin(elevation);
        p[0] = float(0.9 - 0.6 * t);
        p[1] = float(1.0 - 0.4 * t);
        p[2] = float(1.2 + 0.3 * t);
      } else {
        p[0] = 0.25f;
        p[1] = 0.2f;
        p[2] = 0.12f;
      }
      if (std::abs(elevation) < 0.35 && std::abs(azimuth - pi / 2) < 0.4) {
        p[0] = 2.5f;
        p[1] = 0.15f;
        p[2] = 0.1f;
      }
      if (std::abs(elevation) < 0.35 && std::abs(azimuth + pi / 2) < 0.4) {
        p[0] = 0.1f;
        p[1] = 1.8f;
        p[2] = 0.2f;
      }
      const double sunElevation = 0.7;
      const double sunAzimuth = 0.4;
      const double dx = (azimuth - sunAzimuth) * std::cos(elevation);
      const double dy = elevation - sunElevation;
      if (dx * dx + dy * dy < 0.03 * 0.03) {
        p[0] = 9000.0f;
        p[1] = 8500.0f;
        p[2] = 8000.0f;
      }
    }
  }
  return picture;
}

/// One pixel as RGBE: Radiance's own float2rgbe.
inline void rgbe(const float *rgb, uint8_t *out) {
  const float largest = std::max(rgb[0], std::max(rgb[1], rgb[2]));
  if (!(largest >= 1e-32f)) {
    out[0] = out[1] = out[2] = out[3] = 0;
    return;
  }
  int exponent = 0;
  const float scale = float(std::frexp(largest, &exponent) * 256.0 / largest);
  out[0] = uint8_t(std::min(255.0f, rgb[0] * scale));
  out[1] = uint8_t(std::min(255.0f, rgb[1] * scale));
  out[2] = uint8_t(std::min(255.0f, rgb[2] * scale));
  out[3] = uint8_t(exponent + 128);
}

/// What a Radiance reader that takes the middle of each step — cmgen's —
/// makes of one RGBE pixel. Worked out from the bytes, independently of the
/// decoder under test.
inline void fromRgbe(const uint8_t *in, float *rgb) {
  if (in[3] == 0) {
    rgb[0] = rgb[1] = rgb[2] = 0;
    return;
  }
  const float unit = std::ldexp(1.0f, int(in[3]) - 136);
  for (int c = 0; c < 3; c++) rgb[c] = (float(in[c]) + 0.5f) * unit;
}

/// A Radiance file. Run-length encoded scanlines, as every modern writer
/// makes them, unless `flat`; each channel written as runs where it repeats
/// and literal stretches where it does not.
inline std::vector<uint8_t> radianceFile(const Picture &picture, bool flat = false) {
  std::string header = "#?RADIANCE\n# made by the environment check\n"
                       "FORMAT=32-bit_rle_rgbe\n\n";
  char resolution[64];
  std::snprintf(resolution, sizeof resolution, "-Y %u +X %u\n", picture.height,
                picture.width);
  header += resolution;
  std::vector<uint8_t> file(header.begin(), header.end());
  std::vector<uint8_t> row(size_t(picture.width) * 4);
  for (uint32_t y = 0; y < picture.height; y++) {
    for (uint32_t x = 0; x < picture.width; x++) {
      rgbe(&picture.rgb[(size_t(y) * picture.width + x) * 3], &row[size_t(x) * 4]);
    }
    if (flat || picture.width < 8 || picture.width >= 32768) {
      file.insert(file.end(), row.begin(), row.end());
      continue;
    }
    file.push_back(2);
    file.push_back(2);
    file.push_back(uint8_t(picture.width >> 8));
    file.push_back(uint8_t(picture.width & 0xFF));
    for (int channel = 0; channel < 4; channel++) {
      size_t x = 0;
      while (x < picture.width) {
        size_t run = 1;
        while (x + run < picture.width && run < 127 &&
               row[(x + run) * 4 + channel] == row[x * 4 + channel]) {
          run++;
        }
        if (run >= 3) {
          file.push_back(uint8_t(128 + run));
          file.push_back(row[x * 4 + channel]);
          x += run;
          continue;
        }
        size_t literal = 0;
        while (x + literal < picture.width && literal < 128) {
          const size_t at = x + literal;
          if (at + 2 < picture.width && row[at * 4 + channel] == row[(at + 1) * 4 + channel] &&
              row[at * 4 + channel] == row[(at + 2) * 4 + channel]) {
            break;
          }
          literal++;
        }
        if (literal == 0) literal = 1;
        file.push_back(uint8_t(literal));
        for (size_t i = 0; i < literal; i++) file.push_back(row[(x + i) * 4 + channel]);
        x += literal;
      }
    }
  }
  return file;
}

/// An OpenEXR file of `picture`, stored as `pixelType` (TINYEXR_PIXELTYPE_
/// FLOAT or _HALF) with `compression` (TINYEXR_COMPRESSIONTYPE_*).
inline std::vector<uint8_t> openExrFile(const Picture &picture, int pixelType,
                                        int compression) {
  const size_t pixels = size_t(picture.width) * picture.height;
  // OpenEXR lists channels by name, so B, G, R.
  std::vector<float> planes[3];
  for (int c = 0; c < 3; c++) {
    planes[c].resize(pixels);
    for (size_t p = 0; p < pixels; p++) planes[c][p] = picture.rgb[p * 3 + (2 - c)];
  }
  EXRHeader header;
  InitEXRHeader(&header);
  EXRImage image;
  InitEXRImage(&image);
  image.num_channels = 3;
  unsigned char *pointers[3] = {
      reinterpret_cast<unsigned char *>(planes[0].data()),
      reinterpret_cast<unsigned char *>(planes[1].data()),
      reinterpret_cast<unsigned char *>(planes[2].data())};
  image.images = pointers;
  image.width = int(picture.width);
  image.height = int(picture.height);
  header.num_channels = 3;
  EXRChannelInfo channels[3];
  std::memset(channels, 0, sizeof channels);
  std::strcpy(channels[0].name, "B");
  std::strcpy(channels[1].name, "G");
  std::strcpy(channels[2].name, "R");
  header.channels = channels;
  int inTypes[3] = {TINYEXR_PIXELTYPE_FLOAT, TINYEXR_PIXELTYPE_FLOAT,
                    TINYEXR_PIXELTYPE_FLOAT};
  int outTypes[3] = {pixelType, pixelType, pixelType};
  header.pixel_types = inTypes;
  header.requested_pixel_types = outTypes;
  header.compression_type = compression;
  unsigned char *memory = nullptr;
  const char *error = nullptr;
  const size_t size = SaveEXRImageToMemory(&image, &header, &memory, &error);
  std::vector<uint8_t> file;
  if (size > 0 && memory != nullptr) file.assign(memory, memory + size);
  if (error != nullptr) {
    std::fprintf(stderr, "tinyexr could not write the check's picture: %s\n", error);
    FreeEXRErrorMessage(error);
  }
  std::free(memory);
  return file;
}

inline bool writeFile(const std::string &path, const std::vector<uint8_t> &bytes) {
  FILE *out = std::fopen(path.c_str(), "wb");
  if (out == nullptr) return false;
  const bool wrote = std::fwrite(bytes.data(), 1, bytes.size(), out) == bytes.size();
  std::fclose(out);
  return wrote;
}

inline std::vector<uint8_t> readFile(const std::string &path) {
  std::vector<uint8_t> bytes;
  FILE *in = std::fopen(path.c_str(), "rb");
  if (in == nullptr) return bytes;
  std::fseek(in, 0, SEEK_END);
  const long size = std::ftell(in);
  std::fseek(in, 0, SEEK_SET);
  if (size > 0) {
    bytes.resize(size_t(size));
    if (std::fread(bytes.data(), 1, bytes.size(), in) != bytes.size()) bytes.clear();
  }
  std::fclose(in);
  return bytes;
}

}  // namespace pictures
