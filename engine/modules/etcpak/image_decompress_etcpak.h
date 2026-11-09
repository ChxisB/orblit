/**************************************************************************/
/*  image_decompress_etcpak.h                                             */

#pragma once

#include "core/io/image.h"

enum EtcpakFormat {
	Etcpak_R,
	Etcpak_RG,
	Etcpak_RGB,
	Etcpak_RGBA,
};

void _decompress_etc(Image *p_image);
