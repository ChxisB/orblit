/**************************************************************************/
/*  audio_effect_panner.h                                                 */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectPanner;

class AudioEffectPannerInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectPannerInstance, AudioEffectInstance);
	friend class AudioEffectPanner;
	Ref<AudioEffectPanner> base;

	float mix_pan = 0;

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectPanner : public AudioEffect {
	ORBLIT_CLASS(AudioEffectPanner, AudioEffect);

	friend class AudioEffectPannerInstance;
	float pan;

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;
	void set_pan(float p_cpanume);
	float get_pan() const;

	AudioEffectPanner();
};
