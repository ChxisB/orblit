/**************************************************************************/
/*  audio_stream_player_internal.h                                        */

#pragma once

#include "core/containers/safe_refcount.h"
#include "core/object/ref_counted.h"
#include "systems/audio/audio_system_enums.h"

class AudioStream;
class AudioStreamPlayback;
class AudioSamplePlayback;
class Entity;

class AudioStreamPlayerInternal : public Object {
	ORBLIT_CLASS(AudioStreamPlayerInternal, Object);

private:
	struct ParameterData {
		StringName path;
		Variant value;
	};

	static inline const String PARAM_PREFIX = "parameters/";

	Entity *node = nullptr;
	Callable play_callable;
	Callable stop_callable;
	bool physical = false;
	AuSE::PlaybackType playback_type = AuSE::PlaybackType::PLAYBACK_TYPE_DEFAULT;

	HashMap<StringName, ParameterData> playback_parameters;

	void _set_process(bool p_enabled);
	void _update_stream_parameters();

	bool _is_sample();

public:
	Vector<Ref<AudioStreamPlayback>> stream_playbacks;
	Ref<AudioStream> stream;

	SafeFlag active;

	float pitch_scale = 1.0;
	float volume_db = 0.0;
	bool autoplay = false;
	StringName bus;
	int max_polyphony = 1;

	void process();
	void ensure_playback_limit();

	void notification(int p_what);
	bool set(const StringName &p_name, const Variant &p_value);
	bool get(const StringName &p_name, Variant &r_ret) const;
	void get_property_list(List<PropertyInfo> *p_list) const;

	void set_stream(Ref<AudioStream> p_stream);
	void set_pitch_scale(float p_pitch_scale);
	void set_max_polyphony(int p_max_polyphony);

	StringName get_bus() const;

	Ref<AudioStreamPlayback> play_basic();
	void seek(float p_seconds);
	void stop_basic();
	bool is_playing() const;
	float get_playback_position();

	void set_playing(bool p_enable);
	bool is_active() const;

	void set_stream_paused(bool p_pause);
	bool get_stream_paused() const;

	bool has_stream_playback();
	Ref<AudioStreamPlayback> get_stream_playback();

	void set_playback_type(AuSE::PlaybackType p_playback_type);
	AuSE::PlaybackType get_playback_type() const;

	AudioStreamPlayerInternal(Entity *p_node, const Callable &p_play_callable, const Callable &p_stop_callable, bool p_physical);
};
