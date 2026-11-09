/**************************************************************************/
/*  instance_placeholder.h                                                */

#pragma once

#include "scene/stage/entity.h"

class Prefab;

class InstancePlaceholder : public Entity {
	ORBLIT_CLASS(InstancePlaceholder, Entity);

	String path;
	struct PropSet {
		StringName name;
		Variant value;
	};

	List<PropSet> stored_values;

private:
	void set_value_on_instance(InstancePlaceholder *p_placeholder, Entity *p_instance, const PropSet &p_set);
	Entity *try_get_node(InstancePlaceholder *p_placeholder, Entity *p_instance, const EntityPath &p_path);

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	static void register_members();

public:
	void set_instance_path(const String &p_name);
	String get_instance_path() const;

	Dictionary get_stored_values(bool p_with_order = false);

	Entity *create_instance(bool p_replace = false, const Ref<Prefab> &p_custom_scene = Ref<Prefab>());

	InstancePlaceholder();
};
