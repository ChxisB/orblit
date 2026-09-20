#pragma once

// What the texture queue is made of, shared by its three sources and by
// nothing else.
//
// Taking a file in is one job, decoding and uploading it is another, and
// being the queue the renderer asks is a third; each is a source of its own
// beside this one. What they share is here: the table that says how a
// vkFormat is handed to Filament, the placeholder block a texture shows
// before its levels arrive, and `Item` and `Unit` — one texture on its way
// and one upload of it. The helpers were an anonymous namespace and the two
// records were defined out-of-line while this was one file.
//
// Not a public header. OrblitTextures.h is the interface; nothing outside
// these three sources includes this one, and `orblit::textures` is where the
// helpers live so that names as short as `freeBytes` and `mipLevels` do not
// reach the rest of the renderer.

#include "OrblitTextures.h"

#include <algorithm>
#include <array>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <cstring>

#include <ktxreader/Ktx2Reader.h>

#if defined(__APPLE__)
#include <pthread.h>
#include <sys/qos.h>
#endif

#include "OrblitDecode.h"
#include "OrblitPlatform.h"

// stb_image's own declarations, as OrblitPlatform.cpp has them and for the
// same reason: Filament's release links it into libstb.a on every platform
// and ships no header for it.
extern "C" {
unsigned char *stbi_load_from_memory(const unsigned char *buffer, int length,
                                     int *width, int *height, int *channels,
                                     int desiredChannels);
int stbi_info_from_memory(const unsigned char *buffer, int length, int *width,
                          int *height, int *channels);
void stbi_image_free(void *pixels);
}

namespace orblit {

using filament::Texture;
using InternalFormat = Texture::InternalFormat;

namespace textures {

/// How a vkFormat is handed to Filament: the texture's format, and how the
/// bytes of a level are described to setImage.
struct GpuFormat {
  uint32_t vkFormat;
  InternalFormat internal;
  bool compressed;
  Texture::CompressedType compressedType;
  Texture::Format pixelFormat;
  Texture::Type pixelType;
};

using IF = InternalFormat;
using CT = Texture::CompressedType;
using PF = Texture::Format;
using PT = Texture::Type;

#define ORBLIT_PIXELS(vk, internal, format, type) \
  {vk, IF::internal, false, CT::EAC_R11, PF::format, PT::type}
#define ORBLIT_BLOCKS(vk, name) \
  {vk, IF::name, true, CT::name, PF::RGBA, PT::COMPRESSED}

/// Every format OrblitKtx2.cpp reads, as Filament names it. The compressed
/// names are Filament's own for both the texture and the pixel data, which
/// is why one name serves both columns.
inline const GpuFormat kGpuFormats[] = {
    ORBLIT_PIXELS(9, R8, R, UBYTE),
    ORBLIT_PIXELS(16, RG8, RG, UBYTE),
    ORBLIT_PIXELS(23, RGB8, RGB, UBYTE),
    ORBLIT_PIXELS(29, SRGB8, RGB, UBYTE),
    ORBLIT_PIXELS(37, RGBA8, RGBA, UBYTE),
    ORBLIT_PIXELS(43, SRGB8_A8, RGBA, UBYTE),
    ORBLIT_PIXELS(90, RGB16F, RGB, HALF),
    ORBLIT_PIXELS(97, RGBA16F, RGBA, HALF),
    ORBLIT_PIXELS(106, RGB32F, RGB, FLOAT),
    ORBLIT_PIXELS(109, RGBA32F, RGBA, FLOAT),
    ORBLIT_PIXELS(122, R11F_G11F_B10F, RGB, UINT_10F_11F_11F_REV),

    ORBLIT_BLOCKS(131, DXT1_RGB),
    ORBLIT_BLOCKS(132, DXT1_SRGB),
    ORBLIT_BLOCKS(133, DXT1_RGBA),
    ORBLIT_BLOCKS(134, DXT1_SRGBA),
    ORBLIT_BLOCKS(135, DXT3_RGBA),
    ORBLIT_BLOCKS(136, DXT3_SRGBA),
    ORBLIT_BLOCKS(137, DXT5_RGBA),
    ORBLIT_BLOCKS(138, DXT5_SRGBA),
    ORBLIT_BLOCKS(139, RED_RGTC1),
    ORBLIT_BLOCKS(140, SIGNED_RED_RGTC1),
    ORBLIT_BLOCKS(141, RED_GREEN_RGTC2),
    ORBLIT_BLOCKS(142, SIGNED_RED_GREEN_RGTC2),
    ORBLIT_BLOCKS(143, RGB_BPTC_UNSIGNED_FLOAT),
    ORBLIT_BLOCKS(144, RGB_BPTC_SIGNED_FLOAT),
    ORBLIT_BLOCKS(145, RGBA_BPTC_UNORM),
    ORBLIT_BLOCKS(146, SRGB_ALPHA_BPTC_UNORM),

    ORBLIT_BLOCKS(147, ETC2_RGB8),
    ORBLIT_BLOCKS(148, ETC2_SRGB8),
    ORBLIT_BLOCKS(149, ETC2_RGB8_A1),
    ORBLIT_BLOCKS(150, ETC2_SRGB8_A1),
    ORBLIT_BLOCKS(151, ETC2_EAC_RGBA8),
    ORBLIT_BLOCKS(152, ETC2_EAC_SRGBA8),
    ORBLIT_BLOCKS(153, EAC_R11),
    ORBLIT_BLOCKS(154, EAC_R11_SIGNED),
    ORBLIT_BLOCKS(155, EAC_RG11),
    ORBLIT_BLOCKS(156, EAC_RG11_SIGNED),

    ORBLIT_BLOCKS(157, RGBA_ASTC_4x4),
    ORBLIT_BLOCKS(158, SRGB8_ALPHA8_ASTC_4x4),
    ORBLIT_BLOCKS(159, RGBA_ASTC_5x4),
    ORBLIT_BLOCKS(160, SRGB8_ALPHA8_ASTC_5x4),
    ORBLIT_BLOCKS(161, RGBA_ASTC_5x5),
    ORBLIT_BLOCKS(162, SRGB8_ALPHA8_ASTC_5x5),
    ORBLIT_BLOCKS(163, RGBA_ASTC_6x5),
    ORBLIT_BLOCKS(164, SRGB8_ALPHA8_ASTC_6x5),
    ORBLIT_BLOCKS(165, RGBA_ASTC_6x6),
    ORBLIT_BLOCKS(166, SRGB8_ALPHA8_ASTC_6x6),
    ORBLIT_BLOCKS(167, RGBA_ASTC_8x5),
    ORBLIT_BLOCKS(168, SRGB8_ALPHA8_ASTC_8x5),
    ORBLIT_BLOCKS(169, RGBA_ASTC_8x6),
    ORBLIT_BLOCKS(170, SRGB8_ALPHA8_ASTC_8x6),
    ORBLIT_BLOCKS(171, RGBA_ASTC_8x8),
    ORBLIT_BLOCKS(172, SRGB8_ALPHA8_ASTC_8x8),
    ORBLIT_BLOCKS(173, RGBA_ASTC_10x5),
    ORBLIT_BLOCKS(174, SRGB8_ALPHA8_ASTC_10x5),
    ORBLIT_BLOCKS(175, RGBA_ASTC_10x6),
    ORBLIT_BLOCKS(176, SRGB8_ALPHA8_ASTC_10x6),
    ORBLIT_BLOCKS(177, RGBA_ASTC_10x8),
    ORBLIT_BLOCKS(178, SRGB8_ALPHA8_ASTC_10x8),
    ORBLIT_BLOCKS(179, RGBA_ASTC_10x10),
    ORBLIT_BLOCKS(180, SRGB8_ALPHA8_ASTC_10x10),
    ORBLIT_BLOCKS(181, RGBA_ASTC_12x10),
    ORBLIT_BLOCKS(182, SRGB8_ALPHA8_ASTC_12x10),
    ORBLIT_BLOCKS(183, RGBA_ASTC_12x12),
    ORBLIT_BLOCKS(184, SRGB8_ALPHA8_ASTC_12x12),
};

#undef ORBLIT_PIXELS
#undef ORBLIT_BLOCKS

inline const GpuFormat *gpuFormatOf(uint32_t vkFormat) {
  for (const GpuFormat &format : kGpuFormats) {
    if (format.vkFormat == vkFormat) return &format;
  }
  return nullptr;
}

inline const ktx2::Format *formatOfInternal(InternalFormat internal) {
  for (const GpuFormat &format : kGpuFormats) {
    if (format.internal == internal) return ktx2::formatOf(format.vkFormat);
  }
  return nullptr;
}

/// One block — or one texel — of transparent black in `format`, into
/// `block`, which holds sixteen bytes.
///
/// All zeros is transparent black in most formats, and not in three: an ASTC
/// block of zeros is a reserved mode that decodes as the error colour,
/// magenta; a BC7 block needs a mode bit set to be a block at all; and ETC2
/// has no zero, only a base colour plus a modifier, so the most negative
/// modifier is chosen to clamp it there.
inline void placeholderBlock(const ktx2::Format &format, uint8_t block[16]) {
  memset(block, 0, 16);
  const uint32_t vk = format.vkFormat;
  if (vk >= 157 && vk <= 184) {
    // ASTC's void-extent block: one colour, sixteen bits a channel, all
    // nought.
    constexpr uint8_t kVoidExtent[8] = {0xFC, 0xFD, 0xFF, 0xFF,
                                        0xFF, 0xFF, 0xFF, 0xFF};
    memcpy(block, kVoidExtent, sizeof kVoidExtent);
  } else if (vk == 145 || vk == 146) {
    // BC7 mode 6 with every endpoint, p-bit and index nought.
    block[0] = 0x40;
  } else if (vk >= 131 && vk <= 134) {
    // BC1 with both endpoints black and every index three: black, and
    // transparent where the format has alpha.
    memset(block + 4, 0xFF, 4);
  } else if (vk == 147 || vk == 148) {
    // ETC2 individual mode on a black base, every texel at the table's
    // most negative modifier.
    memset(block + 4, 0xFF, 4);
  } else if (vk == 149 || vk == 150) {
    // Punch-through: pixel index two is transparent black.
    block[4] = block[5] = 0xFF;
  } else if (vk == 151 || vk == 152) {
    // Alpha first — an EAC block with a multiplier of nought is its base,
    // nought — then the colour as above.
    memset(block + 12, 0xFF, 4);
  }
}

inline uint32_t mipLevels(uint32_t width, uint32_t height) {
  uint32_t side = std::max(width, height);
  uint32_t levels = 1;
  while (side > 1) {
    side >>= 1;
    levels++;
  }
  return levels;
}

/// Mipmaps made by Filament after the largest level is uploaded, for a
/// format that allows it.
constexpr const char *kKtx2 = "image/ktx2";

/// What decoding on the thread that draws may spend in a frame, in a build
/// with no threads to decode on: a browser without pthreads.
constexpr double kInlineDecodeSeconds = 0.004;

/// What Basis becomes, best first. ASTC is nearly what UASTC already is;
/// BC7 is the desktop's best; ETC2 and BC3 are the floors of GLES and of
/// WebGL; uncompressed is the last resort. The reader takes the first one
/// the device supports whose transfer function matches the request.
constexpr InternalFormat kBasisTargets[] = {
    IF::SRGB8_ALPHA8_ASTC_4x4, IF::RGBA_ASTC_4x4,   IF::SRGB_ALPHA_BPTC_UNORM,
    IF::RGBA_BPTC_UNORM,       IF::ETC2_EAC_SRGBA8, IF::ETC2_EAC_RGBA8,
    IF::DXT5_SRGBA,            IF::DXT5_RGBA,       IF::SRGB8_A8,
    IF::RGBA8};

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
/// How many bytes of decoded answers a frame copies back from the decoder
/// workers into this module's memory, beyond the first answer, which always
/// comes. Copying is the page's part of a worker's decode: about a
/// millisecond for each eight megabytes in Chrome. Twice what the fastest
/// tier uploads in a frame, so the answers keep ahead of the uploads without
/// a frame spending long on copies nothing can upload yet.
constexpr uint64_t kAnswerBytesPerFrame = uint64_t(16) << 20;
#endif

/// Whether a texture whose smaller levels generateMipmaps makes is given a
/// placeholder in its smallest level while it waits.
///
/// Not on Filament's OpenGL backend — in a browser, on desktop OpenGL or on
/// OpenGL ES — because there the placeholder is what breaks it. A
/// placeholder in the smallest level makes Filament sample the texture
/// through a view of that one level, and the OpenGL backend keeps a view as
/// GL_TEXTURE_BASE_LEVEL and GL_TEXTURE_MAX_LEVEL on the one GL texture the
/// view shares, set whenever the view is bound to draw.
/// OpenGLDriver::generateMipmaps binds the texture without putting them back,
/// so glGenerateMipmap, which fills the levels above the base level up to the
/// maximum, fills none: every level but the largest keeps the placeholder's
/// nothing, and a picture drawn at any distance is black. Seen in Chrome:
/// of twelve pictures named at once, only the one whose level arrived before
/// its view was ever drawn had its mipmaps.
///
/// A backend question rather than a platform one, because the code that
/// causes it is the backend's own, in Filament 1.76.0 as in the fork. Whether
/// it shows is up to the driver: macOS's OpenGL fills every level whatever
/// the base and maximum say (`orblit_textures_check mipmaps` draws correctly
/// there with the placeholder and without), and a browser does exactly what
/// the specification says. Android's OpenGL ES drivers are many and were not
/// tried, so every OpenGL backend goes without. WebGL gives every texture's
/// storage zeros when it is
/// made, which for the uncompressed formats this applies to is the same
/// transparent black the placeholder writes. Native drivers promise nothing,
/// but the ones that clear new storage for security are most of them, and a
/// picture that may show last frame's memory for the frames before it
/// arrives is still better than one that is black for good. Metal, Vulkan
/// and WebGPU keep the placeholder until the OpenGL backend resets the
/// levels before it generates.
inline bool placeholderBeforeGeneratedLevels(filament::Engine &engine) {
  return engine.getBackend() != filament::backend::Backend::OPENGL;
}

/// Frees a level's bytes: ours are malloc's, a picture straight from stb is
/// stb's.
inline void freeBytes(void *bytes, size_t, void *fromStb) {
  if (fromStb != nullptr) {
    stbi_image_free(bytes);
  } else {
    free(bytes);
  }
}
}  // namespace textures

/// One upload: a level, a decoded picture, or a transcoded Basis texture.
struct TextureQueue::Unit {
  enum class Kind : uint8_t { level, picture, basis };
  Kind kind = Kind::level;
  uint32_t level = 0;
  uint8_t *bytes = nullptr;
  size_t size = 0;
  /// Whether `bytes` came from stb and goes back to it.
  bool fromStb = false;
  /// What it counts against the frame's budget.
  uint64_t budget = 0;

  Unit() = default;
  Unit(const Unit &) = delete;
  Unit &operator=(const Unit &) = delete;
  Unit(Unit &&other) noexcept { *this = std::move(other); }
  Unit &operator=(Unit &&other) noexcept {
    if (this != &other) {
      drop();
      kind = other.kind;
      level = other.level;
      bytes = other.bytes;
      size = other.size;
      fromStb = other.fromStb;
      budget = other.budget;
      other.bytes = nullptr;
    }
    return *this;
  }
  ~Unit() { drop(); }

  void drop() {
    if (bytes != nullptr) {
      textures::freeBytes(bytes, size, fromStb ? this : nullptr);
    }
    bytes = nullptr;
  }
};

/// One texture on its way.
struct TextureQueue::Item {
  enum class Kind : uint8_t { ktx2, picture, basis };
  Kind kind = Kind::ktx2;
  uint64_t order = 0;
  const void *client = nullptr;
  const void *owner = nullptr;
  std::string name{};
  Texture *texture = nullptr;

  // What the decoder reads. Set when pushed and not changed after.
  SharedBytes source{};
  ktx2::Header header{};
  const textures::GpuFormat *gpu = nullptr;
  uint32_t skip = 0;
  bool srgb = false;
  bool generateMipmaps = false;
  ktxreader::Ktx2Reader::Async *async = nullptr;
  /// Basis in a browser, transcoded by a decode job rather than by `async`:
  /// what it becomes, as basist::transcoder_texture_format numbers it.
  int32_t basisFormat = 0;
  bool basisCompressed = false;

  // A browser's decoder workers, the engine's thread only: the job a worker
  // has, or nought, and whether a worker gave it back to be decoded here.
  int32_t job = 0;
  bool onPage = false;

  // Shared with the decoder, under the queue's lock.
  std::deque<Unit> ready{};
  bool decoding = false;
  bool decoded = false;
  bool abandoned = false;
  std::string failure{};

  // The engine's thread only.
  bool complete = false;
  /// Every level of the texture, in bytes: what the GPU has to find the
  /// first time anything is written into it.
  uint64_t storage = 0;
  /// Whether anything has been written into it yet — its placeholder or a
  /// level.
  bool written = false;
  /// The placeholder it is given before its levels arrive, and into which
  /// level: written when it is made for a texture sampled at once, and for a
  /// model's — hidden until every texture of it has been written — by pump,
  /// under the budget. Null for a texture that has none.
  const ktx2::Format *placeholder = nullptr;
  uint32_t placeholderLevel = 0;
};

}  // namespace orblit
