/**************************************************************************/
/*  resource_preloader.h                                                  */

#pragma once

#include "scene/stage/entity.h"

class ResourcePreloader : public Entity {
	ORBLIT_CLASS(ResourcePreloader, Entity);

	HashMap<StringName, Ref<Resource>> resources;

	void _set_resources(const Array &p_data);
	Array _get_resources() const;
	Vector<String> _get_resource_list() const;

protected:
	static void register_members();

public:
	void add_resource(const StringName &p_name, const Ref<Resource> &p_resource);
	void remove_resource(const StringName &p_name);
	void rename_resource(const StringName &p_from_name, const StringName &p_to_name);
	bool has_resource(const StringName &p_name) const;
	Ref<Resource> get_resource(const StringName &p_name) const;

	void get_resource_list(List<StringName> *p_list);

	ResourcePreloader();
};
