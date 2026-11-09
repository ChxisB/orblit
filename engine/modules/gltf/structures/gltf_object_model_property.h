/**************************************************************************/
/*  gltf_object_model_property.h                                          */

#pragma once

#include "gltf_accessor.h"

#include "core/math/expression.h"
#include "core/variant/typed_array.h"

// Object model: https://github.com/KhronosGroup/glTF/blob/main/specification/2.0/ObjectModel.adoc
// KHR_animation_pointer: https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_animation_pointer

class GLTFObjectModelProperty : public RefCounted {
	ORBLIT_CLASS(GLTFObjectModelProperty, RefCounted);

public:
	enum GLTFObjectModelType {
		GLTF_OBJECT_MODEL_TYPE_UNKNOWN,
		GLTF_OBJECT_MODEL_TYPE_BOOL,
		GLTF_OBJECT_MODEL_TYPE_FLOAT,
		GLTF_OBJECT_MODEL_TYPE_FLOAT_ARRAY,
		GLTF_OBJECT_MODEL_TYPE_FLOAT2,
		GLTF_OBJECT_MODEL_TYPE_FLOAT3,
		GLTF_OBJECT_MODEL_TYPE_FLOAT4,
		GLTF_OBJECT_MODEL_TYPE_FLOAT2X2,
		GLTF_OBJECT_MODEL_TYPE_FLOAT3X3,
		GLTF_OBJECT_MODEL_TYPE_FLOAT4X4,
		GLTF_OBJECT_MODEL_TYPE_INT,
	};

private:
	Ref<Expression> gltf_to_orblit_expr;
	Ref<Expression> orblit_to_gltf_expr;
	TypedArray<EntityPath> node_paths;
	GLTFObjectModelType object_model_type = GLTF_OBJECT_MODEL_TYPE_UNKNOWN;
	Vector<PackedStringArray> json_pointers;
	Variant::Type variant_type = Variant::NIL;

protected:
	static void register_members();

public:
	void append_node_path(const EntityPath &p_node_path);
	void append_path_to_property(const EntityPath &p_node_path, const StringName &p_prop_name);

	GLTFAccessor::GLTFAccessorType get_accessor_type() const;
	GLTFAccessor::GLTFComponentType get_component_type(const Vector<Variant> &p_values) const;

	Ref<Expression> get_gltf_to_orblit_expression() const;
	void set_gltf_to_orblit_expression(const Ref<Expression> &p_gltf_to_orblit_expr);

	Ref<Expression> get_orblit_to_gltf_expression() const;
	void set_orblit_to_gltf_expression(const Ref<Expression> &p_orblit_to_gltf_expr);

	TypedArray<EntityPath> get_node_paths() const;
	bool has_node_paths() const;
	void set_node_paths(const TypedArray<EntityPath> &p_node_paths);

	GLTFObjectModelType get_object_model_type() const;
	void set_object_model_type(GLTFObjectModelType p_type);

	Vector<PackedStringArray> get_json_pointers() const;
	bool has_json_pointers() const;
	void set_json_pointers(const Vector<PackedStringArray> &p_json_pointers);

	TypedArray<PackedStringArray> get_json_pointers_bind() const;
	void set_json_pointers_bind(const TypedArray<PackedStringArray> &p_json_pointers);

	Variant::Type get_variant_type() const;
	void set_variant_type(Variant::Type p_variant_type);

	void set_types(Variant::Type p_variant_type, GLTFObjectModelType p_obj_model_type);
};

VARIANT_ENUM_CAST(GLTFObjectModelProperty::GLTFObjectModelType);
