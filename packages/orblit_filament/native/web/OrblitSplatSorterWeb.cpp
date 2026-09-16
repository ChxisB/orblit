/* Sorting Gaussian splats in a browser: a Web Worker where native has a thread.
 *
 * A browser build without -pthread cannot start a std::thread — its
 * constructor throws "Not supported" — and OrblitSplats.cpp's sorter started
 * one for every cloud, so the first scene with splats in it stopped the web
 * renderer outright. This is the sorter makeSplatSorter hands out there
 * instead: the same request, busy and take, answered by the worker in
 * orblit_splat_worker.js, which build.sh passes to emcc as --pre-js.
 *
 * Why a worker and not pthreads. Threads in a browser need the page served
 * cross-origin isolated, and every object in the link — Filament's own
 * archives included — rebuilt with -pthread, which is a second Filament build
 * and a second renderer to ship beside this one. A sort is the one job here
 * that wants a thread, and it shares nothing but the positions, which cross
 * once. So it goes to a worker, and every page gets it, isolated or not.
 *
 * Kept beside the web build rather than in the shared sources, for the reason
 * OrblitSurfaceWeb.cpp is: nothing else compiles it, and only this build has
 * EM_JS.
 */

#include "OrblitSplats.h"

#include <emscripten/emscripten.h>

#include <chrono>
#include <cstdio>
#include <cstring>
#include <utility>

// Each crosses into Module.orblitSplatWorkers. Pointers are shifted with >>>
// rather than >>, so an address past two gigabytes stays positive.

EM_JS(int, orblit_splat_worker_start, (const float *positions, uint32_t count), {
  const workers = Module['orblitSplatWorkers'];
  if (!workers) return 0;
  const at = positions >>> 2;
  return workers.start(HEAPF32.subarray(at, at + count * 3), count);
});

EM_JS(void, orblit_splat_worker_request,
      (int worker, const float *numbers, int flags), {
  const at = numbers >>> 2;
  Module['orblitSplatWorkers'].request(worker, HEAPF32.slice(at, at + 35), flags);
});

EM_JS(int, orblit_splat_worker_state, (int worker), {
  return Module['orblitSplatWorkers'].state(worker);
});

EM_JS(int, orblit_splat_worker_ready, (int worker), {
  return Module['orblitSplatWorkers'].ready(worker);
});

EM_JS(double, orblit_splat_worker_take, (int worker, uint32_t *into), {
  return Module['orblitSplatWorkers'].take(worker, HEAPU32, into >>> 2);
});

EM_JS(void, orblit_splat_worker_give_up, (int worker), {
  Module['orblitSplatWorkers'].giveUp(worker);
});

EM_JS(void, orblit_splat_worker_stop, (int worker), {
  Module['orblitSplatWorkers'].stop(worker);
});

namespace orblit {
namespace {

/// The camera as the worker reads it: the direction, then the view and clip
/// matrices. Matches the offsets in orblit_splat_worker.js.
constexpr size_t kRequestFloats = 3 + 16 + 16;

/// state()'s answers, as orblit_splat_worker.js gives them.
constexpr int kWaiting = 1;
constexpr int kFailed = 2;

/// How long a worker is given to answer before the sort is done here instead.
///
/// A worker that is merely busy answers in milliseconds — a million splats is
/// tens of them — so half a second means something else: a page that will not
/// schedule it, a tab in the background, a worker the browser has quietly
/// stopped, or a headless run whose clock is not the worker's. The frame this
/// costs is worth more than a cloud left in the wrong order until whenever.
constexpr double kPatienceSeconds = 0.5;

double secondsSince(std::chrono::steady_clock::time_point from) {
  return std::chrono::duration<double>(std::chrono::steady_clock::now() - from)
      .count();
}

class WorkerSplatSorter final : public SplatSorter {
 public:
  WorkerSplatSorter(int worker,
                    std::shared_ptr<const std::vector<float>> positions,
                    uint32_t count)
      : _worker(worker), _positions(std::move(positions)), _count(count) {}

  ~WorkerSplatSorter() override { orblit_splat_worker_stop(_worker); }

  void request(const SplatSortRequest &request) override {
    // Kept whether or not the worker takes it: it is what a sort here would
    // be asked for, if it comes to that.
    _asked = request;
    _askedAt = std::chrono::steady_clock::now();
    _waiting = true;

    // A worker that has failed does not come back. The cloud is still sorted,
    // on this thread, which costs frames and is still a picture.
    if (orblit_splat_worker_state(_worker) == kFailed) {
      if (!_warnedGone) {
        std::fprintf(stderr,
                     "[orblit] splats: the sorting worker is gone; sorting "
                     "%u splats on the page's own thread\n",
                     _count);
        _warnedGone = true;
      }
      sortHere(_inline, _inlineMilliseconds);
      _inlineReady = true;
      _waiting = false;
      return;
    }

    float numbers[kRequestFloats];
    std::memcpy(numbers, request.direction, 3 * sizeof(float));
    std::memcpy(numbers + 3, request.viewFromModel, 16 * sizeof(float));
    std::memcpy(numbers + 19, request.clipFromModel, 16 * sizeof(float));
    orblit_splat_worker_request(
        _worker, numbers, (request.cull ? 1 : 0) | (request.coarse ? 2 : 0));
  }

  bool busy() override {
    return orblit_splat_worker_state(_worker) == kWaiting;
  }

  bool take(std::vector<uint32_t> &order, double &milliseconds) override {
    if (_inlineReady) {
      order.swap(_inline);
      milliseconds = _inlineMilliseconds;
      _inlineReady = false;
      return true;
    }

    const int ready = orblit_splat_worker_ready(_worker);
    if (ready >= 0) {
      _waiting = false;
      order.resize(size_t(ready));
      milliseconds = orblit_splat_worker_take(_worker, order.data());
      return true;
    }

    // Long enough. The order is worked out here instead, and the worker is
    // stopped being waited on rather than waited on for ever — it is asked
    // again the next time the camera moves, and if it answers this request
    // late, that answer is taken then.
    if (_waiting && secondsSince(_askedAt) > kPatienceSeconds) {
      if (!_warnedSlow) {
        std::fprintf(stderr,
                     "[orblit] splats: the sorting worker has not answered in "
                     "%.1f s; sorting %u splats on the page's own thread\n",
                     kPatienceSeconds, _count);
        _warnedSlow = true;
      }
      orblit_splat_worker_give_up(_worker);
      _waiting = false;
      sortHere(order, milliseconds);
      return true;
    }
    return false;
  }

 private:
  void sortHere(std::vector<uint32_t> &order, double &milliseconds) {
    const auto from = std::chrono::steady_clock::now();
    sortSplats(_positions->data(), _count, _asked, order, _scratch);
    milliseconds = secondsSince(from) * 1000.0;
  }

  int _worker;
  std::shared_ptr<const std::vector<float>> _positions;
  uint32_t _count;

  SplatSortRequest _asked;
  std::chrono::steady_clock::time_point _askedAt;
  bool _waiting = false;

  bool _warnedGone = false;
  bool _warnedSlow = false;
  bool _inlineReady = false;
  double _inlineMilliseconds = 0;
  std::vector<uint32_t> _inline;
  std::vector<uint32_t> _scratch;
};

}  // namespace

std::unique_ptr<SplatSorter> makeWorkerSplatSorter(
    std::shared_ptr<const std::vector<float>> positions, uint32_t count) {
  const int worker = orblit_splat_worker_start(positions->data(), count);
  if (worker == 0) return nullptr;
  return std::make_unique<WorkerSplatSorter>(worker, std::move(positions),
                                             count);
}

}  // namespace orblit
