/**************************************************************************/
/*  audio_effect.h                                                        */

#pragma once

#include "core/io/resource.h"
#include "core/object/orblit_virtual.gen.h"
#include "core/variant/native_ptr.h"
#include "systems/audio/audio_frame.h"

class AudioEffectInstance : public RefCounted {
	ORBLIT_CLASS(AudioEffectInstance, RefCounted);

protected:
	ORBLIT_VIRTUAL3_REQUIRED(_process, NativeExtensionPtr<const AudioFrame>, NativeExtensionPtr<AudioFrame>, int)
	ORBLIT_VIRTUAL0RC(bool, _process_silence)
	static void register_members();

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count);
	virtual bool process_silence() const;
};

class AudioEffect : public Resource {
	ORBLIT_CLASS(AudioEffect, Resource);

protected:
	ORBLIT_VIRTUAL0R_REQUIRED(Ref<AudioEffectInstance>, _instantiate)
	static void register_members();

public:
	virtual Ref<AudioEffectInstance> instantiate();
	AudioEffect();
};
