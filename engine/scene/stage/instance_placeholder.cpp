/**************************************************************************/
/*  instance_placeholder.cpp                                              */

#include "instance_placeholder.h"

#include "core/io/resource_loader.h"
#include "core/object/class_registry.h"
#include "scene/resources/prefab.h"

bool InstancePlaceholder::_set(const StringName &p_name, const Variant &p_value) {
	PropSet ps;
	ps.name = p_name;
	ps.value = p_value;
	stored_values.push_back(ps);
	return true;
}

bool InstancePlaceholder::_get(const StringName &p_name, Variant &r_ret) const {
	for (const PropSet &E : stored_values) {
		if (E.name == p_name) {
			r_ret = E.value;
			return true;
		}
	}
	return false;
}

void InstancePlaceholder::_get_property_list(List<PropertyInfo> *p_list) const {
	for (const PropSet &E : stored_values) {
		PropertyInfo pi;
		pi.name = E.name;
		pi.type = E.value.get_type();
		pi.usage = PROPERTY_USAGE_STORAGE;

		p_list->push_back(pi);
	}
}

void InstancePlaceholder::set_instance_path(const String &p_name) {
	path = p_name;
}

String InstancePlaceholder::get_instance_path() const {
	return path;
}

Entity *InstancePlaceholder::create_instance(bool p_replace, const Ref<Prefab> &p_custom_scene) {
	ERR_FAIL_COND_V(!is_inside_stage(), nullptr);

	Entity *base = get_parent();
	if (!base) {
		return nullptr;
	}

	Ref<Prefab> ps;
	if (p_custom_scene.is_valid()) {
		ps = p_custom_scene;
	} else {
		ps = ResourceLoader::load(path, "Prefab");
	}

	if (ps.is_null()) {
		return nullptr;
	}
	Entity *instance = ps->instantiate();
	if (!instance) {
		return nullptr;
	}
	instance->set_name(get_name());
	instance->set_multiplayer_authority(get_multiplayer_authority());
	int pos = get_index();

	for (const PropSet &E : stored_values) {
		set_value_on_instance(this, instance, E);
	}

	if (p_replace) {
		queue_destroy();
		base->remove_child(this);
	}

	base->add_child(instance);
	base->move_child(instance, pos);

	return instance;
}

// This method will attempt to set the correct values on the placeholder instance
// for regular types this is trivial and unnecessary.
// For nodes however this becomes a bit tricky because they might now have existed until the instantiation,
// so this method will try to find the correct nodes and resolve them.
void InstancePlaceholder::set_value_on_instance(InstancePlaceholder *p_placeholder, Entity *p_instance, const PropSet &p_set) {
	bool is_valid;

	// If we don't have any info, we can't do anything,
	// so try setting the value directly.
	Variant current = p_instance->get(p_set.name, &is_valid);
	if (!is_valid) {
		p_instance->set(p_set.name, p_set.value, &is_valid);
		return;
	}

	Variant::Type current_type = current.get_type();
	Variant::Type placeholder_type = p_set.value.get_type();

	// Arrays are a special case, because their containing type might be different.
	if (current_type != Variant::Type::ARRAY) {
		// Check if the variant types match.
		if (Variant::evaluate(Variant::OP_EQUAL, current_type, placeholder_type)) {
			p_instance->set(p_set.name, p_set.value, &is_valid);
			if (is_valid) {
				return;
			}
			// Types match but setting failed? This is strange, so let's print a warning!
			WARN_PRINT(vformat("Property '%s' with type '%s' could not be set when creating instance of '%s'.", p_set.name, Variant::get_type_name(current_type), p_placeholder->get_name()));
			return;
		}
	} else {
		// We are dealing with an Array.
		// Let's check if the subtype of the array matches first.
		// This is needed because the set method of ScriptInstance checks for type,
		// but the ClassRegistry set method doesn't! So we cannot reliably know what actually happens.
		Array current_array = current;
		Array placeholder_array = p_set.value;
		if (current_array.is_same_typed(placeholder_array)) {
			p_instance->set(p_set.name, p_set.value, &is_valid);
			if (is_valid) {
				return;
			}
			// Internal array types match but setting failed? This is strange, so let's print a warning!
			WARN_PRINT(vformat("Array Property '%s' with type '%s' could not be set when creating instance of '%s'.", p_set.name, Variant::get_type_name(Variant::Type(current_array.get_typed_builtin())), p_placeholder->get_name()));
		}
		// Arrays are not the same internal type. This should be happening because we have a EntityPath Array,
		// but the instance wants a Entity Array.
	}

	switch (current_type) {
		case Variant::Type::NIL: {
			Ref<Resource> resource = p_set.value;
			if (placeholder_type != Variant::Type::ENTITY_PATH && resource.is_null()) {
				break;
			}
			// If it's nil but we have a EntityPath or a Resource, we guess what works.
			p_instance->set(p_set.name, p_set.value, &is_valid);
			if (is_valid) {
				break;
			}

			p_instance->set(p_set.name, try_get_node(p_placeholder, p_instance, p_set.value), &is_valid);
			break;
		}
		case Variant::Type::OBJECT: {
			if (placeholder_type != Variant::Type::ENTITY_PATH) {
				break;
			}
			// Easiest case, we want a node, but we have a deferred EntityPath.
			p_instance->set(p_set.name, try_get_node(p_placeholder, p_instance, p_set.value));
			break;
		}
		case Variant::Type::ARRAY: {
			// If we have reached here it means our array types don't match,
			// so we will convert the placeholder array into the correct type
			// and resolve nodes if necessary.
			Array current_array = current;
			Array converted_array;
			Array placeholder_array = p_set.value;
			converted_array = current_array.duplicate();
			converted_array.resize(placeholder_array.size());

			if (Variant::evaluate(Variant::OP_EQUAL, current_array.get_typed_builtin(), Variant::Type::ENTITY_PATH)) {
				// We want a typed EntityPath array.
				for (int i = 0; i < placeholder_array.size(); i++) {
					converted_array.set(i, placeholder_array[i]);
				}
			} else {
				// We want Nodes, convert EntityPaths.
				for (int i = 0; i < placeholder_array.size(); i++) {
					converted_array.set(i, try_get_node(p_placeholder, p_instance, placeholder_array[i]));
				}
			}

			p_instance->set(p_set.name, converted_array, &is_valid);
			if (!is_valid) {
				WARN_PRINT(vformat("Property '%s' with type '%s' could not be set when creating instance of '%s'.", p_set.name, Variant::get_type_name(current_type), p_placeholder->get_name()));
			}
			break;
		}
		default: {
			WARN_PRINT(vformat("Property '%s' with type '%s' could not be set when creating instance of '%s'.", p_set.name, Variant::get_type_name(current_type), p_placeholder->get_name()));
			break;
		}
	}
}

Entity *InstancePlaceholder::try_get_node(InstancePlaceholder *p_placeholder, Entity *p_instance, const EntityPath &p_path) {
	// First try to resolve internally,
	// if that fails try resolving externally.
	Entity *node = p_instance->get_entity_or_null(p_path);
	if (node == nullptr) {
		node = p_placeholder->get_entity_or_null(p_path);
	}

	return node;
}

Dictionary InstancePlaceholder::get_stored_values(bool p_with_order) {
	Dictionary ret;
	PackedStringArray order;

	for (const PropSet &E : stored_values) {
		ret[E.name] = E.value;
		if (p_with_order) {
			order.push_back(E.name);
		}
	};

	if (p_with_order) {
		ret[".order"] = order;
	}

	return ret;
}

void InstancePlaceholder::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_stored_values", "with_order"), &InstancePlaceholder::get_stored_values, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("create_instance", "replace", "custom_scene"), &InstancePlaceholder::create_instance, DEFVAL(false), DEFVAL(Variant()));
	ClassRegistry::bind_method(D_METHOD("get_instance_path"), &InstancePlaceholder::get_instance_path);
}

InstancePlaceholder::InstancePlaceholder() {
}
