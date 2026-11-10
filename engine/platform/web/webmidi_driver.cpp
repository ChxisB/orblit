/**************************************************************************/
/*  webmidi_driver.cpp                                                    */

#include "webmidi_driver.h"

#include "midi.h"

#ifdef PROXY_TO_PTHREAD_ENABLED
#include "core/object/callable_mp.h"
#endif

MIDIDriverWebMidi *MIDIDriverWebMidi::get_singleton() {
	return static_cast<MIDIDriverWebMidi *>(MIDIDriver::get_singleton());
}

Error MIDIDriverWebMidi::open() {
	Error error = (Error)orblit_js_webmidi_open_midi_inputs(&MIDIDriverWebMidi::set_input_names_callback, &MIDIDriverWebMidi::on_midi_message, _event_buffer, MIDIDriverWebMidi::MAX_EVENT_BUFFER_LENGTH);
	if (error == ERR_UNAVAILABLE) {
		ERR_PRINT("Web MIDI is not supported on this browser.");
	}
	return error;
}

void MIDIDriverWebMidi::close() {
	get_singleton()->connected_input_names.clear();
	orblit_js_webmidi_close_midi_inputs();
}

MIDIDriverWebMidi::~MIDIDriverWebMidi() {
	close();
}

void MIDIDriverWebMidi::set_input_names_callback(int p_size, const char **p_input_names) {
	Vector<String> input_names;
	for (int i = 0; i < p_size; i++) {
		input_names.append(String::utf8(p_input_names[i]));
	}
#ifdef PROXY_TO_PTHREAD_ENABLED
	if (!Thread::is_main_thread()) {
		callable_mp_static(MIDIDriverWebMidi::_set_input_names_callback).call_deferred(input_names);
		return;
	}
#endif

	_set_input_names_callback(input_names);
}

void MIDIDriverWebMidi::_set_input_names_callback(const Vector<String> &p_input_names) {
	get_singleton()->connected_input_names.clear();
	for (const String &input_name : p_input_names) {
		get_singleton()->connected_input_names.push_back(input_name);
	}
}

void MIDIDriverWebMidi::on_midi_message(int p_device_index, int p_status, const uint8_t *p_data, int p_data_len) {
	PackedByteArray data;
	data.resize(p_data_len);
	uint8_t *data_ptr = data.ptrw();
	for (int i = 0; i < p_data_len; i++) {
		data_ptr[i] = p_data[i];
	}
#ifdef PROXY_TO_PTHREAD_ENABLED
	if (!Thread::is_main_thread()) {
		callable_mp_static(MIDIDriverWebMidi::_on_midi_message).call_deferred(p_device_index, p_status, data, p_data_len);
		return;
	}
#endif
	_on_midi_message(p_device_index, p_status, data, p_data_len);
}

void MIDIDriverWebMidi::_on_midi_message(int p_device_index, int p_status, const PackedByteArray &p_data, int p_data_len) {
	MIDIDriver::send_event(p_device_index, p_status, p_data.ptr(), p_data_len);
}
