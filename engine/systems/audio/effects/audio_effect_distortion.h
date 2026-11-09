/**************************************************************************/
/*  audio_effect_distortion.h                                             */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectDistortion;

class AudioEffectDistortionInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectDistortionInstance, AudioEffectInstance);
	friend class AudioEffectDistortion;
	Ref<AudioEffectDistortion> base;
	float h[2];

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectDistortion : public AudioEffect {
	ORBLIT_CLASS(AudioEffectDistortion, AudioEffect);

public:
	enum Mode {
		MODE_CLIP,
		MODE_ATAN,
		MODE_LOFI,
		MODE_OVERDRIVE,
		MODE_WAVESHAPE,
	};

	friend class AudioEffectDistortionInstance;
	Mode mode;
	float pre_gain;
	float post_gain;
	float keep_hf_hz;
	float drive;

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;

	void set_mode(Mode p_mode);
	Mode get_mode() const;

	void set_pre_gain(float p_pre_gain);
	float get_pre_gain() const;

	void set_keep_hf_hz(float p_keep_hf_hz);
	float get_keep_hf_hz() const;

	void set_drive(float p_drive);
	float get_drive() const;

	void set_post_gain(float p_post_gain);
	float get_post_gain() const;

	AudioEffectDistortion();
};

VARIANT_ENUM_CAST(AudioEffectDistortion::Mode)
