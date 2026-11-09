/**************************************************************************/
/*  audio_stream_player_planar.h                                              */

#pragma once

#include "scene/planar/entity_planar.h"
#include "systems/audio/audio_system_enums.h"

struct AudioFrame;
class AudioStream;
class AudioStreamPlayback;
class AudioStreamPlayerInternal;

class AudioStreamPlayer2D : public Entity2D {
	ORBLIT_CLASS(AudioStreamPlayer2D, Entity2D);

private:
	enum {
		MAX_OUTPUTS = 8,
		MAX_INTERSECT_AREAS = 32

	};

	AudioStreamPlayerInternal *internal = nullptr;

	SafeNumeric<float> setplay{ -1.0 };
	Ref<AudioStreamPlayback> setplayback;

	Vector<AudioFrame> volume_vector;

	uint64_t last_mix_count = -1;
	bool force_update_panning = false;

	AuSE::PlaybackType playback_type = AuSE::PlaybackType::PLAYBACK_TYPE_DEFAULT;

	void _set_playing(bool p_enable);
	bool _is_active() const;

	StringName _get_actual_bus();
	void _update_panning();

	static void _listener_changed_cb(void *self) { reinterpret_cast<AudioStreamPlayer2D *>(self)->force_update_panning = true; }

	uint32_t area_mask = 0;

	float max_distance = 2000.0;
	float attenuation = 1.0;

	float panning_strength = 1.0f;
	float cached_global_panning_strength = 0.5f;

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

	void play(float p_from_pos = 0.0);
	void seek(float p_seconds);
	void stop();
	bool is_playing() const;
	float get_playback_position();

	void set_bus(const StringName &p_bus);
	StringName get_bus() const;

	void set_autoplay(bool p_enable);
	bool is_autoplay_enabled() const;

	void set_max_distance(float p_pixels);
	float get_max_distance() const;

	void set_attenuation(float p_curve);
	float get_attenuation() const;

	void set_area_mask(uint32_t p_mask);
	uint32_t get_area_mask() const;

	void set_stream_paused(bool p_pause);
	bool get_stream_paused() const;

	void set_max_polyphony(int p_max_polyphony);
	int get_max_polyphony() const;

	void set_panning_strength(float p_panning_strength);
	float get_panning_strength() const;

	bool has_stream_playback();
	Ref<AudioStreamPlayback> get_stream_playback();

	AuSE::PlaybackType get_playback_type() const;
	void set_playback_type(AuSE::PlaybackType p_playback_type);

	AudioStreamPlayer2D();
	~AudioStreamPlayer2D();
};
