/**************************************************************************/
/*  tts_linux.h                                                           */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/list.h"
#include "core/object/object.h"
#include "core/os/thread.h"
#include "core/os/thread_safe.h"
#include "core/string/ustring.h"
#include "core/variant/array.h"

#ifdef SOWRAP_ENABLED
#include "speechd-so_wrap.h"
#else
#include <libspeechd.h>
#endif

struct TTSUtterance;

class TTS_Linux : public Object {
	ORBLIT_SOFTCLASS(TTS_Linux, Object);
	_THREAD_SAFE_CLASS_

	List<TTSUtterance> queue;
	SPDConnection *synth = nullptr;
	bool speaking = false;
	bool paused = false;
	int last_msg_id = -1;
	HashMap<int, int64_t> ids;

	struct VoiceInfo {
		String language;
		String variant;
	};
	mutable bool voices_loaded = false;
	mutable HashMap<String, VoiceInfo> voices;

	Thread init_thread;

	static void speech_init_thread_func(void *p_userdata);
	static void speech_event_callback(size_t p_msg_id, size_t p_client_id, SPDNotificationType p_type);
	static void speech_event_index_mark(size_t p_msg_id, size_t p_client_id, SPDNotificationType p_type, char *p_index_mark);

	static TTS_Linux *singleton;

protected:
	void _load_voices() const;
	void _speech_event(int p_msg_id, int p_type);
	void _speech_index_mark(int p_msg_id, int p_type, const String &p_index_mark);

public:
	static TTS_Linux *get_singleton();

	bool is_speaking() const;
	bool is_paused() const;
	Array get_voices() const;

	void speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false);
	void pause();
	void resume();
	void stop();

	TTS_Linux();
	~TTS_Linux();
};
