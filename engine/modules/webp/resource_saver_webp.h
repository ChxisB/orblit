/**************************************************************************/
/*  resource_saver_webp.h                                                 */

#pragma once

#include "core/io/image.h"
#include "core/io/resource_saver.h"

class ResourceSaverWebP : public ResourceFormatSaver {
	ORBLIT_SOFTCLASS(ResourceSaverWebP, ResourceFormatSaver);

public:
	static Error save_image(const String &p_path, const Ref<Image> &p_img, const bool p_lossy = false, const float p_quality = 0.75f);
	static Vector<uint8_t> save_image_to_buffer(const Ref<Image> &p_img, const bool p_lossy = false, const float p_quality = 0.75f);

	virtual Error save(const Ref<Resource> &p_resource, const String &p_path, uint32_t p_flags = 0) override;
	virtual bool recognize(const Ref<Resource> &p_resource) const override;
	virtual void get_recognized_extensions(const Ref<Resource> &p_resource, List<String> *p_extensions) const override;

	ResourceSaverWebP();
};
