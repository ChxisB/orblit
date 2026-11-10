/**************************************************************************/
/*  audio_effect_pitch_shift.h                                            */

#pragma once

#include "systems/audio/audio_effect.h"

class SMBPitchShift {
	enum {
		MAX_FRAME_LENGTH = 8192
	};

	float gInFIFO[MAX_FRAME_LENGTH] = {};
	float gOutFIFO[MAX_FRAME_LENGTH] = {};
	float gFFTworksp[2 * MAX_FRAME_LENGTH] = {};
	float gLastPhase[MAX_FRAME_LENGTH / 2 + 1] = {};
	float gSumPhase[MAX_FRAME_LENGTH / 2 + 1] = {};
	float gOutputAccum[2 * MAX_FRAME_LENGTH] = {};
	float gAnaFreq[MAX_FRAME_LENGTH] = {};
	float gAnaMagn[MAX_FRAME_LENGTH] = {};
	float gSynFreq[MAX_FRAME_LENGTH] = {};
	float gSynMagn[MAX_FRAME_LENGTH] = {};
	long gRover = 0;

	void smbFft(float *fftBuffer, long fftFrameSize, long sign);

public:
	void PitchShift(float pitchShift, long numSampsToProcess, long fftFrameSize, long osamp, float sampleRate, float *indata, float *outdata, int stride);
};

class AudioEffectPitchShift;

class AudioEffectPitchShiftInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectPitchShiftInstance, AudioEffectInstance);
	friend class AudioEffectPitchShift;
	Ref<AudioEffectPitchShift> base;

	int fft_size = 0;
	SMBPitchShift shift_l;
	SMBPitchShift shift_r;

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
};

class AudioEffectPitchShift : public AudioEffect {
	ORBLIT_CLASS(AudioEffectPitchShift, AudioEffect);

public:
	friend class AudioEffectPitchShiftInstance;

	enum FFTSize : unsigned int {
		FFT_SIZE_256,
		FFT_SIZE_512,
		FFT_SIZE_1024,
		FFT_SIZE_2048,
		FFT_SIZE_4096,
		FFT_SIZE_MAX
	};

	float pitch_scale = 1.0;
	int oversampling = 4;
	FFTSize fft_size = FFT_SIZE_2048;
	float wet = 0.0;
	float dry = 0.0;
	bool filter = false;

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;

	void set_pitch_scale(float p_pitch_scale);
	float get_pitch_scale() const;

	void set_oversampling(int p_oversampling);
	int get_oversampling() const;

	void set_fft_size(FFTSize);
	FFTSize get_fft_size() const;
};

VARIANT_ENUM_CAST(AudioEffectPitchShift::FFTSize);
