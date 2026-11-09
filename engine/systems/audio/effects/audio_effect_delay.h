/**************************************************************************/
/*  audio_effect_delay.h                                                  */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectDelay;

class AudioEffectDelayInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectDelayInstance, AudioEffectInstance);

	friend class AudioEffectDelay;
	Ref<AudioEffectDelay> base;

	Vector<AudioFrame> ring_buffer;

	unsigned int ring_buffer_pos;
	unsigned int ring_buffer_mask;

	/* feedback buffer */
	Vector<AudioFrame> feedback_buffer;

	unsigned int feedback_buffer_pos;

	AudioFrame h;
	void _process_chunk(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count);

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectDelay : public AudioEffect {
	ORBLIT_CLASS(AudioEffectDelay, AudioEffect);

	friend class AudioEffectDelayInstance;
	enum {
		MAX_DELAY_MS = 3000,
		MAX_TAPS = 2
	};

	float dry = 1.0f;

	bool tap_1_active = true;
	float tap_1_delay_ms = 250.0f;
	float tap_1_level = -6.0f;
	float tap_1_pan = 0.2f;

	bool tap_2_active = true;
	float tap_2_delay_ms = 500.0f;
	float tap_2_level = -12.0f;
	float tap_2_pan = -0.4f;

	bool feedback_active = false;
	float feedback_delay_ms = 340.0f;
	float feedback_level = -6.0f;
	float feedback_lowpass = 16000.0f;

protected:
	static void register_members();

public:
	void set_dry(float p_dry);
	float get_dry();

	void set_tap1_active(bool p_active);
	bool is_tap1_active() const;

	void set_tap1_delay_ms(float p_delay_ms);
	float get_tap1_delay_ms() const;

	void set_tap1_level_db(float p_level_db);
	float get_tap1_level_db() const;

	void set_tap1_pan(float p_pan);
	float get_tap1_pan() const;

	void set_tap2_active(bool p_active);
	bool is_tap2_active() const;

	void set_tap2_delay_ms(float p_delay_ms);
	float get_tap2_delay_ms() const;

	void set_tap2_level_db(float p_level_db);
	float get_tap2_level_db() const;

	void set_tap2_pan(float p_pan);
	float get_tap2_pan() const;

	void set_feedback_active(bool p_active);
	bool is_feedback_active() const;

	void set_feedback_delay_ms(float p_delay_ms);
	float get_feedback_delay_ms() const;

	void set_feedback_level_db(float p_level_db);
	float get_feedback_level_db() const;

	void set_feedback_lowpass(float p_lowpass);
	float get_feedback_lowpass() const;

	Ref<AudioEffectInstance> instantiate() override;

	AudioEffectDelay() {}
};
