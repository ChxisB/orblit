/**************************************************************************/
/*  audio_system_enums.h                                                  */

#pragma once

namespace AudioSystemEnums {

//re-expose this here, as AudioDriver is not exposed to script
enum SpeakerMode {
	SPEAKER_MODE_STEREO,
	SPEAKER_SURROUND_31,
	SPEAKER_SURROUND_51,
	SPEAKER_SURROUND_71,
};

enum PlaybackType {
	PLAYBACK_TYPE_DEFAULT,
	PLAYBACK_TYPE_STREAM,
	PLAYBACK_TYPE_SAMPLE,
	PLAYBACK_TYPE_MAX
};
} // namespace AudioSystemEnums

// Alias to make it easier to use.
#define AuSE AudioSystemEnums
