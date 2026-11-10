/**************************************************************************/
/*  tts_android.h                                                         */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/safe_refcount.h"
#include "core/os/thread.h"
#include "core/string/ustring.h"
#include "core/variant/array.h"

#include <jni.h>

class TTS_Android {
	static inline int INIT_STATE_UNKNOWN = 0;
	static inline int INIT_STATE_SUCCESS = 1;
	static inline int INIT_STATE_FAIL = -1;

	static bool initialized;
	static jobject tts;
	static jclass cls;

	static jmethodID _init;
	static jmethodID _is_speaking;
	static jmethodID _is_paused;
	static jmethodID _get_state;
	static jmethodID _get_voices;
	static jmethodID _speak;
	static jmethodID _pause_speaking;
	static jmethodID _resume_speaking;
	static jmethodID _stop_speaking;

	static Thread init_thread;
	static SafeFlag quit_request;
	static SafeFlag init_done;

	static void _thread_function(void *self);

	static HashMap<int64_t, Char16String> ids;

	static void initialize_tts(bool p_wait = true);

public:
	static void setup(jobject p_tts);
	static void terminate();
	static void _java_utterance_callback(int p_event, int64_t p_id, int p_pos);

	static bool is_speaking();
	static bool is_paused();
	static Array get_voices();
	static void speak(const String &p_text, const String &p_voice, int p_volume, float p_pitch, float p_rate, int64_t p_utterance_id, bool p_interrupt);
	static void pause();
	static void resume();
	static void stop();
};
