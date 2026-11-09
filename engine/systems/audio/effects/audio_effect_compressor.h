/**************************************************************************/
/*  audio_effect_compressor.h                                             */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectCompressor;

class AudioEffectCompressorInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectCompressorInstance, AudioEffectInstance);
	friend class AudioEffectCompressor;
	Ref<AudioEffectCompressor> base;

	float rundb, averatio, runratio, runmax, maxover, gr_meter;
	int current_channel;

public:
	void set_current_channel(int p_channel) { current_channel = p_channel; }
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectCompressor : public AudioEffect {
	ORBLIT_CLASS(AudioEffectCompressor, AudioEffect);

	friend class AudioEffectCompressorInstance;
	float threshold;
	float ratio;
	float gain;
	float attack_us;
	float release_ms;
	float mix;
	StringName sidechain;

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;

	void set_threshold(float p_threshold);
	float get_threshold() const;

	void set_ratio(float p_ratio);
	float get_ratio() const;

	void set_gain(float p_gain);
	float get_gain() const;

	void set_attack_us(float p_attack_us);
	float get_attack_us() const;

	void set_release_ms(float p_release_ms);
	float get_release_ms() const;

	void set_mix(float p_mix);
	float get_mix() const;

	void set_sidechain(const StringName &p_sidechain);
	StringName get_sidechain() const;

	AudioEffectCompressor();
};
