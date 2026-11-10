/**************************************************************************/
/*  image_saver_tinyexr.h                                                 */

#pragma once

#include "core/io/image.h"

Error save_exr(const String &p_path, const Ref<Image> &p_img, bool p_grayscale, bool p_color_image, float p_max_value);
Vector<uint8_t> save_exr_buffer(const Ref<Image> &p_img, bool p_grayscale, bool p_color_image, float p_max_value);
