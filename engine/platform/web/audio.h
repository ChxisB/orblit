/**************************************************************************/
/*  audio.h                                                               */

#pragma once

#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

extern int orblit_audio_is_available();
extern int orblit_audio_has_worklet();
extern int orblit_audio_has_script_processor();
extern int orblit_audio_init(int *p_mix_rate, int p_latency, void (*_state_cb)(int), void (*_latency_cb)(float));
extern void orblit_audio_resume();

extern int orblit_audio_input_start();
extern void orblit_audio_input_stop();

// Samples
extern int orblit_audio_sample_stream_is_registered(const char *p_stream_object_id);
extern void orblit_audio_sample_register_stream(const char *p_stream_object_id, float *p_frames_buf, int p_frames_total, const char *p_loop_mode, int p_loop_begin, int p_loop_end);
extern void orblit_audio_sample_unregister_stream(const char *p_stream_object_id);
extern void orblit_audio_sample_start(const char *p_playback_object_id, const char *p_stream_object_id, int p_bus_index, float p_offset, float p_pitch_scale, float *p_volume_ptr);
extern void orblit_audio_sample_stop(const char *p_playback_object_id);
extern void orblit_audio_sample_set_pause(const char *p_playback_object_id, bool p_pause);
extern int orblit_audio_sample_is_active(const char *p_playback_object_id);
extern double orblit_audio_get_sample_playback_position(const char *p_playback_object_id);
extern void orblit_audio_sample_update_pitch_scale(const char *p_playback_object_id, float p_pitch_scale);
extern void orblit_audio_sample_set_volumes_linear(const char *p_playback_object_id, int *p_buses_buf, int p_buses_size, float *p_volumes_buf, int p_volumes_size);
extern void orblit_audio_sample_set_finished_callback(void (*p_callback)(const char *));

extern void orblit_audio_sample_bus_set_count(int p_count);
extern void orblit_audio_sample_bus_remove(int p_index);
extern void orblit_audio_sample_bus_add(int p_at_pos = -1);
extern void orblit_audio_sample_bus_move(int p_bus, int p_to_pos);
extern void orblit_audio_sample_bus_set_send(int p_bus, int p_send_index);
extern void orblit_audio_sample_bus_set_volume_db(int p_bus, float p_volume_db);
extern void orblit_audio_sample_bus_set_solo(int p_bus, bool p_enable);
extern void orblit_audio_sample_bus_set_mute(int p_bus, bool p_enable);

// Worklet
typedef int32_t OrblitAudioState[4];
extern int orblit_audio_worklet_create(int p_channels);
extern void orblit_audio_worklet_start(float *p_in_buf, int p_in_size, float *p_out_buf, int p_out_size, OrblitAudioState p_state);
extern void orblit_audio_worklet_start_no_threads(float *p_out_buf, int p_out_size, void (*p_out_cb)(int p_pos, int p_frames), float *p_in_buf, int p_in_size, void (*p_in_cb)(int p_pos, int p_frames));
extern int orblit_audio_worklet_state_add(OrblitAudioState p_state, int p_idx, int p_value);
extern int orblit_audio_worklet_state_get(OrblitAudioState p_state, int p_idx);
extern int orblit_audio_worklet_state_wait(int32_t *p_state, int p_idx, int32_t p_expected, int p_timeout);

// Script
extern int orblit_audio_script_create(int *p_buffer_size, int p_channels);
extern void orblit_audio_script_start(float *p_in_buf, int p_in_size, float *p_out_buf, int p_out_size, void (*p_cb)());

#ifdef __cplusplus
}
#endif
