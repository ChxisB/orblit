#import "OrblitRenderer.h"

#include <memory>
#include <string>
#include <vector>

#include "OrblitRendererCore.h"
#include "OrblitResources.h"

/// The Objective-C face of the renderer, and nothing more.
///
/// Everything the renderer does is orblit::Renderer, in plain C++ in
/// OrblitRendererCore.cpp — which is also what every other platform reaches,
/// through the C ABI in include/orblit_renderer.h. This class exists because
/// the Swift plugin speaks Objective-C: it owns one orblit::Renderer, gives it
/// the Apple surface (IOSurface-backed CVPixelBuffers, OrblitSurfaceApple.mm),
/// and forwards every call, turning Foundation's strings, arrays and
/// dictionaries into C++ ones on the way in and back again on the way out.
///
/// A method added to include/OrblitRenderer.h is a member added to
/// orblit::Renderer and one line here that forwards to it.

namespace {

/// Foundation's strings as the core takes them.
std::vector<std::string> OrblitStrings(NSArray<NSString *> *strings) {
  std::vector<std::string> out;
  out.reserve(strings.count);
  for (NSString *string in strings) {
    const char *utf8 = string.UTF8String;
    out.emplace_back(utf8 != nullptr ? utf8 : "");
  }
  return out;
}

/// One of them, where nil is the empty string it always meant.
std::string OrblitString(NSString *string) {
  const char *utf8 = string.UTF8String;
  return utf8 != nullptr ? std::string(utf8) : std::string();
}

}  // namespace

@implementation OrblitRenderer {
  std::unique_ptr<orblit::Renderer> _core;
}

- (nullable instancetype)initWithWidth:(uint32_t)width height:(uint32_t)height {
  if (!(self = [super init])) return nil;
  // The Apple surface, and the platform's own backend — Metal. The core
  // starts Filament and says why if it could not.
  _core = std::make_unique<orblit::Renderer>(OrblitCreateSurface(),
                                            ORBLIT_BACKEND_DEFAULT);
  if (!_core->initWithWidth(width, height)) return nil;
  return self;
}

- (void)renderAtTime:(double)time {
  _core->renderAtTime(time);
}

- (void)applyObjects:(const int64_t *)keys
          transforms:(const float *)transforms
             colours:(const float *)colours
              meshes:(const int32_t *)meshes
               flags:(const int32_t *)flags
           materials:(const int32_t *)materials
         morphCounts:(const int32_t *)morphCounts
        morphWeights:(const float *)morphWeights
               paths:(NSArray<NSString *> *)paths
               count:(uint32_t)count {
  _core->applyObjects(keys, transforms, colours, meshes, flags, materials,
                      morphCounts, morphWeights, OrblitStrings(paths), count);
}

- (BOOL)hasPoses {
  return _core->hasPoses();
}

- (void)applyPoses:(const int64_t *)keys
              ints:(const int32_t *)ints
            floats:(const float *)floats
       jointCounts:(const int32_t *)jointCounts
            joints:(const int32_t *)joints
   jointTransforms:(const float *)jointTransforms
                at:(double)at
             count:(uint32_t)count {
  _core->applyPoses(keys, ints, floats, jointCounts, joints, jointTransforms,
                    at, count);
}

- (void)setBatching:(BOOL)enabled {
  _core->setBatching(enabled);
}

- (uint32_t)batchedObjects {
  return _core->batchedObjects();
}

- (uint32_t)batchGroups {
  return _core->batchGroups();
}

- (void)setDepthPrepass:(BOOL)enabled {
  _core->setDepthPrepass(enabled);
}

- (uint32_t)prepassObjects {
  return _core->prepassObjects();
}

- (void)applyMaterials:(const int64_t *)keys
                 flags:(const int32_t *)flags
                params:(const float *)params
                  maps:(const int32_t *)maps
          texturePaths:(NSArray<NSString *> *)texturePaths
           textureSrgb:(const int32_t *)textureSrgb
                videos:(const int32_t *)videos
                 count:(uint32_t)count {
  _core->applyMaterials(keys, flags, params, maps, OrblitStrings(texturePaths),
                        textureSrgb, videos, count);
}

- (void)setPipeline:(const float *)params count:(NSUInteger)count {
  _core->setPipeline(params, count);
}

- (void)applyVideos:(const int64_t *)keys
              flags:(const int32_t *)flags
             params:(const float *)params
              paths:(NSArray<NSString *> *)paths
              count:(uint32_t)count {
  _core->applyVideos(keys, flags, params, OrblitStrings(paths), count);
}

- (void)applyLights:(const int64_t *)keys
              kinds:(const int32_t *)kinds
              flags:(const int32_t *)flags
             params:(const float *)params
              count:(uint32_t)count {
  _core->applyLights(keys, kinds, flags, params, count);
}

- (void)applyDecals:(const float *)params
             images:(const int32_t *)images
              paths:(NSArray<NSString *> *)paths
              count:(uint32_t)count {
  _core->applyDecals(params, images, OrblitStrings(paths), count);
}

- (void)setFogEnabled:(BOOL)enabled params:(const float *)params {
  _core->setFogEnabled(enabled, params);
}

- (void)setPostProcess:(const float *)params count:(NSUInteger)count {
  _core->setPostProcess(params, count);
}

- (void)applyProbes:(const int64_t *)keys
             params:(const float *)params
              count:(uint32_t)count {
  _core->applyProbes(keys, params, count);
}

- (void)applyField:(const float *)params from:(NSString *)from {
  _core->applyField(params, OrblitString(from));
}

- (void)setEnvironmentRadiance:(NSString *)radiance
                        skybox:(NSString *)skybox
                        params:(const float *)params {
  _core->setEnvironmentRadiance(OrblitString(radiance), OrblitString(skybox),
                                params);
}

- (void)setRenderGraph:(const float *)passes
                 count:(uint32_t)count
               targets:(const float *)targets
           targetCount:(uint32_t)targetCount
                 names:(NSArray<NSString *> *)names {
  _core->setRenderGraph(passes, count, targets, targetCount,
                        OrblitStrings(names));
}

// Hook (screen effects): the host's god-ray and distortion settings, kept by
// the plain C++ side until an effect pass reads them.
- (void)setGodRays:(const float *)godRays
              count:(NSUInteger)count
        distortions:(const float *)distortions
    distortionCount:(NSUInteger)distortionCount {
  _core->setGodRays(godRays, count, distortions, distortionCount);
}

- (NSArray<NSNumber *> *)passTimings {
  // Two numbers a pass, a double and an int, as they always were: Flutter's
  // codec sends an NSNumber as the type it was made from, and the Dart side
  // reads the count as an int.
  const std::vector<orblit::PassTiming> timings = _core->passTimings();
  NSMutableArray<NSNumber *> *out =
      [NSMutableArray arrayWithCapacity:timings.size() * 2];
  for (const orblit::PassTiming &pass : timings) {
    [out addObject:@(pass.milliseconds)];
    [out addObject:@(pass.drawn)];
  }
  return out;
}

- (double)gpuMilliseconds {
  return _core->gpuMilliseconds();
}

- (double)cpuMilliseconds {
  return _core->cpuMilliseconds();
}

- (BOOL)hasPopulations {
  return _core->hasPopulations();
}

- (void)applyPopulations:(const int32_t *)keys
                  counts:(const int32_t *)counts
                  meshes:(const int32_t *)meshes
                   flags:(const int32_t *)flags
               revisions:(const int32_t *)revisions
                  ranges:(const float *)ranges
                  bounds:(const float *)bounds
                   paths:(NSArray<NSString *> *)paths
                 changed:(const int32_t *)changed
            changedCount:(uint32_t)changedCount
              transforms:(const float *)transforms
                 colours:(const float *)colours
                   count:(uint32_t)count {
  _core->applyPopulations(keys, counts, meshes, flags, revisions, ranges,
                          bounds, OrblitStrings(paths), changed, changedCount,
                          transforms, colours, count);
}

- (BOOL)hasSprites {
  return _core->hasSprites();
}

- (void)applySprites:(const int32_t *)keys
               flags:(const int32_t *)flags
              orders:(const int32_t *)orders
           revisions:(const int32_t *)revisions
              params:(const float *)params
               paths:(NSArray<NSString *> *)paths
             changed:(const int32_t *)changed
       changedCounts:(const int32_t *)changedCounts
        changedCount:(uint32_t)changedCount
             records:(const float *)records
        recordFloats:(size_t)recordFloats
               count:(uint32_t)count {
  _core->applySprites(keys, flags, orders, revisions, params,
                      OrblitStrings(paths), changed, changedCounts,
                      changedCount, records, recordFloats, count);
}

- (BOOL)hasSplats {
  return _core->hasSplats();
}

- (void)applySplats:(const int32_t *)keys
              flags:(const int32_t *)flags
          revisions:(const int32_t *)revisions
             params:(const float *)params
              paths:(NSArray<NSString *> *)paths
            changed:(const int32_t *)changed
      changedCounts:(const int32_t *)changedCounts
       changedCount:(uint32_t)changedCount
               data:(const uint8_t *)data
         dataLength:(size_t)dataLength
              count:(uint32_t)count {
  _core->applySplats(keys, flags, revisions, params, OrblitStrings(paths),
                     changed, changedCounts, changedCount, data, dataLength,
                     count);
}

- (void)setSkyEnabled:(BOOL)enabled params:(const float *)params {
  _core->setSkyEnabled(enabled, params);
}

- (void)setPrecipitationEnabled:(BOOL)enabled params:(const float *)params {
  _core->setPrecipitationEnabled(enabled, params);
}

- (NSDictionary<NSString *, NSString *> *)notes {
  const orblit::Notes notes = _core->notes();
  NSMutableDictionary<NSString *, NSString *> *out =
      [NSMutableDictionary dictionaryWithCapacity:notes.size()];
  for (const auto &note : notes) {
    NSString *about = [NSString stringWithUTF8String:note.first.c_str()];
    NSString *saying = [NSString stringWithUTF8String:note.second.c_str()];
    if (about != nil && saying != nil) out[about] = saying;
  }
  return out;
}

- (void)setSkyColour:(const float *)colour
             ambient:(float)ambient
            showBody:(BOOL)showBody {
  _core->setSkyColour(colour, ambient, showBody);
}

- (void)setCameraPosition:(const float *)position
                   target:(const float *)target
              fieldOfView:(float)fieldOfView
             orthographic:(BOOL)orthographic
               viewHeight:(float)viewHeight
                       at:(double)at {
  _core->setCameraPosition(position, target, fieldOfView, orthographic,
                           viewHeight, at);
}

- (void)setExposure:(float)aperture
            shutter:(float)shutter
        sensitivity:(float)sensitivity {
  _core->setExposure(aperture, shutter, sensitivity);
}

- (void)setOutlineKeys:(const int64_t *)keys
                 count:(uint32_t)count
                params:(const float *)params {
  _core->setOutlineKeys(keys, count, params);
}

+ (void)provideResourceNamed:(NSString *)name bytes:(NSData *)bytes {
  const auto *start = static_cast<const uint8_t *>(bytes.bytes);
  std::vector<uint8_t> copy(start, start + bytes.length);
  orblit::provideResource(OrblitString(name), std::move(copy));
}

+ (BOOL)releaseResourceNamed:(NSString *)name {
  return orblit::releaseResource(OrblitString(name)) ? YES : NO;
}

- (NSArray<NSNumber *> *)capabilities {
  NSMutableArray<NSNumber *> *out =
      [NSMutableArray arrayWithCapacity:ORBLIT_CAPABILITY_COUNT];
  for (int which = 0; which < ORBLIT_CAPABILITY_COUNT; which++) {
    [out addObject:@(_core->capability(orblit_capability(which)))];
  }
  return out;
}

- (void)resizeToWidth:(uint32_t)width height:(uint32_t)height {
  _core->resizeToWidth(width, height);
}

- (nullable CVPixelBufferRef)copyPresentedBuffer {
  // Opaque on the way out of the core and concrete here, which is the one
  // place on this platform that is entitled to know: the plugin hands it
  // straight to Flutter's texture registry, and the registry wants a
  // CVPixelBuffer.
  return (CVPixelBufferRef)_core->copyPresentedBuffer();
}

- (void)dispose {
  if (_core) _core->dispose();
}

- (void)dealloc {
  [self dispose];
}

@end
