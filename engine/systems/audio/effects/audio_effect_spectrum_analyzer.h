/**************************************************************************/
/*  audio_effect_spectrum_analyzer.h                                      */

#pragma once

#include "systems/audio/audio_effect.h"

class AudioEffectSpectrumAnalyzer;

class AudioEffectSpectrumAnalyzerInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectSpectrumAnalyzerInstance, AudioEffectInstance);

public:
	enum MagnitudeMode {
		MAGNITUDE_AVERAGE,
		MAGNITUDE_MAX,
	};

private:
	friend class AudioEffectSpectrumAnalyzer;
	Ref<AudioEffectSpectrumAnalyzer> base;

	Vector<Vector<AudioFrame>> fft_history;
	Vector<float> temporal_fft;
	int temporal_fft_pos;
	int fft_size;
	int fft_count;
	int fft_pos;
	float mix_rate;

protected:
	static void register_members();

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
	Vector2 get_magnitude_for_frequency_range(float p_begin, float p_end, MagnitudeMode p_mode = MAGNITUDE_MAX) const;
};

VARIANT_ENUM_CAST(AudioEffectSpectrumAnalyzerInstance::MagnitudeMode)

class AudioEffectSpectrumAnalyzer : public AudioEffect {
	ORBLIT_CLASS(AudioEffectSpectrumAnalyzer, AudioEffect);

public:
	enum FFTSize {
		FFT_SIZE_256,
		FFT_SIZE_512,
		FFT_SIZE_1024,
		FFT_SIZE_2048,
		FFT_SIZE_4096,
		FFT_SIZE_MAX
	};

public:
	friend class AudioEffectSpectrumAnalyzerInstance;
	float buffer_length;
	FFTSize fft_size;

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;
	void set_buffer_length(float p_seconds);
	float get_buffer_length() const;

	void set_fft_size(FFTSize);
	FFTSize get_fft_size() const;

	AudioEffectSpectrumAnalyzer();
};

VARIANT_ENUM_CAST(AudioEffectSpectrumAnalyzer::FFTSize);
