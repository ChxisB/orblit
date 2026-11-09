/**************************************************************************/
/*  image_compress_cvtt.h                                                 */

#pragma once

#include "core/io/image.h"

void image_compress_cvtt(Image *p_image, Image::UsedChannels p_channels, Image::BPTCFormat p_bptc_format);
void image_decompress_cvtt(Image *p_image);
