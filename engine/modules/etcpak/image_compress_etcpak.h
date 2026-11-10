/**************************************************************************/
/*  image_compress_etcpak.h                                               */

#pragma once

#ifdef ETCPAK_COMPRESS_ENABLED

#include "core/io/image.h"

enum class EtcpakType {
	ETCPAK_TYPE_ETC1,
	ETCPAK_TYPE_ETC2,
	ETCPAK_TYPE_ETC2_ALPHA,
	ETCPAK_TYPE_ETC2_RA_AS_RG,
	ETCPAK_TYPE_ETC2_R,
	ETCPAK_TYPE_ETC2_RG,
	ETCPAK_TYPE_DXT1,
	ETCPAK_TYPE_DXT5,
	ETCPAK_TYPE_DXT5_RA_AS_RG,
	ETCPAK_TYPE_RGTC_R,
	ETCPAK_TYPE_RGTC_RG,
};

void _compress_etc1(Image *r_img);
void _compress_etc2(Image *r_img, Image::UsedChannels p_channels);
void _compress_bc(Image *r_img, Image::UsedChannels p_channels);

void _compress_etcpak(EtcpakType p_compress_type, Image *r_img);

#endif // ETCPAK_COMPRESS_ENABLED
