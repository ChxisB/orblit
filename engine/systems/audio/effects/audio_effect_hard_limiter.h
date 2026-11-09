/**************************************************************************/
/*  audio_effect_hard_limiter.h                                           */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectHardLimiter;

class AudioEffectHardLimiterInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectHardLimiterInstance, AudioEffectInstance);
	friend class AudioEffectHardLimiter;
	Ref<AudioEffectHardLimiter> base;

private:
	int sample_cursor = 0;

	float release_factor = 0;
	float attack_factor = 0;
	float gain = 1;
	float gain_target = 1;

	LocalVector<float> sample_buffer_left;
	LocalVector<float> sample_buffer_right;

	int gain_samples_to_store = 0;
	int gain_bucket_cursor = 0;
	int gain_bucket_size = 0;
	LocalVector<float> gain_buckets;

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectHardLimiter : public AudioEffect {
	ORBLIT_CLASS(AudioEffectHardLimiter, AudioEffect);

	friend class AudioEffectHardLimiterInstance;
	float pre_gain = 0.0f;
	float ceiling = -0.3f;
	float sustain = 0.02f;
	float release = 0.1f;
	const float attack = 0.002;

protected:
	static void register_members();

public:
	void set_ceiling_db(float p_ceiling);
	float get_ceiling_db() const;

	void set_release(float p_release);
	float get_release() const;

	void set_pre_gain_db(float p_pre_gain);
	float get_pre_gain_db() const;

	Ref<AudioEffectInstance> instantiate() override;
};
