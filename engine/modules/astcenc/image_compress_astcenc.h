/**************************************************************************/
/*  image_compress_astcenc.h                                              */

#pragma once

#include "core/io/image.h"

#ifdef TOOLS_ENABLED
void _compress_astc(Image *r_img, Image::UsedChannels p_channels, Image::CompressProfile p_profile);
#endif

void _decompress_astc(Image *r_img);
