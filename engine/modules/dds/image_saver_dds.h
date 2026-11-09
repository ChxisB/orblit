/**************************************************************************/
/*  image_saver_dds.h                                                     */

#pragma once

#include "core/io/image.h"

Error save_dds(const String &p_path, const Ref<Image> &p_img);
Vector<uint8_t> save_dds_buffer(const Ref<Image> &p_img);
