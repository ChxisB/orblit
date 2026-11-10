/**************************************************************************/
/*  openxr_binding_modifier.cpp                                           */

#include "openxr_binding_modifier.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void OpenXRBindingModifier::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_description);
	ORBLIT_VIRTUAL_BIND(_get_ip_modification);
}

String OpenXRBindingModifier::get_description() const {
	String desc;
	if (ORBLIT_VIRTUAL_CALL(_get_description, desc)) {
		return desc;
	}
	return "";
}

PackedByteArray OpenXRBindingModifier::get_ip_modification() {
	PackedByteArray data;
	if (ORBLIT_VIRTUAL_CALL(_get_ip_modification, data)) {
		return data;
	}
	return PackedByteArray();
}
