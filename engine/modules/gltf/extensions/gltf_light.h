/**************************************************************************/
/*  gltf_light.h                                                          */

#pragma once

#include "core/io/resource.h"

class GLTFObjectModelProperty;
class Light3D;

// https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_lights_punctual

class GLTFLight : public Resource {
	ORBLIT_CLASS(GLTFLight, Resource)
	friend class GLTFDocument;

protected:
	static void register_members();

private:
	Color color = Color(1.0f, 1.0f, 1.0f);
	float intensity = 1.0f;
	String light_type;
	float range = Math::INF;
	float inner_cone_angle = 0.0f;
	float outer_cone_angle = Math::TAU / 8.0f;
	Dictionary additional_data;

public:
	static void set_cone_inner_attenuation_conversion_expressions(Ref<GLTFObjectModelProperty> &r_obj_model_prop);

	Color get_color();
	void set_color(Color p_color);

	float get_intensity();
	void set_intensity(float p_intensity);

	String get_light_type();
	void set_light_type(String p_light_type);

	float get_range();
	void set_range(float p_range);

	float get_inner_cone_angle();
	void set_inner_cone_angle(float p_inner_cone_angle);

	float get_outer_cone_angle();
	void set_outer_cone_angle(float p_outer_cone_angle);

	static Ref<GLTFLight> from_node(const Light3D *p_light);
	Light3D *to_node() const;

	static Ref<GLTFLight> from_dictionary(const Dictionary &p_dictionary);
	Dictionary to_dictionary() const;

	Variant get_additional_data(const StringName &p_extension_name);
	void set_additional_data(const StringName &p_extension_name, Variant p_additional_data);
};
