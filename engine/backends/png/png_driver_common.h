/**************************************************************************/
/*  png_driver_common.h                                                   */

#pragma once

#include "core/io/image.h"

namespace PNGDriverCommon {

// Attempt to load png from buffer (p_source, p_size) into p_image
Error png_to_image(const uint8_t *p_source, size_t p_size, bool p_force_linear, Ref<Image> p_image);

// Append p_image, as a png, to p_buffer.
// Contents of p_buffer is unspecified if error returned.
// p_fast is only used internally by the PNGWAV MovieWriter to allow for faster saving, by prioritizing speed over compression.
Error image_to_png(const Ref<Image> &p_image, Vector<uint8_t> &p_buffer, bool p_fast = false);
} // namespace PNGDriverCommon
