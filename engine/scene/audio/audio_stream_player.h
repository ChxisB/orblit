/**************************************************************************/
/*  audio_stream_player.h                                                 */

#pragma once

#include "scene/stage/entity.h"
#include "systems/audio/audio_system_enums.h"

struct AudioFrame;
class AudioStream;
class AudioStreamPlayback;
class AudioStreamPlayerInternal;

class AudioStreamPlayer : public Entity {
	ORBLIT_CLASS(AudioStreamPlayer, Entity);

public:
	enum MixTarget {
		MIX_TARGET_STEREO,
		MIX_TARGET_SURROUND,
		MIX_TARGET_CENTER
	};

private:
	AudioStreamPlayerInternal *internal = nullptr;

	MixTarget mix_target = MIX_TARGET_STEREO;

	void _set_playing(bool p_enable);
	bool _is_active() const;

	Vector<AudioFrame> _get_volume_vector();

protected:
	void _notification(int p_what);
	static void register_members();

	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	void set_stream(Ref<AudioStream> p_stream);
	Ref<AudioStream> get_stream() const;

	void set_volume_db(float p_volume);
	float get_volume_db() const;

	void set_volume_linear(float p_volume);
	float get_volume_linear() const;

	void set_pitch_scale(float p_pitch_scale);
	float get_pitch_scale() const;

	void set_max_polyphony(int p_max_polyphony);
	int get_max_polyphony() const;

	void play(float p_from_pos = 0.0);
	void seek(float p_seconds);
	void stop();
	bool is_playing() const;
	float get_playback_position();

	void set_bus(const StringName &p_bus);
	StringName get_bus() const;

	void set_autoplay(bool p_enable);
	bool is_autoplay_enabled() const;

	void set_mix_target(MixTarget p_target);
	MixTarget get_mix_target() const;

	void set_stream_paused(bool p_pause);
	bool get_stream_paused() const;

	bool has_stream_playback();
	Ref<AudioStreamPlayback> get_stream_playback();

	AuSE::PlaybackType get_playback_type() const;
	void set_playback_type(AuSE::PlaybackType p_playback_type);

	AudioStreamPlayer();
	~AudioStreamPlayer();
};

VARIANT_ENUM_CAST(AudioStreamPlayer::MixTarget)
