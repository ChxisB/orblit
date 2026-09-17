// tinyexr and miniz, compiled here and nowhere else, and the EXR half of
// OrblitHdrImage.h on top of them.
//
// Vendored:
//  * tinyexr 3.2.0 — third_party/tinyexr/tinyexr.h, exr_reader.hh and
//    streamreader.hh — as Filament's own third_party/tinyexr carries it
//    (google/filament commit dfbe0a3f7, "tinyexr: update to 3.2.0"). BSD
//    3-clause, with OpenEXR's BSD-style notice for the code it contains;
//    LICENSES/tinyexr.txt.
//  * miniz 3.0.0 — third_party/miniz/miniz.h and miniz.c — the deflate
//    tinyexr ships with, from the same place. MIT; LICENSES/miniz.txt. One
//    change: miniz.c declared s_tdefl_num_probes ahead of its use with no
//    initialiser, a tentative definition C allows and C++ does not, so the
//    table's definition is moved up to where that declaration was. Nothing
//    else in either file is touched.
// To update: copy newer files from a Filament checkout's third_party/tinyexr
// (and its deps/miniz), change the versions above, and run
// native/headless/build.sh test, whose environment check decodes real and
// damaged EXR files under the address and undefined-behaviour sanitisers.
//
// Why miniz rather than zlib. Nothing Filament links is zlib — its
// "uberzlib" is the reader for its own zstd-compressed material archives —
// and zlib's header is on Apple's and Android's systems but not Windows'.
// miniz is one C file with a zlib-shaped interface, so it goes wherever the
// renderer does.
//
// Why a .cpp that includes a .c, as OrblitUfbx.cpp explains for ufbx: every
// build takes the top-level .cpp files and none compiles C. miniz compiles as
// C++. Package.swift excludes miniz.c so SwiftPM does not compile it a second
// time; CocoaPods' pattern does not match .c.
//
// miniz is kept to this file. It is included inside an unnamed namespace,
// which gives every one of its functions internal linkage — its extern "C"
// declarations included — so none of them is visible to the linker: an
// application that links its own miniz (a zip plugin, say) does not collide
// with this one. tinyexr's declarations are wrapped in extern "C" and meant
// to be linked against, so its LoadEXR… functions stay ordinary symbols.

#include "OrblitHdrImage.h"

#include <cmath>
#include <cstdio>
#include <cstring>

#if defined(__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wall"
#pragma clang diagnostic ignored "-Wextra"
#pragma clang diagnostic ignored "-Wunused-function"
#pragma clang diagnostic ignored "-Wunused-parameter"
#pragma clang diagnostic ignored "-Wsign-compare"
#pragma clang diagnostic ignored "-Wimplicit-fallthrough"
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
#elif defined(__GNUC__)
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wall"
#pragma GCC diagnostic ignored "-Wextra"
#elif defined(_MSC_VER)
#pragma warning(push, 0)
#endif

// No archives, no files, no clock: tinyexr only ever hands miniz a buffer.
#define MINIZ_NO_STDIO
#define MINIZ_NO_TIME
#define MINIZ_NO_ARCHIVE_APIS
// The C library headers miniz includes, first and outside, so that including
// them again inside the namespace below finds them already included.
#include <assert.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
namespace {
#include "third_party/miniz/miniz.h"
#include "third_party/miniz/miniz.c"
}  // namespace

// tinyexr's zlib route, answered by miniz's zlib-compatible names included
// above. TINYEXR_USE_MINIZ would have tinyexr include <miniz.h> by a search
// path no build here sets. No threads and no OpenMP: decoding already runs
// off the drawing thread, and a browser build has neither.
#define TINYEXR_USE_MINIZ 0
#define TINYEXR_USE_STB_ZLIB 0
#define TINYEXR_USE_NANOZLIB 0
#define TINYEXR_USE_THREAD 0
#define TINYEXR_USE_OPENMP 0
#define TINYEXR_IMPLEMENTATION
#include "third_party/tinyexr/tinyexr.h"

#if defined(__clang__)
#pragma clang diagnostic pop
#elif defined(__GNUC__)
#pragma GCC diagnostic pop
#elif defined(_MSC_VER)
#pragma warning(pop)
#endif

namespace orblit {

namespace {

/// tinyexr's header, freed however the function holding it returns.
struct HeaderHolder {
  EXRHeader header;
  HeaderHolder() { InitEXRHeader(&header); }
  ~HeaderHolder() { FreeEXRHeader(&header); }
};

struct ImageHolder {
  EXRImage image;
  ImageHolder() { InitEXRImage(&image); }
  ~ImageHolder() { FreeEXRImage(&image); }
};

/// tinyexr's own words for what went wrong, when it gave some.
std::string withReason(const char *what, const char *error) {
  std::string note = what;
  if (error != nullptr) {
    note += " (";
    note += error;
    note += ")";
    FreeEXRErrorMessage(error);
  }
  note += ".";
  return note;
}

int channelNamed(const EXRHeader &header, const char *name) {
  for (int i = 0; i < header.num_channels; i++) {
    if (std::strcmp(header.channels[i].name, name) == 0) return i;
  }
  return -1;
}

}  // namespace

HdrDecoded decodeOpenExr(const uint8_t *bytes, size_t size,
                         const HdrLimits &limits) {
  HdrDecoded result;
  if (hdrFormatOfBytes(bytes, size) != HdrFormat::openExr) {
    result.note = "It does not start as an OpenEXR file does.";
    return result;
  }

  EXRVersion version;
  if (ParseEXRVersionFromMemory(&version, bytes, size) != TINYEXR_SUCCESS) {
    result.note = "Its OpenEXR version block is damaged.";
    return result;
  }
  if (version.multipart || version.non_image) {
    result.note = "It is a multi-part or deep OpenEXR file; an environment "
                  "is read from a single-part scanline file.";
    return result;
  }
  if (version.tiled) {
    result.note = "It is a tiled OpenEXR file; an environment is read from a "
                  "scanline file. Re-save it without tiles.";
    return result;
  }

  HeaderHolder header;
  const char *error = nullptr;
  if (ParseEXRHeaderFromMemory(&header.header, &version, bytes, size,
                               &error) != TINYEXR_SUCCESS) {
    result.note = withReason("Its OpenEXR header could not be read", error);
    return result;
  }

  // Worked out in 64 bits before anything is believed: a data window is two
  // signed corners, and a hostile one is wider than an int.
  const EXRBox2i &window = header.header.data_window;
  const int64_t wide = int64_t(window.max_x) - int64_t(window.min_x) + 1;
  const int64_t tall = int64_t(window.max_y) - int64_t(window.min_y) + 1;
  if (wide < 1 || tall < 1) {
    result.note = "Its header gives it no pixels.";
    return result;
  }
  if (!hdrSizeAllowed(uint64_t(wide), uint64_t(tall), limits, result.note)) {
    return result;
  }

  // Colour by name. A photographed environment is R, G and B; a grey one may
  // be luminance alone. Layered names (diffuse.R) are a render pass, not an
  // environment, and are not guessed at.
  int red = channelNamed(header.header, "R");
  int green = channelNamed(header.header, "G");
  int blue = channelNamed(header.header, "B");
  if (red < 0 || green < 0 || blue < 0) {
    const int luminance = channelNamed(header.header, "Y");
    if (luminance < 0) {
      result.note = "It has no R, G and B channels, nor a Y channel.";
      return result;
    }
    red = green = blue = luminance;
  }
  for (int channel : {red, green, blue}) {
    if (header.header.pixel_types[channel] == TINYEXR_PIXELTYPE_UINT) {
      result.note = "Its colour is stored as whole numbers rather than light.";
      return result;
    }
  }
  // Every half-float channel read as a full float, which is what tinyexr
  // converts to and what everything after this reads.
  for (int i = 0; i < header.header.num_channels; i++) {
    if (header.header.pixel_types[i] == TINYEXR_PIXELTYPE_HALF) {
      header.header.requested_pixel_types[i] = TINYEXR_PIXELTYPE_FLOAT;
    }
  }

  ImageHolder image;
  if (LoadEXRImageFromMemory(&image.image, &header.header, bytes, size,
                             &error) != TINYEXR_SUCCESS) {
    result.note = withReason("Its pixels could not be decoded", error);
    return result;
  }
  if (image.image.images == nullptr || image.image.width != wide ||
      image.image.height != tall) {
    result.note = "It decoded to a different size from its header.";
    return result;
  }

  const size_t pixels = size_t(wide) * size_t(tall);
  float *rgb = static_cast<float *>(std::malloc(pixels * 3 * sizeof(float)));
  if (rgb == nullptr) {
    result.note = "There was not the memory to decode it.";
    return result;
  }
  result.image.rgb.reset(rgb);
  const float *planes[3] = {
      reinterpret_cast<const float *>(image.image.images[red]),
      reinterpret_cast<const float *>(image.image.images[green]),
      reinterpret_cast<const float *>(image.image.images[blue])};
  // A value that is not a number, or is infinite, is a fault in whatever
  // wrote the file, and one of them would spread across every harmonic and
  // every blurred level it touches. Nought instead: one dark pixel is
  // invisible where a poisoned environment is black.
  for (size_t p = 0; p < pixels; p++) {
    for (int c = 0; c < 3; c++) {
      const float value = planes[c][p];
      rgb[p * 3 + c] = std::isfinite(value) ? value : 0.0f;
    }
  }
  result.image.width = uint32_t(wide);
  result.image.height = uint32_t(tall);
  return result;
}

}  // namespace orblit
