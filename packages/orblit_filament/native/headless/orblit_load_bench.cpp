// What loading a real model does to the frames around it, measured frame by
// frame through the C ABI — the same calls a gallery makes, on one thread,
// at sixty frames a second.
//
//   ORBLIT_BISTRO=<dir> build/orblit_load_bench [label]
//
// The directory holds BistroExterior.gltf and whatever textures it names —
// Basis, or a cooked set — and bistro_ibl.ktx and bistro_skybox.ktx if they
// were built. Draws a few frames of an empty street, publishes the model, and
// keeps drawing until the renderer says every file has arrived, then a
// hundred and twenty frames more to see what the scene costs on its own.
//
// Nothing here reaches past orblit_renderer.h, so the same file builds
// against any version of the renderer, including one from before the texture
// queue existed: that is what makes a before and an after comparable. What
// the renderer says as it loads is read off its own standard error, stamped
// with the frame it was said in, and that is how a long frame is labelled and
// how arrival is seen — "files decoded in", which every version says.
//
// Switches:
//   ORBLIT_LOAD_WIDTH, ORBLIT_LOAD_HEIGHT   the frame, 1280 by 800
//   ORBLIT_LOAD_AFTER                       frames after arrival, 120
//   ORBLIT_LOAD_TIMEOUT                     seconds to wait for arrival, 60
//   ORBLIT_LOAD_ECHO=1                      pass the renderer's lines through
//   ORBLIT_LOAD_EVERY=1                     print every frame, not the long ones
//   ORBLIT_LOAD_UPLOAD_KB                   the upload budget; nought, the
//                                           device's own

#include "orblit_renderer.h"

#include <unistd.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace {

double now() {
  return std::chrono::duration<double>(
             std::chrono::steady_clock::now().time_since_epoch())
      .count();
}

int number(const char *name, int fallback) {
  const char *value = getenv(name);
  return value != nullptr ? atoi(value) : fallback;
}

/// The renderer's standard error, read on a thread of its own and stamped
/// with when each line arrived.
class Said {
 public:
  struct Line {
    double at;
    std::string text;
  };

  void start(bool echo) {
    _echo = echo;
    _original = dup(STDERR_FILENO);
    int ends[2];
    if (pipe(ends) != 0) return;
    dup2(ends[1], STDERR_FILENO);
    close(ends[1]);
    _read = ends[0];
    _thread = std::thread([this] { run(); });
  }

  void stop() {
    if (_read < 0) return;
    fflush(stderr);
    dup2(_original, STDERR_FILENO);
    _thread.join();
    close(_read);
    _read = -1;
  }

  /// Lines said since the last call.
  std::vector<Line> take() {
    std::lock_guard<std::mutex> hold(_lock);
    std::vector<Line> taken;
    taken.swap(_lines);
    return taken;
  }

 private:
  void run() {
    std::string partial;
    char buffer[4096];
    for (;;) {
      const ssize_t got = read(_read, buffer, sizeof buffer);
      if (got <= 0) break;
      if (_echo) {
        [[maybe_unused]] ssize_t written = write(_original, buffer, size_t(got));
      }
      const double at = now();
      partial.append(buffer, size_t(got));
      size_t end;
      while ((end = partial.find('\n')) != std::string::npos) {
        std::lock_guard<std::mutex> hold(_lock);
        _lines.push_back({at, partial.substr(0, end)});
        partial.erase(0, end + 1);
      }
    }
  }

  bool _echo = false;
  int _original = -1;
  int _read = -1;
  std::thread _thread;
  std::mutex _lock;
  std::vector<Line> _lines;
};

struct Frame {
  double from;
  double milliseconds;
  /// The renderer's own count of frames when this one began.
  uint64_t number;
  std::vector<std::string> said;
};

bool contains(const std::string &text, const char *part) {
  return text.find(part) != std::string::npos;
}

/// Whether a line is worth printing beside a frame: the renderer's own, and
/// not the thousands a flood of per-file lines would be.
bool worthSaying(const std::string &text) {
  return contains(text, "[orblit]");
}

}  // namespace

int main(int argc, char **argv) {
  const char *label = argc > 1 ? argv[1] : "load";
  const char *dir = getenv("ORBLIT_BISTRO");
  if (dir == nullptr) {
    fprintf(stderr, "orblit_load_bench: set ORBLIT_BISTRO\n");
    return 1;
  }
  const std::string model = std::string(dir) + "/BistroExterior.gltf";
  if (access(model.c_str(), R_OK) != 0) {
    fprintf(stderr, "orblit_load_bench: no %s\n", model.c_str());
    return 1;
  }
  const uint32_t width = uint32_t(number("ORBLIT_LOAD_WIDTH", 1280));
  const uint32_t height = uint32_t(number("ORBLIT_LOAD_HEIGHT", 800));
  const int after = number("ORBLIT_LOAD_AFTER", 120);
  const double timeout = number("ORBLIT_LOAD_TIMEOUT", 60);
  const bool every = number("ORBLIT_LOAD_EVERY", 0) != 0;

  Said said;
  said.start(number("ORBLIT_LOAD_ECHO", 0) != 0);

  orblit_surface_desc headless = {ORBLIT_SURFACE_HEADLESS, nullptr};
  orblit_renderer *renderer =
      orblit_renderer_create(ORBLIT_BACKEND_DEFAULT, &headless, width, height);
  if (renderer == nullptr) {
    said.stop();
    fprintf(stderr, "orblit_load_bench: no renderer\n");
    return 1;
  }

  // Roughly the gallery's daylight Bistro: a sun, the photographed sky when
  // it was built, four samples and soft shadows over three cascades.
  const float sky[3] = {0.30f, 0.45f, 0.70f};
  orblit_renderer_set_sky_colour(renderer, sky, 22000.0f, 0);
  {
    const int64_t key = 1;
    const int32_t kind = 0;
    const int32_t flags = 1;
    const float sun[22] = {1.0f, 0.95f, 0.88f, 100000.0f, 0, 0, 0, -0.45f,
                           -0.82f, -0.35f, 0, 0, 0, 0.53f, 0.1f, 10.0f, 80.0f,
                           0, 0, 0, 0, 0};
    orblit_renderer_apply_lights(renderer, 1, &key, &kind, &flags, sun, 22);
  }
  const std::string radiance = std::string(dir) + "/bistro_ibl.ktx";
  const std::string skybox = std::string(dir) + "/bistro_skybox.ktx";
  if (access(radiance.c_str(), R_OK) == 0) {
    const float environment[4] = {30000.0f, 0.0f, 1.0f, 0.0f};
    orblit_renderer_set_environment(
        renderer, radiance.c_str(),
        access(skybox.c_str(), R_OK) == 0 ? skybox.c_str() : "", environment,
        4);
  }
  {
    // The pipeline block as OrblitPipeline packs it for the example, with
    // the texture limits after it left at the device's own. A renderer from
    // before those two floats reads the first twenty-eight.
    float pipeline[30] = {1,    1,    1024, 3,     120, 0.5f, 0.001f, 1,
                          0,    1.2f, 1,    0.6f,  1,   0.9f, 4,      6,
                          5,    100,  0,    0,     0,   1,    0,      0,
                          0.15f, 1,   0.3f, 8,     0,   0};
    pipeline[29] = float(number("ORBLIT_LOAD_UPLOAD_KB", 0));
    orblit_renderer_set_pipeline(renderer, pipeline, 30);
  }
  const float eye[3] = {-4.0f, 1.7f, -12.0f};
  const float look[3] = {4.0f, 1.2f, -4.0f};
  orblit_renderer_set_camera(renderer, eye, look, 65.0f, 0, 10.0f, 0.0);
  orblit_renderer_set_exposure(renderer, 16.0f, 1.0f / 125.0f, 100.0f);

  std::vector<Frame> frames;
  const auto draw = [&](double seconds) {
    Frame frame;
    frame.number = orblit_renderer_rendered_frames(renderer);
    frame.from = now();
    orblit_renderer_draw(renderer, seconds);
    frame.milliseconds = (now() - frame.from) * 1000.0;
    frames.push_back(frame);
    // Paced as a display would pace it, so decoding has the time between
    // frames it would have in an application.
    const double left = 1.0 / 60.0 - (now() - frame.from);
    if (left > 0) {
      std::this_thread::sleep_for(std::chrono::duration<double>(left));
    }
  };

  // An empty street first, so the engine's own first frames are not counted
  // as the model's.
  for (int i = 0; i < 30; i++) draw(1.0);
  said.take();
  frames.clear();

  const int64_t key = 1;
  const float identity[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};
  const float grey[3] = {0.8f, 0.8f, 0.8f};
  const int32_t mesh = 0;
  // Hidden until ORBLIT_LOAD_REVEAL frames after arrival when that is set:
  // which frame a texture's memory is really made in, the one that writes it
  // or the one that first draws it, is told apart that way.
  const int reveal = number("ORBLIT_LOAD_REVEAL", -1);
  const int32_t flags = reveal >= 0 ? 3 : 7;
  const int32_t material = -1;
  const int32_t morphs = 0;
  const float weight = 0;
  const char *paths[1] = {model.c_str()};
  const double published = now();
  orblit_renderer_apply_objects(renderer, 1, &key, identity, 16, grey, 3,
                               &mesh, &flags, &material, &morphs, &weight, 0,
                               paths, 1);
  const double publishMs = (now() - published) * 1000.0;

  double arrivedAt = 0;
  size_t arrivedFrame = 0;
  std::vector<Said::Line> pending;
  std::vector<std::string> summary;
  const auto attach = [&]() {
    for (Said::Line &line : said.take()) pending.push_back(std::move(line));
    // A line belongs to the last frame that started before it was said.
    for (Said::Line &line : pending) {
      size_t owner = frames.size();
      // A renderer that numbers its slow frames says which one it means;
      // anything else belongs to the last frame begun before it was read.
      const size_t numbered = line.text.find("trace: frame ");
      if (numbered != std::string::npos) {
        const uint64_t which =
            strtoull(line.text.c_str() + numbered + 13, nullptr, 10);
        for (size_t i = frames.size(); i-- > 0;) {
          if (frames[i].number == which) {
            owner = i;
            break;
          }
        }
      } else {
        for (size_t i = frames.size(); i-- > 0;) {
          if (frames[i].from <= line.at) {
            owner = i;
            break;
          }
        }
      }
      if (contains(line.text, "files decoded in") && arrivedAt == 0) {
        arrivedAt = line.at;
        arrivedFrame = owner;
      }
      if (owner < frames.size()) {
        if (worthSaying(line.text)) frames[owner].said.push_back(line.text);
      }
      if (contains(line.text, "texture(s) arrived") ||
          contains(line.text, "files decoded in") ||
          contains(line.text, "decoded on")) {
        summary.push_back(line.text);
      }
    }
    pending.clear();
  };

  size_t publishLines = 0;
  size_t passedOver = 0;
  {
    // What the publish itself said, before any frame.
    for (Said::Line &line : said.take()) {
      if (contains(line.text, "passed over")) passedOver++;
      if (worthSaying(line.text) && !contains(line.text, "passed over")) {
        if (publishLines++ < 6) {
          printf("  [publish] %s\n", line.text.c_str());
        }
      }
    }
  }

  while (arrivedAt == 0 && now() - published < timeout) {
    draw(1.0);
    std::this_thread::sleep_for(std::chrono::milliseconds(0));
    attach();
  }
  const size_t arrivalFrames = frames.size();
  for (int i = 0; i < after; i++) {
    if (reveal >= 0 && i == reveal) {
      const int32_t shown = 7;
      const double from = now();
      orblit_renderer_apply_objects(renderer, 1, &key, identity, 16, grey, 3,
                                   &mesh, &shown, &material, &morphs, &weight,
                                   0, paths, 1);
      printf("  revealed at frame %zu, the publish taking %.1f ms\n",
             frames.size(), (now() - from) * 1000.0);
    }
    draw(1.0);
  }
  // Let the reader catch the last lines.
  std::this_thread::sleep_for(std::chrono::milliseconds(50));
  attach();

  said.stop();

  // The scene on its own: the median of the frames after arrival.
  std::vector<double> settled;
  for (size_t i = arrivalFrames; i < frames.size(); i++) {
    settled.push_back(frames[i].milliseconds);
  }
  std::sort(settled.begin(), settled.end());
  const double sceneMs = settled.empty() ? 0 : settled[settled.size() / 2];
  const auto settledAt = [&](double p) {
    return settled.empty()
               ? 0.0
               : settled[std::min(settled.size() - 1,
                                  size_t(double(settled.size()) * p))];
  };

  double longest = 0;
  size_t longestAt = 0;
  double total = 0;
  for (size_t i = 0; i < arrivalFrames && i < frames.size(); i++) {
    total += frames[i].milliseconds;
    if (frames[i].milliseconds > longest) {
      longest = frames[i].milliseconds;
      longestAt = i;
    }
  }
  std::vector<double> loading;
  for (size_t i = 0; i < arrivalFrames; i++) {
    loading.push_back(frames[i].milliseconds);
  }
  std::sort(loading.begin(), loading.end());
  const auto percentile = [&](double p) {
    return loading.empty()
               ? 0.0
               : loading[std::min(loading.size() - 1,
                                  size_t(double(loading.size()) * p))];
  };
  size_t overBudget = 0;
  for (double ms : loading) {
    if (ms > 2 * std::max(sceneMs, 16.7)) overBudget++;
  }

  printf("%s: publish %.0f ms; arrived %.0f ms after the publish, over %zu "
         "frames; longest frame %.0f ms (frame %zu), p99 %.1f, p90 %.1f, "
         "median %.1f; %zu frames over twice the scene's; the scene alone "
         "%.1f ms a frame; %zu siblings passed over\n",
         label, publishMs,
         arrivedAt > 0 ? (arrivedAt - published) * 1000.0 : -1.0,
         arrivalFrames, longest, longestAt, percentile(0.99),
         percentile(0.90), percentile(0.5), overBudget, sceneMs, passedOver);
  printf("  after arrival: median %.1f ms, p90 %.1f, p99 %.1f, longest %.1f\n",
         sceneMs, settledAt(0.9), settledAt(0.99),
         settled.empty() ? 0.0 : settled.back());
  if (arrivedAt == 0) printf("  never arrived within %.0f s\n", timeout);
  for (const std::string &text : summary) {
    const size_t at = text.find("[orblit]");
    printf("  %s\n", text.c_str() + (at != std::string::npos ? at : 0));
  }

  const double threshold = std::max(50.0, 3.0 * sceneMs);
  for (size_t i = 0; i < frames.size(); i++) {
    const Frame &frame = frames[i];
    const bool long_ = frame.milliseconds > threshold;
    if (!every && !long_) continue;
    printf("  frame %4zu at %6.0f ms: %7.1f ms%s\n", i,
           (frame.from - published) * 1000.0, frame.milliseconds,
           i == arrivedFrame && arrivedAt > 0 ? "  (arrived)" : "");
    size_t shown = 0;
    for (const std::string &text : frame.said) {
      if (contains(text, "passed over")) continue;
      if (shown++ >= 4) break;
      printf("      %s\n", text.c_str() + (text.find("[orblit]") != std::string::npos
                                               ? text.find("[orblit]")
                                               : 0));
    }
  }
  (void)total;
  orblit_renderer_destroy(renderer);
  return 0;
}
