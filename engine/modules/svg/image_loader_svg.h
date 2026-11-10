/**************************************************************************/
/*  image_loader_svg.h                                                    */

#pragma once

#include "core/io/image_loader.h"

class ImageLoaderSVG : public ImageFormatLoader {
	static HashMap<Color, Color> forced_color_map;

	static void _replace_color_property(const HashMap<Color, Color> &p_color_map, const String &p_prefix, String &r_string);

	static Ref<Image> load_mem_svg(const uint8_t *p_svg, int p_size, float p_scale);

public:
	static void set_forced_color_map(const HashMap<Color, Color> &p_color_map);

	static Error create_image_from_utf8_buffer(Ref<Image> p_image, const uint8_t *p_buffer, int p_buffer_size, float p_scale, bool p_upsample);
	static Error create_image_from_utf8_buffer(Ref<Image> p_image, const PackedByteArray &p_buffer, float p_scale, bool p_upsample);

	static Error create_image_from_string(Ref<Image> p_image, String p_string, float p_scale, bool p_upsample, const HashMap<Color, Color> &p_color_map);

	virtual Error load_image(Ref<Image> p_image, Ref<FileAccess> p_fileaccess, BitField<ImageFormatLoader::LoaderFlags> p_flags, float p_scale) override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;

	ImageLoaderSVG();
};
