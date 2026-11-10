/**************************************************************************/
/*  tts_driver_sapi.h                                                     */

#pragma once

#include "tts_driver.h"

#include <windows.h>

#include <objbase.h>
#include <sapi.h>
#include <winnls.h>

#include <cwchar>

struct TTSUtterance;

class TTSDriverSAPI : public TTSDriver {
	List<TTSUtterance> queue;
	ISpVoice *synth = nullptr;
	bool paused = false;
	struct UTData {
		Char16String string;
		int offset;
		int64_t id;
	};
	HashMap<uint32_t, UTData> ids;
	bool update_requested = false;

	static void __stdcall speech_event_callback(WPARAM wParam, LPARAM lParam);

	static TTSDriverSAPI *singleton;

public:
	virtual bool is_speaking() const override;
	virtual bool is_paused() const override;
	virtual Array get_voices() const override;

	virtual void speak(const String &p_text, const String &p_voice, int p_volume = 50, float p_pitch = 1.f, float p_rate = 1.f, int64_t p_utterance_id = 0, bool p_interrupt = false) override;
	virtual void pause() override;
	virtual void resume() override;
	virtual void stop() override;

	virtual void process_events() override;

	virtual bool init() override;

	TTSDriverSAPI();
	~TTSDriverSAPI();
};
