/**************************************************************************/
/*  movie_writer_pngwav.h                                                 */

#pragma once

#include "systems/movie_writer/movie_writer.h"

class MovieWriterPNGWAV : public MovieWriter {
	ORBLIT_CLASS(MovieWriterPNGWAV, MovieWriter)

	enum {
		MAX_TRAILING_ZEROS = 8 // more than 10 days at 60fps, no hard drive can put up with this anyway :)
	};

	uint32_t mix_rate = 48000;
	AuSE::SpeakerMode speaker_mode = AuSE::SPEAKER_MODE_STEREO;
	String base_path;
	uint32_t frame_count = 0;
	uint32_t fps = 0;

	uint32_t audio_block_size = 0;

	Ref<FileAccess> f_wav;
	uint32_t wav_data_size_pos = 0;

	String zeros_str(uint32_t p_index);

protected:
	virtual uint32_t get_audio_mix_rate() const override;
	virtual AuSE::SpeakerMode get_audio_speaker_mode() const override;
	virtual void get_supported_extensions(List<String> *r_extensions) const override;

	virtual Error write_begin(const Size2i &p_movie_size, uint32_t p_fps, const String &p_base_path) override;
	virtual Error write_frame(const Ref<Image> &p_image, const int32_t *p_audio_data) override;
	virtual void write_end() override;

	virtual bool handles_file(const String &p_path) const override;

public:
	MovieWriterPNGWAV();
};
