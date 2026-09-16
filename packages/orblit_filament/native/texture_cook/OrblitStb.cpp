// stb_image's implementation, compiled once for the cooker and its check.
//
// PNG and JPEG only: every other format stb_image knows is code that reads
// untrusted bytes and that nothing here needs. Memory only, no stdio: the
// cooker reads files itself, so a path never reaches a decoder.
//
// STBI_MAX_DIMENSIONS is stb's own refusal of a header claiming more than
// that on a side, checked before it allocates; OrblitTextureCook.cpp asks
// the same question first and more strictly, so this is the second lock on
// the same door.

#define STB_IMAGE_IMPLEMENTATION
#define STBI_ONLY_PNG
#define STBI_ONLY_JPEG
#define STBI_NO_STDIO
#define STBI_MAX_DIMENSIONS 16384
#include "stb_image.h"
