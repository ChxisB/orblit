/**************************************************************************/
/*  audio_effect_panner.cpp                                               */

#include "audio_effect_panner.h"

#include "core/object/class_registry.h"

void AudioEffectPannerInstance::process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) {
	// Interpolate pan to avoid clicks on fast changes.
	float pan = base->pan;
	float p = mix_pan;
	float pan_inc = (pan - mix_pan) / float(p_frame_count);

	for (int i = 0; i < p_frame_count; i++) {
		float lvol = CLAMP(1.0 - p, 0, 1);
		float rvol = CLAMP(1.0 + p, 0, 1);
		p_dst_frames[i].left = p_src_frames[i].left * lvol + p_src_frames[i].right * (1.0 - rvol);
		p_dst_frames[i].right = p_src_frames[i].right * rvol + p_src_frames[i].left * (1.0 - lvol);
		p += pan_inc;
	}
	// Set pan for next mix.
	mix_pan = pan;
}

Ref<AudioEffectInstance> AudioEffectPanner::instantiate() {
	Ref<AudioEffectPannerInstance> ins;
	ins.instantiate();
	ins->base = Ref<AudioEffectPanner>(this);
	ins->mix_pan = pan;
	return ins;
}

void AudioEffectPanner::set_pan(float p_cpanume) {
	pan = p_cpanume;
}

float AudioEffectPanner::get_pan() const {
	return pan;
}

void AudioEffectPanner::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_pan", "cpanume"), &AudioEffectPanner::set_pan);
	ClassRegistry::bind_method(D_METHOD("get_pan"), &AudioEffectPanner::get_pan);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "pan", PROPERTY_HINT_RANGE, "-1,1,0.01"), "set_pan", "get_pan");
}

AudioEffectPanner::AudioEffectPanner() {
	pan = 0;
}
