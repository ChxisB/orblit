/**************************************************************************/
/*  audio_effect_eq.h                                                     */

#pragma once

#include "systems/audio/audio_effect.h"
#include "systems/audio/effects/eq_filter.h"

class AudioEffectEQ;

class AudioEffectEQInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectEQInstance, AudioEffectInstance);
	friend class AudioEffectEQ;
	Ref<AudioEffectEQ> base;

	Vector<EQ::BandProcess> bands[2];
	Vector<float> gains;

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectEQ : public AudioEffect {
	ORBLIT_CLASS(AudioEffectEQ, AudioEffect);

	friend class AudioEffectEQInstance;

	EQ eq;
	Vector<float> gain;
	HashMap<StringName, int> prop_band_map;
	Vector<String> band_names;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;
	void set_band_gain_db(int p_band, float p_volume);
	float get_band_gain_db(int p_band) const;
	int get_band_count() const;

	AudioEffectEQ(EQ::Preset p_preset = EQ::PRESET_6_BANDS);
};

class AudioEffectEQ6 : public AudioEffectEQ {
	ORBLIT_CLASS(AudioEffectEQ6, AudioEffectEQ);

public:
	AudioEffectEQ6() :
			AudioEffectEQ(EQ::PRESET_6_BANDS) {}
};

class AudioEffectEQ10 : public AudioEffectEQ {
	ORBLIT_CLASS(AudioEffectEQ10, AudioEffectEQ);

public:
	AudioEffectEQ10() :
			AudioEffectEQ(EQ::PRESET_10_BANDS) {}
};

class AudioEffectEQ21 : public AudioEffectEQ {
	ORBLIT_CLASS(AudioEffectEQ21, AudioEffectEQ);

public:
	AudioEffectEQ21() :
			AudioEffectEQ(EQ::PRESET_21_BANDS) {}
};
