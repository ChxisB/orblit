/**************************************************************************/
/*  container_type_validate.cpp                                           */

#include "core/variant/container_type_validate.h"

const Variant *ContainerTypeValidate::_internal_convert_variant(const Variant &p_variant, Variant &r_tmp_variant, const char *p_operation, bool p_output_errors) const {
	if (Variant::can_convert_strict(p_variant.get_type(), variant_type)) {
		const Variant *converted_from = &p_variant;
		Callable::CallError call_error;
		Variant::construct(variant_type, r_tmp_variant, &converted_from, 1, call_error);

		if (call_error.error == Callable::CallError::CALL_OK) {
			return &r_tmp_variant;
		}
	}

	if (p_output_errors) {
		ERR_FAIL_V_MSG(nullptr, vformat("Attempted to %s a variable of type '%s' into a %s of incompatible type '%s'.", String(p_operation), Variant::get_type_name(p_variant.get_type()), where, Variant::get_type_name(variant_type)));
	} else {
		return nullptr;
	}
}

bool ContainerTypeValidate::_internal_validate_object(const Variant &p_variant, const char *p_operation, bool p_output_errors) const {
	ERR_FAIL_COND_V(p_variant.get_type() != Variant::OBJECT, false);

#ifdef DEBUG_ENABLED
	ObjectID object_id = p_variant;
	if (object_id == ObjectID()) {
		return true; // This is fine, it's null.
	}
	Object *object = ObjectRegistry::get_instance(object_id);
	if (object == nullptr) {
		if (p_output_errors) {
			ERR_FAIL_V_MSG(false, vformat("Attempted to %s an invalid (previously freed?) object instance into a '%s'.", String(p_operation), String(where)));
		} else {
			return false;
		}
	}
#else
	Object *object = p_variant;
	if (object == nullptr) {
		return true; //fine
	}
#endif
	if (class_name == StringName()) {
		return true; // All good, no class type requested.
	}

	const StringName &obj_class = object->get_class_name();
	if (obj_class != class_name && !object->is_class(class_name)) {
		if (p_output_errors) {
			String object_class_name = object->get_class();
			if (ScriptInstance *script_instance = object->get_script_instance(); script_instance != nullptr) {
				if (Script *other_script = script_instance->get_script(); other_script != nullptr) {
					if (const StringName &script_global_name = other_script->get_global_name(); !script_global_name.is_empty()) {
						object_class_name = script_global_name;
					}
				}
			}
			ERR_FAIL_V_MSG(false, vformat("Attempted to %s an object of type '%s' into a %s of incompatible type '%s'.", String(p_operation), object_class_name, where, String(class_name)));
		} else {
			return false;
		}
	}

	if (script.is_null()) {
		return true; // All good, no script requested.
	}

	Script *other_script = object->get_script_instance() != nullptr ? object->get_script_instance()->get_script() : nullptr;

	// Check base script..
	if (other_script == nullptr) {
		if (p_output_errors) {
			ERR_FAIL_V_MSG(false, vformat("Attempted to %s an object into a %s of incompatible type '%s'.", String(p_operation), String(where), String(script->get_class_name())));
		} else {
			return false;
		}
	}
	if (!other_script->inherits_script(script)) {
		if (p_output_errors) {
			ERR_FAIL_V_MSG(false, vformat("Attempted to %s an object into a %s of incompatible type '%s'.", String(p_operation), String(where), String(script->get_class_name())));
		} else {
			return false;
		}
	}

	return true;
}
