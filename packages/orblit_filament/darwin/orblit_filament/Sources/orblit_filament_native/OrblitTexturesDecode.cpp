#include "OrblitTexturesInternal.h"

// The work between a push and a finished texture: threads, decoding, and a
// frame's upload budget.
//
// An item is decoded on a worker, or on the drawing thread when it is short
// enough to fit in a frame, or by the browser's decoder module where there
// are no threads. `pump` is what a frame calls to spend its budget on
// whatever has become ready.
//
// Part of `orblit::TextureQueue`; see OrblitTexturesInternal.h.

namespace orblit {

using namespace textures;

void TextureQueue::enqueue(const std::shared_ptr<Item> &item) {
  {
    std::lock_guard<std::mutex> hold(_lock);
    item->order = _order++;
    if (_items.empty()) {
      _batchFrom = now();
      _batchCount = 0;
    }
    _batchCount++;
    _items.push_back(item);
    _waiting.push_back(item);
  }
  if (!_noDecoderThreads) {
    startWorkers();
    _wake.notify_one();
  }
}

void TextureQueue::startWorkers() {
  if (!_workers.empty() || _noDecoderThreads) return;
  try {
    for (uint32_t i = 0; i < _workerCount; i++) {
      _workers.emplace_back([this] { work(); });
    }
  } catch (const std::exception &) {
    // A thread that will not start is not a reason to stop loading
    // textures: whatever did start keeps working, and with none the drawing
    // thread decodes a little each frame, as a browser does.
    if (_workers.empty()) _noDecoderThreads = true;
  }
}

void TextureQueue::work() {
#if defined(__APPLE__)
  // Below the thread that draws, which a host runs at user-interactive.
  pthread_set_qos_class_self_np(QOS_CLASS_USER_INITIATED, 0);
#endif
  for (;;) {
    std::shared_ptr<Item> item;
    {
      std::unique_lock<std::mutex> hold(_lock);
      _wake.wait(hold, [this] { return _stopping || !_waiting.empty(); });
      if (_stopping) return;
      item = std::move(_waiting.front());
      _waiting.pop_front();
      if (item->abandoned) {
        item->decoded = true;
        _idle.notify_all();
        continue;
      }
      item->decoding = true;
    }
    decode(*item);
    {
      std::lock_guard<std::mutex> hold(_lock);
      item->decoding = false;
      item->decoded = true;
    }
    _idle.notify_all();
  }
}

void TextureQueue::decodeInline() {
  const double from = now();
  do {
    std::shared_ptr<Item> item;
    {
      std::lock_guard<std::mutex> hold(_lock);
      while (!_waiting.empty() && _waiting.front()->abandoned) {
        _waiting.front()->decoded = true;
        _waiting.pop_front();
      }
      if (_waiting.empty()) return;
      auto next = _waiting.begin();
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
      // What a decoder worker will take is left for one. Decoded here: what
      // a worker gave back, and everything while no worker is to be used.
      if (web::decoders::capacity() >= 0) {
        next = std::find_if(_waiting.begin(), _waiting.end(),
                            [](const std::shared_ptr<Item> &waiting) {
                              return waiting->onPage && !waiting->abandoned;
                            });
        if (next == _waiting.end()) return;
      }
#endif
      item = std::move(*next);
      _waiting.erase(next);
      item->decoding = true;
    }
    const double started = now();
    decode(*item);
    // What decoding cost the thread that draws, said with the batch: the
    // number a worker is there to bring down.
    const double took = now() - started;
    _inlineSeconds += took;
    _longestInline = std::max(_longestInline, took);
    _inlineCount++;
    std::lock_guard<std::mutex> hold(_lock);
    item->decoding = false;
    item->decoded = true;
  } while (now() - from < kInlineDecodeSeconds);
}

bool TextureQueue::publish(Item &item, Unit &&unit) {
  std::lock_guard<std::mutex> hold(_lock);
  if (item.abandoned) return false;
  item.ready.push_back(std::move(unit));
  return true;
}

void TextureQueue::decode(Item &item) {
  const auto fail = [&](std::string why) {
    std::lock_guard<std::mutex> hold(_lock);
    item.failure = std::move(why);
  };

  switch (item.kind) {
    case Item::Kind::ktx2: {
      const ktx2::Header &header = item.header;
      const uint8_t *data = item.source->data();
      const size_t size = item.source->size();
      // Smallest first, each handed over as soon as it is ready, so the
      // upload that follows can start on the small levels while the large
      // ones are still being decompressed — and so the range of levels
      // Filament samples only ever grows downwards from the smallest.
      for (uint32_t level = header.levels; level-- > item.skip;) {
        const uint64_t bytes = ktx2::levelBytes(header, level);
        auto *out = static_cast<uint8_t *>(malloc(size_t(bytes)));
        if (out == nullptr) {
          fail(format("There was no memory for level %u.", level));
          return;
        }
        Unit unit;
        unit.kind = item.generateMipmaps ? Unit::Kind::picture
                                         : Unit::Kind::level;
        unit.level = level - item.skip;
        unit.bytes = out;
        unit.size = size_t(bytes);
        unit.budget = bytes;
        std::string why =
            ktx2::readLevel(data, size, header, level, out, size_t(bytes));
        if (!why.empty()) {
          fail(std::move(why));
          return;
        }
        if (!publish(item, std::move(unit))) return;
      }
      // Nothing more needs the file.
      std::lock_guard<std::mutex> hold(_lock);
      item.source.reset();
      return;
    }

    case Item::Kind::picture: {
      const SharedBytes &source = item.source;
      DecodedPicture decoded;
      std::string why = decodePicture(source->data(), source->size(),
                                      item.skip, item.srgb, decoded);
      if (!why.empty()) {
        fail(std::move(why));
        return;
      }
      Unit unit;
      unit.kind = Unit::Kind::picture;
      unit.level = 0;
      unit.bytes = decoded.pixels;
      unit.fromStb = decoded.fromStb;
      unit.size = decoded.size;
      unit.budget = unit.size;
      publish(item, std::move(unit));
      std::lock_guard<std::mutex> hold(_lock);
      item.source.reset();
      return;
    }

    case Item::Kind::basis: {
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
      // The decoder workers' own job, run here, so what the page draws when
      // no worker answers is what a worker would have drawn.
      web::DecodeAnswer answer;
      const double parameters[] = {double(item.basisFormat),
                                   item.basisCompressed ? 1.0 : 0.0};
      web::runDecodeJob(web::DecodeJob::basis, item.source->data(),
                        item.source->size(), parameters, 2, item.name, answer);
      publishAnswer(item, answer);
      return;
#else
      using Result = ktxreader::Ktx2Reader::Result;
      if (item.async->doTranscoding() != Result::SUCCESS) {
        fail("Its Basis data could not be transcoded.");
        return;
      }
      // Counted as every level of the texture it fills, which is what
      // uploadImages hands over in one go.
      Unit unit;
      unit.kind = Unit::Kind::basis;
      const Texture *texture = item.texture;
      if (const ktx2::Format *format = formatOfInternal(texture->getFormat())) {
        for (size_t level = 0; level < texture->getLevels(); level++) {
          const uint64_t across =
              (texture->getWidth(level) + format->blockWidth - 1) /
              format->blockWidth;
          const uint64_t down =
              (texture->getHeight(level) + format->blockHeight - 1) /
              format->blockHeight;
          unit.budget += across * down * format->bytesPerBlock;
        }
      }
      publish(item, std::move(unit));
      return;
#endif
    }
  }
}

void TextureQueue::upload(Item &item, Unit &unit) {
  Texture *texture = item.texture;
  if (unit.kind == Unit::Kind::basis) {
    item.async->uploadImages();
    return;
  }
  const GpuFormat *gpu =
      item.kind == Item::Kind::picture
          ? gpuFormatOf(item.srgb ? 43 : 37)
          : item.gpu;
  const void *fromStb = unit.fromStb ? &unit : nullptr;
  uint8_t *bytes = unit.bytes;
  unit.bytes = nullptr;
  if (gpu->compressed) {
    texture->setImage(
        _engine, unit.level,
        Texture::PixelBufferDescriptor(bytes, unit.size, gpu->compressedType,
                                       uint32_t(unit.size), freeBytes,
                                       const_cast<void *>(fromStb)));
  } else {
    texture->setImage(
        _engine, unit.level,
        Texture::PixelBufferDescriptor(bytes, unit.size, gpu->pixelFormat,
                                       gpu->pixelType, freeBytes,
                                       const_cast<void *>(fromStb)));
  }
  // The largest level of a picture — or of a KTX 2 file that asked for them —
  // with every other made from it, in the same frame, so the range Filament
  // samples is never wider than what has been written.
  if (unit.kind == Unit::Kind::picture && texture->getLevels() > 1) {
    texture->generateMipmaps(_engine);
  }
}

void TextureQueue::release(Item &item) {
  if (item.async != nullptr) {
    _basis->asyncDestroy(&item.async);
    item.async = nullptr;
  }
}

void TextureQueue::pump() {
  if (_batchCount > 0) {
    const double at = now();
    if (_lastPumpAt > 0) _longestFrame = std::max(_longestFrame, at - _lastPumpAt);
    _lastPumpAt = at;
  }
#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
  if (_noDecoderThreads) decodeOnWorkers();
#endif
  if (_noDecoderThreads) decodeInline();

  // Whatever placeholders push wrote since the last pump count against this
  // frame: they went to the GPU in it.
  uint64_t spent = _placeholderBytes;
  _placeholderBytes = 0;

  std::vector<std::shared_ptr<Item>> live;
  {
    std::lock_guard<std::mutex> hold(_lock);
    if (_items.empty()) {
      _frames.lastBytes = spent;
      _frames.lastUploads = 0;
      return;
    }
    live = _items;
  }

  const uint64_t budget =
      _bytesPerFrame == 0 ? UINT64_MAX : _bytesPerFrame;
  // One frame in eight uploads nothing while the budget adapts, so what the
  // scene costs without it can be measured beside what it costs with it.
  // The one frame of the eight that breaks "at least one upload a frame".
  const bool probing = _adapting.on && (++_adapting.pumps % 8) == 0;
  _adapting.lastProbed = probing;
  uint32_t uploads = 0;
  bool finished = false;

  for (const std::shared_ptr<Item> &item : live) {
    if (item->complete) continue;
    if (!probing) {
      // A model's texture not yet written, with no level of its own ready to
      // write: its placeholder, charged what the GPU finds for all of it.
      if (!item->written && item->owner != nullptr &&
          item->placeholder != nullptr) {
        bool levelReady = false;
        {
          std::lock_guard<std::mutex> hold(_lock);
          levelReady = !item->ready.empty() || item->abandoned;
        }
        if (!levelReady) {
          if (uploads > 0 && item->storage > budget - std::min(spent, budget)) {
            continue;
          }
          const bool whole = !startsAsPlaceholder(*item->placeholder);
          writePlaceholder(item->texture, *item->placeholder,
                           item->placeholderLevel, whole);
          item->written = true;
          spent += item->storage;
          uploads++;
        }
      }

      for (;;) {
        Unit unit;
        uint64_t cost = 0;
        {
          std::lock_guard<std::mutex> hold(_lock);
          if (item->abandoned || item->ready.empty()) break;
          Unit &next = item->ready.front();
          // The first write into a texture costs what the GPU has to find for
          // all of it, whichever level it is: that is when a texture's memory
          // is really made.
          cost = item->written ? next.budget : std::max(next.budget, item->storage);
          // At least one upload a frame, whatever its size: a level larger
          // than the budget would otherwise never go.
          if (uploads > 0 && cost > budget - std::min(spent, budget)) {
            break;
          }
          unit = std::move(next);
          item->ready.pop_front();
        }
        upload(*item, unit);
        item->written = true;
        spent += cost;
        uploads++;
      }
    }

    std::lock_guard<std::mutex> hold(_lock);
    if (!item->abandoned && item->decoded && !item->decoding &&
        item->ready.empty()) {
      item->complete = true;
      finished = true;
      _poppable[item->client].push_back(
          {item->texture, item->owner, item->name, item->failure});
      _counts[item->client].decoded++;
      _frames.arrived++;
    }
  }

  std::vector<std::shared_ptr<Item>> done;
  size_t left = 0;
  Frames frames;
  {
    std::lock_guard<std::mutex> hold(_lock);
    for (auto it = _items.begin(); it != _items.end();) {
      if ((*it)->complete) {
        done.push_back(std::move(*it));
        it = _items.erase(it);
      } else {
        ++it;
      }
    }
    left = _items.size();
    _frames.lastBytes = spent;
    _frames.lastUploads = uploads;
    if (uploads > 0) _frames.pumpsWithUploads++;
    _frames.mostBytes = std::max(_frames.mostBytes, spent);
    _frames.mostUploads = std::max(_frames.mostUploads, uploads);
    frames = _frames;
  }
  for (const std::shared_ptr<Item> &item : done) release(*item);

  // Said once a batch has all arrived, because a load's cost is wanted the
  // first time it happens rather than after somebody has reproduced it.
  if (finished && left == 0 && _batchCount > 0) {
    log("[orblit] %llu texture(s) arrived in %.0f ms; at most %llu KB and %u "
        "upload(s) in one frame; the longest frame meanwhile %.1f ms",
        (unsigned long long)_batchCount, (now() - _batchFrom) * 1000.0,
        (unsigned long long)(frames.mostBytes / 1024), frames.mostUploads,
        _longestFrame * 1000.0);
    _lastPumpAt = 0;
    _longestFrame = 0;
    if (_inlineCount > 0) {
      log("[orblit] %llu of them decoded on the drawing thread: %.0f ms in "
          "all, %.0f ms the longest; pushing them took %.0f ms",
          (unsigned long long)_inlineCount, _inlineSeconds * 1000.0,
          _longestInline * 1000.0, _pushSeconds * 1000.0);
    }
    if (_offThreadCount > 0) {
      log("[orblit] %llu of them decoded on workers: %.0f ms of decoding, "
          "%.0f ms the longest; handing them over and back took the drawing "
          "thread %.0f ms, %.0f ms at most in a frame; pushing them took "
          "%.0f ms",
          (unsigned long long)_offThreadCount, _offThreadSeconds * 1000.0,
          _longestOffThread * 1000.0, _handoverSeconds * 1000.0,
          _longestHandover * 1000.0, _pushSeconds * 1000.0);
    }
    _batchCount = 0;
    _pushSeconds = 0;
    _inlineCount = 0;
    _inlineSeconds = 0;
    _longestInline = 0;
    _offThreadCount = 0;
    _offThreadSeconds = 0;
    _longestOffThread = 0;
    _handoverSeconds = 0;
    _longestHandover = 0;
  }
}

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)

void TextureQueue::decodeOnWorkers() {
  namespace decoders = web::decoders;
  const double from = now();

  // Answers first, so a worker one frees can take another job this frame.
  std::vector<std::shared_ptr<Item>> posted;
  {
    std::lock_guard<std::mutex> hold(_lock);
    posted = _posted;
  }
  uint64_t copied = 0;
  size_t answers = 0;
  for (const std::shared_ptr<Item> &item : posted) {
    const decoders::State state = decoders::poll(item->job);
    if (state == decoders::State::waiting ||
        state == decoders::State::started) {
      continue;
    }
    if (state == decoders::State::done && answers > 0 &&
        copied >= kAnswerBytesPerFrame) {
      continue;
    }
    web::DecodeAnswer answer;
    const bool answered =
        state == decoders::State::done && decoders::take(item->job, answer);
    if (!answered) decoders::cancel(item->job);
    item->job = 0;
    {
      std::lock_guard<std::mutex> hold(_lock);
      _posted.erase(std::find(_posted.begin(), _posted.end(), item));
      if (!answered) {
        // Failed or given up on: decoded here, before anything newer.
        item->onPage = true;
        if (!item->abandoned) _waiting.push_front(item);
      }
    }
    if (!answered) continue;
    answers++;
    for (const web::DecodedPart &part : answer.parts) copied += part.size;
    _offThreadCount++;
    _offThreadSeconds += answer.milliseconds / 1000.0;
    _longestOffThread = std::max(_longestOffThread, answer.milliseconds / 1000.0);
    publishAnswer(*item, answer);
    std::lock_guard<std::mutex> hold(_lock);
    item->decoded = true;
  }

  // Then new jobs, one for each idle worker.
  while (decoders::capacity() > 0) {
    std::shared_ptr<Item> item;
    {
      std::lock_guard<std::mutex> hold(_lock);
      const auto next = std::find_if(
          _waiting.begin(), _waiting.end(),
          [](const std::shared_ptr<Item> &waiting) {
            return !waiting->onPage && !waiting->abandoned;
          });
      if (next == _waiting.end()) break;
      item = *next;
      _waiting.erase(next);
    }
    web::DecodeJob job = web::DecodeJob::ktx2Levels;
    std::vector<double> parameters;
    switch (item->kind) {
      case Item::Kind::ktx2:
        job = web::DecodeJob::ktx2Levels;
        parameters = {double(item->skip)};
        break;
      case Item::Kind::picture:
        job = web::DecodeJob::picture;
        parameters = {double(item->skip), item->srgb ? 1.0 : 0.0};
        break;
      case Item::Kind::basis:
        job = web::DecodeJob::basis;
        parameters = {double(item->basisFormat),
                      item->basisCompressed ? 1.0 : 0.0};
        break;
    }
    const int32_t id = decoders::submit(job, item->source->data(),
                                        item->source->size(), parameters,
                                        item->name);
    std::lock_guard<std::mutex> hold(_lock);
    if (id == 0) {
      _waiting.push_front(item);
      break;
    }
    item->job = id;
    _posted.push_back(item);
  }

  const double took = now() - from;
  if (!posted.empty() || !_posted.empty()) {
    _handoverSeconds += took;
    _longestHandover = std::max(_longestHandover, took);
  }
}

void TextureQueue::publishAnswer(Item &item, web::DecodeAnswer &answer) {
  if (!answer.note.empty()) {
    std::lock_guard<std::mutex> hold(_lock);
    item.failure = answer.note;
    return;
  }
  const size_t parts = answer.parts.size();
  const auto levelOf = [&answer](size_t i) {
    return i < answer.numbers.size() ? uint32_t(answer.numbers[i]) : 0u;
  };
  const auto unitOf = [&answer](size_t i, Unit::Kind kind, uint32_t level) {
    Unit unit;
    unit.kind = kind;
    unit.level = level;
    unit.size = answer.parts[i].size;
    unit.bytes = answer.takePart(i);
    unit.budget = unit.size;
    return unit;
  };
  switch (item.kind) {
    case Item::Kind::ktx2:
      // Smallest first already, as the job reads them.
      for (size_t i = 0; i < parts; i++) {
        const Unit::Kind kind =
            item.generateMipmaps ? Unit::Kind::picture : Unit::Kind::level;
        if (!publish(item, unitOf(i, kind, levelOf(i) - item.skip))) return;
      }
      break;
    case Item::Kind::picture:
      if (parts > 0) publish(item, unitOf(0, Unit::Kind::picture, 0));
      break;
    case Item::Kind::basis:
      // Largest first, as Basis Universal transcodes them; uploaded smallest
      // first, as every other texture's levels are.
      for (size_t i = parts; i-- > 0;) {
        if (!publish(item, unitOf(i, Unit::Kind::level, levelOf(i)))) return;
      }
      break;
  }
  std::lock_guard<std::mutex> hold(_lock);
  item.source.reset();
}

void TextureQueue::cancelJobs(const std::vector<std::shared_ptr<Item>> &items) {
  for (const std::shared_ptr<Item> &item : items) {
    if (item->job == 0) continue;
    web::decoders::cancel(item->job);
    item->job = 0;
    _posted.erase(std::remove(_posted.begin(), _posted.end(), item),
                  _posted.end());
  }
}

#endif

}  // namespace orblit
