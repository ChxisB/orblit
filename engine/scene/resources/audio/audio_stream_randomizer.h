/**************************************************************************/
/*  audio_stream_randomizer.h                                             */

#pragma once

#include "scene/property_list_helper.h"
#include "scene/resources/audio/audio_stream.h"

class AudioStreamPlaybackRandomizer;

class AudioStreamRandomizer : public AudioStream {
	ORBLIT_CLASS(AudioStreamRandomizer, AudioStream);

public:
	enum PlaybackMode {
		PLAYBACK_RANDOM_NO_REPEATS,
		PLAYBACK_RANDOM,
		PLAYBACK_SEQUENTIAL,
	};

private:
	friend class AudioStreamPlaybackRandomizer;

	struct PoolEntry {
		Ref<AudioStream> stream;
		float weight = 1.0;
	};

	static inline PropertyListHelper base_property_helper;
	PropertyListHelper property_helper;

	HashSet<AudioStreamPlaybackRandomizer *> playbacks;
	Vector<PoolEntry> audio_stream_pool;
	float random_pitch_scale = 1.0f;
	float random_volume_offset_db = 0.0f;

	Ref<AudioStreamPlayback> instance_playback_random();
	Ref<AudioStreamPlayback> instance_playback_no_repeats();
	Ref<AudioStreamPlayback> instance_playback_sequential();

	Ref<AudioStream> last_playback = nullptr;
	PlaybackMode playback_mode = PLAYBACK_RANDOM_NO_REPEATS;

protected:
	static void register_members();

	bool _set(const StringName &p_name, const Variant &p_value) { return property_helper.property_set_value(p_name, p_value); }
	bool _get(const StringName &p_name, Variant &r_ret) const { return property_helper.property_get_value(p_name, r_ret); }
	void _get_property_list(List<PropertyInfo> *p_list) const { property_helper.get_property_list(p_list); }
	bool _property_can_revert(const StringName &p_name) const { return property_helper.property_can_revert(p_name); }
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const { return property_helper.property_get_revert(p_name, r_property); }

public:
	void add_stream(int p_index, Ref<AudioStream> p_stream, float p_weight = 1.0);
	void move_stream(int p_index_from, int p_index_to);
	void remove_stream(int p_index);

	void set_stream(int p_index, Ref<AudioStream> p_stream);
	Ref<AudioStream> get_stream(int p_index) const;
	void set_stream_probability_weight(int p_index, float p_weight);
	float get_stream_probability_weight(int p_index) const;

	void set_streams_count(int p_count);
	int get_streams_count() const;

	void set_random_pitch_semitones(float p_pitch_semitones);
	float get_random_pitch_semitones() const;

	void set_random_pitch(float p_pitch_scale);
	float get_random_pitch() const;

	void set_random_volume_offset_db(float p_volume_offset_db);
	float get_random_volume_offset_db() const;

	void set_playback_mode(PlaybackMode p_playback_mode);
	PlaybackMode get_playback_mode() const;

	virtual Ref<AudioStreamPlayback> instantiate_playback() override;

	virtual double get_length() const override; //if supported, otherwise return 0
	virtual bool is_monophonic() const override;

	virtual bool is_meta_stream() const override { return true; }

	AudioStreamRandomizer();
};

class AudioStreamPlaybackRandomizer : public AudioStreamPlayback {
	ORBLIT_CLASS(AudioStreamPlaybackRandomizer, AudioStreamPlayback);
	friend class AudioStreamRandomizer;

	Ref<AudioStreamRandomizer> randomizer;
	Ref<AudioStreamPlayback> playback;
	Ref<AudioStreamPlayback> playing;

	float pitch_scale;
	float volume_scale;

public:
	virtual void start(double p_from_pos = 0.0) override;
	virtual void stop() override;
	virtual bool is_playing() const override;

	virtual int get_loop_count() const override; //times it looped

	virtual double get_playback_position() const override;
	virtual void seek(double p_time) override;

	virtual int mix(AudioFrame *p_buffer, float p_rate_scale, int p_frames) override;

	virtual void tag_used_streams() override;

	~AudioStreamPlaybackRandomizer();
};

VARIANT_ENUM_CAST(AudioStreamRandomizer::PlaybackMode);
