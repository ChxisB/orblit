/**************************************************************************/
/*  audio_effect_filter.h                                                 */

#pragma once

#include "systems/audio/audio_effect.h"
#include "systems/audio/audio_filter_sw.h"

class AudioEffectFilter;

class AudioEffectFilterInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectFilterInstance, AudioEffectInstance);
	friend class AudioEffectFilter;

	Ref<AudioEffectFilter> base;

	AudioFilterSW filter;
	AudioFilterSW::Processor filter_process[2][4];

	template <int S>
	void _process_filter(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count);

public:
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;

	AudioEffectFilterInstance();
};

class AudioEffectFilter : public AudioEffect {
	ORBLIT_CLASS(AudioEffectFilter, AudioEffect);

public:
	enum FilterDB {
		FILTER_6DB,
		FILTER_12DB,
		FILTER_18DB,
		FILTER_24DB,
	};
	friend class AudioEffectFilterInstance;

	AudioFilterSW::Mode mode;
	float cutoff;
	float resonance;
	float gain;
	FilterDB db;

protected:
	static void register_members();

public:
	void set_cutoff(float p_freq);
	float get_cutoff() const;

	void set_resonance(float p_amount);
	float get_resonance() const;

	void set_gain(float p_amount);
	float get_gain() const;

	void set_db(FilterDB p_db);
	FilterDB get_db() const;

	Ref<AudioEffectInstance> instantiate() override;

	AudioEffectFilter(AudioFilterSW::Mode p_mode = AudioFilterSW::LOWPASS);
};

VARIANT_ENUM_CAST(AudioEffectFilter::FilterDB)

class AudioEffectLowPassFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectLowPassFilter, AudioEffectFilter);

	void _validate_property(PropertyInfo &p_property) const {
		if (p_property.name == "gain") {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	}

public:
	AudioEffectLowPassFilter() :
			AudioEffectFilter(AudioFilterSW::LOWPASS) {}
};

class AudioEffectHighPassFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectHighPassFilter, AudioEffectFilter);
	void _validate_property(PropertyInfo &p_property) const {
		if (p_property.name == "gain") {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	}

public:
	AudioEffectHighPassFilter() :
			AudioEffectFilter(AudioFilterSW::HIGHPASS) {}
};

class AudioEffectBandPassFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectBandPassFilter, AudioEffectFilter);
	void _validate_property(PropertyInfo &p_property) const {
		if (p_property.name == "gain") {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	}

public:
	AudioEffectBandPassFilter() :
			AudioEffectFilter(AudioFilterSW::BANDPASS) {}
};

class AudioEffectNotchFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectNotchFilter, AudioEffectFilter);

protected:
	void _validate_property(PropertyInfo &p_property) const {
		if (p_property.name == "gain") {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	}

public:
	AudioEffectNotchFilter() :
			AudioEffectFilter(AudioFilterSW::NOTCH) {}
};

class AudioEffectBandLimitFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectBandLimitFilter, AudioEffectFilter);

protected:
	void _validate_property(PropertyInfo &p_property) const {
		if (p_property.name == "gain") {
			p_property.usage = PROPERTY_USAGE_NONE;
		}
	}

public:
	AudioEffectBandLimitFilter() :
			AudioEffectFilter(AudioFilterSW::BANDLIMIT) {}
};

class AudioEffectLowShelfFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectLowShelfFilter, AudioEffectFilter);

public:
	AudioEffectLowShelfFilter() :
			AudioEffectFilter(AudioFilterSW::LOWSHELF) {}
};

class AudioEffectHighShelfFilter : public AudioEffectFilter {
	ORBLIT_CLASS(AudioEffectHighShelfFilter, AudioEffectFilter);

public:
	AudioEffectHighShelfFilter() :
			AudioEffectFilter(AudioFilterSW::HIGHSHELF) {}
};
