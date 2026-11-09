/**************************************************************************/
/*  midi.h                                                                */

#pragma once

#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

extern int orblit_js_webmidi_open_midi_inputs(
		void (*p_callback)(int p_size, const char **p_connected_input_names),
		void (*p_on_midi_message)(int p_device_index, int p_status, const uint8_t *p_data, int p_data_len),
		const uint8_t *p_data_buffer,
		const int p_data_buffer_len);

extern void orblit_js_webmidi_close_midi_inputs();

#ifdef __cplusplus
}
#endif
