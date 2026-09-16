// Environments from pictures: an .hdr or .exr named as a scene's environment,
// decoded, summarised and filtered at run time into the light it stands in.
//
// Two routes to the same light, and when each is the right one:
//
//  * Baked, by tool/bake_environment.sh (cmgen): KTX cubemaps written once.
//    Nothing is decoded or filtered on the device, a launch reads two small
//    files, and every device gets the same reflections at the same size. The
//    route for anything that ships: the result is known before anybody runs
//    it, and it costs no frame.
//  * Filtered here, from the picture: the route for pictures that are not
//    known in advance — an editor trying environments on, a user's own HDR,
//    anything fetched — and for iterating without a bake step. It costs a
//    decode on a worker (tens of milliseconds for a 2K picture) and two
//    frames with GPU work in them, once per picture per session, and sizes
//    follow the device rather than the bake.
//
// The two agree: the diffuse half is cmgen's own arithmetic
// (OrblitEnvironmentBake.cpp), and the bake script passes cmgen the sizes
// this file chooses. native/headless's environment check measures how far
// apart they are in pixels.
//
// How a picture becomes light:
//
//  1. On a worker thread (in place, in a browser, which has no threads yet):
//     read the bytes, hash them, decode them (OrblitHdrImage), make cmgen's
//     cube of them at the reflections' size, mirror it, and project it onto
//     three bands of spherical harmonics — the diffuse light. Then bring the
//     picture down to the size the GPU needs and pack it as half floats.
//  2. The first time, a frame to build the filters: their materials, and the
//     specular filter's sample kernel, rendered once.
//  3. A frame to upload the picture and convert it to a cube twice with
//     Filament's EquirectangularToCubemap — once at the reflections' size,
//     once at the backdrop's sharper one.
//  4. A frame for SpecularFilter to blur the reflections' cube into its
//     chain. The light is built and the scene is lit.
//
// Where the GPU cannot filter (no half-float render targets) the worker runs
// cmgen's own roughness filter on a small cube instead, and there is no GPU
// work beyond the upload. Where the device cannot even sample a
// floating-point cubemap, the scene notes say to bake.
//
// Every stage one frame at a time, so no frame waits on more than one
// filter pass, and nothing on the drawing thread waits on a decode. Measured
// on an M4 Pro (Metal) with 2K pictures, over three runs: building 1–3 ms,
// upload and conversion 3–8 ms, the filter 21–54 ms — the one frame over a
// 60 Hz budget, once per picture; with a cold Metal shader cache the first
// picture a machine ever filters took 225 and 504 ms instead.

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <string>
#include <thread>
#include <utility>
#include <vector>

#include <backend/PixelBufferDescriptor.h>
#include <filament/IndirectLight.h>
#include <filament/Skybox.h>
#include <filament/Texture.h>

#include "OrblitEnvironmentBake.h"
#include "OrblitHdrImage.h"
#include "OrblitPlatform.h"
#include "OrblitRendererCore.h"
#include "OrblitResources.h"

namespace orblit {

namespace {

using filament::IndirectLight;
using filament::Skybox;
using filament::math::mat3f;
using TextureFormat = filament::Texture::InternalFormat;
using TextureUsage = filament::Texture::Usage;

/// How many filtered pictures a renderer keeps, the one lighting the scene
/// included. A 256 reflection chain is 2 MB and a 1024 backdrop 24 MB, so
/// four is at most about a hundred megabytes of GPU memory on a device that
/// chose those sizes — and an editor flicking between a handful of
/// environments filters each once.
constexpr size_t kKeptEnvironments = 4;

/// Samples a texel for the GPU filter: SpecularFilter's own default, and the
/// count cmgen starts its levels at.
constexpr uint16_t kGpuSamples = 1024;

/// Samples a texel for the CPU filter's first two levels, doubling after:
/// cmgen's.
constexpr uint32_t kCpuSamples = 1024;

/// The sizes a picture is filtered at on this device, and which route.
struct EnvironmentPlan {
  /// The reflection cube's side: cmgen's --size.
  uint32_t reflectionSize = 256;
  /// The largest backdrop cube's side; a smaller picture gets a smaller one.
  uint32_t largestSkybox = 1024;
  /// The widest picture uploaded; larger ones are halved first.
  uint32_t widestUpload = 4096;
  /// Filtered by cmgen's arithmetic on the worker rather than on the GPU.
  bool onCpu = false;
  HdrLimits limits;

  std::string key() const {
    return format("%u/%u/%s", reflectionSize, largestSkybox,
                  onCpu ? "cpu" : "gpu");
  }
};

uint32_t floorPowerOfTwo(uint32_t value) {
  uint32_t power = 1;
  while (power <= value / 2) power *= 2;
  return power;
}

/// The rule, from what the renderer measured about the device when it
/// started (OrblitDeviceProfile on the Dart side):
///
///  * The route. The GPU filter renders into half-float cubes and mipmaps a
///    half-float picture, so it runs where ORBLIT_CAPABILITY_HALF_FLOAT_
///    TEXTURES says RGBA16F is renderable and mipmappable: every Metal and
///    Vulkan device, GLES 3 with EXT_color_buffer_(half_)float, WebGL 2 with
///    EXT_color_buffer_float. Elsewhere cmgen's filter runs on the worker.
///    ORBLIT_ENVIRONMENT_FILTER=cpu or =gpu forces one, for checking.
///  * The reflections. `requested` when the scene names a size (a power of
///    two, 16 to 1024 on the GPU, 16 to 128 on the CPU); otherwise 256 at
///    feature level 2 and above — cmgen's default, so a runtime environment
///    matches a default bake — and 128 at feature level 1 (GLES 3.0, WebGL
///    2), where the filter's cost falls fourfold for reflections a mirror
///    shows slightly softer. On the CPU, 64: cmgen's filter at 64 is a
///    fraction of a second on one core, and a device without half-float
///    targets is not one to spend more on.
///  * The backdrop: four times the reflections, so a 256 environment gets a
///    1024 sky, held to 512 below 3 GB of memory and to 256 on the CPU route,
///    and never larger than a quarter of the picture's width.
///  * The upload: the picture halved until it is no wider than eight times
///    the reflections and four times the backdrop need, and than the largest
///    texture the device takes.
///  * The decode: at most a sixteenth of physical memory at 28 bytes a pixel
///    (tinyexr's peak; stb's is lower) — about an 8K picture on 16 GB and a
///    4K one on 4 GB — and 4096 by 2048 where memory is not reported, as in
///    a browser.
EnvironmentPlan planFor(const Renderer &renderer, float requested) {
  EnvironmentPlan plan;
  const int32_t featureLevel =
      renderer.capability(ORBLIT_CAPABILITY_FEATURE_LEVEL);
  const bool halfFloat =
      renderer.capability(ORBLIT_CAPABILITY_HALF_FLOAT_TEXTURES) == 1;
  const int32_t memory =
      renderer.capability(ORBLIT_CAPABILITY_SYSTEM_MEMORY_MEGABYTES);
  const int32_t largestTexture =
      renderer.capability(ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE);

  plan.onCpu = !halfFloat;
  if (const char *forced = std::getenv("ORBLIT_ENVIRONMENT_FILTER")) {
    if (std::strcmp(forced, "cpu") == 0) plan.onCpu = true;
    if (std::strcmp(forced, "gpu") == 0) plan.onCpu = false;
  }

  const uint32_t most = plan.onCpu ? 128 : 1024;
  if (requested >= 1.0f) {
    plan.reflectionSize = std::max<uint32_t>(
        16, std::min(most, floorPowerOfTwo(uint32_t(std::min(requested, 4096.0f)))));
  } else if (plan.onCpu) {
    plan.reflectionSize = 64;
  } else {
    plan.reflectionSize = featureLevel >= 2 ? 256 : 128;
  }

  uint32_t skyCap = memory > 0 && memory < 3000 ? 512 : 1024;
  if (plan.onCpu) skyCap = 256;
  plan.largestSkybox = std::min(skyCap, plan.reflectionSize * 4);

  plan.widestUpload = std::max(plan.largestSkybox * 4, plan.reflectionSize * 8);
  if (largestTexture > 0) {
    plan.widestUpload = std::min(plan.widestUpload, uint32_t(largestTexture));
  }

  if (memory > 0) {
    const uint64_t budget = uint64_t(memory) * 1024 * 1024 / 16 / 28;
    plan.limits.mostPixels = std::max<uint64_t>(
        uint64_t(4096) * 2048, std::min<uint64_t>(budget, uint64_t(16384) * 8192));
    plan.limits.longestSide = 16384;
  } else {
    plan.limits.mostPixels = uint64_t(4096) * 2048;
    plan.limits.longestSide = 8192;
  }
  return plan;
}

std::atomic<uint64_t> &picturesFiltered() {
  static std::atomic<uint64_t> count{0};
  return count;
}

bool coversRequest(const EnvironmentLighting &entry, const EnvironmentPlan &plan,
                   uint64_t hash, bool light, bool sky) {
  return entry.hash == hash && entry.reflectionSize == plan.reflectionSize &&
         entry.largestSkybox == plan.largestSkybox && entry.onCpu == plan.onCpu &&
         (!light || entry.reflections != nullptr) && (!sky || entry.sky != nullptr);
}

ForEach threadedForEach() {
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  return forEachInOrder;
#else
  return [](size_t count, const std::function<void(size_t)> &body) {
    parallelFor(count, body);
  };
#endif
}

uint32_t log2Of(uint32_t powerOfTwo) {
  uint32_t exponent = 0;
  while ((uint32_t(1) << exponent) < powerOfTwo) exponent++;
  return exponent;
}

/// Hands `pixels` to Filament, which frees them once the driver has them.
filament::Texture::PixelBufferDescriptor halfFloats(std::vector<uint16_t> &&pixels) {
  auto *owned = new std::vector<uint16_t>(std::move(pixels));
  return filament::Texture::PixelBufferDescriptor(
      owned->data(), owned->size() * sizeof(uint16_t),
      filament::Texture::Format::RGBA, filament::Texture::Type::HALF,
      [](void *, size_t, void *user) {
        delete static_cast<std::vector<uint16_t> *>(user);
      },
      owned);
}

}  // namespace

uint64_t environmentPicturesFiltered() { return picturesFiltered().load(); }

/// One picture on its way to being light. The worker writes the middle block
/// and then `finished`; the drawing thread reads nothing of it until then,
/// and alone touches the textures.
struct EnvironmentWork {
  // What was asked for, fixed before the worker starts.
  std::string path;
  EnvironmentPlan plan;
  bool wantsLight = false;
  bool wantsSky = false;
  uint64_t generation = 0;
  /// The hashes this renderer already holds lighting for under this plan,
  /// so a picture it has filtered, under any name, is not decoded again.
  std::vector<uint64_t> alreadyFiltered;

  // The worker's answer.
  std::atomic<bool> finished{false};
  std::string note;
  uint64_t hash = 0;
  bool cached = false;
  Harmonics harmonics{};
  uint32_t skyboxSize = 0;
  uint32_t uploadWidth = 0;
  uint32_t uploadHeight = 0;
  std::vector<uint16_t> upload;
  std::vector<std::vector<uint16_t>> levels;
  std::vector<uint16_t> skyFaces;
  double readMilliseconds = 0;
  double decodeMilliseconds = 0;
  double harmonicsMilliseconds = 0;
  double prepareMilliseconds = 0;

  // The drawing thread's half.
  int stage = 0;
  filament::Texture *source = nullptr;
  filament::Texture *sky = nullptr;
  double buildMilliseconds = 0;
  double convertMilliseconds = 0;
};

namespace {

/// Everything a picture needs before the GPU sees it. Runs on a worker, and
/// touches nothing but `work` and the resource store.
void prepare(EnvironmentWork &work) {
  const std::string name = lastPathComponent(work.path);
  double started = now();
  SharedBytes bytes = readResource(work.path);
  if (!bytes || bytes->empty()) {
    work.note = format("%s could not be read.", name.c_str());
    work.finished = true;
    return;
  }
  work.hash = hashBytes(bytes->data(), bytes->size());
  work.readMilliseconds = (now() - started) * 1000;
  if (std::find(work.alreadyFiltered.begin(), work.alreadyFiltered.end(),
                work.hash) != work.alreadyFiltered.end()) {
    work.cached = true;
    work.finished = true;
    return;
  }

  started = now();
  HdrDecoded decoded = decodeHdrImage(bytes->data(), bytes->size(), work.plan.limits);
  bytes.reset();
  work.decodeMilliseconds = (now() - started) * 1000;
  if (!decoded.note.empty()) {
    work.note = name + ": " + decoded.note;
    work.finished = true;
    return;
  }
  HdrImage picture = std::move(decoded.image);
  // cmgen's own test for an equirectangular picture, and the one
  // EquirectangularToCubemap assumes.
  if (picture.width != picture.height * 2) {
    work.note = format("%s is %u by %u. An environment picture is "
                       "equirectangular: exactly twice as wide as it is tall.",
                       name.c_str(), picture.width, picture.height);
    work.finished = true;
    return;
  }

  const ForEach each = threadedForEach();
  const uint32_t reflections = work.plan.reflectionSize;
  work.skyboxSize = std::max<uint32_t>(
      16, std::min(work.plan.largestSkybox, floorPowerOfTwo(picture.width / 4)));

  started = now();
  CpuCubemap cube;
  if (work.wantsLight) {
    cube = mirroredCubemap(cubemapFromEquirectangular(picture, reflections, each), each);
    work.harmonics = irradianceHarmonics(cube, each);
  }
  work.harmonicsMilliseconds = (now() - started) * 1000;

  started = now();
  if (work.plan.onCpu) {
    if (work.wantsLight) {
      std::vector<CpuCubemap> mips;
      makeSeamless(cube);
      mips.push_back(std::move(cube));
      while (mips.back().size > 1) mips.push_back(halvedCubemap(mips.back()));
      for (const CpuCubemap &level : roughnessPrefilter(mips, kCpuSamples, each)) {
        work.levels.push_back(halfFloatRgba(level));
      }
    }
    if (work.wantsSky) {
      work.skyFaces = halfFloatRgba(mirroredCubemap(
          cubemapFromEquirectangular(picture, work.skyboxSize, each), each));
    }
  } else {
    uint32_t widest = 0;
    if (work.wantsLight) widest = std::max(widest, reflections * 8);
    if (work.wantsSky) widest = std::max(widest, work.skyboxSize * 4);
    widest = std::min(widest, work.plan.widestUpload);
    while (picture.width > widest && picture.width >= 4) {
      HdrImage half = halvedImage(picture);
      if (half.empty()) break;
      picture = std::move(half);
    }
    work.uploadWidth = picture.width;
    work.uploadHeight = picture.height;
    work.upload = halfFloatRgba(picture.rgb.get(),
                                size_t(picture.width) * picture.height);
  }
  work.prepareMilliseconds = (now() - started) * 1000;
  work.finished = true;
}

}  // namespace

void Renderer::requestEnvironmentImages(const std::string &radiance,
                                        const std::string &skybox,
                                        float requestedSize) {
  _environmentPublishes++;
  const EnvironmentPlan plan = planFor(*this, requestedSize);

  const auto noteFor = [&](bool light, bool sky, const std::string &words) {
    if (light) _assetNotes["environment"] = words;
    if (sky) _assetNotes["skybox"] = words;
  };

  if (!filament::Texture::isTextureFormatSupported(*_engine, TextureFormat::RGBA16F)) {
    const std::string words =
        "This device cannot hold a floating-point cubemap, so a picture "
        "cannot light the scene here. Bake it with tool/bake_environment.sh "
        "and name the .ktx files it writes instead.";
    noteFor(!radiance.empty(), !skybox.empty(), words);
    return;
  }

  const uint64_t generation = resourceGeneration();
  std::vector<std::string> paths;
  if (!radiance.empty()) paths.push_back(radiance);
  if (!skybox.empty() && skybox != radiance) paths.push_back(skybox);

  for (const std::string &path : paths) {
    const bool light = path == radiance;
    const bool sky = path == skybox;
    const std::string name = lastPathComponent(path);

    bool installed = false;
    const auto known = _environmentNames.find(path + "|" + plan.key());
    if (known != _environmentNames.end()) {
      const bool unchanged = known->second.generation == generation;
      if (unchanged && !known->second.note.empty()) {
        noteFor(light, sky, known->second.note);
        continue;
      }
      if (known->second.note.empty()) {
        for (EnvironmentLighting &entry : _environmentCache) {
          if (coversRequest(entry, plan, known->second.hash, light, sky)) {
            installEnvironment(entry, light, sky);
            installed = true;
            break;
          }
        }
      }
      if (installed && unchanged) continue;
      // Bytes have been provided under some name since this one was read,
      // which is when its own bytes may have changed. What it was lights the
      // scene meanwhile, rather than a frame or two of flat ambient, and a
      // worker hashes it again: the same bytes find the same light, and
      // different ones are filtered and replace it.
    }

    bool pending = false;
    for (const auto &work : _environmentWork) {
      pending = pending || (work->path == path && work->plan.key() == plan.key() &&
                            (work->wantsLight || !light) &&
                            (work->wantsSky || !sky));
    }
    if (!pending) {
      auto work = std::make_shared<EnvironmentWork>();
      work->path = path;
      work->plan = plan;
      work->wantsLight = light;
      work->wantsSky = sky;
      work->generation = generation;
      for (const EnvironmentLighting &entry : _environmentCache) {
        if (coversRequest(entry, plan, entry.hash, light, sky)) {
          work->alreadyFiltered.push_back(entry.hash);
        }
      }
      _environmentWork.push_back(work);
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
      // No threads in this browser build, so the page decodes it, once. A
      // web worker of its own is where this moves; OrblitHdrImage and
      // OrblitEnvironmentBake are written to be compiled into one unchanged.
      prepare(*work);
#else
      bool threaded = false;
      try {
        std::thread([work] { prepare(*work); }).detach();
        threaded = true;
      } catch (const std::exception &) {
        // A thread that will not start is not a reason to go without light.
      }
      if (!threaded) prepare(*work);
#endif
    }
    if (!installed) {
      noteFor(light, sky,
              format("%s is being filtered, and %s once that has finished.",
                     name.c_str(),
                     light ? "lights the scene" : "becomes the backdrop"));
    }
  }
}

/// Puts a filtered picture in charge of the halves asked for.
void Renderer::installEnvironment(const EnvironmentLighting &lighting,
                                  bool light, bool sky) {
  if (light && lighting.reflections != nullptr) {
    if (_environmentLight != nullptr) {
      _scene->setIndirectLight(nullptr);
      _engine->destroy(_environmentLight);
      _environmentLight = nullptr;
    }
    if (_environmentRadiance != nullptr && !_environmentRadianceCached) {
      _engine->destroy(_environmentRadiance);
    }
    _environmentRadiance = lighting.reflections;
    _environmentRadianceCached = true;
    for (int i = 0; i < 9; i++) _environmentHarmonics[i] = lighting.harmonics[i];
    _environmentHasHarmonics = true;
    _environmentLight =
        IndirectLight::Builder()
            .reflections(_environmentRadiance)
            .irradiance(3, _environmentHarmonics)
            .intensity(_environmentParams[0])
            .rotation(mat3f::rotation(_environmentParams[1], float3{0, 1, 0}))
            .build(*_engine);
    _assetNotes.erase("environment");
  }
  if (sky && lighting.sky != nullptr) {
    if (_environmentSkybox != nullptr) {
      if (_skybox != nullptr) _scene->setSkybox(_skybox);
      _engine->destroy(_environmentSkybox);
      _environmentSkybox = nullptr;
    }
    if (_environmentSkyTexture != nullptr && !_environmentSkyCached) {
      _engine->destroy(_environmentSkyTexture);
    }
    _environmentSkyTexture = lighting.sky;
    _environmentSkyCached = true;
    _environmentSkybox = Skybox::Builder()
                             .environment(_environmentSkyTexture)
                             .showSun(false)
                             .build(*_engine);
    _assetNotes.erase("skybox");
  }
  for (EnvironmentLighting &entry : _environmentCache) {
    if (&entry == &lighting) entry.lastUsed = _environmentPublishes;
  }
}

/// Moves any picture that has finished a stage on by one stage, and does at
/// most one stage of GPU work in a frame.
void Renderer::pollEnvironment() {
  if (_environmentWork.empty() || _disposed || _engine == nullptr) return;

  const EnvironmentPlan current = planFor(*this, _environmentParams[3]);
  const auto filtersReady = [this](const EnvironmentWork &work) {
    return _prefilter != nullptr && _equirectangularFilter != nullptr &&
           (!work.wantsLight ||
            (_environmentFilter != nullptr &&
             _environmentFilterLevels == prefilterLevelCount(work.plan.reflectionSize)));
  };

  // Finished work nobody wants now — the scene moved on while it decoded,
  // or a new render graph let go of the environment for a publish — is kept
  // rather than thrown away, two at most, so a scene that names it again
  // picks it up where it stands.
  size_t unwanted = 0;
  for (size_t i = _environmentWork.size(); i-- > 0;) {
    const EnvironmentWork &work = *_environmentWork[i];
    const bool wanted =
        work.plan.key() == current.key() &&
        ((work.wantsLight && work.path == _environmentRadiancePath) ||
         (work.wantsSky && work.path == _environmentSkyboxPath));
    if (!work.finished.load() || work.stage != 0 || wanted) continue;
    if (++unwanted > 2) _environmentWork.erase(_environmentWork.begin() + long(i));
  }

  for (size_t i = 0; i < _environmentWork.size(); i++) {
    const std::shared_ptr<EnvironmentWork> work = _environmentWork[i];
    if (!work->finished.load()) continue;

    const std::string name = lastPathComponent(work->path);
    const bool samePlan = work->plan.key() == current.key();
    const bool light = work->wantsLight && samePlan && work->path == _environmentRadiancePath;
    const bool sky = work->wantsSky && samePlan && work->path == _environmentSkyboxPath;
    const auto forget = [&]() {
      if (work->source != nullptr) _engine->destroy(work->source);
      if (work->sky != nullptr) _engine->destroy(work->sky);
      work->source = work->sky = nullptr;
      _environmentWork.erase(_environmentWork.begin() + long(i));
    };

    if (work->stage == 0) {
      // What the name turned out to be is kept whether or not it is still
      // wanted: a scene that names it again finds the answer here.
      if (_environmentNames.size() > 256) _environmentNames.clear();
      EnvironmentName &known = _environmentNames[work->path + "|" + work->plan.key()];
      known.hash = work->hash;
      known.note = work->note;
      known.generation = work->generation;

      if (!work->note.empty()) {
        if (light) _assetNotes["environment"] = work->note;
        if (sky) _assetNotes["skybox"] = work->note;
        forget();
        return;
      }
      if (!light && !sky) continue;
      if (work->cached) {
        bool installed = false;
        for (EnvironmentLighting &entry : _environmentCache) {
          if (coversRequest(entry, work->plan, work->hash, light, sky)) {
            installEnvironment(entry, light, sky);
            installed = true;
            break;
          }
        }
        forget();
        // Let go of since the worker looked: ask again, from nothing.
        if (!installed) {
          requestEnvironmentImages(light ? _environmentRadiancePath : "",
                                   sky ? _environmentSkyboxPath : "",
                                   _environmentParams[3]);
        }
        showEnvironment();
        return;
      }
    }

    EnvironmentLighting lighting;
    lighting.hash = work->hash;
    lighting.reflectionSize = work->plan.reflectionSize;
    lighting.largestSkybox = work->plan.largestSkybox;
    lighting.onCpu = work->plan.onCpu;
    for (int h = 0; h < 9; h++) {
      lighting.harmonics[h] = float3{work->harmonics[size_t(h) * 3],
                                     work->harmonics[size_t(h) * 3 + 1],
                                     work->harmonics[size_t(h) * 3 + 2]};
    }

    if (work->plan.onCpu) {
      // Filtered on the worker already: an upload, and nothing to render.
      double started = now();
      const uint32_t size = work->plan.reflectionSize;
      if (work->wantsLight && !work->levels.empty()) {
        lighting.reflections = filament::Texture::Builder()
                                   .sampler(filament::Texture::Sampler::SAMPLER_CUBEMAP)
                                   .format(TextureFormat::RGBA16F)
                                   .usage(TextureUsage::DEFAULT)
                                   .width(size)
                                   .height(size)
                                   .levels(uint8_t(work->levels.size()))
                                   .build(*_engine);
        for (size_t level = 0; level < work->levels.size(); level++) {
          const uint32_t side = std::max<uint32_t>(1, size >> level);
          lighting.reflections->setImage(*_engine, level, 0, 0, 0, side, side, 6,
                                         halfFloats(std::move(work->levels[level])));
        }
      }
      if (work->wantsSky && !work->skyFaces.empty()) {
        lighting.sky = filament::Texture::Builder()
                           .sampler(filament::Texture::Sampler::SAMPLER_CUBEMAP)
                           .format(TextureFormat::RGBA16F)
                           .usage(TextureUsage::DEFAULT)
                           .width(work->skyboxSize)
                           .height(work->skyboxSize)
                           .levels(1)
                           .build(*_engine);
        lighting.sky->setImage(*_engine, 0, 0, 0, 0, work->skyboxSize,
                               work->skyboxSize, 6,
                               halfFloats(std::move(work->skyFaces)));
      }
      _engine->flushAndWait();
      log("[orblit] environment %s, filtered on the CPU at %u: read %.0f ms, "
          "decoded %.0f ms, harmonics %.0f ms, filtered %.0f ms (worker); "
          "uploaded %.1f ms (one frame)",
          name.c_str(), size, work->readMilliseconds, work->decodeMilliseconds,
          work->harmonicsMilliseconds, work->prepareMilliseconds,
          (now() - started) * 1000);
    } else if (!filtersReady(*work)) {
      // A frame of its own the first time: building the filters compiles
      // their materials and renders the specular filter's sample kernel,
      // which on a first run is most of what the whole job costs.
      double started = now();
      if (_prefilter == nullptr) _prefilter = new IBLPrefilterContext(*_engine);
      if (_equirectangularFilter == nullptr) {
        _equirectangularFilter =
            new IBLPrefilterContext::EquirectangularToCubemap(*_prefilter);
      }
      const uint8_t levels = uint8_t(prefilterLevelCount(work->plan.reflectionSize));
      if (work->wantsLight &&
          (_environmentFilter == nullptr || _environmentFilterLevels != levels)) {
        delete _environmentFilter;
        IBLPrefilterContext::SpecularFilter::Config config;
        config.sampleCount = kGpuSamples;
        config.levelCount = levels;
        _environmentFilter = new IBLPrefilterContext::SpecularFilter(*_prefilter, config);
        _environmentFilterLevels = levels;
      }
      _engine->flushAndWait();
      work->buildMilliseconds = (now() - started) * 1000;
      return;
    } else if (work->stage == 0) {
      // The picture up, and into cubes.
      double started = now();
      // A half-float cube is what the filter writes; the packed 11/11/10
      // format is half the memory and what cmgen's KTX holds, where it can be
      // rendered into. Mipmappable is the question, because the reflections'
      // source has its levels generated.
      const TextureFormat cubeFormat =
          filament::Texture::isTextureFormatMipmappable(*_engine, TextureFormat::R11F_G11F_B10F)
              ? TextureFormat::R11F_G11F_B10F
              : TextureFormat::RGBA16F;
      const uint8_t pictureLevels =
          uint8_t(std::max(1, std::ilogb(float(work->uploadWidth)) + 1));
      filament::Texture *picture =
          filament::Texture::Builder()
              .sampler(filament::Texture::Sampler::SAMPLER_2D)
              .format(TextureFormat::RGBA16F)
              .usage(TextureUsage::DEFAULT | TextureUsage::GEN_MIPMAPPABLE)
              .width(work->uploadWidth)
              .height(work->uploadHeight)
              .levels(pictureLevels)
              .build(*_engine);
      picture->setImage(*_engine, 0, halfFloats(std::move(work->upload)));
      if (work->wantsLight) {
        const uint32_t size = work->plan.reflectionSize;
        work->source = filament::Texture::Builder()
                           .sampler(filament::Texture::Sampler::SAMPLER_CUBEMAP)
                           .format(cubeFormat)
                           .usage(TextureUsage::COLOR_ATTACHMENT | TextureUsage::SAMPLEABLE |
                                  TextureUsage::GEN_MIPMAPPABLE)
                           .width(size)
                           .height(size)
                           .levels(uint8_t(log2Of(size) + 1))
                           .build(*_engine);
        (*_equirectangularFilter)(picture, work->source);
      }
      if (work->wantsSky) {
        work->sky = filament::Texture::Builder()
                        .sampler(filament::Texture::Sampler::SAMPLER_CUBEMAP)
                        .format(cubeFormat)
                        .usage(TextureUsage::COLOR_ATTACHMENT | TextureUsage::SAMPLEABLE)
                        .width(work->skyboxSize)
                        .height(work->skyboxSize)
                        .levels(1)
                        .build(*_engine);
        (*_equirectangularFilter)(picture, work->sky);
      }
      _engine->destroy(picture);
      // Waited for here rather than at the end of the frame, which would
      // wait for it anyway, so what the stage cost is measured on its own.
      _engine->flushAndWait();
      work->convertMilliseconds = (now() - started) * 1000;
      work->stage = 1;
      return;
    } else {
      // The next frame: the reflections blurred into their chain.
      double started = now();
      if (work->wantsLight && work->source != nullptr) {
        const uint8_t levels = _environmentFilterLevels;
        lighting.reflections =
            filament::Texture::Builder()
                .sampler(filament::Texture::Sampler::SAMPLER_CUBEMAP)
                .format(work->source->getFormat())
                .usage(TextureUsage::COLOR_ATTACHMENT | TextureUsage::SAMPLEABLE)
                .width(work->plan.reflectionSize)
                .height(work->plan.reflectionSize)
                .levels(levels)
                .build(*_engine);
        (*_environmentFilter)(IBLPrefilterContext::SpecularFilter::Options{},
                              work->source, lighting.reflections);
        _engine->destroy(work->source);
        work->source = nullptr;
      }
      lighting.sky = work->sky;
      work->sky = nullptr;
      _engine->flushAndWait();
      log("[orblit] environment %s, filtered on the GPU at %u (backdrop %u): "
          "read %.0f ms, decoded %.0f ms, harmonics %.0f ms, prepared %.0f ms "
          "(worker); filters built %.1f ms, uploaded and converted %.1f ms, "
          "filtered %.1f ms (a frame each)",
          name.c_str(), work->plan.reflectionSize, work->skyboxSize,
          work->readMilliseconds, work->decodeMilliseconds,
          work->harmonicsMilliseconds, work->prepareMilliseconds,
          work->buildMilliseconds, work->convertMilliseconds,
          (now() - started) * 1000);
    }

    picturesFiltered()++;
    _environmentCache.push_back(lighting);
    // Installed only for what is still wanted, but kept either way: it was
    // paid for, and a scene that names it again uses it.
    if (light || sky) {
      installEnvironment(_environmentCache.back(), light, sky);
      showEnvironment();
    }
    forget();

    // Let go of the least recently used beyond the few kept, never one that
    // is lighting the scene now.
    while (_environmentCache.size() > kKeptEnvironments) {
      size_t oldest = _environmentCache.size();
      for (size_t e = 0; e < _environmentCache.size(); e++) {
        const EnvironmentLighting &entry = _environmentCache[e];
        const bool inUse = (entry.reflections != nullptr &&
                            entry.reflections == _environmentRadiance) ||
                           (entry.sky != nullptr && entry.sky == _environmentSkyTexture);
        if (inUse) continue;
        if (oldest == _environmentCache.size() ||
            entry.lastUsed < _environmentCache[oldest].lastUsed) {
          oldest = e;
        }
      }
      if (oldest == _environmentCache.size()) break;
      if (_environmentCache[oldest].reflections != nullptr) {
        _engine->destroy(_environmentCache[oldest].reflections);
      }
      if (_environmentCache[oldest].sky != nullptr) {
        _engine->destroy(_environmentCache[oldest].sky);
      }
      _environmentCache.erase(_environmentCache.begin() + long(oldest));
    }
    return;
  }
}

/// Everything filtered, and the filters, given back: for a renderer that is
/// going away. The environment is released first, so nothing still points at
/// what this destroys.
void Renderer::releaseEnvironmentCache() {
  if (_engine == nullptr) return;
  releaseEnvironment();
  for (const auto &work : _environmentWork) {
    if (work->source != nullptr) _engine->destroy(work->source);
    if (work->sky != nullptr) _engine->destroy(work->sky);
    work->source = work->sky = nullptr;
  }
  _environmentWork.clear();
  for (const EnvironmentLighting &entry : _environmentCache) {
    if (entry.reflections != nullptr) _engine->destroy(entry.reflections);
    if (entry.sky != nullptr) _engine->destroy(entry.sky);
  }
  _environmentCache.clear();
  delete _environmentFilter;
  _environmentFilter = nullptr;
  delete _equirectangularFilter;
  _equirectangularFilter = nullptr;
}

}  // namespace orblit
