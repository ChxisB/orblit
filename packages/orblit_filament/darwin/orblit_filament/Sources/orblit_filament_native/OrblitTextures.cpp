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

namespace {

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
const GpuFormat kGpuFormats[] = {
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

const GpuFormat *gpuFormatOf(uint32_t vkFormat) {
  for (const GpuFormat &format : kGpuFormats) {
    if (format.vkFormat == vkFormat) return &format;
  }
  return nullptr;
}

const ktx2::Format *formatOfInternal(InternalFormat internal) {
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
void placeholderBlock(const ktx2::Format &format, uint8_t block[16]) {
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

uint32_t mipLevels(uint32_t width, uint32_t height) {
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
bool placeholderBeforeGeneratedLevels(filament::Engine &engine) {
  return engine.getBackend() != filament::backend::Backend::OPENGL;
}

/// Frees a level's bytes: ours are malloc's, a picture straight from stb is
/// stb's.
void freeBytes(void *bytes, size_t, void *fromStb) {
  if (fromStb != nullptr) {
    stbi_image_free(bytes);
  } else {
    free(bytes);
  }
}

}  // namespace

// ---- Defaults from the device ----

DeviceTier deviceTier(int32_t featureLevel, int32_t maxTextureSize,
                      int32_t workerThreads, int32_t memoryMegabytes) {
  // Unknown answers as OrblitDeviceProfile.fromCapabilities reads them: the
  // least a device this engine runs on has, and memory not vouched for.
  const int32_t level = featureLevel < 0 ? 1 : featureLevel;
  const int32_t largest = maxTextureSize < 0 ? 2048 : maxTextureSize;
  const int32_t threads = std::max(1, workerThreads);
  const bool knownMemory = memoryMegabytes > 0;
  if (level < kLowTierBelowFeatureLevel || largest < kLowTierBelowTextureSize ||
      threads <= kLowTierAtMostThreads ||
      (knownMemory && memoryMegabytes < kLowTierBelowMegabytes)) {
    return DeviceTier::low;
  }
  if (largest >= kHighTierTextureSize && threads >= kHighTierThreads &&
      knownMemory && memoryMegabytes >= kHighTierMegabytes) {
    return DeviceTier::high;
  }
  return DeviceTier::medium;
}

// ---- The queue ----

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
    if (bytes != nullptr) freeBytes(bytes, size, fromStb ? this : nullptr);
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
  const GpuFormat *gpu = nullptr;
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

TextureQueue::TextureQueue(filament::Engine &engine, uint32_t deviceLargest,
                           uint32_t workerThreads)
    : _engine(engine),
      _deviceLargest(deviceLargest == 0 ? 2048 : deviceLargest),
      _basis(std::make_unique<ktxreader::Ktx2Reader>(engine, true)) {
  for (const GpuFormat &format : kGpuFormats) {
    if (Texture::isTextureFormatSupported(engine, format.internal)) {
      _supported.push_back(format.vkFormat);
    }
  }
  const auto any = [this](std::initializer_list<uint32_t> formats) {
    for (uint32_t vk : formats) {
      if (const ktx2::Format *format = ktx2::formatOf(vk)) {
        if (supports(*format)) return true;
      }
    }
    return false;
  };
  const auto both = [this](uint32_t linear, uint32_t srgb) {
    const ktx2::Format *a = ktx2::formatOf(linear);
    const ktx2::Format *b = ktx2::formatOf(srgb);
    return a != nullptr && b != nullptr && supports(*a) && supports(*b);
  };
  // A family only when its colour formats are sampled in both colour spaces,
  // as ORBLIT_CAPABILITY_COMPRESSED_FORMATS and so OrblitDeviceProfile's
  // textureCandidates count it: a cooked set is chosen by family, and a
  // family that can hold a normal map but not the albedo beside it is only
  // half of one. It is also what keeps the siblings a device will not use
  // from being opened. Filament's Metal backend samples ASTC only as linear,
  // so on Apple the ASTC siblings are passed over without being read.
  _familyUsable[size_t(ktx2::Family::astc)] = both(157, 158);
  _familyUsable[size_t(ktx2::Family::bc)] =
      both(145, 146) || any({141, 139});
  _familyUsable[size_t(ktx2::Family::etc2)] = both(151, 152);

  for (InternalFormat target : kBasisTargets) {
    // Not ASTC where the device has it only in one colour space: Basis would
    // make its linear maps ASTC and its colour maps something else, and ASTC
    // is the one family whose never-written storage does not read as
    // transparent black (see startsAsPlaceholder).
    const ktx2::Format *format = formatOfInternal(target);
    if (format != nullptr && format->family == ktx2::Family::astc &&
        !_familyUsable[size_t(ktx2::Family::astc)]) {
      continue;
    }
    _basis->requestFormat(target);
  }

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  (void)workerThreads;
  _noDecoderThreads = true;
#else
  // Every core but two, the drawing thread's and the backend's, and the
  // decoders below both in priority. Decoding is what a Basis load waits on:
  // the Bistro's 405 transcodes arrived in 6.1 to 7.7 s on two threads, 3.2
  // to 3.8 on four and 2.1 to 2.5 on ten, on an M4 Pro, and the longest frame
  // meanwhile was no worse on ten than on two (orblit_load_bench).
  _workerCount = std::clamp<uint32_t>(workerThreads > 2 ? workerThreads - 2 : 1,
                                      1, 12);
#endif
}

TextureQueue::~TextureQueue() { shutdown(); }

void TextureQueue::setLimits(uint32_t maxSide, uint64_t bytesPerFrame) {
  _maxSide = maxSide;
  _bytesPerFrame = bytesPerFrame;
  _adapting.on = false;
}

void TextureQueue::adaptUploads(uint64_t start, uint64_t least,
                                uint64_t most) {
  if (_adapting.on && _adapting.start == start && _adapting.least == least &&
      _adapting.most == most) {
    return;
  }
  _adapting = Adapting{};
  _adapting.on = true;
  _adapting.start = start;
  _adapting.least = least;
  _adapting.most = most;
  _bytesPerFrame = std::clamp(start, least, most);
}

void TextureQueue::frameTook(double seconds) {
  Adapting &a = _adapting;
  if (!a.on) return;
  const bool probed = a.lastProbed;
  a.lastProbed = false;
  const double ms = seconds * 1000.0;
  const uint64_t bytes = _frames.lastBytes;

  constexpr double kFrameMs = 1000.0 / 60.0;
  if (!probed) {
    if (bytes > 0) {
      a.uploadMs += ms;
      a.uploadMegabytes += double(bytes) / double(1 << 20);
      a.uploadFrames++;
      // A frame far over what the last probe allows halves the budget at
      // once rather than waiting for the next probe to say so.
      if (a.probeMs > 0) {
        const double slack =
            std::max({a.probeMs, kFrameMs - a.probeMs, 4.0});
        if (ms > a.probeMs + 4 * slack) {
          _bytesPerFrame = std::max(a.least, _bytesPerFrame / 2);
        }
      }
    }
    return;
  }

  // The scene's own cost, for the frames since the last probe: the two
  // probes either side of them, so a scene that changed meanwhile — a model
  // appearing — is not blamed on the textures. A probe four times the last is
  // a stall from somewhere else and is not believed.
  const double previous = a.probeMs;
  if (previous > 0 && ms > previous * 4) {
    a.uploadMs = 0;
    a.uploadMegabytes = 0;
    a.uploadFrames = 0;
    return;
  }
  a.probeMs = ms;
  const double scene = previous > 0 ? (previous + ms) / 2 : ms;
  // What a frame may spend on textures: what is left of a sixtieth of a
  // second, or as long as the scene takes, whichever is more.
  const double slack = std::max({scene, kFrameMs - scene, 4.0});
  const uint64_t budget = _bytesPerFrame;
  if (previous <= 0 || a.uploadFrames == 0 || a.uploadMegabytes <= 0) {
    a.uploadMs = 0;
    a.uploadMegabytes = 0;
    a.uploadFrames = 0;
    return;
  }

  const double extraMs =
      std::max(0.0, a.uploadMs / a.uploadFrames - scene);
  const double megabytes = a.uploadMegabytes / a.uploadFrames;
  // Megabytes that cost nothing measurable are allowed twice as many.
  const double fits = extraMs > 0.01
                          ? slack / (extraMs / megabytes) * double(1 << 20)
                          : double(budget) * 2;
  const double next =
      std::clamp(fits, double(budget) / 2, double(budget) * 2);
  _bytesPerFrame =
      std::clamp(uint64_t(next), a.least, a.most);
  if (_trace) {
    log("[orblit] trace: upload budget %.1f MB: a frame without uploads "
        "%.1f ms, with %.1f MB %.1f ms",
        double(_bytesPerFrame) / double(1 << 20), scene, megabytes,
        a.uploadMs / a.uploadFrames);
  }
  a.uploadMs = 0;
  a.uploadMegabytes = 0;
  a.uploadFrames = 0;
}

bool TextureQueue::supports(const ktx2::Format &format) const {
  return std::find(_supported.begin(), _supported.end(), format.vkFormat) !=
         _supported.end();
}

const ktx2::Format *TextureQueue::sampledAs(const ktx2::Format &format,
                                            int transfer, bool strict) const {
  const ktx2::Format *twin =
      transfer < 0 ? &format : ktx2::withTransfer(format, transfer == 1);
  const ktx2::Format *order[2] = {twin, nullptr};
  if (twin == nullptr || (!strict && twin != &format)) {
    order[twin == nullptr ? 0 : 1] = &format;
  }
  for (const ktx2::Format *candidate : order) {
    if (candidate == nullptr) continue;
    if (supports(*candidate)) return candidate;
    // BC1's two forms differ only in what index three of a three-colour
    // block means — black, or transparent black — so where a device has only
    // the form with alpha, it stands in.
    if (candidate->vkFormat == 131 || candidate->vkFormat == 132) {
      const ktx2::Format *withAlpha = ktx2::formatOf(candidate->vkFormat + 2);
      if (withAlpha != nullptr && supports(*withAlpha)) return withAlpha;
    }
  }
  return nullptr;
}

SharedBytes TextureQueue::readCooked(const std::string &path,
                                     std::string *chosen, int transfer) {
  if (chosen != nullptr) *chosen = path;
  if (!ktx2::namesCookedSet(path)) return readResource(path);

  const uint64_t generation = resourceGeneration();
  const std::string key = path + (transfer < 0    ? "|?"
                                  : transfer == 0 ? "|l"
                                                  : "|s");
  std::string remembered;
  {
    std::lock_guard<std::mutex> hold(_cookedLock);
    const auto found = _cooked.find(key);
    if (found != _cooked.end() && found->second.generation == generation) {
      remembered = found->second.name;
    }
  }
  if (!remembered.empty()) {
    if (SharedBytes bytes = readResource(remembered)) {
      if (chosen != nullptr) *chosen = remembered;
      return bytes;
    }
  }

  const auto remember = [&](const std::string &name) {
    std::lock_guard<std::mutex> hold(_cookedLock);
    _cooked[key] = {generation, name};
    if (chosen != nullptr) *chosen = name;
  };

  // Best first: ASTC is the better format wherever both are sampled, BC the
  // desktop's own, ETC2 the floor every GLES 3.0 device has.
  for (ktx2::Family family :
       {ktx2::Family::astc, ktx2::Family::bc, ktx2::Family::etc2}) {
    // A family the device cannot sample at all is not worth reading a file
    // for. One it can is still checked by what the file actually holds.
    if (!_familyUsable[size_t(family)]) continue;
    const std::string name = ktx2::siblingName(path, family);

    // Chosen by its header, read on its own: a sibling passed over costs a
    // few kilobytes rather than every level of a texture. Bytes provided by
    // name are already in memory and are looked at where they are.
    ktx2::Header header;
    std::string why;
    SharedBytes provided = findResource(name);
    if (provided) {
      if (provided->empty()) continue;
      why = ktx2::read(provided->data(), provided->size(), header);
    } else {
      std::vector<uint8_t> start;
      uint64_t size = 0;
      if (!readFileStart(name, ktx2::kHeadBytes, start, &size)) continue;
      why = ktx2::read(start.data(), start.size(), header, size);
    }
    if (why.empty() && (header.basis || header.format == nullptr)) {
      why = "it is Basis, which belongs in " + lastPathComponent(path);
    }
    if (why.empty() && sampledAs(*header.format, transfer, true) == nullptr) {
      const ktx2::Format *wanted =
          transfer < 0 ? header.format
                       : ktx2::withTransfer(*header.format, transfer == 1);
      why = std::string("this device does not sample ") +
            (wanted != nullptr ? wanted : header.format)->name;
    }
    if (!why.empty()) {
      passOver(name, why);
      continue;
    }
    SharedBytes bytes = provided ? provided : readResource(name);
    if (!bytes || bytes->empty()) continue;
    if (!provided) {
      // Read whole, so checked whole: a file that has changed or been cut
      // short since its start was read falls through as it would have.
      why = ktx2::read(bytes->data(), bytes->size(), header);
      if (!why.empty()) {
        passOver(name, why);
        continue;
      }
    }
    remember(name);
    return bytes;
  }
  remember(path);
  return readResource(path);
}

void TextureQueue::passOver(const std::string &name, const std::string &why) {
  std::lock_guard<std::mutex> hold(_cookedLock);
  PassedOver &passed = _passedOver[why];
  if (passed.count++ == 0) passed.example = name;
}

std::vector<std::string> TextureQueue::takePassedOver() {
  std::map<std::string, PassedOver> taken;
  {
    std::lock_guard<std::mutex> hold(_cookedLock);
    taken.swap(_passedOver);
  }
  std::vector<std::string> lines;
  for (const auto &entry : taken) {
    lines.push_back(
        entry.second.count == 1
            ? format("%s passed over: %s", entry.second.example.c_str(),
                     entry.first.c_str())
            : format("%zu cooked siblings passed over, %s among them: %s",
                     entry.second.count, entry.second.example.c_str(),
                     entry.first.c_str()));
  }
  return lines;
}

Texture *TextureQueue::push(const Request &request, std::string &why) {
  why.clear();
  const uint8_t *data =
      request.shared ? request.shared->data() : request.data;
  const size_t size = request.shared ? request.shared->size() : request.size;
  if (data == nullptr || size == 0) {
    why = "It is empty.";
    return nullptr;
  }
  if (_stopping) {
    why = "The renderer is shutting down.";
    return nullptr;
  }

  const double started = now();
  Texture *texture = nullptr;
  if (request.mime == kKtx2 || ktx2::isKtx2(data, size)) {
    texture = pushKtx2(request, data, size, why);
  } else if (request.mime.empty() || request.mime == "image/png" ||
             request.mime == "image/jpeg") {
    texture = pushPicture(request, data, size, why);
  } else {
    why = format("%s is not an image type this renderer reads.",
                 request.mime.c_str());
  }
  if (texture != nullptr) {
    std::lock_guard<std::mutex> hold(_lock);
    _counts[request.client].pushed++;
  }
  _pushSeconds += now() - started;
  _pushSecondsEver += now() - started;
  return texture;
}

Texture *TextureQueue::pushKtx2(const Request &request, const uint8_t *data,
                                size_t size, std::string &why) {
  ktx2::Header header;
  why = ktx2::read(data, size, header);
  if (!why.empty()) return nullptr;
  if (header.basis) return pushBasis(request, data, size, header, why);

  if (header.faces != 1) {
    why = "It is a cubemap, and this is loading a 2D texture.";
    return nullptr;
  }

  // Sampled as the material asks: the sRGB and linear forms of a block
  // format are the same bytes, so a map cooked one way is read the other at
  // no cost. Where the format has no twin, or the device lacks it, the file's
  // own is used.
  const ktx2::Format *format =
      sampledAs(*header.format, request.srgb ? 1 : 0, false);
  if (format == nullptr) {
    why = orblit::format("This device cannot sample %s.", header.format->name);
    return nullptr;
  }
  if (format->srgb != request.srgb &&
      ktx2::withTransfer(*format, request.srgb) != nullptr) {
    // Drawn, but not in the colour space it is used as. Said rather than
    // refused: a texture a little too light or dark is easier to find than
    // one that is missing.
    log("[orblit] %s is sampled as %s: this device has no %s",
        request.name.c_str(), format->name,
        ktx2::withTransfer(*format, request.srgb)->name);
  }
  const GpuFormat *gpu = gpuFormatOf(format->vkFormat);
  if (gpu == nullptr) {
    why = orblit::format("%s has no Filament format.", format->name);
    return nullptr;
  }

  const uint32_t skip = ktx2::levelsToSkip(header, _maxSide);
  const uint32_t width = ktx2::levelWidth(header, skip);
  const uint32_t height = ktx2::levelHeight(header, skip);
  if (std::max(width, height) > _deviceLargest) {
    why = orblit::format("It is %u by %u, larger than this device's largest "
                         "texture, %u, and has no smaller level to use "
                         "instead.",
                         width, height, _deviceLargest);
    return nullptr;
  }

  // A header of nought levels asks for mipmaps to be made, which Filament
  // can do for an uncompressed format and nothing else.
  const bool generate =
      header.generateMipmaps && !gpu->compressed &&
      Texture::isTextureFormatMipmappable(_engine, gpu->internal);
  const uint32_t levels =
      generate ? mipLevels(width, height) : header.levels - skip;

  Texture::Usage usage = Texture::Usage::DEFAULT;
  if (generate) usage = usage | Texture::Usage::GEN_MIPMAPPABLE;
  Texture *texture = Texture::Builder()
                         .width(width)
                         .height(height)
                         .levels(uint8_t(levels))
                         .format(gpu->internal)
                         .sampler(Texture::Sampler::SAMPLER_2D)
                         .usage(usage)
                         .build(_engine);
  if (texture == nullptr) {
    why = "Filament would not make a texture of it.";
    return nullptr;
  }
  auto item = std::make_shared<Item>();
  item->storage = storageOf(*texture, *format);
  if (!generate || placeholderBeforeGeneratedLevels(_engine)) {
    item->placeholder = format;
    item->placeholderLevel = levels - 1;
  }
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::ktx2;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->source = request.shared
                     ? request.shared
                     : std::make_shared<const std::vector<uint8_t>>(
                           data, data + size);
  item->header = std::move(header);
  item->gpu = gpu;
  item->skip = skip;
  item->generateMipmaps = generate;
  enqueue(item);
  return texture;
}

Texture *TextureQueue::pushBasis(const Request &request, const uint8_t *data,
                                 size_t size, const ktx2::Header &header,
                                 std::string &why) {
  // A low tier leaves out Basis levels too, by handing the transcoder a copy
  // of the file that starts lower down.
  std::vector<uint8_t> smaller;
  const uint32_t skip = ktx2::levelsToSkip(header, _maxSide);
  if (skip > 0) {
    why = ktx2::withoutLargestLevels(data, size, header, skip, smaller);
    if (!why.empty()) return nullptr;
    data = smaller.data();
    size = smaller.size();
  }
  const uint32_t width = ktx2::levelWidth(header, skip);
  const uint32_t height = ktx2::levelHeight(header, skip);
  if (std::max(width, height) > _deviceLargest) {
    why = orblit::format("It is %u by %u, larger than this device's largest "
                         "texture, %u.",
                         width, height, _deviceLargest);
    return nullptr;
  }

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  // In a browser the file goes to a decoder worker, so it is not copied and
  // started here as asyncCreate would: the target is chosen from the header
  // exactly as Ktx2Reader chooses it, and the texture made as it makes it.
  (void)header;
  std::vector<web::BasisCandidate> candidates;
  for (InternalFormat target : kBasisTargets) {
    if (const ktx2::Format *format = formatOfInternal(target)) {
      candidates.push_back(
          {format->vkFormat, Texture::isTextureFormatSupported(_engine, target)});
    }
  }
  web::BasisTarget target;
  why = web::chooseBasisTarget(data, size, request.srgb, candidates.data(),
                               candidates.size(), target);
  if (!why.empty()) return nullptr;
  const GpuFormat *gpu = gpuFormatOf(target.vkFormat);
  if (gpu == nullptr || target.levels == 0) {
    why = "Filament's Basis reader would not take it: it may be a cubemap or "
          "an array, or transcode to nothing this device samples.";
    return nullptr;
  }
  Texture *texture = Texture::Builder()
                         .width(target.width)
                         .height(target.height)
                         .levels(uint8_t(target.levels))
                         .sampler(Texture::Sampler::SAMPLER_2D)
                         .format(gpu->internal)
                         .build(_engine);
  if (texture == nullptr) {
    why = "Filament would not make a texture of it.";
    return nullptr;
  }
  auto item = std::make_shared<Item>();
  item->storage = storageOf(*texture, *ktx2::formatOf(target.vkFormat));
  item->placeholder = ktx2::formatOf(target.vkFormat);
  item->placeholderLevel = target.levels - 1;
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::basis;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->gpu = gpu;
  item->basisFormat = target.transcoderFormat;
  item->basisCompressed = target.compressed;
  if (!smaller.empty()) {
    item->source = std::make_shared<const std::vector<uint8_t>>(std::move(smaller));
  } else if (request.shared) {
    item->source = request.shared;
  } else {
    item->source = std::make_shared<const std::vector<uint8_t>>(data, data + size);
  }
  enqueue(item);
  return texture;
#else
  using Transfer = ktxreader::Ktx2Reader::TransferFunction;
  ktxreader::Ktx2Reader::Async *async = _basis->asyncCreate(
      data, size, request.srgb ? Transfer::sRGB : Transfer::LINEAR);
  if (async == nullptr) {
    const bool marked = header.transfer != ktx2::Transfer::unspecified;
    const bool mismatch =
        marked && (header.transfer == ktx2::Transfer::srgb) != request.srgb;
    why = mismatch
              ? orblit::format("It is Basis marked %s, and is used where %s "
                               "is needed; Basis cannot be read the other "
                               "way.",
                               request.srgb ? "linear" : "sRGB",
                               request.srgb ? "sRGB" : "linear")
              : "Filament's Basis reader would not take it: it may be a "
                "cubemap or an array, or transcode to nothing this device "
                "samples.";
    return nullptr;
  }
  Texture *texture = async->getTexture();
  auto item = std::make_shared<Item>();
  if (const ktx2::Format *format = formatOfInternal(texture->getFormat())) {
    item->storage = storageOf(*texture, *format);
    item->placeholder = format;
    item->placeholderLevel = uint32_t(texture->getLevels()) - 1;
  }
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::basis;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->async = async;
  enqueue(item);
  return texture;
#endif
}

Texture *TextureQueue::pushPicture(const Request &request, const uint8_t *data,
                                   size_t size, std::string &why) {
  int wide = 0;
  int tall = 0;
  int channels = 0;
  if (size > size_t(INT_MAX) ||
      !stbi_info_from_memory(data, int(size), &wide, &tall, &channels) ||
      wide <= 0 || tall <= 0) {
    why = "It is not a PNG or JPEG that can be read.";
    return nullptr;
  }

  // Pictures carry no mipmaps to leave out, so a picture larger than the
  // limit is halved on the decoder's thread until it fits.
  uint32_t width = uint32_t(wide);
  uint32_t height = uint32_t(tall);
  uint32_t skip = 0;
  while (_maxSide != 0 && std::max(width, height) > _maxSide &&
         (width > 1 || height > 1)) {
    width = std::max<uint32_t>(1, width / 2);
    height = std::max<uint32_t>(1, height / 2);
    skip++;
  }
  if (std::max(width, height) > _deviceLargest) {
    why = orblit::format("It is %u by %u, larger than this device's largest "
                         "texture, %u.",
                         width, height, _deviceLargest);
    return nullptr;
  }

  const InternalFormat internal = request.srgb ? IF::SRGB8_A8 : IF::RGBA8;
  const uint32_t levels = mipLevels(width, height);
  Texture *texture =
      Texture::Builder()
          .width(width)
          .height(height)
          .levels(uint8_t(levels))
          .format(internal)
          .sampler(Texture::Sampler::SAMPLER_2D)
          .usage(Texture::Usage::DEFAULT | Texture::Usage::GEN_MIPMAPPABLE)
          .build(_engine);
  if (texture == nullptr) {
    why = "Filament would not make a texture of it.";
    return nullptr;
  }
  const ktx2::Format &pixels = *ktx2::formatOf(request.srgb ? 43 : 37);
  auto item = std::make_shared<Item>();
  item->storage = storageOf(*texture, pixels);
  if (placeholderBeforeGeneratedLevels(_engine)) {
    item->placeholder = &pixels;
    item->placeholderLevel = levels - 1;
  }
  placeAtPush(request, *item, texture);
  item->kind = Item::Kind::picture;
  item->client = request.client;
  item->owner = request.owner;
  item->name = request.name;
  item->texture = texture;
  item->source = request.shared
                     ? request.shared
                     : std::make_shared<const std::vector<uint8_t>>(
                           data, data + size);
  item->skip = skip;
  item->srgb = request.srgb;
  enqueue(item);
  return texture;
}

bool TextureQueue::startsAsPlaceholder(const ktx2::Format &format) const {
  // Measured, per format, on Apple silicon through Filament's Metal backend:
  // a texture of these that nothing has been written into samples as the
  // placeholder does, even straight after other textures' memory has been
  // freed (orblit_textures_check, "never-written storage"). Metal gives a
  // texture zeros, and zeros in these formats decode to black with no
  // alpha — within two levels of it for ETC2 without alpha, whose smallest
  // modifier is two. Not ASTC, where zeros are a reserved block that decodes
  // as the error colour, and not the uncompressed formats, which sample as
  // magenta until written. Other backends are not measured, so they keep the
  // placeholder.
  if (_engine.getBackend() != filament::backend::Backend::METAL) return false;
  const uint32_t vk = format.vkFormat;
  return (vk >= 131 && vk <= 134) ||  // BC1
         (vk >= 137 && vk <= 142) ||  // BC3, BC4, BC5
         vk == 145 || vk == 146 ||    // BC7
         (vk >= 147 && vk <= 156);    // ETC2 and EAC
}

uint64_t TextureQueue::storageOf(const Texture &texture,
                                 const ktx2::Format &format) {
  uint64_t bytes = 0;
  for (size_t level = 0; level < texture.getLevels(); level++) {
    bytes += uint64_t((texture.getWidth(level) + format.blockWidth - 1) /
                      format.blockWidth) *
             ((texture.getHeight(level) + format.blockHeight - 1) /
              format.blockHeight) *
             format.bytesPerBlock;
  }
  return bytes;
}

void TextureQueue::placeAtPush(const Request &request, Item &item,
                               Texture *texture) {
  item.texture = texture;
  // A model's textures are hidden with the model until pump has written into
  // every one of them (see unprimed), so their placeholders wait for it: a
  // write makes the GPU find memory for the whole texture, and four hundred of
  // them at once is a frame of a second.
  if (request.owner != nullptr || item.placeholder == nullptr) return;
  // Sampled from the next frame. Where the storage already reads as the
  // placeholder nothing need be written, and the GPU finds the memory when it
  // is first drawn or written.
  if (startsAsPlaceholder(*item.placeholder)) return;
  item.written = writePlaceholder(texture, *item.placeholder,
                                  item.placeholderLevel, true);
  if (item.written) _placeholderBytes += item.storage;
}

bool TextureQueue::writePlaceholder(Texture *texture,
                                    const ktx2::Format &format,
                                    uint32_t level, bool whole) {
  const GpuFormat *gpu = gpuFormatOf(format.vkFormat);
  if (gpu == nullptr) return false;
  const uint32_t levelWidth = uint32_t(texture->getWidth(level));
  const uint32_t levelHeight = uint32_t(texture->getHeight(level));
  // One block is enough where the rest of the storage already reads as the
  // placeholder: what it is written for is making the memory.
  const uint32_t width =
      whole ? levelWidth : std::min<uint32_t>(levelWidth, format.blockWidth);
  const uint32_t height =
      whole ? levelHeight : std::min<uint32_t>(levelHeight, format.blockHeight);
  const size_t across = (width + format.blockWidth - 1) / format.blockWidth;
  const size_t down = (height + format.blockHeight - 1) / format.blockHeight;
  const size_t bytes = across * down * format.bytesPerBlock;

  std::shared_ptr<std::vector<uint8_t>> &kept =
      _placeholders[{format.vkFormat, bytes}];
  if (!kept) {
    uint8_t block[16];
    placeholderBlock(format, block);
    kept = std::make_shared<std::vector<uint8_t>>(bytes);
    for (size_t at = 0; at + format.bytesPerBlock <= bytes;
         at += format.bytesPerBlock) {
      memcpy(kept->data() + at, block, format.bytesPerBlock);
    }
  }

  // The buffer is shared, so what Filament is given to let go of is a
  // reference to it rather than the bytes.
  auto *holder = new std::shared_ptr<std::vector<uint8_t>>(kept);
  const auto letGo = [](void *, size_t, void *user) {
    delete static_cast<std::shared_ptr<std::vector<uint8_t>> *>(user);
  };
  if (gpu->compressed) {
    texture->setImage(_engine, level, 0, 0, width, height,
                      Texture::PixelBufferDescriptor(
                          kept->data(), bytes, gpu->compressedType,
                          uint32_t(bytes), letGo, holder));
  } else {
    texture->setImage(_engine, level, 0, 0, width, height,
                      Texture::PixelBufferDescriptor(
                          kept->data(), bytes, gpu->pixelFormat,
                          gpu->pixelType, letGo, holder));
  }
  return true;
}

void TextureQueue::enqueue(const std::shared_ptr<Item> &item) {
  {
    std::lock_guard<std::mutex> hold(_lock);
    item->order = _order++;
    if (_items.empty()) {
      _batchFrom = now();
      _batchCount = 0;
    }
    _batchCount++;
    _items.push_back(item);
    _waiting.push_back(item);
  }
  if (!_noDecoderThreads) {
    startWorkers();
    _wake.notify_one();
  }
}

void TextureQueue::startWorkers() {
  if (!_workers.empty() || _noDecoderThreads) return;
  try {
    for (uint32_t i = 0; i < _workerCount; i++) {
      _workers.emplace_back([this] { work(); });
    }
  } catch (const std::exception &) {
    // A thread that will not start is not a reason to stop loading
    // textures: whatever did start keeps working, and with none the drawing
    // thread decodes a little each frame, as a browser does.
    if (_workers.empty()) _noDecoderThreads = true;
  }
}

void TextureQueue::work() {
#if defined(__APPLE__)
  // Below the thread that draws, which a host runs at user-interactive.
  pthread_set_qos_class_self_np(QOS_CLASS_USER_INITIATED, 0);
#endif
  for (;;) {
    std::shared_ptr<Item> item;
    {
      std::unique_lock<std::mutex> hold(_lock);
      _wake.wait(hold, [this] { return _stopping || !_waiting.empty(); });
      if (_stopping) return;
      item = std::move(_waiting.front());
      _waiting.pop_front();
      if (item->abandoned) {
        item->decoded = true;
        _idle.notify_all();
        continue;
      }
      item->decoding = true;
    }
    decode(*item);
    {
      std::lock_guard<std::mutex> hold(_lock);
      item->decoding = false;
      item->decoded = true;
    }
    _idle.notify_all();
  }
}

void TextureQueue::decodeInline() {
  const double from = now();
  do {
    std::shared_ptr<Item> item;
    {
      std::lock_guard<std::mutex> hold(_lock);
      while (!_waiting.empty() && _waiting.front()->abandoned) {
        _waiting.front()->decoded = true;
        _waiting.pop_front();
      }
      if (_waiting.empty()) return;
      auto next = _waiting.begin();
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
      // What a decoder worker will take is left for one. Decoded here: what
      // a worker gave back, and everything while no worker is to be used.
      if (web::decoders::capacity() >= 0) {
        next = std::find_if(_waiting.begin(), _waiting.end(),
                            [](const std::shared_ptr<Item> &waiting) {
                              return waiting->onPage && !waiting->abandoned;
                            });
        if (next == _waiting.end()) return;
      }
#endif
      item = std::move(*next);
      _waiting.erase(next);
      item->decoding = true;
    }
    const double started = now();
    decode(*item);
    // What decoding cost the thread that draws, said with the batch: the
    // number a worker is there to bring down.
    const double took = now() - started;
    _inlineSeconds += took;
    _longestInline = std::max(_longestInline, took);
    _inlineCount++;
    std::lock_guard<std::mutex> hold(_lock);
    item->decoding = false;
    item->decoded = true;
  } while (now() - from < kInlineDecodeSeconds);
}

bool TextureQueue::publish(Item &item, Unit &&unit) {
  std::lock_guard<std::mutex> hold(_lock);
  if (item.abandoned) return false;
  item.ready.push_back(std::move(unit));
  return true;
}

void TextureQueue::decode(Item &item) {
  const auto fail = [&](std::string why) {
    std::lock_guard<std::mutex> hold(_lock);
    item.failure = std::move(why);
  };

  switch (item.kind) {
    case Item::Kind::ktx2: {
      const ktx2::Header &header = item.header;
      const uint8_t *data = item.source->data();
      const size_t size = item.source->size();
      // Smallest first, each handed over as soon as it is ready, so the
      // upload that follows can start on the small levels while the large
      // ones are still being decompressed — and so the range of levels
      // Filament samples only ever grows downwards from the smallest.
      for (uint32_t level = header.levels; level-- > item.skip;) {
        const uint64_t bytes = ktx2::levelBytes(header, level);
        auto *out = static_cast<uint8_t *>(malloc(size_t(bytes)));
        if (out == nullptr) {
          fail(format("There was no memory for level %u.", level));
          return;
        }
        Unit unit;
        unit.kind = item.generateMipmaps ? Unit::Kind::picture
                                         : Unit::Kind::level;
        unit.level = level - item.skip;
        unit.bytes = out;
        unit.size = size_t(bytes);
        unit.budget = bytes;
        std::string why =
            ktx2::readLevel(data, size, header, level, out, size_t(bytes));
        if (!why.empty()) {
          fail(std::move(why));
          return;
        }
        if (!publish(item, std::move(unit))) return;
      }
      // Nothing more needs the file.
      std::lock_guard<std::mutex> hold(_lock);
      item.source.reset();
      return;
    }

    case Item::Kind::picture: {
      const SharedBytes &source = item.source;
      DecodedPicture decoded;
      std::string why = decodePicture(source->data(), source->size(),
                                      item.skip, item.srgb, decoded);
      if (!why.empty()) {
        fail(std::move(why));
        return;
      }
      Unit unit;
      unit.kind = Unit::Kind::picture;
      unit.level = 0;
      unit.bytes = decoded.pixels;
      unit.fromStb = decoded.fromStb;
      unit.size = decoded.size;
      unit.budget = unit.size;
      publish(item, std::move(unit));
      std::lock_guard<std::mutex> hold(_lock);
      item.source.reset();
      return;
    }

    case Item::Kind::basis: {
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
      // The decoder workers' own job, run here, so what the page draws when
      // no worker answers is what a worker would have drawn.
      web::DecodeAnswer answer;
      const double parameters[] = {double(item.basisFormat),
                                   item.basisCompressed ? 1.0 : 0.0};
      web::runDecodeJob(web::DecodeJob::basis, item.source->data(),
                        item.source->size(), parameters, 2, item.name, answer);
      publishAnswer(item, answer);
      return;
#else
      using Result = ktxreader::Ktx2Reader::Result;
      if (item.async->doTranscoding() != Result::SUCCESS) {
        fail("Its Basis data could not be transcoded.");
        return;
      }
      // Counted as every level of the texture it fills, which is what
      // uploadImages hands over in one go.
      Unit unit;
      unit.kind = Unit::Kind::basis;
      const Texture *texture = item.texture;
      if (const ktx2::Format *format = formatOfInternal(texture->getFormat())) {
        for (size_t level = 0; level < texture->getLevels(); level++) {
          const uint64_t across =
              (texture->getWidth(level) + format->blockWidth - 1) /
              format->blockWidth;
          const uint64_t down =
              (texture->getHeight(level) + format->blockHeight - 1) /
              format->blockHeight;
          unit.budget += across * down * format->bytesPerBlock;
        }
      }
      publish(item, std::move(unit));
      return;
#endif
    }
  }
}

void TextureQueue::upload(Item &item, Unit &unit) {
  Texture *texture = item.texture;
  if (unit.kind == Unit::Kind::basis) {
    item.async->uploadImages();
    return;
  }
  const GpuFormat *gpu =
      item.kind == Item::Kind::picture
          ? gpuFormatOf(item.srgb ? 43 : 37)
          : item.gpu;
  const void *fromStb = unit.fromStb ? &unit : nullptr;
  uint8_t *bytes = unit.bytes;
  unit.bytes = nullptr;
  if (gpu->compressed) {
    texture->setImage(
        _engine, unit.level,
        Texture::PixelBufferDescriptor(bytes, unit.size, gpu->compressedType,
                                       uint32_t(unit.size), freeBytes,
                                       const_cast<void *>(fromStb)));
  } else {
    texture->setImage(
        _engine, unit.level,
        Texture::PixelBufferDescriptor(bytes, unit.size, gpu->pixelFormat,
                                       gpu->pixelType, freeBytes,
                                       const_cast<void *>(fromStb)));
  }
  // The largest level of a picture — or of a KTX 2 file that asked for them —
  // with every other made from it, in the same frame, so the range Filament
  // samples is never wider than what has been written.
  if (unit.kind == Unit::Kind::picture && texture->getLevels() > 1) {
    texture->generateMipmaps(_engine);
  }
}

void TextureQueue::release(Item &item) {
  if (item.async != nullptr) {
    _basis->asyncDestroy(&item.async);
    item.async = nullptr;
  }
}

void TextureQueue::pump() {
  if (_batchCount > 0) {
    const double at = now();
    if (_lastPumpAt > 0) _longestFrame = std::max(_longestFrame, at - _lastPumpAt);
    _lastPumpAt = at;
  }
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  if (_noDecoderThreads) decodeOnWorkers();
#endif
  if (_noDecoderThreads) decodeInline();

  // Whatever placeholders push wrote since the last pump count against this
  // frame: they went to the GPU in it.
  uint64_t spent = _placeholderBytes;
  _placeholderBytes = 0;

  std::vector<std::shared_ptr<Item>> live;
  {
    std::lock_guard<std::mutex> hold(_lock);
    if (_items.empty()) {
      _frames.lastBytes = spent;
      _frames.lastUploads = 0;
      return;
    }
    live = _items;
  }

  const uint64_t budget =
      _bytesPerFrame == 0 ? UINT64_MAX : _bytesPerFrame;
  // One frame in eight uploads nothing while the budget adapts, so what the
  // scene costs without it can be measured beside what it costs with it.
  // The one frame of the eight that breaks "at least one upload a frame".
  const bool probing = _adapting.on && (++_adapting.pumps % 8) == 0;
  _adapting.lastProbed = probing;
  uint32_t uploads = 0;
  bool finished = false;

  for (const std::shared_ptr<Item> &item : live) {
    if (item->complete) continue;
    if (!probing) {
      // A model's texture not yet written, with no level of its own ready to
      // write: its placeholder, charged what the GPU finds for all of it.
      if (!item->written && item->owner != nullptr &&
          item->placeholder != nullptr) {
        bool levelReady = false;
        {
          std::lock_guard<std::mutex> hold(_lock);
          levelReady = !item->ready.empty() || item->abandoned;
        }
        if (!levelReady) {
          if (uploads > 0 && item->storage > budget - std::min(spent, budget)) {
            continue;
          }
          const bool whole = !startsAsPlaceholder(*item->placeholder);
          writePlaceholder(item->texture, *item->placeholder,
                           item->placeholderLevel, whole);
          item->written = true;
          spent += item->storage;
          uploads++;
        }
      }

      for (;;) {
        Unit unit;
        uint64_t cost = 0;
        {
          std::lock_guard<std::mutex> hold(_lock);
          if (item->abandoned || item->ready.empty()) break;
          Unit &next = item->ready.front();
          // The first write into a texture costs what the GPU has to find for
          // all of it, whichever level it is: that is when a texture's memory
          // is really made.
          cost = item->written ? next.budget : std::max(next.budget, item->storage);
          // At least one upload a frame, whatever its size: a level larger
          // than the budget would otherwise never go.
          if (uploads > 0 && cost > budget - std::min(spent, budget)) {
            break;
          }
          unit = std::move(next);
          item->ready.pop_front();
        }
        upload(*item, unit);
        item->written = true;
        spent += cost;
        uploads++;
      }
    }

    std::lock_guard<std::mutex> hold(_lock);
    if (!item->abandoned && item->decoded && !item->decoding &&
        item->ready.empty()) {
      item->complete = true;
      finished = true;
      _poppable[item->client].push_back(
          {item->texture, item->owner, item->name, item->failure});
      _counts[item->client].decoded++;
      _frames.arrived++;
    }
  }

  std::vector<std::shared_ptr<Item>> done;
  size_t left = 0;
  Frames frames;
  {
    std::lock_guard<std::mutex> hold(_lock);
    for (auto it = _items.begin(); it != _items.end();) {
      if ((*it)->complete) {
        done.push_back(std::move(*it));
        it = _items.erase(it);
      } else {
        ++it;
      }
    }
    left = _items.size();
    _frames.lastBytes = spent;
    _frames.lastUploads = uploads;
    if (uploads > 0) _frames.pumpsWithUploads++;
    _frames.mostBytes = std::max(_frames.mostBytes, spent);
    _frames.mostUploads = std::max(_frames.mostUploads, uploads);
    frames = _frames;
  }
  for (const std::shared_ptr<Item> &item : done) release(*item);

  // Said once a batch has all arrived, because a load's cost is wanted the
  // first time it happens rather than after somebody has reproduced it.
  if (finished && left == 0 && _batchCount > 0) {
    log("[orblit] %llu texture(s) arrived in %.0f ms; at most %llu KB and %u "
        "upload(s) in one frame; the longest frame meanwhile %.1f ms",
        (unsigned long long)_batchCount, (now() - _batchFrom) * 1000.0,
        (unsigned long long)(frames.mostBytes / 1024), frames.mostUploads,
        _longestFrame * 1000.0);
    _lastPumpAt = 0;
    _longestFrame = 0;
    if (_inlineCount > 0) {
      log("[orblit] %llu of them decoded on the drawing thread: %.0f ms in "
          "all, %.0f ms the longest; pushing them took %.0f ms",
          (unsigned long long)_inlineCount, _inlineSeconds * 1000.0,
          _longestInline * 1000.0, _pushSeconds * 1000.0);
    }
    if (_offThreadCount > 0) {
      log("[orblit] %llu of them decoded on workers: %.0f ms of decoding, "
          "%.0f ms the longest; handing them over and back took the drawing "
          "thread %.0f ms, %.0f ms at most in a frame; pushing them took "
          "%.0f ms",
          (unsigned long long)_offThreadCount, _offThreadSeconds * 1000.0,
          _longestOffThread * 1000.0, _handoverSeconds * 1000.0,
          _longestHandover * 1000.0, _pushSeconds * 1000.0);
    }
    _batchCount = 0;
    _pushSeconds = 0;
    _inlineCount = 0;
    _inlineSeconds = 0;
    _longestInline = 0;
    _offThreadCount = 0;
    _offThreadSeconds = 0;
    _longestOffThread = 0;
    _handoverSeconds = 0;
    _longestHandover = 0;
  }
}

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)

void TextureQueue::decodeOnWorkers() {
  namespace decoders = web::decoders;
  const double from = now();

  // Answers first, so a worker one frees can take another job this frame.
  std::vector<std::shared_ptr<Item>> posted;
  {
    std::lock_guard<std::mutex> hold(_lock);
    posted = _posted;
  }
  uint64_t copied = 0;
  size_t answers = 0;
  for (const std::shared_ptr<Item> &item : posted) {
    const decoders::State state = decoders::poll(item->job);
    if (state == decoders::State::waiting ||
        state == decoders::State::started) {
      continue;
    }
    if (state == decoders::State::done && answers > 0 &&
        copied >= kAnswerBytesPerFrame) {
      continue;
    }
    web::DecodeAnswer answer;
    const bool answered =
        state == decoders::State::done && decoders::take(item->job, answer);
    if (!answered) decoders::cancel(item->job);
    item->job = 0;
    {
      std::lock_guard<std::mutex> hold(_lock);
      _posted.erase(std::find(_posted.begin(), _posted.end(), item));
      if (!answered) {
        // Failed or given up on: decoded here, before anything newer.
        item->onPage = true;
        if (!item->abandoned) _waiting.push_front(item);
      }
    }
    if (!answered) continue;
    answers++;
    for (const web::DecodedPart &part : answer.parts) copied += part.size;
    _offThreadCount++;
    _offThreadSeconds += answer.milliseconds / 1000.0;
    _longestOffThread = std::max(_longestOffThread, answer.milliseconds / 1000.0);
    publishAnswer(*item, answer);
    std::lock_guard<std::mutex> hold(_lock);
    item->decoded = true;
  }

  // Then new jobs, one for each idle worker.
  while (decoders::capacity() > 0) {
    std::shared_ptr<Item> item;
    {
      std::lock_guard<std::mutex> hold(_lock);
      const auto next = std::find_if(
          _waiting.begin(), _waiting.end(),
          [](const std::shared_ptr<Item> &waiting) {
            return !waiting->onPage && !waiting->abandoned;
          });
      if (next == _waiting.end()) break;
      item = *next;
      _waiting.erase(next);
    }
    web::DecodeJob job = web::DecodeJob::ktx2Levels;
    std::vector<double> parameters;
    switch (item->kind) {
      case Item::Kind::ktx2:
        job = web::DecodeJob::ktx2Levels;
        parameters = {double(item->skip)};
        break;
      case Item::Kind::picture:
        job = web::DecodeJob::picture;
        parameters = {double(item->skip), item->srgb ? 1.0 : 0.0};
        break;
      case Item::Kind::basis:
        job = web::DecodeJob::basis;
        parameters = {double(item->basisFormat),
                      item->basisCompressed ? 1.0 : 0.0};
        break;
    }
    const int32_t id = decoders::submit(job, item->source->data(),
                                        item->source->size(), parameters,
                                        item->name);
    std::lock_guard<std::mutex> hold(_lock);
    if (id == 0) {
      _waiting.push_front(item);
      break;
    }
    item->job = id;
    _posted.push_back(item);
  }

  const double took = now() - from;
  if (!posted.empty() || !_posted.empty()) {
    _handoverSeconds += took;
    _longestHandover = std::max(_longestHandover, took);
  }
}

void TextureQueue::publishAnswer(Item &item, web::DecodeAnswer &answer) {
  if (!answer.note.empty()) {
    std::lock_guard<std::mutex> hold(_lock);
    item.failure = answer.note;
    return;
  }
  const size_t parts = answer.parts.size();
  const auto levelOf = [&answer](size_t i) {
    return i < answer.numbers.size() ? uint32_t(answer.numbers[i]) : 0u;
  };
  const auto unitOf = [&answer](size_t i, Unit::Kind kind, uint32_t level) {
    Unit unit;
    unit.kind = kind;
    unit.level = level;
    unit.size = answer.parts[i].size;
    unit.bytes = answer.takePart(i);
    unit.budget = unit.size;
    return unit;
  };
  switch (item.kind) {
    case Item::Kind::ktx2:
      // Smallest first already, as the job reads them.
      for (size_t i = 0; i < parts; i++) {
        const Unit::Kind kind =
            item.generateMipmaps ? Unit::Kind::picture : Unit::Kind::level;
        if (!publish(item, unitOf(i, kind, levelOf(i) - item.skip))) return;
      }
      break;
    case Item::Kind::picture:
      if (parts > 0) publish(item, unitOf(0, Unit::Kind::picture, 0));
      break;
    case Item::Kind::basis:
      // Largest first, as Basis Universal transcodes them; uploaded smallest
      // first, as every other texture's levels are.
      for (size_t i = parts; i-- > 0;) {
        if (!publish(item, unitOf(i, Unit::Kind::level, levelOf(i)))) return;
      }
      break;
  }
  std::lock_guard<std::mutex> hold(_lock);
  item.source.reset();
}

void TextureQueue::cancelJobs(const std::vector<std::shared_ptr<Item>> &items) {
  for (const std::shared_ptr<Item> &item : items) {
    if (item->job == 0) continue;
    web::decoders::cancel(item->job);
    item->job = 0;
    _posted.erase(std::remove(_posted.begin(), _posted.end(), item),
                  _posted.end());
  }
}

#endif

bool TextureQueue::pop(const void *client, Popped &out) {
  std::lock_guard<std::mutex> hold(_lock);
  const auto found = _poppable.find(client);
  if (found == _poppable.end() || found->second.empty()) return false;
  out = std::move(found->second.front());
  found->second.pop_front();
  _counts[client].popped++;
  return true;
}

size_t TextureQueue::pushedCount(const void *client) const {
  std::lock_guard<std::mutex> hold(_lock);
  const auto found = _counts.find(client);
  return found == _counts.end() ? 0 : found->second.pushed;
}

size_t TextureQueue::poppedCount(const void *client) const {
  std::lock_guard<std::mutex> hold(_lock);
  const auto found = _counts.find(client);
  return found == _counts.end() ? 0 : found->second.popped;
}

size_t TextureQueue::decodedCount(const void *client) const {
  std::lock_guard<std::mutex> hold(_lock);
  const auto found = _counts.find(client);
  return found == _counts.end() ? 0 : found->second.decoded;
}

void TextureQueue::waitForDecoding(const void *client) {
  // Nothing to wait for without workers: what is decoded inline is decoded
  // a little each frame, and blocking here would decode all of it at once.
  if (_noDecoderThreads) return;
  std::unique_lock<std::mutex> hold(_lock);
  _idle.wait(hold, [&] {
    for (const std::shared_ptr<Item> &item : _items) {
      if (item->client == client && !item->abandoned && !item->decoded) {
        return false;
      }
    }
    return true;
  });
}

void TextureQueue::forget(const void *owner) {
  std::vector<std::shared_ptr<Item>> dropped;
  {
    std::unique_lock<std::mutex> hold(_lock);
    for (auto it = _items.begin(); it != _items.end();) {
      if ((*it)->owner != owner) {
        ++it;
        continue;
      }
      Item &item = **it;
      item.abandoned = true;
      item.ready.clear();
      // Never to be popped, so counted as if it had been: a resource loader
      // measuring progress by the two counts would otherwise wait forever.
      Counts &counts = _counts[item.client];
      if (!item.complete) {
        counts.decoded++;
        counts.popped++;
      }
      dropped.push_back(std::move(*it));
      it = _items.erase(it);
    }
    _waiting.erase(std::remove_if(_waiting.begin(), _waiting.end(),
                                  [owner](const std::shared_ptr<Item> &item) {
                                    return item->owner == owner;
                                  }),
                   _waiting.end());
    // Arrived and not yet popped: those hold texture pointers that are about
    // to be destroyed, and will never be popped either.
    for (auto &entry : _poppable) {
      auto &queue = entry.second;
      for (auto it = queue.begin(); it != queue.end();) {
        if (it->owner == owner) {
          _counts[entry.first].popped++;
          it = queue.erase(it);
        } else {
          ++it;
        }
      }
    }
    _idle.wait(hold, [&] {
      for (const std::shared_ptr<Item> &item : dropped) {
        if (item->decoding) return false;
      }
      return true;
    });
  }
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  cancelJobs(dropped);
#endif
  for (const std::shared_ptr<Item> &item : dropped) release(*item);
}

void TextureQueue::shutdown() {
  std::vector<std::shared_ptr<Item>> dropped;
  {
    std::unique_lock<std::mutex> hold(_lock);
    if (_stopping) return;
    _stopping = true;
    for (const std::shared_ptr<Item> &item : _items) {
      item->abandoned = true;
      item->ready.clear();
    }
    dropped.swap(_items);
    _waiting.clear();
    _poppable.clear();
  }
  _wake.notify_all();
  // A worker part-way through a texture finishes that texture's current
  // level and finds it abandoned; joining waits for exactly that.
  for (std::thread &worker : _workers) {
    if (worker.joinable()) worker.join();
  }
  _workers.clear();
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  cancelJobs(dropped);
#endif
  for (const std::shared_ptr<Item> &item : dropped) release(*item);
  _placeholders.clear();
}

size_t TextureQueue::outstanding() const {
  std::lock_guard<std::mutex> hold(_lock);
  return _items.size();
}

size_t TextureQueue::unprimed(const void *owner) const {
  std::lock_guard<std::mutex> hold(_lock);
  size_t count = 0;
  for (const std::shared_ptr<Item> &item : _items) {
    if (item->owner == owner && !item->abandoned && !item->written &&
        item->placeholder != nullptr) {
      count++;
    }
  }
  return count;
}

TextureQueue::Frames TextureQueue::frames() const {
  std::lock_guard<std::mutex> hold(_lock);
  return _frames;
}

// ---- The provider ----

void QueuedTextureProvider::nameBytes(const uint8_t *data,
                                      const std::string &name,
                                      SharedBytes shared) {
  if (data != nullptr) _names[data] = {name, std::move(shared)};
}

std::vector<QueuedTextureProvider::Note> QueuedTextureProvider::takeNotes() {
  std::vector<Note> taken;
  taken.swap(_notes);
  return taken;
}

QueuedTextureProvider::Texture *QueuedTextureProvider::pushTexture(
    const uint8_t *data, size_t byteCount, const char *mimeType,
    TextureFlags flags) {
  TextureQueue::Request request;
  request.data = data;
  request.size = byteCount;
  request.mime = mimeType != nullptr ? mimeType : "";
  request.srgb = any(flags & TextureFlags::sRGB);
  request.client = this;
  request.owner = _owner;
  const auto named = _names.find(data);
  if (named != _names.end()) {
    request.name = named->second.first;
    // The same bytes, whole: kept rather than copied. A model of four hundred
    // textures is otherwise hundreds of megabytes copied on this thread.
    const SharedBytes &shared = named->second.second;
    if (shared && shared->data() == data && shared->size() == byteCount) {
      request.shared = shared;
    }
  }

  Texture *texture = _queue.push(request, _pushMessage);
  if (texture == nullptr) {
    _notes.push_back({request.name, _owner, _pushMessage});
    log("[orblit] texture %s refused: %s",
        request.name.empty() ? "(embedded)" : request.name.c_str(),
        _pushMessage.c_str());
  }
  return texture;
}

QueuedTextureProvider::Texture *QueuedTextureProvider::popTexture() {
  TextureQueue::Popped popped;
  if (!_queue.pop(this, popped)) {
    _popMessage.clear();
    return nullptr;
  }
  _popMessage = popped.failure;
  if (!popped.failure.empty()) {
    _notes.push_back({popped.name, popped.owner, popped.failure});
  }
  return popped.texture;
}

const char *QueuedTextureProvider::getPushMessage() const {
  return _pushMessage.empty() ? nullptr : _pushMessage.c_str();
}

const char *QueuedTextureProvider::getPopMessage() const {
  return _popMessage.empty() ? nullptr : _popMessage.c_str();
}

void QueuedTextureProvider::waitForCompletion() {
  _queue.waitForDecoding(this);
}

void QueuedTextureProvider::cancelDecoding() {
  // Nothing is cancelled. gltfio asks this before it lets go of an asset and
  // when it is destroyed, and what it needs is for no decoder to be touching
  // anything; the renderer forgets an asset's textures itself, by owner,
  // before destroying it, and shuts the queue down before tearing anything
  // else down. Cancelling here would also stop every other model's textures,
  // which share this provider.
  _queue.waitForDecoding(this);
}

size_t QueuedTextureProvider::getPushedCount() const {
  return _queue.pushedCount(this);
}

size_t QueuedTextureProvider::getPoppedCount() const {
  return _queue.poppedCount(this);
}

size_t QueuedTextureProvider::getDecodedCount() const {
  return _queue.decodedCount(this);
}

}  // namespace orblit
