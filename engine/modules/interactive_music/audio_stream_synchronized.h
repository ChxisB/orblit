/**************************************************************************/
/*  audio_stream_synchronized.h                                           */

#pragma once

#include "scene/resources/audio/audio_stream.h"

class AudioStreamPlaybackSynchronized;

class AudioStreamSynchronized : public AudioStream {
	ORBLIT_CLASS(AudioStreamSynchronized, AudioStream)
	OBJ_SAVE_TYPE(AudioStream)

private:
	friend class AudioStreamPlaybackSynchronized;

	enum {
		MAX_STREAMS = 32
	};

	int stream_count = 0;
	Ref<AudioStream> audio_streams[MAX_STREAMS];
	float audio_stream_volume_db[MAX_STREAMS] = {};
	HashSet<AudioStreamPlaybackSynchronized *> playbacks;

public:
	virtual double get_bpm() const override;
	virtual int get_beat_count() const override;
	virtual int get_bar_beats() const override;
	virtual bool has_loop() const override;
	void set_stream_count(int p_count);
	int get_stream_count() const;
	void set_sync_stream(int p_stream_index, Ref<AudioStream> p_stream);
	Ref<AudioStream> get_sync_stream(int p_stream_index) const;
	void set_sync_stream_volume(int p_stream_index, float p_db);
	float get_sync_stream_volume(int p_stream_index) const;

	virtual Ref<AudioStreamPlayback> instantiate_playback() override;
	virtual double get_length() const override;
	virtual bool is_meta_stream() const override { return true; }

	AudioStreamSynchronized();

protected:
	static void register_members();
	void _validate_property(PropertyInfo &property) const;
};

///////////////////////////////////////

class AudioStreamPlaybackSynchronized : public AudioStreamPlayback {
	ORBLIT_CLASS(AudioStreamPlaybackSynchronized, AudioStreamPlayback)
	friend class AudioStreamSynchronized;

private:
	enum {
		MIX_BUFFER_SIZE = 128
	};
	AudioFrame mix_buffer[MIX_BUFFER_SIZE];

	Ref<AudioStreamSynchronized> stream;
	Ref<AudioStreamPlayback> playback[AudioStreamSynchronized::MAX_STREAMS];

	int play_order[AudioStreamSynchronized::MAX_STREAMS];

	double stream_todo = 0.0;
	int fade_index = -1;
	double fade_volume = 1.0;
	int play_index = 0;
	double offset = 0.0;

	int loop_count = 0;

	bool active = false;

	void _update_playback_instances();

public:
	virtual void start(double p_from_pos = 0.0) override;
	virtual void stop() override;
	virtual bool is_playing() const override;
	virtual int get_loop_count() const override; // times it looped
	virtual double get_playback_position() const override;
	virtual void seek(double p_time) override;
	virtual int mix(AudioFrame *p_buffer, float p_rate_scale, int p_frames) override;

	virtual void tag_used_streams() override;

	AudioStreamPlaybackSynchronized();
	~AudioStreamPlaybackSynchronized();
};
