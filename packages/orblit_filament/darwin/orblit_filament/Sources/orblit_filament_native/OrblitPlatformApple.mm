#include "OrblitPlatform.h"

// The platform layer's answers on macOS and iOS: the frameworks the renderer
// has always used, moved here from OrblitRenderer.mm unchanged so that what an
// Apple build does is exactly what it did before the renderer was portable.

#if ORBLIT_PLATFORM_APPLE

#import <AVFoundation/AVFoundation.h>
#import <CoreGraphics/CoreGraphics.h>
#import <CoreVideo/CoreVideo.h>
#import <Foundation/Foundation.h>
#import <ImageIO/ImageIO.h>
// Metal for one question only — whether Filament will compare depth samples
// on this device, see shadowComparisonAvailable. The renderer draws through
// Filament and holds no Metal object of its own.
#import <Metal/Metal.h>
#import <QuartzCore/QuartzCore.h>
#import <TargetConditionals.h>

#include <filament/Engine.h>
#include <filament/Texture.h>

#include <mutex>
#include <unordered_map>

/// What the end-of-file observer needs in order to loop a video.
///
/// Its own object rather than the decoder, so the observer's block can hold
/// it without holding the decoder: the block runs on the main queue and may
/// arrive after the decoder has gone.
@interface OrblitVideoLoop : NSObject
@property(nonatomic, weak) AVPlayer *player;
@property(nonatomic) BOOL looping;
@property(nonatomic) BOOL playing;
@property(nonatomic) float rate;
@end

@implementation OrblitVideoLoop
@end

namespace orblit {

void log(const char *format, ...) {
  va_list arguments;
  va_start(arguments, format);
  const std::string line = vformat(format, arguments);
  va_end(arguments);
  NSString *text = [NSString stringWithUTF8String:line.c_str()];
  // Bytes that are not UTF-8 — a path off a disk that never promised it —
  // are still worth seeing, so they go through as Latin-1 rather than as
  // nothing at all.
  if (text == nil) {
    text = [NSString stringWithCString:line.c_str()
                              encoding:NSISOLatin1StringEncoding];
  }
  NSLog(@"%@", text);
}

double now() { return CFAbsoluteTimeGetCurrent(); }

bool shadowComparisonAvailable() {
#if TARGET_OS_IPHONE
  // Deliberately the deprecated call, and deliberately this exact feature
  // set: the answer wanted here is not "what can this GPU do" but "what will
  // Filament decide", and what Filament decides is
  // `[device supportsFeatureSet:MTLFeatureSet_iOS_GPUFamily3_v1]` in
  // MetalState.mm. Asking a different question — supportsFamily:, say —
  // would be asking about a different thing and could disagree with the
  // driver that actually builds the sampler. See OrblitPlatform.h.
  //
  // TARGET_OS_IPHONE rather than a simulator test because that is the shape
  // of Filament's own `#if defined(FILAMENT_IOS)`: a device that answered no
  // would be neutered in exactly the same way, and none this package's iOS
  // 13 floor admits does.
  id<MTLDevice> device = MTLCreateSystemDefaultDevice();
  // No Metal device at all is not this function's business to report; the
  // engine will fail to start on its own, and saying "shadows work" leaves
  // that the only failure rather than adding a second.
  if (device == nil) return true;
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
  const bool supported =
      [device supportsFeatureSet:MTLFeatureSet_iOS_GPUFamily3_v1];
#pragma clang diagnostic pop
  return supported;
#else
  // macOS. Filament's rewrite is inside `#if defined(FILAMENT_IOS)`, so a Mac
  // never reaches it however old its GPU.
  return true;
#endif
}

void parallelFor(size_t count, const std::function<void(size_t)> &body) {
  if (count == 0) return;
  // The block captures a pointer to the function, not the function: a
  // captured std::function is copied into the block, once per block.
  const std::function<void(size_t)> *run = &body;
  dispatch_apply(count, dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0),
                 ^(size_t i) { (*run)(i); });
}

/// ImageIO and Core Graphics, which is the one piece of decals that another
/// platform writes again: every other port has its own way to read a PNG. The
/// draw into the bitmap resamples to the square on the way, and premultiplies,
/// which is what the shader's blend expects. Empty on failure.
std::vector<uint8_t> readPicture(const uint8_t *data, size_t size,
                                 uint32_t side) {
  std::vector<uint8_t> pixels;
  if (data == nullptr || size == 0) return pixels;
  // Not copied. The caller holds the bytes until this returns, and the image
  // made from them is drawn and released before it does.
  CFDataRef bytes = CFDataCreateWithBytesNoCopy(
      kCFAllocatorDefault, data, CFIndex(size), kCFAllocatorNull);
  if (bytes == nullptr) return pixels;
  CGImageSourceRef source = CGImageSourceCreateWithData(bytes, nullptr);
  CFRelease(bytes);
  if (source == nullptr) return pixels;
  CGImageRef image = CGImageSourceCreateImageAtIndex(source, 0, nullptr);
  CFRelease(source);
  if (image == nullptr) return pixels;

  pixels.assign(size_t(side) * side * 4, 0);
  CGColorSpaceRef space = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
  CGContextRef context = CGBitmapContextCreate(
      pixels.data(), side, side, 8, size_t(side) * 4, space,
      kCGImageAlphaPremultipliedLast | kCGBitmapByteOrder32Big);
  CGColorSpaceRelease(space);
  if (context != nullptr) {
    CGContextSetInterpolationQuality(context, kCGInterpolationHigh);
    // Row nought of the bitmap is the top of the picture, and row nought of
    // the texture is v nought, which is the far edge of the box's z — so the
    // top of a poster is at the top of the wall it is thrown onto.
    CGContextDrawImage(context, CGRectMake(0, 0, side, side), image);
    CGContextRelease(context);
  } else {
    pixels.clear();
  }
  CGImageRelease(image);
  return pixels;
}

namespace {

/// AVFoundation, decoding into IOSurface-backed buffers that Filament's Metal
/// backend takes as an external image without a copy.
///
/// The frame never becomes an ordinary texture. It stays the buffer the
/// decoder wrote and is handed to the GPU where it lies, which is the whole
/// reason a screen in the scene costs about as much as a flat colour.
class AppleVideoDecoder final : public VideoDecoder {
 public:
  ~AppleVideoDecoder() override {
    stop();
    // The buffer last on the GPU, released after the texture that showed it
    // has been destroyed — which the renderer does before destroying this.
    if (_showing != nullptr) {
      CVPixelBufferRelease(_showing);
      _showing = nullptr;
    }
  }

  /// The pixel format is asked for explicitly: Filament's external images
  /// take 32-bit BGRA or biplanar YUV and nothing else, and a decoder left to
  /// choose will happily hand back something neither of them.
  bool open(const std::string &path) override {
    NSString *text = [NSString stringWithUTF8String:path.c_str()];
    if (text == nil) return false;
    NSURL *url = [text hasPrefix:@"http"] ? [NSURL URLWithString:text]
                                          : [NSURL fileURLWithPath:text];
    if (url == nil) return false;

    AVPlayerItem *item = [AVPlayerItem playerItemWithURL:url];
    _output = [[AVPlayerItemVideoOutput alloc] initWithPixelBufferAttributes:@{
      (id)kCVPixelBufferPixelFormatTypeKey : @(kCVPixelFormatType_32BGRA),
      (id)kCVPixelBufferMetalCompatibilityKey : @YES,
      (id)kCVPixelBufferIOSurfacePropertiesKey : @{},
    }];
    [item addOutput:_output];

    _player = [AVPlayer playerWithPlayerItem:item];
    // Without this the player pauses itself the moment a buffer runs short,
    // and a file on the local disk stutters for no reason a viewer can see.
    _player.automaticallyWaitsToMinimizeStalling = NO;

    _loop = [[OrblitVideoLoop alloc] init];
    _loop.player = _player;
    OrblitVideoLoop *loop = _loop;
    _endObserver = [[NSNotificationCenter defaultCenter]
        addObserverForName:AVPlayerItemDidPlayToEndTimeNotification
                    object:item
                     queue:[NSOperationQueue mainQueue]
                usingBlock:^(NSNotification *note) {
                  // Looping is done here rather than with a queue player,
                  // because a queue restarts by loading the file again and
                  // the gap that leaves is exactly what a loop is meant to
                  // hide.
                  AVPlayer *player = loop.player;
                  if (player == nil || !loop.looping) return;
                  [player seekToTime:kCMTimeZero
                      toleranceBefore:kCMTimeZero
                       toleranceAfter:kCMTimeZero];
                  if (loop.playing) [player playImmediatelyAtRate:loop.rate];
                }];
    return true;
  }

  void seek(double seconds) override {
    [_player seekToTime:CMTimeMakeWithSeconds(seconds, 600)
        toleranceBefore:kCMTimeZero
         toleranceAfter:kCMTimeZero];
  }

  void setVolume(float volume) override { _player.volume = volume; }

  void play(float rate) override {
    _loop.playing = YES;
    _loop.rate = rate;
    [_player playImmediatelyAtRate:rate];
  }

  void pause() override {
    _loop.playing = NO;
    [_player pause];
  }

  void setLooping(bool looping) override { _loop.looping = looping; }

  /// A video that has not advanced hands back nothing and costs a single
  /// comparison; the picture already on the texture stays.
  bool pump(filament::Engine &engine, filament::Texture *texture) override {
    if (_output == nil || texture == nullptr) return false;

    const CMTime at = [_output itemTimeForHostTime:CACurrentMediaTime()];
    if (![_output hasNewPixelBufferForItemTime:at]) return false;
    CVPixelBufferRef buffer = [_output copyPixelBufferForItemTime:at
                                               itemTimeForDisplay:nullptr];
    if (buffer == nullptr) return false;

    texture->setExternalImage(engine, buffer);
    // The one just replaced, not the one just set: the new buffer is what the
    // next draw reads, and releasing it here would pull the picture out from
    // under a frame that has not happened yet.
    if (_showing != nullptr) CVPixelBufferRelease(_showing);
    _showing = buffer;
    return true;
  }

  void stop() override {
    if (_endObserver != nil) {
      [[NSNotificationCenter defaultCenter] removeObserver:_endObserver];
      _endObserver = nil;
    }
    if (_player != nil) {
      [_player pause];
      _player = nil;
    }
    _output = nil;
  }

 private:
  AVPlayer *_player = nil;
  AVPlayerItemVideoOutput *_output = nil;
  OrblitVideoLoop *_loop = nil;
  id _endObserver = nil;

  /// The buffer currently on the GPU. Held until the next one replaces it:
  /// releasing it at the end of the frame that showed it would pull the
  /// picture out from under a draw that has not happened yet.
  CVPixelBufferRef _showing = nullptr;
};

/// The newest picture a producer handed over, and which hand-over it was.
///
/// Held by the producer's end and by every reader, so that releasing a feed
/// does not take the picture from a screen that is still showing it.
class PixelBufferStore {
 public:
  struct Picture {
    CVPixelBufferRef buffer;  // Retained, or null before the first picture.
    uint64_t generation;
  };

  ~PixelBufferStore() {
    if (_latest != nullptr) CVPixelBufferRelease(_latest);
  }

  /// Keeps `buffer` as the newest picture. Any thread.
  void put(CVPixelBufferRef buffer) {
    CVPixelBufferRetain(buffer);
    CVPixelBufferRef replaced;
    {
      std::lock_guard<std::mutex> hold(_lock);
      replaced = _latest;
      _latest = buffer;
      _generation++;
    }
    if (replaced != nullptr) CVPixelBufferRelease(replaced);
  }

  Picture latest() const {
    std::lock_guard<std::mutex> hold(_lock);
    if (_latest != nullptr) CVPixelBufferRetain(_latest);
    return {_latest, _generation};
  }

 private:
  mutable std::mutex _lock;
  CVPixelBufferRef _latest = nullptr;
  uint64_t _generation = 0;
};

/// Hands a feed's newest buffer to the texture, which is the same call a
/// decoded video frame makes and costs the same: nothing is copied.
class PixelBufferFeed final : public ExternalImageSource {
 public:
  explicit PixelBufferFeed(std::shared_ptr<PixelBufferStore> store)
      : _store(std::move(store)) {}

  ~PixelBufferFeed() override {
    // After the texture that showed it, which the renderer destroys first.
    if (_showing != nullptr) CVPixelBufferRelease(_showing);
  }

  bool pump(filament::Engine &engine, filament::Texture *texture) override {
    if (_store == nullptr || texture == nullptr) return false;
    const PixelBufferStore::Picture next = _store->latest();
    if (next.buffer == nullptr) return false;
    if (next.generation == _seen) {
      CVPixelBufferRelease(next.buffer);
      return false;
    }
    _seen = next.generation;
    texture->setExternalImage(engine, next.buffer);
    // The one just replaced, as in AppleVideoDecoder::pump.
    if (_showing != nullptr) CVPixelBufferRelease(_showing);
    _showing = next.buffer;
    return true;
  }

  void stop() override { _store.reset(); }

 private:
  std::shared_ptr<PixelBufferStore> _store;
  CVPixelBufferRef _showing = nullptr;
  uint64_t _seen = 0;
};

/// Every feed in the process, by number. A producer on any thread looks its
/// feed up here, so the table is locked and the stores are shared.
class FeedTable {
 public:
  int64_t add(std::shared_ptr<PixelBufferStore> store) {
    std::lock_guard<std::mutex> hold(_lock);
    const int64_t id = _next++;
    _stores[id] = std::move(store);
    return id;
  }

  std::shared_ptr<PixelBufferStore> find(int64_t id) const {
    std::lock_guard<std::mutex> hold(_lock);
    const auto at = _stores.find(id);
    return at == _stores.end() ? nullptr : at->second;
  }

  void remove(int64_t id) {
    std::lock_guard<std::mutex> hold(_lock);
    _stores.erase(id);
  }

 private:
  mutable std::mutex _lock;
  int64_t _next = 1;
  std::unordered_map<int64_t, std::shared_ptr<PixelBufferStore>> _stores;
};

FeedTable &feeds() {
  static FeedTable table;
  return table;
}

/// Filament's Metal backend takes an IOSurface-backed buffer in one of the
/// formats the decoder above asks for, and asserts on anything else.
bool takenAsExternalImage(CVPixelBufferRef buffer) {
  switch (CVPixelBufferGetPixelFormatType(buffer)) {
    case kCVPixelFormatType_32BGRA:
    case kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange:
    case kCVPixelFormatType_420YpCbCr8BiPlanarFullRange:
      return CVPixelBufferGetIOSurface(buffer) != nullptr;
    default:
      return false;
  }
}

}  // namespace

std::unique_ptr<VideoDecoder> createVideoDecoder() {
  return std::make_unique<AppleVideoDecoder>();
}

int64_t createFeed(int32_t, int32_t, std::string &) {
  // The size is each buffer's own on Apple, so the one asked for here has
  // nothing to configure.
  return feeds().add(std::make_shared<PixelBufferStore>());
}

void releaseFeed(int64_t id) { feeds().remove(id); }

std::unique_ptr<ExternalImageSource> openFeed(int64_t id, std::string &why) {
  std::shared_ptr<PixelBufferStore> store = feeds().find(id);
  if (store == nullptr) {
    why = "Feed " + std::to_string(id) + " is not open.";
    return nullptr;
  }
  return std::make_unique<PixelBufferFeed>(std::move(store));
}

bool submitFeed(int64_t id, CVPixelBufferRef buffer, std::string &why) {
  if (buffer == nullptr || !takenAsExternalImage(buffer)) {
    why =
        "A feed takes an IOSurface-backed buffer in 32BGRA or biplanar 4:2:0 "
        "format.";
    return false;
  }
  const std::shared_ptr<PixelBufferStore> store = feeds().find(id);
  if (store == nullptr) {
    why = "Feed " + std::to_string(id) + " is not open.";
    return false;
  }
  store->put(buffer);
  return true;
}

}  // namespace orblit

#endif  // ORBLIT_PLATFORM_APPLE
