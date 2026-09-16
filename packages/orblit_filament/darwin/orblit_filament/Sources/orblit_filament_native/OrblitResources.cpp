#include "OrblitResources.h"

#include <atomic>
#include <mutex>
#include <unordered_map>
#include <utility>

#include "OrblitPlatform.h"

namespace orblit {

namespace {

struct Store {
  std::mutex lock;
  std::unordered_map<std::string, SharedBytes> named;
  std::atomic<uint64_t> generation{0};
};

/// Made on first use and never destroyed. A renderer torn down during static
/// destruction — an application quitting with a view still open — may still
/// ask, and a store destroyed before it would be a crash on the way out.
Store &store() {
  static Store *const kept = new Store();
  return *kept;
}

}  // namespace

std::string collapseDotSegments(const std::string &name) {
  const bool rooted = !name.empty() && name[0] == '/';
  std::vector<std::string> kept;
  size_t start = 0;
  while (start <= name.size()) {
    size_t end = name.find('/', start);
    if (end == std::string::npos) end = name.size();
    std::string segment = name.substr(start, end - start);
    if (segment == "..") {
      // A `..` with nothing left to climb out of is kept, so two names that
      // both climb past their start still compare equal rather than both
      // silently landing on the root.
      if (!kept.empty() && kept.back() != "..") {
        kept.pop_back();
      } else if (!rooted) {
        kept.push_back(std::move(segment));
      }
    } else if (!segment.empty() && segment != ".") {
      kept.push_back(std::move(segment));
    }
    start = end + 1;
  }

  std::string out = rooted ? "/" : "";
  for (size_t i = 0; i < kept.size(); i++) {
    if (i > 0) out += '/';
    out += kept[i];
  }
  return out;
}

void provideResource(const std::string &name, std::vector<uint8_t> &&bytes) {
  SharedBytes shared =
      std::make_shared<const std::vector<uint8_t>>(std::move(bytes));
  Store &kept = store();
  {
    std::lock_guard<std::mutex> hold(kept.lock);
    kept.named[collapseDotSegments(name)] = std::move(shared);
  }
  kept.generation.fetch_add(1, std::memory_order_relaxed);
}

bool releaseResource(const std::string &name) {
  Store &kept = store();
  std::lock_guard<std::mutex> hold(kept.lock);
  return kept.named.erase(collapseDotSegments(name)) > 0;
}

SharedBytes findResource(const std::string &name) {
  Store &kept = store();
  // A host that never provides anything — every desktop build that reads its
  // assets off disk — pays one atomic read here, not a lock and a hash.
  if (kept.generation.load(std::memory_order_relaxed) == 0) return nullptr;
  const std::string key = collapseDotSegments(name);
  std::lock_guard<std::mutex> hold(kept.lock);
  const auto found = kept.named.find(key);
  return found != kept.named.end() ? found->second : nullptr;
}

uint64_t resourceGeneration() {
  return store().generation.load(std::memory_order_relaxed);
}

SharedBytes readResource(const std::string &path) {
  if (SharedBytes found = findResource(path)) return found;
  auto read = std::make_shared<std::vector<uint8_t>>();
  if (!readFile(path, *read)) return nullptr;
  return read;
}

}  // namespace orblit
