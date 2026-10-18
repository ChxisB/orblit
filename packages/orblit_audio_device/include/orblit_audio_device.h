// A small C ABI over miniaudio's engine: one call per thing a voice does.
//
// Everything above this line is decided in Dart. A voice arrives with its
// final gain, pitch and pan, so spatial sound is switched off here and the
// engine only mixes what it is given.

#ifndef ORBLIT_AUDIO_DEVICE_H
#define ORBLIT_AUDIO_DEVICE_H

#include <stdint.h>

#ifdef _WIN32
#define ORBLIT_AUDIO_API __declspec(dllexport)
#else
#define ORBLIT_AUDIO_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct OrblitAudio OrblitAudio;

/// Opens the default output device, or with [offline] set an engine that has
/// no device and mixes only when [orblit_audio_render] is called. Offline
/// needs a non-zero [sample_rate]. A device runs at its own rate and ignores
/// it. Returns null and sets [*result] to a miniaudio result code on failure.
ORBLIT_AUDIO_API OrblitAudio* orblit_audio_create(int32_t offline,
                                                  uint32_t sample_rate,
                                                  int32_t* result);

ORBLIT_AUDIO_API void orblit_audio_destroy(OrblitAudio* audio);

/// Starts [path] as [voice]. A [stream] reads the file as it plays. Otherwise
/// the file is decoded once and shared by every voice playing it. It begins
/// [offset] seconds in. Returns 0, or a miniaudio result code with no voice
/// started.
ORBLIT_AUDIO_API int32_t orblit_audio_start(OrblitAudio* audio, int64_t voice,
                                            const char* path, int32_t loop,
                                            int32_t stream, float gain,
                                            float pitch, float pan,
                                            float offset);

/// Does nothing for a voice that is not playing.
ORBLIT_AUDIO_API void orblit_audio_update(OrblitAudio* audio, int64_t voice,
                                          float gain, float pitch, float pan);

/// Does nothing for a voice that is not playing.
ORBLIT_AUDIO_API void orblit_audio_stop(OrblitAudio* audio, int64_t voice);

/// False once the clip has played to its end, and for an unknown voice.
ORBLIT_AUDIO_API int32_t orblit_audio_is_playing(const OrblitAudio* audio,
                                                 int64_t voice);

/// Mixes [frames] of interleaved stereo float into [out], on an offline
/// engine. Returns the frames written, which is 0 for an engine with a device.
ORBLIT_AUDIO_API uint64_t orblit_audio_render(OrblitAudio* audio, float* out,
                                              uint64_t frames);

ORBLIT_AUDIO_API const char* orblit_audio_describe(int32_t result);

#ifdef __cplusplus
}
#endif

#endif
