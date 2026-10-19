#include "orblit_audio_device.h"

#include <math.h>
#include <stdlib.h>

#include "miniaudio.h"

typedef struct Voice {
  int64_t id;
  ma_sound sound;
  struct Voice* next;
} Voice;

struct OrblitAudio {
  ma_engine engine;
  Voice* voices;
};

// shortcut: a linear scan, fine to a few hundred voices. Index by id past that.
static Voice* find(const OrblitAudio* audio, int64_t id) {
  for (Voice* voice = audio->voices; voice != NULL; voice = voice->next) {
    if (voice->id == id) return voice;
  }
  return NULL;
}

static void release(Voice** link) {
  Voice* voice = *link;
  *link = voice->next;
  ma_sound_uninit(&voice->sound);
  free(voice);
}

static void release_voice(OrblitAudio* audio, int64_t id) {
  for (Voice** link = &audio->voices; *link != NULL; link = &(*link)->next) {
    if ((*link)->id == id) {
      release(link);
      return;
    }
  }
}

static void release_ended(OrblitAudio* audio) {
  for (Voice** link = &audio->voices; *link != NULL;) {
    if (ma_sound_at_end(&(*link)->sound)) {
      release(link);
    } else {
      link = &(*link)->next;
    }
  }
}

static void apply(Voice* voice, float gain, float pitch, float pan) {
  ma_sound_set_volume(&voice->sound, gain);
  ma_sound_set_pitch(&voice->sound, pitch);
  ma_sound_set_pan(&voice->sound, pan);
}

OrblitAudio* orblit_audio_create(int32_t offline, uint32_t sample_rate,
                                 int32_t* result) {
  OrblitAudio* audio = calloc(1, sizeof(OrblitAudio));
  if (audio == NULL) {
    *result = MA_OUT_OF_MEMORY;
    return NULL;
  }
  ma_engine_config config = ma_engine_config_init();
  // Pan is a left and right balance, so the engine mixes stereo and lets
  // miniaudio fold that onto however many speakers the device has.
  config.channels = 2;
  if (offline) {
    config.noDevice = MA_TRUE;
    config.sampleRate = sample_rate;
  }
  *result = ma_engine_init(&config, &audio->engine);
  if (*result != MA_SUCCESS) {
    free(audio);
    return NULL;
  }
  return audio;
}

void orblit_audio_destroy(OrblitAudio* audio) {
  while (audio->voices != NULL) release(&audio->voices);
  ma_engine_uninit(&audio->engine);
  free(audio);
}

// F2: nine plain values. A struct would need its own allocation for each call
// across the FFI boundary.
int32_t orblit_audio_start(OrblitAudio* audio, int64_t voice_id,
                           const char* path, int32_t loop, int32_t stream,
                           float gain, float pitch, float pan, float offset) {
  // A clip that ended has nobody left to stop it, so its slot is freed when
  // the next one starts.
  release_ended(audio);
  Voice* voice = calloc(1, sizeof(Voice));
  if (voice == NULL) return MA_OUT_OF_MEMORY;
  ma_uint32 flags = MA_SOUND_FLAG_NO_SPATIALIZATION |
                    (stream ? MA_SOUND_FLAG_STREAM : MA_SOUND_FLAG_DECODE);
  ma_result result =
      ma_sound_init_from_file(&audio->engine, path, flags, NULL, NULL,
                              &voice->sound);
  if (result == MA_SUCCESS) {
    voice->id = voice_id;
    ma_sound_set_looping(&voice->sound, loop ? MA_TRUE : MA_FALSE);
    apply(voice, gain, pitch, pan);
    // miniaudio seeks on the mixing thread and drops a seek past the end, so
    // the clip would play from its start. A clip that long gone is over, and
    // a loop is wherever the wrap puts it.
    float length = 0;
    if (ma_sound_get_length_in_seconds(&voice->sound, &length) == MA_SUCCESS &&
        length > 0 && offset >= length) {
      if (!loop) {
        ma_sound_uninit(&voice->sound);
        free(voice);
        return MA_SUCCESS;
      }
      offset = fmodf(offset, length);
    }
    // Set before the start, so the first block mixed is already from here.
    result = offset > 0 ? ma_sound_seek_to_second(&voice->sound, offset)
                        : MA_SUCCESS;
    if (result == MA_SUCCESS) result = ma_sound_start(&voice->sound);
    if (result != MA_SUCCESS) ma_sound_uninit(&voice->sound);
  }
  if (result != MA_SUCCESS) {
    free(voice);
    return result;
  }
  voice->next = audio->voices;
  audio->voices = voice;
  return MA_SUCCESS;
}

void orblit_audio_update(OrblitAudio* audio, int64_t voice_id, float gain,
                         float pitch, float pan) {
  Voice* voice = find(audio, voice_id);
  if (voice != NULL) apply(voice, gain, pitch, pan);
}

void orblit_audio_stop(OrblitAudio* audio, int64_t voice_id) {
  release_voice(audio, voice_id);
}

int32_t orblit_audio_is_playing(const OrblitAudio* audio, int64_t voice_id) {
  const Voice* voice = find(audio, voice_id);
  return voice != NULL && !ma_sound_at_end(&voice->sound);
}

uint64_t orblit_audio_render(OrblitAudio* audio, float* out, uint64_t frames) {
  if (ma_engine_get_device(&audio->engine) != NULL) return 0;
  ma_uint64 read = 0;
  ma_engine_read_pcm_frames(&audio->engine, out, frames, &read);
  return read;
}

const char* orblit_audio_describe(int32_t result) {
  return ma_result_description((ma_result)result);
}
