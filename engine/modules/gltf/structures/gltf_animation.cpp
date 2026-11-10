/**************************************************************************/
/*  gltf_animation.cpp                                                    */

#include "gltf_animation.h"

#include "core/object/class_registry.h"

void GLTFAnimation::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_original_name"), &GLTFAnimation::get_original_name);
	ClassRegistry::bind_method(D_METHOD("set_original_name", "original_name"), &GLTFAnimation::set_original_name);
	ClassRegistry::bind_method(D_METHOD("get_loop"), &GLTFAnimation::get_loop);
	ClassRegistry::bind_method(D_METHOD("set_loop", "loop"), &GLTFAnimation::set_loop);
	ClassRegistry::bind_method(D_METHOD("get_additional_data", "extension_name"), &GLTFAnimation::get_additional_data);
	ClassRegistry::bind_method(D_METHOD("set_additional_data", "extension_name", "additional_data"), &GLTFAnimation::set_additional_data);

	ADD_PROPERTY(PropertyInfo(Variant::STRING, "original_name"), "set_original_name", "get_original_name"); // String
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "loop"), "set_loop", "get_loop"); // bool
}

GLTFAnimation::Interpolation GLTFAnimation::orblit_to_gltf_interpolation(const Ref<Animation> &p_orblit_animation, int32_t p_orblit_anim_track_index) {
	Animation::InterpolationType interpolation = p_orblit_animation->track_get_interpolation_type(p_orblit_anim_track_index);
	switch (interpolation) {
		case Animation::INTERPOLATION_LINEAR:
		case Animation::INTERPOLATION_LINEAR_ANGLE:
			return INTERP_LINEAR;
		case Animation::INTERPOLATION_NEAREST:
			return INTERP_STEP;
		case Animation::INTERPOLATION_CUBIC:
		case Animation::INTERPOLATION_CUBIC_ANGLE:
		case Animation::INTERPOLATION_MAKIMA:
		case Animation::INTERPOLATION_MAKIMA_ANGLE:
			return INTERP_CUBIC_SPLINE;
	}
	return INTERP_LINEAR;
}

Animation::InterpolationType GLTFAnimation::gltf_to_orblit_interpolation(Interpolation p_gltf_interpolation) {
	switch (p_gltf_interpolation) {
		case INTERP_LINEAR:
			return Animation::INTERPOLATION_LINEAR;
		case INTERP_STEP:
			return Animation::INTERPOLATION_NEAREST;
		case INTERP_CATMULLROMSPLINE:
		case INTERP_CUBIC_SPLINE:
			return Animation::INTERPOLATION_CUBIC;
	}
	return Animation::INTERPOLATION_LINEAR;
}

String GLTFAnimation::get_original_name() {
	return original_name;
}

void GLTFAnimation::set_original_name(const String &p_name) {
	original_name = p_name;
}

bool GLTFAnimation::get_loop() const {
	return loop;
}

void GLTFAnimation::set_loop(bool p_val) {
	loop = p_val;
}

HashMap<int, GLTFAnimation::NodeTrack> &GLTFAnimation::get_node_tracks() {
	return node_tracks;
}

HashMap<String, GLTFAnimation::Channel<Variant>> &GLTFAnimation::get_pointer_tracks() {
	return pointer_tracks;
}

bool GLTFAnimation::is_empty_of_tracks() const {
	return node_tracks.is_empty() && pointer_tracks.is_empty();
}

GLTFAnimation::GLTFAnimation() {
}

Variant GLTFAnimation::get_additional_data(const StringName &p_extension_name) {
	return additional_data.get(p_extension_name, Variant());
}

void GLTFAnimation::set_additional_data(const StringName &p_extension_name, Variant p_additional_data) {
	additional_data[p_extension_name] = p_additional_data;
}
