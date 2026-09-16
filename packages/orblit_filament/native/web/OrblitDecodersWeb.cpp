/* The decoder workers, from the renderer's side, in a browser.
 *
 * The texture queue and the environment hand decode jobs to
 * Module.orblitDecoders (orblit_decoder_workers.js, passed to emcc as
 * --pre-js) through the EM_JS functions below, and take the answers back
 * into this module's memory. Kept beside the web build rather than in the
 * shared sources, for the reason OrblitSplatSorterWeb.cpp is: nothing else
 * compiles it, and only this build has EM_JS.
 *
 * Why workers and not pthreads is the splat sorter's answer too: threads in
 * a browser need the page cross-origin isolated and Filament rebuilt with
 * -pthread, a second renderer to ship. A decode shares nothing with the
 * renderer but its bytes, which cross once each way.
 */

#include "OrblitDecode.h"

#include <emscripten/emscripten.h>

#include <cstdlib>
#include <cstring>

// Each crosses into Module.orblitDecoders. Pointers are shifted with >>>
// rather than >>, so an address past two gigabytes stays positive.

EM_JS(int, orblit_decoders_capacity, (), {
  const decoders = Module['orblitDecoders'];
  return decoders ? decoders.capacity() : -1;
});

EM_JS(int, orblit_decoders_submit,
      (int job, const uint8_t *data, uint32_t size, const double *parameters,
       uint32_t count, const char *name), {
  const decoders = Module['orblitDecoders'];
  if (!decoders) return 0;
  // Copied out of the module's memory, which can grow and move, and handed
  // over rather than cloned a second time.
  const at = data >>> 0;
  const bytes = HEAPU8.slice(at, at + (size >>> 0));
  const numbers = HEAPF64.slice(parameters >>> 3, (parameters >>> 3) + count);
  return decoders.submit(job, bytes, numbers, UTF8ToString(name));
});

EM_JS(int, orblit_decoders_poll,
      (int id, double startPatience, double runPatience), {
  return Module['orblitDecoders'].poll(id, startPatience, runPatience);
});

EM_JS(int, orblit_decoders_part_count, (int id), {
  return Module['orblitDecoders'].partCount(id);
});

EM_JS(uint32_t, orblit_decoders_part_size, (int id, int index), {
  return Module['orblitDecoders'].partSize(id, index);
});

EM_JS(int, orblit_decoders_number_count, (int id), {
  return Module['orblitDecoders'].numberCount(id);
});

EM_JS(double, orblit_decoders_milliseconds, (int id), {
  return Module['orblitDecoders'].milliseconds(id);
});

EM_JS(int, orblit_decoders_note_bytes, (int id), {
  return lengthBytesUTF8(Module['orblitDecoders'].note(id)) + 1;
});

EM_JS(void, orblit_decoders_copy,
      (int id, uint8_t **pointers, double *numbers, char *note, int noteRoom), {
  const decoders = Module['orblitDecoders'];
  decoders.copy(id, HEAPU8, HEAPU32, pointers >>> 2, HEAPF64, numbers >>> 3);
  stringToUTF8(decoders.note(id), note, noteRoom);
});

EM_JS(void, orblit_decoders_forget, (int id), {
  Module['orblitDecoders'].forget(id);
});

namespace orblit {
namespace web {
namespace decoders {

int32_t capacity() { return orblit_decoders_capacity(); }

int32_t submit(DecodeJob job, const uint8_t *data, size_t size,
               const std::vector<double> &parameters, const std::string &name) {
  if (size > UINT32_MAX) return 0;
  return orblit_decoders_submit(int(job), data, uint32_t(size),
                                parameters.data(), uint32_t(parameters.size()),
                                name.c_str());
}

State poll(int32_t id) {
  return State(orblit_decoders_poll(id, kDecodeStartPatienceSeconds * 1000.0,
                                    kDecodeRunPatienceSeconds * 1000.0));
}

bool take(int32_t id, DecodeAnswer &out) {
  out = DecodeAnswer();
  if (poll(id) != State::done) {
    orblit_decoders_forget(id);
    return false;
  }
  // Allocated here, where they are freed, and filled from JavaScript.
  const int parts = orblit_decoders_part_count(id);
  std::vector<uint8_t *> pointers(size_t(std::max(parts, 1)), nullptr);
  bool whole = true;
  for (int i = 0; i < parts; i++) {
    const size_t size = orblit_decoders_part_size(id, i);
    auto *bytes = static_cast<uint8_t *>(std::malloc(size == 0 ? 1 : size));
    pointers[size_t(i)] = bytes;
    out.parts.push_back({bytes, size});
    whole = whole && bytes != nullptr;
  }
  out.numbers.resize(size_t(orblit_decoders_number_count(id)));
  const int noteRoom = orblit_decoders_note_bytes(id);
  std::string note(size_t(noteRoom), '\0');
  if (whole) {
    orblit_decoders_copy(id, pointers.data(), out.numbers.data(), &note[0],
                         noteRoom);
    note.resize(std::strlen(note.c_str()));
    out.note = std::move(note);
  } else {
    out.note = "There was no memory to take its decoded bytes.";
    out.release();
  }
  out.milliseconds = orblit_decoders_milliseconds(id);
  orblit_decoders_forget(id);
  return true;
}

void cancel(int32_t id) {
  if (id != 0) orblit_decoders_forget(id);
}

}  // namespace decoders
}  // namespace web
}  // namespace orblit
