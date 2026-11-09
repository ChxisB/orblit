/**************************************************************************/
/*  missing_resource.cpp                                                  */

#include "missing_resource.h"

#include "core/object/class_registry.h"

bool MissingResource::_set(const StringName &p_name, const Variant &p_value) {
	if (is_recording_properties()) {
		properties.insert(p_name, p_value);
		return true; //always valid to set (add)
	} else {
		if (!properties.has(p_name)) {
			return false;
		}

		properties[p_name] = p_value;
		return true;
	}
}

bool MissingResource::_get(const StringName &p_name, Variant &r_ret) const {
	if (!properties.has(p_name)) {
		return false;
	}
	r_ret = properties[p_name];
	return true;
}

void MissingResource::_get_property_list(List<PropertyInfo> *p_list) const {
	for (const KeyValue<StringName, Variant> &E : properties) {
		p_list->push_back(PropertyInfo(E.value.get_type(), E.key));
	}
}

void MissingResource::set_original_class(const String &p_class) {
	original_class = p_class;
}

String MissingResource::get_original_class() const {
	return original_class;
}

void MissingResource::set_recording_properties(bool p_enable) {
	recording_properties = p_enable;
}

bool MissingResource::is_recording_properties() const {
	return recording_properties;
}

String MissingResource::get_save_class() const {
	return original_class;
}

void MissingResource::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_original_class", "name"), &MissingResource::set_original_class);
	ClassRegistry::bind_method(D_METHOD("get_original_class"), &MissingResource::get_original_class);

	ClassRegistry::bind_method(D_METHOD("set_recording_properties", "enable"), &MissingResource::set_recording_properties);
	ClassRegistry::bind_method(D_METHOD("is_recording_properties"), &MissingResource::is_recording_properties);

	// Expose, but not save.
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "original_class", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_original_class", "get_original_class");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "recording_properties", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_recording_properties", "is_recording_properties");
}
