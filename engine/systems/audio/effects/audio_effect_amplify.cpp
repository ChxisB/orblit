/**************************************************************************/
/*  audio_effect_amplify.cpp                                              */

#include "audio_effect_amplify.h"

#include "core/object/class_registry.h"

void AudioEffectAmplifyInstance::process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) {
	//multiply volume interpolating to avoid clicks if this changes
	float volume_db = base->volume_db;
	float vol = Math::db_to_linear(mix_volume_db);
	float vol_inc = (Math::db_to_linear(volume_db) - vol) / float(p_frame_count);

	for (int i = 0; i < p_frame_count; i++) {
		p_dst_frames[i] = p_src_frames[i] * vol;
		vol += vol_inc;
	}
	//set volume for next mix
	mix_volume_db = volume_db;
}

Ref<AudioEffectInstance> AudioEffectAmplify::instantiate() {
	Ref<AudioEffectAmplifyInstance> ins;
	ins.instantiate();
	ins->base = Ref<AudioEffectAmplify>(this);
	ins->mix_volume_db = volume_db;
	return ins;
}

void AudioEffectAmplify::set_volume_db(float p_volume) {
	volume_db = p_volume;
}

float AudioEffectAmplify::get_volume_db() const {
	return volume_db;
}

void AudioEffectAmplify::set_volume_linear(float p_volume) {
	set_volume_db(Math::linear_to_db(p_volume));
}

float AudioEffectAmplify::get_volume_linear() const {
	return Math::db_to_linear(get_volume_db());
}

void AudioEffectAmplify::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_volume_db", "volume"), &AudioEffectAmplify::set_volume_db);
	ClassRegistry::bind_method(D_METHOD("get_volume_db"), &AudioEffectAmplify::get_volume_db);
	ClassRegistry::bind_method(D_METHOD("set_volume_linear", "volume"), &AudioEffectAmplify::set_volume_linear);
	ClassRegistry::bind_method(D_METHOD("get_volume_linear"), &AudioEffectAmplify::get_volume_linear);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "volume_db", PROPERTY_HINT_RANGE, "-80,24,0.01,suffix:dB"), "set_volume_db", "get_volume_db");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "volume_linear", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_volume_linear", "get_volume_linear");
}

AudioEffectAmplify::AudioEffectAmplify() {
	volume_db = 0;
}
