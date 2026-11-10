/**************************************************************************/
/*  embedded_debugger.h                                                   */

#pragma once

#include "core/containers/hash_map.h"
#include "core/variant/array.h"

class DisplaySystemMacOSEmbedded;

/// @brief Singleton class to process embedded debugging message in the child process.
class EmbeddedDebugger {
	inline static EmbeddedDebugger *singleton = nullptr;

	EmbeddedDebugger(DisplaySystemMacOSEmbedded *p_ds);

public:
	static void initialize(DisplaySystemMacOSEmbedded *p_ds);
	static void deinitialize();

	~EmbeddedDebugger();

#ifdef DEBUG_ENABLED
private:
	DisplaySystemMacOSEmbedded *ds;

	/// Message handler function for parse_message.
	typedef Error (EmbeddedDebugger::*ParseMessageFunc)(const Array &p_args);
	static HashMap<String, ParseMessageFunc> parse_message_handlers;
	static void _init_parse_message_handlers();

	Error _msg_window_size(const Array &p_args);
	Error _msg_mouse_set_mode(const Array &p_args);
	Error _msg_event(const Array &p_args);
	Error _msg_win_event(const Array &p_args);
	Error _msg_notification(const Array &p_args);
	Error _msg_ime_update(const Array &p_args);
	Error _msg_ds_state(const Array &p_args);

public:
	static Error parse_message(void *p_user, const String &p_msg, const Array &p_args, bool &r_captured);
#endif
};
