/**************************************************************************/
/*  movie_writer.h                                                        */

#pragma once

#include "core/io/image.h"
#include "core/containers/local_vector.h"
#include "systems/audio/audio_system.h" // IWYU pragma: keep. Bind ORBLIT_VIRTUAL0RC_REQUIRED.
#include "systems/audio/audio_system_enums.h"

class MovieWriter : public Object {
	ORBLIT_CLASS(MovieWriter, Object);

	uint64_t fps = 0;
	uint64_t mix_rate = 0;
	uint32_t audio_channels = 0;

	// The output resolution, which can differ from the window size.
	// Used as a base for resizing all subsequent frames if their resolution differs.
	Vector2i movie_size;

	float cpu_time = 0.0f;
	float gpu_time = 0.0f;
	uint64_t encoding_time_usec = 0;

	String project_name;

	LocalVector<int32_t> audio_mix_buffer;

	enum {
		MAX_WRITERS = 8
	};
	static MovieWriter *writers[];
	static uint32_t writer_count;

protected:
	virtual uint32_t get_audio_mix_rate() const;
	virtual AuSE::SpeakerMode get_audio_speaker_mode() const;

	virtual Error write_begin(const Size2i &p_movie_size, uint32_t p_fps, const String &p_base_path);
	virtual Error write_frame(const Ref<Image> &p_image, const int32_t *p_audio_data);
	virtual void write_end();

	ORBLIT_VIRTUAL0RC_REQUIRED(uint32_t, _get_audio_mix_rate)
	ORBLIT_VIRTUAL0RC_REQUIRED(AuSE::SpeakerMode, _get_audio_speaker_mode)

	ORBLIT_VIRTUAL1RC_REQUIRED(bool, _handles_file, const String &)
	ORBLIT_VIRTUAL0RC_REQUIRED(Vector<String>, _get_supported_extensions)

	ORBLIT_VIRTUAL3R_REQUIRED(Error, _write_begin, const Size2i &, uint32_t, const String &)
	ORBLIT_VIRTUAL2R_REQUIRED(Error, _write_frame, const Ref<Image> &, NativeExtensionPtr<const int32_t>)
	ORBLIT_VIRTUAL0_REQUIRED(_write_end)

	static void register_members();

public:
	virtual bool handles_file(const String &p_path) const;
	virtual void get_supported_extensions(List<String> *r_extensions) const;

	static void add_writer(MovieWriter *p_writer);
	static MovieWriter *find_writer_for_file(const String &p_file);

	void begin(const Size2i &p_movie_size, uint32_t p_fps, const String &p_base_path);
	void add_frame();

	static void set_extensions_hint();

	void end();
};
