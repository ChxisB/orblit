/**************************************************************************/
/*  resource_saver_png.h                                                  */

#pragma once

#include "core/io/image.h"
#include "core/io/resource_saver.h"

class ResourceSaverPNG : public ResourceFormatSaver {
	ORBLIT_SOFTCLASS(ResourceSaverPNG, ResourceFormatSaver);

public:
	static Error save_image(const String &p_path, const Ref<Image> &p_img);
	static Vector<uint8_t> save_image_to_buffer(const Ref<Image> &p_img, bool p_fast = false);

	virtual Error save(const Ref<Resource> &p_resource, const String &p_path, uint32_t p_flags = 0) override;
	virtual bool recognize(const Ref<Resource> &p_resource) const override;
	virtual void get_recognized_extensions(const Ref<Resource> &p_resource, List<String> *p_extensions) const override;

	ResourceSaverPNG();
};
