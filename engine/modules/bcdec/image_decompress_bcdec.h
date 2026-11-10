/**************************************************************************/
/*  image_decompress_bcdec.h                                              */

#pragma once

#include "core/io/image.h"

enum BCdecFormat {
	BCdec_BC1,
	BCdec_BC2,
	BCdec_BC3,
	BCdec_BC4,
	BCdec_BC5,
	BCdec_BC6S,
	BCdec_BC6U,
	BCdec_BC7,
};

void image_decompress_bcdec(Image *p_image);
