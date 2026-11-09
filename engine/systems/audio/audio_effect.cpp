/**************************************************************************/
/*  audio_effect.cpp                                                      */

#include "audio_effect.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void AudioEffectInstance::process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) {
	ORBLIT_VIRTUAL_CALL(_process, p_src_frames, p_dst_frames, p_frame_count);
}
bool AudioEffectInstance::process_silence() const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_process_silence, ret);
	return ret;
}

void AudioEffectInstance::register_members() {
	ORBLIT_VIRTUAL_BIND(_process, "src_buffer", "r_dst_buffer", "frame_count");
	ORBLIT_VIRTUAL_BIND(_process_silence);
}

////

Ref<AudioEffectInstance> AudioEffect::instantiate() {
	Ref<AudioEffectInstance> ret;
	ORBLIT_VIRTUAL_CALL(_instantiate, ret);
	return ret;
}
void AudioEffect::register_members() {
	ORBLIT_VIRTUAL_BIND(_instantiate);
}

AudioEffect::AudioEffect() {
}
