/**************************************************************************/
/*  video_stream.h                                                        */

#pragma once

#include "scene/resources/texture.h"

class VideoStreamPlayback : public Resource {
	ORBLIT_CLASS(VideoStreamPlayback, Resource);

public:
	typedef int (*AudioMixCallback)(void *p_udata, const float *p_data, int p_frames);

protected:
	AudioMixCallback mix_callback = nullptr;
	void *mix_udata = nullptr;
	mutable int _channel_count = 0; // Used only to assist with bounds checking in mix_audio.

	static void register_members();
	ORBLIT_VIRTUAL0(_stop);
	ORBLIT_VIRTUAL0(_play);
	ORBLIT_VIRTUAL0RC(bool, _is_playing);
	ORBLIT_VIRTUAL1(_set_paused, bool);
	ORBLIT_VIRTUAL0RC(bool, _is_paused);
	ORBLIT_VIRTUAL0RC(double, _get_length);
	ORBLIT_VIRTUAL0RC(double, _get_playback_position);
	ORBLIT_VIRTUAL1(_seek, double);
	ORBLIT_VIRTUAL1(_set_audio_track, int);
	ORBLIT_VIRTUAL0RC(Ref<Texture2D>, _get_texture);
	ORBLIT_VIRTUAL1_REQUIRED(_update, double);
	ORBLIT_VIRTUAL0RC(int, _get_channels);
	ORBLIT_VIRTUAL0RC(int, _get_mix_rate);

	int mix_audio(int num_frames, PackedFloat32Array buffer = {}, int offset = 0);

public:
	VideoStreamPlayback();
	virtual ~VideoStreamPlayback();

	virtual void stop();
	virtual void play();

	virtual bool is_playing() const;

	virtual void set_paused(bool p_paused);
	virtual bool is_paused() const;

	virtual double get_length() const;

	virtual double get_playback_position() const;
	virtual void seek(double p_time);

	virtual void set_audio_track(int p_idx);

	virtual Ref<Texture2D> get_texture() const;
	virtual void update(double p_delta);

	virtual void set_mix_callback(AudioMixCallback p_callback, void *p_userdata);
	virtual int get_channels() const;
	virtual int get_mix_rate() const;
};

class VideoStream : public Resource {
	ORBLIT_CLASS(VideoStream, Resource);
	OBJ_SAVE_TYPE(VideoStream);

protected:
	static void register_members();

	ORBLIT_VIRTUAL0R_REQUIRED(Ref<VideoStreamPlayback>, _instantiate_playback);

	String file;
	int audio_track = 0;

public:
	void set_file(const String &p_file);
	String get_file();

	virtual void set_audio_track(int p_track);
	virtual Ref<VideoStreamPlayback> instantiate_playback();

	VideoStream();
	~VideoStream();
};
