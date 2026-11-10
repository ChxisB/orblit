/**************************************************************************/
/*  audio_driver_coreaudio.h                                              */

#pragma once

#ifdef COREAUDIO_ENABLED

#include "core/os/mutex.h"
#include "systems/audio/audio_driver.h"

#import <AudioUnit/AudioUnit.h>
#ifdef MACOS_ENABLED
#import <CoreAudio/AudioHardware.h>
#else
#import <AVFoundation/AVFoundation.h>
#endif

class AudioDriverCoreAudio : public AudioDriver {
	AudioComponentInstance audio_unit = nullptr;
	AudioComponentInstance input_unit = nullptr;

	bool active = false;
	Mutex mutex;

	String output_device_name = "Default";
	String input_device_name = "Default";

	int mix_rate = 0;
	int capture_mix_rate = 0;
	unsigned int channels = 2;
	unsigned int capture_channels = 2;
	unsigned int buffer_frames = 0;
	unsigned int capture_buffer_frames = 0;

	Vector<int32_t> samples_in;
	unsigned int buffer_size = 0;

#ifdef MACOS_ENABLED
	PackedStringArray _get_device_list(bool capture = false);
	void _set_device(const String &output_device, bool capture = false);

	static OSStatus input_device_address_cb(AudioObjectID inObjectID,
			UInt32 inNumberAddresses, const AudioObjectPropertyAddress *inAddresses,
			void *inClientData);

	static OSStatus output_device_address_cb(AudioObjectID inObjectID,
			UInt32 inNumberAddresses, const AudioObjectPropertyAddress *inAddresses,
			void *inClientData);
#endif

	static OSStatus output_callback(void *inRefCon,
			AudioUnitRenderActionFlags *ioActionFlags,
			const AudioTimeStamp *inTimeStamp,
			UInt32 inBusNumber, UInt32 inNumberFrames,
			AudioBufferList *ioData);

	static OSStatus input_callback(void *inRefCon,
			AudioUnitRenderActionFlags *ioActionFlags,
			const AudioTimeStamp *inTimeStamp,
			UInt32 inBusNumber, UInt32 inNumberFrames,
			AudioBufferList *ioData);

	Error init_input_device();
	void finish_input_device();
	void _release_output_unit();

public:
	virtual const char *get_name() const override {
		return "CoreAudio";
	}

	virtual Error init() override;
	virtual void start() override;
	virtual bool can_output_device_sleep() const override { return true; }
	virtual bool set_output_device_sleep(bool p_enable) override;
	virtual int get_mix_rate() const override;
	virtual int get_input_mix_rate() const override;
	virtual SpeakerMode get_speaker_mode() const override;

	virtual void lock() override;
	virtual void unlock() override;
	virtual void finish() override;

#ifdef MACOS_ENABLED
	virtual PackedStringArray get_output_device_list() override;
	virtual String get_output_device() override;
	virtual void set_output_device(const String &p_name) override;

	virtual PackedStringArray get_input_device_list() override;
	virtual String get_input_device() override;
	virtual void set_input_device(const String &p_name) override;
#endif

	virtual Error input_start() override;
	virtual Error input_stop() override;

	bool try_lock();
	void stop();

	AudioDriverCoreAudio();
	~AudioDriverCoreAudio() {}
};

#endif // COREAUDIO_ENABLED
