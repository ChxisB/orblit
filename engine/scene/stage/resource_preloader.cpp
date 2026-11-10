/**************************************************************************/
/*  resource_preloader.cpp                                                */

#include "resource_preloader.h"

#include "core/containers/rb_set.h"
#include "core/object/class_registry.h"

void ResourcePreloader::_set_resources(const Array &p_data) {
	resources.clear();

	ERR_FAIL_COND(p_data.size() != 2);
	Vector<String> names = p_data[0];
	Array resdata = p_data[1];

	ERR_FAIL_COND(names.size() != resdata.size());

	for (int i = 0; i < resdata.size(); i++) {
		Ref<Resource> resource = resdata[i];
		ERR_CONTINUE(resource.is_null());
		resources[names[i]] = resource;

		//add_resource(names[i],resource);
	}
}

Array ResourcePreloader::_get_resources() const {
	Vector<String> names;
	Array arr;
	arr.resize(resources.size());
	names.resize(resources.size());

	RBSet<String> sorted_names;

	for (const KeyValue<StringName, Ref<Resource>> &E : resources) {
		sorted_names.insert(E.key);
	}

	int i = 0;
	for (const String &E : sorted_names) {
		names.set(i, E);
		arr[i] = resources[E];
		i++;
	}

	return Array{ names, arr };
}

void ResourcePreloader::add_resource(const StringName &p_name, const Ref<Resource> &p_resource) {
	ERR_FAIL_COND(p_resource.is_null());
	if (resources.has(p_name)) {
		StringName new_name;
		int idx = 2;

		while (true) {
			new_name = p_name.string() + " " + itos(idx);
			if (resources.has(new_name)) {
				idx++;
				continue;
			}

			break;
		}

		add_resource(new_name, p_resource);
	} else {
		resources[p_name] = p_resource;
#ifdef TOOLS_ENABLED
		emit_signal("_resource_changed");
#endif
	}
}

void ResourcePreloader::remove_resource(const StringName &p_name) {
	ERR_FAIL_COND(!resources.has(p_name));
	if (resources.erase(p_name)) {
#ifdef TOOLS_ENABLED
		emit_signal("_resource_changed");
#endif
	}
}

void ResourcePreloader::rename_resource(const StringName &p_from_name, const StringName &p_to_name) {
	ERR_FAIL_COND(!resources.has(p_from_name));

	Ref<Resource> res = resources[p_from_name];

	resources.erase(p_from_name);
	add_resource(p_to_name, res);
}

bool ResourcePreloader::has_resource(const StringName &p_name) const {
	return resources.has(p_name);
}

Ref<Resource> ResourcePreloader::get_resource(const StringName &p_name) const {
	ERR_FAIL_COND_V(!resources.has(p_name), Ref<Resource>());
	return resources[p_name];
}

Vector<String> ResourcePreloader::_get_resource_list() const {
	Vector<String> res;
	res.resize(resources.size());
	int i = 0;
	for (const KeyValue<StringName, Ref<Resource>> &E : resources) {
		res.set(i, E.key);
		i++;
	}

	return res;
}

void ResourcePreloader::get_resource_list(List<StringName> *p_list) {
	for (const KeyValue<StringName, Ref<Resource>> &E : resources) {
		p_list->push_back(E.key);
	}
}

void ResourcePreloader::register_members() {
	ClassRegistry::bind_method(D_METHOD("_set_resources", "resources"), &ResourcePreloader::_set_resources);
	ClassRegistry::bind_method(D_METHOD("_get_resources"), &ResourcePreloader::_get_resources);

	ClassRegistry::bind_method(D_METHOD("add_resource", "name", "resource"), &ResourcePreloader::add_resource);
	ClassRegistry::bind_method(D_METHOD("remove_resource", "name"), &ResourcePreloader::remove_resource);
	ClassRegistry::bind_method(D_METHOD("rename_resource", "name", "newname"), &ResourcePreloader::rename_resource);
	ClassRegistry::bind_method(D_METHOD("has_resource", "name"), &ResourcePreloader::has_resource);
	ClassRegistry::bind_method(D_METHOD("get_resource", "name"), &ResourcePreloader::get_resource);
	ClassRegistry::bind_method(D_METHOD("get_resource_list"), &ResourcePreloader::_get_resource_list);

	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "resources", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR | PROPERTY_USAGE_INTERNAL), "_set_resources", "_get_resources");

#ifdef TOOLS_ENABLED
	ADD_SIGNAL(MethodInfo("_resource_changed"));
#endif
}

ResourcePreloader::ResourcePreloader() {
}
