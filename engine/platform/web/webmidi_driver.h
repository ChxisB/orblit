/**************************************************************************/
/*  webmidi_driver.h                                                      */

#pragma once

#include "js.h"

#include "core/os/midi_driver.h"

class MIDIDriverWebMidi : public MIDIDriver {
private:
	static const int MAX_EVENT_BUFFER_LENGTH = 2;
	uint8_t _event_buffer[MAX_EVENT_BUFFER_LENGTH];

public:
	// Override return type to make writing static callbacks less tedious.
	static MIDIDriverWebMidi *get_singleton();

	virtual Error open() override;
	virtual void close() override final;

	MIDIDriverWebMidi() = default;
	virtual ~MIDIDriverWebMidi();

	WASM_EXPORT static void set_input_names_callback(int p_size, const char **p_input_names);
	static void _set_input_names_callback(const Vector<String> &p_input_names);

	WASM_EXPORT static void on_midi_message(int p_device_index, int p_status, const uint8_t *p_data, int p_data_len);
	static void _on_midi_message(int p_device_index, int p_status, const PackedByteArray &p_data, int p_data_len);
};
