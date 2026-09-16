#pragma once

// Textures on their way to the GPU: decoded off the drawing thread, uploaded
// a few megabytes a frame, and chosen from a cooked set by what the device
// can sample.
//
// One queue for everything a renderer loads as a texture — a material's maps,
// a sprite layer's picture, and every image a glTF file names — so there is
// one budget per frame rather than one per kind of asset. PNG and JPEG are
// decoded by stb, Basis is transcoded by Filament's own reader, and GPU-ready
// KTX 2 (OrblitKtx2.h) is only decompressed; all three wait on worker threads
// and then take their turn at the upload budget.
//
// What a texture shows before it has all arrived is decided here and nowhere
// else. Filament samples a texture with more than one level through a view of
// the levels that have been uploaded — FTexture keeps the contiguous range
// setImage has reached and binds only that — so a texture whose levels arrive
// smallest first is, at every frame, a blurrier copy of itself rather than
// memory nobody wrote. Before its first level arrives, its smallest level
// holds transparent black: written when the texture is made, the colour
// Filament itself gives an external texture with nothing in it yet. A
// texture of one level has no smaller one to stand in, so that placeholder
// fills the whole of it; a cooked file with its mipmaps never pays that.

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <utility>
#include <vector>

#include <filament/Engine.h>
#include <filament/Texture.h>
#include <gltfio/TextureProvider.h>

#include "OrblitKtx2.h"
#include "OrblitResources.h"

namespace ktxreader {
class Ktx2Reader;
}

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
namespace orblit {
namespace web {
struct DecodeAnswer;
}
}  // namespace orblit
#endif

namespace orblit {

// ---- Defaults from the device ----

/// OrblitDeviceProfile's tiers, worked out again here from the same
/// measurements, so a renderer that has not been told a texture size or an
/// upload budget takes the device's own without any host code.
///
/// native_contract_test reads these constants and checks that Dart's
/// OrblitDeviceProfile puts devices either side of each one in the same
/// tier, and that the budgets are the same numbers.
enum class DeviceTier : uint8_t { low = 0, medium = 1, high = 2 };

/// Low: below this feature level...
constexpr int32_t kLowTierBelowFeatureLevel = 3;
/// ...or textures smaller than this...
constexpr int32_t kLowTierBelowTextureSize = 4096;
/// ...or this many threads or fewer...
constexpr int32_t kLowTierAtMostThreads = 2;
/// ...or less memory than this, where the device says.
constexpr int32_t kLowTierBelowMegabytes = 3072;
/// High: textures at least this large, this many threads, and this much
/// memory the device will vouch for.
constexpr int32_t kHighTierTextureSize = 8192;
constexpr int32_t kHighTierThreads = 8;
constexpr int32_t kHighTierMegabytes = 8192;

/// OrblitDeviceProfile.textureSizeBudget, by tier: the largest side a
/// texture is loaded at before any smaller one is asked for.
constexpr uint32_t kTierTextureSides[3] = {1024, 2048, 4096};

/// OrblitDeviceProfile.textureUploadKilobytes, by tier: how much texture data
/// a frame hands the GPU at most, beyond the one level every frame gets.
///
/// Measured on an M4 Pro through Filament's Metal backend, four hundred 2048²
/// BC7 textures with their mipmaps arriving at once, each frame's uploads
/// waited for (orblit_textures_check bench), on a machine busy with other
/// builds: 32 MB a frame made 26 to 31 ms frames; 16 MB, a 99th percentile of
/// 4.4 ms on one run and 19.9 ms on the next; 8 MB, 4.8 and 2.9 ms, arriving
/// in 5.8 s; 2 MB, 2.5 to 3.5 ms and 14.4 s. The high tier takes 8 MB, the
/// most that held on every run. Phones and browsers are not measured: medium
/// and low take a half and a quarter of it, and are the first numbers to move
/// once they are.
constexpr uint32_t kTierUploadKilobytes[3] = {2048, 4096, 8192};

/// The tier of a device from what its renderer measured. An answer below
/// nought is one the device would not give, read as Dart reads it.
DeviceTier deviceTier(int32_t featureLevel, int32_t maxTextureSize,
                      int32_t workerThreads, int32_t memoryMegabytes);

// ---- The queue ----

class TextureQueue {
 public:
  /// `deviceLargest` is the widest texture the device can make.
  /// `workerThreads` is how many the machine has; the queue takes some of
  /// them for decoding.
  TextureQueue(filament::Engine &engine, uint32_t deviceLargest,
               uint32_t workerThreads);
  ~TextureQueue();

  TextureQueue(const TextureQueue &) = delete;
  TextureQueue &operator=(const TextureQueue &) = delete;

  /// The largest side a texture is loaded at — levels above it are left out
  /// — and how many bytes a frame may upload beyond its first level. Nought
  /// for either is no limit. Textures already made keep the size they were
  /// made at.
  void setLimits(uint32_t maxSide, uint64_t bytesPerFrame);
  uint32_t maxSide() const { return _maxSide; }
  uint64_t bytesPerFrame() const { return _bytesPerFrame; }

  /// Whether the device samples a format, asked once for every format when
  /// the queue was made. Any thread.
  bool supports(const ktx2::Format &format) const;

  /// What a file's blocks are sampled as: their twin in the transfer function
  /// asked for (-1 unknown, 0 linear, 1 sRGB), else the file's own, else —
  /// for BC1 without alpha, which Metal lacks — BC1 with it. Null when the
  /// device samples none of those. `strict` refuses the file's own format
  /// when its twin was wanted and is missing, which is how a sibling that
  /// would draw in the wrong colour space is passed over for one that will
  /// not. Any thread.
  const ktx2::Format *sampledAs(const ktx2::Format &format, int transfer,
                                bool strict) const;

  struct Request {
    /// The bytes, copied. Ignored when `shared` is set, which is kept
    /// instead.
    const uint8_t *data = nullptr;
    size_t size = 0;
    SharedBytes shared{};
    /// image/png, image/jpeg or image/ktx2; the bytes decide when empty.
    std::string mime{};
    bool srgb = false;
    /// What to call it in a note.
    std::string name{};
    /// Who pops it when it has arrived.
    const void *client = nullptr;
    /// What it belongs to, so it can be forgotten when that goes.
    const void *owner = nullptr;
  };

  /// Makes the texture — usable at once, holding its placeholder — and
  /// queues its levels. Null, with why in `why`, when it cannot be made.
  /// The engine's thread.
  filament::Texture *push(const Request &request, std::string &why);

  /// Uploads what has been decoded, up to the frame's budget and never less
  /// than one level. Once a frame, on the engine's thread.
  void pump();

  struct Popped {
    filament::Texture *texture = nullptr;
    const void *owner = nullptr;
    std::string name{};
    /// Empty unless it did not arrive whole.
    std::string failure{};
  };

  /// A texture of `client`'s that has finished arriving, or failed to.
  bool pop(const void *client, Popped &out);

  size_t pushedCount(const void *client) const;
  size_t poppedCount(const void *client) const;
  size_t decodedCount(const void *client) const;

  /// Waits until nothing of `client`'s is being decoded or waiting to be.
  void waitForDecoding(const void *client);

  /// Drops everything of `owner`'s and waits for any of it being decoded,
  /// so its textures can be destroyed. The engine's thread.
  void forget(const void *owner);

  /// Drops everything and stops the workers. The engine's thread, before any
  /// texture the queue has handed out is destroyed.
  void shutdown();

  /// How many textures are still on their way.
  size_t outstanding() const;

  /// What the last pump uploaded, and the most any has since the queue was
  /// made.
  struct Frames {
    uint64_t lastBytes = 0;
    uint32_t lastUploads = 0;
    uint64_t mostBytes = 0;
    uint32_t mostUploads = 0;
    uint64_t pumpsWithUploads = 0;
    uint64_t arrived = 0;
  };
  Frames frames() const;

  /// Reads a texture path, choosing from its cooked set.
  ///
  /// For `x.ktx2`, the first of `x.astc.ktx2`, `x.bc.ktx2` and `x.etc2.ktx2`
  /// that exists and holds a format this device samples — its actual
  /// vkFormat, not its name — and otherwise `x.ktx2` itself. Anything else is
  /// read as it is. Remembered per path, transfer function and resource
  /// generation. `transfer` is how it will be sampled, as sampledAs takes it.
  /// `chosen` is the name read. Any thread.
  SharedBytes readCooked(const std::string &path, std::string *chosen,
                         int transfer = -1);

 private:
  struct Item;
  struct Unit;
  struct Counts {
    size_t pushed = 0;
    size_t popped = 0;
    size_t decoded = 0;
  };

  filament::Texture *pushKtx2(const Request &request, const uint8_t *data,
                              size_t size, std::string &why);
  filament::Texture *pushBasis(const Request &request, const uint8_t *data,
                               size_t size, const ktx2::Header &header,
                               std::string &why);
  filament::Texture *pushPicture(const Request &request, const uint8_t *data,
                                 size_t size, std::string &why);
  void enqueue(const std::shared_ptr<Item> &item);
  void writePlaceholder(filament::Texture *texture,
                        const ktx2::Format &format, uint32_t level);
  void startWorkers();
  void work();
  void decode(Item &item);
  bool publish(Item &item, Unit &&unit);
  void decodeInline();
  void upload(Item &item, Unit &unit);
  void release(Item &item);
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  /// A browser's decoding: jobs handed to the decoder workers and their
  /// answers taken back (native/web/orblit_decoder_workers.js). What they
  /// will not take, decodeInline decodes.
  void decodeOnWorkers();
  void publishAnswer(Item &item, web::DecodeAnswer &answer);
  void cancelJobs(const std::vector<std::shared_ptr<Item>> &items);
  /// Items a worker is decoding. The engine's thread only.
  std::vector<std::shared_ptr<Item>> _posted{};
#endif

  filament::Engine &_engine;
  const uint32_t _deviceLargest;
  uint32_t _maxSide = 0;
  uint64_t _bytesPerFrame = 0;
  std::vector<uint32_t> _supported{};
  bool _familyUsable[4] = {};

  std::unique_ptr<ktxreader::Ktx2Reader> _basis;

  mutable std::mutex _lock;
  std::condition_variable _wake;
  std::condition_variable _idle;
  std::vector<std::shared_ptr<Item>> _items{};
  std::deque<std::shared_ptr<Item>> _waiting{};
  std::map<const void *, std::deque<Popped>> _poppable{};
  std::map<const void *, Counts> _counts{};
  std::vector<std::thread> _workers{};
  uint32_t _workerCount = 1;
  bool _inline = false;
  bool _stopping = false;
  uint64_t _order = 0;

  /// Placeholder levels, shared between every texture of the same format
  /// and size so a scene of four hundred has a handful of them in memory.
  std::map<std::pair<uint32_t, size_t>, std::shared_ptr<std::vector<uint8_t>>>
      _placeholders{};

  Frames _frames{};
  /// Placeholder bytes written since the last pump, the engine's thread only.
  uint64_t _placeholderBytes = 0;
  /// When the batch now arriving started, for the line logged when it
  /// finishes.
  double _batchFrom = 0;
  uint64_t _batchCount = 0;
  /// Of the batch, what was decoded on the drawing thread, and what that
  /// cost it. The engine's thread only.
  uint64_t _inlineCount = 0;
  double _pushSeconds = 0;
  /// Of the batch, what decoder workers decoded, what that took them, and
  /// what handing it over and back cost the drawing thread.
  uint64_t _offThreadCount = 0;
  double _offThreadSeconds = 0;
  double _longestOffThread = 0;
  double _handoverSeconds = 0;
  double _longestHandover = 0;
  double _inlineSeconds = 0;
  double _longestInline = 0;

  struct Cooked {
    uint64_t generation;
    std::string name;
  };
  std::mutex _cookedLock;
  std::unordered_map<std::string, Cooked> _cooked{};
};

/// gltfio's texture provider interface, onto a queue.
///
/// A model's images are pushed while its resources begin to load, and gltfio
/// pops them as they arrive — which here means when their last level has
/// been uploaded, not when they have been decoded. Popping is only
/// bookkeeping: the levels go up whether or not anybody pops.
class QueuedTextureProvider final : public filament::gltfio::TextureProvider {
 public:
  explicit QueuedTextureProvider(TextureQueue &queue) : _queue(queue) {}

  /// What the textures pushed from now on belong to.
  void setOwner(const void *owner) { _owner = owner; }

  /// The file a model's bytes came from, so a note can name it, and the
  /// shared bytes themselves when they are shared, so a push keeps them
  /// rather than copying them. Only for as long as the bytes are being handed
  /// over; see forgetNames.
  void nameBytes(const uint8_t *data, const std::string &name,
                 SharedBytes shared = {});
  void forgetNames() { _names.clear(); }

  /// A problem with one of a model's textures.
  struct Note {
    /// The file, or empty for an image inside the model.
    std::string texture;
    /// The owner it was pushed under.
    const void *owner;
    std::string sentence;
  };

  /// Problems since last asked.
  std::vector<Note> takeNotes();

  Texture *pushTexture(const uint8_t *data, size_t byteCount,
                       const char *mimeType, TextureFlags flags) override;
  Texture *popTexture() override;
  void updateQueue() override {}
  const char *getPushMessage() const override;
  const char *getPopMessage() const override;
  void waitForCompletion() override;
  void cancelDecoding() override;
  size_t getPushedCount() const override;
  size_t getPoppedCount() const override;
  size_t getDecodedCount() const override;

 private:
  TextureQueue &_queue;
  const void *_owner = nullptr;
  std::unordered_map<const uint8_t *, std::pair<std::string, SharedBytes>>
      _names{};
  std::string _pushMessage{};
  std::string _popMessage{};
  std::vector<Note> _notes{};
};

}  // namespace orblit
