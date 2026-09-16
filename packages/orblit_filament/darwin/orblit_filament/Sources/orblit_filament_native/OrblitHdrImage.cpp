#include "OrblitHdrImage.h"

#include <climits>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <vector>

// stb_image's own declarations for the two functions used here, as
// OrblitPlatform.cpp declares the ones it uses. Filament's release links
// stb_image (v2.20, with its HDR loader) into libstb.a on every platform it
// ships, and the WebAssembly build of the fork does the same; neither ships
// the header. These are stb_image.h's public signatures, unchanged since
// long before that version.
extern "C" {
float *stbi_loadf_from_memory(const unsigned char *buffer, int length,
                              int *width, int *height, int *channels,
                              int desired_channels);
}

namespace orblit {

namespace {

/// What stb_image's HDR loader keeps a header line in, less its terminator.
constexpr size_t kLongestHeaderLine = 1023;

/// The first bytes of an OpenEXR file.
constexpr uint8_t kExrSignature[4] = {0x76, 0x2f, 0x31, 0x01};

bool startsWith(const uint8_t *bytes, size_t size, const char *prefix) {
  const size_t length = std::strlen(prefix);
  return size >= length && std::memcmp(bytes, prefix, length) == 0;
}

/// The bytes stb_image reads a Radiance file from, read exactly the way it
/// reads them: a byte past the end reads as nought and does not move.
struct Cursor {
  const uint8_t *at;
  const uint8_t *end;

  bool atEnd() const { return at >= end; }
  size_t left() const { return size_t(end - at); }
  uint8_t next() { return at < end ? *at++ : 0; }
};

/// One header line, as stbi__hdr_gettoken reads it — including its habit of
/// dropping the last byte of a file that ends without a newline, and of
/// cutting a long line short and skipping the rest of it. Walking the header
/// any other way would disagree with the loader about where the pixels start.
std::string headerLine(Cursor &cursor) {
  std::string line;
  char c = char(cursor.next());
  while (!cursor.atEnd() && c != '\n') {
    line.push_back(c);
    if (line.size() == kLongestHeaderLine) {
      while (!cursor.atEnd() && cursor.next() != '\n') {
      }
      break;
    }
    c = char(cursor.next());
  }
  return line;
}

std::string truncated() {
  return "It ends part-way through its pixels, so it is cut short or damaged.";
}

}  // namespace

HdrFormat hdrFormatOfName(const std::string &path) {
  const size_t slash = path.find_last_of("/\\");
  const size_t dot = path.find_last_of('.');
  if (dot == std::string::npos || (slash != std::string::npos && dot < slash)) {
    return HdrFormat::unknown;
  }
  std::string extension = path.substr(dot + 1);
  for (char &c : extension) {
    if (c >= 'A' && c <= 'Z') c = char(c - 'A' + 'a');
  }
  if (extension == "hdr") return HdrFormat::radiance;
  if (extension == "exr") return HdrFormat::openExr;
  return HdrFormat::unknown;
}

HdrFormat hdrFormatOfBytes(const uint8_t *bytes, size_t size) {
  if (bytes == nullptr) return HdrFormat::unknown;
  if (startsWith(bytes, size, "#?RADIANCE\n") ||
      startsWith(bytes, size, "#?RGBE\n")) {
    return HdrFormat::radiance;
  }
  if (size >= 4 && std::memcmp(bytes, kExrSignature, 4) == 0) {
    return HdrFormat::openExr;
  }
  return HdrFormat::unknown;
}

bool hdrSizeAllowed(uint64_t width, uint64_t height, const HdrLimits &limits,
                    std::string &note) {
  if (width == 0 || height == 0) {
    note = "Its header gives it no pixels.";
    return false;
  }
  if (width > limits.longestSide || height > limits.longestSide ||
      width * height > limits.mostPixels) {
    char words[200];
    std::snprintf(words, sizeof words,
                  "It is %llu by %llu, larger than this device decodes "
                  "(%u on a side, %llu pixels). Make it smaller, or bake it "
                  "with tool/bake_environment.sh.",
                  static_cast<unsigned long long>(width),
                  static_cast<unsigned long long>(height), limits.longestSide,
                  static_cast<unsigned long long>(limits.mostPixels));
    note = words;
    return false;
  }
  return true;
}

HdrDecoded decodeHdrImage(const uint8_t *bytes, size_t size,
                          const HdrLimits &limits) {
  switch (hdrFormatOfBytes(bytes, size)) {
    case HdrFormat::radiance:
      return decodeRadiance(bytes, size, limits);
    case HdrFormat::openExr:
      return decodeOpenExr(bytes, size, limits);
    case HdrFormat::unknown:
      break;
  }
  HdrDecoded refused;
  refused.note = "It is neither a Radiance .hdr nor an OpenEXR .exr file.";
  return refused;
}

/// Walks the whole file before stb_image is allowed near it, then lets it
/// decode, then puts back the half step cmgen's own decoder adds.
///
/// Why walk first. The stb_image inside Filament's libstb.a is v2.20, and it
/// cannot be patched from here. Its HDR loader treats a run of length nought
/// as a run that never ends, so one zero byte in the pixels — or a file cut
/// off part-way, whose missing bytes it reads as zeros — loops for ever
/// (CVE-2021-42715, fixed in stb_image 2.28). A flat file cut short decodes
/// uninitialised memory as pixels rather than failing. The walk below follows
/// the loader's reading byte for byte, bounds-checked, and refuses anything
/// the loader would mishandle; only a file it accepts reaches the loader, so
/// the loader only ever reads bytes that exist and every run moves.
///
/// Why the half step. A Radiance pixel is a shared exponent and three 8-bit
/// mantissas, and each mantissa stands for the whole step between it and the
/// next. Radiance's own reader, and cmgen's (libimageio's HDRDecoder), take
/// the middle of that step: (v + 0.5) × 2^(e−136). stb_image takes its
/// bottom, v × 2^(e−136). The difference is up to half a percent of a
/// pixel's brightest channel and more on its dim ones, and it is the whole
/// difference between these harmonics and a bake's. The walk already reads
/// every exponent, so it keeps them, and adds the half step back where the
/// exponent is not nought — which reproduces cmgen's numbers exactly, since a
/// power of two times a small integer and a half is exact in a float.
HdrDecoded decodeRadiance(const uint8_t *bytes, size_t size,
                          const HdrLimits &limits) {
  HdrDecoded result;
  if (hdrFormatOfBytes(bytes, size) != HdrFormat::radiance) {
    result.note = "It does not start as a Radiance .hdr file does.";
    return result;
  }
  // stb_image measures its input in an int.
  if (size > size_t(INT_MAX)) {
    result.note = "It is over two gigabytes, which no environment needs.";
    return result;
  }

  Cursor cursor{bytes, bytes + size};
  (void)headerLine(cursor);  // the signature, checked above

  bool rgbe = false;
  for (;;) {
    const std::string line = headerLine(cursor);
    // As C strings, the way the loader compares them: a nought byte ends a
    // line early there, so it has to here.
    if (line.c_str()[0] == '\0') break;
    if (std::strcmp(line.c_str(), "FORMAT=32-bit_rle_rgbe") == 0) rgbe = true;
  }
  if (!rgbe) {
    result.note = "It is not in the 32-bit RGBE format (XYZE and headerless "
                  "files are not read).";
    return result;
  }

  const std::string resolution = headerLine(cursor);
  const char *text = resolution.c_str();
  if (std::strncmp(text, "-Y ", 3) != 0) {
    result.note = "Its rows run in an order other than top to bottom, which "
                  "is not read. Re-save it from an image editor.";
    return result;
  }
  char *after = nullptr;
  const long tall = std::strtol(text + 3, &after, 10);
  while (*after == ' ') ++after;
  if (std::strncmp(after, "+X ", 3) != 0) {
    result.note = "Its columns run right to left, which is not read. Re-save "
                  "it from an image editor.";
    return result;
  }
  const long wide = std::strtol(after + 3, nullptr, 10);
  if (tall < 1 || wide < 1) {
    result.note = "Its header gives it no pixels.";
    return result;
  }
  if (!hdrSizeAllowed(uint64_t(wide), uint64_t(tall), limits, result.note)) {
    return result;
  }
  const size_t width = size_t(wide);
  const size_t height = size_t(tall);
  const size_t pixels = width * height;

  // The exponents, kept for the half step. One byte a pixel beside twelve.
  std::vector<uint8_t> exponents(pixels);

  // A flat file: four bytes a pixel, every pixel. stb_image reads one when
  // the width rules out run-length encoding, and — from the first scanline —
  // when that scanline does not start with the run-length marker.
  const auto readFlat = [&](Cursor &from) -> bool {
    if (from.left() / 4 < pixels) return false;
    for (size_t p = 0; p < pixels; p++) exponents[p] = from.at[p * 4 + 3];
    from.at += pixels * 4;
    return true;
  };

  if (width < 8 || width >= 32768) {
    if (!readFlat(cursor)) {
      result.note = truncated();
      return result;
    }
  } else {
    for (size_t row = 0; row < height; row++) {
      if (cursor.left() < 4) {
        result.note = truncated();
        return result;
      }
      const uint8_t *marker = cursor.at;
      if (marker[0] != 2 || marker[1] != 2 || (marker[2] & 0x80) != 0) {
        if (row != 0) {
          // stb_image would start the picture again from the top here.
          result.note = "Its scanlines switch from run-length encoded to "
                        "flat part-way through, so it is damaged.";
          return result;
        }
        if (!readFlat(cursor)) {
          result.note = truncated();
          return result;
        }
        break;
      }
      if ((size_t(marker[2]) << 8 | marker[3]) != width) {
        result.note = "A scanline says it is a different width from the "
                      "picture, so it is damaged.";
        return result;
      }
      cursor.at += 4;
      for (int channel = 0; channel < 4; channel++) {
        size_t filled = 0;
        while (filled < width) {
          if (cursor.atEnd()) {
            result.note = truncated();
            return result;
          }
          size_t count = cursor.next();
          if (count > 128) {
            if (cursor.atEnd()) {
              result.note = truncated();
              return result;
            }
            const uint8_t value = cursor.next();
            count -= 128;
            if (count > width - filled) {
              result.note = "A run goes past the end of its scanline, so it "
                            "is damaged.";
              return result;
            }
            if (channel == 3) {
              std::memset(&exponents[row * width + filled], value, count);
            }
          } else {
            if (count == 0) {
              result.note = "It holds a run of no pixels, so it is damaged.";
              return result;
            }
            if (count > width - filled) {
              result.note = "A run goes past the end of its scanline, so it "
                            "is damaged.";
              return result;
            }
            if (cursor.left() < count) {
              result.note = truncated();
              return result;
            }
            if (channel == 3) {
              std::memcpy(&exponents[row * width + filled], cursor.at, count);
            }
            cursor.at += count;
          }
          filled += count;
        }
      }
    }
  }

  int decodedWidth = 0;
  int decodedHeight = 0;
  int channels = 0;
  float *decoded = stbi_loadf_from_memory(bytes, int(size), &decodedWidth,
                                          &decodedHeight, &channels, 3);
  if (decoded == nullptr) {
    result.note = "It could not be decoded, or there was not the memory to.";
    return result;
  }
  result.image.rgb.reset(decoded);
  if (size_t(decodedWidth) != width || size_t(decodedHeight) != height) {
    result.image.rgb.reset();
    result.note = "It decoded to a different size from its header.";
    return result;
  }
  result.image.width = uint32_t(width);
  result.image.height = uint32_t(height);

  for (size_t p = 0; p < pixels; p++) {
    const int exponent = exponents[p];
    if (exponent == 0) continue;
    const float half = std::ldexp(0.5f, exponent - (128 + 8));
    float *rgb = decoded + p * 3;
    rgb[0] += half;
    rgb[1] += half;
    rgb[2] += half;
  }
  return result;
}

uint64_t hashBytes(const uint8_t *bytes, size_t size) {
  // Eight bytes at a time, each word mixed in with a multiply and a rotate,
  // then the tail, then a finaliser that spreads every bit across the result
  // (MurmurHash3's). Around ten gigabytes a second on a phone-class core, so
  // hashing a twenty-megabyte picture is a couple of milliseconds beside the
  // hundreds its decode takes.
  constexpr uint64_t kPrime1 = 0x9E3779B185EBCA87ull;
  constexpr uint64_t kPrime2 = 0xC2B2AE3D27D4EB4Full;
  uint64_t hash = kPrime1 ^ (uint64_t(size) * kPrime2);
  size_t at = 0;
  for (; at + 8 <= size; at += 8) {
    uint64_t word = 0;
    std::memcpy(&word, bytes + at, 8);
    word *= kPrime2;
    word = (word << 31) | (word >> 33);
    word *= kPrime1;
    hash ^= word;
    hash = ((hash << 27) | (hash >> 37)) * kPrime1 + 0x165667B19E3779F9ull;
  }
  for (; at < size; at++) {
    hash ^= uint64_t(bytes[at]) * kPrime1;
    hash = ((hash << 11) | (hash >> 53)) * kPrime2;
  }
  hash ^= hash >> 33;
  hash *= 0xFF51AFD7ED558CCDull;
  hash ^= hash >> 33;
  hash *= 0xC4CEB9FE1A85EC53ull;
  hash ^= hash >> 33;
  return hash;
}

}  // namespace orblit
