/**************************************************************************/
/*  tts_driver.h                                                          */

#pragma once

#include "core/object/object.h"

class TTSDriver : public Object {
	ORBLIT_SOFTCLASS(TTSDriver, Object);

public:
	virtual bool is_speaking() const = 0;
	virtual bool is_paused() const = 0;
	virtual Array get_voices() const = 0;

	virtual void speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false) = 0;
	virtual void pause() = 0;
	virtual void resume() = 0;
	virtual void stop() = 0;

	virtual void process_events() = 0;

	virtual bool init() = 0;

	virtual ~TTSDriver() {}
};
