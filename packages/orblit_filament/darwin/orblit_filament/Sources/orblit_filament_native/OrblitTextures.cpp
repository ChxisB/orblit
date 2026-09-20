#include "OrblitTexturesInternal.h"

// The device's defaults, the queue itself, and what gltfio sees.
//
// What a device may be asked for, how the queue is built and how its upload
// budget follows what frames measure, which formats it will take and which
// cooked file it reads for one, and the counts a client pops through. The
// provider at the end is this same queue behind gltfio's interface.
//
// One of the three files `orblit::TextureQueue` is defined across; see
// OrblitTexturesInternal.h.

namespace orblit {

using namespace textures;

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
    // Anything still here is still on its way: pump erases an item as it
    // completes it. Nothing weaker will do. A written placeholder means the
    // memory has been made, not that the texture can be sampled — it fills
    // one level, and whether the rest reads as the placeholder or as the
    // error colour is a property of the format, the backend and the driver
    // that this cannot see from here. Uploading under a budget stretches the
    // gap between the memory and the levels from nothing to seconds, so a
    // model let through on "has memory" is a model drawn from whatever that
    // memory happens to hold.
    if (item->owner != owner || item->abandoned) continue;
    count++;
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
