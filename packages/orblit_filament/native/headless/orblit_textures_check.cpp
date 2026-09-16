// Textures through the GPU: formats, sibling choice, sizes and the upload
// budget, measured in pixels and in what the queue says it did.
//
// Two halves. The first drives orblit::TextureQueue (OrblitTextures.h) on an
// engine of its own, where what a pump uploaded can be counted exactly: that
// a large texture's levels spread over several frames under a budget, that a
// low limit makes a texture smaller, that a cooked set is chosen by the
// format each file actually holds, and that an owner's textures can be
// forgotten part-way. The second draws through the C ABI, as a host does,
// and reads the colour back: ASTC, BC7, BC1 and ETC2 each sample as the
// colour they were written with, sRGB and linear differ as they should, the
// best sibling wins, a texture on its way shows its own smallest levels
// sharpening and never anything else, and one that never arrives shows the
// transparent black it was made holding.
//
// Every fixture is written in memory (ktx2_fixtures.h). Real Basis files are
// read from ORBLIT_KTX2_SAMPLES when it is set.
//
//   build/orblit_textures_check          the checks, run by build.sh test
//   build/orblit_textures_check bench    the load measurement: 400 BC7
//                                        textures against Basis, frame by
//                                        frame; Basis from ORBLIT_BISTRO

#include "OrblitKtx2.h"
#include "OrblitTextures.h"
#include "orblit_renderer.h"

#include <dirent.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iterator>
#include <random>
#include <string>
#include <thread>
#include <vector>

#include <filament/Engine.h>
#include <filament/Texture.h>
#include <gltfio/TextureProvider.h>

#include "ktx2_fixtures.h"

namespace {

using fixtures::Rgba;
using filament::Engine;
using filament::Texture;

int failures = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

double seconds() {
  return std::chrono::duration<double>(
             std::chrono::steady_clock::now().time_since_epoch())
      .count();
}

void provide(const std::string &name, const std::vector<uint8_t> &bytes) {
  orblit_renderer_provide_resource(name.c_str(), bytes.data(), bytes.size());
}

std::vector<uint8_t> readFile(const std::string &path) {
  std::ifstream in(path, std::ios::binary);
  return std::vector<uint8_t>((std::istreambuf_iterator<char>(in)),
                              std::istreambuf_iterator<char>());
}

/// Every level a different colour, so which level is sampled can be read off
/// a pixel. All channels odd, so BC7 carries them exactly.
Rgba levelColour(uint32_t level) {
  static const Rgba kColours[] = {
      {201, 41, 41, 255},  {41, 201, 41, 255},   {41, 41, 201, 255},
      {201, 201, 41, 255}, {41, 201, 201, 255},  {201, 41, 201, 255},
      {121, 121, 41, 255}, {41, 121, 121, 255},  {121, 41, 121, 255},
      {241, 121, 41, 255}, {41, 241, 121, 255},  {121, 41, 241, 255},
      {241, 241, 241, 255}};
  return kColours[level % (sizeof kColours / sizeof kColours[0])];
}

/// BC7 blocks of every colour at once: each block's endpoints random, so
/// zstd finds as little to squeeze as it would in a real texture.
std::vector<uint8_t> noisyBc7(uint32_t width, uint32_t height,
                              std::mt19937 &random) {
  const size_t blocks = fixtures::blocksIn(width, height, 4, 4);
  std::vector<uint8_t> level(blocks * 16);
  for (size_t i = 0; i < blocks; i++) {
    fixtures::bc7Block(level.data() + i * 16,
                       {uint8_t(random()), uint8_t(random()),
                        uint8_t(random()), 255});
    // Indices too.
    for (int b = 9; b < 16; b++) level[i * 16 + b] = uint8_t(random());
  }
  return level;
}

std::vector<uint8_t> noisyBc7File(uint32_t side, bool zstd, uint32_t seed) {
  std::mt19937 random(seed);
  fixtures::File file;
  file.kind = fixtures::bc7(true);
  file.width = file.height = side;
  file.zstd = zstd;
  for (uint32_t s = side;; s /= 2) {
    file.levels.push_back(noisyBc7(s, s, random));
    if (s == 1) break;
  }
  return fixtures::write(file);
}

// ---- The queue, on an engine of its own ----

struct Pumped {
  uint32_t pumps = 0;
  uint32_t withUploads = 0;
  uint32_t uploads = 0;
  uint64_t bytes = 0;
  uint64_t mostBytes = 0;
  bool overBudgetWithMoreThanOne = false;
};

/// Pumps until nothing is outstanding, as frames would, and says what each
/// pump did.
Pumped pumpUntilDone(Engine &engine, orblit::TextureQueue &queue,
                     uint64_t budget, bool verbose = false) {
  Pumped done;
  while (queue.outstanding() > 0 && done.pumps < 10000) {
    queue.pump();
    engine.flushAndWait();
    const orblit::TextureQueue::Frames frame = queue.frames();
    done.pumps++;
    if (frame.lastUploads > 0) {
      done.withUploads++;
      done.uploads += frame.lastUploads;
      done.bytes += frame.lastBytes;
      done.mostBytes = std::max(done.mostBytes, frame.lastBytes);
      if (verbose) {
        printf("  pump %u: %u level(s), %llu KB\n", done.pumps,
               frame.lastUploads, (unsigned long long)(frame.lastBytes / 1024));
      }
      if (budget != 0 && frame.lastUploads > 1 && frame.lastBytes > budget) {
        done.overBudgetWithMoreThanOne = true;
      }
    }
  }
  return done;
}

void destroyPopped(Engine &engine, orblit::TextureQueue &queue,
                   const void *client, std::vector<std::string> *failures) {
  orblit::TextureQueue::Popped popped;
  while (queue.pop(client, popped)) {
    if (failures != nullptr && !popped.failure.empty()) {
      failures->push_back(popped.failure);
    }
    engine.destroy(popped.texture);
  }
}

struct Supported {
  /// Linear ASTC. Filament's Metal backend maps no sRGB ASTC format at all,
  /// so on Apple the two differ.
  bool astc = false;
  bool astcSrgb = false;
  bool bc7 = false;
  bool bc1 = false;
  bool etc2 = false;
};

Supported theQueueCountsWhatItDoes() {
  Engine *engine = Engine::create(Engine::Backend::METAL);
  expect(engine != nullptr, "an engine starts for the queue");
  Supported supported;
  if (engine == nullptr) return supported;
  static const int kClient = 0;
  const void *client = &kClient;

  {
    orblit::TextureQueue queue(*engine, 16384, 8);
    const auto has = [&](uint32_t vk) {
      return queue.supports(*orblit::ktx2::formatOf(vk));
    };
    supported.astc = has(157);
    supported.astcSrgb = has(158);
    supported.bc7 = has(146) && has(145);
    supported.bc1 = queue.sampledAs(*orblit::ktx2::formatOf(132), 1, true) !=
                    nullptr;
    supported.etc2 = has(152) && has(151) && has(148);
    printf("textures: this device samples ASTC linear %d sRGB %d, BC7 %d, BC1 "
           "RGB %d RGBA %d, BC5 %d, ETC2 %d, EAC RG11 %d, RGB8 %d, RGBA16F %d\n",
           supported.astc, supported.astcSrgb, supported.bc7, has(132),
           has(134), has(141), supported.etc2, has(155), has(23), has(97));

    // A 4096² BC7 texture, every level of it, squeezed.
    const std::vector<uint8_t> large = noisyBc7File(4096, true, 1);
    orblit::TextureQueue::Request request;
    request.data = large.data();
    request.size = large.size();
    request.srgb = true;
    request.name = "large";
    request.client = client;
    std::string why;

    struct Budget {
      uint64_t bytes;
      uint32_t expectedPumps;
      const char *what;
    };
    // Levels of 16 MB, 4 MB, 1 MB and 1.33 MB of everything smaller.
    const Budget budgets[] = {
        {4u << 20, 3, "4 MB"},
        {1u << 20, 4, "1 MB"},
        {1, 13, "one byte"},
        {0, 1, "no budget"},
    };
    for (const Budget &budget : budgets) {
      queue.setLimits(0, budget.bytes);
      Texture *texture = queue.push(request, why);
      expect(texture != nullptr, "a large BC7 texture is made: " + why);
      if (texture == nullptr) continue;
      // Decoded first, so what is counted is the budget and not the decoder.
      queue.waitForDecoding(client);
      const Pumped pumped = pumpUntilDone(*engine, queue, budget.bytes);
      printf("textures: 4096² BC7 under %s: %u level(s) over %u frame(s), at "
             "most %llu KB in one\n",
             budget.what, pumped.uploads, pumped.withUploads,
             (unsigned long long)(pumped.mostBytes / 1024));
      expect(pumped.uploads == 13, std::string("every level goes up under ") +
                                       budget.what);
      expect(pumped.withUploads == budget.expectedPumps,
             std::string("and takes the frames it should under ") +
                 budget.what + ": " + std::to_string(pumped.withUploads));
      expect(!pumped.overBudgetWithMoreThanOne,
             std::string("no frame goes over ") + budget.what +
                 " except to upload a single level");
      std::vector<std::string> failed;
      destroyPopped(*engine, queue, client, &failed);
      expect(failed.empty(), "and it arrives whole");
    }

    // A limit leaves the largest levels out: never built, never uploaded.
    queue.setLimits(1024, 0);
    Texture *limited = queue.push(request, why);
    expect(limited != nullptr && limited->getWidth() == 1024 &&
               limited->getHeight() == 1024 && limited->getLevels() == 11,
           "a 4096 texture under a 1024 limit is made 1024 with 11 levels");
    queue.waitForDecoding(client);
    const Pumped smaller = pumpUntilDone(*engine, queue, 0);
    printf("textures: 4096² BC7 under a 1024 limit: %u level(s), %llu KB "
           "uploaded\n",
           smaller.uploads, (unsigned long long)(smaller.bytes / 1024));
    expect(smaller.uploads == 11 && smaller.bytes < (2u << 20),
           "and only its eleven smaller levels go up");
    destroyPopped(*engine, queue, client, nullptr);

    // A picture has no levels to leave out, so it is halved as it decodes.
    static const uint8_t kWhitePng[] = {
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00,
        0x0d, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00,
        0x00, 0x02, 0x08, 0x06, 0x00, 0x00, 0x00, 0x72, 0xb6, 0x0d, 0x24,
        0x00, 0x00, 0x00, 0x0e, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9c, 0x63,
        0xf8, 0x0f, 0x05, 0x0c, 0x30, 0x06, 0x00, 0x8f, 0x82, 0x0f, 0xf1,
        0x3c, 0xa5, 0x56, 0x51, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e,
        0x44, 0xae, 0x42, 0x60, 0x82};
    queue.setLimits(1, 0);
    orblit::TextureQueue::Request png;
    png.data = kWhitePng;
    png.size = sizeof kWhitePng;
    png.client = client;
    Texture *halved = queue.push(png, why);
    expect(halved != nullptr && halved->getWidth() == 1,
           "a 2x2 PNG under a limit of 1 is made 1x1");
    queue.waitForDecoding(client);
    pumpUntilDone(*engine, queue, 0);
    std::vector<std::string> pngFailed;
    destroyPopped(*engine, queue, client, &pngFailed);
    expect(pngFailed.empty(), "and decodes");

    // Basis, with its largest levels left out by rewriting the file.
    if (const char *dir = getenv("ORBLIT_KTX2_SAMPLES")) {
      const std::vector<uint8_t> albedo =
          readFile(std::string(dir) + "/albedo.ktx2");
      if (!albedo.empty()) {
        queue.setLimits(256, 0);
        orblit::TextureQueue::Request basis;
        basis.data = albedo.data();
        basis.size = albedo.size();
        basis.srgb = true;
        basis.client = client;
        Texture *made = queue.push(basis, why);
        expect(made != nullptr, "a Basis file with mipmaps loads: " + why);
        if (made != nullptr) {
          printf("textures: Basis albedo.ktx2 1024² under a 256 limit made "
                 "%zux%zu, %zu levels\n",
                 made->getWidth(), made->getHeight(), made->getLevels());
          expect(made->getWidth() == 256 && made->getLevels() == 9,
                 "and under a 256 limit is made 256 with nine levels");
          queue.waitForDecoding(client);
          pumpUntilDone(*engine, queue, 0);
          std::vector<std::string> basisFailed;
          destroyPopped(*engine, queue, client, &basisFailed);
          expect(basisFailed.empty(),
                 "and transcodes: " +
                     (basisFailed.empty() ? "" : basisFailed[0]));
        }
      }
    }

    // Refusals come back as reasons, and nothing is made.
    queue.setLimits(0, 0);
    std::vector<uint8_t> truncated(large.begin(), large.begin() + 1000);
    orblit::TextureQueue::Request cut = request;
    cut.data = truncated.data();
    cut.size = truncated.size();
    expect(queue.push(cut, why) == nullptr && !why.empty(),
           "a truncated file is refused with a reason: " + why);
    orblit::TextureQueue::Request junk = request;
    const uint8_t kJunk[64] = {1, 2, 3};
    junk.data = kJunk;
    junk.size = sizeof kJunk;
    expect(queue.push(junk, why) == nullptr && !why.empty(),
           "bytes that are no image are refused: " + why);

    // An owner forgotten part-way: nothing of it is uploaded afterwards, and
    // the counts still add up.
    static const int kOther = 0;
    const std::vector<uint8_t> medium = noisyBc7File(512, true, 2);
    std::vector<Texture *> mine;
    std::vector<Texture *> theirs;
    const size_t pushedBefore = queue.pushedCount(client);
    const size_t poppedBefore = queue.poppedCount(client);
    for (int i = 0; i < 30; i++) {
      orblit::TextureQueue::Request owned = request;
      owned.data = medium.data();
      owned.size = medium.size();
      owned.owner = i < 20 ? client : &kOther;
      Texture *texture = queue.push(owned, why);
      (i < 20 ? mine : theirs).push_back(texture);
    }
    queue.setLimits(0, 64 * 1024);
    queue.pump();
    queue.forget(client);
    for (Texture *texture : mine) engine->destroy(texture);
    expect(queue.outstanding() <= 10, "forgetting an owner drops its textures");
    pumpUntilDone(*engine, queue, 64 * 1024);
    size_t arrived = 0;
    orblit::TextureQueue::Popped popped;
    while (queue.pop(client, popped)) {
      expect(std::find(theirs.begin(), theirs.end(), popped.texture) !=
                 theirs.end(),
             "only the other owner's textures arrive");
      arrived++;
    }
    for (Texture *texture : theirs) engine->destroy(texture);
    expect(arrived == 10, "all ten of the other owner's arrive");
    expect(queue.pushedCount(client) - pushedBefore ==
               queue.poppedCount(client) - poppedBefore,
           "and every texture pushed is counted popped, the forgotten ones "
           "too, so a loader waiting on the counts is not left waiting");
    queue.shutdown();
  }

  // Siblings, chosen by what each file holds. Provided by name, as a host
  // without a file system provides them.
  {
    orblit::TextureQueue queue(*engine, 16384, 8);
    const auto green = [](uint32_t) { return Rgba{41, 201, 41, 255}; };
    const auto file = [&](const fixtures::Kind &kind) {
      return fixtures::write(fixtures::solid(kind, 16, 16, 5, true, green));
    };
    constexpr int kLinear = 0;
    constexpr int kSrgb = 1;

    provide("cooked/a.ktx2", file(fixtures::rgba8(false)));
    provide("cooked/a.astc.ktx2", file(fixtures::astc4x4(false)));
    provide("cooked/a.bc.ktx2", file(fixtures::bc7(false)));
    std::string chosen;
    queue.readCooked("cooked/a.ktx2", &chosen, kLinear);
    const std::string best = supported.astc  ? "cooked/a.astc.ktx2"
                             : supported.bc7 ? "cooked/a.bc.ktx2"
                                             : "cooked/a.ktx2";
    expect(chosen == best, "the best sibling is chosen: " + chosen);

    // The name says ASTC and the file holds BC7: what it holds decides.
    provide("cooked/b.ktx2", file(fixtures::rgba8(false)));
    provide("cooked/b.astc.ktx2", file(fixtures::bc7(false)));
    queue.readCooked("cooked/b.ktx2", &chosen, kLinear);
    expect(chosen == (supported.astc && supported.bc7 ? "cooked/b.astc.ktx2"
                                                      : "cooked/b.ktx2"),
           "a sibling is judged by its format, not its name: " + chosen);

    // sRGB ASTC, which Filament's Metal backend does not sample, and a
    // sibling that is not KTX 2 at all, are passed over for the next.
    provide("cooked/c.ktx2", file(fixtures::rgba8(true)));
    provide("cooked/c.astc.ktx2", file(fixtures::astc4x4(true)));
    provide("cooked/c.bc.ktx2", std::vector<uint8_t>(200, 7));
    provide("cooked/c.etc2.ktx2", file(fixtures::etc2(true)));
    queue.readCooked("cooked/c.ktx2", &chosen, kSrgb);
    if (!supported.astcSrgb && supported.etc2) {
      expect(chosen == "cooked/c.etc2.ktx2",
             "an unsampleable and a broken sibling are passed over for the "
             "next: " + chosen);
    }

    // Linear ASTC used as colour could only draw in the wrong colour space
    // here, so the BC7 after it is taken instead; for a map read as numbers
    // the ASTC is right.
    provide("cooked/e.ktx2", file(fixtures::rgba8(true)));
    provide("cooked/e.astc.ktx2", file(fixtures::astc4x4(false)));
    provide("cooked/e.bc.ktx2", file(fixtures::bc7(false)));
    queue.readCooked("cooked/e.ktx2", &chosen, kSrgb);
    if (supported.astc && !supported.astcSrgb && supported.bc7) {
      expect(chosen == "cooked/e.bc.ktx2",
             "a sibling that would draw in the wrong colour space is passed "
             "over: " + chosen);
    }
    queue.readCooked("cooked/e.ktx2", &chosen, kLinear);
    if (supported.astc) {
      expect(chosen == "cooked/e.astc.ktx2",
             "and taken where the colour space does not matter: " + chosen);
    }

    // Remembered, until something is provided.
    provide("cooked/d.ktx2", file(fixtures::rgba8(false)));
    queue.readCooked("cooked/d.ktx2", &chosen, kLinear);
    expect(chosen == "cooked/d.ktx2", "with no siblings the set is itself");
    provide("cooked/d.bc.ktx2", file(fixtures::bc7(false)));
    queue.readCooked("cooked/d.ktx2", &chosen, kLinear);
    expect(chosen == (supported.bc7 ? "cooked/d.bc.ktx2" : "cooked/d.ktx2"),
           "a sibling provided later is found, because the generation "
           "moved: " + chosen);
    queue.readCooked("cooked/png.png", &chosen, kLinear);
    expect(chosen == "cooked/png.png", "anything else is read as it is");
  }

  Engine::destroy(&engine);
  return supported;
}

// ---- Through the C ABI, in pixels ----

constexpr uint32_t kWidth = 64;
constexpr uint32_t kHeight = 48;

orblit_renderer *start() {
  orblit_surface_desc headless = {ORBLIT_SURFACE_HEADLESS, nullptr};
  orblit_renderer *renderer = orblit_renderer_create(
      ORBLIT_BACKEND_DEFAULT, &headless, kWidth, kHeight);
  if (renderer == nullptr) return nullptr;
  // As the sprite test sets it up: the linear tone mapper, no dithering or
  // anti-aliasing, an exposure of one, a black sky, looking straight on.
  float post[49] = {};
  post[0] = 1.0f;
  post[33] = 3.0f;
  post[35] = post[36] = post[37] = 1.0f;
  for (int i = 40; i < 49; i++) post[i] = 1.0f;
  orblit_renderer_set_post_process(renderer, post, 49);
  orblit_renderer_set_exposure(renderer, 1.0f, 1.0f, 100.0f);
  const float black[3] = {0, 0, 0};
  orblit_renderer_set_sky_colour(renderer, black, 0.0f, 0);
  const float from[3] = {0, 0, 10};
  const float look[3] = {0, 0, 0};
  orblit_renderer_set_camera(renderer, from, look, 50.0f, 1, 10.0f, 0.0);
  // Nothing but the sprite: the placeholder cube is taken away.
  orblit_renderer_apply_objects(renderer, 0, nullptr, nullptr, 0, nullptr, 0,
                               nullptr, nullptr, nullptr, nullptr, nullptr, 0,
                               nullptr, 0);
  return renderer;
}

/// The texture limits, in a pipeline block that is otherwise the defaults.
void limits(orblit_renderer *renderer, float side, float kilobytes) {
  float params[30] = {1, 0,   1024, 2, 0,   0.5f, 0.001f, 1, 0, 1,
                      0, 1,   1,    0.9f, 1, 6,  5,      100, 0, 0,
                      0, 1,   0,    0, 0.15f, 1, 0.3f,   8, side, kilobytes};
  orblit_renderer_set_pipeline(renderer, params, 30);
}

/// One sprite covering the view, of the image at `path`.
void sprite(orblit_renderer *renderer, const std::string &path, bool linear,
            int32_t revision) {
  const int32_t key = 1;
  const int32_t flags = 1 | (linear ? 4 : 0);
  const int32_t order = 0;
  float params[20] = {};
  params[0] = params[5] = params[10] = params[15] = 1.0f;
  params[16] = params[17] = params[18] = params[19] = 1.0f;
  const char *paths[1] = {path.c_str()};
  const float record[16] = {0, 0, 5, 0, 40, 40, 0.5f, 0.5f,
                            0, 0, 1, 1, 1,  1,  1,    1};
  const int32_t changed = key;
  const int32_t count = 1;
  orblit_renderer_apply_sprites(renderer, 1, &key, &flags, &order, &revision,
                               params, 20, paths, 1, &changed, &count, 1,
                               record, 16);
}

struct Colour {
  int r = -1, g = -1, b = -1;
  bool read() const { return r >= 0; }
};

/// The middle of the next frame captured, `after` frames on.
Colour capture(orblit_renderer *renderer, int after = 3) {
  orblit_renderer_request_capture(renderer);
  for (int i = 0; i < after; i++) orblit_renderer_draw(renderer, 1.0);
  uint32_t width = 0;
  uint32_t height = 0;
  const size_t bytes =
      orblit_renderer_read_capture(renderer, nullptr, 0, &width, &height);
  Colour colour;
  if (bytes == 0) return colour;
  std::vector<uint8_t> pixels(bytes);
  orblit_renderer_read_capture(renderer, pixels.data(), bytes, &width, &height);
  const uint8_t *middle =
      pixels.data() + (size_t(height / 2) * width + width / 2) * 4;
  colour.r = middle[0];
  colour.g = middle[1];
  colour.b = middle[2];
  return colour;
}

/// Draws long enough for a small texture to decode and arrive, then reads.
Colour settled(orblit_renderer *renderer) {
  for (int i = 0; i < 20; i++) {
    orblit_renderer_draw(renderer, 1.0);
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
  }
  return capture(renderer, 6);
}

bool near(const Colour &c, int r, int g, int b, int by = 3) {
  return c.read() && std::abs(c.r - r) <= by && std::abs(c.g - g) <= by &&
         std::abs(c.b - b) <= by;
}

std::string said(const Colour &c) {
  return std::to_string(c.r) + "/" + std::to_string(c.g) + "/" +
         std::to_string(c.b);
}

bool noted(orblit_renderer *renderer, const std::string &about,
           const char *saying) {
  const uint32_t count = orblit_renderer_notes(renderer);
  for (uint32_t i = 0; i < count; i++) {
    const char *a = nullptr;
    const char *s = nullptr;
    if (orblit_renderer_note(renderer, i, &a, &s) == ORBLIT_OK && a && s &&
        about == a && strstr(s, saying) != nullptr) {
      return true;
    }
  }
  return false;
}

/// What an sRGB-encoded byte reads as when a texture says it is linear, once
/// the frame encodes it back to sRGB.
int linearReadAsSrgb(int value) {
  const double c = value / 255.0;
  const double s = c <= 0.0031308 ? c * 12.92
                                  : 1.055 * std::pow(c, 1.0 / 2.4) - 0.055;
  return int(std::lround(s * 255.0));
}

void formatsDrawTheirColours(const Supported &supported) {
  orblit_renderer *renderer = start();
  expect(renderer != nullptr, "a renderer starts for the formats");
  if (renderer == nullptr) return;

  const Rgba colour = {201, 61, 121, 255};
  struct Case {
    const char *what;
    fixtures::Kind kind;
    bool zstd;
    bool sampled;
    /// Written in, and sampled as, linear rather than sRGB.
    bool linear;
    int r, g, b;
  };
  const int bc1r = ((201 >> 3) << 3) | ((201 >> 3) >> 2);
  const int bc1g = ((61 >> 2) << 2) | ((61 >> 2) >> 4);
  const int bc1b = ((121 >> 3) << 3) | ((121 >> 3) >> 2);
  // ASTC is linear here because Filament's Metal backend has no sRGB ASTC.
  const Case cases[] = {
      {"ASTC 4x4, zstd", fixtures::astc4x4(false), true, supported.astc, true,
       201, 61, 121},
      {"ASTC 4x4", fixtures::astc4x4(false), false, supported.astc, true, 201,
       61, 121},
      {"BC7, zstd", fixtures::bc7(true), true, supported.bc7, false, 201, 61,
       121},
      {"BC7", fixtures::bc7(true), false, supported.bc7, false, 201, 61, 121},
      {"BC1 without alpha", fixtures::bc1(true), false, supported.bc1, false,
       bc1r, bc1g, bc1b},
      {"ETC2 RGB8, zstd", fixtures::etc2(true), true, supported.etc2, false,
       fixtures::etc2Drawn(201), fixtures::etc2Drawn(61),
       fixtures::etc2Drawn(121)},
      {"ETC2 RGBA8", fixtures::etc2Rgba(true), false, supported.etc2, false,
       fixtures::etc2Drawn(201), fixtures::etc2Drawn(61),
       fixtures::etc2Drawn(121)},
      {"RGBA8, zstd", fixtures::rgba8(true), true, true, false, 201, 61, 121},
  };
  int revision = 0;
  for (const Case &c : cases) {
    if (!c.sampled) {
      printf("textures: skipped %s, which this device does not sample\n",
             c.what);
      continue;
    }
    const std::string path = std::string("formats/") + c.what + ".ktx2";
    provide(path, fixtures::write(fixtures::solid(
                      c.kind, 32, 32, 6, c.zstd,
                      [&](uint32_t) { return colour; })));
    sprite(renderer, path, c.linear, revision++);
    const Colour got = settled(renderer);
    const int r = c.linear ? linearReadAsSrgb(c.r) : c.r;
    const int g = c.linear ? linearReadAsSrgb(c.g) : c.g;
    const int b = c.linear ? linearReadAsSrgb(c.b) : c.b;
    printf("textures: %s draws %s, expected %d/%d/%d\n", c.what,
           said(got).c_str(), r, g, b);
    expect(near(got, r, g, b),
           std::string(c.what) + " draws the colour it was written with");
  }

  // sRGB ASTC, where the device has none, is refused and said so.
  if (!supported.astcSrgb) {
    const std::string path = "formats/astc-srgb.ktx2";
    provide(path, fixtures::write(fixtures::solid(
                      fixtures::astc4x4(true), 32, 32, 6, true,
                      [&](uint32_t) { return colour; })));
    sprite(renderer, path, false, revision++);
    settled(renderer);
    expect(noted(renderer, path, "cannot sample ASTC_4x4_SRGB_BLOCK"),
           "an sRGB ASTC texture on a device without it is noted");
  }

  // The same grey, read as the colour it is and as a number: sRGB is the
  // byte back again, linear is the byte taken as light and encoded.
  const Rgba grey = {129, 129, 129, 255};
  if (supported.bc7) {
    const std::string path = "formats/grey.ktx2";
    provide(path,
            fixtures::write(fixtures::solid(fixtures::bc7(false), 16, 16, 5,
                                            true,
                                            [&](uint32_t) { return grey; })));
    sprite(renderer, path, false, revision++);
    const Colour asColour = settled(renderer);
    sprite(renderer, path, true, revision++);
    const Colour asNumber = settled(renderer);
    const int expected = linearReadAsSrgb(129);
    printf("textures: a linear BC7 file of 129 grey draws %s sampled as sRGB "
           "and %s sampled as linear (expected 129 and %d)\n",
           said(asColour).c_str(), said(asNumber).c_str(), expected);
    expect(near(asColour, 129, 129, 129),
           "a linear-cooked file asked for as colour is read as sRGB, "
           "because the blocks are the same");
    expect(near(asNumber, expected, expected, expected),
           "and asked for as a number is read as linear");
  }
  orblit_renderer_destroy(renderer);
}

void theBestSiblingDraws(const Supported &supported) {
  orblit_renderer *renderer = start();
  if (renderer == nullptr) return;
  const auto solid = [](const fixtures::Kind &kind, Rgba colour) {
    return fixtures::write(fixtures::solid(kind, 16, 16, 5, true,
                                           [=](uint32_t) { return colour; }));
  };
  const Rgba blue = {41, 41, 201, 255};
  const Rgba green = {41, 201, 41, 255};
  const Rgba red = {201, 41, 41, 255};
  const Rgba yellow = {204, 204, 34, 255};
  const Rgba magenta = {201, 41, 201, 255};
  const auto asLinear = [](Rgba c) {
    return Rgba{uint8_t(linearReadAsSrgb(c.r)), uint8_t(linearReadAsSrgb(c.g)),
                uint8_t(linearReadAsSrgb(c.b)), 255};
  };

  // Read as numbers, where this device has ASTC.
  provide("sib/a.ktx2", solid(fixtures::rgba8(false), blue));
  provide("sib/a.astc.ktx2", solid(fixtures::astc4x4(false), green));
  provide("sib/a.bc.ktx2", solid(fixtures::bc7(false), red));
  provide("sib/a.etc2.ktx2", solid(fixtures::etc2(false), yellow));
  provide("sib/b.ktx2", solid(fixtures::rgba8(false), blue));
  provide("sib/b.bc.ktx2", solid(fixtures::bc7(false), red));
  provide("sib/b.etc2.ktx2", solid(fixtures::etc2(false), yellow));
  provide("sib/d.ktx2", solid(fixtures::rgba8(false), blue));
  provide("sib/d.astc.ktx2", solid(fixtures::bc7(false), magenta));
  provide("sib/d.bc.ktx2", solid(fixtures::bc7(false), red));
  // Read as colour, where it has no sRGB ASTC: one sibling it cannot sample
  // at all, and one it could only sample in the wrong colour space.
  provide("sib/c.ktx2", solid(fixtures::rgba8(true), blue));
  provide("sib/c.astc.ktx2", solid(fixtures::astc4x4(true), green));
  provide("sib/c.bc.ktx2", solid(fixtures::bc7(true), red));
  provide("sib/e.ktx2", solid(fixtures::rgba8(true), blue));
  provide("sib/e.astc.ktx2", solid(fixtures::astc4x4(false), green));
  provide("sib/e.bc.ktx2", solid(fixtures::bc7(true), red));

  int revision = 0;
  const auto draw = [&](const char *path, bool linear) {
    sprite(renderer, path, linear, revision++);
    return settled(renderer);
  };
  const Colour a = draw("sib/a.ktx2", true);
  const Colour b = draw("sib/b.ktx2", true);
  const Colour d = draw("sib/d.ktx2", true);
  const Colour c = draw("sib/c.ktx2", false);
  const Colour e = draw("sib/e.ktx2", false);
  printf("textures: siblings draw %s with all three, %s without ASTC, %s "
         "where the ASTC name holds BC7, %s past an unsampleable sRGB ASTC, "
         "%s past a linear ASTC used as colour\n",
         said(a).c_str(), said(b).c_str(), said(d).c_str(), said(c).c_str(),
         said(e).c_str());
  const Rgba g = asLinear(green);
  const Rgba r = asLinear(red);
  const Rgba m = asLinear(magenta);
  if (supported.astc && supported.bc7) {
    expect(near(a, g.r, g.g, g.b), "ASTC is chosen first");
    expect(near(b, r.r, r.g, r.b), "BC next");
    expect(near(d, m.r, m.g, m.b),
           "a sibling is judged by what it holds, not what it is called");
  }
  if (!supported.astcSrgb && supported.bc7) {
    expect(near(c, 201, 41, 41),
           "a sibling the device cannot sample falls through to the next");
    expect(near(e, 201, 41, 41),
           "and so does one it could sample only in the wrong colour space");
  }
  orblit_renderer_destroy(renderer);
}

void aLimitDrawsASmallerLevel(const Supported &supported) {
  if (!supported.bc7) return;
  orblit_renderer *renderer = start();
  if (renderer == nullptr) return;
  const auto file = fixtures::write(
      fixtures::solid(fixtures::bc7(true), 64, 64, 7, true, levelColour));
  provide("limit/full.ktx2", file);
  provide("limit/small.ktx2", file);

  sprite(renderer, "limit/full.ktx2", false, 0);
  const Colour full = settled(renderer);
  limits(renderer, 16, 0);
  sprite(renderer, "limit/small.ktx2", false, 1);
  const Colour small = settled(renderer);
  const Rgba level0 = levelColour(0);
  const Rgba level2 = levelColour(2);
  printf("textures: a 64² texture draws %s, and under a 16 limit %s (levels 0 "
         "and 2 are %d/%d/%d and %d/%d/%d)\n",
         said(full).c_str(), said(small).c_str(), level0.r, level0.g, level0.b,
         level2.r, level2.g, level2.b);
  expect(near(full, level0.r, level0.g, level0.b),
         "with no limit the largest level draws");
  expect(near(small, level2.r, level2.g, level2.b),
         "under a 16 limit the 16² level is the largest there is");
  orblit_renderer_destroy(renderer);
}

void aTextureOnItsWayShowsItsOwnSmallerLevels(const Supported &supported) {
  if (!supported.bc7) return;
  orblit_renderer *renderer = start();
  if (renderer == nullptr) return;
  // One kilobyte a frame: the levels up to 32² fit in the first frame, and
  // every larger one takes a frame of its own.
  limits(renderer, 0, 1);
  provide("arriving/grid.ktx2",
          fixtures::write(fixtures::solid(fixtures::bc7(true), 1024, 1024, 11,
                                          true, levelColour)));
  sprite(renderer, "arriving/grid.ktx2", false, 0);
  // Decoded before the first frame, so what is watched is the upload.
  std::this_thread::sleep_for(std::chrono::milliseconds(300));

  std::vector<int> seen;
  bool garbage = false;
  std::string sequence;
  for (int frame = 0; frame < 30; frame++) {
    const Colour colour = capture(renderer, 1);
    if (!colour.read()) continue;
    int level = -1;
    for (int l = 0; l < 11; l++) {
      const Rgba expected = levelColour(uint32_t(l));
      if (near(colour, expected.r, expected.g, expected.b)) level = l;
    }
    const bool black = near(colour, 0, 0, 0);
    if (level < 0 && !black) garbage = true;
    sequence += (sequence.empty() ? "" : " ") +
                (level >= 0 ? "L" + std::to_string(level)
                            : black ? std::string("black") : said(colour));
    if (level >= 0 && (seen.empty() || seen.back() != level)) {
      seen.push_back(level);
    }
    if (level == 0) break;
  }
  printf("textures: a 1024² texture arriving a kilobyte a frame, captured "
         "frame by frame: %s\n",
         sequence.c_str());
  expect(!garbage, "every frame shows one of its own levels, or nothing");
  expect(!seen.empty() && seen.back() == 0, "and it arrives whole");
  expect(std::is_sorted(seen.rbegin(), seen.rend()) && seen.size() >= 3,
         "sharpening a level at a time, never back");
  expect(!seen.empty() && seen.front() > 0,
         "starting from a smaller level, not the largest");
  orblit_renderer_destroy(renderer);
}

void aTextureThatNeverArrivesShowsItsPlaceholder(const Supported &supported) {
  orblit_renderer *renderer = start();
  if (renderer == nullptr) return;
  // Every level's zstd frame damaged, so decoding fails at the smallest and
  // not one level is ever uploaded.
  const auto broken = [](const fixtures::Kind &kind, uint32_t levels) {
    fixtures::File file = fixtures::solid(
        kind, 64, 64, levels, true,
        [](uint32_t) { return Rgba{201, 201, 201, 255}; });
    std::vector<uint8_t> bytes = fixtures::write(file);
    orblit::ktx2::Header header;
    orblit::ktx2::read(bytes.data(), bytes.size(), header);
    for (const auto &level : header.index) {
      for (uint64_t i = 0; i < level.length; i++) {
        bytes[size_t(level.offset + i)] = 0xEE;
      }
    }
    return bytes;
  };
  struct Case {
    const char *what;
    fixtures::Kind kind;
    uint32_t levels;
    bool sampled;
  };
  const Case cases[] = {
      // Linear, because Filament's Metal backend has no sRGB ASTC.
      {"ASTC with mipmaps", fixtures::astc4x4(false), 7, supported.astc},
      {"ASTC of one level", fixtures::astc4x4(false), 1, supported.astc},
      {"BC7 with mipmaps", fixtures::bc7(true), 7, supported.bc7},
      {"ETC2 RGBA8 of one level", fixtures::etc2Rgba(true), 1,
       supported.etc2},
      {"RGBA8 with mipmaps", fixtures::rgba8(true), 7, true},
  };
  int revision = 0;
  for (const Case &c : cases) {
    if (!c.sampled) continue;
    const std::string path = std::string("never/") + c.what + ".ktx2";
    provide(path, broken(c.kind, c.levels));
    const bool linear = c.kind.vkFormat == 157;
    sprite(renderer, path, linear, revision++);
    const Colour got = settled(renderer);
    printf("textures: %s that never arrives draws %s\n", c.what,
           said(got).c_str());
    expect(near(got, 0, 0, 0, 2),
           std::string(c.what) +
               " that never arrives draws its transparent black placeholder, "
               "not magenta or old memory");
    expect(noted(renderer, path, "arrived only in part"),
           std::string("and ") + c.what + " is noted");
  }

  // Refused outright: never made, and said so.
  std::vector<uint8_t> cut = fixtures::write(fixtures::solid(
      fixtures::rgba8(true), 64, 64, 7, true,
      [](uint32_t) { return Rgba{1, 1, 1, 255}; }));
  cut.resize(cut.size() / 3);
  provide("never/cut.ktx2", cut);
  sprite(renderer, "never/cut.ktx2", false, revision++);
  settled(renderer);
  expect(noted(renderer, "never/cut.ktx2", "could not be loaded"),
         "a truncated texture is refused with a note");
  orblit_renderer_destroy(renderer);
}

// ---- The measurement ----

struct Arrival {
  double pushCpuMs = 0;
  double pushGpuMs = 0;
  double totalMs = 0;
  uint32_t frames = 0;
  double worstMs = 0;
  double worstCpuMs = 0;
  double p99Ms = 0;
  double p50Ms = 0;
  uint64_t bytes = 0;
  size_t made = 0;
};

/// A frame at sixty a second: what is left of 16.6 ms after `took`, slept, so
/// the decoders have the time between frames they would have in an app.
void paceFrom(double took) {
  const double left = 16.6 - took;
  if (left > 0) {
    std::this_thread::sleep_for(std::chrono::microseconds(int(left * 1000)));
  }
}

void percentiles(std::vector<double> frames, Arrival &arrival) {
  std::sort(frames.begin(), frames.end());
  if (frames.empty()) return;
  arrival.p99Ms = frames[frames.size() * 99 / 100];
  arrival.p50Ms = frames[frames.size() / 2];
}

/// Pushes everything as a loader does, in one go, then pumps frame by frame
/// with each frame's GPU work waited for, until the queue is empty. The push
/// is reported on its own: it is the frame a model's resources begin in.
Arrival measureQueue(Engine &engine, const std::vector<orblit::SharedBytes> &files,
                     const std::vector<bool> &srgb, uint64_t budget) {
  orblit::TextureQueue queue(engine, 16384, 14);
  queue.setLimits(0, budget);
  static const int kClient = 0;
  std::vector<Texture *> made;
  Arrival arrival;
  const double from = seconds();
  for (size_t i = 0; i < files.size(); i++) {
    orblit::TextureQueue::Request request;
    request.shared = files[i];
    request.srgb = srgb[i];
    request.client = &kClient;
    std::string why;
    if (Texture *texture = queue.push(request, why)) made.push_back(texture);
  }
  const double pushed = seconds();
  engine.flushAndWait();
  const double flushed = seconds();
  arrival.pushCpuMs = (pushed - from) * 1000;
  arrival.pushGpuMs = (flushed - pushed) * 1000;
  arrival.made = made.size();

  std::vector<double> frames;
  while (queue.outstanding() > 0) {
    const double at = seconds();
    queue.pump();
    const double pumped = seconds();
    engine.flushAndWait();
    const double took = (seconds() - at) * 1000;
    frames.push_back(took);
    arrival.bytes += queue.frames().lastBytes;
    arrival.worstMs = std::max(arrival.worstMs, took);
    arrival.worstCpuMs = std::max(arrival.worstCpuMs, (pumped - at) * 1000);
    paceFrom(took);
  }
  arrival.totalMs = (seconds() - from) * 1000;
  arrival.frames = uint32_t(frames.size());
  percentiles(frames, arrival);
  orblit::TextureQueue::Popped popped;
  while (queue.pop(&kClient, popped)) {}
  queue.shutdown();
  for (Texture *texture : made) engine.destroy(texture);
  engine.flushAndWait();
  return arrival;
}

/// gltfio's own providers, as the renderer had them: every texture uploaded
/// in the frame its decoding is found finished.
Arrival measureStock(Engine &engine, const std::vector<orblit::SharedBytes> &files,
                     const std::vector<bool> &srgb, bool basis) {
  filament::gltfio::TextureProvider *provider =
      basis ? filament::gltfio::createKtx2Provider(&engine)
            : filament::gltfio::createStbProvider(&engine);
  std::vector<Texture *> made;
  Arrival arrival;
  const double from = seconds();
  for (size_t i = 0; i < files.size(); i++) {
    using Flags = filament::gltfio::TextureProvider::TextureFlags;
    if (Texture *texture = provider->pushTexture(
            files[i]->data(), files[i]->size(),
            basis ? "image/ktx2" : "image/png",
            srgb[i] ? Flags::sRGB : Flags::NONE)) {
      made.push_back(texture);
    }
  }
  const double pushed = seconds();
  engine.flushAndWait();
  const double flushed = seconds();
  arrival.pushCpuMs = (pushed - from) * 1000;
  arrival.pushGpuMs = (flushed - pushed) * 1000;
  arrival.made = made.size();
  std::vector<double> frames;
  while (provider->getPoppedCount() < provider->getPushedCount()) {
    const double at = seconds();
    provider->updateQueue();
    while (provider->popTexture() != nullptr) {}
    const double pumped = seconds();
    engine.flushAndWait();
    const double took = (seconds() - at) * 1000;
    frames.push_back(took);
    arrival.worstMs = std::max(arrival.worstMs, took);
    arrival.worstCpuMs = std::max(arrival.worstCpuMs, (pumped - at) * 1000);
    paceFrom(took);
  }
  arrival.totalMs = (seconds() - from) * 1000;
  arrival.frames = uint32_t(frames.size());
  percentiles(frames, arrival);
  delete provider;
  for (Texture *texture : made) engine.destroy(texture);
  engine.flushAndWait();
  return arrival;
}

void report(const char *what, const Arrival &arrival) {
  printf("  %-38s %3zu made; push %5.0f ms + GPU %4.0f ms; arrived in %6.0f ms "
         "over %4u frames; frame worst %6.1f ms (of it on this thread %5.1f), "
         "p99 %5.1f, median %4.1f\n",
         what, arrival.made, arrival.pushCpuMs, arrival.pushGpuMs,
         arrival.totalMs, arrival.frames, arrival.worstMs, arrival.worstCpuMs,
         arrival.p99Ms, arrival.p50Ms);
}

int bench() {
  Engine *engine = Engine::create(Engine::Backend::METAL);
  if (engine == nullptr) return 1;
  printf("textures bench: a frame is a pump and the GPU work it asked for, "
         "waited for; frames paced at 60 Hz; the push is every texture "
         "created at once, as a model's load does\n");

  // Sixteen different files, each pushed count/16 times: decoding and
  // uploading cost the same whether or not two textures share their bytes,
  // and writing four hundred of them block by block would take minutes.
  std::vector<orblit::SharedBytes> distinct;
  for (uint32_t i = 0; i < 16; i++) {
    distinct.push_back(std::make_shared<const std::vector<uint8_t>>(
        noisyBc7File(2048, true, 100 + i)));
  }

  // What a megabyte costs, from a short sweep over 32 textures.
  if (getenv("ORBLIT_BENCH_SWEEP") != nullptr) {
    std::vector<orblit::SharedBytes> few;
    for (uint32_t i = 0; i < 32; i++) few.push_back(distinct[i % 16]);
    const std::vector<bool> srgb(few.size(), true);
    for (uint64_t megabytes : {1, 2, 4, 8, 16, 32, 64}) {
      const Arrival arrival =
          measureQueue(*engine, few, srgb, megabytes << 20);
      printf("  sweep: %2llu MB a frame: p99 %5.1f ms, median %4.1f ms, worst "
             "%5.1f ms, %4u frames\n",
             (unsigned long long)megabytes, arrival.p99Ms, arrival.p50Ms,
             arrival.worstMs, arrival.frames);
    }
  }

  const uint32_t count =
      getenv("ORBLIT_BENCH_COUNT") ? uint32_t(atoi(getenv("ORBLIT_BENCH_COUNT")))
                                   : 400;
  std::vector<orblit::SharedBytes> bc7;
  for (uint32_t i = 0; i < count; i++) bc7.push_back(distinct[i % 16]);
  const std::vector<bool> bc7Srgb(bc7.size(), true);
  printf("  %u 2048² BC7 textures with full mipmaps, zstd, %zu MB a file\n",
         count, distinct[0]->size() >> 20);
  if (const char *list = getenv("ORBLIT_BENCH_MB")) {
    // Budgets given by hand, in megabytes, comma-separated.
    for (const char *at = list; *at != '\0';) {
      const uint64_t megabytes = strtoull(at, nullptr, 10);
      char what[64];
      snprintf(what, sizeof what, "BC7, %llu MB a frame",
               (unsigned long long)megabytes);
      report(what, measureQueue(*engine, bc7, bc7Srgb, megabytes << 20));
      const char *comma = strchr(at, ',');
      at = comma != nullptr ? comma + 1 : at + strlen(at);
    }
  } else {
    for (int tier = 2; tier >= 0; tier--) {
      const uint64_t budget = uint64_t(orblit::kTierUploadKilobytes[tier])
                              << 10;
      char what[64];
      snprintf(what, sizeof what, "BC7, %s tier's %llu MB a frame",
               tier == 2 ? "high" : tier == 1 ? "medium" : "low",
               (unsigned long long)(budget >> 20));
      report(what, measureQueue(*engine, bc7, bc7Srgb, budget));
    }
    report("BC7, no budget", measureQueue(*engine, bc7, bc7Srgb, 0));
  }

  const char *bistro = getenv("ORBLIT_BISTRO");
  if (bistro != nullptr) {
    std::vector<orblit::SharedBytes> basis;
    std::vector<bool> basisSrgb;
    if (DIR *listing = opendir(bistro)) {
      while (dirent *entry = readdir(listing)) {
        const std::string name = entry->d_name;
        if (name.size() > 5 && name.substr(name.size() - 5) == ".ktx2") {
          auto bytes = std::make_shared<const std::vector<uint8_t>>(
              readFile(std::string(bistro) + "/" + name));
          orblit::ktx2::Header header;
          orblit::ktx2::read(bytes->data(), bytes->size(), header);
          basis.push_back(bytes);
          // As the file says it is, so Basis's check of the transfer
          // function passes for every one of them.
          basisSrgb.push_back(header.transfer != orblit::ktx2::Transfer::linear);
        }
      }
      closedir(listing);
    }
    printf("  %zu Basis textures from ORBLIT_BISTRO\n", basis.size());
    report("Basis, gltfio's own provider (before)",
           measureStock(*engine, basis, basisSrgb, true));
    report("Basis, the queue at the high tier's 8 MB",
           measureQueue(*engine, basis, basisSrgb,
                        uint64_t(orblit::kTierUploadKilobytes[2]) << 10));
    report("Basis, the queue with no budget",
           measureQueue(*engine, basis, basisSrgb, 0));
  } else {
    printf("  ORBLIT_BISTRO is not set, so Basis was not measured\n");
  }
  Engine::destroy(&engine);
  return 0;
}
}  // namespace

int main(int argc, char **argv) {
  if (argc > 1 && strcmp(argv[1], "bench") == 0) return bench();

  const Supported supported = theQueueCountsWhatItDoes();
  formatsDrawTheirColours(supported);
  theBestSiblingDraws(supported);
  aLimitDrawsASmallerLevel(supported);
  aTextureOnItsWayShowsItsOwnSmallerLevels(supported);
  aTextureThatNeverArrivesShowsItsPlaceholder(supported);

  if (failures > 0) {
    fprintf(stderr, "orblit_textures_check: %d failed\n", failures);
    return 1;
  }
  printf("orblit_textures_check: passed\n");
  return 0;
}
