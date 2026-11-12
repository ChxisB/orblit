/**************************************************************************/
/*  test_audio_stream_interactive.h                                       */

#pragma once

#include "../audio_stream_interactive.h"

#include "scene/resources/audio/audio_stream_wav.h"
#include "tests/test_macros.h"

namespace TestAudioStreamInteractive {

static Ref<AudioStreamWAV> make_clip(bool p_loop) {
	const int rate = 44100;
	Vector<uint8_t> data;
	data.resize(rate / 20);
	data.fill(128);

	Ref<AudioStreamWAV> clip;
	clip.instantiate();
	clip->set_format(AudioStreamWAV::FORMAT_8_BITS);
	clip->set_mix_rate(rate);
	clip->set_data(data);
	if (p_loop) {
		clip->set_loop_mode(AudioStreamWAV::LOOP_FORWARD);
		clip->set_loop_end(data.size() - 1);
	}
	return clip;
}

TEST_CASE("[Audio][AudioStreamInteractive] A clip reached through a filler still auto advances") {
	enum {
		MAIN,
		CHORUS,
		FILLER
	};

	Ref<AudioStreamInteractive> stream;
	stream.instantiate();
	stream->set_clip_count(3);
	stream->set_clip_stream(MAIN, make_clip(true));
	stream->set_clip_stream(CHORUS, make_clip(false));
	stream->set_clip_stream(FILLER, make_clip(false));
	stream->set_clip_auto_advance(CHORUS, AudioStreamInteractive::AUTO_ADVANCE_ENABLED);
	stream->set_clip_auto_advance_next_clip(CHORUS, MAIN);
	stream->add_transition(MAIN, CHORUS, AudioStreamInteractive::TRANSITION_FROM_TIME_IMMEDIATE, AudioStreamInteractive::TRANSITION_TO_TIME_START, AudioStreamInteractive::FADE_DISABLED, 1.0, true, FILLER);

	Ref<AudioStreamPlayback> base_playback = stream->instantiate_playback();
	Ref<AudioStreamPlaybackInteractive> playback = base_playback;
	REQUIRE(playback.is_valid());
	playback->start();
	playback->switch_to_clip(CHORUS);

	AudioFrame frames[512];
	for (int i = 0; i < 40; i++) {
		playback->mix(frames, 1.0, 512);
	}

	CHECK(playback->get_current_clip_index() == MAIN);
}

} // namespace TestAudioStreamInteractive
