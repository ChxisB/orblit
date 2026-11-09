/**************************************************************************/
/*  audio_effect_capture.h                                                */

#pragma once

#include "core/containers/ring_buffer.h"
#include "core/object/ref_counted.h"
#include "systems/audio/audio_effect.h"
#include "systems/audio/audio_frame.h"

class AudioEffectCapture;

class AudioEffectCaptureInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectCaptureInstance, AudioEffectInstance);
	friend class AudioEffectCapture;
	Ref<AudioEffectCapture> base;

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
	virtual bool process_silence() const override;
};

class AudioEffectCapture : public AudioEffect {
	ORBLIT_CLASS(AudioEffectCapture, AudioEffect)
	friend class AudioEffectCaptureInstance;

	RingBuffer<AudioFrame> buffer;
	SafeNumeric<uint64_t> discarded_frames;
	SafeNumeric<uint64_t> pushed_frames;
	float buffer_length_seconds = 0.1f;
	bool buffer_initialized = false;

protected:
	static void register_members();

public:
	virtual Ref<AudioEffectInstance> instantiate() override;

	void set_buffer_length(float p_buffer_length_seconds);
	float get_buffer_length();

	bool can_get_buffer(int p_frames) const;
	PackedVector2Array get_buffer(int p_len);
	void clear_buffer();

	int get_frames_available() const;
	int64_t get_discarded_frames() const;
	int get_buffer_length_frames() const;
	int64_t get_pushed_frames() const;
};
