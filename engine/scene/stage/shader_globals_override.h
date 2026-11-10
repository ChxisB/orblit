/**************************************************************************/
/*  shader_globals_override.h                                             */

#pragma once

#include "scene/stage/entity.h"

class ShaderGlobalsOverride : public Entity {
	ORBLIT_CLASS(ShaderGlobalsOverride, Entity);

	struct Override {
		bool in_use = false;
		Variant override;
	};

	StringName *_remap(const StringName &p_name) const;

	bool active = false;
	mutable HashMap<StringName, Override> overrides;
	mutable HashMap<StringName, StringName> param_remaps;

	void _activate();

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	void _notification(int p_what);
	static void register_members();

public:
	PackedStringArray get_configuration_warnings() const override;
};
