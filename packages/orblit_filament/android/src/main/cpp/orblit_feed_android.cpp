// Feeds on Android: a Surface the application draws into, read by the
// renderer where the picture lies.
//
// An AImageReader owns the buffer queue. Its window is what the producer
// draws into: a camera session, a MediaCodec decoder, an EGL surface or a
// hardware canvas. The renderer takes the newest AHardwareBuffer off the
// queue and gives it to Filament as an ACQUIRED stream, which samples it
// without a copy. Filament hands the buffer back through a callback when it
// is done with it, and only then does it go back to the queue.
//
// The NDK declares the calls below from API 26, and the plugin's minSdk is
// 24. They are weak symbols (see the CMake file) and guarded at run time, so
// the library still loads on 24 and 25 and a feed there says why it is
// refused.

#include "orblit_feed_android.h"

#include <android/hardware_buffer.h>
#include <android/native_window.h>
#include <android/native_window_jni.h>
#include <media/NdkImage.h>
#include <media/NdkImageReader.h>

#include <atomic>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

#include <filament/Engine.h>
#include <filament/Stream.h>
#include <filament/Texture.h>
#include <math/mat3.h>

#include "OrblitPlatform.h"

namespace orblit {
namespace {

// Filament holds the picture it is drawing and the one it was handed last,
// and the renderer holds the one it just took, so three are in use at once
// and a fourth lets the producer keep writing.
constexpr int32_t kMaxImages = 4;

/// The reader and the number of readers of it: one. A queue has one
/// consumer, so a second screen cannot read the same feed.
struct FeedReader {
  FeedReader(AImageReader *made, int32_t width, int32_t height)
      : reader(made), width(width), height(height) {}
  ~FeedReader() { AImageReader_delete(reader); }
  FeedReader(const FeedReader &) = delete;
  FeedReader &operator=(const FeedReader &) = delete;

  AImageReader *const reader;
  const int32_t width;
  const int32_t height;
  std::atomic<bool> taken{false};
};

/// One picture Filament may still be reading. Keeps the reader alive,
/// because an image cannot outlive the reader it came from.
struct Held {
  std::shared_ptr<FeedReader> reader;
  AImage *image;
};

void giveBack(void *, void *user) {
  std::unique_ptr<Held> held(static_cast<Held *>(user));
  AImage_delete(held->image);
}

class FeedTable {
 public:
  int64_t add(std::shared_ptr<FeedReader> reader) {
    std::lock_guard<std::mutex> lock(_mutex);
    const int64_t id = _next++;
    _readers[id] = std::move(reader);
    return id;
  }

  std::shared_ptr<FeedReader> find(int64_t id) const {
    std::lock_guard<std::mutex> lock(_mutex);
    const auto at = _readers.find(id);
    return at == _readers.end() ? nullptr : at->second;
  }

  void remove(int64_t id) {
    std::lock_guard<std::mutex> lock(_mutex);
    _readers.erase(id);
  }

 private:
  mutable std::mutex _mutex;
  std::unordered_map<int64_t, std::shared_ptr<FeedReader>> _readers;
  int64_t _next = 1;
};

FeedTable &feeds() {
  static FeedTable table;
  return table;
}

bool haveFeeds() {
  if (__builtin_available(android 26, *)) return true;
  return false;
}

/// Reads a feed's queue as a Filament acquired stream.
class AcquiredFeed final : public ExternalImageSource {
 public:
  explicit AcquiredFeed(std::shared_ptr<FeedReader> reader)
      : _reader(std::move(reader)) {}

  ~AcquiredFeed() override {
    // The renderer destroyed the texture before this, so nothing draws
    // from the stream any more. Pictures Filament still holds are handed
    // back by the stream's destruction.
    if (_stream != nullptr) _engine->destroy(_stream);
    _reader->taken.store(false);
  }

  bool pump(filament::Engine &engine, filament::Texture *texture) override {
    if (_stream == nullptr) {
      _engine = &engine;
      _stream = filament::Stream::Builder().build(engine);
      texture->setExternalStream(engine, _stream);
    }
    AImage *image = nullptr;
    if (AImageReader_acquireLatestImage(_reader->reader, &image) != AMEDIA_OK ||
        image == nullptr) {
      return false;
    }
    AHardwareBuffer *buffer = nullptr;
    if (AImage_getHardwareBuffer(image, &buffer) != AMEDIA_OK ||
        buffer == nullptr) {
      AImage_delete(image);
      return false;
    }
    auto held = std::make_unique<Held>(Held{_reader, image});
    // The picture is shown as the producer drew it. Turning it upright is
    // the material's job, because the Vulkan backend ignores the stream's
    // transform.
    _stream->setAcquiredImage(buffer, &giveBack, held.release());
    return true;
  }

  void stop() override {}

 private:
  std::shared_ptr<FeedReader> _reader;
  filament::Engine *_engine = nullptr;
  filament::Stream *_stream = nullptr;
};

}  // namespace

int64_t createFeed(int32_t width, int32_t height, std::string &why) {
  if (!haveFeeds()) {
    why = "A feed needs Android 8.0 (API 26) or later.";
    return 0;
  }
  AImageReader *made = nullptr;
  const media_status_t status = AImageReader_newWithUsage(
      width, height, AIMAGE_FORMAT_PRIVATE,
      AHARDWAREBUFFER_USAGE_GPU_SAMPLED_IMAGE, kMaxImages, &made);
  if (status != AMEDIA_OK || made == nullptr) {
    why = "Android would not make a feed of " + std::to_string(width) + " by " +
          std::to_string(height) + " (media status " +
          std::to_string(status) + ").";
    return 0;
  }
  return feeds().add(std::make_shared<FeedReader>(made, width, height));
}

void releaseFeed(int64_t id) { feeds().remove(id); }

std::unique_ptr<ExternalImageSource> openFeed(int64_t id, std::string &why) {
  const std::shared_ptr<FeedReader> reader = feeds().find(id);
  if (reader == nullptr) {
    why = "Feed " + std::to_string(id) + " is not open.";
    return nullptr;
  }
  if (reader->taken.exchange(true)) {
    why = "Feed " + std::to_string(id) +
          " is already shown by another screen, and a feed on Android has "
          "one reader.";
    return nullptr;
  }
  return std::make_unique<AcquiredFeed>(reader);
}

jobject feedSurface(JNIEnv *env, int64_t id) {
  if (!haveFeeds()) return nullptr;
  const std::shared_ptr<FeedReader> reader = feeds().find(id);
  if (reader == nullptr) return nullptr;
  ANativeWindow *window = nullptr;
  if (AImageReader_getWindow(reader->reader, &window) != AMEDIA_OK ||
      window == nullptr) {
    return nullptr;
  }
  return ANativeWindow_toSurface(env, window);
}

}  // namespace orblit
