#pragma once

// Pictures of light: Radiance .hdr and OpenEXR .exr, decoded to linear RGB.
//
// What an environment is lit from at run time. A photograph of a place,
// stored as the light that arrived rather than as a picture meant for a
// screen, so a value of forty thousand in the sun is forty thousand and not
// a clipped white.
//
// A pure function of bytes and nothing else: no Filament types, no renderer,
// no file system, no threads. That is what lets the same two files run on a
// native worker thread today, on a browser's page thread for now, and later
// inside a small WebAssembly worker of their own, unchanged. OrblitTinyExr.cpp
// is the other half (EXR); this file is everything else.
//
// Files come from anywhere a scene names, so every size is checked against
// the limits before anything is allocated: a header that claims a picture
// far larger than its bytes could hold, or one larger than the device can
// keep, is refused with a note rather than trusted.

#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <memory>
#include <string>

namespace orblit {

/// How large a picture this device will decode.
///
/// From the device, not constants: see environmentImageLimits in
/// OrblitEnvironment.cpp for the rule, which is a share of physical memory.
struct HdrLimits {
  /// The widest picture, and the tallest, in pixels.
  uint32_t longestSide = 8192;
  /// The most pixels in one picture.
  uint64_t mostPixels = uint64_t(8192) * 4096;
};

/// Freed with std::free, because both decoders hand back malloc's memory
/// and copying a few hundred megabytes to change who owns it is not worth it.
struct FreeDeleter {
  void operator()(void *pointer) const { std::free(pointer); }
};

/// A decoded picture: linear light, three floats a pixel, the top row first.
struct HdrImage {
  uint32_t width = 0;
  uint32_t height = 0;
  std::unique_ptr<float[], FreeDeleter> rgb;

  bool empty() const { return rgb == nullptr || width == 0 || height == 0; }
};

/// A picture, or why there is not one.
struct HdrDecoded {
  HdrImage image;
  /// Empty when the picture decoded; a sentence for the scene notes when it
  /// did not.
  std::string note;
};

enum class HdrFormat { unknown, radiance, openExr };

/// What a name says it holds, by its extension and nothing else. This is
/// how the renderer tells a picture to filter from a cubemap cmgen baked.
HdrFormat hdrFormatOfName(const std::string &path);

/// What the bytes say they hold, by their signature.
HdrFormat hdrFormatOfBytes(const uint8_t *bytes, size_t size);

/// Decodes either kind, chosen by the bytes rather than the name, so a file
/// saved with the wrong extension is still read as what it is.
HdrDecoded decodeHdrImage(const uint8_t *bytes, size_t size,
                          const HdrLimits &limits);

/// A Radiance RGBE file, through stb_image.
HdrDecoded decodeRadiance(const uint8_t *bytes, size_t size,
                          const HdrLimits &limits);

/// An OpenEXR file, through tinyexr. In OrblitTinyExr.cpp.
HdrDecoded decodeOpenExr(const uint8_t *bytes, size_t size,
                         const HdrLimits &limits);

/// Whether `width` by `height` is within `limits`. Also the words for why
/// not, in `note`, naming the picture as `what`.
bool hdrSizeAllowed(uint64_t width, uint64_t height, const HdrLimits &limits,
                    std::string &note);

/// A 64-bit hash of `size` bytes: what the renderer keeps a filtered
/// environment under, so the same picture named twice, or under two names,
/// is filtered once. Not cryptographic; nothing here is an adversary's to
/// collide, and the size settings are part of the key beside it.
uint64_t hashBytes(const uint8_t *bytes, size_t size);

}  // namespace orblit
