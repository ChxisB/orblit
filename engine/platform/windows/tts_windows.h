/**************************************************************************/
/*  tts_windows.h                                                         */

#pragma once

#include "core/string/ustring.h"
#include "core/variant/array.h"

class TTSDriver;

struct TTSUtterance;

class TTS_Windows {
	TTSDriver *driver = nullptr;

	static TTS_Windows *singleton;

public:
	static TTS_Windows *get_singleton();

	bool is_speaking() const;
	bool is_paused() const;
	Array get_voices() const;

	void speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false);
	void pause();
	void resume();
	void stop();

	void process_events();

	TTS_Windows();
	~TTS_Windows();
};
