/* The decoder module: what a decoder Web Worker instantiates.
 *
 * A second, small WebAssembly module beside the renderer, holding only what
 * decoding needs — OrblitDecodeJobs.cpp and the pure readers under it, zstd,
 * stb_image and Basis Universal's transcoder — and none of Filament. build.sh
 * links it and embeds it in orblit_renderer.js, and orblit_decoder_workers.js
 * starts it on a worker the first time the renderer hands one a job.
 *
 * A plain C surface, because a worker's JavaScript calls it: run a job on
 * bytes it has copied into this module's memory, read back the answer's
 * note, parts and numbers, and free it.
 */

#include <emscripten/emscripten.h>

#include <cstdint>
#include <string>

#include "OrblitDecode.h"

using orblit::web::DecodeAnswer;
using orblit::web::DecodeJob;

extern "C" {

EMSCRIPTEN_KEEPALIVE
DecodeAnswer *orblit_decoder_run(int32_t job, const uint8_t *data,
                                 uint32_t size, const double *parameters,
                                 uint32_t parameterCount, const char *name) {
  auto *answer = new DecodeAnswer();
  orblit::web::runDecodeJob(DecodeJob(job), data, size, parameters,
                            parameterCount, name != nullptr ? name : "",
                            *answer);
  return answer;
}

EMSCRIPTEN_KEEPALIVE
const char *orblit_decoder_note(DecodeAnswer *answer) {
  return answer->note.c_str();
}

EMSCRIPTEN_KEEPALIVE
double orblit_decoder_milliseconds(DecodeAnswer *answer) {
  return answer->milliseconds;
}

EMSCRIPTEN_KEEPALIVE
uint32_t orblit_decoder_part_count(DecodeAnswer *answer) {
  return uint32_t(answer->parts.size());
}

EMSCRIPTEN_KEEPALIVE
const uint8_t *orblit_decoder_part(DecodeAnswer *answer, uint32_t index) {
  return answer->parts[index].bytes;
}

EMSCRIPTEN_KEEPALIVE
uint32_t orblit_decoder_part_size(DecodeAnswer *answer, uint32_t index) {
  return uint32_t(answer->parts[index].size);
}

EMSCRIPTEN_KEEPALIVE
uint32_t orblit_decoder_number_count(DecodeAnswer *answer) {
  return uint32_t(answer->numbers.size());
}

EMSCRIPTEN_KEEPALIVE
const double *orblit_decoder_numbers(DecodeAnswer *answer) {
  return answer->numbers.data();
}

EMSCRIPTEN_KEEPALIVE
void orblit_decoder_free(DecodeAnswer *answer) { delete answer; }

}  // extern "C"
