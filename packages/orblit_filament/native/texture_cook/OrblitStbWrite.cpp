// stb_image_write's implementation, for the check alone: it writes the PNG and
// JPEG fixtures the check draws, and nothing the cooker ships uses it.
//
// Its own file so the sanitised build can leave one check out here and
// nowhere else. The JPEG writer shifts a bit buffer left past its sign bit
// (stbiw__jpg_writeBits), which UBSan reports as a left shift of a negative
// value — undefined in C++17, defined as the obvious thing in C++20, and what
// every compiler does anyway. It only ever runs on images the check drew
// itself, never on bytes from a file, so it is not what the fuzzing is
// looking for; build.sh compiles this file with -fno-sanitize=shift.
// stb_image, which does read untrusted bytes, stays fully sanitised in
// OrblitStb.cpp.

#define STB_IMAGE_WRITE_IMPLEMENTATION
#define STBI_WRITE_NO_STDIO
#include "stb_image_write.h"
