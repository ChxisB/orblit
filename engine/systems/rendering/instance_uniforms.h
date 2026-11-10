/**************************************************************************/
/*  instance_uniforms.h                                                   */

#pragma once

#include "core/variant/variant.h"
#include "systems/rendering/storage/material_storage.h"

class InstanceUniforms {
public:
	void free(RID p_self);

	void materials_start();
	void materials_append(RID p_material);

	// Assign location() to instance offset if materials_finish returns true.
	bool materials_finish(RID p_self);

	Variant get(const StringName &p_name) const;
	void set(RID p_self, const StringName &p_name, const Variant &p_value);

	Variant get_default(const StringName &p_name) const;
	void get_property_list(List<PropertyInfo> &r_parameters) const;

	inline int32_t location() const { return _location; }
	inline bool is_allocated() const { return _location != -1; }

private:
	struct Item {
		int32_t index = -1;
		int32_t flags = 0;
		Variant value;
		Variant default_value;
		PropertyInfo info;

		inline bool is_valid() const { return index != -1; }
	};
	int32_t _location = -1;
	HashMap<StringName, Item> _parameters;

	void _init_param(Item &r_item, const RendererMaterialStorage::InstanceShaderParam &p_param) const;
	void _invalidate_items();
};
