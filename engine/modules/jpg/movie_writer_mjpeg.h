/**************************************************************************/
/*  movie_writer_mjpeg.h                                                  */

#pragma once

#include "systems/audio/audio_system_enums.h"
#include "systems/movie_writer/movie_writer.h"

class MovieWriterMJPEG : public MovieWriter {
	ORBLIT_CLASS(MovieWriterMJPEG, MovieWriter)

	uint32_t mix_rate = 48000;
	AuSE::SpeakerMode speaker_mode = AuSE::SPEAKER_MODE_STEREO;
	uint32_t audio_bit_depth = 16;
	String base_path;
	uint32_t frame_count = 0;
	uint32_t fps = 0;
	float quality = 0.75;

	uint32_t audio_block_size = 0;

	Vector<uint32_t> jpg_frame_sizes;

	uint64_t total_frames_ofs = 0;
	uint64_t total_frames_ofs2 = 0;
	uint64_t total_frames_ofs3 = 0;
	uint64_t total_audio_frames_ofs4 = 0;
	uint64_t movi_data_ofs = 0;

	Ref<FileAccess> f;

protected:
	virtual uint32_t get_audio_mix_rate() const override;
	virtual AuSE::SpeakerMode get_audio_speaker_mode() const override;
	virtual void get_supported_extensions(List<String> *r_extensions) const override;

	virtual Error write_begin(const Size2i &p_movie_size, uint32_t p_fps, const String &p_base_path) override;
	virtual Error write_frame(const Ref<Image> &p_image, const int32_t *p_audio_data) override;
	virtual void write_end() override;

	virtual bool handles_file(const String &p_path) const override;

public:
	MovieWriterMJPEG();
};
