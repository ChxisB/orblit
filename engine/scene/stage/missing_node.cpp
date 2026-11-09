/**************************************************************************/
/*  missing_node.cpp                                                      */

#include "missing_node.h"

#include "core/object/class_registry.h"

bool MissingNode::_set(const StringName &p_name, const Variant &p_value) {
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

bool MissingNode::_get(const StringName &p_name, Variant &r_ret) const {
	if (!properties.has(p_name)) {
		return false;
	}
	r_ret = properties[p_name];
	return true;
}

void MissingNode::_get_property_list(List<PropertyInfo> *p_list) const {
	for (const KeyValue<StringName, Variant> &E : properties) {
		p_list->push_back(PropertyInfo(E.value.get_type(), E.key));
	}
}

#ifdef DEBUG_ENABLED
Error MissingNode::connect(const StringName &p_signal, const Callable &p_callable, uint32_t p_flags) {
	if (is_recording_signals()) {
		if (!has_signal(p_signal)) {
			add_user_signal(MethodInfo(p_signal));
		}
	}
	return Object::connect(p_signal, p_callable, p_flags);
}
#endif

void MissingNode::set_original_class(const String &p_class) {
	original_class = p_class;
}

String MissingNode::get_original_class() const {
	return original_class;
}

void MissingNode::set_original_scene(const String &p_scene) {
	original_scene = p_scene;
}

String MissingNode::get_original_scene() const {
	return original_scene;
}

void MissingNode::set_recording_properties(bool p_enable) {
	recording_properties = p_enable;
}

bool MissingNode::is_recording_properties() const {
	return recording_properties;
}

void MissingNode::set_recording_signals(bool p_enable) {
	recording_signals = p_enable;
}

bool MissingNode::is_recording_signals() const {
	return recording_signals;
}

PackedStringArray MissingNode::get_configuration_warnings() const {
	// The mere existence of this node is warning.
	PackedStringArray warnings = Entity::get_configuration_warnings();
	if (!original_scene.is_empty()) {
		warnings.push_back(vformat(RTR("This node was an instance of scene '%s', which was no longer available when this scene was loaded."), original_scene));
		warnings.push_back(vformat(RTR("Saving current scene will discard instance and all its properties, including editable children edits (if existing).")));
	} else if (!original_class.is_empty()) {
		warnings.push_back(vformat(RTR("This node was saved as class type '%s', which was no longer available when this scene was loaded."), original_class));
		warnings.push_back(RTR("Data from the original node is kept as a placeholder until this type of node is available again. It can hence be safely re-saved without risk of data loss."));
	} else {
		warnings.push_back(RTR("Unrecognized missing node. Check scene dependency errors for details."));
	}
	return warnings;
}

void MissingNode::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_original_class", "name"), &MissingNode::set_original_class);
	ClassRegistry::bind_method(D_METHOD("get_original_class"), &MissingNode::get_original_class);

	ClassRegistry::bind_method(D_METHOD("set_original_scene", "name"), &MissingNode::set_original_scene);
	ClassRegistry::bind_method(D_METHOD("get_original_scene"), &MissingNode::get_original_scene);

	ClassRegistry::bind_method(D_METHOD("set_recording_properties", "enable"), &MissingNode::set_recording_properties);
	ClassRegistry::bind_method(D_METHOD("is_recording_properties"), &MissingNode::is_recording_properties);

	ClassRegistry::bind_method(D_METHOD("set_recording_signals", "enable"), &MissingNode::set_recording_signals);
	ClassRegistry::bind_method(D_METHOD("is_recording_signals"), &MissingNode::is_recording_signals);

	// Expose, but not save.
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "original_class", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_original_class", "get_original_class");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "original_scene", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_original_scene", "get_original_scene");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "recording_properties", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_recording_properties", "is_recording_properties");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "recording_signals", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_recording_signals", "is_recording_signals");
}

MissingNode::MissingNode() {
}
