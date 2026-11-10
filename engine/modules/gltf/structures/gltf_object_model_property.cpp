/**************************************************************************/
/*  gltf_object_model_property.cpp                                        */

#include "gltf_object_model_property.h"

#include "../gltf_template_convert.h"

#include "core/object/class_registry.h"

void GLTFObjectModelProperty::register_members() {
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_UNKNOWN);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_BOOL);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT_ARRAY);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT2);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT3);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT4);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT2X2);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT3X3);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_FLOAT4X4);
	BIND_ENUM_CONSTANT(GLTF_OBJECT_MODEL_TYPE_INT);

	ClassRegistry::bind_method(D_METHOD("append_node_path", "node_path"), &GLTFObjectModelProperty::append_node_path);
	ClassRegistry::bind_method(D_METHOD("append_path_to_property", "node_path", "prop_name"), &GLTFObjectModelProperty::append_path_to_property);

	ClassRegistry::bind_method(D_METHOD("get_accessor_type"), &GLTFObjectModelProperty::get_accessor_type);
	ClassRegistry::bind_method(D_METHOD("get_gltf_to_orblit_expression"), &GLTFObjectModelProperty::get_gltf_to_orblit_expression);
	ClassRegistry::bind_method(D_METHOD("set_gltf_to_orblit_expression", "gltf_to_orblit_expr"), &GLTFObjectModelProperty::set_gltf_to_orblit_expression);
	ClassRegistry::bind_method(D_METHOD("get_orblit_to_gltf_expression"), &GLTFObjectModelProperty::get_orblit_to_gltf_expression);
	ClassRegistry::bind_method(D_METHOD("set_orblit_to_gltf_expression", "orblit_to_gltf_expr"), &GLTFObjectModelProperty::set_orblit_to_gltf_expression);
	ClassRegistry::bind_method(D_METHOD("get_node_paths"), &GLTFObjectModelProperty::get_node_paths);
	ClassRegistry::bind_method(D_METHOD("has_node_paths"), &GLTFObjectModelProperty::has_node_paths);
	ClassRegistry::bind_method(D_METHOD("set_node_paths", "node_paths"), &GLTFObjectModelProperty::set_node_paths);
	ClassRegistry::bind_method(D_METHOD("get_object_model_type"), &GLTFObjectModelProperty::get_object_model_type);
	ClassRegistry::bind_method(D_METHOD("set_object_model_type", "type"), &GLTFObjectModelProperty::set_object_model_type);
	ClassRegistry::bind_method(D_METHOD("get_json_pointers"), &GLTFObjectModelProperty::get_json_pointers_bind);
	ClassRegistry::bind_method(D_METHOD("has_json_pointers"), &GLTFObjectModelProperty::has_json_pointers);
	ClassRegistry::bind_method(D_METHOD("set_json_pointers", "json_pointers"), &GLTFObjectModelProperty::set_json_pointers_bind);
	ClassRegistry::bind_method(D_METHOD("get_variant_type"), &GLTFObjectModelProperty::get_variant_type);
	ClassRegistry::bind_method(D_METHOD("set_variant_type", "variant_type"), &GLTFObjectModelProperty::set_variant_type);
	ClassRegistry::bind_method(D_METHOD("set_types", "variant_type", "obj_model_type"), &GLTFObjectModelProperty::set_types);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "gltf_to_orblit_expression", PROPERTY_HINT_RESOURCE_TYPE, Expression::get_static_class_name()), "set_gltf_to_orblit_expression", "get_gltf_to_orblit_expression"); // Ref<Expression>
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "orblit_to_gltf_expression", PROPERTY_HINT_RESOURCE_TYPE, Expression::get_static_class_name()), "set_orblit_to_gltf_expression", "get_orblit_to_gltf_expression"); // Ref<Expression>
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "node_paths", PROPERTY_HINT_TYPE_STRING, "EntityPath"), "set_node_paths", "get_node_paths"); // TypedArray<EntityPath>
	ADD_PROPERTY(PropertyInfo(Variant::INT, "object_model_type"), "set_object_model_type", "get_object_model_type"); // GLTFObjectModelType
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_STRING_ARRAY, "json_pointers"), "set_json_pointers", "get_json_pointers"); // TypedArray<PackedStringArray>
	ADD_PROPERTY(PropertyInfo(Variant::INT, "variant_type"), "set_variant_type", "get_variant_type"); // Variant::Type
}

void GLTFObjectModelProperty::append_node_path(const EntityPath &p_node_path) {
	node_paths.push_back(p_node_path);
}

void GLTFObjectModelProperty::append_path_to_property(const EntityPath &p_node_path, const StringName &p_prop_name) {
	Vector<StringName> node_names = p_node_path.get_names();
	Vector<StringName> subpath = p_node_path.get_subnames();
	subpath.append(p_prop_name);
	node_paths.push_back(EntityPath(node_names, subpath, false));
}

GLTFAccessor::GLTFAccessorType GLTFObjectModelProperty::get_accessor_type() const {
	switch (object_model_type) {
		case GLTF_OBJECT_MODEL_TYPE_FLOAT2:
			return GLTFAccessor::TYPE_VEC2;
		case GLTF_OBJECT_MODEL_TYPE_FLOAT3:
			return GLTFAccessor::TYPE_VEC3;
		case GLTF_OBJECT_MODEL_TYPE_FLOAT4:
			return GLTFAccessor::TYPE_VEC4;
		case GLTF_OBJECT_MODEL_TYPE_FLOAT2X2:
			return GLTFAccessor::TYPE_MAT2;
		case GLTF_OBJECT_MODEL_TYPE_FLOAT3X3:
			return GLTFAccessor::TYPE_MAT3;
		case GLTF_OBJECT_MODEL_TYPE_FLOAT4X4:
			return GLTFAccessor::TYPE_MAT4;
		default:
			return GLTFAccessor::TYPE_SCALAR;
	}
}

GLTFAccessor::GLTFComponentType GLTFObjectModelProperty::get_component_type(const Vector<Variant> &p_values) const {
	switch (object_model_type) {
		case GLTFObjectModelProperty::GLTF_OBJECT_MODEL_TYPE_BOOL: {
			return GLTFAccessor::COMPONENT_TYPE_UNSIGNED_BYTE;
		} break;
		case GLTFObjectModelProperty::GLTF_OBJECT_MODEL_TYPE_INT: {
			PackedInt64Array int_values;
			for (int i = 0; i < p_values.size(); i++) {
				int_values.append(p_values[i]);
			}
			return GLTFAccessor::get_minimal_integer_component_type_from_ints(int_values);
		} break;
		default: {
			// The base glTF specification only supports 32-bit float accessors for floating point data.
			return GLTFAccessor::COMPONENT_TYPE_SINGLE_FLOAT;
		} break;
	}
}

Ref<Expression> GLTFObjectModelProperty::get_gltf_to_orblit_expression() const {
	return gltf_to_orblit_expr;
}

void GLTFObjectModelProperty::set_gltf_to_orblit_expression(const Ref<Expression> &p_gltf_to_orblit_expr) {
	gltf_to_orblit_expr = p_gltf_to_orblit_expr;
}

Ref<Expression> GLTFObjectModelProperty::get_orblit_to_gltf_expression() const {
	return orblit_to_gltf_expr;
}

void GLTFObjectModelProperty::set_orblit_to_gltf_expression(const Ref<Expression> &p_orblit_to_gltf_expr) {
	orblit_to_gltf_expr = p_orblit_to_gltf_expr;
}

TypedArray<EntityPath> GLTFObjectModelProperty::get_node_paths() const {
	return TypedArray<EntityPath>(node_paths);
}

bool GLTFObjectModelProperty::has_node_paths() const {
	return !node_paths.is_empty();
}

void GLTFObjectModelProperty::set_node_paths(const TypedArray<EntityPath> &p_node_paths) {
	node_paths = TypedArray<EntityPath>(p_node_paths);
}

GLTFObjectModelProperty::GLTFObjectModelType GLTFObjectModelProperty::get_object_model_type() const {
	return object_model_type;
}

void GLTFObjectModelProperty::set_object_model_type(GLTFObjectModelType p_type) {
	object_model_type = p_type;
}

Vector<PackedStringArray> GLTFObjectModelProperty::get_json_pointers() const {
	return json_pointers;
}

bool GLTFObjectModelProperty::has_json_pointers() const {
	return !json_pointers.is_empty();
}

void GLTFObjectModelProperty::set_json_pointers(const Vector<PackedStringArray> &p_json_pointers) {
	json_pointers = p_json_pointers;
}

TypedArray<PackedStringArray> GLTFObjectModelProperty::get_json_pointers_bind() const {
	return GLTFTemplateConvert::to_array(json_pointers);
}

void GLTFObjectModelProperty::set_json_pointers_bind(const TypedArray<PackedStringArray> &p_json_pointers) {
	GLTFTemplateConvert::set_from_array(json_pointers, p_json_pointers);
}

Variant::Type GLTFObjectModelProperty::get_variant_type() const {
	return variant_type;
}

void GLTFObjectModelProperty::set_variant_type(Variant::Type p_variant_type) {
	variant_type = p_variant_type;
}

void GLTFObjectModelProperty::set_types(Variant::Type p_variant_type, GLTFObjectModelType p_obj_model_type) {
	variant_type = p_variant_type;
	object_model_type = p_obj_model_type;
}
