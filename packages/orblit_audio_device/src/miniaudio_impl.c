// The one file that compiles the two single-header libraries, so the macros
// stb_vorbis defines cannot reach the wrapper. miniaudio finds a Vorbis
// decoder by seeing its declarations first, and its definitions after its own.
#define STB_VORBIS_HEADER_ONLY
#include "stb_vorbis.c"

#define MINIAUDIO_IMPLEMENTATION
#include "miniaudio.h"

#undef STB_VORBIS_HEADER_ONLY
#include "stb_vorbis.c"
