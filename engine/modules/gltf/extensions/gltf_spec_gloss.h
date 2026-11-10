/**************************************************************************/
/*  gltf_spec_gloss.h                                                     */

#pragma once

#include "core/io/resource.h"

class Image;

// KHR_materials_pbrSpecularGlossiness is an archived GLTF extension.
// This means that it is deprecated and not recommended for new files.
// However, it is still supported for loading old files.
// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Archived/KHR_materials_pbrSpecularGlossiness

class GLTFSpecGloss : public Resource {
	ORBLIT_CLASS(GLTFSpecGloss, Resource);
	friend class GLTFDocument;

private:
	Ref<Image> diffuse_img = nullptr;
	Color diffuse_factor = Color(1.0f, 1.0f, 1.0f);
	float gloss_factor = 1.0f;
	Color specular_factor = Color(1.0f, 1.0f, 1.0f);
	Ref<Image> spec_gloss_img = nullptr;

protected:
	static void register_members();

public:
	Ref<Image> get_diffuse_img();
	void set_diffuse_img(Ref<Image> p_diffuse_img);

	Color get_diffuse_factor();
	void set_diffuse_factor(Color p_diffuse_factor);

	float get_gloss_factor();
	void set_gloss_factor(float p_gloss_factor);

	Color get_specular_factor();
	void set_specular_factor(Color p_specular_factor);

	Ref<Image> get_spec_gloss_img();
	void set_spec_gloss_img(Ref<Image> p_spec_gloss_img);
};
