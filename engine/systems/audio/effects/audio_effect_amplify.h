/**************************************************************************/
/*  audio_effect_amplify.h                                                */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectAmplify;

class AudioEffectAmplifyInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectAmplifyInstance, AudioEffectInstance);
	friend class AudioEffectAmplify;
	Ref<AudioEffectAmplify> base;

	float mix_volume_db;

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectAmplify : public AudioEffect {
	ORBLIT_CLASS(AudioEffectAmplify, AudioEffect);

	friend class AudioEffectAmplifyInstance;
	float volume_db;

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;
	void set_volume_db(float p_volume);
	float get_volume_db() const;

	void set_volume_linear(float p_volume);
	float get_volume_linear() const;

	AudioEffectAmplify();
};
