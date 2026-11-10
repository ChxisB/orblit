/**************************************************************************/
/*  image_loader_libjpeg_turbo.h                                          */

#pragma once

#include "core/io/image_loader.h"

class ImageLoaderLibJPEGTurbo : public ImageFormatLoader {
public:
	virtual Error load_image(Ref<Image> p_image, Ref<FileAccess> f, BitField<ImageFormatLoader::LoaderFlags> p_flags, float p_scale);
	virtual void get_recognized_extensions(List<String> *p_extensions) const;
	ImageLoaderLibJPEGTurbo();
};
