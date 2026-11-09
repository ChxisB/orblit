/**************************************************************************/
/*  tts_driver_onecore.h                                                  */

#pragma once

#include "tts_driver.h"
#include "winrt_utils.h"

struct TTSUtterance;

class OrblitMediaEndedEventHandler;
class OrblitMediaFailedEventHandler;
class OrblitMediaMarkerReachedEventHandler;
class OrblitMediaCueEventHandler;

class TTSDriverOneCore : public TTSDriver {
	friend class OrblitMediaEndedEventHandler;
	friend class OrblitMediaFailedEventHandler;
	friend class OrblitMediaMarkerReachedEventHandler;
	friend class OrblitMediaCueEventHandler;

	List<TTSUtterance> queue;

	bool playing = false;
	bool paused = false;
	bool update_requested = false;

	ComPtr<ROMediaPlayer> media;
	struct TrackData {
		ROEventToken token_c;
		ComPtr<ROTypedEventHandler_TimedMetadataTrack_MediaCueEventArgs> handler_c;
		ComPtr<ROTimedMetadataTrack> track;
	};
	Vector<TrackData> tracks;
	ComPtr<ROTypedEventHandler_MediaPlayer_PlaybackMediaMarkerReachedEventArgs> handler_s;
	ComPtr<ROTypedEventHandler_MediaPlayer_MediaPlayerFailedEventArgs> handler_f;
	ComPtr<ROTypedEventHandler_MediaPlayer_IInspectable> handler_e;
	ROEventToken token_s;
	ROEventToken token_f;
	ROEventToken token_e;
	int64_t offset = 0;
	int64_t id = -1;
	Char16String string;

	void _dispose_current(bool p_silent = false, bool p_canceled = false);
	void _speech_cancel(int p_msg_id);
	void _speech_end(int p_msg_id);
	void _speech_index_mark(int p_msg_id, int p_index_mark);

	static TTSDriverOneCore *singleton;

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

	TTSDriverOneCore();
	~TTSDriverOneCore();
};
