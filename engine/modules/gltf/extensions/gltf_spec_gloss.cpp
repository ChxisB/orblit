/**************************************************************************/
/*  gltf_spec_gloss.cpp                                                   */

#include "gltf_spec_gloss.h"

#include "core/io/image.h"
#include "core/object/class_registry.h"

void GLTFSpecGloss::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_diffuse_img"), &GLTFSpecGloss::get_diffuse_img);
	ClassRegistry::bind_method(D_METHOD("set_diffuse_img", "diffuse_img"), &GLTFSpecGloss::set_diffuse_img);
	ClassRegistry::bind_method(D_METHOD("get_diffuse_factor"), &GLTFSpecGloss::get_diffuse_factor);
	ClassRegistry::bind_method(D_METHOD("set_diffuse_factor", "diffuse_factor"), &GLTFSpecGloss::set_diffuse_factor);
	ClassRegistry::bind_method(D_METHOD("get_gloss_factor"), &GLTFSpecGloss::get_gloss_factor);
	ClassRegistry::bind_method(D_METHOD("set_gloss_factor", "gloss_factor"), &GLTFSpecGloss::set_gloss_factor);
	ClassRegistry::bind_method(D_METHOD("get_specular_factor"), &GLTFSpecGloss::get_specular_factor);
	ClassRegistry::bind_method(D_METHOD("set_specular_factor", "specular_factor"), &GLTFSpecGloss::set_specular_factor);
	ClassRegistry::bind_method(D_METHOD("get_spec_gloss_img"), &GLTFSpecGloss::get_spec_gloss_img);
	ClassRegistry::bind_method(D_METHOD("set_spec_gloss_img", "spec_gloss_img"), &GLTFSpecGloss::set_spec_gloss_img);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "diffuse_img"), "set_diffuse_img", "get_diffuse_img"); // Ref<Image>
	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "diffuse_factor"), "set_diffuse_factor", "get_diffuse_factor"); // Color
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "gloss_factor"), "set_gloss_factor", "get_gloss_factor"); // float
	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "specular_factor"), "set_specular_factor", "get_specular_factor"); // Color
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "spec_gloss_img"), "set_spec_gloss_img", "get_spec_gloss_img"); // Ref<Image>
}

Ref<Image> GLTFSpecGloss::get_diffuse_img() {
	return diffuse_img;
}

void GLTFSpecGloss::set_diffuse_img(Ref<Image> p_diffuse_img) {
	diffuse_img = p_diffuse_img;
}

Color GLTFSpecGloss::get_diffuse_factor() {
	return diffuse_factor;
}

void GLTFSpecGloss::set_diffuse_factor(Color p_diffuse_factor) {
	diffuse_factor = p_diffuse_factor;
}

float GLTFSpecGloss::get_gloss_factor() {
	return gloss_factor;
}

void GLTFSpecGloss::set_gloss_factor(float p_gloss_factor) {
	gloss_factor = p_gloss_factor;
}

Color GLTFSpecGloss::get_specular_factor() {
	return specular_factor;
}

void GLTFSpecGloss::set_specular_factor(Color p_specular_factor) {
	specular_factor = p_specular_factor;
}

Ref<Image> GLTFSpecGloss::get_spec_gloss_img() {
	return spec_gloss_img;
}

void GLTFSpecGloss::set_spec_gloss_img(Ref<Image> p_spec_gloss_img) {
	spec_gloss_img = p_spec_gloss_img;
}
