/**************************************************************************/
/*  audio_effect_record.h                                                 */

#pragma once

#include "core/os/thread.h"
#include "scene/resources/audio/audio_stream_wav.h"
#include "systems/audio/audio_effect.h"

class AudioEffectRecord;

class AudioEffectRecordInstance : public AudioEffectInstance {
	ORBLIT_CLASS(AudioEffectRecordInstance, AudioEffectInstance);
	friend class AudioEffectRecord;

	bool is_recording;
	Thread io_thread;

	Vector<AudioFrame> ring_buffer;
	Vector<float> recording_data;

	unsigned int ring_buffer_pos;
	unsigned int ring_buffer_mask;
	unsigned int ring_buffer_read_pos;

	void _io_thread_process();
	void _io_store_buffer();
	static void _thread_callback(void *_instance);
	void _init_recording();
	void _update_buffer();
	static void _update(void *userdata);

public:
	void init();
	void finish();
	virtual void process(const AudioFrame *p_src_frames, AudioFrame *p_dst_frames, int p_frame_count) override;
	virtual bool process_silence() const override;
};

class AudioEffectRecord : public AudioEffect {
	ORBLIT_CLASS(AudioEffectRecord, AudioEffect);

	friend class AudioEffectRecordInstance;

	enum {
		IO_BUFFER_SIZE_MS = 1500
	};

	Ref<AudioEffectRecordInstance> current_instance;

	AudioStreamWAV::Format format;

	void ensure_thread_stopped();

protected:
	static void register_members();

public:
	Ref<AudioEffectInstance> instantiate() override;
	void set_recording_active(bool p_record);
	bool is_recording_active() const;
	void set_format(AudioStreamWAV::Format p_format);
	AudioStreamWAV::Format get_format() const;
	Ref<AudioStreamWAV> get_recording() const;
	AudioEffectRecord();
	~AudioEffectRecord();
};
