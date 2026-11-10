/**************************************************************************/
/*  input_event_codec.h                                                   */

#pragma once

#include "core/input/input_event.h"

/**
 * Encodes the input event as a byte array.
 *
 * Returns `true` if the event was successfully encoded, `false` otherwise.
 */
bool encode_input_event(const Ref<InputEvent> &p_event, PackedByteArray &r_data);
void decode_input_event(const PackedByteArray &p_data, Ref<InputEvent> &r_event);

void encode_input_event_key(const Ref<InputEventKey> &p_event, PackedByteArray &r_data);
void encode_input_event_mouse_button(const Ref<InputEventMouseButton> &p_event, PackedByteArray &r_data);
void encode_input_event_mouse_motion(const Ref<InputEventMouseMotion> &p_event, PackedByteArray &r_data);
void encode_input_event_joypad_button(const Ref<InputEventJoypadButton> &p_event, PackedByteArray &r_data);
void encode_input_event_joypad_motion(const Ref<InputEventJoypadMotion> &p_event, PackedByteArray &r_data);
void encode_input_event_gesture_pan(const Ref<InputEventPanGesture> &p_event, PackedByteArray &r_data);
void encode_input_event_gesture_magnify(const Ref<InputEventMagnifyGesture> &p_event, PackedByteArray &r_data);
