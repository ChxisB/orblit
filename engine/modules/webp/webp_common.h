/**************************************************************************/
/*  webp_common.h                                                         */

#pragma once

#include "core/io/image.h"

namespace WebPCommon {
// Given an image, pack this data into a WebP file.
Vector<uint8_t> _webp_lossy_pack(const Ref<Image> &p_image, float p_quality);
Vector<uint8_t> _webp_lossless_pack(const Ref<Image> &p_image);
// Helper function for those above.
Vector<uint8_t> _webp_packer(const Ref<Image> &p_image, float p_quality, bool p_lossless);
// Given a WebP file, unpack it into an image.
Ref<Image> _webp_unpack(const Vector<uint8_t> &p_buffer);
Error webp_load_image_from_buffer(Image *p_image, const uint8_t *p_buffer, int p_buffer_len);
} //namespace WebPCommon
