/**************************************************************************/
/*  audio_system_constants.h                                              */

#pragma once

namespace AudioSystemConstants {

constexpr int AUDIO_DATA_INVALID_ID = -1;
constexpr int MAX_CHANNELS_PER_BUS = 4;
constexpr int MAX_BUSES_PER_PLAYBACK = 6;
constexpr int LOOKAHEAD_BUFFER_SIZE = 64;

constexpr const float AUDIO_PEAK_OFFSET = 0.0000000001f;
constexpr const float AUDIO_MIN_PEAK_DB = -200.0f; // linear_to_db(AUDIO_PEAK_OFFSET)

} // namespace AudioSystemConstants

// Alias to make it easier to use.
#define AuSC AudioSystemConstants
