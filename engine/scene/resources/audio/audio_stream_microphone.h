/**************************************************************************/
/*  audio_stream_microphone.h                                             */

#pragma once

#include "scene/resources/audio/audio_stream.h"

class AudioStreamPlaybackMicrophone;

class AudioStreamMicrophone : public AudioStream {
	ORBLIT_CLASS(AudioStreamMicrophone, AudioStream);
	friend class AudioStreamPlaybackMicrophone;

	HashSet<AudioStreamPlaybackMicrophone *> playbacks;

public:
	virtual Ref<AudioStreamPlayback> instantiate_playback() override;

	virtual double get_length() const override; //if supported, otherwise return 0

	virtual bool is_monophonic() const override;
};

class AudioStreamPlaybackMicrophone : public AudioStreamPlaybackResampled {
	ORBLIT_CLASS(AudioStreamPlaybackMicrophone, AudioStreamPlaybackResampled);
	friend class AudioStreamMicrophone;

	bool active = false;
	unsigned int input_ofs = 0;

	Ref<AudioStreamMicrophone> microphone;

protected:
	virtual int _mix_internal(AudioFrame *p_buffer, int p_frames) override;
	virtual float get_stream_sampling_rate() override;
	virtual double get_playback_position() const override;

public:
	virtual int mix(AudioFrame *p_buffer, float p_rate_scale, int p_frames) override;

	virtual void start(double p_from_pos = 0.0) override;
	virtual void stop() override;
	virtual bool is_playing() const override;

	virtual int get_loop_count() const override; //times it looped

	virtual void seek(double p_time) override;

	virtual void tag_used_streams() override;

	~AudioStreamPlaybackMicrophone();
	AudioStreamPlaybackMicrophone();
};
