/**************************************************************************/
/*  gltf_light.cpp                                                        */

#include "gltf_light.h"

#include "../structures/gltf_object_model_property.h"

#include "core/object/class_registry.h"
#include "scene/spatial/light_spatial.h"

#include <cfloat> // FLT_MAX

void GLTFLight::register_members() {
	ClassRegistry::bind_static_method("GLTFLight", D_METHOD("from_node", "light_node"), &GLTFLight::from_node);
	ClassRegistry::bind_method(D_METHOD("to_node"), &GLTFLight::to_node);

	ClassRegistry::bind_static_method("GLTFLight", D_METHOD("from_dictionary", "dictionary"), &GLTFLight::from_dictionary);
	ClassRegistry::bind_method(D_METHOD("to_dictionary"), &GLTFLight::to_dictionary);

	ClassRegistry::bind_method(D_METHOD("get_color"), &GLTFLight::get_color);
	ClassRegistry::bind_method(D_METHOD("set_color", "color"), &GLTFLight::set_color);
	ClassRegistry::bind_method(D_METHOD("get_intensity"), &GLTFLight::get_intensity);
	ClassRegistry::bind_method(D_METHOD("set_intensity", "intensity"), &GLTFLight::set_intensity);
	ClassRegistry::bind_method(D_METHOD("get_light_type"), &GLTFLight::get_light_type);
	ClassRegistry::bind_method(D_METHOD("set_light_type", "light_type"), &GLTFLight::set_light_type);
	ClassRegistry::bind_method(D_METHOD("get_range"), &GLTFLight::get_range);
	ClassRegistry::bind_method(D_METHOD("set_range", "range"), &GLTFLight::set_range);
	ClassRegistry::bind_method(D_METHOD("get_inner_cone_angle"), &GLTFLight::get_inner_cone_angle);
	ClassRegistry::bind_method(D_METHOD("set_inner_cone_angle", "inner_cone_angle"), &GLTFLight::set_inner_cone_angle);
	ClassRegistry::bind_method(D_METHOD("get_outer_cone_angle"), &GLTFLight::get_outer_cone_angle);
	ClassRegistry::bind_method(D_METHOD("set_outer_cone_angle", "outer_cone_angle"), &GLTFLight::set_outer_cone_angle);
	ClassRegistry::bind_method(D_METHOD("get_additional_data", "extension_name"), &GLTFLight::get_additional_data);
	ClassRegistry::bind_method(D_METHOD("set_additional_data", "extension_name", "additional_data"), &GLTFLight::set_additional_data);

	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "color"), "set_color", "get_color"); // Color
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "intensity"), "set_intensity", "get_intensity"); // float
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "light_type"), "set_light_type", "get_light_type"); // String
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "range"), "set_range", "get_range"); // float
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "inner_cone_angle"), "set_inner_cone_angle", "get_inner_cone_angle"); // float
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "outer_cone_angle"), "set_outer_cone_angle", "get_outer_cone_angle"); // float
}

// A ratio of 1 makes the attenuation infinite on import and rounds the two angles equal on export.
// The nearest ratio below 1 gives a hard edged spot and keeps the angles apart as glTF requires.
static constexpr float MAX_CONE_ANGLE_RATIO = 0.99999f;

void GLTFLight::set_cone_inner_attenuation_conversion_expressions(Ref<GLTFObjectModelProperty> &r_obj_model_prop) {
	// Expression to convert glTF innerConeAngle to Orblit spot_angle_attenuation.
	Ref<Expression> gltf_to_orblit_expr;
	gltf_to_orblit_expr.instantiate();
	PackedStringArray gltf_to_orblit_args = { "inner_cone_angle" };
	gltf_to_orblit_expr->parse("0.2 / (1.0 - inner_cone_angle / spot_angle) - 0.1", gltf_to_orblit_args);
	r_obj_model_prop->set_gltf_to_orblit_expression(gltf_to_orblit_expr);
	// Expression to convert Orblit spot_angle_attenuation to glTF innerConeAngle.
	Ref<Expression> orblit_to_gltf_expr;
	orblit_to_gltf_expr.instantiate();
	PackedStringArray orblit_to_gltf_args = { "orblit_spot_angle_att" };
	orblit_to_gltf_expr->parse("spot_angle * maxf(0.0, 1.0 - (0.2 / (0.1 + orblit_spot_angle_att)))", orblit_to_gltf_args);
	r_obj_model_prop->set_orblit_to_gltf_expression(orblit_to_gltf_expr);
}

Color GLTFLight::get_color() {
	return color;
}

void GLTFLight::set_color(Color p_color) {
	color = p_color;
}

float GLTFLight::get_intensity() {
	return intensity;
}

void GLTFLight::set_intensity(float p_intensity) {
	intensity = p_intensity;
}

String GLTFLight::get_light_type() {
	return light_type;
}

void GLTFLight::set_light_type(String p_light_type) {
	light_type = p_light_type;
}

float GLTFLight::get_range() {
	return range;
}

void GLTFLight::set_range(float p_range) {
	range = p_range;
}

float GLTFLight::get_inner_cone_angle() {
	return inner_cone_angle;
}

void GLTFLight::set_inner_cone_angle(float p_inner_cone_angle) {
	inner_cone_angle = p_inner_cone_angle;
}

float GLTFLight::get_outer_cone_angle() {
	return outer_cone_angle;
}

void GLTFLight::set_outer_cone_angle(float p_outer_cone_angle) {
	outer_cone_angle = p_outer_cone_angle;
}

Ref<GLTFLight> GLTFLight::from_node(const Light3D *p_light) {
	Ref<GLTFLight> l;
	l.instantiate();
	ERR_FAIL_NULL_V_MSG(p_light, l, "Tried to create a GLTFLight from a Light3D node, but the given node was null.");
	l->color = p_light->get_color().srgb_to_linear();
	if (cast_to<DirectionalLight3D>(p_light)) {
		l->light_type = "directional";
		const DirectionalLight3D *light = cast_to<const DirectionalLight3D>(p_light);
		l->intensity = light->get_param(DirectionalLight3D::PARAM_ENERGY);
		l->range = FLT_MAX; // Range for directional lights is infinite in Orblit.
	} else if (cast_to<const OmniLight3D>(p_light)) {
		l->light_type = "point";
		const OmniLight3D *light = cast_to<const OmniLight3D>(p_light);
		l->range = light->get_param(OmniLight3D::PARAM_RANGE);
		l->intensity = light->get_param(OmniLight3D::PARAM_ENERGY);
	} else if (cast_to<const SpotLight3D>(p_light)) {
		l->light_type = "spot";
		const SpotLight3D *light = cast_to<const SpotLight3D>(p_light);
		l->range = light->get_param(SpotLight3D::PARAM_RANGE);
		l->intensity = light->get_param(SpotLight3D::PARAM_ENERGY);
		l->outer_cone_angle = Math::deg_to_rad(light->get_param(SpotLight3D::PARAM_SPOT_ANGLE));
		// This equation is the inverse of the import equation (which has a desmos link).
		float angle_ratio = 1 - (0.2 / (0.1 + light->get_param(SpotLight3D::PARAM_SPOT_ATTENUATION)));
		angle_ratio = CLAMP(angle_ratio, 0.0f, MAX_CONE_ANGLE_RATIO);
		l->inner_cone_angle = l->outer_cone_angle * angle_ratio;
	}
	return l;
}

Light3D *GLTFLight::to_node() const {
	Light3D *light = nullptr;
	if (light_type == "directional") {
		DirectionalLight3D *dir_light = memnew(DirectionalLight3D);
		dir_light->set_param(Light3D::PARAM_ENERGY, intensity);
		light = dir_light;
	} else if (light_type == "point") {
		OmniLight3D *omni_light = memnew(OmniLight3D);
		omni_light->set_param(OmniLight3D::PARAM_ENERGY, intensity);
		omni_light->set_param(OmniLight3D::PARAM_RANGE, CLAMP(range, 0, 4096));
		light = omni_light;
	} else if (light_type == "spot") {
		SpotLight3D *spot_light = memnew(SpotLight3D);
		spot_light->set_param(SpotLight3D::PARAM_ENERGY, intensity);
		spot_light->set_param(SpotLight3D::PARAM_RANGE, CLAMP(range, 0, 4096));
		spot_light->set_param(SpotLight3D::PARAM_SPOT_ANGLE, Math::rad_to_deg(outer_cone_angle));
		// Line of best fit derived from guessing, see https://www.desmos.com/calculator/biiflubp8b
		// The points in desmos are not exact, except for (1, infinity).
		float angle_ratio = outer_cone_angle > 0.0f ? MIN(inner_cone_angle / outer_cone_angle, MAX_CONE_ANGLE_RATIO) : 0.0f;
		float angle_attenuation = 0.2 / (1 - angle_ratio) - 0.1;
		spot_light->set_param(SpotLight3D::PARAM_SPOT_ATTENUATION, angle_attenuation);
		light = spot_light;
	} else {
		ERR_PRINT("Failed to create a Light3D node from GLTFLight, unknown light type '" + light_type + "'.");
		return nullptr;
	}
	light->set_color(color.linear_to_srgb());
	light->set_param(Light3D::PARAM_ATTENUATION, 2.0);
	return light;
}

Ref<GLTFLight> GLTFLight::from_dictionary(const Dictionary &p_dictionary) {
	ERR_FAIL_COND_V_MSG(!p_dictionary.has("type"), Ref<GLTFLight>(), "Failed to parse glTF light, missing required field 'type'.");
	Ref<GLTFLight> light;
	light.instantiate();
	const String &type = p_dictionary["type"];
	light->light_type = type;

	if (p_dictionary.has("color")) {
		const Array &arr = p_dictionary["color"];
		if (arr.size() == 3) {
			light->color = Color(arr[0], arr[1], arr[2]);
		} else {
			ERR_PRINT("Error parsing glTF light: The color must have exactly 3 numbers.");
		}
	}
	if (p_dictionary.has("intensity")) {
		light->intensity = p_dictionary["intensity"];
	}
	if (p_dictionary.has("range")) {
		light->range = p_dictionary["range"];
	}
	if (type == "spot") {
		const Dictionary &spot = p_dictionary["spot"];
		light->inner_cone_angle = spot["innerConeAngle"];
		light->outer_cone_angle = spot["outerConeAngle"];
		if (light->inner_cone_angle >= light->outer_cone_angle) {
			ERR_PRINT("Error parsing glTF light: The inner angle must be smaller than the outer angle.");
		}
	} else if (type != "point" && type != "directional") {
		ERR_PRINT("Error parsing glTF light: Light type '" + type + "' is unknown.");
	}
	return light;
}

Dictionary GLTFLight::to_dictionary() const {
	Dictionary d;
	if (color != Color(1.0f, 1.0f, 1.0f)) {
		Array color_array;
		color_array.resize(3);
		color_array[0] = color.r;
		color_array[1] = color.g;
		color_array[2] = color.b;
		d["color"] = color_array;
	}
	if (intensity != 1.0f) {
		d["intensity"] = intensity;
	}
	if (light_type != "directional" && range != Math::INF) {
		d["range"] = range;
	}
	if (light_type == "spot") {
		Dictionary spot_dict;
		spot_dict["innerConeAngle"] = inner_cone_angle;
		spot_dict["outerConeAngle"] = outer_cone_angle;
		d["spot"] = spot_dict;
	}
	d["type"] = light_type;
	return d;
}

Variant GLTFLight::get_additional_data(const StringName &p_extension_name) {
	return additional_data.get(p_extension_name, Variant());
}

void GLTFLight::set_additional_data(const StringName &p_extension_name, Variant p_additional_data) {
	additional_data[p_extension_name] = p_additional_data;
}
